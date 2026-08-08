import type { Metadata } from "next";
import { canonicalFor, noIndexFollowMetadata } from "@/lib/metadata";

/**
 * docs/12-seo.md Bölüm 1 "Noindex ama erişilebilir": auth gerektirmiyor ama
 * form/işlevsel bir sayfa — arama sonucunda görünmesi hedef kitleye değer
 * katmaz, ama linkleri (ör. `/kayit`, `/sifremi-unuttum`) takip edilebilir.
 */
export const metadata: Metadata = {
  ...noIndexFollowMetadata,
  title: "Giriş — WinToWar",
  description: "WinToWar hesabınıza giriş yapın ve maçlara katılmaya devam edin.",
  ...canonicalFor("/giris"),
};

export default function GirisLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
