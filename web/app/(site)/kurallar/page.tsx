import type { Metadata } from "next";
import {
  BookOpen,
  Map,
  Zap,
  Swords,
  DoorOpen,
  Coins,
  ShieldOff,
  FlaskConical,
} from "lucide-react";
import { PageHero } from "@/components/layout/PageHero";
import { IllustrationPanel } from "@/components/layout/IllustrationPanel";
import { GameCard } from "@/components/layout/GameCard";
import { CardContent } from "@/components/ui/card";
import { canonicalFor } from "@/lib/metadata";

export const metadata: Metadata = {
  title: "Kurallar — WinToWar",
  description:
    "WinToWar oyun kuralları: üretim, savaş, oda türleri ve kazanç dağılımı.",
  ...canonicalFor("/kurallar"),
};

/**
 * docs/07-pages.md `/kurallar`: müşterinin verdiği sayılar birebir yansıtılır,
 * değiştirilmez (bkz. `01-workflow-rules.md` Bölüm 0.5). Alttaki "Dene" bölümü
 * docs/03-game-rules.md Bölüm 7'deki botsuz tanıtım akışının somut yeridir.
 *
 * `docs/04-style.md` Landing İstisnası — sitede genelleştirilmiş tasarım
 * sistemi: "wiki değil, oyun rehberi" hissi — her kural bloğu ikon rozetli,
 * ızgarada dizilen bir `IllustrationPanel` kartı. Metinlerin tek harfi bile
 * değişmedi, yalnızca sunum kart tabanlı hale geldi.
 */
const RULE_SECTIONS = [
  {
    icon: Map,
    title: "Harita ve Başlangıç",
    accent: "#38BDF8",
    body: "Harita 12 şehir/bölgeden oluşur (Lüksemburg haritası). Her maç başında katılan her oyuncuya haritadaki bölgelerden rastgele biri başlangıç kalesi olarak atanır — hiçbir oyuncu sabit/tahmin edilebilir bir avantajla başlamaz. Kalan bölgeler sahipsiz (gri) başlar. Saldırı yalnızca doğrudan komşu bölgeye yapılabilir.",
  },
  {
    icon: Zap,
    title: "Üretim",
    accent: "#38BDF8",
    body: "Ana kaleniz her 10 saniyede 4 asker üretir. Ele geçirdiğiniz her 1 bölge, üretiminize +1 asker/10sn ekler. Örneğin 4 bölgeniz varsa (ev kalesi hariç), 10 saniyede 4 + 4 = 8 asker üretirsiniz. Asker üretimi tamamen otomatiktir, elle tetiklenmez.",
  },
  {
    icon: Swords,
    title: '"+2 Asker Kuralı"',
    accent: "#F2495C",
    body: "Saldıran ve savunan asker sayıları birebir eşleşip elenir. Bir bölgeyi ele geçirmek için savunma tamamen sıfırlanmalı ve en az 1 saldıran asker bölgede nöbetçi (garnizon) olarak kalmalıdır. Varsayılan gri bölge savunması 1 asker olduğundan: 1 bölge almak için en az 2 asker (1'i savunmayı kırar, 1'i garnizon kalır), 2 bölge zincirleme almak için en az 4 asker gerekir. Saldırı yetersizse (gönderilen asker savunmayı aşamazsa) saldıran tüm gönderdiği askerleri kaybeder, savunan taraf gönderilen kadar kayıp verir.",
  },
  {
    icon: DoorOpen,
    title: "Oda Türleri",
    accent: "#F5B942",
    body: "Standart: sabit $1 giriş ücreti, 4 oyuncu, gri bölge savunması sabit 1, açık harita. VIP: kurucu giriş ücretini, oyuncu sayısını (2-12), gri bölge savunmasını (1-7) ve Fog of War açık/kapalı olduğunu belirler; şifreli/özel davet ile kurulabilir. Pratik: tamamen ücretsiz, kazanç/kayıp yoktur.",
  },
  {
    icon: Coins,
    title: "Kazanç Dağılımı",
    accent: "#F5B942",
    body: "Havuz = Giriş Ücreti × Oyuncu Sayısı. Kazanan, havuzun %10 komisyon düşüldükten sonraki %90'ını LTC olarak otomatik alır. Eşzamanlı eleme (son iki oyuncunun aynı anda elenmesi) durumunda havuz ortak kazananlar arasında eşit bölünür.",
  },
  {
    icon: ShieldOff,
    title: "Bot Politikası",
    accent: "#94A3B8",
    body: 'Sistemde hiçbir zaman bot/yapay oyuncu bulunmaz. Lobi zaman aşımı (Standart/VIP\'te 5 dakika) dolduğunda otomatik iptal/iade yoktur — bekleyen oyuncuya "İptal Et / Bakiyeyi İade Et" ya da "Beklemeye Devam Et" seçimi sunulur.',
  },
];

export default function KurallarPage() {
  return (
    <div className="mx-auto flex w-full min-h-0 max-w-7xl flex-1 flex-col gap-8 px-4 py-4">
      <PageHero
        icon={BookOpen}
        title="Kurallar"
        subtitle="Gerçek zamanlı, bölge ele geçirme temelli çok oyunculu strateji oyunu."
      />

        <CardContent className="h-full overflow-y-auto">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {RULE_SECTIONS.map((section) => (
              <IllustrationPanel
                key={section.title}
                icon={section.icon}
                accent={section.accent}
              >
                <h2 className="mb-2 flex items-center gap-2 text-lg font-semibold">
                  <section.icon
                    className="size-4 shrink-0"
                    style={{ color: section.accent }}
                    aria-hidden="true"
                  />
                  {section.title}
                </h2>
                <p className="text-sm text-muted-foreground">{section.body}</p>
              </IllustrationPanel>
            ))}
          </div>
        </CardContent>
    </div>
  );
}
