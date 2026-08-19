# Claude Code Görev Talimatı: Kimlik Doğrulama (Authentication) Sistemi — v3

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla ilerle, kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ⚙️ **ÇALIŞMA DAVRANIŞI KURALLARI:** Süreç kuralları.
- ❓ Müşteriden ileride doğrulanması gereken nokta — asla "dur ve sor" anlamına gelmez, yanında zaten uygulanmış bir 🛠️ taşır (bkz. `CLAUDE.md`).

> ⚠️ **v2 — neden değişti:** v1'de bu modülün **sıfırdan** kurulacağı varsayılmıştı. `project_tree.txt`'nin tam hâli incelendiğinde durum farklı çıktı: `api/Services/AdminAuthFilter.cs`, `web/components/admin/AdminGate.tsx`, `web/components/layout/AuthGuard.tsx`, `web/lib/identity.ts` **zaten mevcut**; `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`, `/hesap-ayarlari` sayfaları da (muhtemelen iskelet/mock durumda) **zaten oluşturulmuş**. v2, göreve "mevcut sistemi analiz et, tekrar üretme" disipliniyle başlar ve **Google ile kayıt/giriş** talebini içerir.
>
> ⚠️ **v3 — neden değişti (harici inceleme sonrası iki düzeltme):** (1) **Otomatik hesap bağlama kaldırıldı.** v2'de Google ile giriş yapan bir e-posta, parola ile kayıtlı mevcut bir hesapla eşleşiyorsa **sessizce** `GoogleId` bağlanıyordu. Gerçek para taşıyan bir hesapta bu, e-postanın Google tarafından doğrulanmış olmasına güvenerek yapılan **tek taraflı** bir bağlama işlemiydi — hesap sahibinin kendisi olduğunu ayrıca teyit etmeden. v3'te bu akış, kullanıcının önce **mevcut parolasıyla giriş yapıp** Google'ı kendi isteğiyle bağlamasını zorunlu kılacak şekilde değiştirildi (Bölüm 1.2, 3.2). (2) **`WalletService` bu görevin kapsamından tamamen çıkarıldı.** v2'nin "Bölüm 0.4'te ödeme sistemine dokunulmaz" kuralıyla "Bölüm 3.1'de `WalletService`'e ekleme yapılır" ifadesi arasında bir iç tutarsızlık vardı. v3'te auth modülü **yalnızca `PlayerId` üretir**; `Wallet` oluşturma sorumluluğu tamamen mevcut sisteme/ayrı bir göreve bırakılır (Bölüm 3.1, 0.4).

Bu modül, ana oyun motorundan ve ödeme sisteminden **ayrı bir katman** olarak inşa edilir, ama `Player` kimliği üzerinden onlarla entegre çalışır. **Ödeme sistemine (`PaymentService`, `PayoutService`, `WalletService`, `IPaymentProvider`, `BtcPayGreenfieldProvider`, webhook/invoice akışı) hiçbir şekilde dokunulmaz** — bu modül yalnızca güvenilir bir `PlayerId` üretir/doğrular, ödeme mantığını yeniden tasarlamaz.

---

## 0. ÇALIŞMA DAVRANIŞI KURALLARI ⚙️

### 0.0 Kod yazmadan önce ZORUNLU analiz — bu turun en kritik adımı

