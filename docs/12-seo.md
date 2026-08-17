# Claude Code Görev Talimatı: SEO (Arama Motoru Optimizasyonu) — v3.1

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla ilerle, kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ⚙️ **ÇALIŞMA DAVRANIŞI KURALLARI:** Süreç kuralları.
- ❓ Müşteriden ileride doğrulanması gereken nokta — asla "dur ve sor" anlamına gelmez, yanında zaten uygulanmış bir 🛠️ taşır.
- 🚩 Mühendislik kararı değil, **hukuki/iş kararı** — Claude bunu kendi başına çözemez, yalnızca görünür kılar (bkz. `05-payment.md` Bölüm 10.1'deki aynı kullanım).

> ⚠️ **Bu doküman neden var:** `07-pages.md`'de zaten kısa bir "Metadata / SEO" bölümü var (yalnızca `/` ve `/kurallar` için `generateMetadata`, auth'lu sayfalarda `robots: noindex`). Bu doküman onu **geçersiz kılmaz, üzerine inşa eder.**
>
> ⚠️ **v2 — harici teknik SEO incelemesi sonrası düzeltmeler:** v1'de **`robots.txt` ile sayfa-bazlı `noindex` meta etiketi aynı route'lara birlikte uygulanıyordu** — bu, Google'ın kendi resmi rehberliğine göre bir **çelişkidir**: bir URL `robots.txt` ile `Disallow` edilirse Google o sayfayı **crawl edemez**, dolayısıyla sayfanın içindeki `noindex` etiketini de **göremez**; sonuç olarak "engellenen" sayfa yine de yalnızca URL'i (başlıksız/açıklamasız bir satır olarak) arama sonuçlarında görünebilir — v1'in "savunma derinliği" gerekçesiyle önerdiği "ikisi birden" yaklaşımı aslında amacının **tersini** yapıyordu. v2 bunu Bölüm 2.1'de düzeltiyor. Ayrıca canonical/URL normalizasyonu, structured data'da bilgi uydurmama kuralı, internal linking, ve SEO görevinin diğer modüllere (auth/ödeme/oyun) sızmaması için açık bir kapsam sınırı eklendi (Bölüm 11).
>
> ⚠️ **v3 — ikinci incelemeden 5 düzeltme:** (1) Private/auth sayfalarda OG üretilmemesi bir **teknik SEO zorunluluğu değil, ürün kararı** olarak yeniden çerçevelendi (Bölüm 4). (2) Canonical/URL üretiminde `NEXT_PUBLIC_SITE_URL` **tek kaynak** kabul edildi — kod ayrıca protokol/host normalize etmez (Bölüm 6). (3) `priority`/`changeFrequency`'ye gereğinden fazla mühendislik zamanı harcanmaması için açık bir uyarı eklendi (Bölüm 2.2). (4) Görev sonu raporunda **local/build-time doğrulama** ile **production/Search Console doğrulaması** kesin çizgiyle ayrıldı — production verisi yoksa "doğrulandı" değil "doğrulanamadı" yazılır (Bölüm 10). (5) Proje kökünün `web/app/` olduğu netleştirildi; tüm dosya yolları `web/app/robots.ts`, `web/app/sitemap.ts` şeklinde düzeltildi, kök dizinde ikinci bir `app/` klasörü açılmaması açıkça yasaklandı.
>
> ⚠️ **v3.1 — üçüncü incelemeden 4 mikro düzeltme (son rötuş, doküman burada büyütülmüyor):** (1) `noindex`'in "garanti" ettiği ifadesi kaldırıldı, "temel mekanizma" olarak düzeltildi (Bölüm 2.1). (2) Bölüm 10'daki canonical kabul kriteri, Bölüm 6'daki `NEXT_PUBLIC_SITE_URL` tek-kaynak ilkesiyle hizalandı. (3) Lighthouse ölçülemiyorsa "tahmin edilmez, araç/ortam kısıtı olarak raporlanır" kuralı eklendi (Bölüm 9.1). (4) FAQ JSON-LD'nin görünür HTML içeriğinden programatik türetilmesi, aynı içeriğin elle iki kez yazılmaması netleştirildi (Bölüm 5).

