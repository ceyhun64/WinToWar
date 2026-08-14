"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { playerAccentColor, regionFillColorByStrength, UNEXPLORED_COLOR } from "@/lib/game/colors";
import { arrowheadPointsAttr, computeAttackArrow } from "@/lib/game/arrow";
import type { ArmyArrivedEvent, ArmyClashedEvent, ArmyDepartedEvent, MapDto, MatchStateDto } from "@/lib/game/types";
import { useArmyAnimation } from "@/lib/game/useArmyAnimation";
import { ArmyLayer } from "./ArmyLayer";
import type { ArrivalCountdown } from "./RegionNode";
import { RegionLabel, RegionShape } from "./RegionNode";

interface GameMapProps {
  map: MapDto;
  state: MatchStateDto;
  myPlayerId: string;
  selectedRegionId: string | null;
  /** docs/15-asker-hareketi-performans.md Bölüm 6.3: sevkiyat hareketi/çarpışma animasyonu için — bkz. ArmyLayer/useArmyAnimation. */
  armyDeparted: ArmyDepartedEvent | null;
  armyClashed: ArmyClashedEvent | null;
  armyArrived: ArmyArrivedEvent | null;
  onSelectRegion: (regionId: string) => void;
  onAttack: (fromRegionId: string, toRegionId: string) => void;
}

// docs/14-game-map-redesign.md Bölüm 4/6.2: viewBox, api/Data/map.json'daki organik
// polygon geometrisinin gerçek sınırlayıcı kutusuna (~[36.7,6.2]-[596.7,570.1]) dar
// bir kenar payıyla oturacak şekilde seçildi — SVG'nin kendi içinde gereksiz boş
// alan bırakmadan harita mümkün olan en büyük boyutta fit edilir.
const VIEW_MIN_X = 22;
const VIEW_MIN_Y = -9;
const VIEW_WIDTH = 590;
const VIEW_HEIGHT = 594;

/** docs/14-game-map-redesign.md: bölge artık daire değil polygon — hit-test ray-casting ile yapılır. */
function isPointInPolygon(point: { x: number; y: number }, polygon: [number, number][]): boolean {
  let inside = false;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const [xi, yi] = polygon[i];
    const [xj, yj] = polygon[j];
    const intersects = yi > point.y !== yj > point.y && point.x < ((xj - xi) * (point.y - yi)) / (yj - yi) + xi;
    if (intersects) inside = !inside;
  }
  return inside;
}

// docs/03-game-rules.md Bölüm 6/12: GameConfig.MinGarrisonPerSend = 0 (müşteri kararı) —
// sürüklerken kaynaktaki TÜM asker gönderilir, geride hiçbir şey kalmaz; gönderilecek
// asker yoksa (bölge boşsa, 0) sürüklenemez.
const MIN_GARRISON_PER_SEND = 0;

// docs/17-oyun-ici-ui-güclendirme.md Bölüm 8 "Territory Capture Feedback": bir
// bölgenin sahibi değiştiğinde kısa süreli bir highlight — süre kısa/hafif tutulur
// (bkz. TroopMarker'daki aynı "playful ama abartısız" prensip).
const CAPTURE_FLASH_MS = 650;

interface CaptureFlash {
  id: string;
  regionId: string;
  points: [number, number][];
}

// docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2 (gerçek state.io kaydıyla
// DÜZELTİLDİ — önceki tur burada saldıranın renginde genişleyip solan bir "halka"
// uygulamıştı, bugünkü gerçek kayıt bunun yerine hedefin KENDİ rozetindeki sayının
// geri sayarak inmesini + rozetin kendi renginin kısaca açılıp kararmasını gösterdi).
// CombatService sonucunu/zamanlamasını ETKİLEMEZ (§2.B.3) — yalnızca RegionLabel'a
// (RegionNode.tsx) iletilen bir sunum-katmanı geçişidir.
const ARRIVAL_COUNTDOWN_MS = 700;

