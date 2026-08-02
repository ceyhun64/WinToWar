namespace api.Models;

public enum PlayerConnectionStatus
{
    Connected,
    Disconnected
}

/// <summary>
/// Bir maça katılmış oyuncu. Gold, tick bazlı kesirli üretim biriktirebildiği için
/// double olarak tutulur; client'a gönderilirken tam sayıya yuvarlanır.
/// </summary>
public class Player
{
    public required string Id { get; init; }
    public required int Slot { get; init; }
    public required string Name { get; init; }
    public string? ConnectionId { get; set; }
    public PlayerConnectionStatus ConnectionStatus { get; set; } = PlayerConnectionStatus.Connected;
    public double Gold { get; set; }
    public bool IsEliminated { get; set; }
}
