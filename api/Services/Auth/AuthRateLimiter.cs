using System.Collections.Concurrent;

namespace api.Services.Auth;

public enum RateLimitedAction
{
    Login,
    Register,
    ForgotPassword
}

/// <summary>
/// docs/11-auth.md Bölüm 1.6: IP başına dakika/saat pencereli basit istek sayacı.
/// Ayrı bir NuGet paketi (ör. AspNetCoreRateLimit) eklenmez — YAGNI, tek ihtiyaç
/// birkaç sabit-pencereli sayaçtır (06-coding-standards.md Bağımlılık Disiplini).
/// Thread-safe: <see cref="ConcurrentDictionary{TKey,TValue}"/> + <c>lock</c> ile
/// kritik bölge korunur (02-architecture.md Thread Safety).
/// </summary>
public class AuthRateLimiter
{
    private sealed class Window
    {
        public DateTime WindowStartUtc;
        public int Count;
    }

    private readonly ConcurrentDictionary<(RateLimitedAction, string), Window> _windows = new();
    private readonly TimeProvider _timeProvider;

    public AuthRateLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>True: istek izinli (ve sayaca eklendi). False: limit aşıldı.</summary>
    public bool TryConsume(RateLimitedAction action, string ipAddress, int limit, TimeSpan window)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var key = (action, ipAddress);
        var entry = _windows.GetOrAdd(key, _ => new Window { WindowStartUtc = now, Count = 0 });

        lock (entry)
        {
            if (now - entry.WindowStartUtc >= window)
            {
                entry.WindowStartUtc = now;
                entry.Count = 0;
            }

            if (entry.Count >= limit)
            {
                return false;
            }

            entry.Count += 1;
            return true;
        }
    }
}
