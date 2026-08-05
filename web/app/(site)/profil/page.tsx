"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
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
      <Card>
        <CardContent>
          <h1 className="text-sm font-medium text-muted-foreground">Görünen ad</h1>
          <p className="text-lg font-semibold">{displayName}</p>
          <p className="mt-2 text-sm text-muted-foreground">Bakiye: ${balanceUsd ?? "—"}</p>
        </CardContent>
      </Card>

      <Empty className="p-6">
        <EmptyHeader>
          <EmptyTitle>Geçmiş Maçlar</EmptyTitle>
          <EmptyDescription>Henüz tamamlanmış bir maçınız yok.</EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <Button render={<Link href="/lobi" />}>Lobiye Git</Button>
        </EmptyContent>
      </Empty>
    </div>
  );
}
