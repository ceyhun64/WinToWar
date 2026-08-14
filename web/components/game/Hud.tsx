"use client";

import { playerFillColor } from "@/lib/game/colors";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import type { GameConfigDto, MatchStateDto } from "@/lib/game/types";

interface HudProps {
  state: MatchStateDto;
  myPlayerId: string;
  gameConfig: GameConfigDto;
}

/**
 * docs/03-game-rules.md Bölüm 4 (müşteri kararıyla güncellendi): artık tek kaynaklı
 * değil — sahip olunan HER bölge (Ana Kale dahil), Ana Kale ile birebir aynı oranda
 * (`baseProductionPerInterval`) kendi askerini üretir. Toplam üretim, bölge sayısı ×
 * bu oran. Değerler backend'in tek doğruluk kaynağı olan GameConfig'ten (bkz.
 * lib/game/api.ts getGameConfig) okunur, burada tekrar sabitlenmez.
 */
export function Hud({ state, myPlayerId, gameConfig }: HudProps) {
  const myRegions = state.regions.filter((r) => r.ownerId === myPlayerId);
  const myProduction = myRegions.length * gameConfig.baseProductionPerInterval;

  return (
    <Card size="sm" className="flex-row items-center justify-between gap-4 px-4">
      <div className="flex items-center gap-4">
        {state.players.map((player) => {
          const regionCount = state.regions.filter((r) => r.ownerId === player.id).length;
          const isMe = player.id === myPlayerId;
          return (
            <div key={player.id} className="flex items-center gap-3">
              <span
                className="inline-block size-3 rounded-sm"
                style={{
                  backgroundColor: playerFillColor({
                    roomType: state.room.type,
                    ownerId: player.id,
                    ownerSlot: player.slot,
                  }),
                }}
              />
              <div className="flex flex-col leading-tight">
                <span className="text-sm font-medium">
                  {player.name}
                  {isMe ? " (sen)" : ""}
                </span>
                <span className="text-xs text-muted-foreground">{regionCount} bölge</span>
              </div>
              {/* docs/03-game-rules.md Bölüm 7: rakip bot olduğunda her zaman açıkça
                  belirtilir — insan olduğu izlenimi hiçbir şekilde verilmez. */}
              {player.isBot ? <Badge variant="secondary">Bot</Badge> : null}
              {player.isEliminated ? (
                <Badge variant="destructive">Elendi</Badge>
              ) : !player.isConnected && !player.isBot ? (
                <Badge variant="outline">Bağlı değil</Badge>
              ) : null}
            </div>
          );
        })}
      </div>
      {/* docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 25: üretim rakamı/kuralı aynı
          (yalnızca sunum) — daha oyun içi bir "+X / Ys" biçimine kısaltıldı. */}
      <div className="text-sm font-medium tabular-nums">
        {state.status === "Completed" || state.status === "Cancelled"
          ? "Maç bitti"
          : state.status === "Playing"
            ? `+${myProduction} / ${gameConfig.productionIntervalSeconds}s`
            : `${state.lobbyConfirmedCount}/${state.room.maxPlayers} oyuncu`}
      </div>
    </Card>
  );
}
