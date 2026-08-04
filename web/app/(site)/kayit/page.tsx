"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
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
        <label className="text-sm font-medium" htmlFor="displayName">
          Görünen ad
        </label>
        <input
          id="displayName"
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          placeholder="Adınız"
        />
      </div>

      <div className="flex flex-col gap-2">
        <label className="flex items-start gap-2 text-sm text-muted-foreground">
          <input
            type="checkbox"
            className="mt-0.5"
            checked={ageConfirmed}
            onChange={(e) => setAgeConfirmed(e.target.checked)}
          />
          18 yaşından büyüğüm.
        </label>
        <label className="flex items-start gap-2 text-sm text-muted-foreground">
          <input
            type="checkbox"
            className="mt-0.5"
            checked={termsAccepted}
            onChange={(e) => setTermsAccepted(e.target.checked)}
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
        </label>
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <Button onClick={handleSubmit}>Kayıt Ol</Button>

      <p className="text-center text-xs text-muted-foreground">
        Zaten hesabınız var mı? <Link href="/giris" className="underline">Giriş yapın</Link>
      </p>
    </div>
  );
}
