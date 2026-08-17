# WinToWar — Kapsamlı Eksik/Tutarsızlık Denetimi

Bu bir **uygulama görevi değil, denetim görevidir**. Hiçbir kod yazma, hiçbir dosyayı düzeltme. Tek işin: mevcut kod tabanını (`api/`, `web/`) dokümanlardaki (`docs/*.md`) 🔒 ve 🛠️ maddeleriyle satır satır karşılaştırıp **neyin eksik, neyin yarım, neyin dokümana aykırı olduğunu** bulmak.

## Neden bu şekilde ilerleyeceksin

Proje 10 modül dosyası (`01`–`10`) + `11-auth.md` + `12-seo.md` + `CLAUDE.md` + `project_tree.txt`'den oluşuyor ve bunların toplamı tek seferde sağlıklı analiz edilemeyecek kadar büyük. Bu yüzden **tek büyük rapor yerine, aşağıdaki sırayla ayrı ayrı geç, her aşamanın sonunda o aşamanın raporunu yaz, sonra bir sonrakine geç.** Aşamaları atlama, birleştirmeye çalışma.

## Genel yöntem (her aşamada tekrarla)

1. İlgili `docs/0X-*.md` dosyasını uçtan uca oku.
2. İçindeki her 🔒 (müşteri talimatı) ve 🛠️ (mühendislik kararı) maddesini tek tek çıkar — bunları görmezden gelme, "genel olarak uyumlu görünüyor" gibi yüzeysel yorum yapma.
3. Her madde için karşılık gelen kodu gerçekten aç ve oku (yalnızca dosya adına bakıp var/yok deme — içeriği doğrula). Şunu sor:
   - Bu kural için **hiç kod yok mu** (eksik)?
   - Kod var ama kuralın **bir kısmını** karşılıyor, bir kısmını karşılamıyor mu (yarım)?
   - Kod var ama kuralla **çelişiyor mu** (aykırı — ör. dokümanda enum denirken string literal kullanılmış, dokümanda idempotency zorunlu denirken kontrol yok)?
   - Kod `01-workflow-rules.md` Bölüm 0.4'ü ihlal ediyor mu (`TODO`, boş metot gövdesi, `NotImplementedException`, mock servis üretim kodunda)?
4. Bulguyu şu formatta not al:
   ```
   [MODÜL] [EKSİK|YARIM|AYKIRI] <docs dosyası>#<madde/başlık> — <ne bekleniyordu>
   Kod: <dosya yolu:satır ya da "bulunamadı">
   Neden sorun: <1-2 cümle>
   Önerilen aksiyon: <ne yapılmalı — kod yazma, sadece söyle>
   ```
5. Gerçekten yalnızca kanıtladığın şeyleri raporla. "Muhtemelen eksik olabilir" gibi tahmine dayalı madde yazma — dosyayı aç, doğrula, öyle yaz. Emin olmadığın noktaları ayrı bir "❓ Doğrulanamadı" listesine koy.

---

## Aşama 1 — Temel/süreç uyumu (`01-workflow-rules.md`, `06-coding-standards.md`)

Bunlar modül-bağımsız, tüm kod tabanına uygulanır. Şunları tara:

- `Console.WriteLine` kalıntısı var mı (ILogger yerine)?
- `TODO`/`FIXME`/placeholder/boş metot/`NotImplementedException`/mock servis (test dosyaları hariç) var mı?
- State/enum alanları string literal ile karşılaştırılıyor mu (`== "running"` gibi)?
- Aynı state'e eşzamanlı erişimde `lock`/`ConcurrentDictionary`/transaction eksik mi (özellikle `MatchManager`, `PaymentService`, `WalletService`, `EconomyTickService`)?
- Webhook/SignalR/API çağrılarında idempotency kontrolü var mı, yoksa aynı istek iki kez işlenebilir mi?
- Magic number/string kod içine gömülü mü, yoksa `GameConfig`/`PaymentConfig`/sabit dosyalarında mı?
- Backend domain modeli (`Match`, `PaymentInvoice` vb.) doğrudan API/SignalR'dan mı yayınlanıyor, yoksa her zaman DTO'ya mı map'leniyor?
- Secrets (API key, connection string, webhook secret) kod içine hardcode edilmiş mi?
- Entity değişikliği yapılmış ama karşılığında migration eklenmemiş bir yer var mı?
- Tek kullanım noktalı gereksiz interface/abstraction (`IXServiceFactory` tarzı) var mı (YAGNI ihlali)?
- `api.Tests/` içindeki testler gerçekten mevcut servisleri mi test ediyor, yoksa `Services/` altında test edilmeyen "ölü" akış var mı?

