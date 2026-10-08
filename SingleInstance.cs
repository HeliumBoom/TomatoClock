using System.Diagnostics;
using System.Security.Principal;

namespace TomatoClock;

/// <summary>One instance per user/session; the event retains early activation requests.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activation;
    private RegisteredWaitHandle? listener;
    private bool disposed;
    public bool IsPrimary { get; }

    public SingleInstance(string? name = null)
    {
        name ??= $"TomatoClock.{WindowsIdentity.GetCurrent().User!.Value}.{Process.GetCurrentProcess().SessionId}";
        // Create the signal before taking ownership, so even a concurrent second
        // launch can leave a request before the primary creates its main window.
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + name + ".Activate");
        mutex = new Mutex(false, @"Local\" + name + ".Mutex");
        try { IsPrimary = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }

    public void RequestActivation() => activation.Set();

    public void Listen(Action activated)
    {
        if (!IsPrimary) throw new InvalidOperationException("Only the primary instance can listen.");
        listener ??= ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => activated(), null, Timeout.Infinite, false);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        listener?.Unregister(null);
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose(); activation.Dispose();
    }
}