Aşağıdaki dosyaları **gerçekten aç ve içeriğini oku** (yalnızca var/yok kontrolü yeterli değil, `01-workflow-rules.md`'nin genel "kodu gerçekten aç ve doğrula" disipliniyle tutarlı):

- `CLAUDE.md`, `docs/01-workflow-rules.md`, `docs/02-architecture.md`, `docs/05-payment.md`, `docs/06-coding-standards.md`, `docs/07-pages.md`, `docs/08-page-content.md`
- `api/Program.cs` — hangi `DbContext`'ler kayıtlı, authentication/authorization middleware var mı
- `api/Models/Player.cs` — şu an hangi alanları var, **EF Core tarafından herhangi bir `DbContext`'e kayıtlı mı** (bkz. aşağıdaki kritik tespit)
- `api/Services/AdminAuthFilter.cs` — admin yetkisi şu an nasıl kontrol ediliyor (muhtemelen basit bir secret/header kontrolü; gerçek rol-tabanlı bir sistem değilse bu modülle değiştirilecek)
- `api/Services/MatchManager.cs` — `Player` nesnesi burada nasıl yaratılıyor/tutuluyor
- `api/Services/Payments/PaymentDbContext.cs`, `api/Services/GameEventDbContext.cs` — mevcut iki `DbContext`'in kapsamı
- `web/components/layout/AuthGuard.tsx`, `web/components/admin/AdminGate.tsx`, `web/lib/identity.ts` — şu an neyi kontrol ediyorlar (muhtemelen backend'i olmayan bir mock/placeholder — gerçek API'ye bağlanmamış olabilir)
- `web/app/(site)/giris/`, `kayit/`, `sifremi-unuttum/`, `sifre-sifirla/[token]/`, `hesap-ayarlari/` — sayfa iskeletleri zaten var, formların şu an neye submit ettiğini (varsa) incele

**KRİTİK MİMARİ TESPİT — önce doğrula:** `project_tree.txt`'de yalnızca iki `DbContext` görünüyor: `PaymentDbContext` (`Wallet`, `PaymentInvoice`, `Payout`, `Refund`, `WithdrawalRequest`) ve `GameEventDbContext` (`MatchEventLog`). `Player`, `Match`, `Region`, `Army`, `Room` için ayrı bir `DbContext`/migration **görünmüyor** — bu, `02-architecture.md`'nin "Ölçeklenebilirlik" bölümündeki "`MatchManager` in-memory state tutar, lansmanda tek instance" kararıyla tutarlı: `Player` şu an muhtemelen **yalnızca bellekte, bağlantı/maç ömrüyle sınırlı** bir nesne, kalıcı bir hesap değil.

- Bunu `Player.cs`'i ve `Program.cs`'deki `AddDbContext` kayıtlarını okuyarak **doğrula**.
- Doğrularsa: `Player`'ın artık oturumlar arası kalıcı olması gerekiyor (bir e-posta/parola ile giriş yapıp tekrar tekrar aynı hesaba dönebilmesi için) — bu, **yeni bir migration'lı, kalıcı bir `DbContext`** gerektirir. 🛠️ **Karar:** Yeni bir `AuthDbContext` (`api/Services/Auth/AuthDbContext.cs`) açılır, `Player` ve bu modülün yeni entity'leri (Bölüm 2) buraya taşınır — mevcut `PaymentDbContext`/`GameEventDbContext`'e **eklenmez**, çünkü ikisi de kendi modülünün sorumluluğunu taşıyor (`01-workflow-rules.md` Bölüm 0.13 modüller arası izolasyon ilkesi) ve `Player` üçüncü, bağımsız bir kimlik kavramıdır. `Wallet.PlayerId`/`PaymentInvoice.PlayerId` gibi diğer modüllerdeki alanlar bir **id değeri** olarak kalmaya devam eder (doğrudan FK/join değil, sorgu seviyesinde ilişki — zaten `02-architecture.md`'deki "Katman Bağımlılık Kuralları"nın öngördüğü model budur), bu yüzden bu değişiklik onları bozmaz.
- Eğer analiz `Player`'ın aslında zaten farklı bir şekilde (ör. gizli bir üçüncü `DbContext`, ya da `PaymentDbContext` içine sessizce eklenmiş bir `DbSet<Player>`) kalıcı kılındığını gösterirse, yukarıdaki 🛠️ kararı **geçersizdir** — mevcut yapı kullanılır, yeni bir `DbContext` açılmaz. Bu doküman kanıtlanmamış bir varsayımla yazılmıştır; kodu okuduktan sonra gerçek durum farklıysa gerçek durum esas alınır.

### 0.1 Tekrar/duplicate üretme yasağı — bu görevin en kritik kısıtı

`01-workflow-rules.md` Bölüm 0.2'deki genel "kapsam dışı dosyaya dokunma" kuralına ek olarak, bu görevde özellikle:

- `AuthGuard.tsx` zaten varsa **ikinci bir auth guard component'i oluşturma** — mevcut olanı gerçek API'ye bağlayarak genişlet.
- `identity.ts` zaten client-side kimlik/oturum state'i için kullanılıyorsa, onunla **paralel çalışan ikinci bir identity sistemi kurma** — mevcut dosyayı genişlet.
- `AdminAuthFilter.cs`/`AdminGate.tsx` zaten bir admin kontrolü yapıyorsa, **ikinci ve çelişkili bir admin authorization sistemi kurma** — mevcut filtre/guard'ı bu modülün gerçek `Player.Role == Admin` kontrolüyle **değiştir/genişlet**, yanına ikincisini eklemez.
- `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`, `/hesap-ayarlari` sayfaları zaten varsa, bu sayfaları **silip yeniden yazma** — mevcut UI/layout'u koru, yalnızca form submit'lerini gerçek `/auth/*` uçlarına bağla (bkz. `01-workflow-rules.md` Bölüm 0.2 "mevcut kodun stilini/formatını değiştirme" yasağı).

Amaç **"mevcut dosyaya hiç dokunmamak" değil** — "mevcut sistemi bozmadan, tekrar üretmeden doğru entegre etmek".

### 0.2 Ana projedeki tüm kurallar geçerli

`CLAUDE.md` / `docs/01-workflow-rules.md` / `docs/06-coding-standards.md` içindeki kurallar bu modül için de aynen geçerlidir.

### 0.3 Sıra

1. Bölüm 0.0'daki analiz — bulgular kısa bir not olarak (kod yorumu değil, görev sonu raporunda) kaydedilir.
2. Mimari tespit netleşince: `AuthDbContext` (veya mevcut yapı neyse) + `AuthConfig`.
3. `Player.cs`'e auth alanlarının eklenmesi (Bölüm 2.1) — mevcut alanlar korunur.
4. `RefreshToken`, `PasswordResetToken`, `EmailVerificationToken` entity'leri + migration.
5. Password hashing (`PasswordHasher<Player>`).
6. Google OAuth doğrulama servisi (Bölüm 1.2).
7. `AuthService` (register/login/google-login/refresh/logout/forgot-password/reset-password).
8. `JwtTokenService`.
9. Rate limit + lockout.
10. `AuthController`.
11. `Program.cs` authentication configuration (JWT Bearer + `AuthDbContext` DI kaydı).
12. `GameHub` JWT entegrasyonu.
13. Mevcut `AuthGuard.tsx`/`identity.ts`'i gerçek API'ye bağlama.
14. `AdminAuthFilter.cs`/`AdminGate.tsx`'i gerçek `Player.Role` kontrolüne taşıma.
15. Mevcut `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]` sayfalarını gerçek uçlara bağlama + Google butonu ekleme.
16. `/hesap-ayarlari`'daki auth işlemlerini (şifre değiştirme, hesap silme, Google hesabı bağlama) bağlama.
17. Testler (Bölüm 8).
18. Build/test.
19. Görev sonu raporu.

Her aşama sonunda build al, bir sonrakine geçme. Mevcut testlerden biri (`api.Tests/`) bu değişiklikler yüzünden kırılırsa sebebini tespit et ve auth entegrasyonunu mevcut davranışla uyumlu hale getir — testi silerek/atlayarak "geçir" değil.

### 0.4 Gerçek para bağlantısı — dikkat kuralı

Bu modül doğrudan para taşımaz ama `Wallet`'a giden **tek kapıdır**.

- Her API/SignalR isteğinde `PlayerId`, **istemciden gelen bir alan değil**, sunucuda doğrulanmış JWT'nin `sub` claim'inden okunur. `Controller`/`Hub` metotlarına (`/cuzdan`, `/lobi`, `/profil`, `/hesap-ayarlari`, `GameHub` dahil) parametre olarak `playerId` **asla client'tan alınmaz** — client başka bir `PlayerId` göndererek başka bir hesabın wallet/profil/maç verisine erişememelidir; bu kurala uyum Bölüm 8'deki testlerle doğrulanır.
- Parola/token ile ilgili hiçbir değer (parola, JWT, refresh token, reset token, Google id_token) log'a **tam veya kısmi haliyle** yazılmaz.
- Bu görev kapsamında ödeme sistemine (`PaymentService`, `PayoutService`, `WalletService`, `IPaymentProvider`, `BtcPayGreenfieldProvider`, `PaymentWebhooksController`, `PaymentInvoice`, `WithdrawalRequest`) **hiçbir mantıksal değişiklik yapılmaz, tek satır bile eklenmez** — bu dosyalar yalnızca `PlayerId`'yi okur, mimarileri bu görevle yeniden yazılmaz. `Wallet` oluşturma/garanti etme dahil (bkz. Bölüm 3.1) — auth modülünün ürünü yalnızca `PlayerId`'dir, `Wallet`'ın var olup olmadığı bu görevin sorumluluğu değildir.

---

## 1. TEMEL KARARLAR

### 1.1 Auth yöntemi: E-posta + Parola **VE** Google ile giriş 🔒

🔒 **Müşteri talimatı (bu turda eklendi):** E-posta/parola akışına ek olarak **Google ile kayıt/giriş** desteklenecek. Apple/diğer OAuth sağlayıcıları şimdilik **eklenmez** (istenmedi, YAGNI) ama mimari buna kapalı değildir (bkz. Bölüm 1.2, `Player.PasswordHash` zaten nullable tasarlanmıştı — aynı esneklik Google için de geçerli).

### 1.2 Google OAuth — teknik tasarım 🛠️

- **Akış:** Frontend'de Google Identity Services (GIS) JS SDK ile "Google ile Devam Et" butonu; Google bir `id_token` (JWT) döner; frontend bunu `POST /auth/google` ile backend'e iletir. Backend `id_token`'ı **kendisi doğrular** (Google'ın public key'lerine karşı imza + `aud`/`iss`/`exp` kontrolü) — frontend'in ilettiği bilgiye (email, isim) **asla** doğrudan güvenilmez, yalnızca doğrulanmış token içeriği kullanılır.
- 🛠️ **Kütüphane istisnası:** `Google.Apis.Auth` NuGet paketi eklenir — bu, `06-coding-standards.md`/`01-workflow-rules.md` Bölüm 0.11'deki "yeni paket ekleme, gerekirse gerekçelendir" kuralına tabi **meşru bir istisnadır**: id_token doğrulaması Google'ın public key rotasyonuna karşı elle güvenli şekilde yeniden yazılabilecek bir iş değildir, resmi kütüphane kullanmamak güvenlik riski oluşturur.
- **`Player` alanı:** `GoogleId` (`string?`, unique index, nullable). `PasswordHash` **de** nullable olduğundan üç hesap türü doğal olarak desteklenir: yalnızca parola, yalnızca Google, ikisi birden (bkz. hesap bağlama aşağıda).
- **Yeni kayıt (Google ile ilk giriş):** `GoogleId` ile eşleşen `Player` yoksa, aynı e-posta ile parola-tabanlı bir hesap da yoksa → yeni `Player` oluşturulur, `Role = Player`, `Status = Active`. **`EmailVerifiedAt` anında doldurulur** (Google zaten e-postayı doğrulamış durumda — ayrı bir doğrulama e-postası göndermeye gerek yok, bu Bölüm 9'daki genel e-posta doğrulama akışından farklıdır). `AgeConfirmedAt`/`TermsAcceptedAt`: Google girişinde de aynı onay adımı **atlanmaz** — 🛠️ Google ile ilk girişte kullanıcıya tek seferlik bir "18 yaşından büyüğüm + Şartları kabul ediyorum" onay ekranı gösterilir (kayıt formunun Google'a özel kısa versiyonu), bu iki alan doldurulmadan hesap `Active` olmaz.
- **Hesap eşleştirme/bağlama (email çakışması) — v3'te güvenlik lehine değiştirildi:** Google ile giriş yapılan e-posta, `GoogleId` eşleşmesi olmadan, zaten parola ile kayıtlı bir hesabın e-postasıyla **aynıysa**: 🛠️ **otomatik bağlama yapılmaz.** `POST /auth/google` bu durumda `409 Conflict` + `code: "EMAIL_EXISTS_LINK_REQUIRED"` döner; hesap oluşturulmaz, `GoogleId` set edilmez. Frontend bu kodu görünce kullanıcıyı "bu e-posta zaten kayıtlı, devam etmek için önce parolanızla giriş yapın" mesajıyla `/giris`'e yönlendirir. Kullanıcı parolasıyla normal giriş yaptıktan **sonra**, `/hesap-ayarlari`'ndan (mevcut oturumla, `POST /auth/google/link`) Google'ı **kendi isteğiyle** bağlar. Gerekçe: Google tarafında e-postanın doğrulanmış olması, o e-postanın sahibinin **bu platformdaki mevcut hesabın da sahibi olduğunu** kanıtlamaz — iki kimlik iddiasını (Google'ın "bu e-posta size ait" iddiası ile platformun "bu hesap size ait" iddiasını) tek taraflı ve sessizce birleştirmek, gerçek para taşıyan bir hesapta gereksiz bir saldırı yüzeyidir; kullanıcının **iki tarafı da kendi aktif eylemiyle** doğrulaması (önce parolayla giriş, sonra bilinçli bağlama) daha güvenli ve az maliyetli bir tercihtir.
- **`/hesap-ayarlari`'ndan manuel bağlama/ayırma:** Parola ile giriş yapmış bir kullanıcı `/hesap-ayarlari`'ndan Google hesabını sonradan bağlayabilir (`POST /auth/google/link`, mevcut oturum + Google id_token). Bir hesabın **hem** `PasswordHash` **hem** `GoogleId`'si `null` olamaz — biri kaldırılmadan önce diğerinin var olduğu guard ile doğrulanır (kilitli kalan hesap oluşmasın diye).
- **Rate limit/lockout Google'a uygulanmaz** (Bölüm ~1.5'teki brute-force koruması parola akışına özgüdür; Google akışında kaba kuvvet saldırısı senaryosu yoktur, Google kendi tarafında bunu zaten yönetir).

### 1.3 Kimlik entity'si: Mevcut `Player.cs` genişletilir, yeni `User` tablosu açılmaz

`07-pages.md`'nin önerdiği `User.Role` ifadesi bu projede `Player.Role` olarak okunur (gerekçe: `Wallet.PlayerId`, `PaymentInvoice.PlayerId`, `MatchEventLog` zaten `Player` kimliğine bağlı — bkz. `05-payment.md` satır 446 `Player.AgeConfirmedAt`). Kalıcılık katmanı konusunda bkz. Bölüm 0.0'daki kritik tespit — bu, entity'nin **adını** değiştirmez, yalnızca **nerede saklandığını** etkiler.

### 1.4 Token modeli: Kısa ömürlü JWT access token + rotating refresh token

- **Access token:** JWT, `AuthConfig.AccessTokenLifetimeMinutes = 15`. `sub` claim'i = `PlayerId`, `role` claim'i = `Player`/`Admin`. `Authorization: Bearer` header ile API'de, SignalR handshake'inde `access_token` query param'ı ile taşınır (ASP.NET Core'un resmi SignalR+JWT deseni). Frontend `localStorage`'a **yazmaz**, yalnızca memory/state'te tutar.
- **Refresh token:** Kriptografik olarak güvenli 256-bit rastgele değer; DB'de yalnızca hash'i (`RefreshToken.TokenHash`) tutulur. `HttpOnly`, `Secure`, **`SameSite=None` (production) / `Lax` (dev)** — gerekçe için Bölüm 4'teki 🐞 notu, `Path=/auth` cookie. `AuthConfig.RefreshTokenLifetimeDays = 30`.
- **Rotation:** Her `/auth/refresh`'te eski token iptal edilir (`RevokedAt`), yenisi verilir. İptal edilmiş bir token tekrar kullanılırsa (çalıntı token belirtisi): o kullanıcının **tüm** aktif refresh token'ları iptal edilir, `SuspiciousActivityLog`'a (`03-game-rules.md` Bölüm 11'deki mevcut mekanizmayla aynı yapı) kayıt düşülür.

