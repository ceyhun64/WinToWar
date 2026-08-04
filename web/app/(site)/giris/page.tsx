"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { getOrCreatePlayerId, getStoredDisplayName, isSignedIn, setStoredDisplayName } from "@/lib/identity";

/**
 * 🛠️ Auth yöntemi müşteri tarafından netleştirilmedi (bkz. docs/07-pages.md ❓).
 * Backend'de henüz bir kullanıcı/parola sistemi yok — giriş, tarayıcıya kayıtlı
 * kalıcı kimliği (bkz. lib/identity.ts) bir görünen adla ilişkilendirir. Gerçek
 * auth eklendiğinde bu sayfa email/parola formuna dönüşür, geri kalan kod
 * (Wallet/oda entegrasyonu) değişmeden çalışmaya devam eder.
 */
export default function GirisPage() {
  const router = useRouter();
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isSignedIn()) {
      router.replace("/lobi");
      return;
    }
    setDisplayName(getStoredDisplayName() ?? "");
  }, [router]);

  function handleSubmit() {
    if (!displayName.trim()) {
      setError("Görünen adınızı girin.");
      return;
    }
    getOrCreatePlayerId();
    setStoredDisplayName(displayName.trim());
    router.push("/lobi");
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-6 px-4 py-16">
      <div>
        <h1 className="text-lg font-semibold">Giriş</h1>
        <p className="text-sm text-muted-foreground">Devam etmek için görünen adınızı girin.</p>
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
        {error ? <p className="text-sm text-destructive">{error}</p> : null}
      </div>

      <Button onClick={handleSubmit}>Giriş Yap</Button>

      <p className="text-center text-xs text-muted-foreground">
        Hesabınız yok mu? <Link href="/kayit" className="underline">Kayıt olun</Link>
      </p>
    </div>
  );
}
