import type { Metadata } from "next";
import { ShieldCheck } from "lucide-react";
import { LegalPage } from "@/components/layout/LegalPage";
import { canonicalFor } from "@/lib/metadata";

export const metadata: Metadata = {
  title: "Gizlilik Politikası — WinToWar",
  description:
    "WinToWar Gizlilik Politikası: hangi kullanıcı verilerinin toplandığı, nasıl kullanıldığı ve saklandığı açıklanır.",
  ...canonicalFor("/gizlilik"),
};

/**
 * 🛠️ İçerik müşterinin kendi hazırladığı taslaktan alındı (bkz. sohbet geçmişi,
 * 2026-08-08) — köşeli parantezli alanlar ([privacy@... / destek e-postası],
 * [Tarih] vb.) BİLEREK doldurulmadan bırakıldı, gerçek değerler
 * müşteriden/hukuk danışmanından (özellikle KVKK/GDPR kapsamında veri
 * sorumlusu bilgisi) gelmeden değiştirilmez (bkz. `docs/07-pages.md` "İçerik
 * Claude tarafından yazılmaz" kuralı — bu içerik Claude tarafından yazılmadı,
 * yalnızca müşterinin verdiği metin sayfaya taşındı). Bu hâliyle nihai/hukuken
 * onaylanmış bir metin değildir.
 */
export default function GizlilikPage() {
  return (
    <LegalPage title="Gizlilik Politikası" icon={ShieldCheck}>
      <p>
        WinToWar olarak kullanıcılarımızın kişisel verilerinin gizliliğini ve güvenliğini önemsiyoruz. Bu politika,
        WinToWar platformunu kullanırken hangi verilerin işlenebileceğini ve bu verilerin hangi amaçlarla
        kullanılabileceğini açıklar.
      </p>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">1. Toplanan Veriler</h2>
        <p>Platformun kullanımına bağlı olarak aşağıdaki bilgiler işlenebilir:</p>
        <ul className="list-disc pl-5">
          <li>Kullanıcı adı / görünen ad</li>
          <li>Hesap bilgileri</li>
          <li>Oyun ve eşleşme kayıtları</li>
          <li>Bakiye ve finansal işlem kayıtları</li>
          <li>Para yatırma ve çekme işlem bilgileri</li>
          <li>Para çekme sırasında sağlanan Litecoin adresi</li>
          <li>IP adresi</li>
          <li>Cihaz ve tarayıcı bilgileri</li>
          <li>Güvenlik ve hata kayıtları</li>
          <li>Destek talepleri ve kullanıcı ile yapılan iletişimler</li>
        </ul>
        <p>
          Ödeme işlemleri sırasında kullanılan blockchain ve ödeme hizmetlerine bağlı olarak işlem kimliği
          (transaction ID), blockchain adresi ve benzeri teknik bilgiler de sistemlerimizde veya hizmet
          sağlayıcılarımızda bulunabilir.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">2. Verilerin Kullanım Amaçları</h2>
        <p>Veriler aşağıdaki amaçlarla kullanılabilir:</p>
        <ul className="list-disc pl-5">
          <li>Kullanıcı hesabının oluşturulması ve yönetilmesi</li>
          <li>Oyunların ve eşleşmelerin gerçekleştirilmesi</li>
          <li>Bakiye işlemlerinin yürütülmesi</li>
          <li>Para yatırma ve çekme işlemlerinin gerçekleştirilmesi</li>
          <li>Hile ve dolandırıcılığın önlenmesi</li>
          <li>Platform güvenliğinin sağlanması</li>
          <li>Teknik sorunların tespit edilmesi</li>
          <li>Kullanıcı desteğinin sağlanması</li>
          <li>Yasal ve düzenleyici yükümlülüklerin yerine getirilmesi</li>
        </ul>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">3. Ödeme Hizmetleri</h2>
        <p>LTC işlemleri için üçüncü taraf ödeme ve blockchain altyapıları kullanılabilir.</p>
        <p>Ödeme işlemlerinin gerçekleştirilmesi amacıyla gerekli bilgiler ilgili hizmet sağlayıcılarla paylaşılabilir.</p>
        <p>
          Blockchain üzerinde gerçekleşen işlemlerin doğası gereği bazı işlem bilgilerinin kalıcı olarak blockchain
          üzerinde tutulabileceğini unutmayınız.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">4. Verilerin Saklanması</h2>
        <p>
          Kişisel veriler, işlenme amaçlarının gerektirdiği süre boyunca ve uygulanabilir yasal yükümlülüklerin
          gerektirdiği süreler dikkate alınarak saklanır.
        </p>
        <p>
          Güvenlik ve finansal işlem kayıtları, gerektiğinde işlem geçmişinin doğrulanabilmesi ve kötüye kullanımın
          tespit edilebilmesi amacıyla daha uzun süre saklanabilir.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">5. Veri Güvenliği</h2>
        <p>
          WinToWar, kullanıcı verilerini yetkisiz erişim, değiştirme, kayıp veya kötüye kullanıma karşı korumak için
          uygun teknik ve idari güvenlik önlemleri uygulamayı amaçlar.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">6. Kullanıcı Hakları</h2>
        <p>
          Uygulanabilir mevzuat kapsamında kullanıcılar kişisel verileriyle ilgili bilgi talep etme, düzeltme,
          silme veya diğer yasal haklarını kullanma imkanına sahip olabilir.
        </p>
        <p>Bu talepler için:</p>
        <p>E-posta: [privacy@... / destek e-postası]</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">7. Politika Değişiklikleri</h2>
        <p>Bu Gizlilik Politikası gerektiğinde güncellenebilir. Güncel versiyon platform üzerinde yayınlanır.</p>
      </section>

      <p className="text-xs">Son güncelleme: [Tarih]</p>
    </LegalPage>
  );
}
