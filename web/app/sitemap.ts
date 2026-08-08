import type { MetadataRoute } from "next";
import { siteUrl } from "@/lib/metadata";

/**
 * docs/12-seo.md Bölüm 2.2: yalnızca Bölüm 1'deki "Indexlenir" grubundaki
 * statik, public route'lar listelenir. `/giris`, `/kayit` vb. "Noindex ama
 * erişilebilir" grubu ve tüm auth'lu/dinamik route'lar burada **yer almaz**
 * (v1'de `/giris`/`/kayit` yanlışlıkla listelenmişti — düzeltildi).
 *
 * `priority`/`changeFrequency` yalnızca sitemap metadata'sıdır, Google'ın
 * güncel tutumu bunu bir sıralama faktörü olarak saymaz — bu yüzden statik
 * değerlerin ötesinde bir dinamik hesaplama altyapısı kurulmaz (YAGNI).
 *
 * `lastModified` hiçbir route için eklenmez: hiçbir sayfanın gerçek bir
 * değişiklik tarihi kaynağı (Git commit tarihi, bir CMS/DB alanı) şu an
 * bağlı değil — her build'de "bugün" gibi uydurma bir tarih basmak Google'a
 * yanlış bir "içerik güncellendi" sinyali verir.
 */
export default function sitemap(): MetadataRoute.Sitemap {
  return [
    { url: `${siteUrl}/`, changeFrequency: "daily", priority: 1 },
    { url: `${siteUrl}/kurallar`, changeFrequency: "weekly", priority: 0.6 },
    { url: `${siteUrl}/sss`, changeFrequency: "weekly", priority: 0.6 },
    { url: `${siteUrl}/destek`, changeFrequency: "weekly", priority: 0.6 },
    { url: `${siteUrl}/kosullar`, changeFrequency: "monthly", priority: 0.3 },
    { url: `${siteUrl}/gizlilik`, changeFrequency: "monthly", priority: 0.3 },
    { url: `${siteUrl}/sorumlu-oyun`, changeFrequency: "monthly", priority: 0.3 },
    { url: `${siteUrl}/cerezler`, changeFrequency: "monthly", priority: 0.3 },
    { url: `${siteUrl}/durum`, changeFrequency: "hourly", priority: 0.2 },
  ];
}
