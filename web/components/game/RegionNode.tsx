"use client";

import { memo, useEffect, useRef } from "react";
import { lightenHex } from "@/lib/game/colors";
import type { MapRegionDto, RegionStateDto } from "@/lib/game/types";

interface RegionShapeProps {
  region: MapRegionDto;
  color: string;
  isSelected: boolean;
  isAttackTarget: boolean;
  isDragSource: boolean;
  isDragHoverTarget: boolean;
  draggable: boolean;
  /** docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 5/18/19: seçim/hedef vurgusu artık sabit
   * siyah değil, sürükleyen oyuncunun kendi kimlik rengi (bkz. GameMap: playerAccentColor). */
  selectionColor: string;
  onClick: () => void;
  onDragStart: (e: React.PointerEvent) => void;
  onDragMove: (e: React.PointerEvent) => void;
  onDragEnd: (e: React.PointerEvent) => void;
}

/**
 * docs/14-game-map-redesign.md Bölüm 3/4: bölge artık bir daire/node değil, komşularıyla
 * ortak sınır paylaşan bir SVG polygon'dur (bkz. map.json geometry alanı). Asker gönderme
 * hâlâ bu şeklin üzerinde doğrudan sürükle-bırak ile yapılır (docs/03-game-rules.md
 * Bölüm 6/15) — masaüstü fare ve mobil dokunmatik aynı pointer-event mantığını kullanır.
 *
 * Yalnızca dolgu/kenarlık — GameMap bunu TÜM bölgeler için ayrı bir katmanda (RegionLabel'dan
 * ÖNCE) çizer. Bölgeler artık boşluksuz bitişik olduğundan, bir bölgenin etiketi komşusunun
 * alanına taşabilir; etiketler ayrı ve sonraki bir katmanda çizilmezse sonradan çizilen bir
 * komşu polygon'u önceki bölgenin etiketinin bir kısmını gizler (bkz. RegionLabel).
 */
function RegionShapeImpl({
  region,
  color,
  isSelected,
  isAttackTarget,
  isDragSource,
  isDragHoverTarget,
  draggable,
  selectionColor,
  onClick,
  onDragStart,
  onDragMove,
  onDragEnd,
}: RegionShapeProps) {
  const pointsAttr = region.geometry.points.map(([x, y]) => `${x},${y}`).join(" ");

  return (
    <g
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick();
      }}
      onPointerDown={draggable ? onDragStart : undefined}
      onPointerMove={isDragSource ? onDragMove : undefined}
      onPointerUp={isDragSource ? onDragEnd : undefined}
      onPointerCancel={isDragSource ? onDragEnd : undefined}
      className={
        // docs/04-style.md Bölüm 13: odak göstergesi hiçbir yerde tamamen kaldırılmaz —
        // fare tıklamasında varsayılan tarayıcı halkası bastırılır, ama klavye ile
        // (Tab) gelen odakta focus-visible ile görünür bir gösterge kalır.
        (draggable ? "cursor-grab" : "cursor-pointer") +
        " touch-none outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      }
    >
      <title>{region.name}</title>
      <polygon
        points={pointsAttr}
        fill={color}
        opacity={isDragSource ? 0.6 : 1}
        // docs/04-style.md Bölüm 9 "Hover": hafif opaklık azalması + işaretçi değişimi,
        // sahiplik rengi karışmasın diye renk değişmez — sürükleme sırasında karışmasın
        // diye bu geçiş yalnızca sürükleme dışı durumda uygulanır.
        className={isDragSource ? undefined : "transition-opacity duration-150 hover:opacity-90"}
        stroke={
          isDragHoverTarget || isSelected || isAttackTarget ? selectionColor : "rgba(17,24,39,0.35)"
        }
        strokeOpacity={isAttackTarget && !isSelected && !isDragHoverTarget ? 0.55 : 1}
        strokeWidth={isDragHoverTarget ? 3.5 : isSelected ? 2.5 : isAttackTarget ? 2 : 1.5}
        strokeLinejoin="round"
        strokeDasharray={isAttackTarget && !isSelected && !isDragHoverTarget ? "4 3" : undefined}
      />
    </g>
  );
}

