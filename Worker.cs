namespace LiveSync
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a background worker that performs synchronization tasks.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Worker"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance.</param>
    /// <param name="syncConfiguration">The synchronization configuration.</param>
    public class Worker(
        ILogger<Worker> logger,
        IOptions<SyncConfiguration> syncConfiguration,
        FileSyncService fileSyncService) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly FileSyncService fileSyncService = fileSyncService;
        private readonly SyncConfiguration _syncConfiguration = syncConfiguration.Value;

        /// <summary>
        /// Executes the background synchronization task.
        /// </summary>
        /// <param name="stoppingToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                try
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                    foreach (var syncSetting in _syncConfiguration.SyncSettings)
                    {
                        var name = syncSetting.Name;
                        var fileExtensions = syncSetting.FileExtensions;
                        var locations = syncSetting.Locations;
                        
                        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                        await fileSyncService.SyncFilesAsync(locations, fileExtensions, cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    this._logger.LogCritical(ex, "An unhandled exception occurred.");
                }

                await Task.Delay(60000, stoppingToken);
            }
        }
    }
}
