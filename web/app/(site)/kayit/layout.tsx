import type { Metadata } from "next";
import { canonicalFor, noIndexFollowMetadata } from "@/lib/metadata";

/** docs/12-seo.md Bölüm 1 "Noindex ama erişilebilir" — bkz. `giris/layout.tsx` ile aynı gerekçe. */
export const metadata: Metadata = {
  ...noIndexFollowMetadata,
  title: "Kayıt Ol — WinToWar",
  description: "WinToWar'a ücretsiz kayıt olun, birkaç saniyede hesabınızı oluşturun ve oyunlara katılın.",
  ...canonicalFor("/kayit"),
};

export default function KayitLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