/**
 * docs/15-asker-hareketi-performans.md Bölüm 6.4/7: sunucu her saniye TÜM MatchState'i
 * (dolayısıyla `regionState`'i) yeni bir obje referansıyla gönderir; bu yüzden yalnızca
 * referans karşılaştırması (varsayılan React.memo) burada işe yaramaz — geometri/tıklama
 * durumu değişmeyen bir bölge yine de her tick'te yeniden render olurdu. Karşılaştırma
 * bilinçli olarak `onClick`/`onDragStart` gibi callback prop'ları YOK SAYAR (GameMap
 * bunları her render'da yeniden oluşturur, ama davranışları değişmez) — yalnızca
 * gerçekten görsel sonucu etkileyen veri alanları karşılaştırılır.
 */
function regionShapeEqual(prev: RegionShapeProps, next: RegionShapeProps): boolean {
  return (
    prev.region === next.region &&
    prev.color === next.color &&
    prev.isSelected === next.isSelected &&
    prev.isAttackTarget === next.isAttackTarget &&
    prev.isDragSource === next.isDragSource &&
    prev.isDragHoverTarget === next.isDragHoverTarget &&
    prev.draggable === next.draggable &&
    prev.selectionColor === next.selectionColor
  );
}

export const RegionShape = memo(RegionShapeImpl, regionShapeEqual);

/** docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2: hedefe ulaşan bir sevkiyatın, o bölgenin rozetinde tetiklediği kısa geri sayım + renk parlaması. */
export interface ArrivalCountdown {
  from: number;
  to: number;
  startedAt: number;
}

interface RegionLabelProps {
  region: MapRegionDto;
  regionState: RegionStateDto | undefined;
  isMine: boolean;
  /** docs/16-state.io-gorsel-referans.md Bölüm 7: GameMap tarafından owner + oda tipine göre
   * önceden çözülmüş rozet rengi (Standart odada rakip için PlayerStandardOpponent dahil) —
   * bu bileşen kendi içinde ayrı bir renk türetmez. Sahipsiz bölgede nötr koyu ton. */
  accentColor: string;
  /** Yalnızca bu bölgeye az önce bir sevkiyat ulaştıysa dolu — aksi halde rozet her zamanki gibi anında günceller (§2.B.3, genel kural değişmedi). */
  arrivalCountdown?: ArrivalCountdown | null;
}

/** docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2: geri sayımın gerçek süresi — gerçek kayıttaki "yaklaşık yarım-bir saniyelik hızlı geri sayım" ile uyumlu. */
const ARRIVAL_COUNTDOWN_MS = 700;

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}

/**
 * Asker sayısı rozeti ve bölge adı — GameMap tarafından TÜM bölge polygon'ları
 * çizildikten SONRA, ayrı bir üst katmanda render edilir ki hiçbir komşu bölgenin
 * dolgusu bu etiketleri örtmesin. Etkileşimsizdir (pointerEvents:none, bkz. GameMap) —
 * tıklama/sürükleme her zaman RegionShape üzerinden işlenir.
 *
 * docs/16-state.io-gorsel-referans.md Bölüm 1.2/3: rozet artık her yerde aynı beyaz
 * floating oval DEĞİL — owner'ın kimlik renginin koyu tonuyla dolu, ince açık bir
 * halkayla bölgenin kendi pastel dolgusundan ayrışır. docs/03-game-rules.md (müşteri
 * kararı — "kale gibi bir alan olmayacak"): ayrı bir "başkent/ana üs" rozeti YOK, tüm
 * sahipli bölgeler (ilk verilen toprak dahil) aynı boyut/koyulukta gösterilir — bu
 * madde büyütmeden SONRA da geçerlidir, aşağıdaki boyut/ağırlık HER bölgeye eşit uygulanır.
 *
 * docs/20-state-io-army-gorsel-fark-giderme.md §2.B.1: bilgi hiyerarşisi artık
 * "army sayısı > sahiplik rengi > bölge adı" — rozet büyütüldü, sayı daha ağır/büyük,
 * isim ikincil kalacak şekilde küçültüldü (14-game-map-redesign.md §5, 04-style.md §9
 * bu hiyerarşiyi yansıtacak şekilde güncellendi, bkz. o dosyalar).
 *
 * docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2 (gerçek state.io kaydıyla düzeltildi):
 * `arrivalCountdown` dolu olduğunda (yalnızca bu bölgeye az önce bir sevkiyat ulaştığında,
 * GameMap tarafından tetiklenir), rozetteki sayı `from`'dan `to`'ya ~0.5-1 sn'lik hızlı bir
 * geri sayımla iner ve dolgu kısa süreliğine kendi renginin daha açık tonuna parlar — bu,
 * `textRef`/`flashRef` üzerinden doğrudan DOM'a yazan bir requestAnimationFrame döngüsüyle
 * yapılır (TroopMarker'daki aynı imperatif desen, bkz. Bölüm 6.2 "React state/re-render
 * tetiklemez"). Her yerde geçerli olan genel kural DEĞİŞMEDİ (§2.B.3): rutin üretim
 * tik'lerinde veya bu koşulun dışında rozet sayısı hâlâ anında/animasyonsuz günceller.
 */
