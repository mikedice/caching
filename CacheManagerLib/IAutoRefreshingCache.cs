using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CacheManagerLib {

    public interface IAutoRefreshingCache
    {
        /// <summary>
        /// Get a flag that says whether to serve stale data. True to serve stale data otherwise false
        /// </summary>
        bool ServeStale { get; }

        /// <summary>
        /// Get a property that indicates if refresh attempts will be serialized so that only one call to
        /// the cache refresher will be made at a time for any given key. Setting this value to true mitigates
        /// the 'thundering herd' problem but at the same time will block request processors while refresh attempts
        /// are being made
        /// </summary>
        bool SerializeRefresh { get; }
        
        /// <summary>
        /// Get a flag that indicates the amount of time to wait in a blocking refresh
        /// </summary>
        TimeSpan RefreshTimeout { get; }

        /// <summary>
        /// THe default TTL used for items added to the cache if a TTL is not specified
        /// </summary>
        TimeSpan DefaultTtl { get; }

        /// <summary>
        /// Get an async delegate that is used to refresh the cache item specified by a supplied key
        /// </summary>
        Func<string, Task<object>> CacheRefreshser { get; }

        /// <summary>
        /// Get item if it exists in the cache and is not expired
        /// If item exists but is expired then the value of the ServeStale is consulted. If ServeStale is true
        /// then the existing copy of the item is served and a refresh is queued to attempt to update the item using
        /// the CacheRefresher. If ServeStale is false then a blocking refresh is attempted. If the blocking refresh
        /// succeeds within the time period specified by the RefreshTimeout property then the new item is returned. If
        /// an exception or timeout occurs during the blocking refresh the exception be thrown to the caller.
        /// </summary>
        /// <param name="key">The key of the item to fetch</param>
        /// <returns>The object according to the rules mentioned in the comments</returns>
        object GetItem(string key);

        /// <summary>
        /// Add item using the cache refreshser using blocking mode and the default TTL
        /// </summary>
        /// <param name="key">The key for the item. The CacheRefresher delegate understands which object to fetch based on this key</param>
        void AddItem(string key);

        /// <summary>
        /// Add item using the cache refreshser using blocking mode and the specified TTL
        /// </summary>
        /// <param name="key">The key for the item. The CacheRefresher delegate understands which object to fetch based on this key</param>
        /// <param name="ttl">The time to live for the item</param>
        void AddItem(string key, TimeSpan ttl);

        /// <summary>
        /// Add item using the cache refreshser using default TTL and the specified blocking mode
        /// </summary>
        /// <param name="key">The key for the item. The CacheRefresher delegate understands which object to fetch based on this key</param>
        /// <param name="blockingMode">True if this call blocks and waits for the outcome of the cache refresher run, false to not block</param>
        void AddItem(string key, bool blockingMode);

        /// <summary>
        /// Add item using the cache refreshser using the specified blocking mode and TTL
        /// </summary>
        /// <param name="key">The key for the item. The CacheRefresher delegate understands which object to fetch based on this key</param>
        /// <param name="blockingMode">True if this call blocks and waits for the outcome of the cache refresher run, false to not block</param>
        /// <param name="ttl">The time to live for the item</param>
        void AddItem(string key, bool blockingMode, TimeSpan ttl);
    }
}
