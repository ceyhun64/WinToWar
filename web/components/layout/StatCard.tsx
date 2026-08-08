import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { GameCard } from "@/components/layout/GameCard";

/** Küçük istatistik/değer kartı — Profil, Cüzdan, Maç Özeti gibi sayfalardaki tekrar eden "etiket + değer" bloklarının tek kaynağı. */
export function StatCard({
  icon: Icon,
  label,
  value,
  accent = "#38BDF8",
}: {
  icon?: LucideIcon;
  label: string;
  value: ReactNode;
  accent?: string;
}) {
  return (
    <GameCard className="p-4">
      <div className="flex items-center gap-3">
        {Icon ? (
          <span
            className="flex size-9 shrink-0 items-center justify-center rounded-xl"
            style={{ backgroundColor: `${accent}22`, color: accent }}
          >
            <Icon className="size-4" aria-hidden="true" />
          </span>
        ) : null}
        <div className="flex flex-col">
          <span className="text-xs text-muted-foreground">{label}</span>
          <span className="text-lg font-semibold tabular-nums">{value}</span>
        </div>
      </div>
    </GameCard>
  );
}