function RegionLabelImpl({ region, regionState, isMine, accentColor, arrivalCountdown }: RegionLabelProps) {
  const isUnexplored = regionState?.isVisible === false;
  const garrison = regionState?.soldierCount ?? 0;

  const textRef = useRef<SVGTextElement>(null);
  const flashRef = useRef<SVGEllipseElement>(null);

  useEffect(() => {
    if (!arrivalCountdown) return;
    const { from, to, startedAt } = arrivalCountdown;
    let rafId = 0;

    const tick = () => {
      const t = clamp01((Date.now() - startedAt) / ARRIVAL_COUNTDOWN_MS);
      const displayed = Math.round(from + (to - from) * t);
      if (textRef.current) {
        textRef.current.textContent = String(displayed);
      }
      if (flashRef.current) {
        // Kayıttaki davranış: parlama hızlıca tepe yapıp geri sönüyor (bir "flash", sabit bir parlaklık değil).
        const flashT = t < 0.5 ? t / 0.5 : 1 - (t - 0.5) / 0.5;
        flashRef.current.style.opacity = String(flashT * 0.8);
      }
      if (t < 1) {
        rafId = requestAnimationFrame(tick);
      }
    };

    rafId = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafId);
  }, [arrivalCountdown]);

  if (isUnexplored) return null;

  return (
    <g>
      <ellipse cx={region.x} cy={region.y} rx={16.5} ry={12.5} fill={accentColor} stroke="rgba(250,247,240,0.9)" strokeWidth={1.75} />
      {arrivalCountdown ? (
        <ellipse
          ref={flashRef}
          cx={region.x}
          cy={region.y}
          rx={16.5}
          ry={12.5}
          fill={lightenHex(accentColor, 0.55)}
          opacity={0}
          pointerEvents="none"
        />
      ) : null}
      <text ref={textRef} x={region.x} y={region.y + 4.5} textAnchor="middle" fontSize="15" fontWeight={700} fill="#FAF7F0">
        {arrivalCountdown ? arrivalCountdown.from : garrison}
      </text>
      <text
        x={region.x}
        y={region.y + 25}
        textAnchor="middle"
        fontSize="8.5"
        fontWeight={isMine ? 500 : 400}
        fill="#111827"
      >
        {region.name}
      </text>
    </g>
  );
}

/** docs/15-asker-hareketi-performans.md Bölüm 6.4: bkz. regionShapeEqual üstündeki not — aynı gerekçe. */
function regionLabelEqual(prev: RegionLabelProps, next: RegionLabelProps): boolean {
  return (
    prev.region === next.region &&
    prev.regionState?.ownerId === next.regionState?.ownerId &&
    prev.regionState?.soldierCount === next.regionState?.soldierCount &&
    prev.regionState?.isVisible === next.regionState?.isVisible &&
    prev.isMine === next.isMine &&
    prev.accentColor === next.accentColor &&
    prev.arrivalCountdown === next.arrivalCountdown
  );
}

export const RegionLabel = memo(RegionLabelImpl, regionLabelEqual);
