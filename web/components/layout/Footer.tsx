import Link from "next/link";

/**
 * docs/07-pages.md "Footer içeriği — kesinleştirildi": bu beş link her
 * sayfada (oyun ekranı hariç) bulunur. docs/12-seo.md Bölüm 7 "Internal
 * Linking": `/sss`, `/cerezler`, `/durum` public/indexlenir sayfalar olduğu
 * halde hiçbir yerden linklenmiyordu (orphan) — mevcut footer link alanına
 * eklendi, yeni bir pazarlama metni/CTA icat edilmedi.
 */
const FOOTER_LINKS = [
  { href: "/kurallar", label: "Kurallar" },
  { href: "/sss", label: "SSS" },
  { href: "/kosullar", label: "Kullanım Şartları" },
  { href: "/gizlilik", label: "Gizlilik Politikası" },
  { href: "/cerezler", label: "Çerez Politikası" },
  { href: "/sorumlu-oyun", label: "Sorumlu Oyun" },
  { href: "/destek", label: "Destek" },
  { href: "/durum", label: "Sistem Durumu" },
];

export function Footer() {
  return (
    <footer className="min-w-0 border-t border-white/5 px-4 py-3 sm:px-6 lg:px-10">
      <div className="mx-auto flex min-w-0 max-w-7xl flex-col items-center justify-between gap-1.5 text-[11px] text-white/40 sm:flex-row">
        <span>© {new Date().getFullYear()} WinToWar</span>
        <nav className="flex flex-wrap items-center justify-center gap-x-4 gap-y-1">
          {FOOTER_LINKS.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className="transition-colors duration-150 hover:text-white/70"
            >
              {link.label}
            </Link>
          ))}
        </nav>
      </div>
    </footer>
  );
}
