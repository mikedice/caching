using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheManagerLib;

namespace CacheManager {

    class Program
    {
        static void Main(string[] args)
        {
            // Supply your own underlying cache implementation and 
            // underlying tracing implementation
            CmsCachedDataManager dm = new CmsCachedDataManager(
                new TestCache());

            var data1 = dm.GetData("en-us", "/path/to/data", true);
            var data2 = dm.GetData("fr-fr", "path/to/other/data", true);

        }
    }
}
