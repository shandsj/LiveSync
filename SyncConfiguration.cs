namespace LiveSync
{
    /// <summary>
    /// Represents the configuration for synchronization.
    /// </summary>
    public class SyncConfiguration
    {
        /// <summary>
        /// Gets or sets the cache directory path.
        /// </summary>
        public string CacheDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum number of backups to keep per file.
        /// </summary>
        public int MaxBackups { get; set; } = 5;

        /// <summary>
        /// Gets or sets the collection of synchronization settings.
        /// </summary>
        public IEnumerable<SyncSetting> SyncSettings { get; set; } = Array.Empty<SyncSetting>();
    }
}