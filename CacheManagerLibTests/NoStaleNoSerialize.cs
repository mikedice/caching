
using System;
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
            string testValue = null;
            var tracing = new Mock<ITrace>();
            var cache = new Mock<ICache>();

            // The cache.AddItem has to act like a cache and save the value
            // that was passed in. Since the refresher is in ServeStale=false
            // mode cache items will be saved with a TTL.
            cache.Setup(m => m.AddItem(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<object>()))
                    .Callback<string, TimeSpan, object>((k, ttl, d) =>
                    {
                        // set testValue to the passed in value
                        testValue = d as string;
                    });

            // The value of testValue was set in the cache.addItem call
            cache.Setup(m => m.GetItem(
                It.IsAny<string>()))
                    .Returns<string>((k) => {
                        return testValue;
                    });

            // Set up the AutoRefreshingCache
            Func<string, Task<object>> cacheRefreshser = (key) => 
                Task.FromResult<object>(refreshserResult);

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

            var testResult = autoRefreshingCache.GetItem("testKey");
            cache.Verify(m => m.AddItem(testKey, defaultTTL, testValue), "test item was added to the cache");
            cache.Verify(m => m.GetItem(testKey), "testkey was used to access the test value in the cache");
            Assert.AreEqual(testValue, testResult, "was able to retrieve the correct test value");
        }
    }
}
