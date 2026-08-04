/** docs/07-pages.md `/bakim`: kritik bir bağımlılık (BTCPay/SignalR) planlı/plansız kapalıyken gösterilir. */
export default function BakimPage() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-lg font-semibold">Bakımdayız</h1>
      <p className="max-w-sm text-sm text-muted-foreground">
        Sistem kısa bir bakımda. Kısa süre içinde geri döneceğiz — bakiyeniz/ödemeniz etkilenmez.
      </p>
    </div>
  );
}
