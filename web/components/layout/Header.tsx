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
import {
  ensureSessionLoaded,
  getStoredDisplayName,
  isSignedIn,
  signOut,
  subscribeToSession,
} from "@/lib/identity";
import { useWallet } from "@/lib/payments/WalletProvider";
import { cn } from "@/lib/utils";
import Image from "next/image";

/* Kullanıcı talimatı: mobilde header'daki yazılar ve logo küçülür. Kapsam
   bilerek dar tutuldu — YALNIZCA yazı tipi boyutu ve logo yüksekliği; buton
   yüksekliği/padding'i, boşluklar ve min-width'ler değişmedi.

   Token'lar `globals.css` yerine burada duruyor: bu görevde yalnızca bu dosya
   değiştirilebilir. Her formül 390px'te TAM OLARAK bugünkü değeri üretir (üst
   sınır devrede), yalnızca ondan dar ekranlarda iner — yani 390/430 referans
   tasarımı piksel olarak değişmez.

   Üretilen değerler (px):
     LOGO_H     320:28.0  360:34.9  375:37.4  390:40.0  430:40.0  (h-10 = 2.5rem)
     BRAND_TEXT 320:14.0  360:16.3  375:17.1  390:18.0  430:18.0  (text-lg = 1.125rem)
     BODY_TEXT  320:12.0  360:13.1  375:13.6  390:14.0  430:14.0  (text-sm = 0.875rem) */
const LOGO_H =
  "h-[clamp(1.75rem,calc(2.5rem_+_(100vw_-_390px)_*_0.1714),2.5rem)]";
const BRAND_TEXT =
  "text-[length:clamp(0.875rem,calc(1.125rem_+_(100vw_-_390px)_*_0.0571),1.125rem)]";
