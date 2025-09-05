using System;
using System.Collections.Concurrent;
using System.Threading;

namespace VieeExtractor.Server;

class SingleThreadExecutor : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _worker;

    public SingleThreadExecutor()
    {
        _worker = new Thread(Work) { IsBackground = true };
        _worker.Start();
    }

    private void Work()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Task error: {ex}");
            }
        }
    }

    public void Submit(Action action)
    {
        _queue.Add(action);
    }

    public void Shutdown()
    {
        _queue.CompleteAdding();
        _worker.Join();
    }

    public void Dispose()
    {
        Shutdown();
    }
}