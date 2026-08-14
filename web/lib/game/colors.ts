import type { RoomType } from "./types";

// docs/04-style.md Bölüm 2: 12 oyuncu kimlik rengi (VIP paleti) — soluk/desatüre pastel
// tonlar, kırmızı/gül ailesi burada kullanılmaz (Danger ile çakışmasın diye — VIP'de
// oyuncu sayısı çok olduğundan bu genel ilke hâlâ geçerli). Nötr (sahipsiz) bölge ayrı,
// açık sıcak nötr bir tonda.
export const PLAYER_COLORS = [
  "#7C93B3", // mavi
  "#9C8CBF", // mor
  "#82A98D", // yeşil
  "#C2A76B", // altın/amber-dışı
  "#6FAFAE", // turkuaz
  "#C793A8", // pembe
  "#A79EDB", // lavanta
  "#7FBEC6", // camgöbeği
  "#BDAA5C", // hardal
  "#B08A6B", // açık kahve/toprak
  "#5E7FA3", // gök mavisi-koyu
  "#96A15B", // zeytin yeşili
] as const;

// docs/14-game-map-redesign.md Bölüm 5: asker sayısı rozeti artık beyaz/siyah değil,
// owner rengiyle uyumlu koyu bir tonda — bölgenin kendi pastel dolgusunun üstünde
// okunabilir kalması için aynı paletin koyulaştırılmış hali.
export const PLAYER_COLORS_DARK = [
  "#445162",
  "#564D69",
  "#485D4E",
  "#6B5C3B",
  "#3D6060",
  "#6D515C",
  "#5C5778",
  "#46696D",
  "#685E33",
  "#614C3B",
  "#34465A",
  "#535932",
] as const;

// 🔒 Müşteri kararı: Standart (4 kişilik) oda için kırmızı-mavi-mor-yeşil, Practice
// (2 kişilik) için kırmızı-mavi. Bu iki mod, genel 12'lik VIP paletinden BİLEREK ayrı —
// müşteri özellikle bu iki modda kırmızı istedi. Kırmızı yine de Danger token'ından
// (`#f2495c`, bkz. globals.css `--destructive`) ayrı, "tatlı"/soluk bir ton (04-style.md
// Bölüm 2'nin orijinal "Danger ile karışmasın" gerekçesi burada da korunuyor).
export const STANDARD_ROOM_COLORS = ["#7C93B3", "#D9897E", "#9C8CBF", "#82A98D"] as const; // mavi, kırmızı, mor, yeşil
export const STANDARD_ROOM_COLORS_DARK = ["#445162", "#8C4F46", "#564D69", "#485D4E"] as const;
export const PRACTICE_ROOM_COLORS = ["#7C93B3", "#D9897E"] as const; // mavi, kırmızı
export const PRACTICE_ROOM_COLORS_DARK = ["#445162", "#8C4F46"] as const;

export const NEUTRAL_COLOR = "#D8D2C4"; // açık, sıcak nötr — sahipsiz/inert bölge
export const NEUTRAL_DARK_COLOR = "#6B6559"; // NEUTRAL_COLOR'ın koyu hali — sahipsiz bölge rozeti için

