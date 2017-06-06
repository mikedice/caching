using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheManagerLib;

namespace CacheManager
{
    class CmsCachedDataManager
    {
        AutoRefreshingCache cache;

        public CmsCachedDataManager(ICache cache)
        {
            this.cache = new AutoRefreshingCache(
                true, // serve stale data if it is available
                true, // serialize refresh,
                TimeSpan.FromMilliseconds(1000), // the time the AutoRefreshing cache should wait for the cache refresher to complete
                TimeSpan.FromMilliseconds(1000), // the ttl for new items added to the cache
                GetItem, // the cache refresher function
                cache // underlying cache system
                );
        }

        /// <summary>
        /// This is the internal cache refresher. Knows how to populate cache
        /// given a key to an item
        /// </summary>
        /// <param name="key">key to item</param>
        /// <returns></returns>
        private async Task<object> GetItem(string key)
        {
            var tuple = SplitKey(key);

            // simulates a request for content that does not exist.
            if (tuple.Item1 != "en-us") return null;

            // return a generic object that represents data fetched and parsed
            // from the a downstream store such as a CMS. This object
            // will be cached by the AutoRefreshingCache using the key specified
            var content = new
            {
                Locale = tuple.Item1,
                Path = tuple.Item2,
                UsePreview = tuple.Item3
            };

            var result = await Task.FromResult<object>(content);
            return result; ;
        }

        /// <summary>
        /// Simulates a public API for getting applicaiton data 
        /// based on a set of app specific parameters. These test parameters
        /// might make sense for accessing data in a CMS.
        /// </summary>
        public object GetData(string locale, string path, bool usePreview)
        {
            var key = MakeKey(locale, path, usePreview);
            return cache.GetItem(key);
        }

        // Private helpers for hydratingand dehydrating keys
        private Tuple<string, string, bool> SplitKey(string key)
        {
            var elems = key.Split(new char[] { ':' });
            return new Tuple<string, string, bool>(elems[0], elems[1], Boolean.Parse(elems[2]));
        }

        private string MakeKey(string locale, string path, bool usePreview)
        {
            if (string.IsNullOrEmpty(locale) || locale.IndexOf(":") >= 0)
            {
                throw new ArgumentException("locale");
            }
            if (string.IsNullOrEmpty(path) || path.IndexOf(":") >= 0)
            {
                throw new ArgumentException("path");
            }
            return $"{locale}:{path}:{usePreview}";
        }
    }
}
