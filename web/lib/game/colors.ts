import type { RoomType } from "./types";

/**
 * docs/23-game-ui-refresh-v2.md Aşama 1 — takım rengi sistemi.
 *
 * Bu dosya oyun ekranının TEK renk kaynağıdır. Renkler `globals.css`'e taşınmaz
 * çünkü üzerlerinde çalışma zamanında matematik yapılıyor (asker sayısına göre
 * koyulaşma/açılma, rozet tonunun türetilmesi) — bir CSS değişkeni bunu yapamaz.
 * `--game-*` tokenları yalnızca CSS'in kendi kullandığı şeyleri (yüzey, halka,
 * ölçek, hareket) tanımlar.
 *
 * ── Paletin neden değiştiği (ölçüm, tahmin değil) ────────────────────────────
 * Önceki palet soluk/desatüre pastellerden oluşuyordu ve ölçülebilir bir sorunu
 * vardı: renkler birbirine o kadar yakındı ki, sahiplik yalnızca renkle
 * gösterildiği için "kimin bölgesi" sorusu belirsiz kalıyordu.
 *
 * Yöntem: her renk çifti için CIE-Lab ΔE, hem normal görüşte hem üç renk körlüğü
 * tipinde (döteranopi / protanopi / tritanopi) simüle edilerek hesaplandı; ayrıca
 * aşağıdaki güç-bazlı koyulaşma/açılma aralığının HER kombinasyonu dahil edildi
 * (yani ekranda gerçekten oluşabilen en kötü durum). Her modun en kötü çifti:
 *
 *              önce → sonra
 *   Practice    31.2 → 65.2
 *   Standart     1.8 →  6.9
 *   VIP (12)     0.6 →  5.3
 *
 * VIP'te 12 rengin renk körlüğünde tam ayrışması matematiksel olarak mümkün
 * değildir. Bu yüzden görev tanımı §5 gereği sahiplik ASLA yalnızca renkle
 * gösterilmez — `--game-ring-own` / `--game-ring-target` (globals.css) akromatik
 * bir ikinci kanal olarak bu paletin yanında çalışır (bkz. Aşama 2, RegionNode).
 */

/**
 * Renk körlüğü + kontrast ölçümüyle seçilmiş 12'lik VIP paleti. İlk 4 slot, en
 * çok kullanılan modlar (Practice/Standart) olduğu için bilinçli olarak en fazla
 * ayrışan dörtlüdür.
 */
export const PLAYER_COLORS = [
  "#4F9BE0", // mavi
  "#E8705C", // mercan
  "#C77BEE", // menekşe
  "#5FD198", // yeşil
  "#E4E07E", // limon
  "#C9822C", // kehribar
  "#9EE891", // yaprak
  "#8079E8", // indigo
  "#E891DB", // orkide
  "#CEC046", // hardal
  "#91E8CA", // nane
  "#E56CA8", // gül
] as const;

/**
 * 🔒 Müşteri kararı korunuyor: Standart (4 kişilik) oda için mavi-kırmızı-mor-yeşil,
 * Practice (2 kişilik) için mavi-kırmızı. Slot sırası ve renk kimlikleri aynı kaldı,
 * yalnızca tonlar yukarıdaki ölçüme göre canlandırıldı. Kırmızı hâlâ Danger
 * token'ından (`--destructive: #f2495c`) ayrı, daha sıcak/mercan bir tondur —
 * `04-style.md` Bölüm 2'nin "Danger ile karışmasın" gerekçesi geçerliliğini korur.
 */
export const STANDARD_ROOM_COLORS = [
  PLAYER_COLORS[0], // mavi
  PLAYER_COLORS[1], // kırmızı/mercan
  PLAYER_COLORS[2], // mor
  PLAYER_COLORS[3], // yeşil
] as const;
export const PRACTICE_ROOM_COLORS = [PLAYER_COLORS[0], PLAYER_COLORS[1]] as const;

/**
 * Sahipsiz/nötr bölge — düşük doygunlukta ve bilinçli olarak hiçbir takım
 * renginin ailesinde değil (en yakın oyuncu rengine ΔE 6.8, güç aralığı dahil).
 */
export const NEUTRAL_COLOR = "#B9C2CE";

