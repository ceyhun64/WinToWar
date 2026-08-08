"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { KeyRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { CardContent } from "@/components/ui/card";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { resetPassword, AuthApiError } from "@/lib/auth/api";

interface SifreSifirlaPageProps {
  params: Promise<{ token: string }>;
}

/**
 * docs/11-auth.md Bölüm 3.4/6: tek kullanımlık, süreli token — sunucu doğrular,
 * geçersiz/kullanılmış/süresi dolmuşsa açık bir hata gösterilir (sahte bir
 * "başarılı" ekranı gösterilmez, bkz. 01-workflow-rules.md Bölüm 0.4).
 */
export default function SifreSifirlaPage({ params }: SifreSifirlaPageProps) {
  const { token } = use(params);
  const router = useRouter();
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

  async function handleSubmit() {
    if (newPassword.length < 8) {
      setError("Şifre en az 8 karakter olmalı.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Şifreler eşleşmiyor.");
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await resetPassword(token, newPassword);
      setDone(true);
      window.setTimeout(() => router.push("/giris"), 1500);
    } catch (err) {
      if (err instanceof AuthApiError && (err.code === "INVALID_TOKEN" || err.code === "TOKEN_EXPIRED")) {
        setError("Bu bağlantı geçersiz veya süresi dolmuş. Yeni bir sıfırlama bağlantısı isteyin.");
      } else {
        setError(err instanceof Error ? err.message : "Şifre sıfırlanamadı.");
      }
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
        <h1 className="text-lg font-semibold">Şifreniz güncellendi</h1>
        <p className="text-sm text-muted-foreground">Girişe yönlendiriliyorsunuz...</p>
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-6 px-4 py-16">
      <PageHero icon={KeyRound} title="Yeni Şifre Belirle" subtitle="Hesabınız için yeni bir şifre girin." />

      <GameCard>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-3">
            <Label htmlFor="newPassword">Yeni şifre</Label>
            <Input
              id="newPassword"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="En az 8 karakter"
            />
          </div>
          <div className="flex flex-col gap-3">
            <Label htmlFor="confirmPassword">Yeni şifre (tekrar)</Label>
            <Input
              id="confirmPassword"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="Şifrenizi tekrar girin"
              onKeyDown={(e) => e.key === "Enter" && handleSubmit()}
            />
          </div>
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          <Button disabled={busy} onClick={handleSubmit}>
            {busy ? "Kaydediliyor..." : "Şifreyi Güncelle"}
          </Button>
        </CardContent>
      </GameCard>

      <Link href="/giris" className="text-center text-xs text-muted-foreground underline">
        Girişe dön
      </Link>
    </div>
  );
}
