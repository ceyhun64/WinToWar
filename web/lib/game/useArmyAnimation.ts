"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { ArmyArrivedEvent, ArmyClashedEvent, ArmyDepartedEvent, ArmyDto } from "./types";

/**
 * docs/15-asker-hareketi-performans.md Bölüm 6.1/6.2: bir sevkiyat grubunun statik
 * verisi. Pozisyon buradan TÜRETİLİR (departedAtUtc/arrivesAtUtc + requestAnimationFrame,
 * bkz. TroopMarker) — sunucu her frame için ayrı bir pozisyon göndermez. Bu obje
 * yalnızca yola çıkış/çarpışma/varış anlarında değişir, her animasyon karesinde değil.
 */
export interface ArmyMarker {
  id: string;
  ownerId: string;
  fromRegionId: string;
  toRegionId: string;
  departedAtUtc: number;
  arrivesAtUtc: number;
  soldierCount: number;
}

/**
 * TroopMarker'ın, imperatif animasyonları (Bölüm 5.3) tetiklemek için bu hook'a
 * kayıt ettirdiği komut arayüzü — React state/re-render'a dokunmadan doğrudan DOM'a
 * yazar (Bölüm 6.2 "React state güncellemesi/re-render tetiklemez").
 */
export interface TroopMarkerHandle {
  /**
   * Çarpışma anında oynatılır. Kullanıcı talimatı: "karşı taraf askerleri birbirine
   * çarpınca çarpan askerler yok olsun" — kaybeden taraf TÜMÜYLE çarpışma noktasında
   * donup küçülüp söner; kazanan tarafta ise yalnızca GERÇEKTEN kaybedilen askerler
   * (`survivorCount`'a göre TroopMarker'ın kendi hesapladığı fark kadar ikon) aynı
   * ölüm animasyonuyla çarpışma noktasında kaybolur, hayatta kalanlar kısa bir
   * "toslama" pulse'uyla yoluna devam eder. `survivorCount`, yalnızca `role === "winner"`
   * olduğunda anlamlıdır.
   */
  playClash: (role: "winner" | "loser", clashAtUtc: number, survivorCount?: number) => void;
  /** Hedefe varışta oynatılır — küçük bir "pop" ile kaybolur. */
  playArrive: () => void;
}

function parseUtc(iso: string): number {
  return new Date(iso).getTime();
}

function toMarker(army: ArmyDto): ArmyMarker {
  return {
    id: army.id,
    ownerId: army.ownerId,
    fromRegionId: army.fromRegionId,
    toRegionId: army.toRegionId,
    departedAtUtc: parseUtc(army.departedAtUtc),
    arrivesAtUtc: parseUtc(army.arrivesAtUtc),
    soldierCount: army.soldierCount,
  };
}

/**
 * docs/15-asker-hareketi-performans.md Bölüm 6.1/6.2/8: sevkiyat gruplarının yaşam
 * döngüsünü (yola çıkış/çarpışma/varış) yönetir. Bu hook YALNIZCA hangi marker'ların
 * var olduğunu (mount/unmount) React state'i üzerinden yönetir — bu, saniyede birkaç
 * kez olan seyrek bir yapısal değişikliktir ve `GameMap`'in tüm bölge ağacını asla
 * tetiklemez (yalnızca ArmyLayer'ı). Sürekli pozisyon/animasyon güncellemesi
 * TroopMarker'ın kendi requestAnimationFrame döngüsünde, burada kayıtlı handle
 * üzerinden tamamen imperatif olarak yapılır.
 */
