// docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 14-17: saldırı sırasında gösterilen
// çizgi artık düz bir hat değil, kaynaktan hedefe yönlü, net bir arrowhead'i olan bir
// ok. Bu geometri hem interaktif sürükleme önizlemesinde (GameMap) hem de aktif bir
// sevkiyatın üstüne bindirilen sabit okta (ArmyLayer) aynı şekilde kullanıldığından
// (06-coding-standards.md "Kod Tekrarını Önleme") tek bir yerde toplanmıştır.

interface Point {
  x: number;
  y: number;
}

export interface AttackArrowGeometry {
  lineStart: Point;
  lineEnd: Point;
  arrowheadPoints: [Point, Point, Point];
}

// Kaynak bölge rozetinin dışından başlar ki çizgi rozetin içine gömülü görünmesin.
// docs/23-game-ui-refresh-v2.md Aşama 2: rozet, asker sayısı okunabilirliği için
// büyütüldü (48×31, yarı-genişlik 24) — bu pay eski/küçük rozete (rx=14) göre
// ayarlanmıştı ve olduğu gibi bırakılsaydı ok artık rozetin İÇİNDEN çıkıyor
// görünecekti. Yeni değer = rozet yarı-genişliği + küçük bir nefes payı.
const SOURCE_PULLBACK = 26;
// Hedefin merkezine değil, "içine kadar uzanan" bir uca sahip olsun diye merkeze
// yakın ama tam üstünde değil bir noktada biter (docs Bölüm 16).
const TARGET_PULLBACK = 5;
const ARROWHEAD_LENGTH = 7;
const ARROWHEAD_WIDTH = 5.5;
const MIN_DISTANCE = 1;

/** Kaynaktan hedefe, net bir arrowhead'i olan bir ok için çizgi + üçgen köşe noktalarını hesaplar. */
export function computeAttackArrow(from: Point, to: Point): AttackArrowGeometry | null {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const distance = Math.hypot(dx, dy);
  if (distance < MIN_DISTANCE) return null;

  const ux = dx / distance;
  const uy = dy / distance;

  const lineStart: Point = { x: from.x + ux * SOURCE_PULLBACK, y: from.y + uy * SOURCE_PULLBACK };
  const tip: Point = { x: to.x - ux * TARGET_PULLBACK, y: to.y - uy * TARGET_PULLBACK };
  const lineEnd: Point = { x: tip.x - ux * ARROWHEAD_LENGTH, y: tip.y - uy * ARROWHEAD_LENGTH };

  const perpX = -uy;
  const perpY = ux;
  const base1: Point = { x: lineEnd.x + perpX * (ARROWHEAD_WIDTH / 2), y: lineEnd.y + perpY * (ARROWHEAD_WIDTH / 2) };
  const base2: Point = { x: lineEnd.x - perpX * (ARROWHEAD_WIDTH / 2), y: lineEnd.y - perpY * (ARROWHEAD_WIDTH / 2) };

  return { lineStart, lineEnd, arrowheadPoints: [tip, base1, base2] };
}

export function arrowheadPointsAttr(points: [Point, Point, Point]): string {
  return points.map((p) => `${p.x},${p.y}`).join(" ");
}
