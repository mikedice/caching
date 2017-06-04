
namespace CacheManagerLib {
    public class Trace : ITrace
    {
        public void WriteInformational(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}
