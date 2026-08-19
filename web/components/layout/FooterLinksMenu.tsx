"use client";

import Link from "next/link";
import { ChevronUpIcon } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

/**
 * Kullanıcı talimatı: "header'daki isme basınca açılan gibi bir sistem olsun,
 * tüm alanı kaplamasın." Bu yüzden mobil footer'ın açılır listesi artık
 * `components/layout/Header.tsx`'teki kullanıcı menüsüyle aynı primitifi
 * (`components/ui/dropdown-menu.tsx`) kullanır: içerik bir portal'a çizilir,
 * yani footer'ın yüksekliği açıkken de değişmez ve üstteki hiçbir düzen
 * kaymaz — önceki `<details>` + `absolute` overlay çözümünün amacı da buydu,
 * fakat o çözüm satırın tamamını kaplıyordu.
 *
 * `side="top"`: menü footer'ın üstünde açılır (ok da yukarıyı gösterir).
 * `w-auto`: primitifin varsayılanı tetikleyici genişliği (`--anchor-width`)
 * kadar; tetikleyici sadece bir ok olduğu için içerik kadar genişlemesi gerekir.
 *
 * Yalnızca istemci tarafı gerektiği için `Footer.tsx` sunucu bileşeni kalır.
 */
export function FooterLinksMenu({
  links,
}: {
  links: { href: string; label: string }[];
}) {
  return (
    <DropdownMenu>
      {/* `size-6` kutu docs/24 Bölüm 3.1'deki 24×24 dokunma hedefini sağlar. */}
      <DropdownMenuTrigger className="flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-md text-white/55 hover:text-white/80">
        <span className="sr-only">Bağlantılar</span>
        <ChevronUpIcon className="size-3.5 transition-transform duration-150 data-[popup-open]:rotate-180" />
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="end" className="w-auto min-w-44">
        {links.map((link) => (
          <DropdownMenuItem
            key={link.href}
            render={<Link href={link.href}>{link.label}</Link>}
          />
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
