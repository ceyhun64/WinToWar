"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import { ScrollText, Activity, Clock, Trophy, Coins } from "lucide-react";
import { GameMap } from "@/components/game/GameMap";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHero } from "@/components/layout/PageHero";
import { StatCard } from "@/components/layout/StatCard";
import { SectionTitle } from "@/components/layout/SectionTitle";
import { GameCard } from "@/components/layout/GameCard";
import { getMap, getMatchSnapshot } from "@/lib/game/api";
import type { MapDto, MatchStateDto } from "@/lib/game/types";
import { getPayoutSummary } from "@/lib/payments/api";
import type { PayoutSummaryDto } from "@/lib/payments/types";

interface MacPageProps {
  params: Promise<{ matchId: string }>;
}

const MATCH_STATUS_LABEL: Record<string, string> = {
  Lobby: "Lobide",
  Countdown: "Geri Sayımda",
  Playing: "Devam Ediyor",
  Completed: "Tamamlandı",
  Cancelled: "İptal Edildi",
};

function formatDuration(startedAtUtc: string | null, completedAtUtc: string | null): string {
  if (!startedAtUtc || !completedAtUtc) {
    return "—";
  }
  const seconds = Math.max(0, Math.round((new Date(completedAtUtc).getTime() - new Date(startedAtUtc).getTime()) / 1000));
  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${(seconds % 60).toString().padStart(2, "0")}`;
}

/** docs/07-pages.md `/mac/[matchId]`: bir maçın özet detayı — harita, süre, kazanan, net ödül. */
export default function MacPage({ params }: MacPageProps) {
  const { matchId } = use(params);
  const [map, setMap] = useState<MapDto | null>(null);
  const [match, setMatch] = useState<MatchStateDto | null | undefined>(undefined);
  const [payout, setPayout] = useState<PayoutSummaryDto | null>(null);

  useEffect(() => {
    getMap().then(setMap);
    getMatchSnapshot(matchId)
      .then(setMatch)
      .catch(() => setMatch(null));
    getPayoutSummary(matchId).then(setPayout);
  }, [matchId]);

  if (match === undefined || !map) {
    return <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">Yükleniyor...</div>;
  }

  if (match === null) {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
        <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#94A3B822", color: "#94A3B8" }}>
          <ScrollText className="size-6" aria-hidden="true" />
        </span>
        <h1 className="text-lg font-semibold">Maç bulunamadı</h1>
        <Link href="/gecmis" className="text-sm underline">
          Geçmişe dön
        </Link>
      </div>
    );
  }

  const winnerNames = match.winners
    .map((id) => match.players.find((p) => p.id === id)?.name ?? id)
    .join(", ");

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-8 px-4 py-8 md:py-10">
      <PageHero icon={ScrollText} title="Maç Özeti" />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard icon={Activity} label="Durum" value={MATCH_STATUS_LABEL[match.status] ?? match.status} />
        <StatCard icon={Clock} label="Süre" value={formatDuration(match.startedAtUtc, match.completedAtUtc)} />
        <div className="col-span-2">
          <StatCard icon={Trophy} label="Kazanan" value={winnerNames || "—"} accent="#F5B942" />
        </div>
      </div>

      <GameMap
        map={map}
        state={match}
        myPlayerId=""
        selectedRegionId={null}
        // Biten bir maçın anlık görüntüsünde state.armies zaten boştur (bkz.
        // 07-pages.md Non-Goals — hamle hamle replay yok), bu değer hiç kullanılmaz.
        movementDurationSeconds={1}
        onSelectRegion={() => {}}
        onAttack={() => {}}
      />

      {payout ? (
        <section className="flex flex-col gap-3">
          <SectionTitle>Ödül Dağıtımı</SectionTitle>
          <GameCard>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Coins className="size-4" style={{ color: "#F5B942" }} aria-hidden="true" />
                Kazananlara Dağıtım
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ul className="flex flex-col gap-3 text-sm">
                {payout.recipients.map((r) => (
                  <li key={r.winnerPlayerId} className="flex items-center justify-between">
                    <span className="text-muted-foreground">
                      {match.players.find((p) => p.id === r.winnerPlayerId)?.name ?? r.winnerPlayerId}
                    </span>
                    <span className="tabular-nums">${r.amountUsd} bakiyeye eklendi</span>
                  </li>
                ))}
              </ul>
            </CardContent>
          </GameCard>
        </section>
      ) : null}

      <Link href={`/destek?matchId=${matchId}`} className="text-sm text-muted-foreground underline">
        Bu maçla ilgili itiraz et
      </Link>
    </div>
  );
}
