using System.Collections.Concurrent;
using Defra_People_API.Entities;

namespace Defra_People_API.Services.Logger;

public interface ILogQueue
{
    void Enqueue(LogItem item);
    bool TryDequeue(out LogItem item);
    int Count { get; }
}

public class LogQueue : ILogQueue
{
    private readonly ConcurrentQueue<LogItem> _queue = new();
    
    public void Enqueue(LogItem item) => _queue.Enqueue(item);
    
    public bool TryDequeue(out LogItem item) => _queue.TryDequeue(out item);
    
    public int Count => _queue.Count;
}
