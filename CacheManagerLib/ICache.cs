using System;

namespace CacheManagerLib
{
    /// <summary>
    /// ICache is a simple interface to store and retrieve items from a cache.
    /// This interface is intentionally a simple interface that is the least common denominator of 
    /// functionality needed by AutoRefreshingCache that can be easily exposed by popular cache 
    /// implementations like Redis or System.Web.Caching.Cache.
    /// </summary>
    public interface ICache
    {
        /// <summary>
        /// Put item in the cache with specified TTL
        /// </summary>
        /// <param name="key">key for the item</param>
        /// <param name="TTL">Time to live for the item</param>
        /// <param name="value">The item to put in the cache</param>
        void AddItem(string key, TimeSpan TTL, object value);

        /// <summary>
        /// Put item in the cache with no TTL
        /// </summary>
        /// <param name="key">key for the item</param>
        /// <param name="value">The item to put in the cache</param>
        void AddItem(string key, object value);

        /// <summary>
        /// Retrieve item from the cache.
        /// </summary>
        /// <param name="key">Item at key or null if item is not in the cache</param>
        /// <returns>Item from cache at key</returns>
        object GetItem(string key);
    }
}
