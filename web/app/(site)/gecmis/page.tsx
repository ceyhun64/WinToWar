"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getInvoiceHistory } from "@/lib/payments/api";
import type { PaymentInvoiceDto } from "@/lib/payments/types";
import { isSignedIn } from "@/lib/identity";

const STATUS_LABEL: Record<string, string> = {
  Pending: "Bekliyor",
  Confirmed: "Onaylandı",
  Expired: "Süresi Doldu",
  Refunded: "İade Edildi",
  Failed: "Başarısız",
};

/** docs/07-pages.md `/gecmis`: ödeme/maç geçmişi tablosu. */
export default function GecmisPage() {
  const router = useRouter();
  const [invoices, setInvoices] = useState<PaymentInvoiceDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    getInvoiceHistory()
      .then(setInvoices)
      .catch((err) => setError(String(err)));
  }, [router]);

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-8">
      <h1 className="text-lg font-semibold">Geçmiş</h1>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      {invoices === null ? (
        <p className="text-sm text-muted-foreground">Yükleniyor...</p>
      ) : invoices.length === 0 ? (
        <p className="text-sm text-muted-foreground">Henüz bir işleminiz yok.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-left text-xs text-muted-foreground">
                <th className="py-2 font-medium">Tarih</th>
                <th className="py-2 font-medium">Tür</th>
                <th className="py-2 text-right font-medium">Tutar (USD)</th>
                <th className="py-2 text-right font-medium">Durum</th>
              </tr>
            </thead>
            <tbody>
              {invoices.map((invoice) => (
                <tr key={invoice.invoiceId} className="border-b border-border">
                  <td className="py-2 text-xs text-muted-foreground">
                    {new Date(invoice.expiresAt).toLocaleDateString("tr-TR")}
                  </td>
                  <td className="py-2">{invoice.matchId ? "Maça Giriş" : "Bakiye Yükleme"}</td>
                  <td className="py-2 text-right tabular-nums">${invoice.amountUsd}</td>
                  <td className="py-2 text-right">{STATUS_LABEL[invoice.status] ?? invoice.status}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
