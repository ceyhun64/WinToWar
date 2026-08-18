"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { User, Wallet, Trophy } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@/components/ui/empty";
import { PageHero } from "@/components/layout/PageHero";
import { StatCard } from "@/components/layout/StatCard";
import { SectionTitle } from "@/components/layout/SectionTitle";
import { GameCard } from "@/components/layout/GameCard";
import { getWalletBalance } from "@/lib/payments/api";
import { getStoredDisplayName } from "@/lib/identity";
import { AuthGuard } from "@/components/layout/AuthGuard";

/**
 * docs/07-pages.md `/profil`: kullanıcı bilgisi, geçmiş maçlar. Salt okunur.
 * 🛠️ Backend'de henüz kalıcı bir maç geçmişi kaydı yok (oyun state'i bellekte,
 * maç bitince tutulmuyor) — bu yüzden maç geçmişi bölümü dürüstçe Empty state
 * gösterir; ödeme geçmişi için bkz. `/gecmis` (gerçek veriye dayanır).
 *
 * `docs/04-style.md` Landing İstisnası — sitede genelleştirilmiş tasarım
 * sistemi: görsel dil "oyuncu profili" hissi hedefliyor ama gerçek bir
 * achievement/XP veri modeli backend'de yok — `01-workflow-rules.md` Bölüm
 * 0.4 (uydurma veri yasağı) gereği rozet/XP icat edilmedi; yalnızca var olan
 * gerçek veriler (görünen ad, bakiye) yeni kart dilinde gösteriliyor.
 */
export default function ProfilPage() {
  return (
    <AuthGuard>
      <ProfilPageContent />
    </AuthGuard>
  );
}

function ProfilPageContent() {
  const [displayName, setDisplayName] = useState<string | null>(null);
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);

  useEffect(() => {
    setDisplayName(getStoredDisplayName());
    getWalletBalance()
      .then((dto) => setBalanceUsd(dto.balanceUsd))
      .catch(() => setBalanceUsd(null));
  }, []);

  if (!displayName) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-8 px-4 py-8 md:py-10">
      <PageHero icon={User} title="Profil" subtitle="Oyuncu bilgilerin ve bakiyen." />

      <div className="grid grid-cols-1 gap-3 min-[22rem]:grid-cols-2">
        <StatCard icon={User} label="Görünen ad" value={displayName} accent="#38BDF8" />
        <StatCard icon={Wallet} label="Bakiye" value={`$${balanceUsd ?? "—"}`} accent="#F5B942" />
      </div>

      <section className="flex flex-col gap-3">
        <SectionTitle>Geçmiş Maçlar</SectionTitle>
        <GameCard className="p-2">
          <Empty className="p-6">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Trophy aria-hidden="true" />
              </EmptyMedia>
              <EmptyTitle>Henüz tamamlanmış bir maçınız yok</EmptyTitle>
              <EmptyDescription>Bir savaşa katıl, ilk zaferini burada gör.</EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button nativeButton={false} render={<Link href="/lobi" />}>Lobiye Git</Button>
            </EmptyContent>
          </Empty>
        </GameCard>
      </section>
    </div>
  );
}
