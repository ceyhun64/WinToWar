import type { Metadata } from "next";
import Link from "next/link";
import { HelpCircle } from "lucide-react";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { canonicalFor } from "@/lib/metadata";

export const metadata: Metadata = {
  title: "Sıkça Sorulan Sorular — WinToWar",
  description:
    "Yatırma/çekim süresi, komisyon hesaplama, maç iptali gibi sık sorulan sorular.",
  ...canonicalFor("/sss"),
};

const FAQ_ITEMS = [
  {
    question: "LTC yatırma ne kadar sürer?",
    answer:
      "Ödemeniz zincirde onaylandığında (regtest/testnet için 1 onay) bakiyeniz veya maça girişiniz otomatik olarak güncellenir. Bu genellikle birkaç dakika içinde gerçekleşir.",
  },
  {
    question: "Komisyon nasıl hesaplanıyor?",
    answer:
      "Havuz = Giriş Ücreti × Oyuncu Sayısı. Kazanan, havuzun %10 komisyon düşüldükten sonraki %90'ını alır.",
  },
  {
    question: "Maç iptal olursa param ne olur?",
    answer:
      "Lobi 5 dakika içinde dolmazsa otomatik iptal/iade yapılmaz — beklemeye devam edebilir ya da 'İptal Et' ile ödemenizi anında iade alabilirsiniz.",
  },
  {
    question: "Para çekme talebimin durumunu nereden görürüm?",
    answer:
      "/cuzdan sayfasındaki 'Para Çek' bölümünden en son talebinizin durumunu görebilirsiniz.",
  },
];

/**
 * docs/12-seo.md Bölüm 5 (v3.1): FAQPage JSON-LD, sayfada gösterilen
 * `FAQ_ITEMS`'tan programatik olarak türetilir — aynı soru/cevap metni HTML'e
 * bir kez, JSON-LD'ye elle ikinci bir kez yazılmaz (senkron dışı kalma riski
 * önlenir). Kullanıcıya görünmeyen, ekstra bir soru/cevap eklenmez.
 */
const faqJsonLd = {
  "@context": "https://schema.org",
  "@type": "FAQPage",
  mainEntity: FAQ_ITEMS.map((item) => ({
    "@type": "Question",
    name: item.question,
    acceptedAnswer: { "@type": "Answer", text: item.answer },
  })),
};

export default function SssPage() {
  return (
    <div className="mx-auto flex w-full min-h-0 max-w-7xl flex-1 flex-col gap-8 px-4 py-4">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(faqJsonLd) }}
      />
      <PageHero icon={HelpCircle} title="Sıkça Sorulan Sorular" />
        <CardContent className="flex h-full flex-col gap-6 overflow-y-auto">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {FAQ_ITEMS.map((item) => (
              <GameCard key={item.question}>
                <CardHeader>
                  <CardTitle>{item.question}</CardTitle>
                </CardHeader>
                <CardContent className="text-sm text-muted-foreground">
                  {item.answer}
                </CardContent>
              </GameCard>
            ))}
          </div>
          <p className="text-sm text-muted-foreground">
            Sorunun cevabını bulamadın mı?{" "}
            <Link href="/destek" className="underline">
              Destek ile iletişime geç
            </Link>
          </p>
        </CardContent>
    </div>
  );
}
