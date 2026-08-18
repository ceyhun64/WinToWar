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
 * docs/23-game-ui-refresh-v2.md Aşama 4 — kenarlık kaldırıldı.
 *
 * Önceki hâlde bar 14px yüksekliğindeydi ve etrafında 2px'lik beyaz bir kenarlık
 * vardı: yüksekliğin ~%30'u kenarlıktı ve bu beyaz hat, içindeki takım renklerinden
 * DAHA yüksek kontrastlıydı. Sonuç, koyu lacivert zeminde gözün önce bu çerçeveyi
 * görmesiydi — yani ekranın öncelik zincirinde (asker sayıları > harita > aksiyonlar
 * > HUD > dekorasyon) en alttaki öğe, en üsttekinden daha fazla dikkat çekiyordu.
 *
 * Artık ağırlık tamamen segmentlerde: dış kenarlık yok, segmentler birbirinden ince
 * koyu ayraçlarla ayrılıyor (aynı rengin iki komşu segmenti zaten olamaz, ayraç
 * yalnızca kenarları temiz tutar). Bar da inceltildi — bilgi taşıyor ama haritayla
 * yarışmıyor.
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

  const myCount = regionCountByOwner.get(myPlayerId) ?? 0;

  return (
    <div
      className="flex h-2 w-full overflow-hidden rounded-full"
      style={{ background: "var(--game-panel-border)" }}
      role="img"
      aria-label={`Bölge kontrolü: ${totalRegions} bölgenin ${myCount} tanesi sizin`}
    >
      {orderedPlayers.map((player) => {
        const count = regionCountByOwner.get(player.id) ?? 0;
        if (count === 0) return null;
        const color = playerFillColor({ roomType: state.room.type, ownerId: player.id, ownerSlot: player.slot });
        return (
          <div
            key={player.id}
            className="h-full border-r transition-[width] ease-(--game-ease-out) duration-(--game-dur-base) last:border-r-0"
            style={{
              width: `${(count / totalRegions) * 100}%`,
              backgroundColor: color,
              borderColor: "var(--game-map-edge)",
            }}
          />
        );
      })}
      {neutralCount > 0 ? (
        <div
          className="h-full transition-[width] ease-(--game-ease-out) duration-(--game-dur-base)"
          style={{ width: `${(neutralCount / totalRegions) * 100}%`, backgroundColor: NEUTRAL_COLOR }}
        />
      ) : null}
    </div>
  );
}
