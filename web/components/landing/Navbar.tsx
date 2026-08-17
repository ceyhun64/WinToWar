"use client";

import Link from "next/link";
import Image from "next/image";
import { useEffect, useState } from "react";
import { Swords, Gamepad2, BookOpen, HelpCircle } from "lucide-react";
import { ensureSessionLoaded, isSignedIn, getStoredDisplayName, subscribeToSession } from "@/lib/identity";
import { useWallet } from "@/lib/payments/WalletProvider";
import { cn } from "@/lib/utils";

/**
 * Yalnızca `/` (Landing) rotası için özel navbar — site genelindeki
 * `components/layout/Header.tsx`'in yerini almaz, onun yanına eklenen ayrı bir
 * bileşendir (bkz. `app/(site)/layout.tsx` — Landing rotasında paylaşılan
 * Header/Footer render edilmez, bu bileşen onun yerini alır). Nav linkleri
 * yalnızca projede gerçekten var olan rotalara gider — Leaderboard/Discord gibi
 * karşılığı olmayan bir sayfa/URL icat edilmez.
 * Logo: `public/logo/logo-mark.png`, kaynak `public/logo/logo.png`'nin
 * (1536x1024, ikon ortada geniş bir boşluk/glow ile) `sharp` ile şeffaflığa
 * göre sıkı kırpılmış hali — orijinal dosya küçük navbar boyutunda neredeyse
 * görünmez kalıyordu, ham dosya kendisi değiştirilmedi/silinmedi.
 * `docs/04-style.md` Landing İstisnası: saydam/blur zemin yerine düz/opak "HUD
 * çubuğu" görünümü (kullanıcının "çok fazla cam efekti" geri bildirimi), nav
 * linkleri artık ikon eşliğinde.
 */
export function Navbar() {
  const [displayName, setDisplayName] = useState<string | null>(null);
  const { balanceUsd } = useWallet();

  useEffect(() => {
    function syncSession() {
      setDisplayName(isSignedIn() ? getStoredDisplayName() : null);
    }

    ensureSessionLoaded().then(syncSession);
    return subscribeToSession(syncSession);
  }, []);

  const navLinkClass = "flex items-center gap-1.5 text-sm font-medium text-white/70 transition-colors duration-150 hover:text-white";

  return (
    <header className="w-full min-w-0 px-4 py-4 sm:px-6 lg:px-10">
      <div className="mx-auto flex min-w-0 max-w-7xl items-center justify-between rounded-2xl border border-transparent bg-transparent px-4 py-2.5 sm:px-5">
        <Link href="/" className="flex items-center gap-2 text-lg font-bold tracking-tight text-white">
          <Image src="/logo/logo-mark.png" alt="" width={437} height={531} className="h-10 w-auto shrink-0 object-contain" priority />
          WinToWar
        </Link>

        <nav className="hidden items-center gap-6 md:flex">
          <Link href={displayName ? "/lobi" : "/kayit"} className={navLinkClass}>
            <Gamepad2 className="size-4" aria-hidden="true" />
            Oyna
          </Link>
          <Link href="/lobi" className={navLinkClass}>
            <Swords className="size-4" aria-hidden="true" />
            Pratik
          </Link>
          <Link href="/kurallar" className={navLinkClass}>
            <BookOpen className="size-4" aria-hidden="true" />
            Kurallar
          </Link>
          <Link href="/sss" className={navLinkClass}>
            <HelpCircle className="size-4" aria-hidden="true" />
            SSS
          </Link>
        </nav>

        <div className="flex items-center gap-2">
          {displayName ? (
            <Link
              href="/cuzdan"
              className={cn(
                "rounded-lg border border-white/10 bg-white/5 px-3 py-1.5 text-sm font-medium tabular-nums text-white transition-colors duration-150 hover:bg-white/10"
              )}
            >
              {balanceUsd !== null ? `$${balanceUsd}` : "Cüzdan"}
            </Link>
          ) : (
            <>
              <Link
                href="/giris"
                className="hidden rounded-lg px-3 py-1.5 text-sm font-medium text-white/70 transition-colors duration-150 hover:text-white sm:inline-block"
              >
                Giriş Yap
              </Link>
              <Link
                href="/kayit"
                className="rounded-lg bg-[#38BDF8] px-3 py-1.5 text-sm font-semibold text-[#070B14] transition-all duration-150 hover:bg-[#38BDF8]/85"
              >
                Kayıt Ol
              </Link>
            </>
          )}
        </div>
      </div>
    </header>
  );
}
