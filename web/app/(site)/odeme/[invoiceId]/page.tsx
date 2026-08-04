"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { getOrCreatePlayerId } from "@/lib/identity";
import { getPaymentInvoice, simulatePaymentPaid } from "@/lib/payments/api";
import type { PaymentInvoiceDto } from "@/lib/payments/types";

interface OdemePageProps {
  params: Promise<{ invoiceId: string }>;
}

/**
 * docs/07-pages.md `/odeme/[invoiceId]`: BTCPay invoice durumu — top-up VEYA
 * maça giriş (docs/05-payment.md Bölüm 1.9). SignalR yerine polling kullanılır
 * (bkz. Sayfa Bazlı Veri Kaynağı tablosu — bu sayfa için sürekli bağlantı
 * gerekmez, webhook-tetiklemeli).
 */
export default function OdemePage({ params }: OdemePageProps) {
  const { invoiceId } = use(params);
  const router = useRouter();
  const [invoice, setInvoice] = useState<PaymentInvoiceDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [simulating, setSimulating] = useState(false);

  useEffect(() => {
    let cancelled = false;

    function fetchOnce() {
      getPaymentInvoice(invoiceId)
        .then((dto) => {
          if (!cancelled) {
            setInvoice(dto);
          }
        })
        .catch((err) => {
          if (!cancelled) {
            setError(String(err));
          }
        });
    }

    fetchOnce();
    const interval = window.setInterval(fetchOnce, 3000);
    return () => {
      cancelled = true;
      window.clearInterval(interval);
    };
  }, [invoiceId]);

  useEffect(() => {
    if (!invoice || invoice.status !== "Confirmed") {
      return;
    }

    if (invoice.matchId && invoice.matchJoinOutcome === "Joined") {
      window.localStorage.setItem(`wintowar:match:${invoice.matchId}:playerId`, getOrCreatePlayerId());
      const timeout = window.setTimeout(() => router.push(`/game/${invoice.matchId}`), 1200);
      return () => window.clearTimeout(timeout);
    }
  }, [invoice, router]);

  async function handleSimulatePaid() {
    setSimulating(true);
    setError(null);
    try {
      await simulatePaymentPaid(invoiceId);
    } catch (err) {
      setError(String(err));
    } finally {
      setSimulating(false);
    }
  }

  if (error && !invoice) {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
        <p className="text-sm text-destructive">{error}</p>
        <Button variant="outline" onClick={() => router.push("/lobi")}>
          Lobiye Dön
        </Button>
      </div>
    );
  }

  if (!invoice) {
    return (
      <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">Yükleniyor...</div>
    );
  }

  if (invoice.status === "Expired" || invoice.status === "Failed") {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
        <h1 className="text-lg font-semibold">Ödeme Tamamlanamadı</h1>
        <p className="text-sm text-muted-foreground">
          {invoice.status === "Expired" ? "Faturanın süresi doldu." : "Ödeme başarısız oldu."}
        </p>
        <Button variant="outline" onClick={() => router.push("/lobi")}>
          Lobiye Dön
        </Button>
      </div>
    );
  }

  if (invoice.status === "Confirmed") {
    if (invoice.matchJoinOutcome === "RoomFull") {
      return (
        <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
          <h1 className="text-lg font-semibold">Bu oda doldu</h1>
          <p className="text-sm text-muted-foreground">Ödemeniz bakiyenize eklendi.</p>
          <Button onClick={() => router.push("/lobi")}>Lobiye Dön</Button>
        </div>
      );
    }

    if (invoice.matchId && invoice.matchJoinOutcome === "Joined") {
      return (
        <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
          <h1 className="text-lg font-semibold">Ödeme onaylandı</h1>
          <p className="text-sm text-muted-foreground">Lobiye yönlendiriliyorsunuz...</p>
        </div>
      );
    }

    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
        <h1 className="text-lg font-semibold">Bakiyeniz güncellendi</h1>
        <p className="text-sm text-muted-foreground">{invoice.amountUsd} USD bakiyenize eklendi.</p>
        <Button onClick={() => router.push("/cuzdan")}>Cüzdana Dön</Button>
      </div>
    );
  }

  const confirmations = invoice.rateServedFromCache ? "beklemede" : "1/1 onay bekleniyor";

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4 px-4 py-16">
      <div>
        <h1 className="text-lg font-semibold">Ödeme Bekleniyor</h1>
        <p className="text-sm text-muted-foreground">
          {invoice.amountUsd} USD karşılığı {invoice.amountLtc} LTC gönderin.
        </p>
      </div>

      <div className="flex flex-col gap-1 rounded-md border border-border bg-card p-3">
        <span className="text-xs text-muted-foreground">Gönderilecek adres</span>
        <span className="break-all font-mono text-xs">{invoice.receivingAddress}</span>
      </div>

      <div className="flex flex-col gap-1 rounded-md border border-border bg-card p-3">
        <span className="text-xs text-muted-foreground">BIP-21 ödeme URI&apos;si</span>
        <span className="break-all font-mono text-xs">{invoice.bip21Uri}</span>
      </div>

      <dl className="grid grid-cols-2 gap-x-2 gap-y-1 text-xs text-muted-foreground">
        <dt>Kilitlenen kur</dt>
        <dd className="text-right font-mono">{invoice.lockedUsdPerLtc} USD/LTC</dd>
        <dt>Son geçerlilik</dt>
        <dd className="text-right font-mono">{new Date(invoice.expiresAt).toLocaleTimeString("tr-TR")}</dd>
      </dl>

      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <span className="size-2 rounded-full bg-muted-foreground" />
        Onay bekleniyor... ({confirmations})
      </div>

      <Button variant="outline" disabled={simulating} onClick={handleSimulatePaid}>
        {simulating ? "Simüle ediliyor..." : "Ödemeyi Simüle Et (test modu)"}
      </Button>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}
