using api.Models.Rooms;
using api.Services.Rooms;
using Xunit;

namespace api.Tests;

/// <summary>docs/08-page-content.md Bölüm 3.4: "Ali'nin Odası" türü oda kimliği — Türkçe iyelik eki üretimi.</summary>
public class RoomDisplayNameFormatterTests
{
    [Theory]
    [InlineData("Ali", "Ali'nin Odası")]
    [InlineData("Mehmet", "Mehmet'in Odası")]
    [InlineData("Can", "Can'ın Odası")]
    [InlineData("Umut", "Umut'un Odası")]
    [InlineData("Gül", "Gül'ün Odası")]
    [InlineData("Ayşe", "Ayşe'nin Odası")]
    [InlineData("Aslı", "Aslı'nın Odası")]
    [InlineData("Doğu", "Doğu'nun Odası")]
    public void Format_Vip_DerivesTurkishGenitiveFromCreatorName(string creatorName, string expected)
    {
        Assert.Equal(expected, RoomDisplayNameFormatter.Format(RoomType.Vip, creatorName));
    }

    [Fact]
    public void Format_Vip_WithoutCreatorName_FallsBackToGenericLabel()
    {
        Assert.Equal("VIP Oda", RoomDisplayNameFormatter.Format(RoomType.Vip, null));
    }

    [Fact]
    public void Format_Standard_UsesFixedLabel_RegardlessOfCreatorName()
    {
        Assert.Equal("Standart Oda", RoomDisplayNameFormatter.Format(RoomType.Standard, "Ali"));
    }
}
