using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheManagerLib {
    public class Trace : ITrace
    {
        public void WriteInformational(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}