// docs/04-style.md Bölüm 10 "Fog of War": görüş alanı dışındaki bölgeler yalnızca
// arazi şeklini gösteren soluk/gri bir "keşfedilmemiş alan" dolgusuyla render edilir
// — nötr bölge tonundan da belirgin şekilde ayrışır ki "sahipsiz ama görünür" ile
// "görünmez" karışmasın.
export const UNEXPLORED_COLOR = "#C7C7C7";

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
 * docs/03-game-rules.md güncel müşteri kararı: "kaç adet hesap girdiyse o kadar renk
 * olsun" — her oyuncu kendi `slot`'una karşılık gelen ayrı bir renk alır, oda tipine göre
 * ayrı bir palet kullanılır (Standart: mavi/kırmızı/mor/yeşil, Practice: mavi/kırmızı,
 * VIP: genel 12'lik palet). N oyunculu bir maçta ekranda N farklı renk görünür.
 */
export function playerFillColor({ roomType, ownerId, ownerSlot }: PlayerRegionColorInput): string {
  if (ownerId === null || ownerSlot === null) return NEUTRAL_COLOR;
  const { light } = paletteForRoomType(roomType);
  return light[ownerSlot % light.length];
}

/**
 * Rozet/etiket için koyu varyant. docs/03-game-rules.md güncel müşteri kararı: "kale
 * gibi bir alan olmayacak" — hiçbir bölge (ne başlangıç bölgesi ne fethedilen) diğerlerinden
 * daha büyük/koyu bir rozetle ayrıştırılmaz, tüm sahipli bölgeler aynı görsel muameleyi görür.
 */
export function playerAccentColor({ roomType, ownerId, ownerSlot }: PlayerRegionColorInput): string {
  if (ownerId === null || ownerSlot === null) return NEUTRAL_DARK_COLOR;
  const { dark } = paletteForRoomType(roomType);
  return dark[ownerSlot % dark.length];
}

/** Bir hex rengi siyaha doğru `amount` (0-1) oranında koyulaştırır. */
function darkenHex(hex: string, amount: number): string {
  const num = parseInt(hex.slice(1), 16);
  const r = Math.round(((num >> 16) & 0xff) * (1 - amount));
  const g = Math.round(((num >> 8) & 0xff) * (1 - amount));
  const b = Math.round((num & 0xff) * (1 - amount));
  return `#${[r, g, b].map((v) => v.toString(16).padStart(2, "0")).join("")}`;
}

/**
 * Bir hex rengi beyaza doğru `amount` (0-1) oranında açar. docs/20-state-io-army-gorsel-fark-giderme.md
 * §2.B.2: hedefe varış anındaki rozet renk parlaması bu fonksiyonu kullanır (RegionNode.tsx).
 */
export function lightenHex(hex: string, amount: number): string {
  const num = parseInt(hex.slice(1), 16);
  const r = (num >> 16) & 0xff;
  const g = (num >> 8) & 0xff;
  const b = num & 0xff;
  const lr = Math.round(r + (255 - r) * amount);
  const lg = Math.round(g + (255 - g) * amount);
  const lb = Math.round(b + (255 - b) * amount);
  return `#${[lr, lg, lb].map((v) => v.toString(16).padStart(2, "0")).join("")}`;
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}

// 🔒 Müşteri geri bildirimi: önceki oran ("40 askerde tam koyuluk, %35'e kadar") çok
// koyu görünüyordu, "tatlı renkler" isteniyor — hem tavan hem doygunluk noktası
// düşürüldü ki pastel his hiçbir asker sayısında kaybolmasın, yalnızca hafifçe belirginleşsin.
const OWNED_DARKEN_SATURATION_COUNT = 60;
const OWNED_MAX_DARKEN_AMOUNT = 0.15;

// 🔒 Müşteri örneği: fethedilmeyen (nötr) toprakta savunma 10 iken en koyu, 0 iken en
// açık. Oran, odanın gerçek `GreyRegionDefenseCount`'una göre normalize edilir (VIP'de
// bu 1-7 arası farklı bir tavan olabilir, Standart/Practice'te varsayılan 10'dur).
const NEUTRAL_MAX_LIGHTEN_AMOUNT = 0.6;

/**
 * docs/03-game-rules.md güncel müşteri kararı: bir bölgenin dolgu rengi artık yalnızca
 * sahiplik kimliğini değil, o bölgedeki GÜNCEL asker sayısını da yansıtır — sahipli bir
 * bölgede asker arttıkça renk hafifçe koyulaşır (tatlı/pastel his korunur, bkz. yukarıdaki
 * gerekçe); fethedilmeyen bir toprakta savunma azaldıkça (üstüne asker gelip savunma
 * düştükçe) renk açılır. Yalnızca `GameMap.tsx`'teki bölge dolgusu (RegionShape) için
 * kullanılır — HUD rozeti, üst kontrol barı ve sevkiyat işaretleri sabit kimlik rengini
 * (`playerFillColor`) kullanmaya devam eder, çünkü onlar tek bir bölgenin anlık asker
 * sayısına bağlı değildir.
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
