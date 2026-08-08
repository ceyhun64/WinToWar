import type { Metadata } from "next";
import { HeartHandshake } from "lucide-react";
import { LegalPage } from "@/components/layout/LegalPage";
import { canonicalFor } from "@/lib/metadata";

export const metadata: Metadata = {
  title: "Sorumlu Oyun — WinToWar",
  description:
    "WinToWar Sorumlu Oyun: bütçe belirleme, mola verme, yaş sınırı ve harcama limitleri hakkında bilgi ve öneriler.",
  ...canonicalFor("/sorumlu-oyun"),
};

/**
 * 🛠️ İçerik müşterinin kendi hazırladığı taslaktan alındı (bkz. sohbet geçmişi,
 * 2026-08-08) — köşeli parantezli alanlar ([18 yaş], [support@...], [Tarih])
 * BİLEREK doldurulmadan bırakıldı. Özellikle yaş sınırı satırı: sayfada bir
 * sayı yazmak backend'de gerçek bir kontrol anlamına gelmez — bu sayfa yalnızca
 * metni taşır, yaş doğrulamasının kendisi ayrı bir mühendislik/iş kararıdır
 * (bkz. `docs/05-payment.md` Bölüm 10.1 "Coğrafi/yaş kısıtlaması", şu an yalnızca
 * kayıt formunda bir beyan onayı var, gerçek doğrulama yok). Gerçek değerler
 * müşteriden/hukuk danışmanından gelmeden değiştirilmez (bkz. `docs/07-pages.md`
 * "İçerik Claude tarafından yazılmaz" kuralı — bu içerik Claude tarafından
 * yazılmadı, yalnızca müşterinin verdiği metin sayfaya taşındı). Bu hâliyle
 * nihai/hukuken onaylanmış bir metin değildir.
 */
export default function SorumluOyunPage() {
  return (
    <LegalPage title="Sorumlu Oyun" icon={HeartHandshake}>
      <p>
        WinToWar, rekabetçi oyun deneyiminin eğlenceli ve kontrollü şekilde kullanılmasını destekler. Ücretli
        oyunlara katılırken yalnızca kaybetmeyi göze alabileceğiniz tutarlarla oynamanızı öneriyoruz.
      </p>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">Oyun bir gelir kaynağı değildir</h2>
        <p>WinToWar&apos;daki oyun sonuçları garanti edilemez.</p>
        <p>Kazanç elde etmek amacıyla oynadığınız tutardan daha fazlasını yatırmamalısınız.</p>
        <p>Geçmişte kazanmış olmak gelecekte kazanacağınız anlamına gelmez.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">Bütçenizi belirleyin</h2>
        <p>Ücretli oyunlara katılmadan önce kendiniz için bir bütçe belirleyin.</p>
        <p>Kayıplarınızı geri kazanmak amacıyla daha yüksek ücretli oyunlara geçmeyin veya daha fazla para yatırmayın.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">Ara verin</h2>
        <p>Oyunun kontrolünüzden çıktığını düşündüğünüzde ara verin.</p>
        <p>Aşağıdaki durumları yaşıyorsanız oynamaya devam etmek yerine mola vermenizi öneriyoruz:</p>
        <ul className="list-disc pl-5">
          <li>Kaybettiklerinizi geri kazanmak için tekrar tekrar ödeme yapıyorsanız</li>
          <li>Planladığınızdan daha uzun süre oynuyorsanız</li>
          <li>Planladığınızdan daha fazla para harcıyorsanız</li>
          <li>Oyunu bırakmakta zorlanıyorsanız</li>
          <li>Oyun nedeniyle günlük hayatınız veya finansal durumunuz etkileniyorsa</li>
        </ul>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">Yaş sınırı</h2>
        <p>Ücretli oyunlara katılım için minimum yaş sınırı uygulanır.</p>
        <p>[18 yaş] altındaki kullanıcıların ücretli oyunlara katılmasına izin verilmez.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">Harcama ve oyun limitleri</h2>
        <p>
          Kullanıcıların kendi oyun harcamalarını kontrol edebilmesini sağlayacak limit ve güvenlik özellikleri
          uygulanabilir.
        </p>
        <p>WinToWar, şüpheli veya olağandışı kullanım tespit edildiğinde hesabı güvenlik incelemesine alabilir.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">Hesap kapatma / mola</h2>
        <p>
          Kullanıcı, hesabını geçici olarak kullanıma kapatma veya hesabının kapatılması için destek ekibiyle
          iletişime geçebilir.
        </p>
        <p>Destek: [support@...]</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">Yardım</h2>
        <p>
          Oyunun sizin için kontrol edilmesi zor hale geldiğini düşünüyorsanız oynamaya ara verin ve gerektiğinde
          profesyonel destek alın.
        </p>
      </section>

      <p className="text-xs">Son güncelleme: [Tarih]</p>
    </LegalPage>
  );
}