const BODY_TEXT =
  "text-[length:clamp(0.75rem,calc(0.875rem_+_(100vw_-_390px)_*_0.0286),0.875rem)]";

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
  const { balanceUsd } = useWallet();

  useEffect(() => {
    function syncSession() {
      setDisplayName(isSignedIn() ? getStoredDisplayName() : null);
    }

    ensureSessionLoaded().then(syncSession);
    return subscribeToSession(syncSession);
  }, []);

  function handleSignOut() {
    signOut();
    router.push("/");
  }

  const navLinks = displayName ? PLAYER_NAV_LINKS : GUEST_NAV_LINKS;

  return (
    /* docs/25-responsive-v2.md — `px-4`/`gap-4` yerine akışkan `--header-gutter`.
       390px ve üzerinde birebir 1rem üretir (referans tasarım değişmez), yalnızca
       daha dar ekranlarda 0.5rem'e iner; gerekçe ve ölçüm globals.css'te. */
    <header className="w-full min-w-0 px-(--header-gutter) py-4 sm:px-6 lg:px-10">
      <div className="mx-auto flex min-w-0 max-w-7xl items-center justify-between gap-(--header-brand-gap) rounded-2xl border border-transparent bg-transparent px-(--header-gutter) py-2.5 sm:px-5">
        {/* Marka bloğu `shrink-0`: eskiden `min-w-0` + `truncate` idi, yani
            satırdaki tüm daralma payı buraya yönleniyordu — giriş yapılmış
            hâlde 375px'te marka "W.."ye kadar kırpılıyordu. Yazı ve logo dar
            ekranda zaten küçüldüğü için (BRAND_TEXT / LOGO_H) blok olduğu gibi
            sığıyor; daralma payı aşağıda kullanıcı adına bırakıldı, gerçekten
            değişken uzunlukta olan tek metin orası.

            (Eski not — docs/24 Bölüm 2 — `min-w-0` gerekçesi artık geçersiz:
            taşmanın sebebi tek kelimelik markanın kırpılamaması değil, aşağıda
            kaldırılan yapay `--header-actions-min` rezervasyonuydu.) */}
        <Link
          href="/"
          className={cn(
            "flex shrink-0 items-center gap-2 font-bold tracking-tight text-white",
            BRAND_TEXT
          )}
        >
          <Image
            src="/logo/logo-mark.png"
            alt=""
            width={437}
            height={531}
            className={cn("w-auto shrink-0 object-contain", LOGO_H)}
            priority
          />
          <span>WinToWar</span>
        </Link>
        {!minimal ? (
          /* `min-w-(--header-actions-min)` kaldırıldı: 375px'te 241px rezerve edip
             satırın kalanını markadan çalıyordu (globals.css:262). Blok artık
             gerçek içeriği kadar yer kaplar. */
          <div className="flex min-w-0 items-center gap-(--header-gutter)">
            {/* `flex-wrap` + `--header-nav-min` (375px'te 52px taban) nav'ı iki
                satıra kırıp header'ı iki kat yükseltiyordu. `shrink-0` +
                `flex-nowrap`: nav tam içeriği kadar yer kaplar ve hiç daralmaz —
                böylece ne sarmalanır ne de linkleri kutusundan taşar. docs/25'te
                bildirilen taşmanın asıl çözümü budur; taban genişlik rezerve
                etmek semptomu başka yere kaydırıyordu. */}
            <nav
              className={cn(
                "flex shrink-0 flex-nowrap items-center gap-4 whitespace-nowrap text-muted-foreground",
                BODY_TEXT,
                // Satır <sm'de nav ile birlikte sığmıyor (375px'te giriş yapılmış
                // hâlde kullanıcı adına ~7px kalıp tek harfe iniyordu; misafir
                // hâlde "Kayıt Ol" sağdan taşıyordu — `body`'de `overflow-hidden`
                // olduğu için sessizce kırpılarak). Linkler kaybolmuyor: giriş
                // yapılmışsa aşağıdaki kullanıcı menüsüne taşınıyor, misafirde
                // "Kurallar" zaten her sayfadaki footer'da duruyor.
                "max-sm:hidden"
              )}
            >
              {navLinks.map((link) => (
                <Link
                  key={link.href}
                  href={link.href}
                  className="hover:text-foreground"
                >
                  {link.label}
                </Link>
              ))}
            </nav>

            {displayName ? (
              <div className="flex min-w-0 items-center gap-3">
                {/* Bakiye `shrink-0`: tutar kısalırsa okunamaz hale gelir —
                    daralma payı kullanıcı adına bırakılır (aşağıdaki truncate). */}
                <Link
                  href="/cuzdan"
                  className={cn(
                    "shrink-0 rounded-2xl px-2.5 py-1 font-medium tabular-nums hover:bg-muted text-yellow-500",
                    BODY_TEXT
                  )}
                >
                  {balanceUsd !== null ? (
                    `$${balanceUsd}`
                  ) : (
                    <Skeleton className="inline-block h-4 w-10 align-middle" />
                  )}
                </Link>
                <DropdownMenu>
                  <DropdownMenuTrigger
                    className={cn(
                      "min-w-0 truncate rounded-2xl px-2.5 py-1 font-medium hover:bg-muted cursor-pointer",
                      BODY_TEXT
                    )}
                  >
                    {displayName}
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    {/* Yukarıda <sm'de gizlenen nav linklerinin karşılığı. */}
                    {navLinks.map((link) => (
                      <DropdownMenuItem
                        key={link.href}
                        className="sm:hidden"
                        render={<Link href={link.href}>{link.label}</Link>}
                      />
                    ))}
                    <DropdownMenuSeparator className="sm:hidden" />
                    <DropdownMenuItem
                      render={<Link href="/profil">Profil</Link>}
                    />
                    <DropdownMenuItem
                      render={
                        <Link href="/hesap-ayarlari">Hesap Ayarları</Link>
                      }
                    />
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      onClick={handleSignOut}
                    >
                      Çıkış
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            ) : (
              // Buton ölçeği (`components/ui/button.tsx`) 🔒 değiştirilmez;
              // yalnızca YAZI boyutu çağrı yerinde küçültülür — yükseklik ve
              // padding `size: "sm"`de kalır. `cn` (tailwind-merge) şart:
              // `buttonVariants` düz `cva`dır, çakışan sınıfları elemez;
              // `size: "sm"`in `text-sm`i aksi hâlde CSS sırasına göre bizim
              // boyutumuzu eziyordu (ölçüldü).
              <div className="flex shrink-0 items-center gap-3">
                <Link
                  href="/giris"
                  className={cn(
                    buttonVariants({ variant: "ghost", size: "sm" }),
                    BODY_TEXT
                  )}
                >
                  Giriş Yap
                </Link>
                <Link
                  href="/kayit"
                  className={cn(
                    buttonVariants({ size: "sm" }),
                    BODY_TEXT
                  )}
                >
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
