using System.Collections.Concurrent;

namespace MessageHub.Core;

internal sealed class InMemoryMessageLogStore : IMessageLogStore
{
    private readonly ConcurrentQueue<MessageLogEntry> _entries = new();

    public Task AddAsync(MessageLogEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(entry);

        while (_entries.Count > 500)
        {
            _entries.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var items = _entries
            .Reverse()
            .Take(Math.Clamp(count, 1, 200))
            .ToArray();

        return Task.FromResult<IReadOnlyList<MessageLogEntry>>(items);
    }
}
