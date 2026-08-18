"use client";

import { memo, useEffect, useRef } from "react";
import { lightenHex } from "@/lib/game/colors";
import type { MapRegionDto, RegionStateDto } from "@/lib/game/types";

interface RegionShapeProps {
  region: MapRegionDto;
  color: string;
  /**
   * docs/23-game-ui-refresh-v2.md Aşama 2: sahiplik artık YALNIZCA renkle
   * gösterilmiyor. Bu bayrak, renkten bağımsız (akromatik) ikinci kanalı
   * tetikler — kendi bölgelerim sürekli duran parlak bir halka taşır. Renk
   * körlüğünde 12 takım rengi tam ayrışamadığı için (bkz. colors.ts ölçümü)
   * "hangisi benim" sorusunun cevabı bu halkadan okunur.
   */
  isOwn: boolean;
  isSelected: boolean;
  /** Yalnızca AKTİF BİR SÜRÜKLEME sırasında dolu — bkz. GameMap `attackTargets`. */
  isAttackTarget: boolean;
  isDragSource: boolean;
  isDragHoverTarget: boolean;
  draggable: boolean;
  onClick: () => void;
  onDragStart: (e: React.PointerEvent) => void;
  onDragMove: (e: React.PointerEvent) => void;
  onDragEnd: (e: React.PointerEvent) => void;
  /**
   * docs/24-responsive-small-screens.md Problem B: `pointercancel` bir BIRAKMA
   * DEGILDIR — tarayici jesti sahiplendiginde (scroll/pan/zoom) gelir. Eskiden
   * `onDragEnd` ile ayni handler'a bagliydi ve o handler kosulsuz saldiri
   * gonderiyordu; yani oyuncunun hic birakmadigi bir sevkiyat tetiklenebiliyordu.
   * Iptalin kendi yolu var: saldiri gondermeden yalnizca state'i temizler.
   */
  onDragCancel: (e: React.PointerEvent) => void;
}

/**
 * docs/14-game-map-redesign.md Bölüm 3/4: bölge bir daire/node değil, komşularıyla
 * ortak sınır paylaşan bir SVG polygon'dur (bkz. map.json geometry alanı). Asker
 * gönderme hâlâ bu şeklin üzerinde doğrudan sürükle-bırak ile yapılır
 * (docs/03-game-rules.md Bölüm 6/15) — masaüstü fare ve mobil dokunmatik aynı
 * pointer-event mantığını kullanır.
 *
 * Yalnızca dolgu/kenarlık — GameMap bunu TÜM bölgeler için ayrı bir katmanda
 * (RegionLabel'dan ÖNCE) çizer. Bölgeler boşluksuz bitişik olduğundan, bir bölgenin
 * etiketi komşusunun alanına taşabilir; etiketler ayrı ve sonraki bir katmanda
 * çizilmezse sonradan çizilen bir komşu polygon önceki bölgenin etiketinin bir
 * kısmını gizler (bkz. RegionLabel).
 *
 * docs/23-game-ui-refresh-v2.md Aşama 2 — durum matrisi. Kenarlık tek bir
 * hiyerarşi olarak çözülür, ilk eşleşen kazanır:
 *
 *   sürüklenen hedef  → tam beyaz, en kalın   (o an ne vuracağım)
 *   seçili / kaynak   → tam beyaz, kalın      (nereden gönderiyorum)
 *   benim bölgem      → beyaz %92, orta       (kalıcı sahiplik kanalı)
 *   geçerli hedef     → beyaz %32, ince       (yalnızca sürükleme sırasında)
 *   nötr / rakip      → koyu harita sınırı    (varsayılan, en düşük kontrast)
 *
 * Önceki uygulamada "geçerli hedef" KESİKLİ bir aksan kenarlıktı ve saldırı
 * komşulukla sınırlı olmadığı için (GameConfig.AttackAdjacencyOnly=false) bir
 * bölge seçilir seçilmez haritadaki DİĞER TÜM bölgeler kesikli çizgiye
 * boğuluyordu; asker sayıları bu tarama deseninin altında kayboluyordu. Kural
 * değişmedi (tüm bölgeler hâlâ geçerli hedef) — yalnızca gösterimi değişti:
 * ipucu artık kesiksiz, çok daha soluk ve yalnızca gerçekten bir sürükleme
 * varken çiziliyor, asıl vurgu ise o an nişan alınan hedefte.
 */
