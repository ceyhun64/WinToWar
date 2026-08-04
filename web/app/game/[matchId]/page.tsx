"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ActionPanel } from "@/components/game/ActionPanel";
import { GameMap } from "@/components/game/GameMap";
import { Hud } from "@/components/game/Hud";
import { Button } from "@/components/ui/button";
import { getMap } from "@/lib/game/api";
import { useGameStore } from "@/lib/game/store";
import type { MapDto } from "@/lib/game/types";

interface GamePageProps {
  params: Promise<{ matchId: string }>;
}

export default function GamePage({ params }: GamePageProps) {
  const { matchId } = use(params);
  const router = useRouter();

  const [playerId, setPlayerId] = useState<string | null | undefined>(undefined);
  const [map, setMap] = useState<MapDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedRegionId, setSelectedRegionId] = useState<string | null>(null);

  useEffect(() => {
    setPlayerId(window.localStorage.getItem(`wintowar:match:${matchId}:playerId`));
  }, [matchId]);

  useEffect(() => {
    getMap()
      .then(setMap)
      .catch((err) => setLoadError(String(err)));
  }, []);

  const store = useGameStore(matchId, playerId ?? "");

  if (playerId === undefined) {
    return null;
  }

  if (playerId === null) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 text-center">
        <p className="text-sm text-muted-foreground">
          Bu maça ait bir oyuncu oturumu bulunamadı. Lütfen lobiden maça katılın.
        </p>
        <button
          className="text-sm font-medium underline"
          onClick={() => router.push("/lobi")}
        >
          Lobiye dön
        </button>
      </div>
    );
  }

  if (loadError) {
    return <div className="flex flex-1 items-center justify-center text-sm text-destructive">{loadError}</div>;
  }

  if (!map || !store.state) {
    return (
      <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
        Maça bağlanılıyor...
      </div>
    );
  }

  const { state } = store;
  const isWinner = state.status === "Completed" && state.winners.includes(playerId);
  const winnerNames = state.winners
    .map((id) => state.players.find((p) => p.id === id)?.name ?? "Bilinmeyen")
    .join(", ");

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-4 px-4 py-4">
      <Hud state={state} myPlayerId={playerId} />

      {state.status === "Lobby" || state.status === "Countdown" ? (
        <div className="flex flex-col items-center gap-3 rounded-md border border-border bg-card px-4 py-6 text-center text-sm text-muted-foreground">
          <p>
            {state.status === "Countdown" && state.countdownRemainingSeconds !== null
              ? `Lobi doldu, maç ${state.countdownRemainingSeconds}sn içinde başlıyor.`
              : `Diğer oyuncular bekleniyor (${state.lobbyConfirmedCount}/${state.room.maxPlayers}).`}
          </p>
          <p>
            Maç kodu: <span className="font-mono font-medium">{matchId}</span>
          </p>
          {store.lobbyTimeoutReached ? (
            <p className="text-xs text-muted-foreground">
              Eşleşme süresi doldu — beklemeye devam edebilir ya da ayrılıp ödemenizi iade alabilirsiniz.
            </p>
          ) : null}
          {state.status === "Lobby" ? (
            <div className="flex flex-wrap items-center justify-center gap-2">
              <Button variant="outline" size="sm" onClick={() => store.leaveLobby()}>
                {store.lobbyTimeoutReached ? "İptal Et / Bakiyeyi İade Et" : "Lobiden Ayrıl"}
              </Button>
              {state.room.type === "Vip" && state.room.creatorPlayerId === playerId ? (
                <Button size="sm" onClick={() => store.startVipMatchNow()}>
                  Şimdi Başlat
                </Button>
              ) : null}
            </div>
          ) : null}
        </div>
      ) : null}

      {state.status === "Cancelled" ? (
        <div className="rounded-md border border-border bg-card px-4 py-6 text-center text-sm font-medium">
          Lobi zaman aşımına uğradı, ödemeniz iade edildi.
        </div>
      ) : null}

      {state.status === "Completed" ? (
        <div className="rounded-md border border-border bg-card px-4 py-6 text-center text-sm font-medium">
          {isWinner ? "Kazandınız!" : `Kazanan${state.winners.length > 1 ? "lar" : ""}: ${winnerNames}`}
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-[1fr_320px]">
        <GameMap
          map={map}
          state={state}
          myPlayerId={playerId}
          selectedRegionId={selectedRegionId}
          onSelectRegion={setSelectedRegionId}
          onAttack={store.attackRegion}
        />
        <ActionPanel
          map={map}
          state={state}
          myPlayerId={playerId}
          selectedRegionId={selectedRegionId}
        />
      </div>

      {store.error ? (
        <div className="fixed bottom-4 left-1/2 -translate-x-1/2 rounded-md border border-destructive/40 bg-card px-4 py-2 text-sm text-destructive shadow-sm">
          {store.error}
        </div>
      ) : null}
    </div>
  );
}
