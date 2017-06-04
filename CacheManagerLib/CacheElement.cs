using System;

namespace CacheManagerLib
{
    /// <summary>
    /// Used to track last access time for an object. The AutoRefreshingCache uses
    /// this when ServeStale is true to determine when an item is stale.
    /// </summary>
    internal class CacheElement
    {
        public CacheElement(object data)
        {
            this.Data = data;
            this.LastAccess = DateTime.UtcNow;
        }

        /// <summary>
        /// THe last access time for the the object
        /// </summary>
        public DateTime LastAccess { get; private set; }

        /// <summary>
        /// The actual object being stored
        /// </summary>
        public object Data { get; private set; }

        /// <summary>
        /// Update the last access time to DateTime.UtcNow
        /// </summary>
        public void UpdateLastAccess()
        {
            this.LastAccess = DateTime.UtcNow;
        }
    }
}
