using System.Diagnostics;

namespace PubgOverlay;

public class ScopeTimer : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly string _name;
    private bool _disposed;
    public ScopeTimer(string name)
    {
        _name = name;
        _stopwatch = new Stopwatch();
        _stopwatch.Start();
    }

    // Implement the standard Dispose pattern
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // Protected implementation of Dispose pattern
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Free managed resources
            _stopwatch.Stop();
            Console.WriteLine($"{_name} elapsed: {_stopwatch.ElapsedMilliseconds} ms");
        }

        // Free unmanaged resources (none in this case)
        _disposed = true;
    }

    ~ScopeTimer()
    {
        Dispose(false);
    }
}