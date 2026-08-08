import type { LucideIcon } from "lucide-react";
import { GameCard } from "@/components/layout/GameCard";
import { cn } from "@/lib/utils";

/** Rozet/başarı kartı — yalnızca gerçek veriye dayalı olarak kullanılır, uydurma başarı listesi oluşturmak için değil. */
export function AchievementCard({
  icon: Icon,
  title,
  description,
  unlocked,
  accent = "#F5B942",
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  unlocked: boolean;
  accent?: string;
}) {
  return (
    <GameCard className={cn("flex-row items-center gap-3 p-4", !unlocked && "opacity-40 grayscale")}>
      <span
        className="flex size-10 shrink-0 items-center justify-center rounded-xl"
        style={{ backgroundColor: `${accent}22`, color: accent }}
      >
        <Icon className="size-5" aria-hidden="true" />
      </span>
      <div className="flex flex-col">
        <span className="text-sm font-semibold">{title}</span>
        <span className="text-xs text-muted-foreground">{description}</span>
      </div>
    </GameCard>
  );
}
