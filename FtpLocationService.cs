using System.Net.Sockets;
using System.Security.Cryptography;
using FluentFTP;

namespace LiveSync
{
    /// <summary>
    /// Represents a service that performs file synchronization for FTP locations.
    /// </summary>
    public class FtpLocationService : ILocationService
    {
        private readonly string _cacheDirectory;
        private readonly Location _location;
        private readonly IEnumerable<string> _fileExtensions;
        private readonly ILogger<FtpLocationService> _logger;
        private readonly AsyncFtpClient _ftpClient;
        private readonly int _maxBackups;

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpLocationService"/> class.
        /// </summary>
        /// <param name="cacheDirectory">The cache directory path.</param>
        /// <param name="location">The location to synchronize.</param>
        /// <param name="fileExtensions">The file extensions to filter.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="maxBackups">The maximum number of backups to keep per file.</param>
        public FtpLocationService(
            string cacheDirectory,
            Location location,
            IEnumerable<string> fileExtensions,
            ILogger<FtpLocationService> logger,
            int maxBackups)
        {
            _cacheDirectory = cacheDirectory;
            _location = location;
            _fileExtensions = fileExtensions;
            _logger = logger;
            _ftpClient = new AsyncFtpClient(
                location.FtpHost,
                location.Username,
                location.Password,
                location.FtpPort);
                // new FtpConfig()
                // {
                //     ServerTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                //         location.FtpHostTimezone.ToString(),
                //         TimeSpan.FromHours(location.FtpHostTimezone),
                //         null,
                //         null),
                // });
            _maxBackups = maxBackups;
        }

