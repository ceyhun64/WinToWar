"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { buttonVariants } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";
import { getStoredDisplayName, isSignedIn, signOut } from "@/lib/identity";
import { getWalletBalance } from "@/lib/payments/api";

const GUEST_NAV_LINKS = [{ href: "/kurallar", label: "Kurallar" }];
const PLAYER_NAV_LINKS = [
  { href: "/lobi", label: "Lobi" },
  { href: "/kurallar", label: "Kurallar" },
];

/**
 * docs/08-page-content.md Bölüm 2.1: girişsiz kullanıcıya Giriş Yap/Kayıt Ol,
 * girişli kullanıcıya bakiye özeti (tıklanınca /cuzdan) + kullanıcı menüsü
 * (Profil, Hesap Ayarları, Çıkış) gösterilir. Header yalnızca kimlik + bakiye +
 * navigasyon taşır, Katman 2/3 bilgisi (kural/asker sayısı vb.) barındırmaz.
 * docs/07-pages.md "Navigasyon": legal sayfalar yalnızca logo/geri gösteren
 * minimal bir varyant kullanır (bkz. `minimal` prop).
 */
export function Header({ minimal = false }: { minimal?: boolean }) {
  const router = useRouter();
  const [displayName, setDisplayName] = useState<string | null>(null);
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);

  useEffect(() => {
    if (!isSignedIn()) return;
    setDisplayName(getStoredDisplayName());
    getWalletBalance()
      .then((dto) => setBalanceUsd(dto.balanceUsd))
      .catch(() => setBalanceUsd(null));
  }, []);

  function handleSignOut() {
    signOut();
    router.push("/");
  }

  const navLinks = displayName ? PLAYER_NAV_LINKS : GUEST_NAV_LINKS;

  return (
    <header className="border-b border-border bg-card px-4 py-3">
      <div className="mx-auto flex max-w-4xl items-center justify-between gap-4">
        <Link href="/" className="text-sm font-semibold">
          WinToWar
        </Link>
        {!minimal ? (
          <div className="flex items-center gap-4">
            <nav className="flex flex-wrap items-center gap-4 text-sm text-muted-foreground">
              {navLinks.map((link) => (
                <Link key={link.href} href={link.href} className="hover:text-foreground">
                  {link.label}
                </Link>
              ))}
            </nav>

            {displayName ? (
              <div className="flex items-center gap-2">
                <Link
                  href="/cuzdan"
                  className="rounded-2xl px-2.5 py-1 text-sm font-medium tabular-nums hover:bg-muted"
                >
                  {balanceUsd !== null ? `$${balanceUsd}` : <Skeleton className="inline-block h-4 w-10 align-middle" />}
                </Link>
                <DropdownMenu>
                  <DropdownMenuTrigger className="rounded-2xl px-2.5 py-1 text-sm font-medium hover:bg-muted">
                    {displayName}
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem render={<Link href="/profil">Profil</Link>} />
                    <DropdownMenuItem render={<Link href="/hesap-ayarlari">Hesap Ayarları</Link>} />
                    <DropdownMenuSeparator />
                    <DropdownMenuItem variant="destructive" onClick={handleSignOut}>
                      Çıkış
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <Link href="/giris" className={buttonVariants({ variant: "ghost", size: "sm" })}>
                  Giriş Yap
                </Link>
                <Link href="/kayit" className={buttonVariants({ size: "sm" })}>
                  Kayıt Ol
                </Link>
              </div>
            )}
          </div>
        ) : null}
      </div>
    </header>
  );
}
