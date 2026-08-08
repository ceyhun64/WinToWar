import type { Metadata } from "next";
import { canonicalFor } from "@/lib/metadata";

/**
 * docs/12-seo.md Bölüm 1: `/durum` herkese açık, indexlenmesi istenen bir
 * sayfa — ama `page.tsx` `"use client"` olduğu için `metadata` doğrudan
 * oradan export edilemez (bkz. `destek/layout.tsx` ile aynı desen).
 */
export const metadata: Metadata = {
  title: "Sistem Durumu — WinToWar",
  description: "WinToWar sistem durumu: API ve veritabanı bileşenlerinin canlı çalışma durumunu buradan takip edin.",
  ...canonicalFor("/durum"),
};

export default function DurumLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
