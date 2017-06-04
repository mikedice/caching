using System;
using System.Threading.Tasks;

namespace CacheManagerLib {

    /// <summary>
    /// This interface provides a system to automatically add or refresh cached items when callers try
    /// to fetch them. The add or refresh operation is accomplished by a caller supplied delegate which
    /// knows the details of how to fetch an item on behalf of the application as an object. The refresh
    /// delegate is an async operation. The IAutoRefreshingCache can decide whether to make a blocking async
    /// call or not based on the ServeStale properties.
    /// 
    /// If ServeStale is true and the item exists in the cache but its TTL is expired then a non-blocking
    /// async refresh can be performed. If ServeStale is true but the item is not in the cache, then
    /// a blocking async call must be performed so the item can be returned to the user when GetItem
    /// is called. If ServeStale is fals and the item is not in the cache, because it never existed
    /// in the cache or it existed but its TTL expired then a blocking async function must be used to 
    /// acquire the item. 
    /// 
    /// Because items in the AutoRefreshingCache are refreshed or updated at the time they are fetched the TTL
    /// per item isn't set in GetItem. The TTL is set to the value of the DefaultTTL property and it is the
    /// same for all items in the cache.
    /// 
    /// The SerializeRefresh property can be used to control the 'thundering herd' problem. This problem occurs
    /// when many threads are requesting the cached item and the item was never in the cache or is expired.
    /// In this case all threads will try to refresh the item using the cache refresher. The cache refresher
    /// may make use of an expensive resource, such as a remote database call, to refresh the cache and in this
    /// case it may be undesirable to let many threads attempt to refresh the same cache key at the same time. To 
    /// control this, the SerializeRefresh property can be used. If set to true then only one thread at a time
    /// will be allowed to attempt to refresh the cache with the cache refresher. If set to false there will be
    /// no serialization of attempts to refresh the cache which could potentially result in many threads trying
    /// to refresh the same cache item at the same time.
    /// </summary>
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
        /// THe default TTL used for items added to the cache
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
        /// an exception or timeout occurs during the blocking refresh null is returned
        /// </summary>
        /// <param name="key">The key of the item to fetch</param>
        /// <returns>The object according to the rules mentioned in the comments</returns>
        object GetItem(string key);
    }
}
