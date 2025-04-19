namespace LiveSync
{
    /// <summary>
    /// Represents a location to synchronize.
    /// </summary>
    public class Location
    {
        /// <summary>
        /// Gets or sets the path of the location.
        /// </summary>
        public required string Path { get; set; }

        /// <summary>
        /// Gets or sets the type of the location.
        /// </summary>
        public LocationType Type { get; set; }

        /// <summary>
        /// Gets or sets the username for accessing the location.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the password for accessing the location.
        /// </summary>
        public string? Password { get; set; }

        public string? FtpHost { get; set; }
        public int FtpPort { get; set; }
        public int FtpHostTimezone {get;set;}

        /// <summary>
        /// Gets or sets the mappings for renaming file extensions during synchronization for this location.
        /// </summary>
        public IDictionary<string, string> RenameMappings { get; set; } = new Dictionary<string, string>();
    }
}