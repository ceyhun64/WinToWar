"use client";

import { usePathname } from "next/navigation";
import { Footer } from "@/components/layout/Footer";
import { Header } from "@/components/layout/Header";

/** docs/07-pages.md "Navigasyon": bu sayfalar yalnızca logo/geri gösteren minimal header kullanır. */
const MINIMAL_HEADER_PATHS = ["/kosullar", "/gizlilik", "/sorumlu-oyun", "/cerezler", "/sss"];

/**
 * docs/07-pages.md: `/game/[matchId]` hariç hemen hemen tüm sayfalarda ortak
 * Header/Footer. `game` kendi (chrome'suz) layout'unu taşıdığı için bu route
 * group'un dışında kalır (bkz. `02-architecture.md` dosya ağacı — `game` gerçek
 * bir segmenttir, bu grubun parçası değildir).
 */
export default function SiteLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const minimal = MINIMAL_HEADER_PATHS.includes(pathname);

  return (
    <>
      <Header minimal={minimal} />
      <main className="flex flex-1 flex-col">{children}</main>
      <Footer />
    </>
  );
}
