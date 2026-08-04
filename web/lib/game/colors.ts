// docs/04-style.md Bölüm 2: 12 oyuncu kimlik rengi — soluk/desatüre pastel tonlar,
// kırmızı/gül ailesi hiçbirinde kullanılmaz (Danger ile çakışmasın diye). Nötr
// (sahipsiz) bölge ayrı, açık sıcak nötr bir tonda.

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

export const NEUTRAL_COLOR = "#D8D2C4"; // açık, sıcak nötr — sahipsiz/inert bölge

// docs/04-style.md Bölüm 10 "Fog of War": görüş alanı dışındaki bölgeler yalnızca
// arazi şeklini gösteren soluk/gri bir "keşfedilmemiş alan" dolgusuyla render edilir
// — nötr bölge tonundan da belirgin şekilde ayrışır ki "sahipsiz ama görünür" ile
// "görünmez" karışmasın.
export const UNEXPLORED_COLOR = "#C7C7C7";

export function colorForSlot(slot: number): string {
  return PLAYER_COLORS[slot % PLAYER_COLORS.length];
}
