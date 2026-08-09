"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { UserPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { CardContent } from "@/components/ui/card";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { ensureSessionLoaded, isSignedIn } from "@/lib/identity";
import { register, AuthApiError } from "@/lib/auth/api";
import { GoogleContinueButton } from "@/components/auth/GoogleContinueButton";

/**
 * docs/07-pages.md "Yaş/onay notu": "18 yaşından büyüğüm" + "Kullanım Şartları'nı
 * ve Gizlilik Politikası'nı kabul ediyorum" onay kutuları kayıt formunun zorunlu
 * bir parçasıdır. docs/11-auth.md Bölüm 1.8: aynı onay Google ile ilk kayıtta da
 * (checkbox'lar dolu olarak) gereklidir.
 */
export default function KayitPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [ageConfirmed, setAgeConfirmed] = useState(false);
  const [termsAccepted, setTermsAccepted] = useState(false);
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
    if (!displayName.trim() || !email.trim() || !password) {
      setError("Tüm alanları doldurun.");
      return;
    }
    if (!ageConfirmed || !termsAccepted) {
      setError("Devam etmek için her iki onay kutusunu da işaretlemelisiniz.");
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await register({
        email: email.trim(),
        password,
        displayName: displayName.trim(),
        ageConfirmed,
        termsAccepted,
      });
      router.push("/lobi");
    } catch (err) {
      if (err instanceof AuthApiError && err.code === "EMAIL_ALREADY_EXISTS") {
        setError("Bu e-posta ile zaten bir hesap var. Giriş yapmayı deneyin.");
      } else {
        setError(err instanceof Error ? err.message : "Kayıt başarısız oldu.");
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4 px-4 py-6 sm:gap-6 sm:py-16">
      <PageHero icon={UserPlus} title="Kayıt Ol" subtitle="Birkaç saniyede hesabını oluştur." accent="#F5B942" />

      <GameCard>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-3">
            <Label htmlFor="displayName">Görünen ad</Label>
            <Input
              id="displayName"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="Adınız"
            />
          </div>

          <div className="flex flex-col gap-3">
            <Label htmlFor="email">E-posta</Label>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="ornek@eposta.com"
            />
          </div>

          <div className="flex flex-col gap-3">
            <Label htmlFor="password">Şifre</Label>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="En az 8 karakter"
            />
          </div>

          <div className="flex flex-col gap-3">
            <Label className="items-start gap-3 font-normal text-muted-foreground">
              <Checkbox
                className="mt-0.5"
                checked={ageConfirmed}
                onCheckedChange={(checked) => setAgeConfirmed(checked)}
              />
              18 yaşından büyüğüm.
            </Label>
            <Label className="items-start gap-3 font-normal text-muted-foreground">
              <Checkbox
                className="mt-0.5"
                checked={termsAccepted}
                onCheckedChange={(checked) => setTermsAccepted(checked)}
              />
              <span>
                <Link href="/kosullar" className="underline">
                  Kullanım Şartları
                </Link>
                &apos;nı ve{" "}
                <Link href="/gizlilik" className="underline">
                  Gizlilik Politikası
                </Link>
                &apos;nı kabul ediyorum.
              </span>
            </Label>
          </div>

          {error ? <p className="text-sm text-destructive">{error}</p> : null}

          <Button disabled={busy} onClick={handleSubmit}>
            {busy ? "Kayıt olunuyor..." : "Kayıt Ol"}
          </Button>

          <div className="flex items-center gap-3 text-xs text-muted-foreground">
            <span className="h-px flex-1 bg-border" />
            veya
            <span className="h-px flex-1 bg-border" />
          </div>

          <GoogleContinueButton
            onError={setError}
            onSuccess={() => router.push("/lobi")}
            consent={{ ageConfirmed, termsAccepted }}
          />
          {!ageConfirmed || !termsAccepted ? (
            <p className="text-center text-xs text-muted-foreground">
              Google ile devam etmeden önce de yukarıdaki onay kutularını işaretlemelisiniz.
            </p>
          ) : null}
        </CardContent>
      </GameCard>

      <p className="text-center text-xs text-muted-foreground">
        Zaten hesabınız var mı? <Link href="/giris" className="underline">Giriş yapın</Link>
      </p>
    </div>
  );
}