### 1.5 Parola hashing: `Microsoft.AspNetCore.Identity.PasswordHasher<Player>`

Ayrı bir NuGet paketi (BCrypt.Net vb.) eklenmez — ASP.NET Core'un yerleşik `PasswordHasher<T>` (PBKDF2) kullanılır. `Player.PasswordHash` (`string?`, Google-only hesaplarda `null`).

### 1.6 Rate limit ve hesap kilitleme (yalnızca parola akışı)

- `/auth/login`: IP başına `AuthConfig.LoginRateLimitPerMinute = 10`.
- `Player.FailedLoginAttempts`, art arda `AuthConfig.MaxFailedLoginAttempts = 5` başarısız denemeden sonra `Player.LockedUntil = now + 15 dk` (`AuthConfig.LockoutDurationMinutes`), `423 Locked`. Başarılı girişte sıfırlanır.
- `/auth/register`, `/auth/forgot-password`: IP başına `AuthConfig.RegisterRateLimitPerHour = 5` / `AuthConfig.ForgotPasswordRateLimitPerHour = 5`.

### 1.7 E-posta doğrulama: Oynamayı bloklamaz, çekimi bloklar

E-posta/parola ile kayıt olan kullanıcı doğrulama yapmadan da Practice/Standart/VIP oynayabilir ve bakiye yükleyebilir. `WithdrawalRequest` oluşturma (`05-payment.md` Bölüm 1.9) `Player.EmailVerifiedAt != null` şartına bağlıdır. Google ile kayıtta bu alan anında dolar (Bölüm 1.2). ❓ Müşteriye doğrulanmalı: eşik yeterli mi yoksa oyuna girişte de zorunlu mu olmalı.

