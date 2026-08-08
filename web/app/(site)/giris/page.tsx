"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { LogIn } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { CardContent } from "@/components/ui/card";
import { ensureSessionLoaded, isSignedIn } from "@/lib/identity";
import { login } from "@/lib/auth/api";
import { GoogleContinueButton } from "@/components/auth/GoogleContinueButton";

/** docs/11-auth.md Bölüm 3.3/6: gerçek e-posta/parola girişi + Google ile devam et. */
export default function GirisPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    ensureSessionLoaded().then(() => {
      if (isSignedIn()) {
        router.replace("/lobi");
      }
    });
  }, [router]);

  async function handleSubmit() {
    if (!email.trim() || !password) {
      setError("E-posta ve şifrenizi girin.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
      router.push("/lobi");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Giriş başarısız oldu.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-6 px-4 py-16">
      <PageHero icon={LogIn} title="Giriş" subtitle="Devam etmek için e-posta ve şifrenizi girin." />

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
          <div className="flex flex-col gap-3">
            <Label htmlFor="password">Şifre</Label>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Şifreniz"
              onKeyDown={(e) => e.key === "Enter" && handleSubmit()}
            />
          </div>
          {error ? <p className="text-sm text-destructive">{error}</p> : null}

          <Button disabled={busy} onClick={handleSubmit}>
            {busy ? "Giriş yapılıyor..." : "Giriş Yap"}
          </Button>

          <div className="flex items-center gap-3 text-xs text-muted-foreground">
            <span className="h-px flex-1 bg-border" />
            veya
            <span className="h-px flex-1 bg-border" />
          </div>

          <GoogleContinueButton onError={setError} onSuccess={() => router.push("/lobi")} />
        </CardContent>
      </GameCard>

      <div className="flex flex-col items-center gap-1 text-center text-xs text-muted-foreground">
        <p>
          Hesabınız yok mu? <Link href="/kayit" className="underline">Kayıt olun</Link>
        </p>
        <Link href="/sifremi-unuttum" className="underline">
          Şifremi unuttum
        </Link>
      </div>
    </div>
  );
}
