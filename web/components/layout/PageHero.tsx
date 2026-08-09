import type { LucideIcon } from "lucide-react";

/**
 * `docs/04-style.md` Landing İstisnası — sitede genelleştirilmiş tasarım
 * sistemi: Landing'in "büyük sinematik hero"su hiçbir yerde tekrarlanmaz
 * (kullanıcı talimatı — "hero'yu her sayfada tekrar etmek istemiyorum"), ama
 * her sayfanın aynı kimlik dilini taşıyan tek, küçük bir başlık bloğu vardır:
 * ikon rozeti + başlık + tek cümlelik alt başlık. Video/illüstrasyon yok,
 * `docs/08-page-content.md` Bölüm 1.10'daki "tek H1, kısa öbek" kuralıyla
 * birebir uyumlu — `subtitle` marka sloganı değil, Katman 2 bağlam metnidir.
 */
export function PageHero({
  icon: Icon,
  title,
  subtitle,
  accent = "#38BDF8",
}: {
  icon: LucideIcon;
  title: string;
  subtitle?: string;
  accent?: string;
}) {
  return (
    <div className="flex items-center justify-center gap-4">
   
      <div className="flex flex-col gap-0.5">
        <h1 className="text-2xl font-bold tracking-tight text-center">{title}</h1>
        {subtitle ? <p className="text-sm text-muted-foreground text-center">{subtitle}</p> : null}
      </div>
    </div>
  );
}
