"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Wallet, Clock, ArrowDownToLine, ArrowUpFromLine, Coins } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHero } from "@/components/layout/PageHero";
import { SectionTitle } from "@/components/layout/SectionTitle";
import { GameCard } from "@/components/layout/GameCard";
import { InvoiceRow } from "@/components/payments/InvoiceRow";
import {
  createTopUpInvoice,
  getInvoiceHistory,
  getPendingWithdrawals,
  getWalletBalance,
  requestWithdrawal,
} from "@/lib/payments/api";
import type { PaymentInvoiceDto, WithdrawalRequestDto } from "@/lib/payments/types";
import { AuthGuard } from "@/components/layout/AuthGuard";

const WITHDRAWAL_STATUS_LABEL: Record<string, string> = {
  Pending: "Bekliyor",
  Approved: "Onaylandı",
  Sent: "Gönderildi",
};

/**
 * docs/07-pages.md `/cuzdan`: Bakiye, bakiye yükleme, para çekme talebi (bkz. docs/05-payment.md Bölüm 1.9).
 * `docs/04-style.md` Landing İstisnası — sitede genelleştirilmiş tasarım
 * sistemi: "premium oyun mağazası" hissi — büyük altın vurgulu bakiye, Yatır/
 * Çek yan yana iki panel, işlem geçmişi ikon rozetli satırlar. İş mantığı
 * (state, handler'lar, API çağrıları) birebir korunur, yalnızca sunum değişti.
 */
export default function CuzdanPage() {
  return (
    <AuthGuard>
      <CuzdanPageContent />
    </AuthGuard>
  );
}

function CuzdanPageContent() {
  const router = useRouter();
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [pendingWithdrawals, setPendingWithdrawals] = useState<WithdrawalRequestDto[]>([]);
  const [recentInvoices, setRecentInvoices] = useState<PaymentInvoiceDto[]>([]);

  const [topUpAmount, setTopUpAmount] = useState(1);
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
    refreshBalance();
    refreshPendingWithdrawals();
    // docs/08-page-content.md Bölüm 3.9 Katman 2: son 5 işlem — tam geçmiş /gecmis'te.
    getInvoiceHistory()
      .then((invoices) => setRecentInvoices(invoices.slice(0, 5)))
      .catch(() => setRecentInvoices([]));
  }, [refreshBalance, refreshPendingWithdrawals]);

  async function handleTopUp() {
    setTopUpBusy(true);
    setTopUpError(null);
    try {
      const invoice = await createTopUpInvoice(topUpAmount);
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
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-8 px-4 py-8 md:py-10">
      <PageHero icon={Wallet} title="Cüzdan" subtitle="Bakiyeni yönet, yatır veya çek." accent="#F5B942" />

      <GameCard className="relative overflow-hidden p-6">
        <Coins
          aria-hidden="true"
          className="pointer-events-none absolute -bottom-4 -right-4 size-24 opacity-[0.06] blur-[1.5px]"
          style={{ color: "#F5B942" }}
        />
        <div className="relative z-10 flex flex-col gap-1">
          <span className="text-xs font-medium uppercase tracking-widest text-muted-foreground">Bakiye</span>
          <span className="text-4xl font-bold tabular-nums" style={{ color: "#F5B942" }}>
            ${balanceUsd ?? "0.00"}
          </span>
          {loadError ? <p className="mt-2 text-sm text-destructive">{loadError}</p> : null}
        </div>
      </GameCard>

      {pendingWithdrawals.length > 0 ? (
        <section className="flex flex-col gap-3">
          <SectionTitle>Bekleyen Transferler</SectionTitle>
          <GameCard>
            <CardContent className="flex flex-col gap-3">
              {pendingWithdrawals.map((w) => (
                <div key={w.id} className="flex items-center justify-between text-sm">
                  <span className="flex items-center gap-2 text-muted-foreground">
                    <Clock className="size-3.5" aria-hidden="true" />
                    {new Date(w.createdAt).toLocaleDateString("tr-TR")}
                  </span>
                  <span className="flex items-center gap-3 tabular-nums">
                    ${w.amountUsd}
                    <Badge variant="outline">{WITHDRAWAL_STATUS_LABEL[w.status] ?? w.status}</Badge>
                  </span>
                </div>
              ))}
            </CardContent>
          </GameCard>
        </section>
      ) : null}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <GameCard>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ArrowDownToLine className="size-4" style={{ color: "#38BDF8" }} aria-hidden="true" />
              Para Yatır
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <div className="flex flex-col gap-3">
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
            <Button disabled={topUpBusy} onClick={handleTopUp}>
              {topUpBusy ? "Fatura oluşturuluyor..." : "Fatura Oluştur"}
            </Button>
            {topUpError ? <p className="text-sm text-destructive">{topUpError}</p> : null}
          </CardContent>
        </GameCard>

        <GameCard>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ArrowUpFromLine className="size-4" style={{ color: "#F5B942" }} aria-hidden="true" />
              Para Çek
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <div className="flex flex-col gap-3">
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
            <div className="flex flex-col gap-3">
              <Label htmlFor="withdrawAddress">Hedef LTC adresi</Label>
              <Input
                id="withdrawAddress"
                className="font-mono"
                value={withdrawAddress}
                onChange={(e) => setWithdrawAddress(e.target.value)}
                placeholder="ltc1q... veya L..."
              />
            </div>
            <Button
              variant="outline"
              disabled={withdrawBusy || !withdrawAddress.trim()}
              title={!withdrawAddress.trim() ? "Önce hedef LTC adresini girin" : undefined}
              onClick={handleWithdraw}
            >
              {withdrawBusy ? "Gönderiliyor..." : "Çekim Talebi Oluştur"}
            </Button>
            {withdrawError ? <p className="text-sm text-destructive">{withdrawError}</p> : null}
          </CardContent>
        </GameCard>
      </div>

      {recentInvoices.length > 0 ? (
        <section className="flex flex-col gap-3">
          <SectionTitle>Son İşlemler</SectionTitle>
          <GameCard className="p-2">
            <CardContent className="flex flex-col gap-1 px-2">
              {recentInvoices.map((invoice) => (
                <InvoiceRow key={invoice.invoiceId} invoice={invoice} />
              ))}
            </CardContent>
          </GameCard>
        </section>
      ) : null}
    </div>
  );
}
