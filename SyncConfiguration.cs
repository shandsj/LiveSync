namespace LiveSync
{
    /// <summary>
    /// Represents the configuration for synchronization.
    /// </summary>
    public class SyncConfiguration
    {
        /// <summary>
        /// Gets or sets the collection of synchronization settings.
        /// </summary>
        public IEnumerable<SyncSetting> SyncSettings { get; set; } = [];
    }
}