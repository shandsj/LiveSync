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
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly FileSyncService _fileSyncService;
        private readonly SyncConfiguration _syncConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Worker"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="syncConfiguration">The synchronization configuration.</param>
        /// <param name="fileSyncService">The file synchronization service.</param>
        public Worker(ILogger<Worker> logger, IOptions<SyncConfiguration> syncConfiguration, FileSyncService fileSyncService)
        {
            _logger = logger;
            _syncConfiguration = syncConfiguration.Value;
            _fileSyncService = fileSyncService;
        }

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
                        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                        await _fileSyncService.SyncFilesAsync(syncSetting, cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "An unhandled exception occurred.");
                }

                await Task.Delay(60000, stoppingToken);
            }
        }
    }
}
