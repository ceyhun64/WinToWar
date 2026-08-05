import type { Metadata } from "next";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { LandingCta } from "@/components/layout/LandingCta";

export const metadata: Metadata = {
  title: "WinToWar — Gerçek Zamanlı Bölge Ele Geçirme",
  description: "Bölge ele geçir, rakiplerini ele, kazandığın havuzu LTC olarak çek.",
};

const HOW_TO_PLAY_STEPS = [
  { title: "Katıl", description: "Standart, VIP veya ücretsiz Pratik'ten birini seç." },
  { title: "Bölge Fethet", description: "Askerlerini komşu bölgelere gönder, haritayı ele geçir." },
  { title: "Kazan", description: "Son ayakta kalan oyuncu havuzu LTC olarak alır." },
];

/**
 * docs/08-page-content.md Bölüm 3.1: Katman 1 = tek cümlelik tanım + tek CTA.
 * Katman 2 = kazanç formülü + 3 adımlık "nasıl oynanır" özeti. Oda türleri
 * (Standart/VIP) farkı burada **anlatılmaz** — bu bilgi zaten `/lobi`'de
 * görselleşiyor, tekrarı Bölüm 1.3'teki "gereksiz bilgi" testine takılır.
 */
export default function LandingPage() {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col items-center gap-8 px-4 py-16 text-center">
      <div className="flex flex-col gap-3">
        <h1 className="text-3xl font-semibold tracking-tight">WinToWar</h1>
        <p className="text-base text-muted-foreground">
          Gerçek zamanlı, bölge ele geçirme temelli çok oyunculu strateji oyunu, gerçek parayla.
          Rastgele bir başlangıç kalesi alırsın, askerlerini komşu bölgelere gönderip haritayı
          ele geçirirsin. Son ayakta kalan oyuncu kazanır.
        </p>
      </div>

      <LandingCta />

      <div className="grid w-full grid-cols-1 gap-4 sm:grid-cols-3">
        {HOW_TO_PLAY_STEPS.map((step, i) => (
          <Card key={step.title} className="text-left">
            <CardHeader>
              <CardTitle>
                {i + 1}. {step.title}
              </CardTitle>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground">{step.description}</CardContent>
          </Card>
        ))}
      </div>

      <Card className="w-full text-left">
        <CardContent className="text-sm text-muted-foreground">
          <span className="font-medium text-foreground">Kazanç formülü:</span> Havuz = Giriş Ücreti ×
          Oyuncu Sayısı, Kazanç = Havuz × %90 (komisyon %10).
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Ücretsiz denemek mi istiyorsun? <Link href="/kurallar" className="underline">Kuralları oku</Link>{" "}
        veya lobiden <span className="font-medium text-foreground">Pratik Oyna</span>&apos;yı dene.
      </p>
    </div>
  );
}
