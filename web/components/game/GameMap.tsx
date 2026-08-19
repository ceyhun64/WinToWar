"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { playerAccentColor, playerFillColor, regionFillColorByStrength, UNEXPLORED_COLOR } from "@/lib/game/colors";
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

// docs/14-game-map-redesign.md Bölüm 4/6.2: viewBox, api/Data/map.json.daki
// polygon geometrisinin gerçek sınırlayıcı kutusuna dar bir kenar payıyla (~15
// birim) oturacak şekilde seçilir — SVG.nin kendi içinde gereksiz boş alan
// bırakmadan harita mümkün olan en büyük boyutta fit edilir.
//
// Kullanıcı talimatı: bölge şekilleri artık Lüksemburg.un 12 gerçek kantonunun
// sınırları (bkz. map.json). Yeni bbox [120.1, 6.2]-[513.2, 570.1]: gerçek ülke
// silueti eskisi gibi ~kare değil DİKEY (en/boy ~0.70), bu yüzden viewBox
// genişliği 590 -> 423.
const VIEW_MIN_X = 105;
const VIEW_MIN_Y = -9;
const VIEW_WIDTH = 423;
const VIEW_HEIGHT = 594;

/**
 * docs/23-game-ui-refresh-v2.md Aşama 2 — etiket çapası ("pole of inaccessibility").
 *
 * `map.json`'daki `region.x/y`, bölgenin sınırlayıcı kutu merkezidir ve içbükey
 * (concave) bölgelerde kenara çok yakın düşebiliyor — ölçüldü: Mersch'te merkezin
 * en yakın kenara uzaklığı yalnızca **6 birim**, Differdange'da 10. Bu yüzden asker
 * sayısı rozeti bu iki bölgede (ESKİ, daha küçük rozet boyutunda bile) kendi
 * bölgesinin dışına taşıp komşunun alanına giriyordu — yani "bu sayı hangi
 * bölgenin?" sorusu belirsizleşiyordu. Öncelik zincirinin en tepesindeki bilgi
 * için kabul edilemez.
 *
 * Çözüm: rozet artık kutu merkezine değil, polygon'un İÇİNDE kenarlardan en uzak
 * noktaya oturur. Kaçış payı 6 → 31 birime çıkar; böylece rozet 12 bölgenin
 * hepsinde kendi sınırları içinde kalır.
 *
 * ⚠️ Bu tamamen bir SUNUM hesabıdır — `map.json` değişmez, `region.x/y` alanı
 * olduğu gibi durur, oyun mantığı (komşuluk, hit-test, hareket süresi) bu
 * değerden etkilenmez.
 */
function distanceToPolygonEdge(x: number, y: number, polygon: [number, number][]): number {
  let min = Infinity;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const [x1, y1] = polygon[j];
    const [x2, y2] = polygon[i];
    const dx = x2 - x1;
    const dy = y2 - y1;
    const lengthSquared = dx * dx + dy * dy || 1;
    const t = Math.max(0, Math.min(1, ((x - x1) * dx + (y - y1) * dy) / lengthSquared));
    min = Math.min(min, Math.hypot(x - (x1 + t * dx), y - (y1 + t * dy)));
  }
  return min;
}

