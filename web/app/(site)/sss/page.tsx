import type { Metadata } from "next";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = {
  title: "Sıkça Sorulan Sorular — WinToWar",
  description: "Yatırma/çekim süresi, komisyon hesaplama, maç iptali gibi sık sorulan sorular.",
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
    answer: "/cuzdan sayfasındaki 'Para Çek' bölümünden en son talebinizin durumunu görebilirsiniz.",
  },
];

export default function SssPage() {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-6 px-4 py-8">
      <h1 className="text-2xl font-semibold">Sıkça Sorulan Sorular</h1>
      <div className="flex flex-col gap-4">
        {FAQ_ITEMS.map((item) => (
          <Card key={item.question}>
            <CardHeader>
              <CardTitle>{item.question}</CardTitle>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground">{item.answer}</CardContent>
          </Card>
        ))}
      </div>
      <p className="text-sm text-muted-foreground">
        Sorunun cevabını bulamadın mı? <Link href="/destek" className="underline">Destek ile iletişime geç</Link>
      </p>
    </div>
  );
}