export function GameMap({
  map,
  state,
  myPlayerId,
  selectedRegionId,
  armyDeparted,
  armyClashed,
  armyArrived,
  onSelectRegion,
  onAttack,
}: GameMapProps) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [dragFromRegionId, setDragFromRegionId] = useState<string | null>(null);
  const [dragPointerSvg, setDragPointerSvg] = useState<{ x: number; y: number } | null>(null);
  const [dragHoverTargetId, setDragHoverTargetId] = useState<string | null>(null);
  // docs/16-state.io-gorsel-referans.md doğrulama sırasında bulunan hata: RegionShape
  // pointerdown'da setPointerCapture çağırdığından pointerup HER ZAMAN kaynak elemente
  // düşer — tarayıcı bu pointerup'ın ardından kaynak elemente bir "click" event'i de
  // gönderiyor, bu da asker gönderiminden hemen sonra o bölgenin ActionPanel'ini açıyordu.
  // Yalnızca gerçek bir sürükleme (eşik üstü hareket) sonrasındaki bu sahte click'i yok
  // sayarız; basit bir dokunuş/tık (hareketsiz) hâlâ normal şekilde bölgeyi seçer.
  //
  // ⚠️ ÖNEMLİ — kaynak RegionShape'in memo karşılaştırıcısı (`regionShapeEqual`,
  // RegionNode.tsx) `onDragMove`/`onDragEnd` gibi callback prop'ları KASITLI olarak yok
  // sayar (bkz. oradaki yorum); bu da sürükleme sırasında bu callback'lerin İÇİNDEN
  // OKUNAN state değerlerinin, sürükleme başladığı andaki değerde DONMUŞ (stale closure)
  // kalabileceği anlamına gelir — kaynak eleman, kendisini değiştirmeyen prop'lar için
  // yeniden render OLMAZ. Bu yüzden handleDragMove/handleDragEnd içinde okunan HER ŞEY
  // (dragFromRegionId hariç — o yalnızca sürüklemenin başında bir kez, taze bir render'da
  // set edilir) ref üzerinden okunmalı, useState üzerinden DEĞİL.
  const dragStartClientRef = useRef<{ x: number; y: number } | null>(null);
  const hasDraggedRef = useRef(false);
  const suppressClickRef = useRef(false);
  const CLICK_VS_DRAG_THRESHOLD_PX = 4;

  const slotByPlayerId = useMemo(() => {
    const result = new Map<string, number>();
    state.players.forEach((p) => result.set(p.id, p.slot));
    return result;
  }, [state.players]);

  // docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 18/19: seçim/sürükleme/hedef vurgusu
  // artık sabit siyah değil, o an sürükleyen/seçen oyuncunun kendi kimlik rengi.
  const mySelectionColor = useMemo(
    () =>
      playerAccentColor({
        roomType: state.room.type,
        ownerId: myPlayerId,
        ownerSlot: slotByPlayerId.get(myPlayerId) ?? 0,
      }),
    [state.room.type, myPlayerId, slotByPlayerId]
  );

  const { markers: armyMarkers, registerHandle, unregisterHandle, removeMarker } = useArmyAnimation(
    state.armies,
    armyDeparted,
    armyClashed,
    armyArrived
  );

  const regionStateById = useMemo(() => {
    return new Map(state.regions.map((r) => [r.id, r]));
  }, [state.regions]);

  const regionById = useMemo(() => {
    return new Map(map.regions.map((r) => [r.id, r]));
  }, [map.regions]);

  // docs/17-oyun-ici-ui-güclendirme.md Bölüm 8: sahip değişimini tespit edip kısa bir
  // flash tetikler. Fog of War açıkken görünürlük kapandığında sunucu ownerId'yi null
  // gönderir (bkz. MatchStateMapper.ToDto) — bu gerçek bir el değiştirme DEĞİLDİR, bu
  // yüzden yalnızca o an GÖRÜNÜR bölgeler karşılaştırılır/kaydedilir; görünmeyen bir
  // bölgenin son bilinen sahibi silinmez, tekrar görünür olduğunda ona karşı kıyaslanır.
  const [captureFlashes, setCaptureFlashes] = useState<CaptureFlash[]>([]);
  const prevOwnerByRegionRef = useRef<Map<string, string | null>>(new Map());
  const isFirstOwnerSyncRef = useRef(true);

  useEffect(() => {
    const prevOwners = prevOwnerByRegionRef.current;
    if (isFirstOwnerSyncRef.current) {
      isFirstOwnerSyncRef.current = false;
    } else {
      const newlyCaptured: CaptureFlash[] = [];
      for (const regionState of state.regions) {
        if (!regionState.isVisible) continue;
        const prevOwner = prevOwners.get(regionState.id);
        if (prevOwner !== undefined && prevOwner !== regionState.ownerId) {
          const region = regionById.get(regionState.id);
          if (region) {
            newlyCaptured.push({ id: `${regionState.id}-${Date.now()}`, regionId: regionState.id, points: region.geometry.points });
          }
        }
      }
      if (newlyCaptured.length > 0) {
        setCaptureFlashes((prev) => [...prev, ...newlyCaptured]);
        for (const flash of newlyCaptured) {
          setTimeout(() => {
            setCaptureFlashes((prev) => prev.filter((f) => f.id !== flash.id));
          }, CAPTURE_FLASH_MS);
        }
      }
    }

    const nextOwners = new Map(prevOwners);
    for (const regionState of state.regions) {
      if (regionState.isVisible) {
        nextOwners.set(regionState.id, regionState.ownerId);
      }
    }
    prevOwnerByRegionRef.current = nextOwners;
  }, [state.regions, regionById]);

  // docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2: her `armyArrived` event'inde
  // (ele geçirme VEYA püskürtme, ikisi de) hedef bölgenin rozetinde kısa bir geri sayım +
  // renk parlaması tetiklenir — CombatService sonucundan/zamanlamasından tamamen bağımsız
  // bir sunum katmanı geçişi (bkz. yukarıdaki ARRIVAL_COUNTDOWN_MS notu). "Eski" (from)
  // değer, bu MatchState güncellemesinden HEMEN ÖNCEKİ (yani sunucunun bu varışı henüz
  // uygulamadığı andaki) bilinen asker sayısıdır — sunucu ArmyArrived'ı her zaman ilgili
  // MatchState'ten SONRA yayınladığından (EconomyTickService.TickMatchAsync), bu event
  // geldiğinde `state.regions` zaten sonucu (yeni "to" değerini) içerir.
  const [arrivalCountdowns, setArrivalCountdowns] = useState<Map<string, ArrivalCountdown>>(new Map());
  const soldierCountBeforeUpdateRef = useRef<Map<string, number>>(new Map());
  const latestSoldierCountRef = useRef<Map<string, number>>(new Map());

  useEffect(() => {
    soldierCountBeforeUpdateRef.current = new Map(latestSoldierCountRef.current);
    for (const regionState of state.regions) {
      if (regionState.isVisible) {
        latestSoldierCountRef.current.set(regionState.id, regionState.soldierCount);
      }
    }
  }, [state.regions]);

  useEffect(() => {
    if (!armyArrived) return;
    const regionId = armyArrived.regionId;
    const to = latestSoldierCountRef.current.get(regionId);
    // Fog of War: bölge bu viewer için görünmüyorsa (bkz. MatchStateMapper) animasyon
    // gösterecek bir sayı yok — sessizce atlanır, rozet zaten render edilmiyor.
    if (to === undefined) return;
    const from = soldierCountBeforeUpdateRef.current.get(regionId) ?? to;

    const countdown: ArrivalCountdown = { from, to, startedAt: Date.now() };
    setArrivalCountdowns((prev) => {
      const next = new Map(prev);
      next.set(regionId, countdown);
      return next;
    });
    const timeout = setTimeout(() => {
      setArrivalCountdowns((prev) => {
        if (prev.get(regionId) !== countdown) return prev;
        const next = new Map(prev);
        next.delete(regionId);
        return next;
      });
    }, ARRIVAL_COUNTDOWN_MS);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [armyArrived]);

  const selectedRegion = selectedRegionId ? regionStateById.get(selectedRegionId) : undefined;
  const selectedIsMine = selectedRegion?.ownerId === myPlayerId;
  // docs/03-game-rules.md Bölüm 3/6/15-D.1: saldırı artık komşulukla sınırlı değil —
  // (GameConfig.AttackAdjacencyOnly=false ile birebir eşleşir) kendi bölgen seçiliyken
  // haritadaki TÜM diğer bölgeler geçerli bir gönderim hedefidir.
  const attackTargets = useMemo(() => {
    if (!selectedRegionId || !selectedIsMine) return new Set<string>();
    return new Set(map.regions.filter((r) => r.id !== selectedRegionId).map((r) => r.id));
  }, [selectedRegionId, selectedIsMine, map.regions]);

  function toSvgPoint(clientX: number, clientY: number): { x: number; y: number } | null {
    const svg = svgRef.current;
    if (!svg) return null;
    const ctm = svg.getScreenCTM();
    if (!ctm) return null;
    const point = svg.createSVGPoint();
    point.x = clientX;
    point.y = clientY;
    const transformed = point.matrixTransform(ctm.inverse());
    return { x: transformed.x, y: transformed.y };
  }

  function findRegionAtPoint(point: { x: number; y: number }): string | null {
    for (const region of map.regions) {
      if (isPointInPolygon(point, region.geometry.points)) {
        return region.id;
      }
    }
    return null;
  }

  function handleDragStart(regionId: string) {
    return (e: React.PointerEvent) => {
      const regionState = regionStateById.get(regionId);
      const isMine = regionState?.ownerId === myPlayerId;
      if (state.status !== "Playing" || !isMine || (regionState?.soldierCount ?? 0) <= MIN_GARRISON_PER_SEND) {
        return;
      }
      e.currentTarget.setPointerCapture(e.pointerId);
      setDragFromRegionId(regionId);
      dragStartClientRef.current = { x: e.clientX, y: e.clientY };
      hasDraggedRef.current = false;
      const point = toSvgPoint(e.clientX, e.clientY);
      setDragPointerSvg(point);
      setDragHoverTargetId(null);
    };
  }

  function handleDragMove(e: React.PointerEvent) {
    if (!dragFromRegionId) return;
    const point = toSvgPoint(e.clientX, e.clientY);
    if (!point) return;
    setDragPointerSvg(point);
    const start = dragStartClientRef.current;
    if (start && Math.hypot(e.clientX - start.x, e.clientY - start.y) > CLICK_VS_DRAG_THRESHOLD_PX) {
      hasDraggedRef.current = true;
    }
    // docs/03-game-rules.md Bölüm 3/6/15-D.1: saldırı artık komşulukla sınırlı değil
    // (GameConfig.AttackAdjacencyOnly=false) — kaynak dışındaki HERHANGİ bir bölge
    // geçerli bir bırakma hedefidir.
    const hoverId = findRegionAtPoint(point);
    setDragHoverTargetId(hoverId !== null && hoverId !== dragFromRegionId ? hoverId : null);
  }

  function handleDragEnd(e: React.PointerEvent) {
    if (!dragFromRegionId) return;
    const point = toSvgPoint(e.clientX, e.clientY);
    const targetId = point ? findRegionAtPoint(point) : null;
    if (targetId && targetId !== dragFromRegionId) {
      onAttack(dragFromRegionId, targetId);
    }
    if (hasDraggedRef.current) {
      suppressClickRef.current = true;
    }
    setDragFromRegionId(null);
    setDragPointerSvg(null);
    setDragHoverTargetId(null);
  }

  function handleRegionClick(regionId: string) {
    if (suppressClickRef.current) {
      suppressClickRef.current = false;
      return;
    }
    onSelectRegion(regionId);
  }

  const dragFromRegion = dragFromRegionId ? regionById.get(dragFromRegionId) : undefined;

  // docs/14-game-map-redesign.md: bölgeler artık boşluksuz bitişik olduğundan, dolgu/kenarlık
  // (RegionShape) ve etiket (RegionLabel) iki AYRI katmanda çizilir — aksi halde sonradan
  // çizilen bir komşu polygon, önceki bölgenin etiketinin bir kısmını örter (bkz. RegionNode.tsx).
  const regionRenderData = map.regions.map((region) => {
    const regionState = regionStateById.get(region.id);
    const isMine = regionState?.ownerId === myPlayerId;
    const ownerId = regionState?.ownerId ?? null;
    const ownerSlot = ownerId ? (slotByPlayerId.get(ownerId) ?? 0) : null;
    const colorInput = { roomType: state.room.type, ownerId, ownerSlot };
    // docs/03-game-rules.md (müşteri kararı): bölge dolgusu artık yalnızca kimlik değil,
    // o bölgedeki güncel asker sayısına göre de koyulaşır/açılır (bkz. colors.ts).
    const color =
      regionState?.isVisible === false
        ? UNEXPLORED_COLOR
        : regionFillColorByStrength(colorInput, regionState?.soldierCount ?? 0, state.room.greyRegionDefenseCount);
    // docs/03-game-rules.md (müşteri kararı — "kale gibi bir alan olmayacak"): hiçbir
    // bölge diğerlerinden ayrı bir "başkent" muamelesi görmez, hepsi aynı rozet stiliyle çizilir.
    const accentColor = playerAccentColor(colorInput);
    const draggable =
      isMine && state.status === "Playing" && (regionState?.soldierCount ?? 0) > MIN_GARRISON_PER_SEND;
    return { region, regionState, isMine, color, accentColor, ownerSlot, draggable };
  });

  return (
    <svg
      ref={svgRef}
      viewBox={`${VIEW_MIN_X} ${VIEW_MIN_Y} ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
      className="w-full h-auto max-h-[75vh] rounded-md border border-border bg-card"
      role="img"
      aria-label="Lüksemburg haritası"
    >
      {/* docs/14-game-map-redesign.md Bölüm 4: kalıcı bir komşuluk çizgisi ağı (node-graph
          görünümü) kasıtlı olarak çizilmez — bölgeler artık gerçekten ortak sınır paylaşan
          polygon'lar, komşuluk zaten paylaşılan kenarlardan görülüyor. Bölgeler artık boşluksuz
          tüm haritayı kapladığından, hareket/sürükleme göstergeleri bölgelerin ÜSTÜNDE
          (sonra) çizilir — aksi halde tamamen gizlenirlerdi. */}
      <g>
        {regionRenderData.map(({ region, color, draggable }) => (
          <RegionShape
            key={region.id}
            region={region}
            color={color}
            isSelected={selectedRegionId === region.id}
            isAttackTarget={attackTargets.has(region.id)}
            isDragSource={dragFromRegionId === region.id}
            isDragHoverTarget={dragHoverTargetId === region.id}
            draggable={draggable}
            selectionColor={mySelectionColor}
            onClick={() => handleRegionClick(region.id)}
            onDragStart={handleDragStart(region.id)}
            onDragMove={handleDragMove}
            onDragEnd={handleDragEnd}
          />
        ))}
      </g>
      {/* docs/17-oyun-ici-ui-güclendirme.md Bölüm 8: sahip değişen bölgenin üstüne
          bindirilen, kendi kendine sönen (SVG native <animate>, ek JS/CSS animasyon
          döngüsü gerekmez) bir flash — GameMap CAPTURE_FLASH_MS sonra state'ten düşürür. */}
      <g pointerEvents="none">
        {captureFlashes.map((flash) => (
          <polygon
            key={flash.id}
            points={flash.points.map(([x, y]) => `${x},${y}`).join(" ")}
            fill="#FAF7F0"
          >
            <animate attributeName="opacity" from="0.65" to="0" dur={`${CAPTURE_FLASH_MS / 1000}s`} fill="freeze" />
          </polygon>
        ))}
      </g>
      {/* docs/15-asker-hareketi-performans.md Bölüm 6.1: bölge SVG'sinin üzerine
          bindirilen, gerçekten hareket eden bir sevkiyat katmanı — statik bir
          ilerleme çizgisi değil. Pozisyon TroopMarker'ın kendi requestAnimationFrame
          döngüsünde imperatif olarak hesaplanır (Bölüm 6.2), bu katman yalnızca
          hangi sevkiyatların var olduğu (yapısal) değiştiğinde yeniden render olur. */}
      <ArmyLayer
        markers={armyMarkers}
        regionById={regionById}
        slotByPlayerId={slotByPlayerId}
        roomType={state.room.type}
        registerHandle={registerHandle}
        unregisterHandle={unregisterHandle}
        removeMarker={removeMarker}
      />
      {/* docs/16-state.io-gorsel-referans.md Bölüm 1.3: kaynak bölge seçildiğinde (drag
          başladığında) rozetin etrafında soluk, genişleyip solan bir halka — Tailwind'in
          çekirdek `animate-ping` utility'si zaten tam olarak bu "pulsing ring" efektini
          üretir, ayrı bir animasyon kütüphanesi/keyframe eklenmez. */}
      {dragFromRegion ? (
        <circle
          cx={dragFromRegion.x}
          cy={dragFromRegion.y}
          r={15}
          fill="none"
          stroke={mySelectionColor}
          strokeWidth={1.5}
          opacity={0.55}
          className="animate-ping"
          pointerEvents="none"
        />
      ) : null}
      {/* docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 14-17: sürükleme önizlemesi artık
          siyah/dashed bir çizgi değil, sürükleyen oyuncunun kendi renginde, net bir
          arrowhead'i olan bir ok — bkz. computeAttackArrow (lib/game/arrow.ts). */}
      {dragFromRegion && dragPointerSvg
        ? (() => {
            const arrow = computeAttackArrow(dragFromRegion, dragPointerSvg);
            if (!arrow) return null;
            return (
              <g pointerEvents="none">
                <line
                  x1={arrow.lineStart.x}
                  y1={arrow.lineStart.y}
                  x2={arrow.lineEnd.x}
                  y2={arrow.lineEnd.y}
                  stroke={mySelectionColor}
                  strokeWidth={2.5}
                  strokeLinecap="round"
                />
                <polygon points={arrowheadPointsAttr(arrow.arrowheadPoints)} fill={mySelectionColor} />
              </g>
            );
          })()
        : null}
      {/* docs/14-game-map-redesign.md: etiketler (rozet/isim) en üst katmanda, tüm
          bölge polygon'ları çizildikten sonra render edilir — bkz. RegionNode.tsx üstündeki not. */}
      <g pointerEvents="none">
        {regionRenderData.map(({ region, regionState, isMine, accentColor }) => (
          <RegionLabel
            key={region.id}
            region={region}
            regionState={regionState}
            isMine={isMine}
            accentColor={accentColor}
            arrivalCountdown={arrivalCountdowns.get(region.id) ?? null}
          />
        ))}
      </g>
    </svg>
  );
}
