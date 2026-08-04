import type { Metadata } from "next";
import { PracticeDemo } from "@/components/rules/PracticeDemo";

export const metadata: Metadata = {
  title: "Kurallar — WinToWar",
  description: "WinToWar oyun kuralları: üretim, savaş, oda türleri ve kazanç dağılımı.",
};

/**
 * docs/07-pages.md `/kurallar`: müşterinin verdiği sayılar birebir yansıtılır,
 * değiştirilmez (bkz. `01-workflow-rules.md` Bölüm 0.5). Alttaki "Dene" bölümü
 * docs/03-game-rules.md Bölüm 7'deki botsuz tanıtım akışının somut yeridir.
 */
export default function KurallarPage() {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-8 px-4 py-8">
      <div>
        <h1 className="text-2xl font-semibold">Kurallar</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Gerçek zamanlı, bölge ele geçirme temelli çok oyunculu strateji oyunu.
        </p>
      </div>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Harita ve Başlangıç</h2>
        <p className="text-sm text-muted-foreground">
          Harita 12 şehir/bölgeden oluşur (Lüksemburg haritası). Her maç başında katılan her
          oyuncuya haritadaki bölgelerden rastgele biri başlangıç kalesi olarak atanır — hiçbir
          oyuncu sabit/tahmin edilebilir bir avantajla başlamaz. Kalan bölgeler sahipsiz (gri)
          başlar. Saldırı yalnızca doğrudan komşu bölgeye yapılabilir.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Üretim</h2>
        <p className="text-sm text-muted-foreground">
          Ana kaleniz her 10 saniyede 4 asker üretir. Ele geçirdiğiniz her 1 bölge, üretiminize
          +1 asker/10sn ekler. Örneğin 4 bölgeniz varsa (ev kalesi hariç), 10 saniyede 4 + 4 = 8
          asker üretirsiniz. Asker üretimi tamamen otomatiktir, elle tetiklenmez.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Savaş — &quot;+2 Asker Kuralı&quot;</h2>
        <p className="text-sm text-muted-foreground">
          Saldıran ve savunan asker sayıları birebir eşleşip elenir. Bir bölgeyi ele geçirmek için
          savunma tamamen sıfırlanmalı ve en az 1 saldıran asker bölgede nöbetçi (garnizon) olarak
          kalmalıdır. Varsayılan gri bölge savunması 1 asker olduğundan: <strong>1 bölge almak
          için en az 2 asker</strong> (1&apos;i savunmayı kırar, 1&apos;i garnizon kalır),{" "}
          <strong>2 bölge zincirleme almak için en az 4 asker</strong> gerekir. Saldırı
          yetersizse (gönderilen asker savunmayı aşamazsa) saldıran tüm gönderdiği askerleri
          kaybeder, savunan taraf gönderilen kadar kayıp verir.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Oda Türleri</h2>
        <p className="text-sm text-muted-foreground">
          <strong>Standart:</strong> sabit $1 giriş ücreti, 4 oyuncu, gri bölge savunması sabit 1,
          açık harita. <strong>VIP:</strong> kurucu giriş ücretini, oyuncu sayısını (2-12), gri
          bölge savunmasını (1-7) ve Fog of War açık/kapalı olduğunu belirler; şifreli/özel davet
          ile kurulabilir. <strong>Pratik:</strong> tamamen ücretsiz, kazanç/kayıp yoktur.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Kazanç Dağılımı</h2>
        <p className="text-sm text-muted-foreground">
          Havuz = Giriş Ücreti × Oyuncu Sayısı. Kazanan, havuzun %10 komisyon düşüldükten sonraki
          %90&apos;ını LTC olarak otomatik alır. Eşzamanlı eleme (son iki oyuncunun aynı anda
          elenmesi) durumunda havuz ortak kazananlar arasında eşit bölünür.
        </p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Bot Politikası</h2>
        <p className="text-sm text-muted-foreground">
          Sistemde hiçbir zaman bot/yapay oyuncu bulunmaz. Lobi zaman aşımı (Standart/VIP&apos;te
          5 dakika) dolduğunda otomatik iptal/iade yoktur — bekleyen oyuncuya &quot;İptal Et /
          Bakiyeyi İade Et&quot; ya da &quot;Beklemeye Devam Et&quot; seçimi sunulur.
        </p>
      </section>

      <section className="flex flex-col gap-3">
        <div>
          <h2 className="text-lg font-semibold">Dene</h2>
          <p className="text-sm text-muted-foreground">
            Gerçek bir maça hiç bağlanmayan, sabit senaryolu bir gösterim — mekanikleri risksiz
            deneyin.
          </p>
        </div>
        <PracticeDemo />
      </section>
    </div>
  );
}
