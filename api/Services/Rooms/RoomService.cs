using System.Security.Cryptography;
using api.Models;
using api.Models.Rooms;
using api.Models.Rooms.Dtos;
using api.Services;
using Microsoft.Extensions.Options;

namespace api.Services.Rooms;

/// <summary>
/// Oda oluşturma/listeleme/şifreli giriş doğrulama (docs/03-game-rules.md Bölüm 2).
/// Bir Room, MatchManager'ın ürettiği bir Match'in ayarlarıdır (Match.Room) — ayrı
/// bir kalıcı depo yoktur, tek doğruluk kaynağı MatchManager'daki aktif maç listesidir.
/// </summary>
public class RoomService
{
    private const int PasswordSaltSize = 16;
    private const int PasswordHashSize = 32;
    private const int PasswordIterations = 100_000;

    private readonly MatchManager _matchManager;
    private readonly ILogger<RoomService> _logger;
    private readonly PaymentConfig _paymentConfig;

    /// <summary>Practice tek paylaşılan kuyruk — kurucusu yoktur (Bölüm 7).</summary>
    private const string PracticeCreatorPlaceholder = "practice-queue";

    public RoomService(MatchManager matchManager, ILogger<RoomService> logger, IOptions<PaymentConfig> paymentConfig)
    {
        _matchManager = matchManager;
        _logger = logger;
        _paymentConfig = paymentConfig.Value;
    }

    /// <summary>Dolmamış açık bir Standart maç varsa onu döndürür, yoksa yeni bir tanesini oluşturur. Rezervasyon yapmaz.</summary>
    public Match FindOrCreateStandardMatch(DateTime now)
        => _matchManager.FindJoinableRoom(RoomType.Standard) ?? CreateStandardMatch(now);

    /// <summary>
    /// Practice: tek paylaşılan otomatik eşleşme kuyruğu (Bölüm 7 — oda listesi değil).
    /// docs/11-auth.md Bölüm 0.4/3.5: forcedPlayerId olarak JWT'den gelen playerId
    /// zorunludur — aksi halde GameHub, Context.UserIdentifier (hesap id'si) ile
    /// bu match-katılımcısının rastgele üretilmiş id'sini eşleştiremez.
    /// </summary>
    public (Match match, Player player) JoinPracticeQueue(string playerId, string playerName, DateTime now)
    {
        var match = _matchManager.FindJoinableRoom(RoomType.Practice) ?? CreatePracticeMatch(now);
        var player = _matchManager.ReservePlayer(match, playerName, now, forcedPlayerId: playerId);
        // Practice'te ödeme akışı hiç tetiklenmez (docs/05-payment.md Bölüm 1.8).
        _matchManager.ConfirmPlayerPayment(match.Id, player.Id, now);
        return (match, player);
    }

    /// <summary>
    /// VIP oda kurma: kurucu odanın 1. slotuna otomatik katılan oyuncu olarak işlenir
    /// (docs/03-game-rules.md Bölüm 2.2) — kurucu burada rezerve edilir ama ödemesi
    /// henüz onaylanmaz; giriş ücreti tahsilatı (Wallet debit/top-up) çağıran tarafta
    /// (RoomsController, RoomEntryService ile) aynı istekte hemen ardından yürütülür.
    /// 🛠️ creatorPlayerId client tarafından üretilip kalıcı saklanan (localStorage)
    /// bir kimliktir (bkz. Wallet.cs) — Room.CreatorPlayerId ile Player.Id aynı olmalı
    /// ki Wallet debit'i doğru kişiden yapılabilsin.
    /// </summary>
    public (Match match, Player creator) CreateVipRoom(
        string creatorPlayerId,
        string creatorName,
        int maxPlayers,
        int greyRegionDefenseCount,
        bool fogOfWar,
        decimal entryFeeUsd,
        string? password,
        DateTime now,
        string? creatorIpAddress = null)
    {
        if (maxPlayers < GameConfig.VipRoomMinPlayers || maxPlayers > GameConfig.VipRoomMaxPlayers)
        {
            throw new InvalidOperationException(
                $"Oyuncu sayısı {GameConfig.VipRoomMinPlayers}-{GameConfig.VipRoomMaxPlayers} arasında olmalıdır.");
        }

        if (greyRegionDefenseCount < GameConfig.GreyRegionDefenseMin || greyRegionDefenseCount > GameConfig.GreyRegionDefenseMax)
        {
            throw new InvalidOperationException(
                $"Gri bölge savunması {GameConfig.GreyRegionDefenseMin}-{GameConfig.GreyRegionDefenseMax} arasında olmalıdır.");
        }

        if (entryFeeUsd < 0)
        {
            throw new InvalidOperationException("Giriş ücreti negatif olamaz.");
        }

        // 🔔🛠️❓ docs/07-pages.md ❓ notu: üst sınır müşteriden netleşene kadar
        // geçici bir değerle korunur — bkz. PaymentConfig.MaxVipEntryFeeUsd gerekçesi.
        if (entryFeeUsd > _paymentConfig.MaxVipEntryFeeUsd)
        {
            throw new InvalidOperationException(
                $"Giriş ücreti en fazla {_paymentConfig.MaxVipEntryFeeUsd} USD olabilir.");
        }

        var room = new Room
        {
            Id = Guid.NewGuid().ToString("N"),
            // 🛠️ docs/03-game-rules.md Bölüm 7 "VIP-tarzı özel Practice odası": Type
            // kasıtlı olarak Vip kalır (aksi halde /lobi VIP sekmesindeki listede hiç
            // görünmez, davet linki/parola mekanizması değişmeden çalışmaya devam
            // etmeli) — EntryFeeUsd=0 olduğunda Practice davranışı Room.IsPractice
            // üzerinden ayrıca sağlanır (bkz. Room.cs).
            Type = RoomType.Vip,
            MaxPlayers = maxPlayers,
            GreyRegionDefenseCount = greyRegionDefenseCount,
            FogOfWar = fogOfWar,
            EntryFeeUsd = entryFeeUsd,
            CreatorPlayerId = creatorPlayerId,
            RoomPasswordHash = string.IsNullOrEmpty(password) ? null : HashPassword(password),
            InviteToken = Guid.NewGuid().ToString("N")
        };

        var match = _matchManager.CreateMatch(room, now);
        var creator = _matchManager.ReservePlayer(match, creatorName, now, forcedPlayerId: creatorPlayerId, joinIpAddress: creatorIpAddress);
        return (match, creator);
    }