Bu görev, sayfa **içeriğini** (`08-page-content.md`'nin kapsamı) yeniden yazmaz; yalnızca teknik/meta katmanı ekler. Yasal sayfaların metin içeriği bu görevin kapsamında değildir.

---

## 0. ÇALIŞMA DAVRANIŞI KURALLARI ⚙️

### 0.0 Önce mevcut sistemi analiz et — tekrar üretme

Kod yazmadan önce şunları aç ve oku:

- `web/lib/metadata.ts` — mevcut yardımcı dosya; **ikinci bir paralel metadata sistemi kurma**, bu dosyayı genişlet.
- `web/app/layout.tsx` (`RootLayout`) — `<html lang>`, favicon, varsayılan metadata şu an nerede tanımlı.
- `web/public/logo/`, `web/public/` altındaki **mevcut görseller** — Bölüm 4'te yeni bir OG görseli oluşturmadan önce burada uygun bir marka görseli olup olmadığını kontrol et; varsa onu kullan, **duplicate bir görsel üretme**.
- `07-pages.md`'nin "Metadata / SEO" ve "Route Tablosu" bölümleri.
- `web/app/` altındaki mevcut sayfa dosyaları — `generateMetadata` zaten tanımlı olanları tekrar yazma.
- `web/app/robots.ts`, `web/app/sitemap.ts` var mı — `project_tree.txt`'ye göre şu an yok, bu görevde eklenecek.

### 0.1 Sıra

1. `web/lib/metadata.ts` analizi + genişletme (Bölüm 3).
2. `web/app/robots.ts` (Bölüm 2.1 — düzeltilmiş strateji).
3. `web/app/sitemap.ts` (Bölüm 2.2).
4. Route-bazlı `generateMetadata` tamamlama (Bölüm 1).
5. Canonical/URL normalizasyonu (Bölüm 6).
6. OG/Twitter Card (Bölüm 4).
7. JSON-LD structured data (Bölüm 5).
8. Internal linking denetimi (Bölüm 7).
9. Semantic/accessibility minimum kontrolü (Bölüm 8).
10. Performans/Core Web Vitals (Bölüm 9).
11. Test + ölçüm + build (Bölüm 10).

### 0.2 Ana projedeki kurallar geçerli

`CLAUDE.md` / `01-workflow-rules.md` / `06-coding-standards.md` aynen geçerlidir. Bu görev **hiçbir sayfanın metnini/kopyasını** değiştirmez — yalnızca `<head>` meta katmanını ve teknik SEO altyapısını ekler.

---

## 1. INDEX / NOINDEX POLİTİKASI — TAM ROUTE MATRİSİ

| Kategori | Route'lar | Politika | Gerekçe |
|---|---|---|---|
| **Indexlenir** | `/`, `/kurallar`, `/sss`, `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`, `/destek`, `/durum` | `index: true, follow: true` | Herkese açık, içerik taşıyan sayfalar. |
| **Noindex ama erişilebilir** | `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`, `/bakim` | `index: false, follow: true` | Auth gerektirmiyor ama form/işlevsel sayfalar; arama sonuçlarında görünmesi hedef kitleye değer katmaz. |
| **Noindex, nofollow** | `/lobi`, `/lobi/vip-olustur`, `/lobi/[inviteToken]`, `/cuzdan`, `/profil`, `/hesap-ayarlari`, `/gecmis`, `/mac/[matchId]`, `/odeme/[invoiceId]`, `/game/[matchId]`, `/admin`, `/admin/*` | `index: false, follow: false` | Gerçek para/bakiye/davet token'ı içeren hiçbir sayfa indexlenmez veya oradan link takip edilmez. |
| **Zaten `not-found`/`error`** | `not-found.tsx`, `error.tsx` | Next.js varsayılanı (`404`/`500` HTTP status ile) | Bölüm 10'da doğrulanır. |

🛠️ **v2 — düzeltildi:** Bu tablodaki **hiçbir** route `robots.txt` ile `Disallow` edilmez (bkz. Bölüm 2.1) — indexleme kararı **yalnızca** sayfa-bazlı `robots` meta etiketiyle verilir, Google bu sayfaları crawl edebilmeli ki `noindex` sinyalini görebilsin.

---

## 2. `robots.txt` VE `sitemap.xml`

### 2.1 `web/app/robots.ts` — 🛠️ v2'de stratejisi değişti

> ⚠️ **v3 — yol netleştirmesi:** Bu projenin Next.js App Router kökü `web/app/`'dir (`api/` kardeş dizinidir, root değil). `robots.ts`, `sitemap.ts` ve bu görevde eklenen tüm route dosyaları **`web/app/` altında** oluşturulur. Proje köküne (`web/`'in dışına) **ikinci bir `app/` klasörü kesinlikle açılmaz.**

