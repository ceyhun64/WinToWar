"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ActionPanel } from "@/components/game/ActionPanel";
import { GameMap } from "@/components/game/GameMap";
import { Hud } from "@/components/game/Hud";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
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
  const [gameConfig, setGameConfig] = useState<GameConfigDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedRegionId, setSelectedRegionId] = useState<string | null>(null);

  useEffect(() => {
    setPlayerId(window.localStorage.getItem(`wintowar:match:${matchId}:playerId`));
  }, [matchId]);

  useEffect(() => {
    getMap()
      .then(setMap)
      .catch((err) => setLoadError(String(err)));
    getGameConfig()
      .then(setGameConfig)
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

  if (!map || !gameConfig || !store.state) {
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
      <Hud state={state} myPlayerId={playerId} gameConfig={gameConfig} />

      {/* docs/08-page-content.md Bölüm 3.8: bağlantı durumu içeriği — sunucu-otoriter
          mimaride bu bant/uyarı yalnızca istemcinin senkron olmadığını gösterir,
          harita/HUD'u gizlemez, oyun kuralı/ikna metni içermez. */}
      {store.connectionStatus === "reconnecting" ? (
        <div className="rounded-2xl border border-border bg-muted/40 px-4 py-2 text-center text-sm text-muted-foreground">
          Bağlantı kesildi, yeniden bağlanılıyor…
        </div>
      ) : null}

      {store.connectionStatus === "disconnected" ? (
        <div className="flex flex-col items-center gap-3 rounded-2xl border border-destructive/40 bg-card px-4 py-3 text-center text-sm">
          <p>Maçınız sunucuda devam ediyor, bağlantınızı yeniden kurun.</p>
          <Button size="sm" onClick={() => store.reconnect()}>
            Yeniden Bağlan
          </Button>
        </div>
      ) : null}

      {state.status === "Lobby" || state.status === "Countdown" ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 text-center text-sm text-muted-foreground">
            {state.status === "Countdown" && state.countdownRemainingSeconds !== null ? (
              <p>{`Lobi doldu, maç ${state.countdownRemainingSeconds}sn içinde başlıyor.`}</p>
            ) : (
              <>
                {/* docs/08-page-content.md Bölüm 1.4/3.4: "Ali / Mehmet · 3/4" gibi somut,
                    dolan/boş slotları isimle gösteren bir liste — jenerik bir sayaç yerine.
                    docs/03-game-rules.md Bölüm 7: bot olan koltuklar burada da (masa
                    listesinde) açıkça "Bot" rozetiyle işaretlenir. */}
                <p className="flex flex-wrap items-center justify-center gap-x-1 gap-y-1 text-foreground">
                  {Array.from({ length: state.room.maxPlayers }, (_, slot) => {
                    const occupant = state.players.find((p) => p.slot === slot);
                    return (
                      <span key={slot} className="inline-flex items-center gap-1">
                        {slot > 0 ? <span className="text-muted-foreground">·</span> : null}
                        <span>{occupant ? occupant.name : "Bekleniyor…"}</span>
                        {occupant?.isBot ? <Badge variant="secondary">Bot</Badge> : null}
                      </span>
                    );
                  })}
                </p>
                <p>{`${state.lobbyConfirmedCount}/${state.room.maxPlayers} oyuncu`}</p>
                {state.room.maxPlayers - state.lobbyConfirmedCount === 1 ? (
                  <p className="font-medium text-foreground">Son oyuncu bekleniyor</p>
                ) : null}
              </>
            )}
            <p>
              Maç kodu: <span className="font-mono font-medium">{matchId}</span>
            </p>
            {store.lobbyTimeoutReached ? (
              <p className="text-xs text-muted-foreground">
                Eşleşme süresi doldu — beklemeye devam edebilir ya da ayrılıp ödemenizi iade alabilirsiniz.
              </p>
            ) : null}
            {state.status === "Lobby" ? (
              <div className="flex flex-wrap items-center justify-center gap-3">
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
          </CardContent>
        </Card>
      ) : null}

      {state.status === "Cancelled" ? (
        <Card>
          <CardContent className="text-center text-sm font-medium">
            Lobi zaman aşımına uğradı, ödemeniz iade edildi.
          </CardContent>
        </Card>
      ) : null}

      {state.status === "Completed" ? (
        <Card>
          <CardContent className="text-center text-sm font-medium">
            {isWinner ? "Kazandınız!" : `Kazanan${state.winners.length > 1 ? "lar" : ""}: ${winnerNames}`}
          </CardContent>
        </Card>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-[1fr_320px]">
        <GameMap
          map={map}
          state={state}
          myPlayerId={playerId}
          selectedRegionId={selectedRegionId}
          movementDurationSeconds={gameConfig.movementDurationSeconds}
          onSelectRegion={setSelectedRegionId}
          onAttack={store.attackRegion}
        />
        <ActionPanel
          map={map}
          state={state}
          myPlayerId={playerId}
          selectedRegionId={selectedRegionId}
          gameConfig={gameConfig}
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
