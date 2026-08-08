"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { QRCodeSVG } from "qrcode.react";
import { XCircle, CheckCircle2, Clock, Check, Copy } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { CardContent } from "@/components/ui/card";
import { GameCard } from "@/components/layout/GameCard";
import { AuthGuard } from "@/components/layout/AuthGuard";
import { cn } from "@/lib/utils";
import { getCurrentPlayerId } from "@/lib/identity";
import { getPaymentInvoice, simulatePaymentPaid } from "@/lib/payments/api";
import type { PaymentInvoiceDto } from "@/lib/payments/types";

interface OdemePageProps {
  params: Promise<{ invoiceId: string }>;
}

/** Uzun LTC adreslerini "ltc1qxxxx…xxxxxx" şeklinde ortadan kısaltır — Kopyala her zaman tam adresi kopyalar. */
function truncateMiddle(value: string, front = 10, back = 8): string {
  if (value.length <= front + back + 1) {
    return value;
  }
  return `${value.slice(0, front)}…${value.slice(-back)}`;
}

function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

/**
 * docs/07-pages.md `/odeme/[invoiceId]`: BTCPay invoice durumu — top-up VEYA
 * maça giriş (docs/05-payment.md Bölüm 1.9). SignalR yerine polling kullanılır
 * (bkz. Sayfa Bazlı Veri Kaynağı tablosu — bu sayfa için sürekli bağlantı
 * gerekmez, webhook-tetiklemeli).
 * 🔒 Yetki Matrisi: yalnızca invoice'ın sahibi görebilir — AuthGuard oturum
 * kontrolünü yapar, sahiplik kontrolü backend'de JWT'den okunan playerId ile
 * (bkz. lib/payments/api.ts getPaymentInvoice) uygulanır.
 */
export default function OdemePage(props: OdemePageProps) {
  return (
    <AuthGuard>
      <OdemePageContent {...props} />
    </AuthGuard>
  );
}

