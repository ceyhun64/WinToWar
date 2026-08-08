import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono, Inter } from "next/font/google";
import "./globals.css";
import { cn } from "@/lib/utils";
import { siteUrl } from "@/lib/metadata";

/** docs/12-seo.md Bölüm 9: next/font kullanımında `display: "swap"` — font yüklenene kadar sistem fontuyla render, FOIT engellenir. */
const inter = Inter({ subsets: ["latin"], variable: "--font-sans", display: "swap" });

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
  display: "swap",
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: {
    default: "WinToWar",
    template: "%s",
  },
  // not: template "%s" (no-op) — mevcut sayfalar (`/`, `/kurallar`) title'ı zaten
  // "X — WinToWar" biçiminde birebir yazıyor, template burada yalnızca title
  // vermeyen (noindex) sayfalara `default` fallback'i sağlamak için var.
  description: "Gerçek zamanlı, bölge ele geçirme temelli çok oyunculu strateji oyunu.",
  applicationName: "WinToWar",
  robots: { index: true, follow: true },
  openGraph: {
    type: "website",
    locale: "tr_TR",
    siteName: "WinToWar",
    title: "WinToWar",
    description: "Gerçek zamanlı, bölge ele geçirme temelli çok oyunculu strateji oyunu.",
    images: [{ url: "/og-image.png", width: 1200, height: 630, alt: "WinToWar" }],
  },
  twitter: {
    card: "summary_large_image",
    title: "WinToWar",
    description: "Gerçek zamanlı, bölge ele geçirme temelli çok oyunculu strateji oyunu.",
    images: ["/og-image.png"],
  },
};

export const viewport: Viewport = {
  themeColor: "#07111f",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="tr"
      className={cn("h-full", "antialiased", geistSans.variable, geistMono.variable, "font-sans", inter.variable)}
    >
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