/** Kaba bir ızgara taraması + ardışık daraltma. Harita başına bir kez hesaplanır (bkz. `labelAnchorById`). */
function labelAnchorForPolygon(polygon: [number, number][]): { x: number; y: number } {
  const xs = polygon.map((p) => p[0]);
  const ys = polygon.map((p) => p[1]);
  let minX = Math.min(...xs);
  let maxX = Math.max(...xs);
  let minY = Math.min(...ys);
  let maxY = Math.max(...ys);

  let best = { x: (minX + maxX) / 2, y: (minY + maxY) / 2, clearance: -Infinity };
  let step = Math.max(maxX - minX, maxY - minY) / 16;

  for (let pass = 0; pass < 6; pass++) {
    for (let x = minX; x <= maxX; x += step) {
      for (let y = minY; y <= maxY; y += step) {
        if (!isPointInPolygon({ x, y }, polygon)) continue;
        const clearance = distanceToPolygonEdge(x, y, polygon);
        if (clearance > best.clearance) best = { x, y, clearance };
      }
    }
    minX = best.x - step;
    maxX = best.x + step;
    minY = best.y - step;
    maxY = best.y + step;
    step /= 4;
  }

  return { x: best.x, y: best.y };
}

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
//
// docs/23-game-ui-refresh-v2.md Aşama 3: 650 → 420 ms. Efekt artık dolgu yıkaması
// değil sınır darbesi (bkz. render bloğu); bir kenar parlaması anlık okunur, 650 ms
// boyunca sürmesi bilgi eklemiyor, yalnızca varış geri sayımıyla (700 ms) üst üste
// binip aynı bölgede iki ayrı hareket yaratıyordu.
const CAPTURE_FLASH_MS = 420;

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
  /**
   * docs/24-responsive-small-screens.md Problem B (madde 8) — aktif sürüklemenin
   * pointer kimliği. Eskiden takip edilmiyordu: ikinci bir parmak başka bir kendi
   * bölgesine dokununca `dragFromRegionId` üzerine yazılıyor, ilk parmağın
   * pointerup'ı artık başka bir elemana düşüyor ve state makinesi tutarsızlaşıyordu.
   * Artık sürüklemeyi yalnızca birincil pointer başlatır ve yalnızca onu başlatan
   * pointer ilerletebilir/bitirebilir.
   */
  const activePointerIdRef = useRef<number | null>(null);
  /**
   * docs/24-responsive-small-screens.md Problem B (madde 7) — tap/drag ayrımı.
   * Eski değer 4px idi: parmak sabit durmadığı için bir dokunuş kolayca "sürükleme"
   * sayılıyordu. 10px, dokunmatik arayüzlerde yerleşik "slop" aralığıdır.
   *
   * ⚠️ Bu eşik artık yalnızca sahte click'i bastırmakla kalmaz, saldırının
   * gönderilip gönderilmeyeceğini de belirler (bkz. handleDragEnd) — eskiden
   * saldırı hiçbir eşiğe bağlı değildi, yani eşik altındaki bir parmak titremesi
   * bile geri alınamaz bir sevkiyat başlatabiliyordu.
   */
  const CLICK_VS_DRAG_THRESHOLD_PX = 10;

  const slotByPlayerId = useMemo(() => {
    const result = new Map<string, number>();
    state.players.forEach((p) => result.set(p.id, p.slot));
    return result;
  }, [state.players]);

  // docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 18/19'daki `mySelectionColor`
  // (seçim/hedef vurgusunun oyuncunun koyu kimlik tonunda olması) kaldırıldı:
  // docs/23-game-ui-refresh-v2.md Aşama 2'de bölge vurguları renkten bağımsız,
  // akromatik halkalara geçti (bkz. RegionNode durum matrisi) — geriye yalnızca
  // sürükleme okunun/halkasının rengi kaldı, o da aşağıdaki `myArrowColor`.

  /**
   * docs/23-game-ui-refresh-v2.md Aşama 3: sürükleme okunun/nabız halkasının rengi.
   * Bilinçli olarak `playerAccentColor` (koyu ton) DEĞİL, `playerFillColor` (açık
   * kimlik tonu) — ok hem açık bölge dolgularının hem koyu harita zemininin üstünden
   * geçiyor; koyu tonlu bir ok zeminde kayboluyordu. Altındaki koyu halo ile birlikte
   * her iki yüzeyde de okunur (bkz. ok render bloğu).
   */
  const myArrowColor = useMemo(
    () =>
      playerFillColor({
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

  /**
   * docs/23-game-ui-refresh-v2.md Aşama 2: rozetin/etiketin ve sevkiyat uçlarının
   * ortak dayanak noktası (bkz. `labelAnchorForPolygon`). Harita başına bir kez
   * hesaplanır ve referansı sabit kalır — RegionLabel'ın memo karşılaştırıcısı bu
   * kararlılığa güvenir.
   *
   * Sevkiyat okları/asker ikonları da bilinçli olarak bu noktayı kullanır: rozet
   * bir yere, ordu başka bir yere gitseydi, varış geri sayımı rozette oynarken
   * askerler 38 birim uzakta kaybolurdu. Hareketin SÜRESİ/sonucu sunucudan gelir
   * ve bu değişiklikten etkilenmez — yalnızca çizilen yolun uçları kayar.
   */
  const labelAnchorById = useMemo(() => {
    return new Map(map.regions.map((r) => [r.id, labelAnchorForPolygon(r.geometry.points)]));
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

  // docs/03-game-rules.md Bölüm 3/6/15-D.1: saldırı komşulukla sınırlı DEĞİL
  // (GameConfig.AttackAdjacencyOnly=false) — kaynak dışındaki her bölge geçerli bir
  // gönderim hedefidir. Bu kural DEĞİŞMEDİ.
  //
  // docs/23-game-ui-refresh-v2.md Aşama 2 — değişen tek şey bu bilginin NE ZAMAN
  // gösterildiği: eskiden tetikleyici `selectedRegionId` idi, yani bir bölgeye
  // tıklamak (ki tıklamak yalnızca bilgi panelini açar, saldırı başlatmaz) haritadaki
  // diğer 11 bölgeyi vurguluyordu. "Her yer hedef" bilgisi her zaman doğru olduğu için
  // tek başına hiçbir şey söylemiyor, yalnızca gürültü üretiyordu. Artık tetikleyici
  // AKTİF SÜRÜKLEME: oyuncu gerçekten bir hedef ararken (ve yalnızca o anda) ipucu
  // görünür. Saldırı doğrulaması (handleDragEnd) bu setten bağımsızdır, dokunulmadı.
  const attackTargets = useMemo(() => {
    if (!dragFromRegionId) return new Set<string>();
    return new Set(map.regions.filter((r) => r.id !== dragFromRegionId).map((r) => r.id));
  }, [dragFromRegionId, map.regions]);

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

  /**
   * Sürükleme sahipliğini bırakır. Tarayıcı pointerup/pointercancel'da capture'ı
   * zaten örtük olarak serbest bırakır, ama bunu açıkça yapmak (ve önce
   * `hasPointerCapture` ile doğrulamak) elemanın o sırada DOM'dan kalkmış olduğu
   * durumda `NotFoundError` fırlatılmasını da önler.
   */
  function releaseDragCapture(e: React.PointerEvent) {
    const target = e.currentTarget;
    if (target.hasPointerCapture(e.pointerId)) {
      target.releasePointerCapture(e.pointerId);
    }
  }

  /** Sürükleme state'ini sıfırlar — hem normal bitiş hem iptal bu yoldan geçer. */
  function resetDragState() {
    activePointerIdRef.current = null;
    dragStartClientRef.current = null;
    setDragFromRegionId(null);
    setDragPointerSvg(null);
    setDragHoverTargetId(null);
  }

  function handleDragStart(regionId: string) {
    return (e: React.PointerEvent) => {
      // Yalnızca birincil pointer sürükleme başlatır; zaten süren bir sürükleme
      // varsa ikinci bir parmak onu devralamaz (bkz. activePointerIdRef notu).
      if (!e.isPrimary || activePointerIdRef.current !== null) return;
      const regionState = regionStateById.get(regionId);
      const isMine = regionState?.ownerId === myPlayerId;
      if (state.status !== "Playing" || !isMine || (regionState?.soldierCount ?? 0) <= MIN_GARRISON_PER_SEND) {
        return;
      }
      e.currentTarget.setPointerCapture(e.pointerId);
      activePointerIdRef.current = e.pointerId;
      setDragFromRegionId(regionId);
      dragStartClientRef.current = { x: e.clientX, y: e.clientY };
      hasDraggedRef.current = false;
      const point = toSvgPoint(e.clientX, e.clientY);
      setDragPointerSvg(point);
      setDragHoverTargetId(null);
    };
  }

  function handleDragMove(e: React.PointerEvent) {
    if (!dragFromRegionId || e.pointerId !== activePointerIdRef.current) return;
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
    if (!dragFromRegionId || e.pointerId !== activePointerIdRef.current) return;
    releaseDragCapture(e);
    const point = toSvgPoint(e.clientX, e.clientY);
    const targetId = point ? findRegionAtPoint(point) : null;
    // Saldırı yalnızca GERÇEK bir sürüklemeden sonra gönderilir (bkz.
    // CLICK_VS_DRAG_THRESHOLD_PX notu) — eşik altında kalan bir dokunuş, parmak
    // komşu bir bölgenin üstüne kaysa bile yalnızca bir "tap"tır ve bölgeyi seçer.
    if (hasDraggedRef.current && targetId && targetId !== dragFromRegionId) {
      onAttack(dragFromRegionId, targetId);
    }
    if (hasDraggedRef.current) {
      suppressClickRef.current = true;
    }
    resetDragState();
  }

  /**
   * docs/24-responsive-small-screens.md Problem B: `pointercancel` bir bırakma
   * DEĞİLDİR — tarayıcı jesti sahiplendiğinde ya da pointer geçersizleştiğinde
   * gelir. Bu yüzden ASLA saldırı göndermez, yalnızca sürüklemeyi düşürür.
   * Eşik aşılmışsa ardından gelebilecek sahte click yine de bastırılır; aksi halde
   * iptal edilen bir sürükleme kaynak bölgenin bilgi panelini açardı.
   */
  function handleDragCancel(e: React.PointerEvent) {
    if (e.pointerId !== activePointerIdRef.current) return;
    releaseDragCapture(e);
    if (hasDraggedRef.current) {
      suppressClickRef.current = true;
    }
    resetDragState();
  }

  function handleRegionClick(regionId: string) {
    if (suppressClickRef.current) {
      suppressClickRef.current = false;
      return;
    }
    onSelectRegion(regionId);
  }

  // Sürükleme göstergeleri (nabız halkası + ok) rozetle AYNI noktadan çıkar —
  // bkz. `labelAnchorById`. Bölgenin geometrik merkezi kullanılsaydı ok, rozetin
  // görünür konumundan sapan bir yerden başlardı.
  const dragFromPoint = dragFromRegionId ? labelAnchorById.get(dragFromRegionId) : undefined;

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
    const anchor = labelAnchorById.get(region.id) ?? { x: region.x, y: region.y };
    return { region, regionState, isMine, color, accentColor, ownerSlot, draggable, anchor };
  });

  return (
    // docs/23-game-ui-refresh-v2.md Aşama 2 — harita artık çerçeveli bir kart değil.
    //
    // Önceki hâlde SVG'nin kendi kenarlığı ve `bg-card` zemini vardı; kap
    // `max-w-6xl` genişliğinde, harita ise ~1:1 olduğu için `preserveAspectRatio`
    // gereği ortaya oturuyor ve YANLARDA geniş boş zemin bantları kalıyordu —
    // "büyük çerçeveye yapıştırılmış küçük resim" görüntüsü. Çözüm haritayı
    // zorlamak değil, ÇERÇEVEYİ KALDIRMAK: bölgeler zaten tüm harita alanını
    // boşluksuz kapladığı için ayrı bir zemine ihtiyaç yok, letterbox alanı da
    // görünmez hale gelir. Bu aynı zamanda "node'lar kart gibi değil, haritanın
    // doğal parçası gibi" isteğiyle birebir uyumlu.
    //
    // Aşama 4 (sayfa iskeleti): sabit bir `vh`/`dvh` tavanı yerine harita artık
    // kendisine ayrılan alanın TAMAMINI kaplar (`h-full w-full`) — sayfa kabuğu
    // `flex-1 min-h-0` ile bu alanı hesaplar, yani harita her ekran yüksekliğinde
    // olabildiğince büyük olur ve hiçbir durum kartı onu küçültmez. Çerçeve/zemin
    // olmadığı için `preserveAspectRatio` letterbox'ı görünmez kalır.
    <svg
      ref={svgRef}
      viewBox={`${VIEW_MIN_X} ${VIEW_MIN_Y} ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
      // Aşama 5: dikeyde ORTALAMAK yerine ÜSTE hizala (`YMin`). Harita ~kare
      // olduğundan dikey boşluk yalnızca portre ekranlarda oluşur (masaüstünde ve
      // yatayda yükseklik zaten kısıtlayıcıdır, hizalamanın etkisi yoktur). Portrede
      // artan boşluğun altta toplanması, ActionPanel alttan açıldığında haritanın
      // daha büyük bir kısmının görünür kalması demek — oynanabilirlik > simetri.
      preserveAspectRatio="xMidYMin meet"
      className="block h-full w-full"
      role="img"
      aria-label="Lüksemburg haritası"
    >
      {/* docs/14-game-map-redesign.md Bölüm 4: kalıcı bir komşuluk çizgisi ağı (node-graph
          görünümü) kasıtlı olarak çizilmez — bölgeler artık gerçekten ortak sınır paylaşan
          polygon'lar, komşuluk zaten paylaşılan kenarlardan görülüyor. Bölgeler artık boşluksuz
          tüm haritayı kapladığından, hareket/sürükleme göstergeleri bölgelerin ÜSTÜNDE
          (sonra) çizilir — aksi halde tamamen gizlenirlerdi. */}
      <g>
        {regionRenderData.map(({ region, color, isMine, draggable }) => (
          <RegionShape
            key={region.id}
            region={region}
            color={color}
            // docs/23-game-ui-refresh-v2.md Aşama 2: sahiplik ikinci kanalı — kendi
            // bölgelerim renkten bağımsız, kalıcı bir halka taşır (bkz. RegionNode).
            isOwn={isMine}
            isSelected={selectedRegionId === region.id}
            isAttackTarget={attackTargets.has(region.id)}
            isDragSource={dragFromRegionId === region.id}
            isDragHoverTarget={dragHoverTargetId === region.id}
            draggable={draggable}
            onClick={() => handleRegionClick(region.id)}
            onDragStart={handleDragStart(region.id)}
            onDragMove={handleDragMove}
            onDragEnd={handleDragEnd}
            onDragCancel={handleDragCancel}
          />
        ))}
      </g>
      {/* docs/17-oyun-ici-ui-güclendirme.md Bölüm 8: sahip değişen bölgenin üstüne
          bindirilen, kendi kendine sönen (SVG native <animate>, ek JS/CSS animasyon
          döngüsü gerekmez) bir flash — GameMap CAPTURE_FLASH_MS sonra state'ten düşürür.

          docs/23-game-ui-refresh-v2.md Aşama 3 — efekt "dolgu yıkaması"ndan "sınır
          darbesi"ne çevrildi. Önceki hâl tüm polygon'u %65 opaklıkla neredeyse beyaza
          boyuyordu; bu, bölgenin el değiştirdiği ANDA — yani oyuncunun yeni asker
          sayısını en çok okumak istediği anda — rozetin çevresini beyaza çeviriyor ve
          rozeti dolgudan ayıran açık halkayı etkisiz bırakıyordu. Artık ağırlık
          sınırda: kenar kısaca parlayıp sönüyor, dolgu yalnızca hafifçe aydınlanıyor.
          Bilgi (bura el değiştirdi) korunuyor, okunabilirlik bozulmuyor. */}
      <g pointerEvents="none">
        {captureFlashes.map((flash) => {
          const points = flash.points.map(([x, y]) => `${x},${y}`).join(" ");
          return (
            <g key={flash.id}>
              <polygon
                points={points}
                fill="var(--game-capture-wash)"
                style={{
                  opacity: 0,
                  animation: `game-capture-wash ${CAPTURE_FLASH_MS}ms var(--game-ease-out) forwards`,
                }}
              />
              <polygon
                points={points}
                fill="none"
                stroke="var(--game-capture-edge)"
                strokeWidth={4}
                strokeLinejoin="round"
                vectorEffect="non-scaling-stroke"
                style={{
                  opacity: 0,
                  animation: `game-capture-edge ${CAPTURE_FLASH_MS}ms var(--game-ease-out) forwards`,
                }}
              />
            </g>
          );
        })}
      </g>
      {/* docs/15-asker-hareketi-performans.md Bölüm 6.1: bölge SVG'sinin üzerine
          bindirilen, gerçekten hareket eden bir sevkiyat katmanı — statik bir
          ilerleme çizgisi değil. Pozisyon TroopMarker'ın kendi requestAnimationFrame
          döngüsünde imperatif olarak hesaplanır (Bölüm 6.2), bu katman yalnızca
          hangi sevkiyatların var olduğu (yapısal) değiştiğinde yeniden render olur. */}
      <ArmyLayer
        markers={armyMarkers}
        pointById={labelAnchorById}
        slotByPlayerId={slotByPlayerId}
        roomType={state.room.type}
        registerHandle={registerHandle}
        unregisterHandle={unregisterHandle}
        removeMarker={removeMarker}
      />
      {/* docs/16-state.io-gorsel-referans.md Bölüm 1.3: sürükleme başladığında kaynak
          rozetin etrafında genişleyip solan bir halka — "buradan gönderiyorsun".

          docs/23-game-ui-refresh-v2.md Aşama 3: Tailwind'in `animate-ping` utility'si
          bir CSS `transform: scale()` üretir; SVG'de bunun dönüşüm merkezi (`transform-box`)
          tarayıcıya göre değişir, dolayısıyla halka bazı tarayıcılarda daireden değil
          viewBox köşesinden büyür. Yerine SVG'nin kendi `<animate>`'i kullanılıyor —
          `r` doğrudan animate edildiği için dönüşüm merkezi sorunu hiç doğmaz ve bu,
          hemen yukarıdaki ele geçirme flash'ıyla aynı desen (ek kütüphane/keyframe yok). */}
      {dragFromPoint ? (
        <circle
          cx={dragFromPoint.x}
          cy={dragFromPoint.y}
          // Rozetin (48×31) dışından başlar — içeriden büyüseydi sayının üstünü tarardı.
          r={28}
          fill="none"
          stroke={myArrowColor}
          strokeWidth={2}
          vectorEffect="non-scaling-stroke"
          pointerEvents="none"
          style={{
            // Dönüşüm merkezi AÇIKÇA veriliyor: SVG'de CSS transform'un varsayılan
            // referans kutusu tarayıcıya göre değişir; verilmezse halka daireden
            // değil viewBox köşesinden büyür (eski `animate-ping` kullanımının sorunu).
            transformBox: "view-box",
            transformOrigin: `${dragFromPoint.x}px ${dragFromPoint.y}px`,
            animation: "game-drag-ping 1.15s var(--game-ease-out) infinite",
          }}
        />
      ) : null}
      {/* docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 14-17: sürükleme önizlemesi net
          bir arrowhead'i olan bir ok — bkz. computeAttackArrow (lib/game/arrow.ts).

          docs/23-game-ui-refresh-v2.md Aşama 3 — üç şey eklendi:
          1. GEÇERLİ/GEÇERSİZ AYRIMI. `dragHoverTargetId` doluysa bırakma bir saldırıya
             dönüşür; boşsa (parmak kaynağın kendi üstünde ya da hiçbir bölgede değil)
             bırakma hiçbir şey yapmaz. Eskiden iki durum birebir aynı görünüyordu, yani
             "buraya bırakırsam ne olur" sorusunun görsel cevabı yoktu. Artık geçersiz
             durumda ok kesikli, soluk ve ucu içi boş — iptal yolu böylece açıkça
             görünür hale gelir (mevcut sürükleme/bırakma DAVRANIŞI değişmedi, yalnızca
             görünür oldu; doğrulama hâlâ handleDragEnd'de).
          2. KONTRAST. Ok, hem açık bölge dolgularının hem koyu harita zemininin üstünden
             geçiyor; tek renkli bir çizgi ikisinden birinde kayboluyordu. Altına koyu bir
             halo çizilip üstüne oyuncunun AÇIK kimlik rengi bindiriliyor.
          3. Uç büyütüldü (bkz. arrow.ts) — yön mobilde de okunuyor. */}
      {dragFromPoint && dragPointerSvg
        ? (() => {
            const arrow = computeAttackArrow(dragFromPoint, dragPointerSvg);
            if (!arrow) return null;
            const isValidDrop = dragHoverTargetId !== null;
            const headPoints = arrowheadPointsAttr(arrow.arrowheadPoints);
            return (
              <g pointerEvents="none" opacity={isValidDrop ? 1 : 0.6}>
                <line
                  x1={arrow.lineStart.x}
                  y1={arrow.lineStart.y}
                  x2={arrow.lineEnd.x}
                  y2={arrow.lineEnd.y}
                  stroke="var(--game-arrow-halo)"
                  strokeWidth={7.5}
                  strokeLinecap="round"
                />
                <line
                  x1={arrow.lineStart.x}
                  y1={arrow.lineStart.y}
                  x2={arrow.lineEnd.x}
                  y2={arrow.lineEnd.y}
                  stroke={isValidDrop ? myArrowColor : "var(--game-arrow-invalid)"}
                  strokeWidth={4}
                  strokeLinecap="round"
                  strokeDasharray={isValidDrop ? undefined : "10 8"}
                />
                <polygon
                  points={headPoints}
                  fill={isValidDrop ? myArrowColor : "none"}
                  stroke={isValidDrop ? "var(--game-arrow-halo)" : "var(--game-arrow-invalid)"}
                  strokeWidth={isValidDrop ? 1.5 : 3}
                  strokeLinejoin="round"
                />
              </g>
            );
          })()
        : null}
      {/* docs/14-game-map-redesign.md: etiketler (rozet/isim) en üst katmanda, tüm
          bölge polygon'ları çizildikten sonra render edilir — bkz. RegionNode.tsx üstündeki not. */}
      <g pointerEvents="none">
        {regionRenderData.map(({ region, regionState, isMine, accentColor, anchor }) => (
          <RegionLabel
            key={region.id}
            region={region}
            anchor={anchor}
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
