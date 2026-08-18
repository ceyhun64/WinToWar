"use client";

import { memo, useEffect, useRef } from "react";
import type { TroopMarkerHandle } from "@/lib/game/useArmyAnimation";

interface Point {
  x: number;
  y: number;
}

interface TroopMarkerProps {
  id: string;
  fromPoint: Point;
  toPoint: Point;
  departedAtUtc: number;
  arrivesAtUtc: number;
  initialSoldierCount: number;
  /** docs/15-asker-hareketi-performans.md §5.1: owner'ın KOYU aksan tonu (playerAccentColor) — açık/zemin tonu değil, aksi halde partikül bölge zeminiyle karışıp içi boş görünür. */
  color: string;
  onRegisterHandle: (id: string, handle: TroopMarkerHandle) => void;
  onUnregisterHandle: (id: string) => void;
  onRemove: (id: string) => void;
}

// docs/15-asker-hareketi-performans.md Bölüm 5.3: playful his dekoratif efektten
// değil hareketten gelir — süreler kısa/hafif tutulur, ağır bir "patlama" efekti yok.
const POP_IN_MS = 220;
const CLASH_IMPACT_MS = 260;
const DEATH_MS = 320;

// Müşteri kararı: gönderilen asker sayısı kadar (bir sayı rozetiyle DEĞİL, gerçek
// ikon adediyle) gösterilir — sabit "1-3 küme" soyutlaması ve üstteki sayı rozeti
// kaldırıldı. Performans/okunabilirlik için üst sınır var (koca bir orduyu tek tek
// yüzlerce nokta olarak çizmek hem yavaşlar hem "kalabalık" değil "karmaşa" hissi verir).
const MAX_VISIBLE_TROOPS = 10;

/**
 * docs/23-game-ui-refresh-v2.md Aşama 3 — ikon yarıçapı artık sabit değil, sevkiyatın
 * büyüklüğüyle birlikte hafifçe büyür.
 *
 * Gerekçe: müşteri kararı gereği ekranda sayı rozeti YOK ve ikon adedi
 * `MAX_VISIBLE_TROOPS`'ta tavan yapıyor — yani 10 asker gönderen bir sevkiyat ile
 * 300 asker gönderen bir sevkiyat BİREBİR aynı görünüyordu. Öncelik zincirinin en
 * tepesi "asker sayıları" olduğu için bu gerçek bir bilgi kaybı. Boyut, sayıyı geri
 * getirmeden (🔒 "bir sayı rozetiyle DEĞİL" kararı korunuyor) büyüklük hissini verir.
 *
 * Ölçek bilinçli olarak logaritmik ve dar tutuldu (yalnızca ~%40 büyüme): amaç
 * "kabaca ne kadar büyük bir dalga geliyor" hissi, kesin bir okuma değil.
 */
const TROOP_ICON_MIN_RADIUS = 3.1;
const TROOP_ICON_MAX_RADIUS = 4.4;
const TROOP_ICON_SATURATION_COUNT = 120;

function troopIconRadius(soldierCount: number): number {
  const t = clamp01(Math.log10(1 + Math.max(0, soldierCount)) / Math.log10(1 + TROOP_ICON_SATURATION_COUNT));
  return TROOP_ICON_MIN_RADIUS + (TROOP_ICON_MAX_RADIUS - TROOP_ICON_MIN_RADIUS) * t;
}

