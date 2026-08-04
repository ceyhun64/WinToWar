import type { Metadata } from "next";
import Link from "next/link";
import { LandingCta } from "@/components/layout/LandingCta";

export const metadata: Metadata = {
  title: "WinToWar — Gerçek Zamanlı Bölge Ele Geçirme",
  description: "Bölge ele geçir, rakiplerini ele, kazandığın havuzu LTC olarak çek.",
};

/** docs/07-pages.md `/` Sayfa Detayları: kazanç mekaniği somut bir sayı değil, formülle anlatılır. */
export default function LandingPage() {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col items-center gap-8 px-4 py-16 text-center">
      <div className="flex flex-col gap-3">
        <h1 className="text-3xl font-semibold tracking-tight">WinToWar</h1>
        <p className="text-base text-muted-foreground">
          Gerçek zamanlı, bölge ele geçirme temelli çok oyunculu strateji oyunu. Rastgele bir
          başlangıç kalesi alırsın, askerlerini komşu bölgelere gönderip haritayı ele geçirirsin.
          Son ayakta kalan oyuncu kazanır.
        </p>
      </div>

      <div className="grid w-full grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="rounded-md border border-border bg-card p-4 text-left">
          <h2 className="text-sm font-semibold">Standart Oda</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            $1 giriş ücreti, 4 oyuncu. Kazanan havuzun %90&apos;ını alır.
          </p>
        </div>
        <div className="rounded-md border border-border bg-card p-4 text-left">
          <h2 className="text-sm font-semibold">VIP Oda</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Giriş ücretini ve oyuncu sayısını (2-12) sen belirlersin, şifreli/özel davetle
            kurabilirsin.
          </p>
        </div>
      </div>

      <div className="rounded-md border border-border bg-card p-4 text-left text-sm text-muted-foreground">
        <span className="font-medium text-foreground">Kazanç formülü:</span> Havuz = Giriş Ücreti ×
        Oyuncu Sayısı, Kazanç = Havuz × %90 (komisyon %10).
      </div>

      <LandingCta />

      <p className="text-xs text-muted-foreground">
        Ücretsiz denemek mi istiyorsun? <Link href="/kurallar" className="underline">Kuralları oku</Link>{" "}
        veya lobiden <span className="font-medium text-foreground">Pratik Oyna</span>&apos;yı dene.
      </p>
    </div>
  );
}
