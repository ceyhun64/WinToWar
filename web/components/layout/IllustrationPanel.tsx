import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { GameCard } from "@/components/layout/GameCard";
import { cn } from "@/lib/utils";

/**
 * Köşede çok soluk (%6), hafif bulanık bir ikon "watermark" taşıyan kart —
 * `components/lobby/GameModeTiles.tsx`'te kurulan desenin genel/paylaşılan
 * hali (Lobi'nin kendi dosyası değiştirilmedi, yalnızca aynı görsel dil
 * burada tekrar üretildi). Fotoğraf/render değil, mevcut `lucide-react`
 * ikonları büyütülüp soluklaştırılıyor.
 */
export function IllustrationPanel({
  icon: Icon,
  accent = "#38BDF8",
  className,
  children,
}: {
  icon: LucideIcon;
  accent?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <GameCard className={cn("relative overflow-hidden p-5", className)}>
      <Icon
        aria-hidden="true"
        className="pointer-events-none absolute -bottom-3 -right-3 size-20 opacity-[0.06] blur-[1.5px]"
        style={{ color: accent }}
      />
      <div className="relative z-10">{children}</div>
    </GameCard>
  );
}