/**
 * docs/04-style.md Bölüm 10 "Fog of War": görüş alanı dışındaki bölge yalnızca
 * arazi şeklini gösterir. Önceki değer (`#C7C7C7`, açık gri) koyu haritada nötr
 * bölgeden daha PARLAK kalıyordu — yani "bilinmiyor" durumu "sahipsiz" durumundan
 * daha çok dikkat çekiyordu. Artık zeminden yalnızca ~1.9:1 ayrışan koyu bir ton:
 * arazi şekli görünür kalır ama katman geri çekilir.
 */
export const UNEXPLORED_COLOR = "#3A4A68";

/**
 * Rozet tonunun hedef bağıl parlaklığı. Bu değer, rozetin üstündeki beyaz asker
 * sayısına her slotta en az 7.7:1 kontrast verir (WCAG AAA küçük metin eşiği
 * 7:1) — asker sayısı ekrandaki en önemli bilgi olduğu için AA değil AAA hedeflendi.
 */
const BADGE_TARGET_LUMINANCE = 0.085;

function channelToLinear(value: number): number {
  const v = value / 255;
  return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
}

function relativeLuminance(hex: string): number {
  const r = channelToLinear(parseInt(hex.slice(1, 3), 16));
  const g = channelToLinear(parseInt(hex.slice(3, 5), 16));
  const b = channelToLinear(parseInt(hex.slice(5, 7), 16));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function scaleHex(hex: string, factor: number): string {
  const scaled = [1, 3, 5].map((i) =>
    Math.round(parseInt(hex.slice(i, i + 2), 16) * factor)
      .toString(16)
      .padStart(2, "0")
  );
  return `#${scaled.join("")}`;
}

/**
 * Bir takım renginden rozet/etiket için koyu varyantını TÜRETİR — el ile yazılmış
 * ikinci bir dizi tutulmaz. Böylece paletteki bir renk değiştiğinde rozet tonu
 * otomatik takip eder ve beyaz asker sayısının kontrast garantisi (yukarıdaki
 * `BADGE_TARGET_LUMINANCE`) hiçbir slotta yanlışlıkla bozulamaz.
 */
function toBadgeTone(hex: string): string {
  let low = 0;
  let high = 1;
  for (let i = 0; i < 24; i++) {
    const mid = (low + high) / 2;
    if (relativeLuminance(scaleHex(hex, mid)) > BADGE_TARGET_LUMINANCE) {
      high = mid;
    } else {
      low = mid;
    }
  }
  return scaleHex(hex, (low + high) / 2);
}

export const PLAYER_COLORS_DARK = PLAYER_COLORS.map(toBadgeTone);
export const STANDARD_ROOM_COLORS_DARK = STANDARD_ROOM_COLORS.map(toBadgeTone);
export const PRACTICE_ROOM_COLORS_DARK = PRACTICE_ROOM_COLORS.map(toBadgeTone);
export const NEUTRAL_DARK_COLOR = toBadgeTone(NEUTRAL_COLOR);

export function colorForSlot(slot: number): string {
  return PLAYER_COLORS[slot % PLAYER_COLORS.length];
}

export function darkColorForSlot(slot: number): string {
  return PLAYER_COLORS_DARK[slot % PLAYER_COLORS_DARK.length];
}

function paletteForRoomType(roomType: RoomType): { light: readonly string[]; dark: readonly string[] } {
  if (roomType === "Standard") return { light: STANDARD_ROOM_COLORS, dark: STANDARD_ROOM_COLORS_DARK };
  if (roomType === "Practice") return { light: PRACTICE_ROOM_COLORS, dark: PRACTICE_ROOM_COLORS_DARK };
  return { light: PLAYER_COLORS, dark: PLAYER_COLORS_DARK };
}

export interface PlayerRegionColorInput {
  roomType: RoomType;
  ownerId: string | null;
  ownerSlot: number | null;
}

/**
 * docs/03-game-rules.md güncel müşteri kararı: "kaç adet hesap girdiyse o kadar
 * renk olsun" — her oyuncu kendi `slot`'una karşılık gelen ayrı bir renk alır,
 * palet oda tipine göre değişir (Standart: mavi/kırmızı/mor/yeşil, Practice:
 * mavi/kırmızı, VIP: genel 12'lik palet). N oyunculu bir maçta ekranda N farklı
 * renk görünür.
 */
export function playerFillColor({ roomType, ownerId, ownerSlot }: PlayerRegionColorInput): string {
  if (ownerId === null || ownerSlot === null) return NEUTRAL_COLOR;
  const { light } = paletteForRoomType(roomType);
  return light[ownerSlot % light.length];
}

/**
 * Rozet/etiket/sevkiyat için koyu varyant. docs/03-game-rules.md güncel müşteri
 * kararı: "kale gibi bir alan olmayacak" — hiçbir bölge (ne başlangıç bölgesi ne
 * fethedilen) diğerlerinden daha büyük/koyu bir rozetle ayrıştırılmaz, tüm sahipli
 * bölgeler aynı görsel muameleyi görür.
 */
export function playerAccentColor({ roomType, ownerId, ownerSlot }: PlayerRegionColorInput): string {
  if (ownerId === null || ownerSlot === null) return NEUTRAL_DARK_COLOR;
  const { dark } = paletteForRoomType(roomType);
  return dark[ownerSlot % dark.length];
}

/** Bir hex rengi siyaha doğru `amount` (0-1) oranında koyulaştırır. */
function darkenHex(hex: string, amount: number): string {
  return scaleHex(hex, 1 - amount);
}

/**
 * Bir hex rengi beyaza doğru `amount` (0-1) oranında açar. Hedefe varış anındaki
 * rozet renk parlaması bu fonksiyonu kullanır (RegionNode.tsx).
 */
export function lightenHex(hex: string, amount: number): string {
  const lightened = [1, 3, 5].map((i) => {
    const v = parseInt(hex.slice(i, i + 2), 16);
    return Math.round(v + (255 - v) * amount)
      .toString(16)
      .padStart(2, "0");
  });
  return `#${lightened.join("")}`;
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}

// 🔒 Müşteri geri bildirimi: koyulaşma hiçbir asker sayısında rengin kimliğini
// yutmamalı, yalnızca hafifçe belirginleştirmeli.
//
// 🛠️ Tavan 0.15 → 0.12. Gerekçe ölçüme dayanıyor: koyulaşma aralığı bir oyuncunun
// rengini BAŞKA bir oyuncunun rengine yaklaştırabiliyor. Palet seçimi zaten bu
// aralığın tamamı hesaba katılarak yapıldı (yukarıdaki not), ama 0.12 en kötü
// çapraz çifti ölçülebilir şekilde iyileştirir ve müşterinin istediği "asker
// arttıkça hafifçe koyulaşsın" etkisini gözle görülür biçimde korur.
const OWNED_DARKEN_SATURATION_COUNT = 60;
const OWNED_MAX_DARKEN_AMOUNT = 0.12;

// 🔒 Müşteri örneği: fethedilmeyen (nötr) toprakta savunma tavandayken en koyu,
// 0 iken en açık. Oran odanın gerçek `GreyRegionDefenseCount`'una göre normalize
// edilir (VIP'de 1-7, Standart/Practice'te varsayılan 10).
//
// 🛠️ Tavan 0.6 → 0.55: yeni nötr ton zaten daha açık başladığı için 0.6, zayıf bir
// bölgeyi neredeyse saf beyaza taşıyor ve ele geçirme flash'ıyla karışıyordu.
const NEUTRAL_MAX_LIGHTEN_AMOUNT = 0.55;

/**
 * docs/03-game-rules.md güncel müşteri kararı: bir bölgenin dolgu rengi yalnızca
 * sahiplik kimliğini değil, o bölgedeki GÜNCEL asker sayısını da yansıtır —
 * sahipli bir bölgede asker arttıkça renk hafifçe koyulaşır; fethedilmeyen bir
 * toprakta savunma azaldıkça (üstüne asker gelip savunma düştükçe) renk açılır.
 * Yalnızca haritadaki bölge dolgusu (RegionShape) için kullanılır — HUD rozeti,
 * üst kontrol barı ve sevkiyat işaretleri sabit kimlik rengini (`playerFillColor`)
 * kullanmaya devam eder, çünkü onlar tek bir bölgenin anlık asker sayısına bağlı
 * değildir.
 */
export function regionFillColorByStrength(
  input: PlayerRegionColorInput,
  soldierCount: number,
  greyRegionDefenseCount: number
): string {
  if (input.ownerId === null || input.ownerSlot === null) {
    const max = Math.max(1, greyRegionDefenseCount);
    const strength = clamp01(soldierCount / max);
    return lightenHex(NEUTRAL_COLOR, (1 - strength) * NEUTRAL_MAX_LIGHTEN_AMOUNT);
  }
  const base = playerFillColor(input);
  const t = clamp01(soldierCount / OWNED_DARKEN_SATURATION_COUNT);
  return darkenHex(base, t * OWNED_MAX_DARKEN_AMOUNT);
}
