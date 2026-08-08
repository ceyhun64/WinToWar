using api.Models;
using api.Services;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public record AdminMetricsResponse(int PendingWithdrawalCount, int ActiveMatchCount, string DailyVolumeUsd);

/// <summary>docs/07-pages.md `/admin`: özet metrikler.</summary>
[ApiController]
[AdminAuth]
[Route("api/admin/metrics")]
public class AdminMetricsController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly PaymentService _paymentService;
    private readonly MatchManager _matchManager;

    public AdminMetricsController(WalletService walletService, PaymentService paymentService, MatchManager matchManager)
    {
        _walletService = walletService;
        _paymentService = paymentService;
        _matchManager = matchManager;
    }

    [HttpGet]
    public async Task<ActionResult<AdminMetricsResponse>> Get(CancellationToken cancellationToken)
    {
        var pendingWithdrawalCount = await _walletService.CountPendingWithdrawalsAsync(cancellationToken);

        var activeMatchCount = _matchManager.ActiveMatches.Count(m =>
            m.Status is MatchStatus.Lobby or MatchStatus.Countdown or MatchStatus.Playing);

        var dailyVolume = await _paymentService.GetTodayConfirmedVolumeUsdAsync(cancellationToken);

        return Ok(new AdminMetricsResponse(
            pendingWithdrawalCount,
            activeMatchCount,
            dailyVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
    }
}
