"use client";

import { Castle, Crown, Users, Coins, Shield, Eye, EyeOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import type { RoomSummary } from "@/lib/game/api";
import type { RoomType } from "@/lib/game/types";

/**
 * `docs/04-style.md` Landing İstisnası pilotu — 3. tur geri bildirim: yatay,
 * tek satırlık "liste öğesi" hâlâ dashboard hissi veriyordu. Oda kartı artık
 * dikey, ikon üstte, ızgarada yan yana dizilen bir "oyun karosu" (bkz.
 * `lobi/page.tsx` — kartlar `grid` içinde). Gösterilen her alan
 * `RoomSummary`'den gelen gerçek veridir, uydurma metin yok.
 */
export function RoomCard({
  room,
  type,
  busy,
  onJoin,
}: {
  room: RoomSummary;
  type: RoomType;
  busy: boolean;
  onJoin: () => void;
}) {
  const Icon = type === "Vip" ? Crown : Castle;
  const accent = type === "Vip" ? "#F5B942" : "#38BDF8";

  return (
    <Card className="items-center gap-3 rounded-2xl border border-border bg-card p-5 text-center backdrop-blur-md">
      <span
        className="flex size-12 shrink-0 items-center justify-center rounded-xl"
        style={{ backgroundColor: `${accent}26`, color: accent }}
      >
        <Icon className="size-6" aria-hidden="true" />
      </span>

      <span className="font-semibold">{room.roomName}</span>

      <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
        <span className="flex items-center gap-1">
          <Users className="size-3.5" aria-hidden="true" />
          {room.playerCount}/{room.maxPlayers}
        </span>
        <span className="flex items-center gap-1">
          <Coins className="size-3.5" aria-hidden="true" />${room.entryFeeUsd}
        </span>
        <span className="flex items-center gap-1">
          <Shield className="size-3.5" aria-hidden="true" />
          {room.greyRegionDefenseCount}
        </span>
        <span className="flex items-center gap-1">
          {room.fogOfWar ? <EyeOff className="size-3.5" aria-hidden="true" /> : <Eye className="size-3.5" aria-hidden="true" />}
          {room.fogOfWar ? "Sisli" : "Açık harita"}
        </span>
      </div>

      {/* docs/04-style.md Bölüm 5: sayfada en fazla 1 primary buton — o da mod karolarındaki seçim değil, gerçek bir aksiyon olmadığından burası outline kalır. */}
      <Button variant="default" className="" disabled={busy} onClick={onJoin}>
        Katıl
      </Button>
    </Card>
  );
}
