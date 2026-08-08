import type { ComponentProps } from "react";
import { Card } from "@/components/ui/card";
import { cn } from "@/lib/utils";

/**
 * `docs/04-style.md` Landing İstisnası — sitede genelleştirilmiş tasarım
 * sistemi: "tüm kartlar aynı tasarım sistemini kullansın (border-radius,
 * border, shadow aynı)" talimatının tek kaynağı. Mevcut `Card` bileşenini
 * (paylaşılan, Landing/Lobi dahil her yerde kullanılan) değiştirmeden sarar —
 * yalnızca bu yeni sayfalarda tekrar eden opaklık/blur/radius class'larını
 * tek yerden yönetir.
 */
export function GameCard({ className, ...props }: ComponentProps<typeof Card>) {
  return <Card className={cn("rounded-2xl border border-border bg-card/60 backdrop-blur-md", className)} {...props} />;
}
