import { Background } from "@/components/landing/Background";
import { Navbar } from "@/components/landing/Navbar";
import { Hero } from "@/components/landing/Hero";
import { BattleScene } from "@/components/landing/BattleScene";
import { FloatingCards } from "@/components/landing/FloatingCards";
import { Footer } from "@/components/layout/Footer";

/**
 * Yalnızca `/` rotası için tam ekran (100dvh, scrollsuz) premium landing
 * kompozisyonu. `app/(site)/layout.tsx` bu rotada paylaşılan Header/Footer'ı
 * gizler, bu bileşen kendi navbar.ını taşır (bkz. o dosyadaki not); footer ise
 * artık kopyalanmaz, paylaşılan `components/layout/Footer.tsx` kullanılır.
 */
export function Landing() {
  return (
    <div className="relative flex h-dvh w-full min-w-0 flex-col overflow-hidden text-white">
      <Background />

      <div className="relative z-10 flex h-full min-w-0 flex-col">
        <Navbar />

        {/* docs/24-responsive-small-screens.md Bölüm 4 — `overflow-hidden` →
            `overflow-y-auto`. Ölçüm: 320×568'de Hero yığını (başlık + paragraf +
            iki CTA + adım şeridi + 2×2 istatistik kartı) ~527px yer istiyor, oysa
            navbar ve footer düşüldükten sonra ~386px kalıyor. Akışkan ölçek
            (`--landing-gap`/`--landing-cta-h`, bkz. globals.css) bu açığı 375px'e
            kadar kapatıyor; daha dar ekranlarda içerik yine de taşabiliyor ve eski
            `overflow-hidden` + `justify-center` bileşimi taşan kısmı HEM ÜSTTEN
            HEM ALTTAN kırpıp erişilemez bırakıyordu (metin/CTA silinmiş gibi
            görünüyordu). docs/13-scroll-lock.md Bölüm 1.3'ün öngördüğü "son çare iç
            scroll" burada yalnızca bir güvenlik ağıdır: 390px ve üzerinde içerik
            zaten sığdığı için scroll HİÇ devreye girmez, görünüm birebir aynıdır.
            `html`/`body` scroll kilidi (aynı dosyanın 🔒 asıl talimatı) bozulmaz —
            kaydırma bu `main`'in içinde kalır. */}
        {/* docs/25-responsive-v2.md — `justify-center` → `justify-center-safe`.
            KÖK NEDEN: bir kaydırma kabında ortalanmış (`justify-content: center`)
            içerik kabından uzun olduğunda taşma İKİ UÇTAN birden olur; üstteki
            yarısı scroll başlangıcının (scrollTop = 0) YUKARISINA düşer ve
            kaydırılarak dahi erişilemez — tarayıcı negatif scroll offset'i yoktur.
            Ölçüm: H1'in üst kısmı 320px'te 45px, 360px'te 34px, 375px'te 31px
            kırpılıyordu; 390px ve üzerinde içerik zaten sığdığı için 0px.
            `safe center`, YALNIZCA taşma olduğunda hizayı `start`'a düşürür —
            taşma yoksa davranışı `center` ile birebir aynıdır, dolayısıyla
            390/430 referans görünümü piksel olarak değişmez (docs/24 Bölüm 1 🔒). */}
        <main className="flex min-w-0 flex-1 flex-col items-center justify-center-safe gap-4 overflow-y-auto px-4 sm:px-6 lg:px-10">
          <div className="mx-auto grid w-full min-w-0 max-w-7xl items-center gap-8 lg:grid-cols-[1.1fr_1fr] lg:gap-12">
            <Hero />
            <div className="relative hidden h-[min(50vh,440px)] lg:block">
              <BattleScene />
              <FloatingCards variant="floating" />
             
            </div>
          </div>

        
        </main>

        {/* Kullanıcı geri bildirimi: "en küçük ekranlı mobil cihazlarda footer
            çok yüksek". KÖK NEDEN: Landing kendi footer.ını kopyalamıştı, bu
            yüzden `components/layout/Footer.tsx`.teki mobil açılır (`<details>`)
            çözümü `/` rotasına hiç ulaşmıyordu. Kopya kaldırıldı, paylaşılan
            bileşen kullanılıyor — mobilde kapalıyken tek satır, `sm`+ düzen
            aynı. Yan etki: docs/12-seo.md Bölüm 7 gereği paylaşılan footer.da
            bulunan /sss, /cerezler, /durum linkleri artık `/` üzerinde de var
            (orphan sayfa kalmaması zaten istenen davranıştı). */}
        <Footer />
      </div>
    </div>
  );
}
