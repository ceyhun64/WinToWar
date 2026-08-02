// Sade tasarım paleti (bkz. Bölüm 5): nötr + oyuncu 1 + oyuncu 2 + 1 vurgu rengi.
// Gradient/neon/glow yok, düz (flat) renkler.

export const PLAYER_COLORS = ["#2563eb", "#dc2626"] as const; // slot 0: mavi, slot 1: kırmızı
export const NEUTRAL_COLOR = "#9ca3af"; // gri

export function colorForSlot(slot: number): string {
  return PLAYER_COLORS[slot % PLAYER_COLORS.length];
}
