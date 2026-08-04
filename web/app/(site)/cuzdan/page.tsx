"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { createTopUpInvoice, getWalletBalance, requestWithdrawal } from "@/lib/payments/api";
import type { WithdrawalRequestDto } from "@/lib/payments/types";
import { isSignedIn } from "@/lib/identity";

/** docs/07-pages.md `/cuzdan`: Bakiye, bakiye yükleme, para çekme talebi (bkz. docs/05-payment.md Bölüm 1.9). */
export default function CuzdanPage() {
  const router = useRouter();
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [topUpAmount, setTopUpAmount] = useState(1);
  const [topUpAddress, setTopUpAddress] = useState("");
  const [topUpBusy, setTopUpBusy] = useState(false);
  const [topUpError, setTopUpError] = useState<string | null>(null);

  const [withdrawAmount, setWithdrawAmount] = useState(1);
  const [withdrawAddress, setWithdrawAddress] = useState("");
  const [withdrawBusy, setWithdrawBusy] = useState(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);
  const [lastWithdrawal, setLastWithdrawal] = useState<WithdrawalRequestDto | null>(null);

  const refreshBalance = useCallback(() => {
    getWalletBalance()
      .then((dto) => setBalanceUsd(dto.balanceUsd))
      .catch((err) => setLoadError(String(err)));
  }, []);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    refreshBalance();
  }, [router, refreshBalance]);

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
      const dto = await requestWithdrawal(withdrawAmount, withdrawAddress.trim());
      setLastWithdrawal(dto);
      refreshBalance();
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
      <div className="rounded-md border border-border bg-card p-4">
        <h1 className="text-sm font-medium text-muted-foreground">Bakiye</h1>
        <p className="text-2xl font-semibold tabular-nums">${balanceUsd ?? "0.00"}</p>
        {loadError ? <p className="mt-2 text-sm text-destructive">{loadError}</p> : null}
      </div>

      <div className="flex flex-col gap-3 rounded-md border border-border bg-card p-4">
        <h2 className="text-sm font-semibold">Bakiye Yükle</h2>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="topUpAmount">
            Tutar (USD)
          </label>
          <input
            id="topUpAmount"
            type="number"
            min={1}
            step={0.01}
            value={topUpAmount}
            onChange={(e) => setTopUpAmount(Number(e.target.value))}
            className="h-9 rounded-md border border-input bg-background px-3 text-right text-sm"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="topUpAddress">
            LTC ödül adresiniz
          </label>
          <input
            id="topUpAddress"
            className="h-9 rounded-md border border-input bg-background px-3 font-mono text-sm"
            value={topUpAddress}
            onChange={(e) => setTopUpAddress(e.target.value)}
            placeholder="ltc1q... veya L..."
          />
        </div>
        <Button disabled={topUpBusy || !topUpAddress.trim()} onClick={handleTopUp}>
          {topUpBusy ? "Fatura oluşturuluyor..." : "Fatura Oluştur"}
        </Button>
        {topUpError ? <p className="text-sm text-destructive">{topUpError}</p> : null}
      </div>

      <div className="flex flex-col gap-3 rounded-md border border-border bg-card p-4">
        <h2 className="text-sm font-semibold">Para Çek</h2>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="withdrawAmount">
            Tutar (USD)
          </label>
          <input
            id="withdrawAmount"
            type="number"
            min={1}
            step={0.01}
            value={withdrawAmount}
            onChange={(e) => setWithdrawAmount(Number(e.target.value))}
            className="h-9 rounded-md border border-input bg-background px-3 text-right text-sm"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="withdrawAddress">
            Hedef LTC adresi
          </label>
          <input
            id="withdrawAddress"
            className="h-9 rounded-md border border-input bg-background px-3 font-mono text-sm"
            value={withdrawAddress}
            onChange={(e) => setWithdrawAddress(e.target.value)}
            placeholder="ltc1q... veya L..."
          />
        </div>
        <Button variant="outline" disabled={withdrawBusy || !withdrawAddress.trim()} onClick={handleWithdraw}>
          {withdrawBusy ? "Gönderiliyor..." : "Çekim Talebi Oluştur"}
        </Button>
        {withdrawError ? <p className="text-sm text-destructive">{withdrawError}</p> : null}
        {lastWithdrawal ? (
          <p className="text-sm text-muted-foreground">
            Talep oluşturuldu ({lastWithdrawal.amountUsd} USD) — durum: {lastWithdrawal.status}
          </p>
        ) : null}
      </div>
    </div>
  );
}
