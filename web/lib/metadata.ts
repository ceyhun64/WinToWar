import type { Metadata } from "next";

/**
 * docs/12-seo.md Bölüm 3 + Bölüm 6 (tek kaynak ilkesi): `NEXT_PUBLIC_SITE_URL`
 * canonical/sitemap/robots/OG URL'lerinin tamamının türediği tek kaynaktır.
 * Production build'de tanımlı değilse build **başarısız olur** — sessiz bir
 * `localhost` fallback'iyle yanlış bir domain'le canonical/OG üretilip
 * production'a çıkılmasına izin verilmez. Development'ta güvenli bir fallback
 * kullanılır. `robots.ts`/`sitemap.ts`/`layout.tsx` kendi kopyasını hesaplamaz,
 * hepsi bu tek değeri import eder (Bölüm 10 kabul kriteri: "tek bir yerden
 * yönetiliyor").
 */
function resolveSiteUrl(): string {
  const value = process.env.NEXT_PUBLIC_SITE_URL;
  if (value) return value;
  if (process.env.NODE_ENV === "production") {
    throw new Error(
      "NEXT_PUBLIC_SITE_URL tanımlı değil. Production build için zorunludur " +
        "(docs/12-seo.md Bölüm 3) — canonical/sitemap/robots/OG URL'leri bu değerden türer."
    );
  }
  return "http://localhost:3000";
}

export const siteUrl = resolveSiteUrl();

/** docs/12-seo.md Bölüm 6: `siteUrl` üzerinden yalnızca mutlak URL birleştirmesi — host/protokol ayrıca normalize edilmez. */
export function absoluteUrl(path: string): string {
  return new URL(path, siteUrl).toString();
}

/**
 * docs/12-seo.md Bölüm 6: canonical her zaman sayfanın temel (parametresiz)
 * URL'idir — tracking/filtre/davet query parametreleri canonical'ı değiştirmez,
 * çünkü `path` zaten parametresiz verilir.
 */
export function canonicalFor(path: string): Pick<Metadata, "alternates"> {
  return { alternates: { canonical: absoluteUrl(path) } };
}

/** docs/07-pages.md#Metadata/SEO: auth gerektiren, bakiye/oyun verisi taşıyan sayfalar — indexlenmez, linkleri de takip edilmez. */
export const noIndexMetadata: Metadata = {
  robots: { index: false, follow: false },
};

/**
 * docs/12-seo.md Bölüm 1 "Noindex ama erişilebilir": auth gerektirmeyen ama
 * form/işlevsel sayfalar (`/giris`, `/kayit`, `/sifremi-unuttum`,
 * `/sifre-sifirla/[token]`, `/bakim`) — arama sonucunda görünmesi hedef
 * kitleye değer katmaz ama sayfadaki linkler yine de takip edilebilir
 * (`noIndexMetadata`'dan farkı: `follow: true`).
 */
export const noIndexFollowMetadata: Metadata = {
  robots: { index: false, follow: true },
};
