"use client";

import { colorForSlot } from "@/lib/game/colors";
import type { MatchStateDto } from "@/lib/game/types";

interface HudProps {
  state: MatchStateDto;
  myPlayerId: string;
}

function formatTime(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

export function Hud({ state, myPlayerId }: HudProps) {
  return (
    <div className="flex items-center justify-between gap-4 rounded-md border border-border bg-card px-4 py-2">
      <div className="flex items-center gap-4">
        {state.players.map((player) => {
          const regionCount = state.regions.filter((r) => r.ownerId === player.id).length;
          const isMe = player.id === myPlayerId;
          return (
            <div key={player.id} className="flex items-center gap-2">
              <span
                className="inline-block size-3 rounded-sm"
                style={{ backgroundColor: colorForSlot(player.slot) }}
              />
              <div className="flex flex-col leading-tight">
                <span className="text-sm font-medium">
                  {player.name}
                  {isMe ? " (sen)" : ""}
                  {player.isEliminated ? " — elendi" : !player.isConnected ? " — bağlı değil" : ""}
                </span>
                <span className="text-xs text-muted-foreground">
                  {isMe ? `${player.gold} altın · ` : ""}
                  {regionCount} bölge
                </span>
              </div>
            </div>
          );
        })}
      </div>
      <div className="text-sm font-medium tabular-nums">
        {state.status === "Finished" ? "Maç bitti" : formatTime(state.remainingSeconds)}
      </div>
    </div>
  );
}
