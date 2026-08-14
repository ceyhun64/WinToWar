"use client";

import { NEUTRAL_COLOR, playerFillColor } from "@/lib/game/colors";
import type { MatchStateDto } from "@/lib/game/types";

interface TerritoryControlBarProps {
  state: MatchStateDto;
  myPlayerId: string;
}

/**
 * docs/16-state.io-gorsel-referans.md Bölüm 1.1/4: haritanın hemen üstünde, tam
 * genişlikte, uçları yuvarlatılmış tek bir hap — segment genişlikleri o anki bölge
 * sayısı dağılımıyla orantılı, bir "skor çubuğu" değil gerçek zamanlı toprak oranı
 * göstergesi. Yerel oyuncu her zaman en solda (kendi rengiyle), ardından diğer
 * oyuncular slot sırasıyla, sahipsiz/nötr bölge oranı en sağda.
 *
 * Segment renkleri `playerFillColor` üzerinden çözülür — docs/03-game-rules.md güncel
 * müşteri kararı: her oyuncunun kendi `slot` rengi kullanılır (kaç oyuncu varsa o kadar
 * farklı renk), palet oda tipine göre değişir (Standart/Practice/VIP, bkz. colors.ts).
 */
export function TerritoryControlBar({ state, myPlayerId }: TerritoryControlBarProps) {
  const totalRegions = state.regions.length;
  if (totalRegions === 0) return null;

  const regionCountByOwner = new Map<string, number>();
  let neutralCount = 0;
  for (const region of state.regions) {
    if (region.ownerId) {
      regionCountByOwner.set(region.ownerId, (regionCountByOwner.get(region.ownerId) ?? 0) + 1);
    } else {
      neutralCount += 1;
    }
  }

  const orderedPlayers = [...state.players].sort((a, b) => {
    if (a.id === myPlayerId) return -1;
    if (b.id === myPlayerId) return 1;
    return a.slot - b.slot;
  });

  // docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 30: State.io referansındaki gibi daha
  // kalın ve belirgin bir "white border" — arka plan koyu lacivert olduğundan (docs
  // Bölüm 3, değişmez) beyaz kenarlık burada bardaki segmentleri net ayırır.
  return (
    <div
      className="flex h-3.5 w-full overflow-hidden rounded-full border-2 border-white/85"
      role="img"
      aria-label="Bölge kontrol oranı"
    >
      {orderedPlayers.map((player) => {
        const count = regionCountByOwner.get(player.id) ?? 0;
        if (count === 0) return null;
        const color = playerFillColor({ roomType: state.room.type, ownerId: player.id, ownerSlot: player.slot });
        return (
          <div
            key={player.id}
            className="h-full transition-all duration-150 ease-out"
            style={{ width: `${(count / totalRegions) * 100}%`, backgroundColor: color }}
          />
        );
      })}
      {neutralCount > 0 ? (
        <div
          className="h-full transition-all duration-150 ease-out"
          style={{ width: `${(neutralCount / totalRegions) * 100}%`, backgroundColor: NEUTRAL_COLOR }}
        />
      ) : null}
    </div>
  );
}
