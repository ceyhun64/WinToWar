using api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests.TestSupport;

/// <summary>Testlerde MatchEventLogWriter'ın gerçek DB'ye yazmayan, sadece bounded channel'a buffer'layan halinin tek satırlık kurulumu.</summary>
public static class TestEventLog
{
    public static MatchEventLogWriter Writer() => new(TimeProvider.System, NullLogger<MatchEventLogWriter>.Instance);
}
