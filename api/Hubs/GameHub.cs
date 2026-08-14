using api.Models;
using api.Models.Dtos;
using api.Services;
using api.Services.GameEngine;
using api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace api.Hubs;

/// <summary>
/// Oyuncu bağlantıları, maç yönetimi ve gerçek zamanlı aksiyon mesajları. Sunucu
/// otoriterdir: her aksiyon burada doğrulanır, client'tan gelen veriye güvenilmez.
/// Her state mutasyonu ilgili Match.Lock altında yapılır, ardından güncel durum
/// DTO'ya map'lenip gruba yayınlanır.
///
/// Asker üretimi tamamen otomatik/pasif olduğundan burada bir "asker üret" aksiyonu
/// YOKTUR; General/Upgrade kavramları WinToWar'da yoktur (bkz. docs/03-game-rules.md).
///
/// docs/11-auth.md Bölüm 0.4/3.5: [Authorize] + JWT Bearer, access_token query
/// param'ı ile taşınır (SignalR handshake, bkz. Program.cs). Context.UserIdentifier
/// (JWT sub claim, ClaimTypes.NameIdentifier) tüm hub metotlarında tek PlayerId
/// kaynağıdır — client'tan playerId parametresi olarak asla alınmaz.
/// </summary>
[Authorize]
public class GameHub : Hub
{
    private readonly MatchManager _matchManager;
    private readonly MovementService _movementService;
    private readonly WalletService _walletService;
    private readonly MapProvider _mapProvider;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        MatchManager matchManager,
        MovementService movementService,
        WalletService walletService,
        MapProvider mapProvider,
        ILogger<GameHub> logger)
    {
        _matchManager = matchManager;
        _movementService = movementService;
        _walletService = walletService;
        _mapProvider = mapProvider;
        _logger = logger;
    }

    public async Task JoinMatch(string matchId)
    {
        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            await Clients.Caller.SendAsync("ActionError", "Maç bulunamadı.");
            return;
        }

        var playerId = Context.UserIdentifier!;
        try
        {
            _matchManager.ReconnectPlayer(match, playerId, Context.ConnectionId);
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("ActionError", ex.Message);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
        await BroadcastState(match);
    }

    /// <summary>
    /// docs/05-payment.md Bölüm 1.7: yalnızca Match.Status == Lobby iken izinli —
    /// hem 5 dakikalık süre dolmadan gönüllü erken ayrılma, hem de süre dolduktan
    /// sonra "İptal Et" seçimi aynı aksiyonla karşılanır.
    ///
    /// docs/05-payment.md Bölüm 1.9: bir odaya giriş her zaman Wallet.BalanceUsd
    /// üzerinden yürür (RoomEntryService — doğrudan bakiye düşümü veya top-up-ve-katıl
    /// invoice'ı onaylandıktan sonraki bakiye düşümü, ikisi de aynı kod yoluyla
    /// Room.EntryFeeUsd kadar düşer). Bu yüzden "tam otomatik refund" burada zaten
    /// düşülmüş olan Room.EntryFeeUsd'nin doğrudan Wallet'a geri eklenmesidir — ayrı
    /// bir on-chain Refund/PaymentInvoice akışı gerekmez (o akış yalnızca ödeme daha
    /// hiç bir Match'e/Wallet'a dönüşmeden reddedilen/süresi dolan invoice'lar içindir).
    /// </summary>
    public async Task LeaveLobby()
    {
        if (!_matchManager.TryGetByConnection(Context.ConnectionId, out var matchId, out var playerId) ||
            !_matchManager.TryGetMatch(matchId, out var match))
        {
            await Clients.Caller.SendAsync("ActionError", "Bir maça bağlı değilsiniz.");
            return;
        }

        var entryFeeUsd = match.Room.EntryFeeUsd;
        var isPractice = match.Room.IsPractice;

        var removed = _matchManager.RemovePlayerFromLobby(match, playerId);
        if (!removed)
        {
            await Clients.Caller.SendAsync("ActionError", "Lobiden şu anda ayrılamazsınız.");
            return;
        }

        if (!isPractice && entryFeeUsd > 0)
        {
            try
            {
                await _walletService.CreditAsync(playerId, entryFeeUsd, Context.ConnectionAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LeaveLobby bakiye iadesi başarısız: {MatchId}, {PlayerId}", matchId, playerId);
            }
        }

        await BroadcastState(match);
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 8 "VIP masa manuel başlatma": yalnızca VIP masa
    /// kurucusu, koltuklar dolmadan mevcut oyuncu sayısıyla maçı başlatabilir.
    /// </summary>
    public async Task StartVipMatchNow()
    {
        if (!_matchManager.TryGetByConnection(Context.ConnectionId, out var matchId, out var playerId) ||
            !_matchManager.TryGetMatch(matchId, out var match))
        {
            await Clients.Caller.SendAsync("ActionError", "Bir maça bağlı değilsiniz.");
            return;
        }

        if (!_matchManager.TryForceStartVip(match, playerId, DateTime.UtcNow))
        {
            await Clients.Caller.SendAsync("ActionError", "Maç şimdi başlatılamıyor.");
            return;
        }

        await BroadcastState(match);
    }

    /// <summary>
    /// Bir bölgeden haritadaki başka bir bölgeye saldırı/takviye sevkiyatı başlatır
    /// (docs/03-game-rules.md Bölüm 6, docs/19-army.md). Kaynaktaki o an FİİLEN
    /// kullanılabilir asker sayısının (diğer aktif sevkiyatların rezerve payı hariç)
    /// TAMAMI yeni, bağımsız bir sevkiyata atanır — asker sayısı client'tan gelmez.
    /// Askerler bu sevkiyattan anında değil, gerçek geçen zamana göre kademeli
    /// gruplar halinde ayrılır (docs/19-army.md §8) — bkz. EconomyTickService.Tick →
    /// MovementService.ProcessDispatches; ilk grup burada anında yola çıkar, geri
    /// kalanı sonraki tick'lerde.
    ///
    /// docs/15-asker-hareketi-performans.md Bölüm 6.3: MatchState resync'e ek olarak,
    /// animasyon katmanının anında tepki verebilmesi için ArmyDeparted (ve varsa
    /// ArmyClashed) event'i yayınlanır.
    /// </summary>
    public async Task AttackRegion(string fromRegionId, string toRegionId)
    {
        var outcome = await HandleAction(fromRegionId, (match, player, region, now) =>
            _movementService.StartDispatch(match, player, region, toRegionId, now));

        if (outcome is null)
        {
            return;
        }

        var (match, startResult) = outcome.Value;
        if (startResult.FirstBatch is not null)
        {
            await BroadcastDeparture(match, startResult.FirstBatch);
        }
    }

    /// <summary>
    /// docs/19-army.md: Army grubu yola çıktığında (dispatch başlangıcında veya
    /// EconomyTickService'in tick'i içinde) ArmyDeparted/ArmyClashed event'lerini
    /// yayınlar — DTO mapping'i tek yerde (MatchStateMapper.ToArmyDto).
    /// </summary>
    private async Task BroadcastDeparture(Match match, ArmyDepartureResult departureResult)
    {
        await Clients.Group(match.Id).SendAsync("ArmyDeparted", new ArmyDepartedDto
        {
            Army = MatchStateMapper.ToArmyDto(departureResult.DepartedArmy)
        });

        if (departureResult.Clash is not null)
        {
            var clash = departureResult.Clash;
            await Clients.Group(match.Id).SendAsync("ArmyClashed", new ArmyClashedDto
            {
                FirstArmyId = clash.FirstArmyId,
                SecondArmyId = clash.SecondArmyId,
                WinningArmyId = clash.WinningArmyId,
                SurvivorCount = clash.SurvivorCount,
                ClashAtUtc = clash.ClashAtUtc
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _matchManager.MarkDisconnected(Context.ConnectionId, DateTime.UtcNow);
        if (_matchManager.TryGetByConnection(Context.ConnectionId, out var matchId, out _) &&
            _matchManager.TryGetMatch(matchId, out var match))
        {
            await BroadcastState(match);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Ortak aksiyon iskeleti: oyuncuyu ve kaynak bölgeyi doğrular, spam koruması
    /// (PlayerActionRateLimitPerSecond) uygular, mutasyonu Match.Lock altında
    /// uygular, ardından güncel durumu yayınlar. Hata durumunda sadece isteği yapan
    /// client'a ActionError gönderilir ve null döner.
    ///
    /// Generic dönüş değeri (docs/15-asker-hareketi-performans.md Bölüm 6.3): çağıran,
    /// BroadcastState'ten SONRA aksiyonun sonucuna göre ek event'ler (ör. ArmyDeparted)
    /// yayınlayabilsin diye hem <paramref name="action"/>'ın sonucu hem de ilgili
    /// <see cref="Match"/> geri döner.
    /// </summary>
    private async Task<(Match Match, TResult Result)?> HandleAction<TResult>(
        string regionId, Func<Match, Player, Region, DateTime, TResult> action)
    {
        if (!_matchManager.TryGetByConnection(Context.ConnectionId, out var matchId, out var playerId) ||
            !_matchManager.TryGetMatch(matchId, out var match))
        {
            await Clients.Caller.SendAsync("ActionError", "Bir maça bağlı değilsiniz.");
            return null;
        }

        TResult result;
        try
        {
            lock (match.Lock)
            {
                var player = match.Players.FirstOrDefault(p => p.Id == playerId)
                    ?? throw new InvalidOperationException("Oyuncu bulunamadı.");

                if (match.Status != MatchStatus.Playing)
                {
                    throw new InvalidOperationException("Maç şu anda devam etmiyor.");
                }

                if (player.IsEliminated)
                {
                    throw new InvalidOperationException("Elendiniz, aksiyon alamazsınız.");
                }

                EnforceRateLimit(player, DateTime.UtcNow);

                if (!match.Regions.TryGetValue(regionId, out var region))
                {
                    throw new InvalidOperationException("Bölge bulunamadı.");
                }

                player.LastActionAtUtc = DateTime.UtcNow;
                result = action(match, player, region, DateTime.UtcNow);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation("Aksiyon reddedildi: {Message}", ex.Message);
            await Clients.Caller.SendAsync("ActionError", ex.Message);
            return null;
        }

        await BroadcastState(match);
        return (match, result);
    }

    private static void EnforceRateLimit(Player player, DateTime now)
    {
        if ((now - player.ActionWindowStartUtc).TotalSeconds >= 1)
        {
            player.ActionWindowStartUtc = now;
            player.ActionCountInWindow = 0;
        }

        player.ActionCountInWindow += 1;
        if (player.ActionCountInWindow > GameConfig.PlayerActionRateLimitPerSecond)
        {
            throw new InvalidOperationException("Çok hızlı işlem yapıyorsunuz, birkaç saniye bekleyin.");
        }
    }

    /// <summary>
    /// docs/04-style.md Bölüm 10 "Fog of War": Room.FogOfWar açık bir odada Playing
    /// durumdayken her oyuncuya kendi görüş alanına göre farklı, sunucu tarafında
    /// zaten filtrelenmiş bir DTO gönderilir (tek bir ortak snapshot yayınlanmaz).
    /// Aksi halde (fog kapalı/lobi/sonuç ekranı) tek bir DTO tüm gruba yayınlanır.
    /// </summary>
    private async Task BroadcastState(Match match)
    {
        var applyFog = match.Room.FogOfWar && match.Status == MatchStatus.Playing;
        if (!applyFog)
        {
            MatchStateDto dto;
            lock (match.Lock)
            {
                dto = MatchStateMapper.ToDto(match, DateTime.UtcNow);
            }

            await Clients.Group(match.Id).SendAsync("MatchState", dto);
            return;
        }

        List<(string ConnectionId, MatchStateDto Dto)> perPlayerDtos;
        lock (match.Lock)
        {
            var now = DateTime.UtcNow;
            perPlayerDtos = match.Players
                .Where(p => p.ConnectionId is not null)
                .Select(p => (p.ConnectionId!, MatchStateMapper.ToDto(match, now, _mapProvider, p.Id)))
                .ToList();
        }

        foreach (var (connectionId, dto) in perPlayerDtos)
        {
            await Clients.Client(connectionId).SendAsync("MatchState", dto);
        }
    }
}