> ⚠️ **v1'deki hata:** Bölüm 1'deki "Noindex ama erişilebilir" ve "Noindex, nofollow" gruplarını `robots.txt`'te de `Disallow` etmek — bu, Google'ın sayfayı crawl edip `noindex` meta etiketini okumasını **engeller**, sonuç olarak sayfa hâlâ (başlıksız bir URL satırı olarak) arama sonuçlarında görünebilir. `robots.txt` bir **gizlilik/erişim kontrol mekanizması değildir** (herkese açık, herkes okuyabilir) — yalnızca crawl bütçesini yönetmek için kullanılır.

- 🛠️ **v2 karar:** `robots.txt`'te **hiçbir route `Disallow` edilmez.** Tüm indexleme kararı Bölüm 1'deki sayfa-bazlı `robots` meta etiketine bırakılır — bu, Google'a bir sayfanın indexlenmemesi gerektiğini bildirmek için kullanılan **temel mekanizmadır** (🛠️ v3.1: "garanti" ifadesi kaldırıldı — `noindex` Google'ın sinyali doğru okumasına bağlıdır, mutlak bir garanti değildir).
- `robots.txt` yalnızca şunu içerir: `User-agent: *`, `Allow: /` (varsayılan zaten izin verir, satır opsiyonel ama açıklık için eklenir), `Sitemap: <mutlak URL>`.
- ❓ `/lobi/[inviteToken]` özel bir durumdur — davet token'ı URL'de taşındığı için asıl risk SEO değil, **token sızıntısı** (referrer header, tarayıcı geçmişi, sunucu logları, üçüncü parti analytics). Bu, `robots.txt`/`noindex` ile çözülecek bir SEO sorunu değildir; Claude Code görev sırasında mevcut `RoomService`/ilgili route handler'ın token'ı referrer/log/analytics'e sızdırıp sızdırmadığını **inceler** ve bulgusunu rapora yazar — bu görev kapsamında bir güvenlik değişikliği **yapılmaz** (kapsam sınırı, bkz. Bölüm 11), yalnızca tespit edilir.

### 2.2 `web/app/sitemap.ts`

Yalnızca Bölüm 1'deki "Indexlenir" grubundaki statik route'lar listelenir.

- `changeFrequency`/`priority`: v1'deki değerler korunur (`/` → `daily`/`1.0`, yasal sayfalar → `monthly`/`0.3`, diğerleri → `weekly`/`0.6`, `/durum` → `hourly`/`0.2`). 🛠️ **v3'te uyarı eklendi:** Bu iki alan yalnızca sitemap metadata'sıdır — Google'ın güncel resmi tutumu `priority`/`changeFrequency`'yi sıralama faktörü olarak **saymadığını** belirtir. Bu alanlar için statik değerlerin ötesinde bir dinamik altyapı (ör. gerçek trafik/değişiklik verisine göre otomatik hesaplama) **kurulmaz** — zaman/karmaşıklık maliyeti, olası SEO faydasını haklı çıkarmaz (YAGNI).
- 🛠️ **`lastModified` — v2'de eklendi:** Yalnızca sayfanın **gerçek** bir değişiklik tarihi kaynaktan (ör. Git commit tarihi, bir CMS/DB alanı) belirlenebiliyorsa eklenir. Böyle bir kaynak yoksa `lastModified` **tamamen atlanır** — her build'de "bugün" gibi uydurma bir tarih basılmaz; bu, Google'a yanlış bir "içerik güncellendi" sinyali verir ve güven kaybına yol açar.
- Dinamik/private route'lar sitemap'e hiç eklenmez.

---

## 3. `web/lib/metadata.ts` — ORTAK YARDIMCI

- **Title template:** `%s | WinToWar`, `/` için `08-page-content.md`'deki mevcut landing metninden türetilen özet.
- 🛠️ **`metadataBase` — v2'de production güvencesi eklendi:** `new URL(process.env.NEXT_PUBLIC_SITE_URL)`. **Production build'de bu environment variable tanımlı değilse build başarısız olur** (açık bir hata fırlatılır) — sessiz bir fallback (`localhost` vb.) ile production'a çıkılmasına **izin verilmez**, çünkü canonical/sitemap/OG URL'lerinin tamamı bu değerden türer; yanlış bir domain'le üretilmiş canonical/OG etiketleri SEO'yu görünmez şekilde bozar. Development ortamında (`NODE_ENV !== "production"`) `http://localhost:3000` gibi güvenli bir fallback kullanılabilir.
- `description`: 120-160 karakter, gerçek `08-page-content.md` içeriğinden türetilir.
- `robots`: Bölüm 1'deki matrise göre.
- `alternates.canonical`: Bölüm 6.

