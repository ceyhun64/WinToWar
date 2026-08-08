import Link from "next/link";
import { Background } from "@/components/landing/Background";
import { Navbar } from "@/components/landing/Navbar";
import { Hero } from "@/components/landing/Hero";
import { BattleScene } from "@/components/landing/BattleScene";
import { FloatingCards } from "@/components/landing/FloatingCards";

/** docs/07-pages.md Footer içeriği ile birebir aynı 5 link — yalnızca sunum tarzı (ince tek satır) değişir. */
const FOOTER_LINKS = [
  { href: "/kurallar", label: "Kurallar" },
  { href: "/kosullar", label: "Kullanım Şartları" },
  { href: "/gizlilik", label: "Gizlilik Politikası" },
  { href: "/sorumlu-oyun", label: "Sorumlu Oyun" },
  { href: "/destek", label: "Destek" },
];

/**
 * Yalnızca `/` rotası için tam ekran (100dvh, scrollsuz) premium landing
 * kompozisyonu. `app/(site)/layout.tsx` bu rotada paylaşılan Header/Footer'ı
 * gizler, bu bileşen kendi navbar/footer'ını taşır (bkz. o dosyadaki not).
 */
export function Landing() {
  return (
    <div className="relative flex h-dvh w-full min-w-0 flex-col overflow-hidden text-white">
      <Background />

      <div className="relative z-10 flex h-full min-w-0 flex-col">
        <Navbar />

        <main className="flex min-w-0 flex-1 flex-col items-center justify-center gap-4 overflow-hidden px-4 sm:px-6 lg:px-10">
          <div className="mx-auto grid w-full min-w-0 max-w-7xl items-center gap-8 lg:grid-cols-[1.1fr_1fr] lg:gap-12">
            <Hero />
            <div className="relative hidden h-[min(50vh,440px)] lg:block">
              <BattleScene />
              <FloatingCards variant="floating" />
             
            </div>
          </div>

        
        </main>

        <footer className="min-w-0 border-t border-white/5 px-4 py-3 sm:px-6 lg:px-10">
          <div className="mx-auto flex min-w-0 max-w-7xl flex-col items-center justify-between gap-1.5 text-[11px] text-white/40 sm:flex-row">
            <span>© {new Date().getFullYear()} WinToWar</span>
            <nav className="flex flex-wrap items-center justify-center gap-x-4 gap-y-1">
              {FOOTER_LINKS.map((link) => (
                <Link key={link.href} href={link.href} className="transition-colors duration-150 hover:text-white/70">
                  {link.label}
                </Link>
              ))}
            </nav>
          </div>
        </footer>
      </div>
    </div>
  );
}
