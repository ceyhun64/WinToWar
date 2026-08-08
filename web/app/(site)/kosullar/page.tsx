import type { Metadata } from "next";
import { FileText } from "lucide-react";
import { LegalPage } from "@/components/layout/LegalPage";
import { canonicalFor } from "@/lib/metadata";

export const metadata: Metadata = {
  title: "Kullanım Şartları — WinToWar",
  description:
    "WinToWar Kullanım Şartları: hesap sorumluluğu, oyun ücretleri, komisyon, para yatırma/çekme ve iade kuralları.",
  ...canonicalFor("/kosullar"),
};

/**
 * 🛠️ İçerik müşterinin kendi hazırladığı taslaktan alındı (bkz. sohbet geçmişi,
 * 2026-08-08) — köşeli parantezli alanlar ([Şirket / kişi unvanı], [destek
 * e-postası], [yasal adres], [Tarih] vb.) BİLEREK doldurulmadan bırakıldı,
 * gerçek değerler müşteriden/hukuk danışmanından gelmeden değiştirilmez (bkz.
 * `docs/07-pages.md` "İçerik Claude tarafından yazılmaz" kuralı — bu içerik
 * Claude tarafından yazılmadı, yalnızca müşterinin verdiği metin sayfaya
 * taşındı). Bu hâliyle nihai/hukuken onaylanmış bir metin değildir.
 */
