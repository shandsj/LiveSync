using FluentFTP;
using System.Security.Cryptography;

namespace LiveSync
{
    /// <summary>
    /// Represents a service that performs file synchronization between multiple locations.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FileSyncService"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance.</param>
    public class FileSyncService(ILogger<FileSyncService> logger)
    {
        private readonly ILogger<FileSyncService> _logger = logger;

        /// <summary>
        /// Synchronizes files between the specified locations based on hash comparison.
        /// </summary>
        /// <param name="locations">The locations to synchronize.</param>
        /// <param name="fileExtensions">The file extensions to filter.</param>
        public async Task SyncFilesAsync(
            IEnumerable<Location> locations,
            IEnumerable<string> fileExtensions,
            CancellationToken token)
        {
            var locationList = locations.ToList();

            if (locationList.Count < 2)
            {
                throw new ArgumentException("At least two locations are required for synchronization.");
            }

            foreach (var location in locationList)
            {
                foreach (var filePath in await GetFilesAsync(location, token))
                {
                    if (fileExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        var fileName = Path.GetFileName(filePath);
                        var fileHashes = new Dictionary<string, (string Hash, DateTime LastModified)>();

                        foreach (var loc in locationList)
                        {
                            var locFilePath = Path.Combine(loc.Path, fileName);
                            if (await FileExistsAsync(loc, locFilePath, token))
                            {
                                try
                                {
                                    var hash = await GetFileHashAsync(loc, locFilePath, token);
                                    var lastModified = await GetFileLastModifiedAsync(loc, locFilePath, token);
                                    fileHashes[loc.Path] = (hash, lastModified);
                                }
                                catch (Exception)
                                {
                                    _logger.LogDebug(
                                        "Could not get file information for location {location} and file {file}",
                                        loc,
                                        locFilePath);
                                }
                            }
                        }

                        if (fileHashes.Values.Select(v => v.Hash).Distinct().Count() > 1)
                        {
                            var mostRecentFile = fileHashes.OrderByDescending(x => x.Value.LastModified).First();
                            var sourceLocation = mostRecentFile.Key;

                            foreach (var loc in locationList.Where(l => l.Path != sourceLocation))
                            {
                                try
                                {
                                    await CopyFileAsync(locationList.First(l => l.Path == sourceLocation), loc, fileName, token);
                                    _logger.LogInformation(
                                        "Synchronized: {fileName} {sourceLocation} --> {loc.Path}",
                                        fileName,
                                        sourceLocation,
                                        loc.Path);
                                }
                                catch (Exception)
                                {
                                    _logger.LogWarning(
                                        "Could not synchronize: {fileName} {sourceLocation} --> {loc.Path}",
                                        fileName,
                                        sourceLocation,
                                        loc.Path);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the list of files in the specified location.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <returns>The list of files.</returns>
        private static async Task<IEnumerable<string>> GetFilesAsync(Location location, CancellationToken token)
        {
            try
            {
                if (location.Type == LocationType.Ftp)
                {
                    return await GetFtpFilesAsync(location, token);
                }
                else
                {
                    return Directory.GetFiles(location.Path);
                }
            }
            catch (Exception)
            {
                return [];
            }
        }

        /// <summary>
        /// Checks if the specified file exists in the location.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>True if the file exists, otherwise false.</returns>
        private static async Task<bool> FileExistsAsync(Location location, string filePath, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            if (location.Type == LocationType.Ftp)
            {
                return await FtpFileExistsAsync(location, filePath, cts.Token);
            }
            else
            {
                return File.Exists(filePath);
            }
        }

        /// <summary>
        /// Computes the hash of the specified file.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>The hash of the file.</returns>
        private static async Task<string> GetFileHashAsync(Location location, string filePath, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            if (location.Type == LocationType.Ftp)
            {
                return await GetFtpFileHashAsync(location, filePath, cts.Token);
            }
            else
            {
                using var stream = File.OpenRead(filePath);
                using var sha256 = SHA256.Create();
                var hash = await sha256.ComputeHashAsync(stream, cts.Token);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Gets the last modified time of the specified file.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>The last modified time of the file.</returns>
        private static async Task<DateTime> GetFileLastModifiedAsync(
            Location location,
            string filePath,
            CancellationToken token)
        {
            if (location.Type == LocationType.Ftp)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                return await GetFtpFileLastModifiedAsync(location, filePath, cts.Token);
            }
            else
            {
                return File.GetLastWriteTime(filePath);
            }
        }

        /// <summary>
        /// Copies a file from the source location to the target location.
        /// </summary>
        /// <param name="sourceLocation">The source location.</param>
        /// <param name="targetLocation">The target location.</param>
        /// <param name="fileName">The file name.</param>
        private async Task CopyFileAsync(Location sourceLocation, Location targetLocation, string fileName, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var sourceFilePath = Path.Combine(sourceLocation.Path, fileName);
            var targetFilePath = Path.Combine(targetLocation.Path, fileName);

            if (sourceLocation.Type == LocationType.Ftp)
            {
                var tempFilePath = Path.GetTempFileName();
                await DownloadFtpFileAsync(sourceLocation, sourceFilePath, tempFilePath, cts.Token);
                sourceFilePath = tempFilePath;
            }

            if (targetLocation.Type == LocationType.Ftp)
            {
                await UploadFtpFileAsync(targetLocation, sourceFilePath, targetFilePath, cts.Token);
            }
            else
            {
                Directory.CreateDirectory(targetLocation.Path);
                File.Copy(sourceFilePath, targetFilePath, true);
            }

            if (sourceLocation.Type == LocationType.Ftp)
            {
                File.Delete(sourceFilePath);
            }
        }

        /// <summary>
        /// Downloads a file from an FTP location.
        /// </summary>
        /// <param name="location">The FTP location.</param>
        /// <param name="sourceFilePath">The source file path.</param>
        /// <param name="targetFilePath">The target file path.</param>
        private static async Task DownloadFtpFileAsync(Location location, string sourceFilePath, string targetFilePath, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            using var client = CreateFtpClient(location);
            await client.Connect(cts.Token);
            await client.DownloadFile(targetFilePath, sourceFilePath, token: cts.Token);
        }

        /// <summary>
        /// Gets the list of files in an FTP location.
        /// </summary>
        /// <param name="location">The FTP location.</param>
        /// <returns>The list of files.</returns>
        private static async Task<IEnumerable<string>> GetFtpFilesAsync(Location location, CancellationToken token)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                using var client = CreateFtpClient(location);
                await client.Connect(cts.Token);
                var items = await client.GetListing(location.Path, cts.Token);
                return [.. items.Where(i => i.Type == FtpObjectType.File).Select(i => i.FullName)];
            }
            catch (Exception)
            {
                return [];
            }
        }

        /// <summary>
        /// Checks if a file exists in an FTP location.
        /// </summary>
        /// <param name="location">The FTP location.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>True if the file exists, otherwise false.</returns>
        private static async Task<bool> FtpFileExistsAsync(Location location, string filePath, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                using var client = CreateFtpClient(location);
                await client.Connect(cts.Token);
                return await client.FileExists(filePath, cts.Token);
            }
            catch (Exception) 
            {
                return false;
            }
        }

        /// <summary>
        /// Computes the hash of a file in an FTP location.
        /// </summary>
        /// <param name="location">The FTP location.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>The hash of the file.</returns>
        private static async Task<string> GetFtpFileHashAsync(Location location, string filePath, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var client = CreateFtpClient(location);
            await client.Connect(cts.Token);
            using var stream = await client.OpenRead(filePath, token: cts.Token);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream, cts.Token);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Gets the last modified time of a file in an FTP location.
        /// </summary>
        /// <param name="location">The FTP location.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>The last modified time of the file.</returns>
        private static async Task<DateTime> GetFtpFileLastModifiedAsync(Location location, string filePath, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var client = CreateFtpClient(location);
            await client.Connect(token);

            var filename = Path.GetFileName(filePath);
            var listing = await client.GetListing(location.Path, FtpListOption.Modify, token);
            return listing
                .Where(i => i.Name == filename)
                .First()
                .Modified;
    }

        /// <summary>
        /// Uploads a file to an FTP location.
        /// </summary>
        /// <param name="location">The FTP location.</param>
        /// <param name="sourceFilePath">The source file path.</param>
        /// <param name="targetFilePath">The target file path.</param>
        private static async Task UploadFtpFileAsync(Location location, string sourceFilePath, string targetFilePath, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            using var client = CreateFtpClient(location);
            await client.Connect(cts.Token);
            await client.UploadFile(sourceFilePath, targetFilePath, FtpRemoteExists.Overwrite, token: cts.Token);        
        }

        private static AsyncFtpClient CreateFtpClient(Location location)
        {
            return new AsyncFtpClient(location.FtpHost,
                location.Username,
                location.Password,
                location.FtpPort,
                new FtpConfig()
                {
                    ServerTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                        "utc+11",
                        TimeSpan.FromHours(location.FtpHostTimezone),
                        null,
                        null)
                });
        }
    }
}