"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getOrCreatePlayerId, isSignedIn, setStoredDisplayName } from "@/lib/identity";

/**
 * docs/07-pages.md "Yaş/onay notu": "18 yaşından büyüğüm" + "Kullanım Şartları'nı
 * ve Gizlilik Politikası'nı kabul ediyorum" onay kutuları kayıt formunun zorunlu
 * bir parçasıdır (ayrı bir sayfa değil). En basit haliyle (checkbox) başlanır —
 * ❓ resmi kimlik doğrulama (KYC) gerekip gerekmediği müşteriye doğrulatılmalı.
 */
export default function KayitPage() {
  const router = useRouter();
  const [displayName, setDisplayName] = useState("");
  const [ageConfirmed, setAgeConfirmed] = useState(false);
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isSignedIn()) {
      router.replace("/lobi");
    }
  }, [router]);

  function handleSubmit() {
    if (!displayName.trim()) {
      setError("Görünen adınızı girin.");
      return;
    }
    if (!ageConfirmed || !termsAccepted) {
      setError("Devam etmek için her iki onay kutusunu da işaretlemelisiniz.");
      return;
    }
    getOrCreatePlayerId();
    setStoredDisplayName(displayName.trim());
    router.push("/lobi");
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-6 px-4 py-16">
      <div>
        <h1 className="text-lg font-semibold">Kayıt Ol</h1>
        <p className="text-sm text-muted-foreground">Birkaç saniyede hesabını oluştur.</p>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="displayName">Görünen ad</Label>
        <Input
          id="displayName"
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          placeholder="Adınız"
        />
      </div>

      <div className="flex flex-col gap-2">
        <Label className="items-start gap-2 font-normal text-muted-foreground">
          <Checkbox
            className="mt-0.5"
            checked={ageConfirmed}
            onCheckedChange={(checked) => setAgeConfirmed(checked)}
          />
          18 yaşından büyüğüm.
        </Label>
        <Label className="items-start gap-2 font-normal text-muted-foreground">
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

      <Button onClick={handleSubmit}>Kayıt Ol</Button>

      <p className="text-center text-xs text-muted-foreground">
        Zaten hesabınız var mı? <Link href="/giris" className="underline">Giriş yapın</Link>
      </p>
    </div>
  );
}
