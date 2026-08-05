"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  createTopUpInvoice,
  getInvoiceHistory,
  getPendingWithdrawals,
  getWalletBalance,
  requestWithdrawal,
} from "@/lib/payments/api";
import type { PaymentInvoiceDto, WithdrawalRequestDto } from "@/lib/payments/types";
import { isSignedIn } from "@/lib/identity";

const WITHDRAWAL_STATUS_LABEL: Record<string, string> = {
  Pending: "Bekliyor",
  Approved: "Onaylandı",
  Sent: "Gönderildi",
};

const INVOICE_STATUS_LABEL: Record<string, string> = {
  Pending: "Bekliyor",
  Confirmed: "Onaylandı",
  Expired: "Süresi Doldu",
  Refunded: "İade Edildi",
  Failed: "Başarısız",
};

/** docs/07-pages.md `/cuzdan`: Bakiye, bakiye yükleme, para çekme talebi (bkz. docs/05-payment.md Bölüm 1.9). */
export default function CuzdanPage() {
  const router = useRouter();
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [pendingWithdrawals, setPendingWithdrawals] = useState<WithdrawalRequestDto[]>([]);
  const [recentInvoices, setRecentInvoices] = useState<PaymentInvoiceDto[]>([]);

  const [topUpAmount, setTopUpAmount] = useState(1);
  const [topUpAddress, setTopUpAddress] = useState("");
  const [topUpBusy, setTopUpBusy] = useState(false);
  const [topUpError, setTopUpError] = useState<string | null>(null);

  const [withdrawAmount, setWithdrawAmount] = useState(1);
  const [withdrawAddress, setWithdrawAddress] = useState("");
  const [withdrawBusy, setWithdrawBusy] = useState(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);

  const refreshBalance = useCallback(() => {
    getWalletBalance()
      .then((dto) => setBalanceUsd(dto.balanceUsd))
      .catch((err) => setLoadError(String(err)));
  }, []);

  const refreshPendingWithdrawals = useCallback(() => {
    getPendingWithdrawals()
      .then(setPendingWithdrawals)
      .catch(() => setPendingWithdrawals([]));
  }, []);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    refreshBalance();
    refreshPendingWithdrawals();
    // docs/08-page-content.md Bölüm 3.9 Katman 2: son 5 işlem — tam geçmiş /gecmis'te.
    getInvoiceHistory()
      .then((invoices) => setRecentInvoices(invoices.slice(0, 5)))
      .catch(() => setRecentInvoices([]));
  }, [router, refreshBalance, refreshPendingWithdrawals]);

  async function handleTopUp() {
    setTopUpBusy(true);
    setTopUpError(null);
    try {
      const invoice = await createTopUpInvoice(topUpAmount, topUpAddress.trim());
      router.push(`/odeme/${invoice.invoiceId}`);
    } catch (err) {
      setTopUpError(String(err));
      setTopUpBusy(false);
    }
  }

  async function handleWithdraw() {
    setWithdrawBusy(true);
    setWithdrawError(null);
    try {
      await requestWithdrawal(withdrawAmount, withdrawAddress.trim());
      refreshBalance();
      refreshPendingWithdrawals();
    } catch (err) {
      setWithdrawError(String(err));
    } finally {
      setWithdrawBusy(false);
    }
  }

  if (balanceUsd === null && !loadError) {
    return <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">Yükleniyor...</div>;
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-6 px-4 py-6">
      <Card>
        <CardContent>
          <h1 className="text-sm font-medium text-muted-foreground">Bakiye</h1>
          <p className="text-2xl font-semibold tabular-nums">${balanceUsd ?? "0.00"}</p>
          {loadError ? <p className="mt-2 text-sm text-destructive">{loadError}</p> : null}
        </CardContent>
      </Card>

      {pendingWithdrawals.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>Bekleyen Transferler</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {pendingWithdrawals.map((w) => (
              <div key={w.id} className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">
                  {new Date(w.createdAt).toLocaleDateString("tr-TR")}
                </span>
                <span className="flex items-center gap-1.5 tabular-nums">
                  ${w.amountUsd}
                  <Badge variant="outline">{WITHDRAWAL_STATUS_LABEL[w.status] ?? w.status}</Badge>
                </span>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Para Yatır</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="topUpAmount">Tutar (USD)</Label>
            <Input
              id="topUpAmount"
              type="number"
              min={1}
              step={0.01}
              value={topUpAmount}
              onChange={(e) => setTopUpAmount(Number(e.target.value))}
              className="text-right"
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="topUpAddress">LTC ödül adresiniz</Label>
            <Input
              id="topUpAddress"
              className="font-mono"
              value={topUpAddress}
              onChange={(e) => setTopUpAddress(e.target.value)}
              placeholder="ltc1q... veya L..."
            />
          </div>
          <Button disabled={topUpBusy || !topUpAddress.trim()} onClick={handleTopUp}>
            {topUpBusy ? "Fatura oluşturuluyor..." : "Fatura Oluştur"}
          </Button>
          {topUpError ? <p className="text-sm text-destructive">{topUpError}</p> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Para Çek</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="withdrawAmount">Tutar (USD)</Label>
            <Input
              id="withdrawAmount"
              type="number"
              min={1}
              step={0.01}
              value={withdrawAmount}
              onChange={(e) => setWithdrawAmount(Number(e.target.value))}
              className="text-right"
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="withdrawAddress">Hedef LTC adresi</Label>
            <Input
              id="withdrawAddress"
              className="font-mono"
              value={withdrawAddress}
              onChange={(e) => setWithdrawAddress(e.target.value)}
              placeholder="ltc1q... veya L..."
            />
          </div>
          <Button variant="outline" disabled={withdrawBusy || !withdrawAddress.trim()} onClick={handleWithdraw}>
            {withdrawBusy ? "Gönderiliyor..." : "Çekim Talebi Oluştur"}
          </Button>
          {withdrawError ? <p className="text-sm text-destructive">{withdrawError}</p> : null}
        </CardContent>
      </Card>

      {recentInvoices.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>Son İşlemler</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {recentInvoices.map((invoice) => (
              <div key={invoice.invoiceId} className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">
                  {new Date(invoice.createdAt).toLocaleDateString("tr-TR")} ·{" "}
                  {invoice.matchId ? "Maça Giriş" : "Para Yatırma"}
                </span>
                <span className="flex items-center gap-1.5 tabular-nums">
                  ${invoice.amountUsd}
                  <Badge variant="outline">{INVOICE_STATUS_LABEL[invoice.status] ?? invoice.status}</Badge>
                </span>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
