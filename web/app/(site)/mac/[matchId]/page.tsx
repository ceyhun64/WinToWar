"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import { GameMap } from "@/components/game/GameMap";
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

const PAYOUT_STATUS_LABEL: Record<string, string> = {
  PayoutPending: "Bekliyor",
  PayoutSent: "Gönderildi",
  Completed: "Tamamlandı",
  Failed: "Başarısız",
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
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-6">
      <h1 className="text-lg font-semibold">Maç Özeti</h1>

      <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
        <div className="rounded-md border border-border bg-card p-3">
          <span className="text-xs text-muted-foreground">Durum</span>
          <p className="font-medium">{MATCH_STATUS_LABEL[match.status] ?? match.status}</p>
        </div>
        <div className="rounded-md border border-border bg-card p-3">
          <span className="text-xs text-muted-foreground">Süre</span>
          <p className="font-medium tabular-nums">{formatDuration(match.startedAtUtc, match.completedAtUtc)}</p>
        </div>
        <div className="rounded-md border border-border bg-card p-3 sm:col-span-2">
          <span className="text-xs text-muted-foreground">Kazanan</span>
          <p className="font-medium">{winnerNames || "—"}</p>
        </div>
      </div>

      <GameMap
        map={map}
        state={match}
        myPlayerId=""
        selectedRegionId={null}
        onSelectRegion={() => {}}
        onAttack={() => {}}
      />

      {payout ? (
        <div className="rounded-md border border-border bg-card p-4">
          <h2 className="text-sm font-semibold">Ödül Dağıtımı</h2>
          <ul className="mt-2 flex flex-col gap-1 text-sm">
            {payout.recipients.map((r) => (
              <li key={r.winnerPlayerId} className="flex items-center justify-between">
                <span className="text-muted-foreground">
                  {match.players.find((p) => p.id === r.winnerPlayerId)?.name ?? r.winnerPlayerId}
                </span>
                <span className="tabular-nums">
                  {r.amountLtc} LTC · {PAYOUT_STATUS_LABEL[r.status] ?? r.status}
                </span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <Link href={`/destek?matchId=${matchId}`} className="text-sm text-muted-foreground underline">
        Bu maçla ilgili itiraz et
      </Link>
    </div>
  );
}
