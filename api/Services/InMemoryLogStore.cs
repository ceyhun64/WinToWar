using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace api.Services;

public record LogEntry(DateTime TimestampUtc, string Level, string Category, string Message);

/// <summary>
/// docs/07-pages.md `/admin/loglar`: "mevcut log altyapısının admin tarafında
/// filtrelenebilir bir okuma ekranı — yeni bir loglama sistemi kurulmaz (YAGNI)."
/// Bu, tam da o okuma katmanıdır: kalıcı bir log deposu değil, son N ILogger
/// kaydını bellekte tutan bir halka tampon.
/// </summary>
public class InMemoryLogStore
{
    private readonly int _maxEntries;
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public InMemoryLogStore(int maxEntries = 500)
    {
        _maxEntries = maxEntries;
    }

    public void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > _maxEntries && _entries.TryDequeue(out _))
        {
        }
    }

    public List<LogEntry> GetRecent(string? levelFilter, string? searchText)
    {
        IEnumerable<LogEntry> query = _entries;

        if (!string.IsNullOrWhiteSpace(levelFilter))
        {
            query = query.Where(e => string.Equals(e.Level, levelFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(e => e.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(e => e.TimestampUtc).ToList();
    }
}

public class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly InMemoryLogStore _store;

    public InMemoryLoggerProvider(InMemoryLogStore store)
    {
        _store = store;
    }

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _store);

    public void Dispose()
    {
    }
}

public class InMemoryLogger : ILogger
{
    private readonly string _category;
    private readonly InMemoryLogStore _store;

    public InMemoryLogger(string category, InMemoryLogStore store)
    {
        _category = category;
        _store = store;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message += $" | {exception.Message}";
        }

        _store.Add(new LogEntry(DateTime.UtcNow, logLevel.ToString(), _category, message));
    }
}
