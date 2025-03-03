namespace LiveSync
{
    /// <summary>
    /// Implements a factory for creating instances of <see cref="ILocationService"/>.
    /// </summary>
    public class LocationServiceFactory : ILocationServiceFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocationServiceFactory"/> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory instance.</param>
        public LocationServiceFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public ILocationService Create(string cacheDirectory, Location location, IEnumerable<string> fileExtensions)
        {
            switch (location.Type)
            {
                case LocationType.Local:
                case LocationType.FileShare:
                    return new LocalLocationService(
                        cacheDirectory,
                        location,
                        fileExtensions,
                        _loggerFactory.CreateLogger<LocalLocationService>());
                case LocationType.Ftp:
                    return new FtpLocationService(
                        cacheDirectory,
                        location,
                        fileExtensions,
                        _loggerFactory.CreateLogger<FtpLocationService>());
                default:
                    throw new ArgumentException($"Unsupported location type: {location.Type}");
            }
        }
    }
}