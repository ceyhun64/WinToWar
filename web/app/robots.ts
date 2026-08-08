import type { MetadataRoute } from "next";
import { siteUrl } from "@/lib/metadata";

/**
 * docs/12-seo.md Bölüm 2.1 (v2 düzeltmesi): hiçbir route burada `Disallow`
 * edilmez. `robots.txt` ile Google'ın crawl etmesi engellenirse, sayfanın
 * içindeki `noindex` meta etiketi (bkz. `lib/metadata.ts`) de görülemez —
 * "engellenen" sayfa yine de başlıksız bir URL satırı olarak arama
 * sonuçlarında görünebilir. İndeksleme kararı yalnızca sayfa-bazlı `robots`
 * meta etiketine bırakılır; `robots.txt` burada yalnızca crawl bütçesi/sitemap
 * bildirimi için kullanılır.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
    },
    sitemap: `${siteUrl}/sitemap.xml`,
  };
}