    /// <summary>
    /// Herkese açık listede yalnızca şifresiz odalar görünür (Bölüm 2.2 "özel davet").
    /// VIP odalarda kurucunun kendi giriş ücretini henüz ödememiş olabileceği bir ara
    /// durum vardır (bkz. CreateVipRoom) — Room/Match oluşturma ile ödeme aynı istekte
    /// yürür ama gerçek para akışı senkron tamamlanamayabilir (top-up-ve-katıl webhook
    /// beklemesi); bu sırada oda başka oyunculara açık listede görünmez, yalnızca davet
    /// linkiyle bulunabilir olur ki kurucusu hiç ödemeyen bir odaya kimse katılmasın.
    /// </summary>
    public IReadOnlyList<Match> ListOpenRooms(RoomType type)
        => _matchManager.ActiveMatches
            .Where(m => m.Room.Type == type && m.Status == MatchStatus.Lobby && !m.Room.IsPasswordProtected && IsCreatorPaymentConfirmed(m))
            .ToList();

    private static bool IsCreatorPaymentConfirmed(Match match)
    {
        if (match.Room.Type != RoomType.Vip)
        {
            return true;
        }

        var creator = match.Players.FirstOrDefault(p => p.Id == match.Room.CreatorPlayerId);
        return creator is not null && creator.IsPaymentConfirmed;
    }

    public Match? FindByInviteToken(string inviteToken)
        => _matchManager.ActiveMatches.FirstOrDefault(m => m.Room.InviteToken == inviteToken);

    /// <summary>
    /// docs/02-architecture.md "Mapping tek yerde yapılır": RoomsController bu
    /// dönüşümü önceden kendi içinde yapıyordu (docs/09-eksik-tarama-promptu.md
    /// denetimi, Faz 8'de düzeltildi) — oyun motorundaki MatchStateMapper deseniyle
    /// tutarlı olarak Service katmanına taşındı. docs/08-page-content.md Bölüm 3.4:
    /// liste ve davet-token uçlarının ikisi de aynı oda kimliği türetme mantığını
    /// (RoomDisplayNameFormatter) kullanır.
    /// </summary>
    public RoomSummaryResponse ToRoomSummaryResponse(Match match)
    {
        var creatorName = match.Players.FirstOrDefault(p => p.Id == match.Room.CreatorPlayerId)?.Name;
        return new RoomSummaryResponse(
            match.Id,
            RoomDisplayNameFormatter.Format(match.Room.Type, creatorName),
            match.Players.Count,
            match.Room.MaxPlayers,
            match.Room.EntryFeeUsd.ToString(System.Globalization.CultureInfo.InvariantCulture),
            match.Room.FogOfWar,
            match.Room.GreyRegionDefenseCount,
            match.Room.IsPasswordProtected);
    }

    public bool VerifyPassword(Room room, string password)
    {
        if (room.RoomPasswordHash is null)
        {
            return true;
        }

        var parts = room.RoomPasswordHash.Split(':');
        var salt = Convert.FromHexString(parts[0]);
        var expectedHash = Convert.FromHexString(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, PasswordHashSize);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(PasswordSaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, PasswordHashSize);
        return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    private Match CreateStandardMatch(DateTime now)
    {
        var room = new Room
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = RoomType.Standard,
            MaxPlayers = GameConfig.StandardRoomPlayerCount,
            GreyRegionDefenseCount = GameConfig.StandardRoomGreyRegionDefenseCount,
            FogOfWar = GameConfig.StandardRoomFogOfWar,
            EntryFeeUsd = GameConfig.StandardRoomEntryFeeUsd,
            CreatorPlayerId = string.Empty
        };
        return _matchManager.CreateMatch(room, now);
    }

    private Match CreatePracticeMatch(DateTime now)
    {
        var room = new Room
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = RoomType.Practice,
            MaxPlayers = GameConfig.PracticeRoomDefaultPlayerCount,
            GreyRegionDefenseCount = GameConfig.PracticeGreyRegionDefenseCount,
            FogOfWar = GameConfig.PracticeFogOfWar,
            EntryFeeUsd = 0m,
            CreatorPlayerId = PracticeCreatorPlaceholder
        };
        return _matchManager.CreateMatch(room, now);
    }
}
