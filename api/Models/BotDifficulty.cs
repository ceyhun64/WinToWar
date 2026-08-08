namespace api.Models;

/// <summary>
/// docs/03-game-rules.md Bölüm 7: bot zorluk profili — maç başına
/// GameConfig.BotDifficulty*Weight dağılımından rastgele seçilir, belirli bir
/// oyuncuya/sıraya göre sabitlenmez.
/// </summary>
public enum BotDifficulty
{
    Easy,
    Normal,
    Hard
}
