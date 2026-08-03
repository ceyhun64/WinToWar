namespace api.Models.Payments;

/// <summary>
/// Bölüm 9 (Reconciliation job): DB-tablosu tabanlı distributed lock. Tek satırlık
/// bir "kilit" satırıdır; birden fazla API instance'ı çalışsa bile aynı anda
/// yalnızca biri reconciliation taramasını yürütür. ExpiresAt geçmişse kilit
/// serbest kabul edilir (crash-safe — sonsuza kadar kilitli kalmaz).
/// </summary>
public class ReconciliationLock
{
    public required string LockName { get; set; }
    public required string HolderId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