function OdemePageContent({ params }: OdemePageProps) {
  const { invoiceId } = use(params);
  const router = useRouter();
  const [invoice, setInvoice] = useState<PaymentInvoiceDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [simulating, setSimulating] = useState(false);
  const [copied, setCopied] = useState(false);
  const [showQrMobile, setShowQrMobile] = useState(false);
  const [now, setNow] = useState(() => Date.now());

  // Kullanıcı geri bildirimi: "Son geçerlilik" statik bir saat değil, saniye
  // saniye azalan bir geri sayım olmalı. Otoriter durum (Expired) hâlâ
  // yalnızca backend'den (polling ile) gelir — bu sayaç yalnızca görsel geri
  // bildirimdir, backend'in webhook'tan gelen gerçek Expired durumunu
  // aşağıdaki polling ayrıca yakalar.
  useEffect(() => {
    const interval = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, []);

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

    const myPlayerId = getCurrentPlayerId();
    if (invoice.matchId && invoice.matchJoinOutcome === "Joined" && myPlayerId) {
      window.localStorage.setItem(`wintowar:match:${invoice.matchId}:playerId`, myPlayerId);
      const timeout = window.setTimeout(() => router.push(`/game/${invoice.matchId}`), 1200);
      return () => window.clearTimeout(timeout);
    }
  }, [invoice, router]);

  async function handleCopyAddress() {
    if (!invoice) return;
    try {
      await navigator.clipboard.writeText(invoice.receivingAddress);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      // Panoya erişim izni yoksa (ör. http üzerinde) sessizce yoksayılır — adres zaten metin olarak seçilebilir.
    }
  }

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
        <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#F2495C22", color: "#F2495C" }}>
          <XCircle className="size-6" aria-hidden="true" />
        </span>
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
          <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#38BDF822", color: "#38BDF8" }}>
            <CheckCircle2 className="size-6" aria-hidden="true" />
          </span>
          <h1 className="text-lg font-semibold">Bu oda doldu</h1>
          <p className="text-sm text-muted-foreground">Ödemeniz bakiyenize eklendi.</p>
          <Button onClick={() => router.push("/lobi")}>Lobiye Dön</Button>
        </div>
      );
    }

    if (invoice.matchId && invoice.matchJoinOutcome === "Joined") {
      return (
        <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
          <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#38BDF822", color: "#38BDF8" }}>
            <CheckCircle2 className="size-6" aria-hidden="true" />
          </span>
          <h1 className="text-lg font-semibold">Ödeme onaylandı</h1>
          <p className="text-sm text-muted-foreground">Lobiye yönlendiriliyorsunuz...</p>
        </div>
      );
    }

    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
        <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#F5B94222", color: "#F5B942" }}>
          <CheckCircle2 className="size-6" aria-hidden="true" />
        </span>
        <h1 className="text-lg font-semibold">Bakiyeniz güncellendi</h1>
        <p className="text-sm text-muted-foreground">{invoice.amountUsd} USD bakiyenize eklendi.</p>
        <Button onClick={() => router.push("/cuzdan")}>Cüzdana Dön</Button>
      </div>
    );
  }

  const remainingSeconds = Math.max(0, Math.floor((new Date(invoice.expiresAt).getTime() - now) / 1000));
  const expiredLocally = remainingSeconds <= 0;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4 px-4 py-16">
      <div className="flex flex-col items-center gap-3 text-center">
        <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#38BDF822", color: "#38BDF8" }}>
          <Clock className="size-6" aria-hidden="true" />
        </span>
        <h1 className="text-lg font-semibold">Ödeme Bekleniyor</h1>
      </div>

      {/* Kullanıcı geri bildirimi: ekran önce "ne kadar" sorusuna cevap vermeli —
          USD tutarı büyük, LTC karşılığı hemen altında ikincil vurguda. */}
      <div className="flex flex-col items-center gap-0.5 text-center">
        <span className="text-3xl font-bold tabular-nums">${invoice.amountUsd}</span>
        <span className="font-mono text-sm text-muted-foreground">{invoice.amountLtc} LTC</span>
        {/* docs/08-page-content.md Bölüm 3.7 Katman 2: ödemenin ne için olduğu + onay sonrası ne olacağı tek cümleyle. */}
        <p className="mt-1 text-sm text-muted-foreground">
          {invoice.matchId
            ? "Bu ödeme maça giriş içindir. Onaylanınca otomatik olarak lobiye eklenirsiniz."
            : "Bu ödeme bakiye yüklemedir. Onaylanınca bakiyeniz güncellenir."}
        </p>
      </div>

      <GameCard size="sm">
        <CardContent className="flex flex-col gap-3">
          {/* Kullanıcı geri bildirimi: mobilde QR okutulamaz — adres her zaman
              metin olarak görünür ve tek dokunuşla kopyalanabilir olmalı, QR
              yalnızca ikincil/tamamlayıcı bir yöntemdir. */}
          <div className="flex flex-col gap-1.5">
            <span className="text-xs text-muted-foreground">Gönderilecek adres</span>
            <div className="flex items-center gap-2">
              <span
                title={invoice.receivingAddress}
                className="min-w-0 flex-1 truncate rounded-xl border border-border bg-background/60 px-2.5 py-1.5 font-mono text-xs"
              >
                {truncateMiddle(invoice.receivingAddress)}
              </span>
              <Button type="button" variant="outline" size="sm" onClick={handleCopyAddress}>
                {copied ? <Check className="size-3.5" aria-hidden="true" /> : <Copy className="size-3.5" aria-hidden="true" />}
                {copied ? "Kopyalandı" : "Kopyala"}
              </Button>
            </div>
          </div>

          <a href={invoice.bip21Uri} className={buttonVariants({ variant: "ghost", size: "sm" })}>
            LTC Cüzdanında Aç
          </a>

          {/* 🛠️ docs/04-style.md Bölüm 2 istisnası (docs/09-eksik-tarama-promptu.md
              denetimi, Faz 7'de belgelendi): bu tek yerde ham bg-white bilinçli —
              QR okuyucuların güvenilir taraması için gerçek beyaz zemin + koyu
              modül kontrastı gerekir, dark modda --card gibi koyu bir token
              kullanmak taramayı bozar. Palet dışına çıkan tek, gerekçeli istisna.
              Masaüstünde her zaman görünür; mobilde telefonun kendi kamerasıyla
              kendi ekranını okutması mantıksız olduğundan varsayılan gizlidir. */}
          <div className="hidden justify-center sm:flex">
            <div className="rounded-md bg-white p-3">
              <QRCodeSVG value={invoice.bip21Uri} size={160} />
            </div>
          </div>

          <div className="sm:hidden">
            <button
              type="button"
              onClick={() => setShowQrMobile((v) => !v)}
              className="text-xs text-muted-foreground underline underline-offset-2"
            >
              {showQrMobile ? "QR kodu gizle" : "QR kodu göster"}
            </button>
            {showQrMobile ? (
              <div className="mt-3 flex justify-center">
                <div className="rounded-md bg-white p-3">
                  <QRCodeSVG value={invoice.bip21Uri} size={160} />
                </div>
              </div>
            ) : null}
          </div>
        </CardContent>
      </GameCard>

      <dl className="grid grid-cols-2 gap-x-2 gap-y-1 text-xs text-muted-foreground">
        <dt>Kilitlenen kur</dt>
        <dd className="text-right font-mono">{invoice.lockedUsdPerLtc} USD/LTC</dd>
        <dt>Son geçerlilik</dt>
        <dd className={cn("text-right font-mono", expiredLocally && "text-destructive")}>
          {expiredLocally ? "Süre doldu" : formatCountdown(remainingSeconds)}
        </dd>
      </dl>

      <div className="flex items-center justify-between text-sm text-muted-foreground">
        <span>{expiredLocally ? "Süre doldu, ödeme kabul edilmiyor" : "Onay bekleniyor..."}</span>
        <Badge variant="outline">{invoice.currentConfirmations}/{invoice.requiredConfirmations} onay</Badge>
      </div>

      {/* docs/05-payment.md Bölüm 0.3: bu buton yalnızca FakePaymentProvider'ın
          etkin olduğu Development ortamında anlamlıdır (bkz. Program.cs,
          PaymentsDevController) — üretimde hiç render edilmez. */}
      {process.env.NODE_ENV !== "production" ? (
        <Button variant="white" disabled={simulating} onClick={handleSimulatePaid}>
          {simulating ? "Simüle ediliyor..." : "Ödemeyi Simüle Et (test modu)"}
        </Button>
      ) : null}

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}