## Aşama 2 — Mimari uyumu (`02-architecture.md`)

- `project_tree.txt`'deki gerçek klasör yapısı, dokümandaki "Proje Yapısı" ve "Yeni Modül Ekleme Kuralı" ile birebir örtüşüyor mu? Yeni modül (ör. `Payments/`, `Rooms/`) mevcut `Models/`, `Services/`, `Controllers/` klasörlerinin **içinde** alt klasör mü, yoksa kendi üst-düzey klasörü mü olmuş?
- "Katman Bağımlılık Kuralları" ihlali var mı (ör. bir servisin doğrudan başka bir modülün domain modeline erişmesi gerekirken sorgu seviyesinde değil doğrudan bağımlılık kurması)?
- "Maç Denetim Kaydı (Audit Log)" ve "Ölçeklenebilirlik" bölümlerindeki 🛠️ kararları gerçekten kodda karşılığı var mı (`MatchEventLogWriter`, `MatchEventLogFlushService` vb. dokümandaki beklentiyle eşleşiyor mu)?
- Dosya/sınıf isimlendirme kuralına aykırı isimlendirme var mı?

## Aşama 3 — Oyun motoru (`03-game-rules.md`)

Bölüm bölüm ilerle (Oda Türleri, Harita/Başlangıç Ataması, Üretim/Ekonomi, Savaş/Çarpışma, Hareket Mekaniği, Eşleşme/Bot Politikası, Kazanma Koşulu, Uç Durumlar, Maçtan Ayrılma, Non-Goals, `GameConfig` alan listesi):

- Bölüm 12'deki `GameConfig` alan listesindeki **her alan** gerçekten `GameConfig.cs`'de var mı, tipi/varsayılan değeri dokümanla uyuşuyor mu?
- Bölüm 6 (Hareket Mekaniği) ve Bölüm 8 (Kazanma Koşulu) ❓🛠️ işaretli — kod bu konuda **bir karar vermiş mi**, yoksa gerçekten belirsiz/eksik mi bırakılmış?
- Bölüm 10.1 (Maçtan Ayrılma Seçenekleri) tüm senaryolarıyla (bağlantı kopması, gönüllü çıkış, vb.) `MatchManager`/`GameHub`'da karşılanıyor mu?
- Bölüm 11 (Yapılmayacaklar) — kodda **kasıtlı olarak yapılmaması gereken** bir şey yanlışlıkla eklenmiş mi?
- Bölüm 9 (Ödeme Bağlantısı özet) ile `05-payment.md`'deki detay arasında bir tutarsızlık var mı (özet ile detay çelişiyor mu)?

## Aşama 4 — Ödeme sistemi (`05-payment.md`)

Bu en kritik ve en büyük modül; gerçek para akışı olduğu için özellikle titiz ol:

