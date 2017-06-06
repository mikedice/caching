
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CacheManagerLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CacheManagerLibTests
{
    [TestClass]
    public class StaleSerialize
    {
        [TestMethod]
        public void TestServeStaleSerialize()
        {
            // Set up the mocks
            string testKey = "testKey";
            string refreshserResult = "testValue";
            var fakeCacheData = new Dictionary<string, object>();
            var tracing = new Mock<ITrace>();
            var cache = new Mock<ICache>();
            var refreshserCalls = 0;

            tracing.Setup(m => m.WriteInformational(It.IsAny<string>()))
                .Callback<string>((s) => { System.Diagnostics.Debug.WriteLine(s); });

            cache.Setup(m => m.AddItem(
                It.IsAny<string>(),
                It.IsAny<object>()))
                    .Callback<string, object>((key, value) =>
                    {
                        fakeCacheData[key] = value;
                    });

            cache.Setup(m => m.GetItem(
                It.IsAny<string>()))
                    .Returns<string>((key) => {
                        return fakeCacheData.ContainsKey(key) ? fakeCacheData[key] : null;
                    });

            // Set up the AutoRefreshingCache
            // cache refreshser returns a constant value
            Func<string, Task<object>> cacheRefreshser = (key) =>
            {
                refreshserCalls++;
                return Task.FromResult<object>(refreshserResult);
            };

            var refreshTimeout = TimeSpan.FromMilliseconds(10000);
            var defaultTTL = TimeSpan.FromMilliseconds(500);
            var autoRefreshingCache = new AutoRefreshingCache(
                true, // serve stale
                true, // serialize refresher calls
                refreshTimeout,
                defaultTTL,
                cacheRefreshser,
                cache.Object,
                tracing.Object);

            var testResult = autoRefreshingCache.GetItem(testKey);
            cache.Verify(m => m.AddItem(testKey, It.IsAny<CacheElement>()), Times.Exactly(1), "test item was added to the cache with no TTL");
            cache.Verify(m => m.GetItem(testKey), "testkey was used to access the test value in the cache");
            Assert.AreEqual(refreshserResult, testResult, "was able to retrieve the correct test value");
            Assert.AreEqual(1, refreshserCalls, "Called refreshser 1 time to get the initial value for the cache");
            Assert.AreEqual(typeof(CacheElement), fakeCacheData[testKey].GetType(), "cache holds the right type of cache element for this key");
            Assert.AreEqual(1, fakeCacheData.Count, "fake cache has expected number of entries");

            // items expire after 500ms so make sure item is expired
            Thread.Sleep(1000);
            testResult = autoRefreshingCache.GetItem(testKey);

            // the refresh was queued on the thread pool so this thread
            // has to wait for that to complete before we check that refresher was called
            Thread.Sleep(1000);
            Assert.AreEqual(2, refreshserCalls, "Called refresher again to refresh expired item");
            cache.Verify(m => m.AddItem(testKey, It.IsAny<CacheElement>()), Times.Exactly(2), "test item was added to the cache with no TTL");

            Thread.Sleep(1000); // again wait for item to expire

            // many threads try to retrieve at once
            ManualResetEvent gate = new ManualResetEvent(false);
            var asyncCount = 4;
            Task[] tasks = new Task[4];
            for (var i = 0; i < asyncCount; i++)
            {
                tasks[i] = new Task(() =>
                {
                    gate.WaitOne();
                    autoRefreshingCache.GetItem(testKey);
                });
                tasks[i].Start();
            }

            // release all the waiting threads, they should all call GetItem 
            // at the same time 
            gate.Set();

            // wait for them all to complete
            Task.WaitAll(tasks);

            // Verify that the cacheRefreshser was only called one more time
            Assert.AreEqual(3, refreshserCalls, "cache was only called one more time because multiple threads were serialized");
            cache.Verify(m => m.AddItem(testKey, It.IsAny<CacheElement>()), Times.Exactly(3), "test item was added to the cache with no TTL");

            foreach (var t in tasks) t.Dispose();
            gate.Dispose();
        }
    }
}
