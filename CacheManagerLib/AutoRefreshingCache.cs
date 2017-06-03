using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CacheManagerLib 
{
    internal class RefreshserData
    {
        public RefreshserData(string key)
        {
            this.Key = key;
            this.RefreshSemaphore = new SemaphoreSlim(1);
        }

        public string Key { get; private set; }
        public SemaphoreSlim RefreshSemaphore { get; private set; }
    }

    public class AutoRefreshingCache : IAutoRefreshingCache
    {
        private ConcurrentDictionary<string, RefreshserData> refresherData;
        private ITrace trace;

        public AutoRefreshingCache(bool serveStale,
            bool serializeRefresh,
            TimeSpan refreshTimeout,
            TimeSpan defaultTtl,
            Func<string, Task<object>> cacheRefresher,
            ITrace trace)
        {
            this.ServeStale = serveStale;
            this.SerializeRefresh = serializeRefresh;
            this.RefreshTimeout = refreshTimeout;
            this.DefaultTtl = defaultTtl;
            this.CacheRefreshser = cacheRefresher;
            this.refresherData = new ConcurrentDictionary<string, RefreshserData>();
            this.trace = trace;
        }

        public bool ServeStale { get; private set; }
        public bool SerializeRefresh { get; private set; }
        public TimeSpan RefreshTimeout { get; private set; }
        public TimeSpan DefaultTtl { get; private set; }
        public Func<string, Task<object>> CacheRefreshser { get; private set;  }

        public object GetItem(string key)
        {
            throw new NotImplementedException();
        }

        public void AddItem(string key)
        {
            AddItem(key, true, DefaultTtl);
        }

        public void AddItem(string key, TimeSpan ttl)
        {
            AddItem(key, true, ttl);
        }

        public void AddItem(string key, bool blockingMode)
        {
            AddItem(key, blockingMode, DefaultTtl);
        }

        public void AddItem(string key, bool blockingMode, TimeSpan ttl)
        {
            if (blockingMode)
            {
                BlockingAddItem(key, ttl);
            }
            else
            {
                QueueAddItem(key, ttl);
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
                        $"BlockingAddItemImpl successfully acquired item from CacheRefresher for key {key}");
                }
                else
                {
                    trace.WriteInformational(
                        $"BlockingAddItemImpl timed out trying to acquire item from CacheRefresher for key {key}");
                }
            }
            catch (Exception e)
            {
                trace.WriteInformational(
                            $"BlockingAddItemImpl caught exception trying to acquire item from CacheRefresher for key {key} {e}");
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
                trace.WriteInformational($"BlockingSerializedAddItem item for key {key} is already in the cache so no refresh performed");
                return;
            }

            // GetRefreshSemaphoreForKey is guaranteed to return a semaphore using find or create
            var refreshSempahore = GetRefreshSemaphoreForKey(key);

            if (refreshSempahore.Wait(this.RefreshTimeout))
            {
                // semaphore acquired so everything goes in try/finally from here on out to 
                // guarantee release of the semaphore
                try
                {
                    trace.WriteInformational($"BlockingSerializedAddItem semaphore acquired for key {key}");
                    if (IsItemValid(key))
                    {
                        trace.WriteInformational(
                            $"BlockingSerializedAddItem semaphore acquired but item is already in the cache for key {key} so no refresh performed");
                        return;
                    }

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
                            $"BlockingSerializedAddItem successfully acquired item from CacheRefresher for key {key}");
                    }
                    else
                    {
                        trace.WriteInformational(
                            $"BlockingSerializedAddItem timed out trying to acquire item from CacheRefresher for key {key}");
                    }
                }
                catch (Exception e)
                {
                    trace.WriteInformational(
                            $"BlockingSerializedAddItem caught exception trying to acquire item from CacheRefresher for key {key} {e}");

                }
                finally
                {
                    trace.WriteInformational($"BlockingSerializedAddItem releasing semaphore for key {key}");
                }
            }
            else
            {
                trace.WriteInformational($"BlockingSerializedAddItem Timeout waiting for refresh semaphore for key {key}");
            }
        }

        private void QueuedSerializedAddItem(string key, TimeSpan ttl)
        {

        }

        private void QueuedNotSerializedAddItem(string key, TimeSpan ttl)
        {
            Task.Run(() => CacheRefreshser(key)).ContinueWith((t) =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    trace.WriteInformational($"QueueAddItem retrieved item successfully for key {key}");
                    CacheItem(key, ttl, t.Result);
                    return t.Result;
                }
                else if (t.Exception != null)
                {
                    trace.WriteInformational($"QueueAddItem exception retrieving item for key {key}");
                }
                return null;
            });
        }

        private void CacheItem(string key, TimeSpan ttl, object item)
        {

        }

        private bool IsItemValid(string key)
        {
            return false;
        }

        SemaphoreSlim GetRefreshSemaphoreForKey(string key)
        {
            return null;
        }
    }
}