// docs/15-asker-hareketi-performans.md Bölüm 5.2 + kullanıcı talimatı (geniş bir
// formasyon — yan yana 3-4 asker olabilir — ama aralarındaki [rank'lar arası] boşluk
// hâlâ eşit ve giderek artan olmalı): ikonlar tek bir ince çizgi yerine sabit,
// deterministik bir GRID'e (sıra × şerit) yerleştirilir — rastgele jitter/scatter yok.
// - lane (şerit): kaynak→hedef hattına dik yönde, aynı sırada (rank) yan yana duran
//   ikonların konumu — `PARTICLE_LANE_COUNT` kadar şerit, aralarında sabit
//   `PARTICLE_LANE_SPACING` boşluk (bu, formasyonun GENİŞLİĞİni verir).
// - rank (sıra): kaç ikon ilerideyse aynı rank'tadır, birlikte hareket eder; ardışık
//   rank'lar arasındaki mesafe `PARTICLE_TRAIL_GAP` ile orantılıdır — kaynaktan
//   çıkarken (t=0) hepsi bitişik başlar, hedefe yaklaştıkça (t=1) rank'lar arasındaki
//   mesafe EŞİT adımlarla ve giderek büyüyerek açılır (deterministik, rastgele değil).
// - kademeli çıkış (stagger): rank'lar kaynaktan hepsi aynı anda değil, birbiri
//   ardına belirir/yola çıkar — aynı rank'taki şerit arkadaşları birlikte çıkar.
const PARTICLE_LANE_COUNT = 3;
const PARTICLE_LANE_SPACING = 4.5;
const PARTICLE_TRAIL_GAP = 0.09;
const PARTICLE_DEPART_STAGGER_MS = 110;

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function clamp01(value: number): number {
  return clamp(value, 0, 1);
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

/** Hafif "overshoot" ease — pop-in'e küçük bir bounce hissi verir (Bölüm 5.3). */
function backOut(t: number): number {
  const c = 1.7;
  const shifted = t - 1;
  return 1 + (c + 1) * shifted ** 3 + c * shifted ** 2;
}

/** Grup genelinde geçerli, tüm hâlâ-canlı ikonları aynı anda etkileyen aşamalar. */
type GroupPhase = { kind: "traveling" } | { kind: "arriving"; startedAt: number };

/**
 * Kullanıcı talimatı (bugünkü mesaj — `CLAUDE.md` Öncelik Sırası madde 1): "karşı taraf
 * askerleri birbirine çarpınca çarpan askerler yok olsun." Önceki uygulamada yalnızca
 * KAYBEDEN taraf (tüm grubu) çarpışma noktasında donup küçülüp soluyordu; KAZANAN
 * tarafın kaybettiği askerler ise `setSoldierCount` ile GÖRÜNMEZ bir sayaç geçişi
 * üzerinden, hareketlerine devam ederlerken (çarpışma noktasından bağımsız bir yerde)
 * aniden opaklığı 0'a düşerek kayboluyordu — bu, "çarpışıp yok olma" hissi vermiyordu.
 * Bu ikon-bazlı ölüm kaydı, hangi ikonun (index) ne zaman ve NEREDE (çarpışma noktasında,
 * donmuş halde) küçülüp soluyacağını tutar — kazanan tarafın kaybettiği askerler artık
 * kaybeden tarafla AYNI görsel muameleyi (dondur + küçült + söndür) çarpışma noktasında
 * görür, yalnızca hayatta kalanlar yollarına devam eder.
 */
interface IconDeath {
  startedAt: number;
  frozenPoint: Point;
}

/**
 * docs/15-asker-hareketi-performans.md Bölüm 5/6.1/6.2 + docs/19-army.md: bir
 * sevkiyat grubunun görsel temsili. Her ikonun pozisyon/ölçek/opaklığı, kendi
 * bağımsız progress'iyle (lateral spread + eşit/orantılı trail gap + kademeli çıkış), aynı
 * requestAnimationFrame döngüsü içinde doğrudan DOM'a (`transform` attribute +
 * `style.opacity`) yazılır — React state/re-render ASLA bu döngüden tetiklenmez
 * (Bölüm 6.2). Varış gibi paylaşımlı bir faz (GroupPhase) tüm ikonları aynı anda
 * etkiler (hep birlikte kaybolur); çarpışmada ise yalnızca GERÇEKTEN ölen ikonlar
 * (bkz. IconDeath) çarpışma noktasında donup küçülüp söner, hayatta kalanlar normal
 * seyahatine (ve kısa bir "toslama" pulse'una) devam eder.
 */
function TroopMarkerImpl({
  id,
  fromPoint,
  toPoint,
  departedAtUtc,
  arrivesAtUtc,
  initialSoldierCount,
  color,
  onRegisterHandle,
  onUnregisterHandle,
  onRemove,
}: TroopMarkerProps) {
  const iconRefs = useRef<(SVGCircleElement | null)[]>([]);

  // docs/15 Bölüm 6.2: fromPoint/toPoint bölge merkezleri (map.regions'tan) — aynı
  // maç boyunca değişmez, ama her render'da tazelenir ki tick() her zaman güncel
  // referansı görsün (region koordinatları zaten stabil obje referanslarıdır).
  const configRef = useRef({ fromPoint, toPoint, departedAtUtc, arrivesAtUtc });
  configRef.current = { fromPoint, toPoint, departedAtUtc, arrivesAtUtc };

  const phaseRef = useRef<GroupPhase>({ kind: "traveling" });
  const mountedAtRef = useRef(Date.now());
  const removedRef = useRef(false);

  // Bu andaki toplam hayatta kalan (görsel) ikon sayısı — başlangıçta gönderilen
  // miktar, bir çarpışmada kazanılırsa hayatta kalan sayıya düşer (bkz. playClash).
  const aliveCountRef = useRef(clamp(initialSoldierCount, 0, MAX_VISIBLE_TROOPS));
  // index -> ölüm bilgisi: yalnızca gerçekten "çarpışıp ölen" ikonlar burada olur.
  const iconDeathsRef = useRef<Map<number, IconDeath>>(new Map());
  // Kazanan tarafın hayatta kalan ikonlarına uygulanan kısa "toslama" pulse'u.
  const winnerBumpStartRef = useRef<number | null>(null);
  // Kaybeden taraf TÜMÜYLE öldüğünde (bkz. playClash "loser"), bu zamanda marker tamamen kaldırılır.
  const pendingRemovalAtRef = useRef<number | null>(null);

  useEffect(() => {
    const handle: TroopMarkerHandle = {
      playClash(role, clashAtUtc, survivorCount) {
        const cfg = configRef.current;
        const duration = Math.max(1, cfg.arrivesAtUtc - cfg.departedAtUtc);
        const frozenProgress = clamp01((clashAtUtc - cfg.departedAtUtc) / duration);
        const frozenPoint: Point = {
          x: lerp(cfg.fromPoint.x, cfg.toPoint.x, frozenProgress),
          y: lerp(cfg.fromPoint.y, cfg.toPoint.y, frozenProgress),
        };
        const now = Date.now();

        if (role === "winner") {
          // Yalnızca çarpışmada GERÇEKTEN kaybedilen ikonlar (eski hayatta kalan sayısı
          // ile yeni hayatta kalan sayısı arasındaki fark) ölüm animasyonuna girer —
          // önceden zaten görünmeyen (MAX_VISIBLE_TROOPS üstü) ikonlara dokunulmaz.
          const previousAlive = aliveCountRef.current;
          const nextAlive = clamp(survivorCount ?? previousAlive, 0, MAX_VISIBLE_TROOPS);
          for (let i = nextAlive; i < previousAlive; i++) {
            iconDeathsRef.current.set(i, { startedAt: now, frozenPoint });
          }
          aliveCountRef.current = nextAlive;
          winnerBumpStartRef.current = Math.min(now, clashAtUtc);
          return;
        }

        // docs/15 Bölüm 5.3 "kaybeden grup çarpışma anında küçülüp solarak kaybolur" —
        // müşteri kararı: kaybeden taraf TÜMÜYLE (tüm ikonlarıyla), çarpışma noktasında
        // donarak, kazanan tarafın kaybettiği askerlerle AYNI ölüm animasyonuyla kaybolur.
        for (let i = 0; i < aliveCountRef.current; i++) {
          iconDeathsRef.current.set(i, { startedAt: now, frozenPoint });
        }
        aliveCountRef.current = 0;
        pendingRemovalAtRef.current = now + DEATH_MS;
      },
      playArrive() {
        phaseRef.current = { kind: "arriving", startedAt: Date.now() };
      },
    };

    onRegisterHandle(id, handle);
    return () => onUnregisterHandle(id);
  }, [id, onRegisterHandle, onUnregisterHandle]);

  useEffect(() => {
    let rafId = 0;

    const tick = () => {
      const now = Date.now();
      const cfg = configRef.current;
      const duration = Math.max(1, cfg.arrivesAtUtc - cfg.departedAtUtc);

      const dx = cfg.toPoint.x - cfg.fromPoint.x;
      const dy = cfg.toPoint.y - cfg.fromPoint.y;
      const dist = Math.hypot(dx, dy) || 1;
      const perpX = -dy / dist;
      const perpY = dx / dist;

      if (pendingRemovalAtRef.current !== null && now >= pendingRemovalAtRef.current) {
        if (!removedRef.current) {
          removedRef.current = true;
          onRemove(id);
        }
        return;
      }

      const phase = phaseRef.current;
      let groupScaleMul = 1;
      let groupOpacityMul = 1;
      let groupComplete = false;

      if (phase.kind === "arriving") {
        const t = clamp01((now - phase.startedAt) / DEATH_MS);
        groupScaleMul = 1 - 0.4 * t;
        groupOpacityMul = 1 - t;
        groupComplete = t >= 1;
      }

      if (groupComplete) {
        if (!removedRef.current) {
          removedRef.current = true;
          onRemove(id);
        }
        return;
      }

      // Kazanan tarafın hayatta kalan ikonlarına kısa bir "toslama" pulse'u — çarpışmanın
      // hissedildiği ama hayatta kalanların yoluna devam ettiği an (Bölüm 5.3).
      let winnerBumpMul = 1;
      if (winnerBumpStartRef.current !== null) {
        const t = clamp01((now - winnerBumpStartRef.current) / CLASH_IMPACT_MS);
        winnerBumpMul = 1 + Math.sin(t * Math.PI) * 0.28;
        if (t >= 1) {
          winnerBumpStartRef.current = null;
        }
      }

      for (let i = 0; i < MAX_VISIBLE_TROOPS; i++) {
        const icon = iconRefs.current[i];
        if (!icon) continue;

        const lane = i % PARTICLE_LANE_COUNT;
        const rank = Math.floor(i / PARTICLE_LANE_COUNT);
        const lateral = (lane - (PARTICLE_LANE_COUNT - 1) / 2) * PARTICLE_LANE_SPACING;

        const death = iconDeathsRef.current.get(i);
        if (death) {
          const t = clamp01((now - death.startedAt) / DEATH_MS);
          if (t >= 1) {
            icon.style.opacity = "0";
            continue;
          }
          const x = death.frozenPoint.x + perpX * lateral;
          const y = death.frozenPoint.y + perpY * lateral;
          icon.setAttribute("transform", `translate(${x} ${y}) scale(${1 - t})`);
          icon.style.opacity = String(clamp01(1 - t));
          continue;
        }

        if (i >= aliveCountRef.current) {
          icon.style.opacity = "0";
          continue;
        }

        // Kullanıcı talimatı: eşit ve giderek büyüyen boşluk — her rank, önündeki
        // rank'ın kat ettiği mesafeyle orantılı, sabit bir payla geride kalır. t=0'da
        // (kaynaktan çıkışta) hepsi bitişik, t=1'e (hedefe varış) yaklaştıkça ardışık
        // rank'lar arasındaki mesafe eşit adımlarla ve orantılı olarak açılır.
        const departStagger = rank * PARTICLE_DEPART_STAGGER_MS;
        const iconAge = now - mountedAtRef.current - departStagger;
        if (iconAge < 0) {
          icon.style.opacity = "0";
          continue;
        }

        const leadProgress = clamp01((now - cfg.departedAtUtc) / duration);
        const travelProgress = clamp01(leadProgress * (1 - rank * PARTICLE_TRAIL_GAP));

        const x = lerp(cfg.fromPoint.x, cfg.toPoint.x, travelProgress) + perpX * lateral;
        const y = lerp(cfg.fromPoint.y, cfg.toPoint.y, travelProgress) + perpY * lateral;

        const popInT = clamp01(iconAge / POP_IN_MS);
        const iconScale = backOut(popInT) * groupScaleMul * winnerBumpMul;
        const iconOpacity = clamp01(iconAge / (POP_IN_MS * 0.6)) * groupOpacityMul;

        icon.setAttribute("transform", `translate(${x} ${y}) scale(${iconScale})`);
        icon.style.opacity = String(clamp01(iconOpacity));
      }

      rafId = requestAnimationFrame(tick);
    };

    rafId = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafId);
    // Bilinçli olarak boş bağımlılık: tüm girdiler configRef/phaseRef/iconDeathsRef vb.
    // üzerinden okunuyor, döngü mount'ta bir kez kurulup unmount'ta temizleniyor.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, onRemove]);

  const initialVisibleCount = clamp(initialSoldierCount, 0, MAX_VISIBLE_TROOPS);
  // Sevkiyatın büyüklüğü ikon adedinde tavan yaptığı için boyuta da yansıtılır — bkz. `troopIconRadius`.
  const iconRadius = troopIconRadius(initialSoldierCount);

  return (
    <g pointerEvents="none">
      {/* Müşteri kararı: sabit "1-3 küme + sayı rozeti" yerine, gönderilen asker sayısı
          kadar (üst sınır MAX_VISIBLE_TROOPS) gerçek ikon — ayrı bir sayı metni yok.
          Her ikonun pozisyonu tamamen imperatif olarak (tick() içinde) hesaplanır, bu
          yüzden cx/cy burada sabit 0'dır — statik bir küme şekli DEĞİL, bağımsız hareket. */}
      {Array.from({ length: MAX_VISIBLE_TROOPS }, (_, i) => (
        <circle
          key={i}
          ref={(el) => {
            iconRefs.current[i] = el;
          }}
          cx={0}
          cy={0}
          r={iconRadius}
          fill={color}
          // docs/23-game-ui-refresh-v2.md Aşama 3: ince açık halka. İkon dolgusu
          // owner'ın KOYU aksan tonu olduğu için (bkz. `color` prop'unun notu) bir
          // sevkiyat koyu bir yüzeyin üstünden geçtiğinde — en belirgin olarak Fog of
          // War'daki keşfedilmemiş bölgelerde (`UNEXPLORED_COLOR`, koyu) — zeminle
          // birleşip görünmez oluyordu. Halka, ikonu geçtiği yüzeyden bağımsız olarak
          // ayırır; kalınlık harita ölçeğinden bağımsız gerçek ekran pikselidir.
          stroke="rgba(240,246,253,0.85)"
          strokeWidth={1}
          vectorEffect="non-scaling-stroke"
          opacity={i < initialVisibleCount ? 1 : 0}
        />
      ))}
    </g>
  );
}

export const TroopMarker = memo(TroopMarkerImpl);
