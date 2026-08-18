"use client";

import { useState } from "react";
import Link from "next/link";
import { KeyRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { CardContent } from "@/components/ui/card";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { forgotPassword } from "@/lib/auth/api";

/**
 * docs/11-auth.md Bölüm 3.4/6: e-posta girilir, sunucu her zaman aynı ("gönderildiyse
 * kontrol edin") yanıtı döner — hesap enumeration'ı önlemek için e-postanın
 * gerçekten kayıtlı olup olmadığı burada asla belli edilmez.
 */
export default function SifremiUnuttumPage() {
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [busy, setBusy] = useState(false);

  async function handleSubmit() {
    if (!email.trim()) return;
    setBusy(true);
    try {
      await forgotPassword(email.trim());
    } finally {
      setBusy(false);
      setSubmitted(true);
    }
  }

  if (submitted) {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
        <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#38BDF822", color: "#38BDF8" }}>
          <KeyRound className="size-6" aria-hidden="true" />
        </span>
        <h1 className="text-lg font-semibold">E-postanızı kontrol edin</h1>
        <p className="text-sm text-muted-foreground">
          Bu e-posta kayıtlıysa, şifre sıfırlama bağlantısı gönderildi. Bağlantı kısa süre içinde geçerliliğini yitirir.
        </p>
        <Link href="/giris" className="text-sm font-medium underline">
          Girişe dön
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-6 px-4 py-(--gutter-16)">
      <PageHero icon={KeyRound} title="Şifremi Unuttum" subtitle="E-posta adresinize bir sıfırlama bağlantısı gönderelim." />

      <GameCard>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-3">
            <Label htmlFor="email">E-posta</Label>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="ornek@eposta.com"
              onKeyDown={(e) => e.key === "Enter" && handleSubmit()}
            />
          </div>
          <Button disabled={busy || !email.trim()} onClick={handleSubmit}>
            {busy ? "Gönderiliyor..." : "Sıfırlama Bağlantısı Gönder"}
          </Button>
        </CardContent>
      </GameCard>

      <Link href="/giris" className="text-center text-xs text-muted-foreground underline">
        Girişe dön
      </Link>
    </div>
  );
}
