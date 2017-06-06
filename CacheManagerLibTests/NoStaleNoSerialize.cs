
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CacheManagerLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CacheManagerLibTests
{
    [TestClass]
    public class NoStaleNoSerialize
    {
        [TestMethod]
        public void TestNoStaleNoSerialize()
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
                It.IsAny<TimeSpan>(),
                It.IsAny<object>()))
                    .Callback<string, TimeSpan, object>((key, ttl, value) =>
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

            var refreshTimeout = TimeSpan.FromMilliseconds(1000);
            var defaultTTL = TimeSpan.FromMilliseconds(500);
            var autoRefreshingCache = new AutoRefreshingCache(
                false, // don't serve stale
                false, // don't serialize refresher calls
                refreshTimeout,
                defaultTTL,
                cacheRefreshser,
                cache.Object,
                tracing.Object);

            var testResult = autoRefreshingCache.GetItem(testKey);
            cache.Verify(m => m.AddItem(testKey, defaultTTL, refreshserResult), Times.Exactly(1), "test item was added to the cache");
            cache.Verify(m => m.GetItem(testKey), "testkey was used to access the test value in the cache");
            Assert.AreEqual(refreshserResult, testResult, "was able to retrieve the correct test value");
            Assert.AreEqual(1, refreshserCalls, "Called refreshser 1 time to get the initial value for the cache");

            // simulate cache clearing the item after its TTL expires
            // then access item again. Cache refreshser should get called
            // because the item is not in the cache and we don't have a 
            // stale item to serve
            fakeCacheData.Clear();
            testResult = autoRefreshingCache.GetItem(testKey);
            Assert.AreEqual(2, refreshserCalls, "Called refresher again to refresh expired item");
            cache.Verify(m => m.AddItem(testKey, defaultTTL, refreshserResult), Times.Exactly(2), "test item was added to the cache again after it expired");

            // Clear the cache again and then call from multiple threads. Each
            // thread should make a call to the cache refresher
            fakeCacheData.Clear();
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

            // Verify that the cacheRefreshser was called 4 more times
            Assert.AreEqual(6, refreshserCalls, "cache refresher was called four more times");

            foreach (var t in tasks) t.Dispose();
            gate.Dispose();
        }
    }
}
