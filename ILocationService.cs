namespace LiveSync
{
    /// <summary>
    /// Defines methods for synchronizing files between a source location and a destination location.
    /// </summary>
    public interface ILocationService
    {
        /// <summary>
        /// Pulls the latest files from the source location to the cache directory.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task PullLatestAsync(CancellationToken token);

        /// <summary>
        /// Pushes the latest files from the cache directory to the destination location.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task PushLatestAsync(CancellationToken token);
    }
}