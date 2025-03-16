using Microsoft.Extensions.Options;

namespace LiveSync
{
    /// <summary>
    /// Represents a service that performs file synchronization between multiple locations.
    /// </summary>
    public class FileSyncService
    {
        private readonly ILogger<FileSyncService> _logger;
        private readonly ILocationServiceFactory _locationServiceFactory;
        private readonly SyncConfiguration _syncConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSyncService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="locationServiceFactory">The location service factory instance.</param>
        /// <param name="syncConfiguration">The synchronization configuration.</param>
        public FileSyncService(ILogger<FileSyncService> logger, ILocationServiceFactory locationServiceFactory, IOptions<SyncConfiguration> syncConfiguration)
        {
            _logger = logger;
            _locationServiceFactory = locationServiceFactory;
            _syncConfiguration = syncConfiguration.Value;
        }

        /// <summary>
        /// Synchronizes files between the specified locations based on hash comparison.
        /// </summary>
        /// <param name="syncSetting">The synchronization setting.</param>
        /// <param name="token">The cancellation token.</param>
        public async Task SyncFilesAsync(SyncSetting syncSetting, CancellationToken token)
        {
            var locationList = syncSetting.Locations.ToList();

            if (locationList.Count < 2)
            {
                throw new ArgumentException("At least two locations are required for synchronization.");
            }

            var cacheSubDirectory = Path.Combine(_syncConfiguration.CacheDirectory, syncSetting.Name);

            foreach (var location in locationList)
            {
                var locationService = _locationServiceFactory.Create(
                    cacheSubDirectory,
                    location,
                    syncSetting.FileExtensions,
                    _syncConfiguration.MaxBackups);

                // Pull the latest files from the location to the cache subdirectory
                await locationService.PullLatestAsync(token);
            }

            foreach (var location in locationList)
            {
                var locationService = _locationServiceFactory.Create(
                    cacheSubDirectory,
                    location,
                    syncSetting.FileExtensions,
                    _syncConfiguration.MaxBackups);

                // Push the latest files from the cache subdirectory to the location
                await locationService.PushLatestAsync(token);
            }
        }
    }
}