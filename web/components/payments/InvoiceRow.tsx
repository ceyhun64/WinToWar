import { ArrowDownToLine, Swords } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import type { PaymentInvoiceDto } from "@/lib/payments/types";

/** `/cuzdan` (Son İşlemler) ve `/gecmis` (tam liste) aynı satır görünümünü paylaşır — tek kaynak, kod tekrarı yok. */
export const INVOICE_STATUS_LABEL: Record<string, string> = {
  Pending: "Bekliyor",
  Confirmed: "Onaylandı",
  Expired: "Süresi Doldu",
  Refunded: "İade Edildi",
  Failed: "Başarısız",
};

export const INVOICE_STATUS_VARIANT: Record<string, "default" | "secondary" | "destructive" | "outline"> = {
  Pending: "outline",
  Confirmed: "default",
  Expired: "destructive",
  Refunded: "secondary",
  Failed: "destructive",
};

export function InvoiceRow({ invoice }: { invoice: PaymentInvoiceDto }) {
  const Icon = invoice.matchId ? Swords : ArrowDownToLine;
  const accent = invoice.matchId ? "#F5B942" : "#38BDF8";

  return (
    /* docs/24-responsive-small-screens.md Bölüm 5: satırın iki yanında da
       `min-w-0` yoktu — flex çocuklarının varsayılan `min-width: auto` değeri
       daralmayı engellediği için satır, kart genişliğini aşıyordu. Kart
       `overflow-hidden` olduğundan taşan durum rozeti sessizce kırpılıyordu
       (320px'te kart içi alan ~240px, satırın istediği ~380px).

       Daralma payı bilinçli olarak SOL tarafa verildi: tutar ve durum rozeti
       kısaltılırsa bilgi okunamaz hale gelir, tarih/tip metni ise ellipsis ile
       hâlâ anlaşılır. 390px ve üzerinde satır zaten sığdığı için truncate hiç
       devreye girmez, görünüm değişmez. */
    <div className="flex items-center justify-between gap-3 rounded-xl px-2 py-2 text-sm">
      <span className="flex min-w-0 items-center gap-2.5 text-muted-foreground">
        <span
          className="flex size-8 shrink-0 items-center justify-center rounded-lg"
          style={{ backgroundColor: `${accent}22`, color: accent }}
        >
          <Icon className="size-4" aria-hidden="true" />
        </span>
        <span className="truncate">
          {new Date(invoice.createdAt).toLocaleDateString("tr-TR")} · {invoice.matchId ? "Maça Giriş" : "Para Yatırma"}
        </span>
      </span>
      <span className="flex shrink-0 items-center gap-3 tabular-nums">
        ${invoice.amountUsd}
        <Badge variant={INVOICE_STATUS_VARIANT[invoice.status] ?? "outline"}>
          {INVOICE_STATUS_LABEL[invoice.status] ?? invoice.status}
        </Badge>
      </span>
    </div>
  );
}
