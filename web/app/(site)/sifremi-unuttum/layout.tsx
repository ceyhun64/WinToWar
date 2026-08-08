import type { Metadata } from "next";
import { canonicalFor, noIndexFollowMetadata } from "@/lib/metadata";

/** docs/12-seo.md Bölüm 1 "Noindex ama erişilebilir" — bkz. `giris/layout.tsx` ile aynı gerekçe. */
export const metadata: Metadata = {
  ...noIndexFollowMetadata,
  title: "Şifremi Unuttum — WinToWar",
  description: "WinToWar hesap erişiminizi kaybettiyseniz şifrenizi e-posta ile sıfırlayın.",
  ...canonicalFor("/sifremi-unuttum"),
};

export default function SifremiUnuttumLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
