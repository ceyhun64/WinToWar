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
const TARGET_PULLBACK = 8;
// docs/23-game-ui-refresh-v2.md Aşama 3: uç, "yön okunur bir ok" olacak kadar
// büyütüldü. Önceki 7×5.5'lik üçgen, 590 birimlik viewBox 360px'e indiğinde
// ~4×3 piksele düşüyordu — yani okun hangi yöne baktığı mobilde okunmuyordu.
const ARROWHEAD_LENGTH = 15;
const ARROWHEAD_WIDTH = 13;
// Bu eşiğin altında hiç ok çizilmez — parmak henüz kıpırdamışken bir "çöp" ok
// belirmesin diye. Sürüklemenin başladığı, kaynaktaki nabız halkasından zaten belli.
const MIN_DISTANCE = 12;

/** Kaynaktan hedefe, net bir arrowhead'i olan bir ok için çizgi + üçgen köşe noktalarını hesaplar. */
export function computeAttackArrow(from: Point, to: Point): AttackArrowGeometry | null {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const distance = Math.hypot(dx, dy);
  if (distance < MIN_DISTANCE) return null;

  const ux = dx / distance;
  const uy = dy / distance;

  // docs/23-game-ui-refresh-v2.md Aşama 3 — paylar artık mesafeyle ÖLÇEKLENİR.
  //
  // Aşama 2'de rozet büyüdüğü için kaynak payı 15 → 26, uç 7 → 15 oldu. Sabit
  // paylar toplamı (26 + 15 + 8 = 49 birim) kısa bir sürüklemede mesafeden büyük
  // kalabiliyor; o durumda `lineEnd` `lineStart`'ın GERİSİNE düşüyor ve ok ters
  // yöne bakan bozuk bir şekle dönüşüyordu. Paylar mevcut mesafeden pay alacak
  // şekilde kısıtlanınca ok her mesafede geçerli kalır, yalnızca küçülür.
  const targetPullback = Math.min(TARGET_PULLBACK, distance * 0.12);
  const available = distance - targetPullback;
  const sourcePullback = Math.min(SOURCE_PULLBACK, available * 0.5);
  const headLength = Math.max(0, Math.min(ARROWHEAD_LENGTH, (available - sourcePullback) * 0.9));
  const headWidth = ARROWHEAD_WIDTH * (headLength / ARROWHEAD_LENGTH);

  const lineStart: Point = { x: from.x + ux * sourcePullback, y: from.y + uy * sourcePullback };
  const tip: Point = { x: to.x - ux * targetPullback, y: to.y - uy * targetPullback };
  const lineEnd: Point = { x: tip.x - ux * headLength, y: tip.y - uy * headLength };

  const perpX = -uy;
  const perpY = ux;
  const base1: Point = { x: lineEnd.x + perpX * (headWidth / 2), y: lineEnd.y + perpY * (headWidth / 2) };
  const base2: Point = { x: lineEnd.x - perpX * (headWidth / 2), y: lineEnd.y - perpY * (headWidth / 2) };

  return { lineStart, lineEnd, arrowheadPoints: [tip, base1, base2] };
}

export function arrowheadPointsAttr(points: [Point, Point, Point]): string {
  return points.map((p) => `${p.x},${p.y}`).join(" ");
}
