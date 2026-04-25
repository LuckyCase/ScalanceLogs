namespace ScalanceLogs.Helpers;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsOwner { get; }

    public SingleInstance(string name)
    {
        _mutex  = new Mutex(true, name, out bool created);
        IsOwner = created;
    }

    public void Dispose() => _mutex.Dispose();
}
