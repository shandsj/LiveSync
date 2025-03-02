using FluentFTP;

namespace LiveSync
{
    /// <summary>
    /// Represents a factory that creates instances of the <see cref="AsyncFtpClient"/> class.
    /// </summary>
    public class FtpClientFactory(ILogger<FtpClientFactory> logger) : IFtpClientFactory
    {
        private readonly Dictionary<string, AsyncFtpClient> clients = [];
        private readonly ILogger<FtpClientFactory> logger = logger;

        public async Task<AsyncFtpClient> CreateFtpClientAsync(Location location, CancellationToken token)
        {
            if (location.Type != LocationType.Ftp)
            {
                throw new ArgumentException("The location must be of type FTP.", nameof(location));
            }

            if (string.IsNullOrWhiteSpace(location.FtpHost))
            {
                throw new ArgumentException("The FTP host must be specified.", nameof(location));
            }

            if (location.FtpPort <= 0)
            {
                throw new ArgumentException("The FTP port must be specified.", nameof(location));
            }

            if (!this.clients.TryGetValue(location.FtpHost, out var client) || !client.IsConnected)
            {
                this.logger.LogInformation("Creating a new FTP client for location {Location}.", location.Path);
                client = new AsyncFtpClient(location.FtpHost,
                    location.Username,
                    location.Password,
                    location.FtpPort,
                    new FtpConfig()
                    {
                        ServerTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                            location.FtpHostTimezone.ToString(),
                            TimeSpan.FromHours(location.FtpHostTimezone),
                            null,
                            null)
                    });

                await client.Connect(token);
                this.clients[location.FtpHost] = client;
            }

            return client;
        }
    }
}