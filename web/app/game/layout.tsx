import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

/**
 * docs/07-pages.md "Layout Yapısı": GameLayout — Header/Footer içermez (oyun
 * ekranı sade kalmalı), yalnızca minimal bağlantı durumu göstergesi taşır.
 * Bağlantı durumu göstergesinin kendisi `app/game/[matchId]/page.tsx` içinde
 * render edilir (SignalR store hook'u sayfanın kendisinde yaşıyor) — bu layout
 * yalnızca Header/Footer'sız kabuğu ve noindex metadata'yı sağlar.
 */
export default function GameLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
