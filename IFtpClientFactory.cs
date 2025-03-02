using FluentFTP;

namespace LiveSync
{
    /// <summary>
    /// Represents a factory that creates instances of the <see cref="AsyncFtpClient"/> class.
    /// </summary>
    public interface IFtpClientFactory
    {
        /// <summary>
        /// Creates a new instance of the <see cref="AsyncFtpClient"/> class.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The created <see cref="AsyncFtpClient"/> instance.</returns>
        Task<AsyncFtpClient> CreateFtpClientAsync(Location location, CancellationToken token);
    }
}