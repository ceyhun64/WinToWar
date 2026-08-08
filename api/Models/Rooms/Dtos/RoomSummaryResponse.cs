namespace api.Models.Rooms.Dtos;

/// <summary>
/// docs/02-architecture.md "Models/Dtos": API'ye giden oda özeti. Önceden
/// RoomsController.cs içinde tanımlıydı (docs/09-eksik-tarama-promptu.md denetimi,
/// Faz 8'de buraya taşındı) — Service katmanının (RoomService) bu DTO'yu üretebilmesi
/// için Controller'a değil Model'e ait olması gerekiyordu (Service asla Controller
/// referansı almaz, bkz. "Katman Bağımlılık Kuralları").
/// </summary>
public record RoomSummaryResponse(
    string MatchId,
    string RoomName,
    int PlayerCount,
    int MaxPlayers,
    string EntryFeeUsd,
    bool FogOfWar,
    int GreyRegionDefenseCount,
    bool IsPasswordProtected);
