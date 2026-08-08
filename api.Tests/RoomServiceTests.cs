using api.Models.Rooms;
using api.Services;
using api.Services.Rooms;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// docs/09-eksik-tarama-promptu.md denetimi (Faz 8): RoomService'in parola hash/
/// doğrulama ve VIP sınır guard'ları — Stage-1 denetiminde test kapsamı eksikliği
/// olarak bulunmuştu.
/// </summary>
public class RoomServiceTests
{
    private readonly RoomService _sut;

    public RoomServiceTests()
    {
        var mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        var matchManager = new MatchManager(mapProvider, TestEventLog.Writer(), NullLogger<MatchManager>.Instance);
        _sut = new RoomService(matchManager, NullLogger<RoomService>.Instance, Options.Create(new PaymentConfig()));
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var (match, _) = _sut.CreateVipRoom("creator", "Alice", 2, 1, false, 1m, "gizli-parola", DateTime.UtcNow);

        Assert.True(_sut.VerifyPassword(match.Room, "gizli-parola"));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var (match, _) = _sut.CreateVipRoom("creator", "Alice", 2, 1, false, 1m, "gizli-parola", DateTime.UtcNow);

        Assert.False(_sut.VerifyPassword(match.Room, "yanlis-parola"));
    }

    [Fact]
    public void VerifyPassword_NoPasswordSet_AlwaysReturnsTrue()
    {
        var (match, _) = _sut.CreateVipRoom("creator", "Alice", 2, 1, false, 1m, null, DateTime.UtcNow);

        Assert.True(_sut.VerifyPassword(match.Room, "herhangi-bir-sey"));
        Assert.True(_sut.VerifyPassword(match.Room, ""));
    }

    [Fact]
    public void VerifyPassword_HashIsNotPlainText()
    {
        var (match, _) = _sut.CreateVipRoom("creator", "Alice", 2, 1, false, 1m, "gizli-parola", DateTime.UtcNow);

        Assert.NotEqual("gizli-parola", match.Room.RoomPasswordHash);
        Assert.NotNull(match.Room.RoomPasswordHash);
        Assert.Contains(':', match.Room.RoomPasswordHash!); // salt:hash formatı
    }

    [Theory]
    [InlineData(1)]  // GameConfig.VipRoomMinPlayers'ın altında
    [InlineData(13)] // GameConfig.VipRoomMaxPlayers'ın üstünde
    public void CreateVipRoom_MaxPlayersOutOfBounds_Throws(int maxPlayers)
    {
        Assert.Throws<InvalidOperationException>(() =>
            _sut.CreateVipRoom("creator", "Alice", maxPlayers, 1, false, 1m, null, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)] // GameConfig.GreyRegionDefenseMin'in altında
    [InlineData(8)] // GameConfig.GreyRegionDefenseMax'ın üstünde
    public void CreateVipRoom_GreyRegionDefenseCountOutOfBounds_Throws(int greyRegionDefenseCount)
    {
        Assert.Throws<InvalidOperationException>(() =>
            _sut.CreateVipRoom("creator", "Alice", 4, greyRegionDefenseCount, false, 1m, null, DateTime.UtcNow));
    }

    [Fact]
    public void CreateVipRoom_NegativeEntryFee_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _sut.CreateVipRoom("creator", "Alice", 4, 1, false, -1m, null, DateTime.UtcNow));
    }

    /// <summary>docs/07-pages.md ❓ notu: 🛠️ geçici üst sınır — bkz. PaymentConfig.MaxVipEntryFeeUsd.</summary>
    [Fact]
    public void CreateVipRoom_EntryFeeAboveMaxVipEntryFee_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _sut.CreateVipRoom("creator", "Alice", 4, 1, false, 1000m, null, DateTime.UtcNow));
    }

    [Fact]
    public void CreateVipRoom_EntryFeeAtMax_Succeeds()
    {
        var config = new PaymentConfig();
        var (match, _) = _sut.CreateVipRoom("creator", "Alice", 4, 1, false, config.MaxVipEntryFeeUsd, null, DateTime.UtcNow);

        Assert.Equal(config.MaxVipEntryFeeUsd, match.Room.EntryFeeUsd);
    }

    [Fact]
    public void ToRoomSummaryResponse_VipRoomWithCreator_DerivesRoomNameFromCreatorDisplayName()
    {
        var (match, _) = _sut.CreateVipRoom("creator", "Ali", 2, 1, false, 1m, null, DateTime.UtcNow);

        var summary = _sut.ToRoomSummaryResponse(match);

        Assert.Equal("Ali'nin Odası", summary.RoomName);
        Assert.Equal(match.Id, summary.MatchId);
    }
}
