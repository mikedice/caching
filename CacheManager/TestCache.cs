using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheManagerLib;

namespace CacheManager
{
    public class TestCache : ICache
    {
        Dictionary<string, object> data;
        public TestCache()
        {
            this.data = new Dictionary<string, object>();
        }
        public void AddItem(string key, TimeSpan TTL, object value)
        {
            this.data[key] = value;
        }

        public void AddItem(string key, object value)
        {
            AddItem(key, TimeSpan.MinValue, value);
        }

        public object GetItem(string key)
        {
            return data.ContainsKey(key) ? data[key] : null;
        }
    }
}
