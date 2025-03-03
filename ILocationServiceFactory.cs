namespace LiveSync
{
    /// <summary>
    /// Defines a factory for creating instances of <see cref="ILocationService"/>.
    /// </summary>
    public interface ILocationServiceFactory
    {
        /// <summary>
        /// Creates an instance of <see cref="ILocationService"/> based on the specified location.
        /// </summary>
        /// <param name="cacheDirectory">The cache directory path.</param>
        /// <param name="location">The location to synchronize.</param>
        /// <param name="fileExtensions">The file extensions to filter.</param>
        /// <returns>An instance of <see cref="ILocationService"/>.</returns>
        ILocationService Create(string cacheDirectory, Location location, IEnumerable<string> fileExtensions);
    }
}