function RegionShapeImpl({
  region,
  color,
  isOwn,
  isSelected,
  isAttackTarget,
  isDragSource,
  isDragHoverTarget,
  draggable,
  onClick,
  onDragStart,
  onDragMove,
  onDragEnd,
  onDragCancel,
}: RegionShapeProps) {
  const pointsAttr = region.geometry.points.map(([x, y]) => `${x},${y}`).join(" ");

  const edge = isDragHoverTarget
    ? { stroke: "var(--game-ring-hover)", strokeWidth: "var(--game-ring-hover-width)" }
    : isSelected || isDragSource
      ? { stroke: "var(--game-ring-selected)", strokeWidth: "var(--game-ring-selected-width)" }
      : isOwn
        ? { stroke: "var(--game-ring-own)", strokeWidth: "var(--game-ring-own-width)" }
        : isAttackTarget
          ? { stroke: "var(--game-ring-target)", strokeWidth: "var(--game-ring-target-width)" }
          : { stroke: "var(--game-map-edge)", strokeWidth: "var(--game-edge-width)" };

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
      onPointerCancel={isDragSource ? onDragCancel : undefined}
      className={
        // docs/04-style.md Bölüm 13: odak göstergesi hiçbir yerde tamamen kaldırılmaz —
        // fare tıklamasında varsayılan tarayıcı halkası bastırılır, ama klavye ile
        // (Tab) gelen odakta focus-visible ile görünür bir gösterge kalır.
        //
        // docs/24-responsive-small-screens.md Problem B: buradaki `touch-none`
        // KALDIRILDI. `touch-action`, Chromium ve WebKit'te SVG alt elemanlarinda
        // (`<g>`, `<polygon>`) uygulanmaz — hit-test edilen SVG layout nesnesi bu
        // degeri tasimaz. Sinif kodda "var" gorundugu halde etkisizdi ve harita
        // yuzeyinin gercek touch-action'i `auto` kaliyordu; tarayici tek parmak
        // hareketini pan adayi sayip pointer akisini `pointercancel` ile kesiyordu
        // (fare bu yoldan hic gecmedigi icin sorun yalnizca dokunmatikte gorunurdu).
        // Kural artik HTML tarafindaki harita yuzeyinde: `[data-game-map-surface]`
        // (bkz. app/globals.css + app/game/[matchId]/page.tsx).
        (draggable ? "cursor-grab active:cursor-grabbing" : "cursor-pointer") +
        " outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      }
    >
      <title>{region.name}</title>
      <polygon
        points={pointsAttr}
        fill={color}
        // Sürükleme kaynağı hafifçe geri çekilir ki üstündeki ok ve hedef vurgusu
        // öne çıksın — eskiden 0.6 idi, bu kadar şeffaflıkta bölgenin kendi asker
        // sayısı okunamaz hale geliyordu (öncelik zinciri: asker sayıları önce).
        opacity={isDragSource ? 0.82 : 1}
        // docs/04-style.md Bölüm 9 "Hover": hafif opaklık değişimi + işaretçi
        // değişimi; sahiplik rengi karışmasın diye renk değişmez. Sürükleme
        // sırasında geçiş uygulanmaz (kaynak zaten kendi durumunda).
        className={
          isDragSource
            ? undefined
            : "transition-opacity duration-(--game-dur-fast) ease-(--game-ease-out) hover:opacity-90 active:opacity-[0.86]"
        }
        // Kenarlık kalınlıkları harita ölçeğinden bağımsız gerçek ekran pikselidir
        // (bkz. globals.css `--game-edge-width` notu) — aksi halde 360px'te
        // sahiplik halkası görünmeyecek kadar incelirdi.
        vectorEffect="non-scaling-stroke"
        strokeLinejoin="round"
        style={edge}
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
 *
 * ⚠️ Bu listeye yeni bir görsel prop eklenirse BURAYA da eklenmelidir; unutulursa o
 * prop değiştiğinde bölge yeniden çizilmez (bayat kalır).
 */
function regionShapeEqual(prev: RegionShapeProps, next: RegionShapeProps): boolean {
  return (
    prev.region === next.region &&
    prev.color === next.color &&
    prev.isOwn === next.isOwn &&
    prev.isSelected === next.isSelected &&
    prev.isAttackTarget === next.isAttackTarget &&
    prev.isDragSource === next.isDragSource &&
    prev.isDragHoverTarget === next.isDragHoverTarget &&
    prev.draggable === next.draggable
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
  /**
   * docs/23-game-ui-refresh-v2.md Aşama 2: rozetin oturacağı nokta. Bölgenin
   * geometrik merkezi (`region.x/y`) DEĞİL — içbükey bölgelerde o nokta kenara
   * çok yakın düşüp rozetin komşu bölgeye taşmasına yol açıyordu (bkz. GameMap
   * `labelAnchorForPolygon`). GameMap tarafından harita başına bir kez hesaplanır,
   * referansı sabittir (memo karşılaştırıcısı buna güvenir).
   */
  anchor: { x: number; y: number };
  regionState: RegionStateDto | undefined;
  isMine: boolean;
  /** GameMap tarafından owner + oda tipine göre önceden çözülmüş rozet rengi — bu bileşen kendi içinde ayrı bir renk türetmez. Sahipsiz bölgede nötr koyu ton. */
  accentColor: string;
  /** Yalnızca bu bölgeye az önce bir sevkiyat ulaştıysa dolu — aksi halde rozet her zamanki gibi anında günceller (§2.B.3, genel kural değişmedi). */
  arrivalCountdown?: ArrivalCountdown | null;
}

/** docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2: geri sayımın gerçek süresi — gerçek kayıttaki "yaklaşık yarım-bir saniyelik hızlı geri sayım" ile uyumlu. */
const ARRIVAL_COUNTDOWN_MS = 700;

/**
 * docs/23-game-ui-refresh-v2.md §5 "En önemli UX kuralı": bir bölgeye bakıldığında
 * ilk görülen şey kaç asker olduğudur. Rozet ölçüleri buna göre seçildi:
 *
 * - Sabit genişlikte bir hap (elips değil). Sayı 1, 2 veya 3 haneli olsun rozet
 *   AYNI boyutta kalır — "öngörülebilir genişlik" isteği bu; bölgeden bölgeye
 *   zıplayan bir rozet gözü yorar ve varış geri sayımında titremeye yol açardı.
 * - Genişlik, en küçük bölgenin sınırlayıcı kutusuna (ölçüldü: 116×112 birim)
 *   sığacak şekilde seçildi; 590 birimlik viewBox 360px'e indiğinde sayı ~13px
 *   fiziksel boyutta kalır (önceki tasarımda ~9px idi).
 * - Sayı her zaman beyaz; rozet tonu colors.ts'te bu beyaza ≥7.7:1 kontrast
 *   verecek şekilde türetilir (AAA).
 */
const BADGE_WIDTH = 48;
const BADGE_HEIGHT = 31;
const BADGE_RADIUS = 15.5;
const NUMBER_FONT_SIZE = 21;
/** Rakamın optik merkezini rozetin geometrik merkezine oturtan taban çizgisi kaydırması. */
const NUMBER_BASELINE_OFFSET = 7.4;
const NAME_FONT_SIZE = 11;
const NAME_BASELINE_OFFSET = 30;

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}

/**
 * Asker sayısı rozeti ve bölge adı — GameMap tarafından TÜM bölge polygon'ları
 * çizildikten SONRA, ayrı bir üst katmanda render edilir ki hiçbir komşu bölgenin
 * dolgusu bu etiketleri örtmesin. Etkileşimsizdir (pointerEvents:none, bkz. GameMap) —
 * tıklama/sürükleme her zaman RegionShape üzerinden işlenir.
 *
 * docs/03-game-rules.md (müşteri kararı — "kale gibi bir alan olmayacak"): ayrı bir
 * "başkent/ana üs" rozeti YOK, tüm sahipli bölgeler (ilk verilen toprak dahil) aynı
 * boyut/koyulukta gösterilir.
 *
 * docs/23-game-ui-refresh-v2.md Aşama 2 — bilgi hiyerarşisi iki farklı tipografik
 * REGISTER ile kurulur, yalnızca boyut farkıyla değil: asker sayısı koyu zemin
 * üzerinde beyaz/ağır/büyük, bölge adı ise açık zemin üzerinde koyu/ince/küçük.
 * İkisi farklı "dil" konuştuğu için göz artık ikisi arasında bölünmez.
 *
 * Bölge adı, dolgusu ne olursa olsun okunabilir kalsın diye açık bir halo ile
 * (paint-order: stroke) çizilir — ölçümde, isim mürekkebi bazı takım renkleri
 * üzerinde AA eşiğini (4.5:1) tutturamıyordu (en kötü durum indigo dolguda 3.96);
 * halo bu bağımlılığı tamamen ortadan kaldırır, metin artık halonun üstünde okunur.
 *
 * docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2: `arrivalCountdown` dolu
 * olduğunda rozetteki sayı `from`'dan `to`'ya hızlı bir geri sayımla iner ve dolgu
 * kısa süreliğine kendi renginin daha açık tonuna parlar — `textRef`/`flashRef`
 * üzerinden doğrudan DOM'a yazan bir requestAnimationFrame döngüsüyle (TroopMarker'daki
 * aynı imperatif desen — React state/re-render tetiklemez). Genel kural DEĞİŞMEDİ
 * (§2.B.3): rutin üretim tik'lerinde rozet sayısı hâlâ anında/animasyonsuz günceller.
 */
function RegionLabelImpl({ region, anchor, regionState, isMine, accentColor, arrivalCountdown }: RegionLabelProps) {
  const isUnexplored = regionState?.isVisible === false;
  const garrison = regionState?.soldierCount ?? 0;

  const textRef = useRef<SVGTextElement>(null);
  const flashRef = useRef<SVGRectElement>(null);

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

  const badgeX = anchor.x - BADGE_WIDTH / 2;
  const badgeY = anchor.y - BADGE_HEIGHT / 2;

  return (
    <g>
      <rect
        x={badgeX}
        y={badgeY}
        width={BADGE_WIDTH}
        height={BADGE_HEIGHT}
        rx={BADGE_RADIUS}
        fill={accentColor}
        vectorEffect="non-scaling-stroke"
        // Rozeti bölgenin kendi dolgusundan ayıran açık halka. Ölçümde rozet/dolgu
        // kontrastı bazı slotlarda 2.2:1'e kadar düşebiliyor — bu halka ayrışmayı
        // renkten bağımsız olarak garanti eder.
        //
        // Token'lı değerler bilinçli olarak `style` üzerinden veriliyor: `var()`
        // bir SVG presentation attribute'unda (stroke="var(--x)") tarayıcılar
        // arasında güvenilir şekilde çözülmez, CSS özelliği olarak ise çözülür.
        style={{ stroke: "var(--game-badge-ring)", strokeWidth: "var(--game-badge-ring-width)" }}
      />
      {arrivalCountdown ? (
        <rect
          ref={flashRef}
          x={badgeX}
          y={badgeY}
          width={BADGE_WIDTH}
          height={BADGE_HEIGHT}
          rx={BADGE_RADIUS}
          fill={lightenHex(accentColor, 0.55)}
          opacity={0}
          pointerEvents="none"
        />
      ) : null}
      <text
        ref={textRef}
        x={anchor.x}
        y={anchor.y + NUMBER_BASELINE_OFFSET}
        textAnchor="middle"
        fontSize={NUMBER_FONT_SIZE}
        fontWeight={800}
        // tabular-nums: 1/2/3 haneli sayılarda rakam genişliği sabit kalır, sayı
        // rozetin içinde zıplamaz — özellikle varış geri sayımında kritik.
        // `fill` token'ı için bkz. yukarıdaki `style` notu (var() + presentation attribute).
        style={{ fill: "var(--game-text-on-badge)", fontVariantNumeric: "tabular-nums" }}
      >
        {arrivalCountdown ? arrivalCountdown.from : garrison}
      </text>
      <text
        x={anchor.x}
        y={anchor.y + NAME_BASELINE_OFFSET}
        textAnchor="middle"
        fontSize={NAME_FONT_SIZE}
        fontWeight={isMine ? 600 : 500}
        strokeWidth={2.6}
        paintOrder="stroke"
        strokeLinejoin="round"
        // Token'lar `style` üzerinden — bkz. yukarıdaki rozet notundaki gerekçe
        // (`var()` bir SVG presentation attribute'unda güvenilir çözülmez).
        style={{ fill: "var(--game-label-ink)", stroke: "var(--game-label-halo)" }}
      >
        {region.name}
      </text>
    </g>
  );
}

/** docs/15-asker-hareketi-performans.md Bölüm 6.4: bkz. regionShapeEqual üstündeki not — aynı gerekçe ve aynı uyarı. */
function regionLabelEqual(prev: RegionLabelProps, next: RegionLabelProps): boolean {
  return (
    prev.region === next.region &&
    prev.anchor === next.anchor &&
    prev.regionState?.ownerId === next.regionState?.ownerId &&
    prev.regionState?.soldierCount === next.regionState?.soldierCount &&
    prev.regionState?.isVisible === next.regionState?.isVisible &&
    prev.isMine === next.isMine &&
    prev.accentColor === next.accentColor &&
    prev.arrivalCountdown === next.arrivalCountdown
  );
}

export const RegionLabel = memo(RegionLabelImpl, regionLabelEqual);
