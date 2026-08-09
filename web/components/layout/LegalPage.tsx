import type { ReactNode } from "react";
import { FileText, type LucideIcon } from "lucide-react";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { CardContent } from "@/components/ui/card";

/**
 * docs/07-pages.md `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`: içerik
 * hukuki/uyumluluk metnidir, Claude tarafından **icat edilmez** — ama müşteri
 * kendi taslağını (ör. bir hukuk danışmanı/ChatGPT ile hazırladığı metni)
 * verirse, o metin olduğu gibi (köşeli parantezli alanlar dahil, değiştirmeden)
 * buraya `children` olarak geçirilir. `children` verilmezse eski "açıkça
 * geçici olduğu belirtilen" placeholder gösterilir (`01-workflow-rules.md`
 * Bölüm 0.4'teki placeholder yasağının doküman tarafından tanımlanmış istisnası).
 */
export function LegalPage({
  title,
  icon = FileText,
  children,
}: {
  title: string;
  icon?: LucideIcon;
  children?: ReactNode;
}) {
  return (
    <div className="mx-auto flex w-full min-h-0 max-w-7xl flex-1 flex-col gap-8 px-4 py-6">
      <PageHero icon={icon} title={title} accent="#94A3B8" />
      <GameCard className="flex-1 min-h-0">
        <CardContent className="flex h-full flex-col gap-6 overflow-y-auto text-sm text-muted-foreground">
          {children ?? (
            <p>
              İçerik yakında eklenecektir. Bu metin, müşteriden veya hukuk danışmanından gelecek resmi
              içerikle değiştirilene kadar yer tutucudur.
            </p>
          )}
        </CardContent>
      </GameCard>
    </div>
  );
}
