using System.Security.Cryptography;

namespace LiveSync
{
    /// <summary>
    /// Represents a service that performs file synchronization for local locations.
    /// </summary>
    public class LocalLocationService : ILocationService
    {
        private readonly string _cacheDirectory;
        private readonly Location _location;
        private readonly IEnumerable<string> _fileExtensions;
        private readonly ILogger<LocalLocationService> _logger;
        private readonly int _maxBackups;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalLocationService"/> class.
        /// </summary>
        /// <param name="cacheDirectory">The cache directory path.</param>
        /// <param name="location">The location to synchronize.</param>
        /// <param name="fileExtensions">The file extensions to filter.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="maxBackups">The maximum number of backups to keep per file.</param>
        public LocalLocationService(string cacheDirectory, Location location, IEnumerable<string> fileExtensions, ILogger<LocalLocationService> logger, int maxBackups)
        {
            _cacheDirectory = cacheDirectory;
            _location = location;
            _fileExtensions = fileExtensions;
            _logger = logger;
            _maxBackups = maxBackups;
        }

        /// <inheritdoc />
        public async Task PullLatestAsync(CancellationToken token)
        {
            try
            {
                var locationFiles = GetFiles(_location.Path);
                var cacheFiles = GetFiles(_cacheDirectory);

                foreach (var file in locationFiles)
                {
                    var sourceFilePath = Path.Combine(_location.Path, file);
                    var destinationFilePath = Path.Combine(_cacheDirectory, file);

                    foreach (var mapping in _location.RenameMappings)
                    {
                        if (sourceFilePath.EndsWith(mapping.Value))
                        {
                            destinationFilePath = Path.ChangeExtension(destinationFilePath, mapping.Key);
                            break;
                        }
                    }
                    
                    var sourceFileTimestamp = File.GetLastWriteTimeUtc(sourceFilePath);
                    var destinationFileTimestamp = File.Exists(destinationFilePath) ? File.GetLastWriteTimeUtc(destinationFilePath) : DateTime.MinValue;

                    if (!File.Exists(destinationFilePath) || sourceFileTimestamp > destinationFileTimestamp)
                    {
                        if (!File.Exists(destinationFilePath) || !await FilesAreEqualAsync(sourceFilePath, destinationFilePath))
                        {
                            _logger.LogInformation(
                                "Source file {sourceFilePath} timestamp  {sourceFileTimestamp} and destination file {destinationFilePath} timestamp {destinationFileTimestamp} differ",
                                sourceFilePath,
                                sourceFileTimestamp,
                                destinationFilePath,
                                destinationFileTimestamp);

                            BackupFile(destinationFilePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
                            File.Copy(sourceFilePath, destinationFilePath, true);
                            _logger.LogInformation("Copied file from {Source} to {Destination}", sourceFilePath, destinationFilePath);
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                // Ignore IO Exceptions for remote locations
                if (this._location.Type == LocationType.FileShare)
                {
                    return;
                }

                _logger.LogError(ex, "An error occurred while pulling the latest files.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while pulling the latest files.");
            }
        }

        /// <inheritdoc />
        public async Task PushLatestAsync(CancellationToken token)
        {
            try
            {
                var locationFiles = GetFiles(_location.Path);
                var cacheFiles = GetFiles(_cacheDirectory);

                foreach (var file in cacheFiles)
                {
                    var sourceFilePath = Path.Combine(_cacheDirectory, file);
                    var destinationFilePath = Path.Combine(_location.Path, file);

                    foreach (var mapping in _location.RenameMappings)
                    {
                        if (destinationFilePath.EndsWith(mapping.Key))
                        {
                            destinationFilePath = Path.ChangeExtension(destinationFilePath, mapping.Value);
                            break;
                        }
                    }

                    var sourceFileTimestamp = File.GetLastWriteTimeUtc(sourceFilePath);
                    var destinationFileTimestamp = File.Exists(destinationFilePath) ? File.GetLastWriteTimeUtc(destinationFilePath) : DateTime.MinValue;

                    if (!File.Exists(destinationFilePath) || sourceFileTimestamp > destinationFileTimestamp)
                    {
                        if (!File.Exists(destinationFilePath) || !await FilesAreEqualAsync(sourceFilePath, destinationFilePath))
                        {
                            _logger.LogInformation(
                                "Source file {sourceFilePath} timestamp  {sourceFileTimestamp} and destination file {destinationFilePath} timestamp {destinationFileTimestamp} differ",
                                sourceFilePath,
                                sourceFileTimestamp,
                                destinationFilePath,
                                destinationFileTimestamp);

                            BackupFile(destinationFilePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
                            File.Copy(sourceFilePath, destinationFilePath, true);
                            _logger.LogInformation("Copied file from {Source} to {Destination}", sourceFilePath, destinationFilePath);
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                // Ignore IO Exceptions for remote locations
                if (this._location.Type == LocationType.FileShare)
                {
                    return;
                }

                _logger.LogError(ex, "An error occurred while pushing the latest files.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while pushing the latest files.");
            }
        }

        /// <summary>
        /// Creates a backup of the specified file.
        /// </summary>
        /// <param name="filePath">The path of the file to back up.</param>
        private void BackupFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                var backupFilePath = $"{filePath}.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bak";
                File.Copy(filePath, backupFilePath);
                _logger.LogInformation("Created backup of file {FilePath} at {BackupFilePath}", filePath, backupFilePath);

                // Delete old backups if they exceed the maximum number of backups
                var backupFiles = Directory.GetFiles(Path.GetDirectoryName(filePath), $"{Path.GetFileName(filePath)}.*.bak")
                    .OrderByDescending(f => f)
                    .Skip(_maxBackups)
                    .ToList();

                foreach (var backup in backupFiles)
                {
                    File.Delete(backup);
                    _logger.LogInformation("Deleted old backup file {BackupFilePath}", backup);
                }
            }
        }

        /// <summary>
        /// Gets the list of files in the specified directory that match the file extensions.
        /// </summary>
        /// <param name="directory">The directory to search for files.</param>
        /// <returns>The list of files.</returns>
        private IEnumerable<string> GetFiles(string directory)
        {
            try
            {
                return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Where(file => _fileExtensions.Contains(Path.GetExtension(file)))
                    .Select(file => Path.GetRelativePath(directory, file));
            }
            catch (Exception)
            {
                return [];
            }
        }

        /// <summary>
        /// Compares two files to determine if they are equal based on their hash values.
        /// </summary>
        /// <param name="filePath1">The path of the first file.</param>
        /// <param name="filePath2">The path of the second file.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the files are equal.</returns>
        private async Task<bool> FilesAreEqualAsync(string filePath1, string filePath2)
        {
            try
            {
                using var hashAlgorithm = SHA256.Create();

                var hash1 = await ComputeHashAsync(filePath1, hashAlgorithm);
                var hash2 = await ComputeHashAsync(filePath2, hashAlgorithm);

                return hash1.SequenceEqual(hash2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while comparing files {FilePath1} and {FilePath2}", filePath1, filePath2);
                return false;
            }
        }

        /// <summary>
        /// Computes the hash value of a file.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        /// <param name="hashAlgorithm">The hash algorithm to use.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the hash value of the file.</returns>
        private async Task<byte[]> ComputeHashAsync(string filePath, HashAlgorithm hashAlgorithm)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                return await hashAlgorithm.ComputeHashAsync(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while computing hash for file {FilePath}", filePath);
                return [];
            }
        }
    }
}