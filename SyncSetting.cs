namespace LiveSync
{
    /// <summary>
    /// Represents a synchronization setting.
    /// </summary>
    public class SyncSetting
    {
        /// <summary>
        /// Gets or sets the name of the synchronization setting.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the collection of locations to synchronize.
        /// </summary>
        public IEnumerable<Location> Locations { get; set; } = [];

        /// <summary>
        /// Gets or sets the collection of file extensions to filter.
        /// </summary>
        public IEnumerable<string> FileExtensions { get; set; } = [];
    }
}