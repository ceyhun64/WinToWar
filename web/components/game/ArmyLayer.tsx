"use client";

import { playerAccentColor } from "@/lib/game/colors";
import type { ArmyMarker, TroopMarkerHandle } from "@/lib/game/useArmyAnimation";
import type { MapRegionDto, RoomType } from "@/lib/game/types";
import { TroopMarker } from "./TroopMarker";

interface ArmyLayerProps {
  markers: ArmyMarker[];
  regionById: Map<string, MapRegionDto>;
  slotByPlayerId: Map<string, number>;
  /** docs/03-game-rules.md güncel müşteri kararı: renk paleti oda tipine göre değişir (Standart/Practice/VIP). */
  roomType: RoomType;
  registerHandle: (id: string, handle: TroopMarkerHandle) => void;
  unregisterHandle: (id: string) => void;
  removeMarker: (id: string) => void;
}

/**
 * docs/15-asker-hareketi-performans.md Bölüm 6.1: bölge SVG'sinin üzerine bindirilen
 * ayrı bir hareket katmanı. Bu bileşen yalnızca YAPISAL değişikliklerde (ordu
 * yola çıktı/kalktı) yeniden render olur — pozisyon animasyonu TroopMarker'ın kendi
 * içinde imperatif olarak çalışır, burada React state'i tetiklemez.
 */
export function ArmyLayer({ markers, regionById, slotByPlayerId, roomType, registerHandle, unregisterHandle, removeMarker }: ArmyLayerProps) {
  return (
    <g pointerEvents="none">
      {markers.map((marker) => {
        const fromRegion = regionById.get(marker.fromRegionId);
        const toRegion = regionById.get(marker.toRegionId);
        if (!fromRegion || !toRegion) return null;

        const colorInput = {
          roomType,
          ownerId: marker.ownerId,
          ownerSlot: slotByPlayerId.get(marker.ownerId) ?? null,
        };
        // docs/15-asker-hareketi-performans.md §5.1 / docs/20-...md §2.A.1: parçacık
        // dolgusu KASITLI olarak playerAccentColor (koyu ton) — playerFillColor (açık,
        // bölge zeminiyle aynı ton) kullanılırsa parçacıklar zeminle karışıp "içi boş
        // daire" gibi görünür (daha önce bir kez düzeltilmiş bir hata, burada tekrar
        // edilmesin diye yalnızca darkColor hesaplanır).
        const darkColor = playerAccentColor(colorInput);

        return (
          <g key={marker.id}>
            <TroopMarker
              id={marker.id}
              fromPoint={fromRegion}
              toPoint={toRegion}
              departedAtUtc={marker.departedAtUtc}
              arrivesAtUtc={marker.arrivesAtUtc}
              initialSoldierCount={marker.soldierCount}
              color={darkColor}
              onRegisterHandle={registerHandle}
              onUnregisterHandle={unregisterHandle}
              onRemove={removeMarker}
            />
          </g>
        );
      })}
    </g>
  );
}
