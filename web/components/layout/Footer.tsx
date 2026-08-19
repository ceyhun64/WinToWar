import Link from "next/link";
import { FooterLinksMenu } from "@/components/layout/FooterLinksMenu";

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

function FooterLinks({ className }: { className?: string }) {
  return (
    <nav className={className}>
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
  );
}

/**
 * Kullanıcı talimatı: "footer da şu anda mobile için çok yüksek, onun
 * yüksekliğini azalt, gerekirse açılır yap veya yazıları küçült."
 *
 * Sekiz link `flex-wrap` ile alt alta sarmalanıyor, üstüne globals.css'teki
 * dokunma hedefi kuralı (`footer nav a`, `pointer: coarse` + <390px) satır
 * yüksekliğini açıyordu: ölçülen 109px. Seçilen çözüm **açılır menü** — yazıyı
 * küçültmek 11px.in altına inmek demekti (okunabilirlik) ve linkleri atmak
 * docs/12-seo.md Bölüm 7.deki internal linking kararını bozardı.
 *
 * İlk uygulama `<details>` idi; kullanıcı talimatı üzerine ("header.daki isme
 * basınca açılan gibi olsun, tüm alanı kaplamasın") header.daki kullanıcı
 * menüsüyle aynı dropdown primitifine geçildi — bkz. `FooterLinksMenu`.
 *
 * ⚠️ docs/24-responsive-small-screens.md Bölüm 1'in 🔒 regresyon kuralı
 * 390–430px'i referans tasarım ilan eder; bu değişiklik o aralığı da
 * etkiliyor (footer orada da açılır hâle geliyor). Kuralın üstüne bilerek
 * çıkılmadı: müşterinin bu mesajdaki açık talimatı "mobil" diyor ve
 * CLAUDE.md öncelik sırasında 1. kademe odur. docs/24 Bölüm 3.1'deki
 * ❓ "390px+ telefonlarda da..." maddesi zaten bu kararın müşteriye
 * bırakıldığını işaretliyordu.
 *
 * ≥640px (`sm`) hiç değişmedi — eski tek satırlık düzen aynen korunur.
 */
export function Footer() {
  const year = new Date().getFullYear();

  return (
    <footer className="min-w-0 border-t border-white/5 px-4 py-1.5 sm:px-6 sm:py-3 lg:px-10">
      <div className="mx-auto min-w-0 max-w-8xl text-[10px] text-white/40 sm:text-[11px] lg:max-w-7xl">
        {/* ≥sm: önceki düzen, birebir. */}
        <div className="hidden min-w-0 items-center justify-between gap-1.5 sm:flex">
          <span>© {year} WinToWar</span>
          <FooterLinks className="flex flex-wrap items-center justify-center gap-x-4 gap-y-1 text-sm" />
        </div>

        {/* <sm: tek satır — telif solda, sağda yalnızca ok. Ok, header.daki
            kullanıcı menüsüyle aynı dropdown primitifini kullanır ve içerik
            portal.a çizildiği için footer.ın yüksekliği açıkken de değişmez,
            üstteki düzen kaymaz (bkz. FooterLinksMenu). `min-h-6` docs/24
            Bölüm 3.1.deki WCAG 2.2 AA 24×24 eşiğini satır için de sağlar. */}
        <div className="flex min-h-6 items-center justify-between gap-2 sm:hidden">
          <span>© {year} WinToWar</span>
          <FooterLinksMenu links={FOOTER_LINKS} />
        </div>
      </div>
    </footer>
  );
}
