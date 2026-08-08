import type { Metadata } from "next";
import { canonicalFor } from "@/lib/metadata";

/**
 * docs/12-seo.md Bölüm 1: `/destek` herkese açık, indexlenmesi istenen bir
 * sayfa — ama `page.tsx` `"use client"` olduğu için `metadata` doğrudan
 * oradan export edilemez (bkz. lobi/cuzdan vb. için zaten kurulmuş aynı desen).
 */
export const metadata: Metadata = {
  title: "Destek — WinToWar",
  description: "WinToWar Destek: sorularınız, hesap veya ödeme anlaşmazlıklarınız için destek ekibiyle iletişime geçin.",
  ...canonicalFor("/destek"),
};

export default function DestekLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