export function useArmyAnimation(
  matchStateArmies: ArmyDto[],
  armyDeparted: ArmyDepartedEvent | null,
  armyClashed: ArmyClashedEvent | null,
  armyArrived: ArmyArrivedEvent | null
) {
  const [markers, setMarkers] = useState<ArmyMarker[]>([]);
  const markersMapRef = useRef<Map<string, ArmyMarker>>(new Map());
  const handlesRef = useRef<Map<string, TroopMarkerHandle>>(new Map());

  const syncMarkers = useCallback(() => setMarkers(Array.from(markersMapRef.current.values())), []);

  // docs/15-asker-hareketi-performans.md Bölüm 6.4 ruhu: bu callback'ler useCallback
  // ile kararlı tutulur ki TroopMarker'ın handle-kayıt useEffect'i, GameMap'in
  // ilgisiz bir sebeple (ör. selectedRegionId) yeniden render olmasında gereksiz
  // yere tekrar çalışmasın.
  const removeMarker = useCallback((id: string) => {
    if (markersMapRef.current.delete(id)) {
      syncMarkers();
    }
  }, [syncMarkers]);

  const registerHandle = useCallback((id: string, handle: TroopMarkerHandle) => {
    handlesRef.current.set(id, handle);
  }, []);

  const unregisterHandle = useCallback((id: string) => {
    handlesRef.current.delete(id);
  }, []);

  // docs/15 Bölüm 6.2 "resync": ilk katılım / yeniden bağlanma sonrası gelen MatchState,
  // o an aktif tüm sevkiyatları içerir — henüz izlenmeyen (ör. ArmyDeparted event'i
  // kaçırılmış) ordular burada eklenir. Zaten izlenen bir marker MatchState yüzünden
  // ASLA erken silinmez (silme yalnızca ArmyClashed/ArmyArrived event'leriyle olur) —
  // aksi halde çarpışma/varış animasyonu, ordunun bir sonraki tick'te MatchState'ten
  // düşmesiyle yarıda kesilirdi.
  useEffect(() => {
    let changed = false;
    for (const army of matchStateArmies) {
      if (!markersMapRef.current.has(army.id)) {
        markersMapRef.current.set(army.id, toMarker(army));
        changed = true;
      }
    }

    // Savunma amaçlı temizlik: bir marker artık MatchState'te yok VE varış zamanı
    // çoktan geçmiş (bağlantı kopup yeniden bağlanırken bir ArmyArrived/ArmyClashed
    // event'i kaçırılmış olabilir) — sonsuza kadar haritada asılı kalmasın.
    const activeIds = new Set(matchStateArmies.map((a) => a.id));
    const now = Date.now();
    for (const [id, marker] of markersMapRef.current) {
      if (!activeIds.has(id) && now > marker.arrivesAtUtc + 2000) {
        markersMapRef.current.delete(id);
        changed = true;
      }
    }

    if (changed) {
      syncMarkers();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [matchStateArmies]);

  useEffect(() => {
    if (!armyDeparted) return;
    if (markersMapRef.current.has(armyDeparted.army.id)) return;
    markersMapRef.current.set(armyDeparted.army.id, toMarker(armyDeparted.army));
    syncMarkers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [armyDeparted]);

  useEffect(() => {
    if (!armyClashed) return;
    const { firstArmyId, secondArmyId, winningArmyId, survivorCount, clashAtUtc } = armyClashed;
    const clashAtMs = parseUtc(clashAtUtc);

    for (const armyId of [firstArmyId, secondArmyId]) {
      const isWinner = armyId === winningArmyId;
      const handle = handlesRef.current.get(armyId);

      if (isWinner) {
        const marker = markersMapRef.current.get(armyId);
        if (marker) {
          marker.soldierCount = survivorCount;
        }
        handle?.playClash("winner", clashAtMs, survivorCount);
      } else {
        if (handle) {
          handle.playClash("loser", clashAtMs);
        } else {
          // Handle henüz mount olmadan event geldiyse (nadir bir yarış durumu), doğrudan kaldır.
          removeMarker(armyId);
        }
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [armyClashed]);

  useEffect(() => {
    if (!armyArrived) return;
    const handle = handlesRef.current.get(armyArrived.armyId);
    if (handle) {
      handle.playArrive();
    } else {
      removeMarker(armyArrived.armyId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [armyArrived]);

  return { markers, registerHandle, unregisterHandle, removeMarker };
}
