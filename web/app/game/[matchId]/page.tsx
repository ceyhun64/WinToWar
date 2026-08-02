"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ActionPanel } from "@/components/game/ActionPanel";
import { GameMap } from "@/components/game/GameMap";
import { Hud } from "@/components/game/Hud";
import { getGameConfig, getMap } from "@/lib/game/api";
import { useGameStore } from "@/lib/game/store";
import type { GameConfigDto, MapDto } from "@/lib/game/types";

interface GamePageProps {
  params: Promise<{ matchId: string }>;
}

export default function GamePage({ params }: GamePageProps) {
  const { matchId } = use(params);
  const router = useRouter();

  const [playerId, setPlayerId] = useState<string | null | undefined>(undefined);
  const [map, setMap] = useState<MapDto | null>(null);
  const [config, setConfig] = useState<GameConfigDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedRegionId, setSelectedRegionId] = useState<string | null>(null);

  useEffect(() => {
    setPlayerId(window.localStorage.getItem(`porsuk:match:${matchId}:playerId`));
  }, [matchId]);

  useEffect(() => {
    Promise.all([getMap(), getGameConfig()])
      .then(([mapDto, configDto]) => {
        setMap(mapDto);
        setConfig(configDto);
      })
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
          onClick={() => router.push("/game")}
        >
          Lobiye dön
        </button>
      </div>
    );
  }

  if (loadError) {
    return <div className="flex flex-1 items-center justify-center text-sm text-destructive">{loadError}</div>;
  }

  if (!map || !config || !store.state) {
    return (
      <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
        Maça bağlanılıyor...
      </div>
    );
  }

  const { state } = store;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-4 px-4 py-4">
      <Hud state={state} myPlayerId={playerId} />

      {state.status === "WaitingForPlayers" ? (
        <div className="rounded-md border border-border bg-card px-4 py-6 text-center text-sm text-muted-foreground">
          Rakip bekleniyor. Maç kodu: <span className="font-mono font-medium">{matchId}</span>
        </div>
      ) : null}

      {state.status === "Finished" ? (
        <div className="rounded-md border border-border bg-card px-4 py-6 text-center text-sm font-medium">
          {state.winnerId
            ? state.winnerId === playerId
              ? "Kazandınız!"
              : `Kazanan: ${state.players.find((p) => p.id === state.winnerId)?.name ?? "Rakip"}`
            : "Maç berabere bitti."}
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-[1fr_320px]">
        <GameMap
          map={map}
          state={state}
          myPlayerId={playerId}
          selectedRegionId={selectedRegionId}
          onSelectRegion={setSelectedRegionId}
        />
        <ActionPanel
          map={map}
          state={state}
          config={config}
          myPlayerId={playerId}
          selectedRegionId={selectedRegionId}
          onTrainSoldier={store.trainSoldier}
          onTrainGeneral={store.trainGeneral}
          onUpgradeNest={store.upgradeNest}
          onAttack={store.attackRegion}
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
