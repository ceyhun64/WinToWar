import type { Metadata } from "next";
import { Landing } from "@/components/landing/Landing";
import { absoluteUrl, canonicalFor, siteUrl } from "@/lib/metadata";

export const metadata: Metadata = {
  title: "WinToWar — Gerçek Zamanlı Bölge Ele Geçirme",
  description: "Bölge ele geçir, rakiplerini ele, kazandığın havuzu LTC olarak çek.",
  ...canonicalFor("/"),
};

/**
 * docs/12-seo.md Bölüm 5: yalnızca projede doğrulanabilen alanlar dolduruluyor
 * (`name`, `url`, ve projede gerçekten kullanılan marka görseli `logo`).
 * Şirket unvanı/adres/telefon/sosyal medya hesabı gibi kaynağı olmayan hiçbir
 * alan icat edilmez.
 */
const organizationJsonLd = {
  "@context": "https://schema.org",
  "@type": "Organization",
  name: "WinToWar",
  url: siteUrl,
  logo: absoluteUrl("/logo/logo.png"),
};

const websiteJsonLd = {
  "@context": "https://schema.org",
  "@type": "WebSite",
  name: "WinToWar",
  url: siteUrl,
};

export default function LandingPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationJsonLd) }}
      />
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(websiteJsonLd) }}
      />
      <Landing />
    </>
  );
}