- Bölüm 1 (İş kuralları): giriş ücreti/komisyon, kur/cache/stale/single-flight/timeout/fallback zinciri, confirmation eşiği, payout hedefi doğrulama, iptal/lobi dolmama senaryosu, bireysel vazgeçme, bakiye/cüzdan modeli, practice mod — her biri kodda tam karşılığını buluyor mu?
- Bölüm 2 (Veri modeli): `PaymentInvoice`, `Payout`/`PayoutRecipient`, `Refund` alanları dokümanla birebir mi? Precision/rounding kuralı (Bölüm 2.3) uygulanmış mı? Parasal alanlar `string` tipinde mi (Bölüm 2.5)?
- Bölüm 2.6 (Network Fee sorumluluğu) — yalnızca actual persist ediliyor mu?
- Bölüm 3 (Akış diyagramları): giriş ödemesi, payout, refund akışlarının **her adımı** kodda var mı, sıra doğru mu?
- Bölüm 4 (Çifte ödeme/duplicate webhook koruması) — `ProcessedWebhookEvent` gerçekten her webhook girişinde kontrol ediliyor mu?
- Bölüm 5 (State machine'ler) — `PaymentInvoice.Status`, `PayoutRecipient.Status`/`Payout.Status`, `Refund.Status` geçişleri dokümandaki izinli geçişlerle uyuşuyor mu, izinsiz bir geçiş mümkün mü (monotonluk kuralı Bölüm 5.4 dahil)?
- Bölüm 8 (Güvenlik/eşzamanlılık/gözlemlenebilirlik): webhook signature doğrulanıyor mu (`WebhookSignatureValidator`), loglama scope kuralı (`InvoiceId`/`MatchId`/`PlayerId`) uygulanıyor mu?
- Bölüm 9 (Test senaryoları) — dokümanda listelenen senaryoların **her biri** için `api.Tests/` altında gerçekten bir test var mı, yoksa bir kısmı listelenmiş ama yazılmamış mı?
- Bölüm 10 / 10.1 (Uç durumlar, yasal/finansal uyum notları) — kodda karşılığı olmayan bir madde var mı?
- Bölüm 0.3 ("Gerçek para — ekstra dikkat") ve `FakePaymentProvider` kullanımı: sahte implementasyon yalnızca dokümanın izin verdiği kapsamda mı kalmış, yoksa üretim akışına sızmış mı?

## Aşama 5 — Sayfalar/route'lar (`07-pages.md`) ve içerik (`08-page-content.md`)

- "Route Tablosu"ndaki **her route**, `project_tree.txt`'deki `web/app/` altında gerçekten var mı? Tersi de kontrol et: `app/` altında dokümanda tanımlanmamış bir route var mı?
- "Sayfa State Kuralları"/"Sayfa Bazlı State Matrisi" ve "Yetki Matrisi" her sayfa için kodda (middleware/guard/AdminGate vb.) karşılanıyor mu?
- 404/Error/Bakım sayfaları (`not-found.tsx`, `error.tsx`, `/bakim`) dokümandaki davranışla eşleşiyor mu?
- `08-page-content.md` Bölüm 3'teki her sayfa blueprint'i (özellikle 1.13 "Sayfa giriş sırası" ve 1.6 "blok sınırı") gerçek `page.tsx` içeriğiyle karşılaştırıldığında eksik blok var mı, sıra farklı mı?
- Bölüm 1.12 (Boş liste standardı), 1.7 (Hata içeriği yapısı), 1.4 ("Boş hissi" testi) somut kriterleri sayfalarda uygulanmış mı, yoksa jenerik/placeholder metinler mi bırakılmış?
- `07-pages.md` sonundaki ve `08-page-content.md` sonundaki "❓ Müşteriden Doğrulanması Gereken Noktalar" listeleri — bunlar için kod bir 🛠️ varsayımıyla ilerlemiş mi, yoksa hiç ele alınmamış mı?

## Aşama 6 — Stil/UI (`04-style.md`)

- Design token'lar (`components.json`, `globals.css`, Tailwind config) dokümandaki "Design Tokens" bölümüyle birebir mi, yoksa kodda farklı/gelişigüzel renk-spacing değerleri mi kullanılmış?
- "Component Usage Rules" ve "Pattern Library" ihlali var mı (dokümanda kullanılması söylenen bileşen yerine farklı/özel bir bileşen mi yazılmış)?
- "Yapılmayacaklar (Yasaklar)" bölümündeki bir şey yanlışlıkla uygulanmış mı?
- Erişilebilirlik (Bölüm 13) ve Empty/Error/Loading states (Bölüm 14) standartları her sayfada tutarlı mı?

## Aşama 7 — Çapraz doküman tutarlılığı

- `CLAUDE.md`'deki öncelik sırası ile modül dosyaları arasında **gerçekten** çözülmüş görünen ama kodda iki farklı yerde birbiriyle çelişen bir uygulama var mı (ör. `03-game-rules.md` Bölüm 9'daki özet ile `05-payment.md`'nin detayı arasında kod hangisini uygulamış)?
- Tüm modül dosyalarındaki ❓ işaretli maddelerin bir listesini çıkar ve her biri için: kod bir varsayımla ilerlemiş mi (🛠️ karşılığı var mı) yoksa sessizce atlanmış mı?

## Aşama 8 — Authentication (`11-auth.md`)

Gerçek para taşıyan bir hesaba giden tek kapı olduğu için Aşama 4 (Ödeme) kadar titiz ol. `11-auth.md`'nin kendisi de v1→v2→v3 arası revize edildi (Google OAuth eklendi, otomatik hesap bağlama kaldırıldı, `WalletService` kapsam dışına alındı) — dokümanın **en güncel (v3) hâlindeki** kararları esas al, dosyanın başındaki "neden değişti" notlarını oku ki eski/geçersiz bir karara göre denetim yapma.

- **Bölüm 0.0 (kritik mimari tespit):** `Player` gerçekten kalıcı mı (bir `DbContext`'e bağlı, migration'lı) yoksa hâlâ yalnızca `MatchManager` içinde bellekte mi? Doküman bunun için bir `AuthDbContext` açılmasını öngörüyordu (mevcut yapı farklıysa gerekçesiyle birlikte not düş) — kod hangi yolu seçmiş, seçim rapor edilmiş mi (görev sonu raporunda bu netleşmiş miydi)?
- **Bölüm 0.1 (duplicate yasağı):** Mevcut `AuthGuard.tsx`, `identity.ts`, `AdminGate.tsx`, `AdminAuthFilter.cs` gerçekten **genişletilmiş** mi, yoksa yanına ikinci bir paralel auth/identity/admin-authorization sistemi mi eklenmiş? İkisinin de var olduğu (eski dosya hâlâ kullanılıyor, yeni dosya da eklenmiş ama hiçbir yerden çağrılmıyor — "ölü kod") bir durum özellikle arayın.
- **Bölüm 1.1–1.2 (Google OAuth):** `id_token` gerçekten **backend'de** (`Google.Apis.Auth` ile signature/`aud`/`iss`/`exp`) doğrulanıyor mu, yoksa frontend'den gelen email'e mi güveniliyor? `Player.GoogleId` nullable/unique mi? Aynı e-postalı hesapla Google girişinde **otomatik bağlama yapılmadığı** (v3'ün kritik düzeltmesi), bunun yerine `409 EMAIL_EXISTS_LINK_REQUIRED` dönüp `/auth/google/link`'in yalnızca **oturumlu** kullanıcı tarafından çağrılabildiği doğrulanmış mı?
- **Bölüm 1.3/2.1 guard:** `PasswordHash` ve `GoogleId`'nin ikisinin birden `null` olamayacağı bir guard var mı, yoksa böyle bir hesap (giriş yapılamaz durumda) oluşturulabiliyor mu?
- **Bölüm 1.4 (token modeli):** Access token JWT + 15 dk, refresh token DB'de yalnızca hash'lenmiş + `HttpOnly/Secure/SameSite=Strict` cookie mi? Rotation gerçekten uygulanıyor mu — eski refresh token kullanıldığında **tüm** aktif token'ların iptal edildiği (reuse detection) kod var mı?
- **Bölüm 0.4/5 (PlayerId güvenliği):** `Wallet`, `PaymentInvoice`, `GameHub`, `/profil`, `/hesap-ayarlari` gibi hiçbir endpoint/hub metodu `playerId`'yi **client'tan parametre olarak** almıyor mu — hepsi JWT `sub`/`Context.UserIdentifier`'dan mı okuyor? Bunun tersini kanıtlayan tek bir örnek bile kritik bir bulgu olarak öne çıkar.
- **Bölüm 1.9/5.1 (rol/durum):** İlk admin gerçekten `SeedAdminEmail`/`SeedAdminPassword` env var'larından mı geliyor, yoksa kod içine hardcode edilmiş bir admin var mı? `Player.Status` state machine'i (`Active`→`Suspended`/`PendingDeletion`→`Deleted`, `Deleted`'ten geri dönüş yok) doğru uygulanmış mı?
- **Bölüm 3.1/11 (ödeme sınırına dokunmama):** `WalletService`, `PaymentService`, `PayoutService`, `IPaymentProvider`, `BtcPayGreenfieldProvider` dosyalarında bu görevle **tek satır bile değişmemiş mi** — `git diff`/dosya zaman damgası ile doğrula, dokümanın en katı kısıtlarından biri budur.
- **Bölüm 8 (test senaryoları):** Dokümanda listelenen senaryoların (lockout, rotation, reuse detection, Google 409, guard, PlayerId güvenliği, SignalR JWT) her biri için `api.Tests/` altında gerçek bir test var mı?
- **Bölüm 4 (loglama):** Parola/JWT/refresh token/reset token/id_token hiçbir log satırında tam/kısmi görünmüyor mu?

## Aşama 9 — SEO (`12-seo.md`)

`12-seo.md` de v1→v3.1 arası revize edildi (`robots.txt`/`noindex` çelişkisi düzeltildi, canonical `NEXT_PUBLIC_SITE_URL` tek kaynağa bağlandı, kapsam sınırı eklendi) — **v3.1**'deki kararları esas al.

- **Bölüm 1/2.1 (index politikası):** `robots.txt`'te (`web/app/robots.ts`) **hiçbir route** `Disallow` edilmemiş mi (v1'in düzeltilen hatası — eğer hâlâ bir `Disallow` listesi varsa bu net bir "aykırı" bulgusudur, dokümanın kendi revizyon notuyla doğrudan çelişir)? Bölüm 1'deki route matrisindeki her kategori (`index:true`, `noindex+follow`, `noindex+nofollow`) gerçek sayfalarda `generateMetadata`/`robots` alanıyla eşleşiyor mu?
- **Bölüm 2.2 (sitemap):** `web/app/sitemap.ts` yalnızca "Indexlenir" grubundaki route'ları mı listeliyor — dinamik/private bir route (`/mac/[matchId]`, `/game/[matchId]` vb.) yanlışlıkla sitemap'e girmiş mi? `lastModified` varsa gerçek bir kaynaktan mı geliyor, yoksa her build'de "bugün" gibi üretilen bir tarih mi (dokümanın açıkça yasakladığı bir şey)?
- **Bölüm 3 (metadata.ts):** `metadataBase`/canonical üretimi tek bir yardımcıdan mı geliyor, yoksa her sayfada elle tekrar mı yazılmış? `NEXT_PUBLIC_SITE_URL` tanımsızken production build'in gerçekten **başarısız olduğu** (dokümanın kabul kriteri) kod içinde bir guard/throw olarak doğrulanabiliyor mu?
- **Bölüm 5 (JSON-LD, bilgi uydurmama kuralı):** `/` ve `/sss`'teki JSON-LD'de dokümanın yasakladığı türden (uydurma `logo`, `sameAs`, `rating`, `price`, `Game`/`Product`/`Offer`/`AggregateRating` şema türü) bir alan var mı? `FAQPage` içeriği gerçekten sayfadaki görünür FAQ verisinden **programatik türetilmiş** mi, yoksa JSON-LD içine elle ikinci kez mi yazılmış (dokümanın v3.1'de özellikle eklediği kural)?
- **Bölüm 4/11 (OG, kapsam sınırı):** Yeni bir OG görseli oluşturulmadan önce mevcut `public/logo/` asset'leri kontrol edilmiş mi (duplicate görsel riski)? SEO görevi gerekçesiyle `PaymentService`/`WalletService`/`AuthService`/`GameHub`'da **hiçbir** değişiklik yapılmamış mı?
- **Bölüm 9/10 (performans, raporlama disiplini):** Görev sonu raporunda LCP/CLS/INP/FCP/TTFB ham sonuçları var mı, yoksa bu ölçüm adımı hiç atlanmış mı? Rapor, local/build-time doğrulamalarla production/Search Console'a bağlı maddeleri (dokümanın özellikle istediği gibi) **açıkça ayırmış** mı, yoksa "doğrulandı" gibi kanıtlanmamış bir ifade mi kullanılmış?
- **Bölüm 6 (redirect/metadata sızıntısı):** Girişli bir kullanıcı `/giris`'ten `/lobi`'ye yönlendirilirken private sayfanın metadata'sının public sayfanınkiyle karışmadığı test edilmiş mi?
- **Bölüm 10 kabul kriterleri:** Listedeki her madde (checkbox) tek tek kod/rapor üzerinden doğrulanmış mı, yoksa görev sonu raporunda "kapsam dışı" diye geçiştirilmiş ama aslında dokümanın kendi kabul kriterinde olan bir madde (ör. `/bakim`'in gerçek `503` döndürmesi) var mı? Bu tür bir madde varsa "AYKIRI" değil ayrı bir "🛠️→❓ kapsam gerekçesi tartışmalı" bulgusu olarak işaretle — Claude Code'un kendi verdiği gerekçe (ör. "bu ops/infra kapsamı") dokümanın kabul kriteriyle örtüşüyor mu, gerçekten haklı bir kapsam daraltması mı, yoksa görevi eksik bırakmanın bir gerekçesi mi, ikisini ayırt et.
- ❓ **Özellikle doğrula:** Görev sonu raporundaki "önceki geliştiricinin bıraktığı no-op title template kararı korundu" gibi bir geçmiş karara atıf içeren ifadeler — böyle bir kararın gerçekten `web/lib/metadata.ts`'in mevcut/önceki halinde var olup olmadığını kodun kendisinden (git geçmişi/dosya içeriği) doğrula; dokümanlarda böyle bir karar hiç geçmiyorsa bu iddianın kaynağını sorgula.

---

## Rapor formatı (her aşama sonunda ve en sonda genel özet)

Aşama başına:

```
## Aşama N — <modül adı>
### Eksik (hiç kod yok)
- ...
### Yarım (kısmen var)
- ...
### Dokümana aykırı
- ...
### ❓ Doğrulanamadı / manuel bakılmalı
- ...
```

Tüm aşamalar bittikten sonra, en sona tek bir **"Öncelik Sıralı Eksik Listesi"** ekle:

1. Gerçek para/güvenlik etkisi olanlar (ödeme, auth, webhook, PlayerId güvenliği) — en üstte.
2. Oyun motoru çekirdek mantığı (savaş, ekonomi, kazanma koşulu).
3. Sayfa/route/içerik eksikleri.
4. Stil/UI tutarsızlıkları.
5. SEO/görünürlük eksikleri (indexleme, structured data, performans) — auth'tan sonra ama oyun/sayfa mantığından düşük öncelikli, çünkü kullanıcı deneyimini/parayı doğrudan etkilemez.
6. Kod standardı/temizlik (loglama, magic number vb.) — en altta.

Hiçbir aşamada kod düzeltme, dosya değiştirme — bu tamamen salt-okunur bir denetim. Bulguları raporladıktan sonra dur, bir sonraki adımı (düzeltme sırası) kullanıcı belirleyecek.
