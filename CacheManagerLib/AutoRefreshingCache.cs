using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CacheManagerLib
{
    public class AutoRefreshingCache : IAutoRefreshingCache
    {
        private ConcurrentDictionary<string, RefreshserData> refresherData;
        private ICache cache;
        private ITrace trace;

        public AutoRefreshingCache(bool serveStale,
            bool serializeRefresh,
            TimeSpan refreshTimeout,
            TimeSpan defaultTtl,
            Func<string, Task<object>> cacheRefresher,
            ICache cache) : this(serveStale, serializeRefresh, refreshTimeout, defaultTtl, cacheRefresher, cache, new NoTrace())
        {
        }

        public AutoRefreshingCache(bool serveStale,
            bool serializeRefresh,
            TimeSpan refreshTimeout,
            TimeSpan defaultTtl,
            Func<string, Task<object>> cacheRefresher,
            ICache cache,
            ITrace trace)
        {
            this.ServeStale = serveStale;
            this.SerializeRefresh = serializeRefresh;
            this.RefreshTimeout = refreshTimeout;
            this.DefaultTtl = defaultTtl;
            this.CacheRefreshser = cacheRefresher;
            this.refresherData = new ConcurrentDictionary<string, RefreshserData>();
            this.cache = cache;
            this.trace = trace;
        }

        public bool ServeStale { get; private set; }
        public bool SerializeRefresh { get; private set; }
        public TimeSpan RefreshTimeout { get; private set; }
        public TimeSpan DefaultTtl { get; private set; }
        public Func<string, Task<object>> CacheRefreshser { get; private set;  }

        public object GetItem(string key)
        {
            // not in serve stale mode
            // we have item or
            // we don't have item
            //    becaues it is expired and was automatically removed by cache
            //    because it was never in the cache.
            if (!ServeStale)
            {
                // If item is not in the cache we call AddItem and wait for it to return. We wait
                // because we are not in ServeStale mode and we don't have an item to return
                if (cache.GetItem(key)==null) AddItem(key, true, DefaultTtl);
                return cache.GetItem(key);
            }

            // in serve stale mode
            // we have item and it is not stale or
            // we have item and it is stale or
            // we never had the item.
            if (cache.GetItem(key) != null)
            {
                if (IsItemStale(key))
                {
                    // have item but it is stale
                    AddItem(key, false, DefaultTtl); // non blocking refresh because we can return the stale item
                    return AccessStaleItem(key);
                }
            }
            else
            {
                // item was never in the cache so blocking refresh adds it
                AddItem(key, true, DefaultTtl);
            }

            // would return null in the case there was an error adding the item the first time
            return AccessItem(key);
        }

        private void AddItem(string key, bool blockingMode, TimeSpan ttl)
        {
            if (blockingMode)
            {
                BlockingAddItem(key, ttl);
            }
            else
            {
                QueuedAddItem(key, ttl);
            }
        }

        private void BlockingAddItem(string key, TimeSpan ttl)
        {
            if (SerializeRefresh)
            {
                BlockingSerializedAddItem(key, ttl);
            }
            else
            {
                BlockingNotSerializedAddItem(key, ttl);
            }
        }

        private void QueuedAddItem(string key, TimeSpan ttl)
        {
            if (SerializeRefresh)
            {
                QueuedSerializedAddItem(key, ttl);
            }
            else
            {
                QueuedNotSerializedAddItem(key, ttl);
            }
        }

        /// <summary>
        /// Many threads could request the same item at the same time. If many threads make the request for the same
        /// item at the same time they will all call the remote server and all udpate the cache when they are done.
        /// Each thread will block until its refresh is completed.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ttl"></param>
        private void BlockingNotSerializedAddItem(string key, TimeSpan ttl)
        {
            try
            {
                // queue the cache refresher run on the thread pool
                var resultTask = Task.Run(() => CacheRefreshser(key));

                // wait for cache refresher to complete. This call will return true
                // if the task completed before the timeout, false if the task did not
                // complete within the timeout or it will throw an exception if an exception
                // is thrown inside the task while it is running.
                if (resultTask.Wait(RefreshTimeout))
                {
                    CacheItem(key, ttl, resultTask.Result);
                    trace.WriteInformational(
                        $"BlockingNotSerializedAddItem successfully acquired item from CacheRefresher for key {key}");
                }
                else
                {
                    trace.WriteInformational(
                        $"BlockingNotSerializedAddItem timed out trying to acquire item from CacheRefresher for key {key}");
                }
            }
            catch (Exception e)
            {
                trace.WriteInformational(
                            $"BlockingNotSerializedAddItem caught exception trying to acquire item from CacheRefresher for key {key} {e}");
            }
        }

        /// <summary>
        /// Block and wait for the refresh task to complete. If multiple threads call at the same time only one thread will 
        /// be allowed to attempt the refresh at a time. If one of the threads succeeds the other threads will not try to refresh
        /// after that.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ttl"></param>
        private void BlockingSerializedAddItem(string key, TimeSpan ttl)
        {
            if (IsItemValid(key))
            {
                trace.WriteInformational($"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} item for key {key} is already in the cache so no refresh performed");
                return;
            }

            // GetRefreshSemaphoreForKey is guaranteed to return a semaphore using find or create. Each key in the 
            // cache has its own semaphore
            var refreshSempahore = GetRefreshSemaphoreForKey(key);

            if (refreshSempahore.Wait(this.RefreshTimeout))
            {
                // semaphore acquired so everything goes in try/finally from here on out to 
                // guarantee release of the semaphore
                try
                {
                    trace.WriteInformational($"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} semaphore acquired for key {key}");
                    if (IsItemValid(key))
                    {
                        trace.WriteInformational(
                            $"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} semaphore acquired but item is already in the cache for key {key} so no refresh performed");
                        return;
                    }

                    // queue the cache refresher run on the thread pool
                    var resultTask = Task.Run(() => {
                        trace.WriteInformational($"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} calling the CacheRefresher key {key}");

                        return CacheRefreshser(key);
                    });

                    // wait for cache refresher to complete. This call will return true
                    // if the task completed before the timeout, false if the task did not
                    // complete within the timeout or it will throw an exception if an exception
                    // is thrown inside the task while it is running.
                    if (resultTask.Wait(RefreshTimeout))
                    {
                        CacheItem(key, ttl, resultTask.Result);
                        trace.WriteInformational(
                            $"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} successfully acquired item from CacheRefresher for key {key}");
                    }
                    else
                    {
                        trace.WriteInformational(
                            $"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} timed out trying to acquire item from CacheRefresher for key {key}");
                    }
                }
                catch (Exception e)
                {
                    trace.WriteInformational(
                            $"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} caught exception trying to acquire item from CacheRefresher for key {key} {e}");

                }
                finally
                {
                    trace.WriteInformational($"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} releasing semaphore for key {key}");
                    refreshSempahore.Release();
                }
            }
            else
            {
                trace.WriteInformational($"BlockingSerializedAddItem {Thread.CurrentThread.ManagedThreadId} Timeout waiting for refresh semaphore for key {key}");
            }
        }

        /// <summary>
        /// Allow any number of threads to queue an refresh request for the same key. Does not block waiting for result
        /// </summary>
        /// <param name="key">They key of the item to refresh</param>
        /// <param name="ttl">The ttl for the item</param>
        private void QueuedNotSerializedAddItem(string key, TimeSpan ttl)
        {
            Task.Run(() => CacheRefreshser(key)).ContinueWith((t) =>
            {
                object result = null;
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    trace.WriteInformational($"QueueAddItem retrieved item successfully for key {key}");
                    CacheItem(key, ttl, t.Result);
                    result = t.Result;
                }
                else if (t.Exception != null)
                {
                    trace.WriteInformational($"QueueAddItem exception retrieving item for key {key} {t.Exception}");
                }
                return result;
            });
        }

        /// <summary>
        /// Only allow one refresh to be queued at a time for the same key. If a refresh is already in flight
        /// then this function does no work and returns immediately
        /// </summary>
        /// <param name="key">The key of the item to refresh</param>
        /// <param name="ttl">The ttl for the item</param>
        private void QueuedSerializedAddItem(string key, TimeSpan ttl)
        {
            if (IsItemValid(key))
            {
                trace.WriteInformational($"QueuedSerializedAddItem item for key {key} is already in the cache so no refresh performed");
                return;
            }
            // GetRefreshSemaphoreForKey is guaranteed to return a semaphore using find or create. Each key in the 
            // cache has its own semaphore
            var refreshSempahore = GetRefreshSemaphoreForKey(key);

            if (refreshSempahore.Wait(0))
            {
                try
                {
                    Task.Run(() => CacheRefreshser(key)).ContinueWith((t) =>
                    {
                        // The ContinueWith is guaranteed to run if the task got started so release the
                        // semaphore inside the ContinueWith
                        object result = null;
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            trace.WriteInformational($"QueueAddItem retrieved item successfully for key {key}");
                            CacheItem(key, ttl, t.Result);
                            result = t.Result;
                        }
                        else if (t.Exception != null)
                        {
                            trace.WriteInformational($"QueueAddItem exception retrieving item for key {key} {t.Exception}");
                        }
                        refreshSempahore.Release();
                        return result;
                    });
                }
                catch (Exception e)
                {
                    refreshSempahore.Release();
                    trace.WriteInformational(
                            $"QueuedSerializedAddItem caught exception trying to start Task to refresh item from CacheRefresher for key {key} {e}");
                }
            }
            else
            {
                trace.WriteInformational($"QueuedSerializedAddItem did not acquire semaphore for {key} so no work being performed");
            }

        }

        /// <summary>
        /// Check if item is stale or not
        /// </summary>
        /// <param name="key">item key</param>
        /// <returns>Flag indicating staleness</returns>
        private bool IsItemStale(string key)
        {
            if (!ServeStale) return false;
            var element = this.cache.GetItem(key) as CacheElement;
            return element != null ? (DateTime.UtcNow - element.LastAccess > DefaultTtl) : false;
        }

        /// <summary>
        /// Store item in underlying iCache with specified TTL
        /// </summary>
        /// <param name="key">key of item</param>
        /// <param name="ttl">ttl of item</param>
        /// <param name="item">the item</param>
        private void CacheItem(string key, TimeSpan ttl, object item)
        {
            if (!ServeStale)
            {
                this.cache.AddItem(key, ttl, item);
            }
            else
            {
                // In ServeStale mode we have to keep track of the item's lifetime outside the context
                // of the ICache. We can never remove the item from the ICache because if we did we 
                // would not have stale items to serve.
                this.cache.AddItem(key, new CacheElement(item));
            }
        }

        /// <summary>
        /// Return stale tracked item and update last access time
        /// </summary>
        /// <param name="key">key of item</param>
        /// <returns>The item if it is available otherwise null</returns>
        private object AccessItem(string key)
        {
            var item = cache.GetItem(key) as CacheElement;
            object result = null;
            if (item != null)
            {
                item.UpdateLastAccess();
                cache.AddItem(key, DefaultTtl, item);
                result = item.Data;
            }
            return result;
        }

        /// <summary>
        /// Return stale tracked item and do not update last access time
        /// </summary>
        /// <param name="key">key of item</param>
        /// <returns>The item if it is available otherwise null</returns>
        private object AccessStaleItem(string key)
        {
            var item = cache.GetItem(key) as CacheElement;
            return item ?? item.Data;
        }

        private bool IsItemValid(string key)
        {
            if (ServeStale)
            {
                var item = cache.GetItem(key) as CacheElement;
                return item != null && !IsItemStale(key);
            }
            return cache.GetItem(key) != null;
        }

        // TODO mike: In ServeStale=false mode we would need a worker to 
        //            periodically clear out this dictionary
        //            to remove elements that are no longer tracked in the cache.

        SemaphoreSlim GetRefreshSemaphoreForKey(string key)
        {
            var data = this.refresherData.GetOrAdd(key, (k) => new RefreshserData(key));
            return data.RefreshSemaphore;
        }
    }
}
