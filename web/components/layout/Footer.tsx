import Link from "next/link";

/** docs/07-pages.md "Footer içeriği — kesinleştirildi": bu beş link her sayfada (oyun ekranı hariç) bulunur. */
const FOOTER_LINKS = [
  { href: "/kurallar", label: "Kurallar" },
  { href: "/kosullar", label: "Kullanım Şartları" },
  { href: "/gizlilik", label: "Gizlilik Politikası" },
  { href: "/sorumlu-oyun", label: "Sorumlu Oyun" },
  { href: "/destek", label: "Destek" },
];

export function Footer() {
  return (
    <footer className="mt-auto border-t border-border bg-card px-4 py-6">
      <div className="mx-auto flex max-w-4xl flex-col gap-3 text-xs text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
        <span>© {new Date().getFullYear()} WinToWar</span>
        <nav className="flex flex-wrap gap-x-4 gap-y-1">
          {FOOTER_LINKS.map((link) => (
            <Link key={link.href} href={link.href} className="hover:text-foreground">
              {link.label}
            </Link>
          ))}
        </nav>
      </div>
    </footer>
  );
}
