namespace SyslogViewer.Helpers;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private bool _released;
    public bool IsOwner { get; }

    public SingleInstance(string name)
    {
        _mutex  = new Mutex(initiallyOwned: true, name, out bool created);
        IsOwner = created;
    }

    public void Dispose()
    {
        if (IsOwner && !_released)
        {
            try { _mutex.ReleaseMutex(); } catch { /* not owned / already released */ }
            _released = true;
        }
        _mutex.Dispose();
    }
}
