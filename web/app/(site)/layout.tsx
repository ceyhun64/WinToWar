"use client";

import { usePathname } from "next/navigation";
import { Footer } from "@/components/layout/Footer";
import { Header } from "@/components/layout/Header";
import { PageBackground } from "@/components/layout/PageBackground";

/** docs/07-pages.md "Navigasyon": bu sayfalar yalnızca logo/geri gösteren minimal header kullanır. */
const MINIMAL_HEADER_PATHS = ["/kosullar", "/gizlilik", "/sorumlu-oyun", "/cerezler", "/sss"];

/**
 * Landing redesign talimatı: `/` tek ekran (100dvh, scrollsuz), kendi
 * saydam/blur navbar'ını ve ince footer satırını taşıyan tam-ekran bir
 * kompozisyon (`components/landing/Landing.tsx`). Paylaşılan Header/Footer
 * (light tema, sabit yükseklik) burada render edilirse hem tema çakışması
 * hem de fazladan yükseklik (dolayısıyla scroll) oluşur — bu yüzden `/` bu
 * ortak chrome'un dışında tutulur, tıpkı `MINIMAL_HEADER_PATHS`'ın yaptığı
 * gibi tek bir yol listesiyle. Diğer hiçbir route etkilenmez.
 */
const CHROMELESS_PATHS = ["/"];

/**
 * `docs/04-style.md` Landing İstisnası — sitede genelleştirilmiş tasarım
 * sistemi: Landing'in görsel dilini (navy/mavi/altın palet, cam panel, aynı
 * buton/kart stili) `/lobi` pilotundan sonra kullanıcı onayıyla kalan tüm
 * kullanıcı sayfalarına taşır. Header/Footer ve `Button`/`Card`/`Badge`/
 * `Tabs`/`Input` zaten CSS değişken (`--background`, `--card`, `--primary`
 * vb.) tabanlı olduğundan bu sayfalar `.dark` kapsamına alınınca hiçbir
 * paylaşılan bileşen değiştirilmeden otomatik olarak yeni paleti alır (bkz.
 * `app/globals.css` `.dark` bloğu).
 *
 * 🔒 Kullanıcı talimatı: `/` (Landing), `/lobi` (tam eşleşme — alt route'ları
 * değil) ve `/admin/*` referans/tamamlanmış kabul edilir, bu listeye dahil
 * edilmez ve bu dosyada onlara özel hiçbir dal değiştirilmez. `/game/[matchId]`
 * zaten kendi ayrı (chrome'suz) layout'unu taşıdığı için bu group'un dışında —
 * bu listeyle hiç ilişkisi yok.
 */
const DARK_THEME_PATHS = [
  "/lobi",
  "/giris",
  "/kayit",
  "/sifremi-unuttum",
  "/hesap-ayarlari",
  "/destek",
  "/durum",
  "/sss",
  "/kosullar",
  "/gizlilik",
  "/sorumlu-oyun",
  "/cerezler",
  "/bakim",
  "/profil",
  "/cuzdan",
  "/gecmis",
  "/kurallar",
];

/** Dinamik segment taşıyan route'lar (`[token]`, `[invoiceId]`, `[matchId]`, `[inviteToken]`) — tam eşleşme yerine önek kontrolü gerekir. `/lobi/` öneki `/lobi` tam eşleşmesini etkilemez (trailing slash yok). */
const DARK_THEME_PREFIXES = ["/sifre-sifirla/", "/lobi/", "/odeme/", "/mac/"];

function isDarkThemePath(pathname: string): boolean {
  return DARK_THEME_PATHS.includes(pathname) || DARK_THEME_PREFIXES.some((prefix) => pathname.startsWith(prefix));
}

/**
 * docs/07-pages.md: `/game/[matchId]` hariç hemen hemen tüm sayfalarda ortak
 * Header/Footer. `game` kendi (chrome'suz) layout'unu taşıdığı için bu route
 * group'un dışında kalır (bkz. `02-architecture.md` dosya ağacı — `game` gerçek
 * bir segmenttir, bu grubun parçası değildir).
 */
export default function SiteLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  if (CHROMELESS_PATHS.includes(pathname)) {
    return <main className="flex min-w-0 flex-1 flex-col">{children}</main>;
  }

  const minimal = MINIMAL_HEADER_PATHS.includes(pathname);

  if (isDarkThemePath(pathname)) {
    return (
      <div className="dark flex min-h-dvh flex-1 flex-col text-foreground">
        <PageBackground />
        <Header minimal={minimal} />
        <main className="flex flex-1 flex-col">{children}</main>
        <Footer />
      </div>
    );
  }

  return (
    <>
      <Header minimal={minimal} />
      <main className="flex flex-1 flex-col">{children}</main>
      <Footer />
    </>
  );
}
