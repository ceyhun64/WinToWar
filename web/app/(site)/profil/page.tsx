"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getWalletBalance } from "@/lib/payments/api";
import { getStoredDisplayName, isSignedIn } from "@/lib/identity";

/**
 * docs/07-pages.md `/profil`: kullanıcı bilgisi, geçmiş maçlar. Salt okunur.
 * 🛠️ Backend'de henüz kalıcı bir maç geçmişi kaydı yok (oyun state'i bellekte,
 * maç bitince tutulmuyor) — bu yüzden maç geçmişi bölümü dürüstçe Empty state
 * gösterir; ödeme geçmişi için bkz. `/gecmis` (gerçek veriye dayanır).
 */
export default function ProfilPage() {
  const router = useRouter();
  const [displayName, setDisplayName] = useState<string | null>(null);
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    setDisplayName(getStoredDisplayName());
    getWalletBalance()
      .then((dto) => setBalanceUsd(dto.balanceUsd))
      .catch(() => setBalanceUsd(null));
  }, [router]);

  if (!displayName) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-6 px-4 py-8">
      <div className="rounded-md border border-border bg-card p-4">
        <h1 className="text-sm font-medium text-muted-foreground">Görünen ad</h1>
        <p className="text-lg font-semibold">{displayName}</p>
        <p className="mt-2 text-sm text-muted-foreground">Bakiye: ${balanceUsd ?? "—"}</p>
      </div>

      <div>
        <h2 className="text-sm font-semibold">Geçmiş Maçlar</h2>
        <p className="mt-2 text-sm text-muted-foreground">Henüz tamamlanmış bir maçınız yok.</p>
      </div>
    </div>
  );
}
