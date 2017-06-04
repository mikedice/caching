using System.Threading;

namespace CacheManagerLib
{
    /// <summary>
    /// Keeps track of a refresh semaphore per key. This allows serialization
    /// of refreshes for a single key or parallel refreshes for different keys
    /// </summary>
    internal class RefreshserData
    {
        public RefreshserData(string key)
        {
            this.Key = key;
            this.RefreshSemaphore = new SemaphoreSlim(1);
        }

        /// <summary>
        /// Get the Key property
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Get the semaphore used to serialize refreshes for the key
        /// </summary>
        public SemaphoreSlim RefreshSemaphore { get; private set; }
    }
}
