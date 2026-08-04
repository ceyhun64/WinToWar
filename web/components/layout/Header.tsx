import Link from "next/link";

const NAV_LINKS = [
  { href: "/lobi", label: "Lobi" },
  { href: "/cuzdan", label: "Cüzdan" },
  { href: "/kurallar", label: "Kurallar" },
  { href: "/profil", label: "Profil" },
];

/**
 * docs/07-pages.md "Navigasyon": çoğu sayfada tam header (logo + nav) gösterilir.
 * Legal sayfalar (`/kosullar` vb.) yalnızca logo/geri gösteren minimal bir
 * varyant kullanır (bkz. `minimal` prop).
 */
export function Header({ minimal = false }: { minimal?: boolean }) {
  return (
    <header className="border-b border-border bg-card px-4 py-3">
      <div className="mx-auto flex max-w-4xl items-center justify-between gap-4">
        <Link href="/" className="text-sm font-semibold">
          WinToWar
        </Link>
        {!minimal ? (
          <nav className="flex flex-wrap items-center gap-4 text-sm text-muted-foreground">
            {NAV_LINKS.map((link) => (
              <Link key={link.href} href={link.href} className="hover:text-foreground">
                {link.label}
              </Link>
            ))}
          </nav>
        ) : null}
      </div>
    </header>
  );
}
