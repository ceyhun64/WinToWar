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
    /* docs/24-responsive-small-screens.md Bölüm 7: metin sütununda `min-w-0`
       yoktu. `grid-cols-2` içinde 320px'te hücreye ~138px, `p-4` ve ikon
       düşüldükten sonra metne ~58px kalıyor; uzun bir görünen ad ya da
       "Tamamlandı" gibi bir durum değeri kartı aşıyor ve `Card`'ın
       `overflow-hidden`'ı yüzünden sessizce kırpılıyordu. 390px ve üzerinde
       değerler zaten sığdığı için ellipsis hiç görünmez. */
    <GameCard className="p-4">
      <div className="flex min-w-0 items-center gap-3">
        {Icon ? (
          <span
            className="flex size-9 shrink-0 items-center justify-center rounded-xl"
            style={{ backgroundColor: `${accent}22`, color: accent }}
          >
            <Icon className="size-4" aria-hidden="true" />
          </span>
        ) : null}
        <div className="flex min-w-0 flex-col">
          <span className="truncate text-xs text-muted-foreground">{label}</span>
          {/* `title`: değer kısaldığında tam hâli hâlâ erişilebilir kalsın —
              ellipsis bilgiyi gizlememeli. */}
          <span
            className="truncate text-lg font-semibold tabular-nums"
            title={typeof value === "string" ? value : undefined}
          >
            {value}
          </span>
        </div>
      </div>
    </GameCard>
  );
}
