using api.Models.Payments;
using api.Services.Payments;

namespace api.Tests.TestSupport;

/// <summary>Test amaçlı, çağrı sayısını sayan ve isteğe bağlı gecikme/hata simüle edebilen sağlayıcı.</summary>
public class FakeExchangeRateProvider : IExchangeRateProvider
{
    private int _callCount;

    public PriceOracleSource Source { get; }
    public Func<decimal>? ResultFactory { get; set; }
    public Exception? ThrowException { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public int CallCount => _callCount;

    public FakeExchangeRateProvider(PriceOracleSource source)
    {
        Source = source;
    }

    public async Task<decimal> GetUsdPerLtcAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, CancellationToken.None);
        }

        if (ThrowException is not null)
        {
            throw ThrowException;
        }

        return ResultFactory?.Invoke() ?? 50m;
    }
}