        /// <inheritdoc />
        public async Task PullLatestAsync(CancellationToken token)
        {
            try
            {
                await _ftpClient.Connect(token);

                var locationFiles = await GetFtpFilesAsync(_location.Path, token);
                var cacheFiles = GetFiles(_cacheDirectory);

                foreach (var file in locationFiles)
                {
                    var sourceFilePath = $"{_location.Path}/{file}";
                    var destinationFilePath = Path.Combine(_cacheDirectory, file);

                    foreach (var mapping in _location.RenameMappings)
                    {
                        if (sourceFilePath.EndsWith(mapping.Value))
                        {
                            destinationFilePath = Path.ChangeExtension(destinationFilePath, mapping.Key);
                            break;
                        }
                    }

                    var sourceFileTimestamp = (await _ftpClient.GetListing(_location.Path, FtpListOption.Modify, token))
                        .Where(item => item.Type == FtpObjectType.File && item.Name == file)
                        .Select(item => item.RawModified)
                        .FirstOrDefault();
                        
                    var destinationFileTimestamp = File.Exists(destinationFilePath) ? File.GetLastWriteTimeUtc(destinationFilePath) : DateTimeOffset.MinValue;

                    if (sourceFileTimestamp > destinationFileTimestamp)
                    {
                        if (!File.Exists(destinationFilePath) || !await FilesAreEqualAsync(sourceFilePath, destinationFilePath, token))
                        {
                            _logger.LogInformation(
                                "Source file {sourceFilePath} timestamp  {sourceFileTimestamp} and destination file {destinationFilePath} timestamp {destinationFileTimestamp} differ",
                                sourceFilePath,
                                sourceFileTimestamp,
                                destinationFilePath,
                                destinationFileTimestamp);

                            BackupFile(destinationFilePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
                            await _ftpClient.DownloadFile(destinationFilePath, sourceFilePath, FtpLocalExists.Overwrite, token: token);
                            File.SetLastWriteTimeUtc(destinationFilePath, sourceFileTimestamp);
                            _logger.LogInformation("Downloaded file from {Source} to {Destination}", sourceFilePath, destinationFilePath);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException)
            {
                // Ignore connection errors and timeouts, just skip this location.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while pulling the latest files.");
            }
            finally
            {
                await _ftpClient.Disconnect(token);
            }
        }

        /// <inheritdoc />
        public async Task PushLatestAsync(CancellationToken token)
        {
            try
            {
                await _ftpClient.Connect(token);

                var locationFiles = await GetFtpFilesAsync(_location.Path, token);
                var cacheFiles = GetFiles(_cacheDirectory);

                foreach (var file in cacheFiles)
                {
                    var sourceFilePath = Path.Combine(_cacheDirectory, file);
                    var destinationFilePath = $"{_location.Path}/{file}";

                    foreach (var mapping in _location.RenameMappings)
                    {
                        if (destinationFilePath.EndsWith(mapping.Key))
                        {
                            destinationFilePath = Path.ChangeExtension(destinationFilePath, mapping.Value);
                            break;
                        }
                    }

                    var sourceFileTimestamp = File.GetLastWriteTimeUtc(sourceFilePath);
                    var destinationFileTimestamp = (await _ftpClient.GetListing(_location.Path, FtpListOption.Modify, token))
                        .Where(item => item.Type == FtpObjectType.File && item.Name == file)
                        .Select(item => item.RawModified)
                        .FirstOrDefault();                        

                    if (sourceFileTimestamp > destinationFileTimestamp)
                    {
                        if (!await FtpFileExistsAsync(destinationFilePath, token) || !await FilesAreEqualAsync(destinationFilePath, sourceFilePath, token))
                        {
                            _logger.LogInformation(
                                "Source file {sourceFilePath} timestamp  {sourceFileTimestamp} and destination file {destinationFilePath} timestamp {destinationFileTimestamp} differ",
                                sourceFilePath,
                                sourceFileTimestamp,
                                destinationFilePath,
                                destinationFileTimestamp);

                            //BackupFile(destinationFilePath);
                            await _ftpClient.UploadFile(sourceFilePath, destinationFilePath, FtpRemoteExists.Overwrite, token: token);
                            await _ftpClient.SetModifiedTime(destinationFilePath, sourceFileTimestamp, token);
                            _logger.LogInformation(
                                "Uploaded file from {Source} to {Destination}",
                                sourceFilePath,
                                destinationFilePath);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException)
            {
                // Ignore connection errors and timeouts, just skip this location.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while pushing the latest files.");
            }
            finally
            {
                await _ftpClient.Disconnect(token);
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
        /// Gets the list of files in the specified FTP directory that match the file extensions.
        /// </summary>
        /// <param name="directory">The directory to search for files.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The list of files.</returns>
        private async Task<IEnumerable<string>> GetFtpFilesAsync(string directory, CancellationToken token)
        {
            try
            {
                var files = await _ftpClient.GetNameListing(directory, token);
                return files.Where(file => _fileExtensions.Contains(Path.GetExtension(file)))
                            .Select(file => Path.GetRelativePath(directory, file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting files from FTP directory {Directory}", directory);
                return [];
            }
        }

        /// <summary>
        /// Checks if a file exists in the specified FTP path.
        /// </summary>
        /// <param name="filePath">The FTP file path.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the file exists.</returns>
        private async Task<bool> FtpFileExistsAsync(string filePath, CancellationToken token)
        {
            try
            {
                return await _ftpClient.FileExists(filePath, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking if file exists at FTP path {FilePath}", filePath);
                return false;
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting files from directory {Directory}", directory);
                return [];
            }
        }

        /// <summary>
        /// Compares two files to determine if they are equal based on their hash values.
        /// </summary>
        /// <param name="filePath1">The path of the first file.</param>
        /// <param name="filePath2">The path of the second file.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the files are equal.</returns>
        private async Task<bool> FilesAreEqualAsync(string ftpFilePath, string localFilePath, CancellationToken token)
        {
            try
            {
                using var hashAlgorithm = SHA256.Create();

                var hash1 = await ComputeFtpFileHashAsync(ftpFilePath, hashAlgorithm, token);
                var hash2 = await ComputeHashAsync(localFilePath, hashAlgorithm);

                return hash1.SequenceEqual(hash2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while comparing files {FilePath1} and {FilePath2}", ftpFilePath, localFilePath);
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

        /// <summary>
        /// Computes the hash value of a file on the FTP server.
        /// </summary>
        /// <param name="filePath">The FTP file path.</param>
        /// <param name="hashAlgorithm">The hash algorithm to use.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the hash value of the file.</returns>
        private async Task<byte[]> ComputeFtpFileHashAsync(string filePath, HashAlgorithm hashAlgorithm, CancellationToken token)
        {
            try
            {
                using var stream = await _ftpClient.OpenRead(filePath, token: token);
                return await hashAlgorithm.ComputeHashAsync(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while computing hash for FTP file {FilePath}", filePath);
                return [];
            }
        }
    }
}