### 1.8 Yaş ve sözleşme onayı — mevcut alanlarla birebir

`Player.AgeConfirmedAt` / `Player.TermsAcceptedAt` (zaten `05-payment.md`/`07-pages.md`'de tanımlı) hem `/auth/register` hem `/auth/google` (ilk kayıt anı) tarafından doldurulur; ikisi de dolmadan hesap `Active` olmaz (Bölüm 1.2).

### 1.9 Rol modeli ve ilk admin

`Player.Role` enum'u (`Player`, `Admin`). Yeni kayıtların tamamı (parola veya Google fark etmeksizin) `Role = Player`. İlk admin self-servis kayıttan oluşturulamaz; `AuthConfig.SeedAdminEmail`/`SeedAdminPassword` **ortam değişkenlerinden** okunur (kod içine hardcode edilmez), `Program.cs` startup'ında yalnızca eşleşen `Player` yoksa bir kez oluşturulur. Mevcut `AdminAuthFilter.cs` şu an farklı bir mekanizma (ör. sabit secret/header) kullanıyorsa, bu filtre **kaldırılıp** gerçek `Player.Role == Admin` kontrolüne taşınır — iki paralel admin kontrol sistemi bırakılmaz (Bölüm 0.1).

### 1.10 Hesap durumu — askıya alma ve silme

`Player.Status` enum'u ve `AccountDeletionRequest` entity'si (Bölüm 2, 5). Aktif bakiye veya açık maç varsa hesap silme isteği reddedilir (`05-payment.md` Bölüm 1.9 ile tutarlı guard).

### 1.11 Practice mod dahil, tüm oyun akışı auth gerektirir

`07-pages.md`'nin Yetki Matrisi'nde `/game/*` istisnasız auth gerektiriyor; misafir/anonim oynama yoktur. Practice'in "ücretsiz" olması yalnızca ödeme akışının tetiklenmediği anlamına gelir, auth gerekliliğini kaldırmaz.

---

## 2. VERİ MODELİ VE KONFİGÜRASYON

### 2.1 `Player.cs` — eklenecek alanlar

> Dosyayı önce aç; hangi alan zaten varsa tekrar ekleme, çakışıyorsa mevcut alanı koru.

| Alan                  | Tip                 | Not                                                                     |
| --------------------- | ------------------- | ----------------------------------------------------------------------- |
| `Email`               | `string`            | Unique index                                                            |
| `PasswordHash`        | `string?`           | Bölüm 1.5 — Google-only hesapta `null`                                  |
| `GoogleId`            | `string?`           | Unique index (nullable) — Bölüm 1.2                                     |
| `DisplayName`         | `string`            | Zaten yoksa eklenir                                                     |
| `Role`                | `PlayerRole` enum   | `Player` \| `Admin`                                                     |
| `Status`              | `PlayerStatus` enum | Bölüm 5.1                                                               |
| `EmailVerifiedAt`     | `DateTime?`         | Google ile kayıtta anında dolar                                         |
| `AgeConfirmedAt`      | `DateTime?`         | Zaten `05-payment.md`'de tanımlı                                        |
| `TermsAcceptedAt`     | `DateTime?`         | Zaten `07-pages.md`'de tanımlı                                          |
| `FailedLoginAttempts` | `int`               | Varsayılan 0                                                            |
| `LockedUntil`         | `DateTime?`         |                                                                         |
| `CreatedAt`           | `DateTime`          | `TimeProvider` üzerinden (`05-payment.md` Bölüm 2.8 ile aynı soyutlama) |
| `LastLoginAt`         | `DateTime?`         |                                                                         |

Guard: `PasswordHash` ve `GoogleId` **aynı anda ikisi de `null` olamaz** (Bölüm 1.2).

### 2.2 `RefreshToken`, `PasswordResetToken`, `EmailVerificationToken`

v1'deki tanımlarla aynı (`Models/Auth/`): `Id`, `PlayerId`, `TokenHash` (unique index), `ExpiresAt`, `CreatedAt`, `RevokedAt`/`UsedAt` (nullable). `EmailVerificationToken` yalnızca parola-akışı kayıtlarında üretilir (Google akışında gerek yok, Bölüm 1.2).

### 2.3 `AccountDeletionRequest`

`Id`, `PlayerId`, `RequestedAt`, `Status` (`Pending`→`Completed`/`Rejected`), `RejectionReason` (nullable).

### 2.4 `AuthConfig` — Tam Alan Listesi

| Alan                                   | Değer   | Kaynak                           |
| -------------------------------------- | ------- | -------------------------------- |
| `AccessTokenLifetimeMinutes`           | 15      | Bölüm 1.4 🛠️                     |
| `RefreshTokenLifetimeDays`             | 30      | Bölüm 1.4 🛠️                     |
| `PasswordResetTokenExpirySeconds`      | 900     | `07-pages.md`'de önerilmiş değer |
| `EmailVerificationTokenExpirySeconds`  | 86400   | 🛠️ varsayım                      |
| `MaxFailedLoginAttempts`               | 5       | Bölüm 1.6 🛠️                     |
| `LockoutDurationMinutes`               | 15      | Bölüm 1.6 🛠️                     |
| `LoginRateLimitPerMinute`              | 10      | Bölüm 1.6 🛠️                     |
| `RegisterRateLimitPerHour`             | 5       | Bölüm 1.6 🛠️                     |
| `ForgotPasswordRateLimitPerHour`       | 5       | Bölüm 1.6 🛠️                     |
| `RevokeAllOnReuseDetected`             | true    | Bölüm 1.4 🛠️                     |
| `MinPasswordLength`                    | 8       | 🛠️ varsayım                      |
| `GoogleClientId`                       | env var | Bölüm 1.2 — hardcode edilmez     |
| `SeedAdminEmail` / `SeedAdminPassword` | env var | Bölüm 1.9                        |

### 2.5 `PlayerStatus`

`Active`, `Suspended`, `PendingDeletion`, `Deleted` (Bölüm 5.1).

---

## 3. AKIŞ DİYAGRAMLARI

### 3.1 Kayıt (`POST /auth/register`)

Email/Password/DisplayName/AgeConfirmed/TermsAccepted → guard'lar (Bölüm 1.8) → hash → `Player` oluştur → `EmailVerificationToken` üretilir, e-posta gönderilir (`IEmailSender` abstraction, gerçek SMTP/sağlayıcı seçimi bu görevde hardcode edilmez, ❓ müşteriden sağlayıcı tercihi netleşmeli) → access+refresh token dönülür.

🛠️ **`Wallet` bu akışın kapsamında DEĞİLDİR (v3'te netleştirildi):** Auth modülünün tek sorumluluğu geçerli bir `PlayerId` üretmektir; `Wallet` oluşturma `WalletService`'in sorumluluğudur ve bu göreve dahil değildir. `05-payment.md` zaten `Wallet`'ın ne zaman/nasıl oluşturulacağını (top-up, maça giriş vb.) tanımlıyor — o akış bir `Wallet` bulamazsa ne yapacağını kendi modülü içinde ele almalı. Bu görev kapsamında `WalletService`'e **hiçbir satır eklenmez/değiştirilmez** (Bölüm 0.4). Eğer `WalletService` şu an bir `Player` için `Wallet` bulunamadığında hata veriyorsa (yani "kayıt olduktan hemen sonra, ilk top-up'tan önce" `Wallet` yoksa bir sorun oluşuyorsa), bu **ayrı, izole bir bulgu** olarak görev sonu raporunda belirtilir — bu görevde düzeltilmez.

### 3.2 Google ile kayıt/giriş/bağlama (`POST /auth/google`, `POST /auth/google/link`)

```
Google ile Devam Et
        │
     id_token
        │
        ▼
Backend doğrulama (Google.Apis.Auth: signature + aud + iss + exp)
        │
  GoogleId eşleşiyor mu?
   ├─ Evet ──────────────────────────────► Giriş (access+refresh token)
   └─ Hayır
        │
   Aynı e-postalı Player var mı?
   ├─ Hayır ─► Yeni Player (EmailVerifiedAt=now) ─► Age+Terms onayı ─► access+refresh token
   └─ Evet ──► 409 EMAIL_EXISTS_LINK_REQUIRED (hesap oluşturulmaz, bağlanmaz)
                    │
             Kullanıcı /giris'te parolasıyla giriş yapar
                    │
             /hesap-ayarlari'ndan POST /auth/google/link (mevcut oturum + id_token)
                    │
             GoogleId bağlanır
```

`POST /auth/google/link`, yalnızca **geçerli bir oturumla** (Bölüm 1.4'teki access token) çağrılabilir; kimliksiz bir istekle rastgele bir hesaba Google bağlanamaz.

### 3.3 Giriş (`POST /auth/login`)

v1 ile aynı (Bölüm 3.2 — bkz. önceki doküman): genel hata mesajı, lockout, `Suspended` durumunda "başarılı ama status=suspended" davranışı.

### 3.4 Token yenileme / Şifremi unuttum / Çıkış

v1'deki tanımlarla aynı (rotation, tek kullanımlık reset token, parola değişince tüm refresh token'ların iptali, logout'ta cookie temizleme).

### 3.5 SignalR (`GameHub`) bağlantı doğrulama

`[Authorize]` + JWT Bearer, `access_token` query string üzerinden SignalR handshake'inde taşınır. `Context.UserIdentifier` (JWT `sub`) tüm hub metotlarında `PlayerId` kaynağıdır.

---

## 4. GÜVENLİK

- Parola politikası: min 8 karakter, karmaşıklık zorunluluğu yok (NIST 800-63B, sürtünmeyi artırmadan makul güvenlik).
- Refresh token cookie: `HttpOnly`, `Secure`, **`SameSite=None` (production) / `Lax` (dev)**, `Path=/auth`. Access token yalnızca memory'de.

  🐞 **Canlı ortam bulgusu — `Strict` neden kullanılamıyor.** Web (`win-to-war.vercel.app`) ile API
  (`wintowar.onrender.com`) farklı site'lardır. `SameSite=Strict`, cookie'yi yalnızca aynı site'tan çıkan
  isteklere iliştirdiği için refresh cookie'si tarayıcıda duruyor ama `POST /api/auth/refresh`'e **hiç
  gönderilmiyordu**: her sayfa yüklemesinde oturum düşüyor, ardından gelen her korumalı istek 401 alıyordu
  (canlıda `POST /api/rooms/{id}/join` → 401 olarak gözlendi). Cross-site bir web/API ayrımında tek geçerli
  değer `None`'dır; tarayıcılar `None` için `Secure` zorunlu kılar, production zaten https'tir. Dev'de API http
  üzerinden çalıştığından `None` reddedilirdi — orada `localhost:3000` ile `localhost:5019` zaten aynı site
  olduğu için (`SameSite` port'a bakmaz) `Lax` yeterlidir.

  ⚠️ **Ödünleşim:** `None`, `Strict`'in sağladığı CSRF korumasını kaldırır. Kabul edilebilir bulundu: cookie
  yalnızca `Path=/api/auth` altındaki refresh ucunda kullanılır ve o uç yan etki üretmez (yalnızca yeni bir
  access token verir); para taşıyan tüm uçlar cookie'ye değil `Authorization: Bearer` header'ına bakar ve bir
  header CSRF saldırısıyla taklit edilemez. ❓ Müşteri daha sıkı bir duruş isterse alternatif, web ve API'yi aynı
  site altına almaktır (ör. `api.win-to-war.com`) — o zaman `Strict`'e geri dönülebilir.
- JWT imzalama anahtarı ve `GoogleClientId` ortam değişkeninden okunur, kod içine hardcode edilmez.
- CORS yalnızca gerçek frontend origin'ine izin verir; `AllowAnyOrigin + credentials` birlikte kullanılmaz.
- Loglarda `PlayerId` scope'u bulunur (`05-payment.md` Bölüm 8.2 ile tutarlı) ama parola/JWT/refresh token/reset token/Google id_token hiçbir log satırında tam veya kısmi yer almaz.

---

## 5. STATE MACHINE — `Player.Status`

```
Active ──(admin askıya alır)──► Suspended ──(admin kaldırır)──► Active
Active ──(AccountDeletionRequest onaylanır)──► PendingDeletion ──► Deleted
```

`Deleted`'ten geri dönüş yok. `Deleted` hesapla giriş (parola veya Google fark etmeksizin) → `401`.

---

## 6. API UÇLARI (ÖZET)

| Uç Nokta                | Method | Auth              | Açıklama                                                                   |
| ----------------------- | ------ | ----------------- | -------------------------------------------------------------------------- |
| `/auth/register`        | POST   | Hayır             | Bölüm 3.1                                                                  |
| `/auth/google`          | POST   | Hayır             | Bölüm 3.2 — **yeni**                                                       |
| `/auth/google/link`     | POST   | Evet              | Bölüm 1.2/3.2 manuel bağlama — yalnızca oturum açmış kullanıcı çağırabilir |
| `/auth/login`           | POST   | Hayır             | Bölüm 3.3                                                                  |
| `/auth/refresh`         | POST   | Cookie            | Bölüm 3.4                                                                  |
| `/auth/logout`          | POST   | Evet              |                                                                            |
| `/auth/forgot-password` | POST   | Hayır             |                                                                            |
| `/auth/reset-password`  | POST   | Hayır (token ile) |                                                                            |
| `/auth/verify-email`    | POST   | Hayır (token ile) |                                                                            |
| `/auth/change-password` | POST   | Evet              | `/hesap-ayarlari`                                                          |
| `/auth/me`              | GET    | Evet              |                                                                            |

Mevcut API naming convention'a uyulur (`Controllers/` altındaki diğer controller'ların stiliyle tutarlı).

---

## 7. SAYFA/ROUTE ENTEGRASYONU

- Mevcut `AuthGuard.tsx`, `/auth/me`'yi çağırıp oturum doğrular; başarısızsa `/giris`'e yönlendirir — component **yeniden yazılmaz**, gerçek API çağrısına bağlanır.
- Mevcut `AdminGate.tsx`/`AdminAuthFilter.cs`, gerçek `Player.Role == Admin` kontrolüne taşınır (Bölüm 1.9).
- Mevcut `/kayit` sayfasına "Google ile Devam Et" butonu eklenir (Google Identity Services JS SDK, `GoogleClientId` frontend env var üzerinden). Buton, mevcut form tasarım dilini bozmayacak şekilde (`04-style.md`/`10-ui-redesign.md` ile tutarlı, sade) eklenir — yeni bir ayrı tasarım dili **oluşturulmaz**.
- `/hesap-ayarlari`, Google hesabı bağlama/ayırma aksiyonunu (Bölüm 1.2) alır.

---

## 8. TEST SENARYOLARI

v1'deki tüm senaryolara (register/login/lockout/refresh rotation/reset token/suspended/deleted/admin 403/SignalR JWT) ek olarak:

- Google id_token ile ilk giriş (eşleşen e-posta yok) → yeni `Player` oluşuyor, `EmailVerifiedAt` dolu mu.
- Aynı e-postalı mevcut parola-hesabına, `GoogleId` eşleşmesi olmadan Google ile giriş denemesi → `Player` **oluşturulmuyor/bağlanmıyor**, `409 EMAIL_EXISTS_LINK_REQUIRED` dönüyor mu.
- Kullanıcı parolayla giriş yaptıktan sonra `/auth/google/link` ile bağlama → sonrasında **hem** parolayla **hem** Google ile giriş mümkün mü.
- Oturumsuz (auth'suz) bir istekle `/auth/google/link` çağrısı → reddediliyor mu (bir hesaba yetkisiz bağlama yapılamaz).
- Geçersiz/sahte id_token → reddediliyor mu.
- `PasswordHash` ve `GoogleId` ikisi de `null` olacak bir durum oluşturulamıyor mu (guard testi).
- Register/Google-register sonrası `Wallet` işlemi tetiklenmiyor mu (auth modülünün `WalletService`'e dokunmadığının doğrulaması).
- **PlayerId güvenliği:** client sahte/başka bir `PlayerId` gönderdiğinde `/cuzdan`, `/profil`, `/lobi`, `GameHub` üzerinden başka bir hesabın verisine erişilemiyor mu (v2'nin özellikle vurguladığı, kritik bir güvenlik testi seti).
- `Player` oluşturulduktan sonra `Wallet` oluşturma başarısız olursa (Bölüm 3.1'deki iki-adımlı akış) sonraki bir istekte `Wallet` otomatik tamamlanıyor mu.

---

## 9. KABUL KRİTERLERİ (DEFINITION OF DONE)

- [ ] Bölüm 0.0 analizi yapıldı, gerçek mimari tespiti (Player'ın kalıcılık durumu) doğrulandı ve rapora yazıldı.
- [ ] Mevcut `AuthGuard.tsx`/`identity.ts`/`AdminGate.tsx`/`AdminAuthFilter.cs` **tekrar üretilmedi**, genişletildi.
- [ ] E-posta/parola **ve** Google akışları uçtan uca çalışıyor.
- [ ] `GameHub` JWT ile korunuyor, `PlayerId` yalnızca token'dan okunuyor.
- [ ] Ödeme sistemi dosyalarında (`WalletService` dahil) **hiçbir** mantıksal değişiklik yok — auth modülü yalnızca `PlayerId` üretiyor.
- [ ] Google ile giriş, e-posta çakışması durumunda **otomatik bağlamıyor** — kullanıcı önce parolayla giriş yapıp bilinçli olarak bağlıyor.
- [ ] Rate limit + hesap kilitleme çalışıyor.
- [ ] Hiçbir log satırında parola/token/id_token tam haliyle yer almıyor.
- [ ] `dotnet build`, `dotnet test`, `npm run build` geçiyor; mevcut testler kırılmadı.
- [ ] Görev sonu raporu sunuldu (Bölüm 10).

---

## 10. GÖREV SONU RAPORU

`01-workflow-rules.md` Bölüm 0.14 formatına ek olarak şunları da içerir:

- Oluşturulan / değiştirilen / silinen dosyalar (ayrı listeler).
- Database migration'ları.
- API uç noktaları.
- Frontend auth akışı (hangi mevcut component genişletildi, ne eklendi).
- Güvenlik önlemleri.
- Test sonuçları, build sonuçları.
- Ödeme sistemiyle entegrasyon noktaları (yalnızca `PlayerId` okuma, başka hiçbir şey).
- Bölüm 0.0'daki mimari tespitin sonucu ne çıktı (`AuthDbContext` gerçekten yeni mi açıldı, yoksa mevcut farklı bir yapı mı bulundu).
- Riskler / ❓ müşteriye sorulması gereken noktalar (e-posta sağlayıcısı tercihi, 2FA — Google hesap bağlama akışı v3'te kesinleşti, artık açık bir soru değil).
- `Wallet` konusunda Bölüm 3.1'de bahsedilen "izole bulgu" varsa (kayıt sonrası `Wallet` yokluğu bir soruna yol açıyorsa) bu ayrıca raporlanır — bu görevde düzeltilmez.

**Özellikle doğrula ve rapora yaz:** duplicate bir auth/identity/admin-authorization sistemi oluşturulmadığını.
