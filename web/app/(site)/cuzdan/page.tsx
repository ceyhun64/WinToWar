"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Wallet,
  Clock,
  ArrowDownToLine,
  ArrowUpFromLine,
  Coins,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NumberInput } from "@/components/ui/number-input";
import { Label } from "@/components/ui/label";
import { PageHero } from "@/components/layout/PageHero";
import { SectionTitle } from "@/components/layout/SectionTitle";
import { GameCard } from "@/components/layout/GameCard";
import { InvoiceRow } from "@/components/payments/InvoiceRow";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  createTopUpInvoice,
  getInvoiceHistory,
  getPendingWithdrawals,
  getWithdrawalAddressSuggestions,
  requestWithdrawal,
} from "@/lib/payments/api";
import type {
  PaymentInvoiceDto,
  WithdrawalAddressSuggestionDto,
  WithdrawalRequestDto,
} from "@/lib/payments/types";
import { AuthGuard } from "@/components/layout/AuthGuard";
import { useWallet } from "@/lib/payments/WalletProvider";
import { truncateAddress } from "@/lib/utils";

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
  const { balanceUsd } = useWallet();
  const [pendingWithdrawals, setPendingWithdrawals] = useState<
    WithdrawalRequestDto[]
  >([]);
  const [recentInvoices, setRecentInvoices] = useState<PaymentInvoiceDto[]>([]);
  const [addressSuggestions, setAddressSuggestions] = useState<
    WithdrawalAddressSuggestionDto[]
  >([]);

  const [topUpAmount, setTopUpAmount] = useState(1);
  const [topUpBusy, setTopUpBusy] = useState(false);
  const [topUpError, setTopUpError] = useState<string | null>(null);

  const [withdrawAmount, setWithdrawAmount] = useState(1);
  const [withdrawAddress, setWithdrawAddress] = useState("");
  const [withdrawBusy, setWithdrawBusy] = useState(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);

  // "Son İşlemler" kartının boyu masaüstünde soldaki sütunla eşitlenir, taşan
  // içerik kartın kendi içinde scroll olur.
  //
  // ÖNEMLİ: Dış grid'de `items-stretch` KULLANILMAZ. items-stretch olsaydı,
  // grid satırının yüksekliği sağ kartın (kısıtlanmamış) doğal içerik
  // yüksekliğine göre belirlenir, sonra items-stretch sol sütunu da o satıra
  // gerer — böylece ResizeObserver'ın ölçtüğü "sol sütun yüksekliği" aslında
  // sağ kartın etkisiyle şişmiş bir değer olur. Bu da şu kısır döngüyü
  // doğurur: sağ karta maxHeight uygulanır → satır küçülür → sol sütun
  // (stretch nedeniyle) küçülür → ResizeObserver daha küçük bir değer ölçer →
  // sağ karta daha küçük bir yükseklik uygulanır → ... → yükseklik hiçbir
  // zaman doğru değere oturmaz.
  //
  // Bunun yerine `items-start` kullanılır: grid hiçbir sütunu otomatik
  // germez, sol sütun her zaman kendi doğal (sağ karttan bağımsız)
  // yüksekliğinde kalır. ResizeObserver bu gerçek/sabit değeri ölçer, sağ
  // karta da `height` (maxHeight değil — içerik azken de eşit yükseklikte
  // kalması için) olarak uygulanır.
  const leftColumnRef = useRef<HTMLDivElement>(null);
  const [leftColumnHeight, setLeftColumnHeight] = useState<number | null>(null);
  const [isDesktopLayout, setIsDesktopLayout] = useState(false);

  useEffect(() => {
    const mql = window.matchMedia("(min-width: 1024px)");
    const onChange = () => setIsDesktopLayout(mql.matches);
    onChange();
    mql.addEventListener("change", onChange);
    return () => mql.removeEventListener("change", onChange);
  }, []);

  useEffect(() => {
    const el = leftColumnRef.current;
    if (!el) return;
    const observer = new ResizeObserver((entries) => {
      const measured = entries[0].contentRect.height;
      setLeftColumnHeight((prev) =>
        prev !== null && Math.abs(prev - measured) < 1 ? prev : measured,
      );
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const refreshPendingWithdrawals = useCallback(() => {
    getPendingWithdrawals()
      .then(setPendingWithdrawals)
      .catch(() => setPendingWithdrawals([]));
  }, []);

  useEffect(() => {
    refreshPendingWithdrawals();
    getInvoiceHistory()
      .then(setRecentInvoices)
      .catch(() => setRecentInvoices([]));
    getWithdrawalAddressSuggestions()
      .then(setAddressSuggestions)
      .catch(() => setAddressSuggestions([]));
  }, [refreshPendingWithdrawals]);

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
      refreshPendingWithdrawals();
    } catch (err) {
      setWithdrawError(String(err));
    } finally {
      setWithdrawBusy(false);
    }
  }

  if (balanceUsd === null) {
    return (
      <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
        Yükleniyor...
      </div>
    );
  }

  const rightCardHeight =
    isDesktopLayout && leftColumnHeight ? leftColumnHeight : undefined;

  return (
    <div className="mx-auto flex w-full max-w-7xl flex-1 flex-col gap-8 px-4 py-4">
      <PageHero
        icon={Wallet}
        title="Cüzdan"
        subtitle="Bakiyeni yönet, yatır veya çek."
        accent="#F5B942"
      />

      <div className="flex flex-col gap-8 lg:grid lg:grid-cols-2 lg:items-start lg:gap-8">
        <div ref={leftColumnRef} className="flex flex-col gap-8">
          <GameCard className="relative overflow-hidden p-6">
            <Coins
              aria-hidden="true"
              className="pointer-events-none absolute -bottom-4 -right-4 size-24 opacity-[0.06] blur-[1.5px]"
              style={{ color: "#F5B942" }}
            />
            <div className="relative z-10 flex flex-col gap-1">
              <span className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                Bakiye
              </span>
              <span className="text-4xl font-bold tabular-nums text-yellow-500">
                ${balanceUsd ?? "0.00"}
              </span>
            </div>
          </GameCard>

          {pendingWithdrawals.length > 0 ? (
            <section className="flex flex-col gap-3">
              <SectionTitle>Bekleyen Transferler</SectionTitle>
              <GameCard>
                <CardContent className="flex flex-col gap-3">
                  {pendingWithdrawals.map((w) => (
                    <div
                      key={w.id}
                      className="flex items-center justify-between gap-3 text-sm"
                    >
                      <span className="flex min-w-0 items-center gap-2 text-muted-foreground">
                        <Clock className="size-3.5 shrink-0" aria-hidden="true" />
                        <span className="truncate">{new Date(w.createdAt).toLocaleDateString("tr-TR")}</span>
                      </span>
                      <span className="flex shrink-0 items-center gap-3 tabular-nums">
                        ${w.amountUsd}
                        <Badge variant="outline">
                          {WITHDRAWAL_STATUS_LABEL[w.status] ?? w.status}
                        </Badge>
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
                  <ArrowDownToLine
                    className="size-4"
                    style={{ color: "#38BDF8" }}
                    aria-hidden="true"
                  />
                  Para Yatır
                </CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                <div className="flex flex-col gap-3">
                  <Label htmlFor="topUpAmount">Tutar (USD)</Label>
                  <NumberInput
                    id="topUpAmount"
                    min={1}
                    step={0.01}
                    value={topUpAmount}
                    onValueChange={(value) =>
                      setTopUpAmount((prev) => value ?? prev)
                    }
                  />
                </div>
                <Button className="rounded-full" disabled={topUpBusy} onClick={handleTopUp}>
                  {topUpBusy ? "Fatura oluşturuluyor..." : "Fatura Oluştur"}
                </Button>
                {topUpError ? (
                  <p className="text-sm text-destructive">{topUpError}</p>
                ) : null}
              </CardContent>
            </GameCard>

            <GameCard>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <ArrowUpFromLine
                    className="size-4"
                    style={{ color: "#F5B942" }}
                    aria-hidden="true"
                  />
                  Para Çek
                </CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                <div className="flex flex-col gap-3">
                  <Label htmlFor="withdrawAmount">Tutar (USD)</Label>
                  <NumberInput
                    id="withdrawAmount"
                    min={1}
                    step={0.01}
                    value={withdrawAmount}
                    onValueChange={(value) =>
                      setWithdrawAmount((prev) => value ?? prev)
                    }
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
                {addressSuggestions.length > 0 ? (
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs text-muted-foreground">
                      Son kullanılan adresler
                    </span>
                    <div className="flex flex-wrap gap-1.5">
                      {addressSuggestions.map((s) => (
                        <button
                          key={s.address}
                          type="button"
                          onClick={() => setWithdrawAddress(s.address)}
                          className="rounded-md border px-2 py-1 font-mono text-xs text-muted-foreground hover:border-foreground/40 hover:text-foreground"
                        >
                          {truncateAddress(s.address)}
                        </button>
                      ))}
                    </div>
                  </div>
                ) : null}
                <Button
                  variant="outline"
                  className="rounded-full"
                  disabled={withdrawBusy || !withdrawAddress.trim()}
                  title={
                    !withdrawAddress.trim()
                      ? "Önce hedef LTC adresini girin"
                      : undefined
                  }
                  onClick={handleWithdraw}
                >
                  {withdrawBusy ? "Gönderiliyor..." : "Çekim Talebi Oluştur"}
                </Button>
                {withdrawError ? (
                  <p className="text-sm text-destructive">{withdrawError}</p>
                ) : null}
              </CardContent>
            </GameCard>
          </div>
        </div>

        {recentInvoices.length > 0 ? (
          <GameCard
            className="flex flex-col min-h-0"
            style={rightCardHeight ? { height: rightCardHeight } : undefined}
          >
            <CardHeader className="shrink-0">
              <SectionTitle>Son İşlemler</SectionTitle>
            </CardHeader>
            <CardContent className="min-h-0 flex-1">
              <ScrollArea className="h-full">
                <div className="flex flex-col gap-1">
                  {recentInvoices.map((invoice) => (
                    <InvoiceRow key={invoice.invoiceId} invoice={invoice} />
                  ))}
                </div>
              </ScrollArea>
            </CardContent>
          </GameCard>
        ) : null}
      </div>
    </div>
  );
}
