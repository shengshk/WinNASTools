using WinNASTools.Core.Contracts;

namespace WinNASTools.Core.Hosting;

public sealed class FeatureBus : IFeatureBus
{
    private readonly object _gate = new();
    private readonly List<(Type Type, Delegate Handler)> _handlers = new();

    public void Publish<TEvent>(TEvent evt)
    {
        List<Delegate> snapshot;
        lock (_gate)
        {
            snapshot = _handlers
                .Where(h => h.Type == typeof(TEvent))
                .Select(h => h.Handler)
                .ToList();
        }

        foreach (var handler in snapshot)
        {
            try { ((Action<TEvent>)handler)(evt); }
            catch { /* 单个订阅者失败不影响其余 */ }
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        lock (_gate)
            _handlers.Add((typeof(TEvent), handler));

        return new Unsubscriber(() =>
        {
            lock (_gate)
                _handlers.RemoveAll(h => h.Handler.Equals(handler) && h.Type == typeof(TEvent));
        });
    }

    private sealed class Unsubscriber(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
