"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { GameConnection } from "@/lib/game/signalr-client";
import { createPaymentInvoice, getPaymentInvoice, simulatePaymentPaid } from "@/lib/payments/api";
import type { PaymentInvoiceDto } from "@/lib/payments/types";

interface PaymentPanelProps {
  matchId: string;
  playerId: string;
  onConfirmed: () => void;
}

/**
 * Bölüm 3.1 (docs/05-payment.md) giriş ödemesi akışının istemci tarafı:
 * PayoutAddress alınır → invoice oluşturulur → BIP-21 URI gösterilir →
 * SignalR "PaymentConfirmed" (yedek olarak polling) ile onay beklenir.
 */
export function PaymentPanel({ matchId, playerId, onConfirmed }: PaymentPanelProps) {
  const [payoutAddress, setPayoutAddress] = useState("");
  const [invoice, setInvoice] = useState<PaymentInvoiceDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [simulating, setSimulating] = useState(false);
  const confirmedRef = useRef(false);

  async function handleCreateInvoice() {
    if (!payoutAddress.trim()) {
      setError("Kazanırsanız ödülünüzü alacağınız LTC adresini girin.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const dto = await createPaymentInvoice(matchId, playerId, payoutAddress.trim());
      setInvoice(dto);
    } catch (err) {
      setError(String(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleSimulatePaid() {
    if (!invoice) {
      return;
    }
    setSimulating(true);
    setError(null);
    try {
      await simulatePaymentPaid(invoice.invoiceId);
    } catch (err) {
      setError(String(err));
    } finally {
      setSimulating(false);
    }
  }

  // SignalR üzerinden anlık onay; yedek olarak periyodik polling (bağlantı
  // kopması/kaçırılan event ihtimaline karşı).
  useEffect(() => {
    if (!invoice || invoice.status !== "Pending") {
      return;
    }

    let cancelled = false;
    const connection = new GameConnection();

    connection.onPaymentConfirmed((event) => {
      if (!cancelled && event.invoiceId === invoice.invoiceId && !confirmedRef.current) {
        confirmedRef.current = true;
        onConfirmed();
      }
    });

    connection.start().then(() => connection.joinMatch(matchId, playerId)).catch(() => {
      // SignalR bağlanamazsa polling zaten yedek olarak devrede.
    });

    const pollInterval = window.setInterval(() => {
      getPaymentInvoice(matchId, invoice.invoiceId)
        .then((dto) => {
          if (cancelled) {
            return;
          }
          setInvoice(dto);
          if (dto.status === "Confirmed" && !confirmedRef.current) {
            confirmedRef.current = true;
            onConfirmed();
          }
        })
        .catch(() => {
          // Geçici ağ hatalarında sessizce bir sonraki turu bekle.
        });
    }, 3000);

    return () => {
      cancelled = true;
      window.clearInterval(pollInterval);
      connection.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [invoice?.invoiceId, invoice?.status]);

  if (!invoice) {
    return (
      <div className="flex w-full max-w-sm flex-col gap-4 rounded-md border border-border bg-card p-6">
        <div>
          <h2 className="text-lg font-semibold">Giriş Ücreti</h2>
          <p className="text-sm text-muted-foreground">
            Maça katılmak için 1 USD karşılığı LTC ödemesi gerekir. Kazanırsanız ödülünüz aşağıda
            gireceğiniz adrese gönderilir.
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="payoutAddress">
            LTC ödül adresiniz
          </label>
          <input
            id="payoutAddress"
            className="h-9 rounded-md border border-input bg-background px-3 font-mono text-sm"
            value={payoutAddress}
            onChange={(e) => setPayoutAddress(e.target.value)}
            placeholder="ltc1q... veya L..."
          />
        </div>

        <Button disabled={busy} onClick={handleCreateInvoice}>
          {busy ? "Fatura oluşturuluyor..." : "Fatura Oluştur"}
        </Button>

        {error ? <p className="text-sm text-destructive">{error}</p> : null}
      </div>
    );
  }

  return (
    <div className="flex w-full max-w-sm flex-col gap-4 rounded-md border border-border bg-card p-6">
      <div>
        <h2 className="text-lg font-semibold">Ödeme Bekleniyor</h2>
        <p className="text-sm text-muted-foreground">
          {invoice.amountUsd} USD karşılığı {invoice.amountLtc} LTC gönderin.
        </p>
      </div>

      <div className="flex flex-col gap-1 rounded-md border border-border bg-background p-3">
        <span className="text-xs text-muted-foreground">Gönderilecek adres</span>
        <span className="break-all font-mono text-xs">{invoice.receivingAddress}</span>
      </div>

      <div className="flex flex-col gap-1 rounded-md border border-border bg-background p-3">
        <span className="text-xs text-muted-foreground">BIP-21 ödeme URI&apos;si</span>
        <span className="break-all font-mono text-xs">{invoice.bip21Uri}</span>
      </div>

      <dl className="grid grid-cols-2 gap-x-2 gap-y-1 text-xs text-muted-foreground">
        <dt>Kilitlenen kur</dt>
        <dd className="text-right font-mono">{invoice.lockedUsdPerLtc} USD/LTC</dd>
        <dt>Son geçerlilik</dt>
        <dd className="text-right font-mono">{new Date(invoice.expiresAt).toLocaleTimeString("tr-TR")}</dd>
      </dl>

      <div className="flex items-center gap-2 text-sm">
        <span className="size-2 rounded-full bg-muted-foreground" />
        Ödeme onayı bekleniyor...
      </div>

      <Button variant="outline" disabled={simulating} onClick={handleSimulatePaid}>
        {simulating ? "Simüle ediliyor..." : "Ödemeyi Simüle Et (test modu)"}
      </Button>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}