---

## 4. OPEN GRAPH / TWITTER CARD

- 🛠️ **Önce mevcut asset'leri kontrol et (Bölüm 0.0):** `web/public/logo/` ve `public/` altında OG için uygun bir görsel varsa **onu kullan**; yalnızca uygun bir asset gerçekten yoksa yeni `public/og/default.png` (1200×630) oluşturulur. Var olan bir görseli göz ardı edip ikinci bir tane üretmek yasaktır (`01-workflow-rules.md` Bölüm 0.2 ruhuyla tutarlı — gereksiz duplicate dosya).
- `og:type` = `website`, `og:locale` = `tr_TR`, `twitter:card` = `summary_large_image`.
- 🛠️ **v3'te netleştirildi:** Bölüm 1'in noindex/nofollow grubunda OG/Twitter metadata'sı **teknik bir SEO zorunluluğu olduğu için değil**, bu URL'lerin (bakiye/maç/davet içeren sayfalar) bir sosyal paylaşım yüzeyi olarak kullanılmasının **istenmemesi** — bir ürün/güvenlik kararı — nedeniyle üretilmez. Bu ayrım önemlidir: Claude Code bu kuralı "SEO gereği" olarak genelleştirip başka bir yerde yanlış uygulamamalı, bunun teknik SEO kuralı değil bilinçli bir proje politikası olduğunu bilmelidir.
- ❓ Sayfa-bazlı özel OG görseli istenirse ayrı bir görev.

---

## 5. YAPILANDIRILMIŞ VERİ (JSON-LD)

🔒/🛠️ **v2'de eklenen en kritik kural — bilgi uydurma yasağı:** JSON-LD **yalnızca** mevcut proje dosyalarında/dokümanlarında **doğrulanabilen** bilgilerle doldurulur. Şirket adı, yasal adres, telefon numarası, sosyal medya hesabı, `logo` URL'i, `rating`, `price`, `offer` gibi hiçbir alan **icat edilmez** — kaynağı olmayan bir alan, doldurulmak yerine **tamamen atlanır**. Sahte/doğrulanmamış yapılandırılmış veri, Google'ın structured data politikalarını ihlal eder ve manuel aksiyon riski taşır.

- `/` sayfasında `Organization` + `WebSite` schema — yalnızca doğrulanabilir alanlar (`name: "WinToWar"`, `url`) doldurulur; `logo`/`sameAs` (sosyal medya) gibi alanlar için gerçek bir kaynak yoksa **eklenmez**.
- `/sss` sayfasında `FAQPage` schema — 🛠️ **v2'de netleştirildi:** yalnızca sayfada **kullanıcıya gerçekten gösterilen** soru/cevaplar işaretlenir; arama motoru için ekstra, kullanıcıya görünmeyen soru/cevap **üretilmez** (Google'ın "gizli içerik" politikasına aykırı olur). Ayrıca bu schema'nın Google'da bir "rich result" olarak görüneceği **garanti edilmez** — amaç yalnızca sayfadaki gerçek FAQ içeriğini semantik olarak doğru işaretlemektir, belirli bir arama sonucu görünümü vaat etmek değildir. 🛠️ **v3.1:** JSON-LD, sayfadaki görünür FAQ içeriğinden (aynı veri kaynağından/component'ten) **programatik olarak türetilir** — aynı soru/cevap metni HTML'e bir kez, JSON-LD'ye ayrıca elle ikinci bir kez **yazılmaz**; bu, içerik ileride güncellendiğinde HTML ile JSON-LD'nin birbirinden kopmasını (senkron dışı kalmasını) önler.
- 🚩 **v2'de gerekçesi netleştirildi:** `Game`, `Product`, `Offer`, `AggregateRating` gibi ticari/oyun zengin sonuçlarına yönelik schema türleri bu görevde kullanılmaz. Bunun nedeni bir teknik zorunluluk değildir (`Game` schema'sı teknik olarak fiyat/rating içermeden de kullanılabilir) — nedeni, platformun gerçek para içeren oyun modeliyle bu tür zengin sonuçların nasıl ilişkilendirileceğinin ayrı bir pazarlama/hukuk değerlendirmesi gerektirmesidir (bkz. Bölüm 12).