export default function KosullarPage() {
  return (
    <LegalPage title="Kullanım Şartları" icon={FileText}>
      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">1. Genel</h2>
        <p>
          WinToWar, kullanıcıların çevrim içi strateji ve rekabet oyunlarına katılabildiği bir oyun platformudur.
          Platformu kullanarak bu Kullanım Şartları&apos;nı kabul etmiş olursunuz.
        </p>
        <p>
          WinToWar&apos;a kayıt olarak veya platformdaki herhangi bir oyuna katılarak bu şartlara uymayı kabul etmiş
          sayılırsınız.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">2. Hesap</h2>
        <p>Kullanıcı hesabınızın güvenliğinden siz sorumlusunuz.</p>
        <p>
          Hesabınız üzerinden gerçekleştirilen işlemlerden, hesabınızın üçüncü kişiler tarafından kullanılmasına
          ilişkin durumlar da dahil olmak üzere, kullanıcı olarak sorumlu olabilirsiniz.
        </p>
        <p>
          Yanlış, yanıltıcı veya başkasına ait bilgiler kullanılması; hile, bot veya otomatik yazılım kullanılması;
          başka kullanıcıların oyun deneyimini bozacak davranışlarda bulunulması yasaktır.
        </p>
        <p>
          WinToWar, güvenlik veya platform kurallarının ihlali şüphesi halinde hesabı geçici olarak sınırlandırabilir
          veya kapatabilir.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">3. Oyunlar ve Eşleşmeler</h2>
        <p>
          WinToWar&apos;daki oyunlar gerçek oyuncular arasında gerçekleştirilir. Sistem tarafından oyuna dahil edilen
          kazanç sağlayan botlar kullanılmaz.
        </p>
        <p>Standart ve VIP odaların kuralları oda oluşturulurken veya ilgili oyun ekranında gösterilir.</p>
        <p>
          VIP odalarda oda sahibi oyuncu sayısı, giriş ücreti, gri bölge savunması, harita görüşü ve varsa parola
          gibi seçenekleri belirleyebilir.
        </p>
        <p>Oyun sonuçları sunucudaki oyun kayıtları esas alınarak belirlenir.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">4. Oyun Ücretleri ve Komisyon</h2>
        <p>Ücretli oyunlarda giriş ücreti oyun başlamadan önce kullanıcıya gösterilir.</p>
        <p>
          Bir oyundaki toplam ödül havuzu, ilgili oyuna katılan ve ücret ödemesi onaylanan oyuncuların giriş
          ücretlerine göre hesaplanır.
        </p>
        <p>WinToWar, oyun ödül havuzundan %10 oranında sistem komisyonu alır.</p>
        <p className="rounded-2xl border border-border bg-background/60 p-3 text-xs">
          Örneğin: $4,00 toplam giriş → $4,00 ödül havuzu → $0,40 sistem komisyonu → $3,60 kazanan bakiyesi.
        </p>
        <p>Uygulanan ücretler ve komisyonlar kullanıcıya işlem öncesinde gösterilir.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">5. Bakiye</h2>
        <p>Platformdaki kullanıcı bakiyesi arayüzde USD cinsinden gösterilir.</p>
        <p>
          Kullanıcının oyun bakiyesi; oyun giriş ücretleri, kazançlar, iadeler ve diğer desteklenen finansal işlemler
          sonucunda değişebilir.
        </p>
        <p>Kullanıcı bakiyesi, yalnızca platformda desteklenen işlemler kapsamında kullanılabilir.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">6. Para Yatırma ve Çekme</h2>
        <p>Para yatırma ve çekme işlemleri Litecoin (LTC) altyapısı üzerinden gerçekleştirilebilir.</p>
        <p>
          Para yatırma sırasında kullanıcıya ilgili ödeme ekranında gerekli Litecoin ödeme bilgileri gösterilir.
        </p>
        <p>
          Para çekme işlemlerinde kullanıcı tarafından sağlanan Litecoin adresinin doğruluğundan kullanıcı
          sorumludur. Yanlış adrese gerçekleştirilen ve blockchain üzerinde tamamlanan transferlerin geri alınması
          mümkün olmayabilir.
        </p>
        <p>Para çekme işlemleri güvenlik, teknik kontrol, uyumluluk veya manuel inceleme gerektirebilir.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">7. İadeler</h2>
        <p>
          Eşleşme gerçekleşmemesi, teknik hata, fazla ödeme veya sistem tarafından belirlenen diğer iade
          durumlarında kullanıcı bakiyesine iade yapılabilir.
        </p>
        <p>
          Bir oyun için ücret alınmış ancak ilgili oyun koşulları nedeniyle kullanıcı oyuna katılamamışsa, sistem
          kurallarına göre ücretin tamamı veya ilgili tutar kullanıcı bakiyesine iade edilir.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">8. Hile ve Kötüye Kullanım</h2>
        <p>Aşağıdaki davranışlar yasaktır:</p>
        <ul className="list-disc pl-5">
          <li>Bot veya otomasyon kullanmak</li>
          <li>Oyunun istemci veya sunucu davranışını manipüle etmek</li>
          <li>Açık/bug kullanarak haksız avantaj elde etmek</li>
          <li>Birden fazla hesap kullanarak sistemi kötüye kullanmak</li>
          <li>Başka kullanıcılarla haksız kazanç amacıyla anlaşmak</li>
          <li>Ödeme sistemini veya bakiyeyi manipüle etmeye çalışmak</li>
          <li>Başka kullanıcıların hesaplarına erişmeye çalışmak</li>
        </ul>
        <p>İhlal durumunda hesap ve ilgili işlemler incelemeye alınabilir.</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">9. Sorumluluk</h2>
        <p>
          WinToWar, teknik altyapının mümkün olan en yüksek erişilebilirlik ve güvenlik seviyesinde çalışması için
          gerekli önlemleri alır; ancak internet bağlantısı, blockchain ağı, üçüncü taraf ödeme hizmetleri veya
          kullanıcının cihazından kaynaklanan kesintilerden her durumda sorumlu tutulamaz.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">10. Değişiklikler</h2>
        <p>
          WinToWar, hizmetlerin veya bu Kullanım Şartları&apos;nın içeriğini gerektiğinde güncelleyebilir. Güncel
          şartlar platformda yayınlandığı tarihten itibaren geçerli olur.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-foreground">11. İletişim</h2>
        <p>Platform: WinToWar</p>
        <p>İşletmeci: [Şirket / kişi unvanı]</p>
        <p>E-posta: [destek e-postası]</p>
        <p>Adres: [yasal adres]</p>
      </section>

      <p className="text-xs">Son güncelleme: [Tarih]</p>
    </LegalPage>
  );
}
