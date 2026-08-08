import type { Metadata } from "next";
import { noIndexFollowMetadata } from "@/lib/metadata";

/** docs/12-seo.md Bölüm 1 "Noindex ama erişilebilir" — bkz. `giris/layout.tsx` ile aynı gerekçe. Token'lı URL indekslenmez, canonical eklenmez. */
export const metadata: Metadata = {
  ...noIndexFollowMetadata,
  title: "Şifre Sıfırla — WinToWar",
  description: "WinToWar hesabınız için yeni bir şifre belirleyin.",
};

export default function SifreSifirlaLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