---

## 6. URL NORMALİZASYONU / CANONICAL — v2'de genişletildi

Canonical stratejisi aşağıdakilerin **tamamını** tek bir kanonik URL'ye indirger:

- 🛠️ **v3'te netleştirildi — tek kaynak ilkesi:** `NEXT_PUBLIC_SITE_URL` (ör. `https://wintowar.com`) **kanonik production origin'in tek kaynağıdır** — protokolü (`https://`) ve host'u (`www` olup olmadığı) zaten bu değerin içinde taşır. Bölüm 3'teki URL helper (`buildMetadata`/canonical üretici) bu origin üzerinden yalnızca **mutlak URL birleştirmesi** yapar; host/protokolü **ayrıca kod içinde yeniden yazmaz, normalize etmez** — "her zaman HTTPS'e çevir" gibi bağımsız bir normalizasyon katmanı **kurulmaz** (gereksiz bir soyutlama olurdu, `01-workflow-rules.md` Bölüm 0.10 YAGNI). `www`/`www` olmayan kararı bir DNS/domain kararıdır (❓ müşteriye doğrulanmalı, bu görevin kapsamı dışında) — karar netleştiğinde tek yapılması gereken `NEXT_PUBLIC_SITE_URL` değerini doğru origin'le ayarlamaktır, kodda değişiklik gerekmez.
- **Trailing slash:** Next.js varsayılanı korunur, tutarlı uygulanır.
- **Query parametreleri:** `canonical`, sayfanın **temel** (parametresiz) URL'ini gösterir. Bu, aşağıdaki parametre gruplarının **hiçbirinin** canonical URL'i değiştirmediği anlamına gelir: filtre/sıralama (`?tip=vip`, `?sort=`, `?filter=`, `?page=`), tracking (`?utm_source=`, `?utm_medium=`, `?utm_campaign=`, `?ref=`), davet (`?invite=`).
- 🔒 **Güvenlik notu (v2'de eklendi):** Canonical URL bir **erişim kontrol mekanizması değildir** — bir private/hassas URL'i "gizlemek" için canonical kullanılmaz; davet token'ları, session/state değerleri veya başka bir hassas kullanıcı verisi hiçbir zaman canonical, sitemap veya JSON-LD çıktısına **dahil edilmez** (bunlar zaten public olmayan yollardır, bkz. Bölüm 1 — ama bu kural, ileride yanlışlıkla bir hassas parametrenin canonical'a sızmasına karşı ayrıca not düşülüyor).
- **URL encoding/case:** Büyük/küçük harf ve encoding tutarsızlıkları normalize edilir (ör. `/Lobi` → `/lobi`'ye yönlendirme, ayrı bir sayfa olarak değerlendirilmez).

---

## 7. INTERNAL LINKING — v2'de eklendi

SEO yalnızca meta etiketlerden ibaret değildir; bir sayfanın arama motoru tarafından "önemli" sayılması için site içinden erişilebilir olması gerekir.

- Bölüm 1'deki "Indexlenir" grubundaki her sayfanın, **en az bir** başka public sayfadan (header, footer veya sayfa içi doğal bir link ile) erişilebilir olduğu doğrulanır — `07-pages.md`'nin Navigasyon/Footer tablosu zaten bunu büyük ölçüde sağlıyor (`04-style.md`/`07-pages.md`'deki footer linkleri: Kurallar, Kullanım Şartları, Gizlilik, Sorumlu Oyun, Destek); bu görev yalnızca **orphan** (hiçbir yerden linklenmeyen) bir public sayfa olup olmadığını kontrol eder.
- 🛠️ Orphan bir public sayfa bulunursa, **mevcut tasarım/onaylı metin yapısı bozulmadan**, zaten `07-pages.md`/`08-page-content.md`'de öngörülen bir link alanına (ör. footer, ilgili sayfa içindeki doğal bir referans) eklenir — yeni bir pazarlama metni/CTA bloğu **icat edilmez**.
- SEO amacıyla Bölüm 1'in "Noindex, nofollow" grubundaki route'lara (private/oyun/ödeme sayfaları) **gereksiz internal link eklenmez** — internal linking iyileştirmesi yalnızca public sayfalar arasında yapılır.

---

## 8. SEMANTIC HTML / ERİŞİLEBİLİRLİK — MİNİMUM SEO KONTROLÜ — v2'de eklendi

Kapsam dar tutulur — bu bir tam erişilebilirlik denetimi değildir, yalnızca SEO'yu doğrudan etkileyen temel noktalardır:

- Her public sayfada **tek ve anlamlı bir `<h1>`** var mı.
- Heading hiyerarşisi (`h1`→`h2`→`h3`) sırayı atlamıyor mu.
- `<img>`/`next/image` kullanımlarında anlamlı `alt` metni var mı (dekoratif görsellerde boş `alt=""`, bilgi taşıyan görsellerde açıklayıcı metin).
- Link metinleri "buraya tıkla" gibi anlamsız ifadeler yerine bağlantının hedefini tanımlıyor mu.
- 🛠️ Tespit edilen sorunlarda **yalnızca teknik olarak gerekli minimum düzeltme** yapılır (ör. eksik bir `alt` metni eklemek) — sayfa metni, pazarlama dili veya onaylanmış içerik blokları (`08-page-content.md`) **yeniden yazılmaz**.

---

## 9. PERFORMANS / CORE WEB VITALS

- Landing sayfası video arka planları (`landing.mp4` vb.) lazy-load + `poster` görseli + `preload="none"/"metadata"` ile yüklenir (v1 ile aynı).
- Tüm `<img>` kullanımları `next/image`'e taşınır.
- `next/font` kullanılıyorsa `display: swap`.

### 9.1 Ölçüm ve raporlama — v2'de netleştirildi

🛠️ Landing sayfası (`/`) için mümkün olan ölçümler (Lighthouse/PageSpeed Insights) yapılır ve **ham sonuç olarak** rapora eklenir: **LCP, CLS, INP, FCP, TTFB**. 🛠️ Bu görevde **sert bir geçme/kalma eşiği (ör. "LCP < 2.5s değilse görev tamamlanmadı") konulmaz** — development/local ortam ölçümleri production'ı birebir temsil etmeyebilir; rapor "mevcut sonuç: X" şeklinde şeffaf bir şekilde sunulur, hedefe ulaşılıp ulaşılmadığı yorumu müşteri/gerçek production ortamında ayrıca değerlendirilir. 🛠️ **v3.1:** Lighthouse ölçümü bu ortamda (ör. bir araç/erişim kısıtı nedeniyle) çalıştırılamıyorsa, bu açıkça "ölçülemedi — araç/ortam kısıtı" olarak raporlanır; hiçbir metrik **tahmin edilerek** yazılmaz.

---

## 10. TEST / KABUL KRİTERLERİ

- [ ] `robots.txt`'te **hiçbir route `Disallow` edilmemiş** — yalnızca `Sitemap` referansı var (Bölüm 2.1).
- [ ] `robots.txt` ile sayfa-bazlı `noindex` arasında crawl/indexleme açısından çelişki yok.
- [ ] `/sitemap.xml` yalnızca Bölüm 1'deki public route'ları listeliyor.
- [ ] Bölüm 1'deki her route için gerçek `robots` meta etiketi (view-source ile) doğrulandı.
- [ ] `/` ve `/sss` sayfalarında JSON-LD, Google Rich Results Test ile hatasız **ve** yalnızca doğrulanabilir bilgi içerdiği doğrulandı — sahte rating/fiyat/offer/şirket bilgisi yok.
- [ ] Canonical URL'ler `NEXT_PUBLIC_SITE_URL` üzerinden doğru origin ile üretiliyor; tracking/filtre parametreleri canonical'ı değiştirmiyor.
- [ ] `NEXT_PUBLIC_SITE_URL` olmadan production build'in **başarısız olduğu** doğrulandı (Bölüm 3).
- [ ] Public sayfalarda orphan route kontrolü yapıldı, bulgu varsa mevcut link alanlarına eklendi.
- [ ] Mevcut OG/logo asset'leri kontrol edilmeden yeni bir görsel oluşturulmadı.
- [ ] `not-found.tsx` gerçekten `404`, bakım modu gerçekten `503` dönüyor.
- [ ] Auth gerektiren bir route'a girişsiz erişimde oluşan redirect'in private sayfanın metadata'sını **sızdırmadığı** doğrulandı (Bölüm 2.1'deki ilkeyle aynı mantık — bkz. not aşağıda).
- [ ] LCP/CLS/INP/FCP/TTFB ölçümleri rapora eklendi (hedef değil, mevcut durum olarak).
- [ ] `web/lib/metadata.ts` tek bir yerden yönetiliyor.
- [ ] SEO değişiklikleri auth/ödeme/oyun business logic'ine dokunmadı (Bölüm 11).
- [ ] `npm run build` geçiyor.
- [ ] Rapor, local/build-time doğrulamaları ile production/Search Console gerektiren maddeleri ayırıyor; ikincisi için "doğrulanamadı — production sonrası" ifadesi kullanılmış, uydurma bir "doğrulandı" ifadesi yok.
- [ ] Tüm yeni dosyalar `web/app/` altında (kök dizinde ikinci bir `app/` klasörü açılmadı).

> 🛠️ **Local/production doğrulama ayrımı — v3'te eklendi, önemli:** Görev sonu raporunda **local/build-time doğrulamalar** (`/robots.txt`, `/sitemap.xml`, metadata, canonical, JSON-LD sözdizimi, build, HTTP status, Lighthouse) ile **yalnızca gerçek production'da/Search Console'da doğrulanabilecek** şeyler (Google'ın siteyi gerçekten nasıl indexlediği, Search Console coverage raporu, gerçek crawler davranışı, gerçek production Core Web Vitals, rich result'ın arama sonucunda fiilen görünmesi) **açıkça ayrılır**. Production verisi mevcut değilse rapor bu maddeler için **"doğrulandı" değil, "doğrulanamadı — production sonrası ayrıca doğrulanmalı"** yazar. Claude Code hiçbir şekilde "Google Rich Results Test'ten geçti" veya "Search Console'da indexlendi" gibi gerçekte çalıştırılmamış bir doğrulamayı **uydurmaz**.

> 🛠️ **Redirect/metadata notu (v2'de eklendi):** `/giris` gibi bir sayfa girişli bir kullanıcıyı `/lobi`'ye yönlendiriyorsa, bu **sunucu tarafı** bir redirect (uygun HTTP status, `307`/`302`) olmalı ve yönlendirilen `/lobi`'nin kendi (private) metadata'sı hiçbir zaman `/giris`'in public metadata'sının yerine geçip yanlışlıkla public gibi görünmemeli — bu, Bölüm 1'deki matrisin her route'un **kendi** `generateMetadata`'sını kullanmasıyla zaten doğal olarak sağlanır, ayrı bir mekanizma gerekmez; yalnızca test aşamasında doğrulanır.

---

## 11. KAPSAM SINIRI — v2'de eklendi

SEO görevi nedeniyle **authentication, payment, wallet, GameHub veya oyun business logic'i değiştirilmez.** Özellikle:

- `PaymentService`, `PayoutService`, `WalletService`, `IPaymentProvider`, `BtcPayGreenfieldProvider`, webhook/invoice akışları
- `AuthService`, `JwtTokenService` ve `docs/11-auth.md`'de tanımlı auth mimarisi
- `MatchManager`, `GameHub`, oyun motoru servisleri

üzerinde **SEO gerekçesiyle** hiçbir refactor veya mantıksal değişiklik yapılmaz. Bölüm 2.1'deki `/lobi/[inviteToken]` token-sızıntısı tespiti gibi, bu modüllerle ilgili bir bulgu ortaya çıkarsa, **bu görevde çözülmez** — görev sonu raporunda ayrı bir bulgu olarak belirtilir.

---

## 12. ❓ MÜŞTERİDEN DOĞRULANMASI GEREKEN NOKTALAR

- 🚩 **En kritik:** Gerçek parayla oynanan bir "beceri oyunu" platformunun SEO/reklam açısından hedef pazarlarda ne tür kısıtlamalara tabi olduğu (Google Ads/AdSense kumar politikası, bölgesel kısıtlamalar) — hukuki bir konudur, launch öncesi ayrıca ele alınmalı.
- Kanonik domain (`www` ile/olmadan).
- Sayfa-bazlı özel OG görseli istenip istenmediği.
- `/lobi/[inviteToken]`'daki token sızıntısı riski (Bölüm 2.1) — bu görevde yalnızca tespit edildi, çözümü ayrı bir güvenlik görevi gerektirir.
- Google Search Console/Analytics entegrasyonu — bu doküman kapsamında değil.