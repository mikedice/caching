namespace CacheManagerLib 
{
    /// <summary>
    /// A simple tracing interface that is used to log messages.
    /// </summary>
    public interface ITrace
    {
        void WriteInformational(string message);
    }
}
