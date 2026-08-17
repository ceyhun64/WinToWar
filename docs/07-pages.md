# 07 — Sayfa / Route Haritası 🛠️

> **Doküman önceliği:** Bu dosya, `02-architecture.md`'deki genel klasör kuralının ("hangi dosya nereye gider") sayfa/route seviyesindeki somut karşılığıdır. Hangi sayfanın hangi modülün altında olduğu, route'u, ve o sayfanın sorumluluğu burada tanımlıdır. Bir sayfanın **içindeki** UI/görsel kuralları için `04-style.md`, ödeme akışının **iş mantığı** kuralları için `05-payment.md`, oyunun **iş mantığı** kuralları için `03-game-rules.md` esastır — bu dosya onlarla çelişmez, yalnızca "nerede, hangi route'ta, hangi durumda, hangi veri kaynağıyla" sorusuna cevap verir. Genel çakışma önceliği için tek doğruluk kaynağı `CLAUDE.md`'dir.

> ⚠️ **Not (güncel durum):** Bu dosya artık `03-game-rules.md`, `04-style.md` ve `05-payment.md` ile çapraz kontrol edilmiş ve tutarlı hale getirilmiştir (bkz. Route Tablosu başındaki değişiklik notu). O üç dosyada tanımlı bir sayfa/akış/state varsa, iş mantığı için **onlar önceliklidir** — bu dosya yalnızca "nerede, hangi route'ta" sorusuna cevap verir.

---

## Genel Kurallar

- Tüm route'lar `web/app/` altında, Next.js App Router yapısına uygun tanımlanır.
- Bir sayfa birden fazla modülü ilgilendiriyorsa (ör. lobi sayfası hem maç hem ödeme bilgisini gösterir), sayfa component'i yalnızca **render + veri birleştirme** yapar; her modülün kendi verisi kendi `lib/<modül>/` client'ından çekilir — bkz. `02-architecture.md` "Modüller arası izolasyon" (`01-workflow-rules.md` Bölüm 0.13).
- Kullanıcıya görünen tüm metinler Türkçe (bkz. `04-style.md`, `06-coding-standards.md` İsimlendirme).
- Auth gerektiren sayfalar (`/lobi`, `/cuzdan`, `/profil`, `/game/*`) girişsiz erişimde `/giris`'e yönlendirilir. 🛠️ Auth mekanizmasının kendisi (email/parola, OAuth, sadece cüzdan bağlama vb.) müşteri tarafından belirtilmemiş — bu dosya route'ların **var olduğunu** varsayar, auth yöntemi ayrı bir görevde netleştirilmeli.

---

## Route Tablosu

> 🛠️ **Bu turda eklenen/düzeltilen maddeler:** (1) `/lobi/[inviteToken]` — `03-game-rules.md` Bölüm 2.2'de "şifreli oda davet linkiyle girilir" diye tanımlanmıştı ama bu route tabloya hiç eklenmemişti, bu bir tutarsızlıktı, düzeltildi. (2) Şifre sıfırlama akışı eksikti. (3) Gerçek para/kripto taşıyan bir platformda yasal sayfalar (Kullanım Şartları, Gizlilik, Sorumlu Oyun) müşteri tarafından hiç talep edilmedi ama **hiçbiri opsiyonel değil** — bunlar olmadan bir ödeme sağlayıcısı/banka entegrasyonu genelde reddeder, bu yüzden ❓ değil, asgari bir 🛠️ zorunluluk olarak ekleniyor (içerik/metin müşteriden/hukuk danışmanından gelir, Claude yalnızca route'u ve boş şablonu hazırlar). (4) `/admin` tek düz sayfa yerine alt-route'lara bölündü — bekleyen ödemeler, maçlar, kullanıcılar ayrı ekranlardır, tek sayfada karışması operasyonel hataya açık.

| Route                    | Modül        | Auth                   | Sorumluluk                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ------------------------ | ------------ | ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/`                      | Genel        | Hayır                  | Landing — oyunun nasıl çalıştığı, giriş ücreti, kazanç örneği                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `/giris`                 | Auth         | Hayır                  | Giriş                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `/kayit`                 | Auth         | Hayır                  | Kayıt — 18 yaş ve Kullanım Şartları/Gizlilik onay kutusu içerir (bkz. aşağıda 🛠️ **Yaş/Onay** notu)                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `/sifremi-unuttum`       | Auth         | Hayır                  | 🛠️ **yeni — eksikti.** E-posta ile şifre sıfırlama linki talebi. Auth yöntemi email/parola ise zorunlu; yalnızca cüzdan-bağlama ile giriş seçilirse bu route kapsam dışı kalır (bkz. auth yöntemi ❓)                                                                                                                                                                                                                                                                                                                   |
| `/sifre-sifirla/[token]` | Auth         | Hayır (token ile)      | 🛠️ **yeni — eksikti.** Token doğrulanır, yeni şifre belirlenir; token tek kullanımlık ve süreli (`PasswordResetTokenExpirySeconds`, 🛠️ öneri 900 sn)                                                                                                                                                                                                                                                                                                                                                                    |
| `/lobi`                  | Oyun + Ödeme | Evet                   | Standart/VIP sekmeleri (oda listesi) + tek bir "Pratik Oyna" butonu (oda listesi değil, otomatik eşleşme kuyruğu — bkz. `03-game-rules.md` Bölüm 7). Standart/VIP'te "kaç oyuncu bekleniyor" (X/N), odaya katıl (ücretli ise ödeme akışını tetikler)                                                                                                                                                                                                                                                                    |
| `/lobi/vip-olustur`      | Oyun + Ödeme | Evet                   | VIP oda kurma formu — gri bölge savunması (1-7), Fog of War/Açık Harita, giriş ücreti, oyuncu sayısı (2-12), opsiyonel şifre. 🛠️ **Kurucu, formu gönderdiği anda odanın 1. oyuncusu olarak kendi giriş ücretini öder** (bkz. `03-game-rules.md` Bölüm 2.2) — bakiye yetmezse aynı `/odeme/[invoiceId]` akışına yönlendirilir, oda ödeme onaylanana kadar **oluşturulmaz**                                                                                                                                               |
| `/lobi/[inviteToken]`    | Oyun         | Evet                   | 🛠️ **yeni — eksikti.** Şifreli/parolalı VIP odaya kısayol linki (parolayı atlamaz, yalnızca oda ekranını doğrudan açar — bkz. `03-game-rules.md` Bölüm 2.2 "DÜZELTME"); token geçersiz/süresi dolmuşsa `404` değil, anlamlı bir "bu davet artık geçerli değil" mesajı gösterir (`04-style.md` Empty/Error state kurallarıyla tutarlı)                                                                                                                                                                                   |
| `/odeme/[invoiceId]`     | Ödeme        | Evet                   | BTCPay invoice durumu (top-up **veya** doğrudan maça giriş invoice'ı — bkz. `05-payment.md` Bölüm 1.9 Wallet modeli) — modal olarak da uygulanabilir                                                                                                                                                                                                                                                                                                                                                                    |
| `/game/[matchId]`        | Oyun         | Evet                   | Zaten uygulanmış — harita, HUD, action panel (bkz. `02-architecture.md` dosya ağacı)                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `/cuzdan`                | Ödeme        | Evet                   | Bakiye (`Wallet.BalanceUsd`), LTC yatırma adresi/QR, para çekme talebi (`WithdrawalRequest`) — bkz. `05-payment.md` Bölüm 1.9                                                                                                                                                                                                                                                                                                                                                                                           |
| `/profil`                | Genel        | Evet                   | Kullanıcı bilgisi, geçmiş maçlar                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `/hesap-ayarlari`        | Genel        | Evet                   | 🛠️ **yeni — eksikti, standart bir sayfa.** E-posta/şifre değiştirme (auth yöntemi email/parola ise), hesabı kalıcı silme talebi (KVKK/GDPR "unutulma hakkı" — bkz. `/gizlilik`). `/profil` salt-okunur olduğu için (bkz. Bölüm "Sayfa Sorumluluğu") bu aksiyonlar ayrı bir route'ta toplanır, `/profil`'e karıştırılmaz. ❓ 2FA (iki adımlı doğrulama) eklenip eklenmeyeceği müşteriye sorulmalı — gerçek para/bakiye taşıyan bir hesapta önerilir ama müşteri hiç talep etmedi, bloklamaz.                             |
| `/gecmis`                | Ödeme        | Evet                   | Ödeme/maç geçmişi tablosu (istenirse `/profil` içine gömülebilir, ayrı route zorunlu değil)                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `/kurallar`              | Genel        | Hayır                  | Oyun kuralları — gerçek kural metni, pazarlama metni değil                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `/sss`                   | Genel        | Hayır                  | 🛠️ **yeni — ChatGPT incelemesinde önerilen, kabul edilen madde.** LTC yatırma/çekim süresi, komisyon hesaplama, "maç iptal olursa ne olur" gibi tekrarlayan soruların statik cevapları — `/kosullar` ile aynı şablon. Gerekçe: destek yükünü doğrudan azaltır (`/destek`'e düşecek biletlerin önemli kısmı burada karşılanır), tek bir statik sayfa olduğu için "sade basit" talimatıyla çelişmez.                                                                                                                      |
| `/mac/[matchId]`         | Oyun         | Evet                   | 🛠️ **yeni — kısmen kabul edildi (ChatGPT'nin önerdiği tam kapsam değil).** Bir maçın özet detayı — harita, kim kimi aldı (yalnızca son durum, hamle hamle replay değil — bkz. Non-Goals), süre, kazanan, net ödül. `/gecmis` tablosundaki bir satırdan linklenir; ayrı bir "istatistik/performans" sayfası değildir, yalnızca bir maçın kaydını gösterir. `/destek`'teki maç itirazı akışının (bkz. Bölüm "Sayfa Detayları") doğal bir uzantısıdır — itiraz eden kullanıcı önce bu sayfada kendi maçının kaydını görür. |
| `/kosullar`              | Genel        | Hayır                  | 🛠️ **yeni — eksikti.** Kullanım Şartları (Terms of Service). İçerik hukuki, Claude yalnızca route/şablonu hazırlar, metni doldurmaz — ❓ müşteriden/hukuk danışmanından gelmeli                                                                                                                                                                                                                                                                                                                                         |
| `/gizlilik`              | Genel        | Hayır                  | 🛠️ **yeni — eksikti.** Gizlilik Politikası (KVKK/GDPR — kullanıcı verisi, cüzdan adresi, IP gibi verilerin işlenmesi). Aynı şekilde içerik ❓ hukuktan gelmeli                                                                                                                                                                                                                                                                                                                                                          |
| `/sorumlu-oyun`          | Genel        | Hayır                  | 🛠️ **yeni — eksikti.** Gerçek parayla oynanan bir oyun için "Sorumlu Oyun" (Responsible Gaming) sayfası — harcama limiti, kendini hariç tutma (self-exclusion), yaş sınırı bilgisi. Bkz. aşağıda ❓ notu — bazı ödeme sağlayıcıları/bankalar bu sayfa olmadan entegrasyonu reddedebilir                                                                                                                                                                                                                                 |
| `/cerezler`              | Genel        | Hayır                  | 🛠️ **yeni — eksikti, standart bir sayfa.** Çerez (Cookie) Politikası — `/kosullar`/`/gizlilik` ile aynı şablon, aynı hukuki-içerik kısıtı geçerli (Claude metni yazmaz). Gizlilik Politikası'nın doğal tamamlayıcısı; ayrı sayfa olması, oturum/analytics çerezlerinin nasıl kullanıldığının Gizlilik metninden ayrı, net bir yerde durmasını sağlar.                                                                                                                                                                   |
| `/destek`                | Genel        | Hayır (form için Evet) | 🛠️ **yeni — eksikti.** Destek/iletişim — özellikle "param nerede" tipi ödeme anlaşmazlıkları için tek başvuru noktası; gerçek para akan bir sistemde bunun olmaması operasyonel risktir                                                                                                                                                                                                                                                                                                                                 |
| `/admin`                 | Admin        | Evet (admin rolü)      | Admin ana panel — özet metrikler (bekleyen çekim sayısı, aktif maç sayısı, günlük hacim)                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `/admin/odemeler`        | Admin        | Evet (admin rolü)      | 🛠️ **`/admin`'den ayrıldı.** Bekleyen `WithdrawalRequest`'ler, başarısız/şüpheli `PaymentInvoice`/`Payout` işlemleri, manuel onay/red aksiyonları                                                                                                                                                                                                                                                                                                                                                                       |
| `/admin/maclar`          | Admin        | Evet (admin rolü)      | 🛠️ **`/admin`'den ayrıldı.** Aktif/geçmiş maç listesi, anlaşmazlık durumunda maç detayına bakma (izleyici modu — kapsamı `03-game-rules.md` ile netleşmeli)                                                                                                                                                                                                                                                                                                                                                             |
| `/admin/kullanicilar`    | Admin        | Evet (admin rolü)      | 🛠️ **yeni.** Kullanıcı arama, bakiye görüntüleme, gerekirse hesap askıya alma (ör. şüpheli/çoklu hesap) — ❓ kapsamı müşteriden onay bekliyor                                                                                                                                                                                                                                                                                                                                                                           |
| `/admin/destek`          | Admin        | Evet (admin rolü)      | 🛠️ **yeni — ChatGPT incelemesinde önerilen, kabul edilen madde.** `/destek`'teki `SupportTicket` tablosu daha önce eklenmişti ama bunu görüntüleyecek bir admin ekranı hiç tanımlanmamıştı — gerçek bir boşluktu. Bekleyen destek talepleri (maç itirazı içerenler `MatchId` ile öne çıkarılır), durum güncelleme (Açık/Yanıtlandı/Kapalı).                                                                                                                                                                             |
| `/admin/loglar`          | Admin        | Evet (admin rolü)      | 🛠️ **yeni — ChatGPT incelemesinde önerilen, kabul edilen madde.** Webhook/reconciliation/payout retry gibi kritik olayların (zaten `ILogger` ile loglanan, bkz. `06-coding-standards.md` Loglama) aranabilir bir görünümü — yeni bir loglama sistemi **kurulmaz** (YAGNI), mevcut log altyapısının admin tarafında filtrelenebilir bir okuma ekranıdır.                                                                                                                                                                 |
| `/durum`                 | Genel        | Hayır                  | 🛠️ **yeni — ChatGPT incelemesinde önerilen, kabul edilen madde.** BTCPay/SignalR/API bileşenlerinin canlı durumu (Çalışıyor/Yavaş/Kesinti) — gerçek para akan bir sistemde güven inşa eder. Karmaşık bir monitoring sistemi kurmaz (YAGNI); her bileşen için basit bir health-check endpoint'i okuyup yeşil/kırmızı gösterir.                                                                                                                                                                                           |
| `/bakim`                 | Genel        | Hayır                  | Bakım modu — sistem geçici olarak kapalıyken gösterilir (özellikle ödeme/BTCPay entegrasyonu için önemli, bkz. Bölüm "404 / Error / Bakım")                                                                                                                                                                                                                                                                                                                                                                             |

🛠️ **Yaş/onay notu:** Müşteri hiç belirtmedi ama gerçek parayla oynanan bir platformda `/kayit` formunda "18 yaşından büyüğüm" + "Kullanım Şartları'nı ve Gizlilik Politikası'nı kabul ediyorum" onay kutuları **ayrı bir sayfa değil**, kayıt formunun zorunlu bir parçası olarak eklenir (YAGNI — ayrı bir "yaş doğrulama" route'una gerek yok, tek bir checkbox + kayıt anındaki `AgeConfirmedAt`/`TermsAcceptedAt` timestamp'i yeterli kanıttır). ❓ Müşteriden bunun yeterli olup olmadığı (ör. resmi kimlik doğrulama/KYC gerekip gerekmediği) doğrulanmalı — bu, geliştirmeyi bloklamaz, en basit haliyle (checkbox) başlanır.

❓ **Bölgesel kısıtlama:** Bazı ülkelerde gerçek parayla oynanan beceri/strateji oyunları düzenlemeye tabi olabilir. Müşteri bir ülke/bölge kısıtlaması belirtmedi; bu netleşene kadar geliştirme bloklanmaz ama **launch öncesi müşteriyle ayrıca konuşulması gereken bir konu** olarak burada işaretleniyor (bu bir sayfa/route kararı değil, bir hukuki/iş kararıdır — Claude bunu kendi başına çözemez).

---

## Sayfa Detayları

### `/` — Landing

- Oyunun kısa açıklaması özetlenir. 🛠️ **Düzeltildi:** Kazanç mekaniği artık sabit bir sayı ("10.8 birim") ile anlatılmaz — Standart oda örneği kullanılır ("$1 giriş, kazanan havuzun %90'ını alır"); VIP odalarda havuz kurucunun belirlediği giriş ücreti ve oyuncu sayısına göre değiştiğinden Landing'de somut bir tutar sabitlenmez, yalnızca formül (`Havuz = Giriş Ücreti × Oyuncu Sayısı`, `Kazanç = Havuz × %90`) gösterilir.
- CTA: Giriş yapmamışsa `/kayit`'e, yapmışsa `/lobi`'ye yönlendirir.

### `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`

- Standart auth formları. 🛠️ Bu dosyanın kapsamı dışında; auth yöntemi netleşince ayrı bir görev olarak ele alınmalı.
- `/kayit` formunda "18 yaşından büyüğüm" + "Kullanım Şartları/Gizlilik Politikası'nı kabul ediyorum" onay kutuları zorunlu alan olarak bulunur (ayrı bir sayfa değil — bkz. Route Tablosu "Yaş/onay notu").
- 🛠️ **`/sifremi-unuttum`/`/sifre-sifirla/[token]` — yeni, eksikti:** E-posta girilir → tek kullanımlık, süreli (`PasswordResetTokenExpirySeconds`) bir link gönderilir → linke tıklanınca `/sifre-sifirla/[token]` yeni şifre formunu gösterir. Token kullanıldıktan hemen sonra veya süresi dolduğunda geçersiz kılınır, tekrar kullanılamaz.

### `/lobi`

- 🛠️ **WinToWar'a göre güncellendi (v2 — Practice artık oda listesi değil, tek kuyruk):** İki sekme + bir buton — **Standart** (sabit $1 giriş, varsayılan 4 kişilik, gri bölge savunması 1) ve **VIP** (kurucunun ayarladığı giriş ücreti/oyuncu sayısı/gri bölge savunması/Fog of War; şifreli odalar herkese açık listede **görünmez** — müşterinin "özel davet" ifadesi bunu gerektirir — yalnızca `/lobi/[inviteToken]` linkiyle erişilir, **ve** o linke gidildiğinde de ayrıca parola istenir — müşterinin "şifreli" ifadesi bunu gerektirir; link tek başına yeterli değildir, bkz. `03-game-rules.md` Bölüm 2.2 "DÜZELTME") sekmeleri oda listesi gösterir; ayrıca üstte tek bir **"Pratik Oyna"** butonu bulunur — bu bir sekme/liste değil, doğrudan tıklanınca tek paylaşılan Practice kuyruğuna ekleyen bir aksiyondur (bkz. `03-game-rules.md` Bölüm 7 — kök neden analizi: oda listesi modeli Practice'te oyuncuları gereksiz yere dağıtıp eşleşmeyi zorlaştırıyordu).
- Standart/VIP sekmeleri kendi odalarını "X/N oyuncu" olarak listeler (N, Standart’ta varsayılan 4, VIP’de oda kurulurken seçilen değer).
- "Katıl" butonu → ücretli odada `/cuzdan`'daki bakiye (`Wallet.BalanceUsd`) giriş ücretine yetiyorsa direkt katılım (bkz. `05-payment.md` Bölüm 1.9 Wallet modeli), yetmiyorsa `/odeme/[invoiceId]` akışına yönlendirir; Practice'te ödeme adımı hiç yok, direkt katılım.
- 🛠️ **Bakiye yetersizliği akışı — kesinleştirildi (Gemini incelemesinde bulunan senkronizasyon boşluğuna karşılık):** Bakiye kısmen yetiyorsa (ör. bakiye $0.40, giriş ücreti $1) açılan invoice yalnızca **eksik kısım** için oluşturulur (`AmountUsd = Room.EntryFeeUsd − Wallet.BalanceUsd`), `MatchId` bu odanın maçına ayarlanır. Invoice onaylandığında `Wallet.BalanceUsd` **sıfırlanır** (mevcut bakiye + gelen tutar tam giriş ücretini karşılar) ve oyuncu **otomatik olarak** o maçın lobisine eklenir — `/odeme/[invoiceId]` sayfasında kullanıcı ayrıca "Katıl"a tekrar basmaz, onay geldiği an sunucu katılımı kendisi tetikler ve client **doğrudan `/game/[matchId]`'e** yönlendirilir (bkz. yukarıdaki 🔔 Route Geçiş Akışı notu — bekleme ekranı artık `/lobi`'de değil, `/game/[matchId]`'in Lobby state'inde gösterilir). Bu davranış `/odeme/[invoiceId]`'in `MatchId` dolu invoice'ları için ortak akıştır (bkz. aşağıda).
- VIP sekmesinde "+ Oda Kur" butonu → `/lobi/vip-olustur`.
- Bir odaya girip **5 dakika** eşleşme tamamlanmazsa (oda dolmazsa) "İptal Et / Bakiyeyi İade Et" veya "Beklemeye Devam Et" seçimi bir modal ile sunulur (bkz. `03-game-rules.md` Bölüm 7) — otomatik iade **yoktur**, karar oyuncuya aittir. "İptal Et" seçilirse lobi **iptal olmaz**, yalnızca o oyuncunun slotu boşalır ve sayaç diğerleri için sıfırlanmaz (bkz. `03-game-rules.md` Bölüm 10).
- SignalR üzerinden gerçek zamanlı güncellenir.

### `/lobi/[inviteToken]` 🛠️ **yeni — eksikti**

- Şifreli/özel VIP odaya kısayol linki. `inviteToken` sunucuda doğrulanır: geçerliyse oda **doğrudan açılmaz** — önce bir parola giriş ekranı gösterilir (`Room.RoomPasswordHash` doğrulaması); doğru parola girilince (ve katılım başarılıysa) **doğrudan `/game/[matchId]`'e** yönlendirir — bekleme ekranı orada, Lobby state'inde gösterilir (bkz. yukarıdaki 🔔 Route Geçiş Akışı notu). Token bulunamıyorsa veya oda zaten dolmuş/başlamışsa **404 değil**, "bu davet artık geçerli değil" diyen anlamlı bir Empty/Error state'i gösterilir (`04-style.md`'deki Empty/Error kurallarına uygun — bir davet linkinin süresi dolması, teknik bir hata değil, beklenen bir kullanıcı senaryosudur).
- Bu route herkese açık `/lobi` listesinde **hiçbir yerde linklenmez** — yalnızca oda kurucusunun paylaştığı URL üzerinden erişilir. Link paylaşmak parolayı **atlamaz**, yalnızca odayı bulma kolaylığı sağlar.
- Bakiye yetersizliği durumunda burada da yukarıdaki `/lobi` ile birebir aynı top-up-ve-otomatik-katıl akışı geçerlidir (parola doğrulamasından sonra).

### `/odeme/[invoiceId]`

- BTCPay'den dönen invoice durumunu gösterir. `MatchId` alanı `null` ise bu bir bakiye yükleme (top-up) invoice'ıdır, doluysa doğrudan/kısmi maça giriş invoice'ıdır (bkz. `05-payment.md` Bölüm 1.9).
- 🛠️ **Önceliklendirilen UX detayı — Gemini'nin sorduğu noktaya cevap:** "Bekleniyor" state'i tek bir statik metin değildir. LTC ağ onayı anlık değildir (`RequiredConfirmations` — bkz. `05-payment.md` Bölüm 1.4 — mainnet'te 1'den fazla olabilir), bu yüzden kullanıcı dakikalarca boş bir "bekleniyor" yazısına bakmamalı: sayfa canlı bir **onay ilerlemesi** gösterir ("1/2 onay" gibi, `PaymentInvoice`'ın confirmation sayısı arttıkça — webhook/polling ile güncellenir, bkz. veri kaynağı tablosu). Bu, ayrı bir state **değildir** — mevcut `Pending`/Bekleniyor state'inin **içindeki** bir alt-detaydır (`04-style.md`'deki "Sayaçlar" kuralına uygun: rakamlar hizasız zıplamaz). Gerekçe: özellikle kısmi top-up-ve-katıl akışında (Bölüm 1.9), kullanıcı parasının nereye gittiğini göremediği her dakika, "param gitti mi" endişesiyle sayfadan ayrılıp tekrar deneme riski taşır — bu da idempotency'yi zorlamasa da (zaten korunuyor) gereksiz destek talebi (`/destek`) yaratır.
- 🛠️ **Onay sonrası yönlendirme — kesinleştirildi (Gemini incelemesinde bulunan eksik üçüncü senaryo eklendi):** `MatchId=null` (saf top-up) → onaylandığında `Wallet.BalanceUsd` artar, kullanıcı `/cuzdan`'a döner, **otomatik maça katılım tetiklenmez** (kullanıcı bilinçli olarak yalnızca bakiye yüklemiş olabilir). `MatchId` dolu (maça giriş/top-up-ve-katıl) → iki alt senaryo vardır:
  1. **Normal durum:** Oda hâlâ müsaitse sunucu oyuncuyu **otomatik olarak** o maçın lobisine ekler, client `/lobi` bekleme state'ine veya oda o an tam doluysa doğrudan `/game/[matchId]`'e yönlendirilir; kullanıcı ayrıca bir "katıl" aksiyonu tetiklemez.
  2. **Yarış durumu (`RoomFullAfterPayment`) — eksikti, eklendi:** Ödeme onaylandığında oda başka oyuncularla dolup maç başlamışsa (bkz. `05-payment.md` Bölüm 1.9), sunucudan `RoomFullAfterPayment` eventi gelir; `/odeme/[invoiceId]` sayfası bu durumda **başarı ekranı göstermez** — bunun yerine `04-style.md`'deki mevcut **Empty** state kategorisini kullanır (yeni bir "Info" kategorisi icat edilmez, YAGNI): kısa açıklayıcı metin ("Bu oda doldu, ödemeniz bakiyenize eklendi") + tek bir aksiyon butonu ("Lobiye Dön" → `/lobi`). Bu **Error** değildir — ödemede gerçek bir hata olmadığı için Bölüm 7'deki hata/danger tonuyla karıştırılmaz.
- Bu sayfanın iş mantığı tamamen `05-payment.md`'de tanımlı olmalı; burada yalnızca route'un var olduğu ve yönlendirme davranışı belirtiliyor.

### `/game/[matchId]`

- Oyun sayfası zaten uygulanmış (bkz. `02-architecture.md`).
- Maç sonucu ayrı bir route değil, bu sayfanın `Finished` state'idir: kazanan oyuncu, brüt ödül, %10 komisyon düşülmüş net tutar, otomatik LTC transferinin durumu gösterilir (bkz. "Sayfa State Kuralları") — ekstra navigasyon karmaşası yaratmaz.
- 🛠️ **(State.io incelemesi sonrası eklendi)** ActionPanel artık ayrı bir asker-sayısı input'u içermez, gönderim haritada sürükle-bırak ile yapılır (bkz. `03-game-rules.md` Bölüm 6/15, `04-style.md`); bu sayfanın route/state tablosu değişmiyor, yalnızca `Playing` state'i içindeki etkileşim modeli netleşiyor.

### `/cuzdan`

- Bakiye (`Wallet.BalanceUsd`), yatırma adresi/QR (bakiye yükleme = `MatchId=null` invoice), para çekme formu (`WithdrawalRequest` oluşturur) — bkz. `05-payment.md` Bölüm 1.9.
- Para çekme talebi state'i, `05-payment.md`'deki `WithdrawalRequest.Status` state modeliyle birebir aynı yapıda olmalı (bkz. `06-coding-standards.md` Enum ve State Yönetimi) — `Refund.Status` ile karıştırılmaz, ikisi ayrı state machine'lerdir.

### `/profil`, `/gecmis`

- Geçmiş maçlar, kazanç/kayıp özeti. Salt okunur, iş mantığı içermez.

### `/hesap-ayarlari` 🛠️ **yeni — eksikti, standart bir sayfa**

- `/profil`'in aksine **yazma işlemi içerir**: e-posta/şifre değiştirme formu (auth yöntemine bağlı, bkz. auth ❓), "Hesabımı Sil" aksiyonu. Hesap silme talebi anlık/geri alınamaz bir işlem **değildir** — `06-coding-standards.md`'deki genel disiplinle tutarlı olarak bir onay adımı (modal, "Standart Modal" şablonu — bkz. `04-style.md`) ve arkasında bir `AccountDeletionRequest` durumu (hemen silmek yerine, bekleyen bakiye/aktif maç varsa engellenmesi gerekir — 🛠️ aktif bakiyesi/açık maçı olan bir hesap silinemez, önce `/cuzdan`'dan çekim yapılması istenir) gerektirir. Bu, `06-coding-standards.md` Exception/Guard prensibiyle aynı mantık — beklenen bir kısıt, exception değil, kullanıcıya net bir mesajla gösterilir.

### `/kurallar`

- 🛠️ **Düzeltildi:** Üretim formülü (10 sn'de 4 asker + fethedilen bölge başı +1), savaş/çarpışma mantığı ("+2 asker kuralı"), Standart/VIP oda farkları, bot politikası, giriş ücreti ve kazanç dağılımı — müşterinin verdiği bilgiler birebir yansıtılır, sayı değiştirilmez (bkz. `01-workflow-rules.md` Bölüm 0.5).
- 🛠️ **Eklendi (Bölüm 7'deki "botsuz tanıtım akışı" fikrinin somut yeri — önceki turda belirsiz bırakılmıştı, kesinleştirildi):** Sayfanın altında, kural metninden ayrı bir "Dene" bölümü — tek oyunculu, sabit senaryolu, gerçek bir maça hiç bağlanmayan interaktif bir gösterim (bkz. `03-game-rules.md` Bölüm 7). Bu, sayfanın **Success** state'inin bir parçasıdır, ayrı bir route/auth gerektirmez, ayrı bir loading/error state'i de yoktur (statik içerikle aynı anda render edilir).

### `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler` 🛠️ **yeni — eksikti**

- Statik, tek sütun metin sayfaları (`/kurallar` ile aynı şablon). **İçerik Claude tarafından yazılmaz** — bunlar hukuki/uyumluluk metinleridir, müşteriden veya hukuk danışmanından gelmeden yayına alınmaz. Claude yalnızca route'u, sayfa şablonunu (başlık + statik markdown içerik alanı) ve footer'daki linkleri hazırlar; içerik yer tutucu olarak "İçerik yakında eklenecektir" gibi **açıkça geçici olduğu belirtilen** bir metinle bırakılır (bu, `01-workflow-rules.md` Bölüm 0.4'teki placeholder yasağının istisnasıdır — çünkü burada eksik olan kod değil, hukuki metindir, Claude'un icat etmesi daha risklidir).

### `/destek` 🛠️ **yeni — eksikti**

- Basit bir iletişim formu (konu, açıklama, opsiyonel işlem/invoice ID referansı) + destek e-postası. Gerçek para akışı olan bir sistemde ödeme anlaşmazlıkları için tek başvuru noktasıdır. Form gönderimi bir e-posta servisine veya basitçe bir destek tablosuna (`SupportTicket`) yazılabilir — 🛠️ e-posta entegrasyonu ayrı bir görev, ilk sürümde `SupportTicket` tablosuna yazıp `/admin`'de görünür olması yeterli (YAGNI).
- 🛠️ **Maç itirazı — Gemini'nin önerdiği ayrı `/destek/itiraz/[matchId]` route'u yerine, mevcut forma opsiyonel bir alan olarak eklendi (YAGNI — aynı ihtiyaç için iki ayrı sayfa açmak gereksiz):** `SupportTicket.MatchId` (nullable) alanı eklenir; form `/destek?matchId=...` şeklinde bir query param ile açılırsa (`/game/[matchId]`'in Finished state'inde veya `/gecmis`'teki bir maç satırında "İtiraz Et" linki ile) bu alan otomatik doldurulur, kullanıcı ayrıca ID aramaz. `/admin/maclar`'daki izleyici modu (bkz. o route'un tanımı) zaten bu `MatchId` üzerinden ilgili maçın kaydına bağlanabiliyor — ayrı bir entegrasyon gerekmez, mevcut alan bağlantıyı sağlıyor.

### `/admin`, `/admin/odemeler`, `/admin/maclar`, `/admin/kullanicilar` 🛠️

- 🛠️ Müşteri tarafından hiç talep edilmemiş sayfalar — ancak gerçek parayla, %10 komisyonlu bir sistemde bekleyen para çekme taleplerinin ve başarısız ödemelerin görünür olmaması operasyonel risk oluşturur. **Müşteriden açık onay alınmadan uygulama aşamasına geçilmemeli** (bkz. Bölüm "❓ Müşteriden Doğrulanması Gereken Noktalar").
- `/admin` yalnızca özet metrikler gösterir (bekleyen çekim sayısı, aktif maç sayısı, günlük hacim); detay/aksiyon alt route'larda yapılır — tek bir dev sayfaya her şeyi doldurmak (`06-coding-standards.md`'deki component tekil sorumluluk ilkesiyle de tutarlı olarak) yerine sorumluluk ayrılır.

---

## Sayfa State Kuralları ⭐

Claude her sayfayı yazarken state'leri kendi kafasına göre üretmez — her sayfa **en az** aşağıdaki genel state'leri ele almak zorundadır:

- **Loading** — veri henüz gelmedi.
- **Error** — veri çekilemedi / sunucu hata döndü. Kullanıcıya anlamlı mesaj gösterilir (bkz. `02-architecture.md` "Uç Durumlar ve Hata Yönetimi").
- **Empty** — veri geldi ama gösterilecek içerik yok (uygunsa).
- **Success** — veri geldi, normal render.

**Gerçek zamanlı (SignalR bağlantılı) sayfalar** ayrıca şu bağlantı state'lerini desteklemek zorundadır:

- **Connecting** — bağlantı ilk kez kuruluyor.
- **Connected** — bağlantı kuruldu, veri akıyor.
- **Reconnecting** — bağlantı koptu, yeniden deneniyor (bkz. `02-architecture.md` "Bağlantı kopması → client yeniden bağlandığında state resync edilir").
- **Disconnected** — yeniden bağlanma denemeleri tükendi, kullanıcıya net bir "bağlantı yok" göstergesi + manuel yeniden dene seçeneği sunulur.

### Sayfa Bazlı State Matrisi

| Sayfa                                                                       | Loading            | Error              | Empty                   | Success                                             | Connecting/Reconnecting/Disconnected                                                                                                                    |
| --------------------------------------------------------------------------- | ------------------ | ------------------ | ----------------------- | --------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/lobi`                                                                     | ✔                  | ✔                  | ✔ (açık maç yok)        | ✔                                                   | ✔ (SignalR)                                                                                                                                             |
| `/lobi/[inviteToken]`                                                       | ✔                  | ✔                  | ✔ (token geçersiz/dolu) | ✔                                                   | ✔ (SignalR)                                                                                                                                             |
| `/odeme/[invoiceId]`                                                        | ✔                  | ✔                  | ❌                      | ✔ (durumlar: Bekleniyor / Onaylandı / Süresi Doldu) | ❌ (polling veya webhook-tetiklemeli, sürekli bağlantı değil — bkz. `05-payment.md`)                                                                    |
| `/game/[matchId]`                                                           | ✔ (Connecting)     | ✔                  | ❌                      | ✔ (Playing)                                         | ✔ (SignalR) — ek olarak **Synchronizing** (yeniden bağlanınca state resync ediliyor) ve **Finished** (maç bitti, sonuç gösteriliyor) state'leri zorunlu |
| `/cuzdan`                                                                   | ✔                  | ✔                  | ✔ (hiç işlem yok)       | ✔                                                   | ✔ (bakiye SignalR ile güncelleniyorsa)                                                                                                                  |
| `/profil`, `/hesap-ayarlari`, `/gecmis`                                     | ✔                  | ✔                  | ✔ (hiç maç oynanmamış)  | ✔                                                   | ❌                                                                                                                                                      |
| `/mac/[matchId]`                                                            | ✔                  | ✔                  | ❌                      | ✔                                                   | ❌                                                                                                                                                      |
| `/kurallar`, `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`, `/sss` | ❌ (statik içerik) | ❌                 | ❌                      | ✔                                                   | ❌                                                                                                                                                      |
| `/durum`                                                                    | ✔                  | ❌                 | ❌                      | ✔ (bileşen bazlı yeşil/kırmızı)                     | ❌                                                                                                                                                      |
| `/destek`                                                                   | ❌                 | ✔ (form gönderimi) | ❌                      | ✔                                                   | ❌                                                                                                                                                      |
| `/admin`, `/admin/*`                                                        | ✔                  | ✔                  | ✔                       | ✔                                                   | ✔ (bekleyen taleplerin canlı listesi ise)                                                                                                               |

🛠️ `/game/[matchId]` state akışı önerisi: `Connecting → Synchronizing → Playing → (Reconnecting ↔ Playing) → Finished`. `Disconnected` yalnızca yeniden bağlanma denemeleri tükendiğinde gösterilir; oyun sunucu-otoriter olduğu için (`02-architecture.md`) kullanıcı bağlantısı kopsa da maç sunucuda devam eder, bu yüzden `Disconnected` state'i "maç bitti" anlamına gelmez, yalnızca client'ın senkron olmadığını gösterir.

---

## Yetki Matrisi ⭐

| Route                                                                                                 | Guest (girişsiz)                                      | Player                       | Admin                                            |
| ----------------------------------------------------------------------------------------------------- | ----------------------------------------------------- | ---------------------------- | ------------------------------------------------ |
| `/`                                                                                                   | ✔                                                     | ✔                            | ✔                                                |
| `/kurallar`, `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`, `/sss`                           | ✔                                                     | ✔                            | ✔                                                |
| `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`                                      | ✔                                                     | ➜ `/lobi`'ye yönlendirilir   | ➜ `/lobi`'ye yönlendirilir                       |
| `/lobi`                                                                                               | ❌ → `/giris`                                         | ✔                            | ✔                                                |
| `/lobi/vip-olustur`                                                                                   | ❌ → `/giris`                                         | ✔                            | ✔                                                |
| `/lobi/[inviteToken]`                                                                                 | ❌ → `/giris` (giriş sonrası aynı token'a geri döner) | ✔                            | ✔                                                |
| `/odeme/[invoiceId]`                                                                                  | ❌                                                    | ✔ (yalnızca kendi invoice'ı) | ✔                                                |
| `/game/[matchId]`                                                                                     | ❌                                                    | ✔ (yalnızca kendi maçı)      | ✔ (izleyici/denetim modu — 🛠️ kapsam netleşmeli) |
| `/cuzdan`                                                                                             | ❌                                                    | ✔ (yalnızca kendi cüzdanı)   | ✔                                                |
| `/profil`, `/hesap-ayarlari`, `/gecmis`                                                               | ❌                                                    | ✔ (yalnızca kendi verisi)    | ✔                                                |
| `/mac/[matchId]`                                                                                      | ❌                                                    | ✔ (yalnızca kendi maçı)      | ✔                                                |
| `/durum`                                                                                              | ✔                                                     | ✔                            | ✔                                                |
| `/destek`                                                                                             | ✔ (görüntüleme)                                       | ✔ (form gönderimi)           | ✔                                                |
| `/admin`, `/admin/odemeler`, `/admin/maclar`, `/admin/kullanicilar`, `/admin/destek`, `/admin/loglar` | ❌                                                    | ❌ → `403`                   | ✔                                                |
| `/bakim`                                                                                              | ✔                                                     | ✔                            | ✔                                                |

- "Yalnızca kendi X'i" ifadesi sunucu tarafında doğrulanır — client'a route erişimi verilmesi, o veriye erişim yetkisi anlamına gelmez; her istek sunucuda kullanıcı kimliğine göre filtrelenir (bkz. `02-architecture.md` "Sunucu otoriter olmalı").
- Admin rolünün oyuncu hesaplarından nasıl ayrıldığı (ayrı bir `Role` enum'u, ayrı bir tablo vb.) 🛠️ — müşteri belirtmedi, en basit çözüm `User.Role` enum'u (`Player`, `Admin`) önerilir.

---

## 404 / Error / Bakım Sayfaları ⭐

Gerçek para akan bir sistemde bu sayfaların eksik olması özellikle risklidir — kullanıcı bir hata anında "param ne oldu" belirsizliğiyle baş başa kalmamalı.

- **`not-found.tsx`** (Next.js App Router konvansiyonu) — kök seviyede tanımlanır, geçersiz route'larda gösterilir.
- **`error.tsx`** — segment bazlı (özellikle `game/`, `cuzdan/` altında) tanımlanır; beklenmeyen render/veri hatasında kullanıcıya "bir şeyler ters gitti, bakiyeniz etkilenmedi" gibi güven verici ama abartısız bir mesaj gösterir. 🔒/🛠️ sınırı: bu sayfa **asla** ödeme durumunun kesin olarak ne olduğunu tahmin ederek yazmaz (ör. "ödemeniz alındı" gibi doğrulanmamış bir ifade kullanmaz) — yalnızca "durumu kontrol ediyoruz" der, gerçek durum sunucudan resync edilerek gösterilir.
- **`/bakim`** — BTCPay/SignalR gibi kritik bir bağımlılık planlı/plansız kapalıyken tüm auth'lu route'lar buraya yönlendirilir. 🛠️ Bu müşteriden talep edilmedi ama ödeme altyapısı olan bir sistemde "sistem kapalı" ile "hata" durumlarının kullanıcıya farklı gösterilmesi (biri geçici/planlı, diğeri beklenmeyen) güven açısından önemli — onay gerektirir.
- 🛠️ **403/429/500 durumları — ChatGPT incelemesinde önerilen, kabul edilen madde (önceki turlarda yalnızca 404/genel error vardı):** Ayrı route/dosya **açılmaz** (YAGNI — Next.js'te bunlar `error.tsx` içinde HTTP status koduna göre dallanır, üçü için üç ayrı sayfa dosyası gerekmez):
  - **403 (yetkisiz):** "Bu sayfaya erişim yetkiniz yok" + `/lobi`'ye dön butonu. Hesap askıya alınmışsa (bkz. `/admin/kullanicilar`) genel 403 yerine özel bir mesaj gösterilir: askıya alınma sebebi (varsa), `/destek`'e direkt link — kullanıcı boş bir 403 ile baş başa bırakılmaz (ChatGPT'nin "hesap kilitli" önerisi burada, ayrı bir route açmadan karşılanır).
  - **429 (rate limit):** `PlayerActionRateLimitPerSecond` guard'ının (bkz. `03-game-rules.md` Bölüm 11) client karşılığı — "çok hızlı işlem yapıyorsunuz, birkaç saniye bekleyin" mesajı, otomatik yeniden dener.
  - **500 (beklenmeyen hata):** Yukarıdaki `error.tsx` davranışıyla aynı — "durumu kontrol ediyoruz" tonuyla, ödeme durumu hakkında tahmin yapmadan.

---

## Sayfa Sorumluluğu ⭐

Her `page.tsx` mümkün olduğunca ince tutulur.

Bir sayfanın sorumluluğu yalnızca:

- Veriyi başlatmak
- Gerekli componentleri birleştirmek
- Layout oluşturmak
- Gerekli store'ları başlatmak

olmalıdır.

İş kuralları (`iş mantığı`) hiçbir zaman `page.tsx` içinde yazılmaz.

İş mantığı ilgili modülün:

- Services
- Store
- API Client

katmanında bulunur.

Bu kural `06-coding-standards.md` dosyasındaki "Kod Tekrarını Önleme" ve `02-architecture.md` dosyasındaki katman ayrımıyla birebir uyumludur.

---

## Layout Yapısı ⭐

Next.js App Router'ın nested layout yapısı kullanılır:

- **`RootLayout`** (`app/layout.tsx`) — tüm sayfalarda ortak: temel HTML/font/tema, genel `Header`/`Footer` (bkz. Navigasyon).
- **`GameLayout`** (`app/game/layout.tsx`) — `Header`/`Footer` içermez (bkz. Navigasyon — oyun ekranı sade kalmalı), yalnızca minimal bağlantı durumu göstergesi taşır.
- **`AdminLayout`** (`app/admin/layout.tsx`) — kendi `Sidebar`'ı olan, oyuncu tarafı navigasyonundan tamamen ayrı bir kabuk.
- Auth gerektiren sayfalar (`(auth)` route group'u altında toplanmıyorsa) kendi içlerinde bir `AuthGuard` component'i ile korunur; bu, her sayfanın kendi içine auth kontrolü kopyalamasını önler (bkz. `06-coding-standards.md` "Kod Tekrarını Önleme").

## Route Group Kullanımı ⭐

🛠️ Öneri: aşağıdaki route group'ları kullanılır (URL'yi etkilemez, yalnızca layout/organizasyon amaçlıdır):

- `(public)` — `/`, `/kurallar`, `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`, `/sss`, `/destek`, `/durum`, `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`, `/bakim`
- `(player)` — `/lobi`, `/lobi/vip-olustur`, `/lobi/[inviteToken]`, `/odeme/[invoiceId]`, `/cuzdan`, `/profil`, `/hesap-ayarlari`, `/gecmis`, `/mac/[matchId]`
- `game` (group değil, gerçek segment — çünkü `[matchId]` dinamik parametresi taşıyor ve kendi layout'u var)
- `admin` (aynı şekilde gerçek segment, `AdminLayout` için; `/admin/odemeler`, `/admin/maclar`, `/admin/kullanicilar` bu segmentin alt sayfalarıdır, ayrı bir route group gerekmez)

Bu netleşmezse Claude kendi kararına göre gruplama yapar; bu bölüm o belirsizliği kapatır.

---

## Next.js Özel Dosyalar (`loading.tsx` / `error.tsx` / `not-found.tsx`) ⭐

Her route segmenti için, veri çekimi olan sayfalarda (`/lobi`, `/game/[matchId]`, `/cuzdan`, `/profil`, `/hesap-ayarlari`, `/gecmis`, `/admin`) **`loading.tsx` kullanılır** — "Sayfa State Kuralları"ndaki `Loading` state'inin Next.js seviyesindeki karşılığıdır, component içinde ayrıca elle bir loading state'i kurmakla çelişmez, ikisi birlikte kullanılabilir (ilk yüklemede `loading.tsx`, sonraki client-side güncellemelerde component içi state).

`error.tsx` yukarıdaki "404 / Error / Bakım" bölümünde tanımlandığı gibi segment bazlı kullanılır.

---

## Metadata / SEO ⭐

Yalnızca **herkese açık, indexlenmesi istenen** sayfalarda (`/`, `/kurallar`) `generateMetadata` ile `title`/`description`/`open graph` tanımlanır.

Auth gerektiren sayfalarda (`/lobi`, `/cuzdan`, `/profil`, `/game/*`, `/admin`) `robots: { index: false, follow: false }` set edilir — gerçek para/bakiye içeren sayfaların arama motorunda indexlenmesi istenmez.

Favicon/genel site adı `RootLayout`'ta bir kez tanımlanır, sayfa bazlı tekrar edilmez.

---

## Navigasyon ⭐

| Sayfa                                                                                                                     | Header                      | Footer | Sidebar | Mobile Menü                                                                                                                                                          |
| ------------------------------------------------------------------------------------------------------------------------- | --------------------------- | ------ | ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/`, `/kurallar`, `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`, `/destek`, `/durum`                   | ✔                           | ✔      | ❌      | ✔                                                                                                                                                                    |
| `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`, `/sss`                                                            | ✔ (yalnızca logo/geri)      | ✔      | ❌      | ✔ — 🛠️ bu dördü yalnızca footer linkiyle erişilir, ana navigasyon menüsünde yer kaplamaz (client'ın "sade" talebiyle tutarlı), ama footer'da **her sayfada** bulunur |
| `/lobi`, `/lobi/vip-olustur`, `/lobi/[inviteToken]`, `/cuzdan`, `/profil`, `/hesap-ayarlari`, `/gecmis`, `/mac/[matchId]` | ✔                           | ✔      | ❌      | ✔                                                                                                                                                                    |
| `/game/[matchId]`                                                                                                         | ❌                          | ❌     | ❌      | ❌ — oyun ekranı sade kalacak (müşteri: "tasarım olarak biraz farklılaştıracağız, daha sade basit olacak"), header/footer harita/HUD alanından yer çalar             |
| `/admin`, `/admin/*`                                                                                                      | ❌ (kendi minimal header'ı) | ❌     | ✔       | ❌ (admin masaüstü öncelikli varsayılır — 🛠️)                                                                                                                        |

Bu tablo doğrudan müşterinin "daha sade basit olacak" notuyla ilişkilendirildiği için `04-style.md` ile çelişen bir detay çıkarsa `04-style.md` esas alınır.

🛠️ **Footer içeriği — kesinleştirildi:** `RootLayout`'taki footer, oyun ekranı (`/game/*`) hariç **her sayfada** şu linkleri taşır: Kurallar, Kullanım Şartları, Gizlilik Politikası, Sorumlu Oyun, Destek. Bu, gerçek para taşıyan bir platformda yasal sayfaların "bulunabilir" olması için asgari bir gerekliliktir — yalnızca `/kayit` formundaki onay kutusuna gömülü kalması yeterli değildir.

---

## Breadcrumb ⭐

🛠️ Yalnızca iç içe/derin sayfalarda anlamlı: `/profil`, `/hesap-ayarlari`, `/gecmis`, `/cuzdan` gibi tek seviyeli sayfalarda breadcrumb'a gerek yok (Header navigasyonu yeterli). Breadcrumb yalnızca `/odeme/[invoiceId]` gibi bir üst akıştan (`/lobi` → ödeme) gelindiğinde, "Lobi > Ödeme" şeklinde kısa bir geri-iz olarak kullanılır. Ayrı bir component olarak zorunlu tutulmaz, düşük öncelikli bir UI detayıdır.

---

## Route Geçiş Akışı (Oyun Girişi) ⭐

> 🔔 **Mimari karar — kod kazandı, doküman güncellendi (docs/09-eksik-tarama.md denetimi, Faz 6):** Bu bölüm önceden "Katıl" sonrası oyuncunun oda dolana kadar `/lobi`'de bir bekleme state'inde kaldığını, yalnızca oda dolunca `/game/[matchId]`'e yönlendirildiğini tarif ediyordu. Gerçek kod (`web/app/(site)/lobi/page.tsx`, `web/app/(site)/lobi/[inviteToken]/page.tsx`, `web/app/(site)/odeme/[invoiceId]/page.tsx`) bunun yerine "Joined" sonucunda oyuncuyu **her zaman doğrudan** `/game/[matchId]`'e yönlendirir — bekleme ekranı (oyuncu listesi, "X/N oyuncu", "Son oyuncu bekleniyor", İptal Et/Beklemeye Devam Et) `/game/[matchId]`'in kendi `Lobby`/`Countdown` state'i içinde gösterilir (bkz. `08-page-content.md` Bölüm 3.8 ve Bölüm 3.4'teki not). Bu tercih zaten inşa edilmiş, test edilmiş bir davranışı geri almaktan daha düşük riskli olduğu için korunmuştur — `/lobi`, `/lobi/[inviteToken]` ve `/odeme/[invoiceId]` yalnızca **giriş noktalarıdır**, bekleme ekranının kendisi değildir.

Bir oyuncunun maça giriş yaptığı uçtan uca akış:

```
/lobi (maç seç, "Katıl")
   │
   ▼
Bakiye yeterli mi?
   │                          │
   │ Evet                     │ Hayır
   ▼                          ▼
Doğrudan maça             /odeme/[invoiceId]
katılım kaydı              (BTCPay invoice, LTC bekleniyor)
   │                          │
   │                          ▼
   │                     Onaylandı → bakiye güncellenir
   │                          │
   └──────────────┬───────────┘
                   ▼
         /game/[matchId]  (Lobby state: "X/N oyuncu" bekleme ekranı — bkz. 08-page-content.md Bölüm 3.8)
                   │
                   ▼  (oda dolunca sunucu Countdown'a, ardından maçı Playing'e geçirir)
         /game/[matchId]  (Connecting → Synchronizing → Playing)
                   │
                   ▼ (maç biter)
         /game/[matchId] içinde Finished state'i
         (kazanan, net ödül, otomatik LTC transfer durumu)
                   │
                   ▼
         /gecmis veya /profil'e dönüş (opsiyonel yönlendirme)
```

Bu akış `05-payment.md`'deki invoice/webhook mekanizmasıyla, `03-game-rules.md`'deki maç başlatma koşuluyla (🛠️ kesinleşti: oda ayarındaki oyuncu sayısı N dolunca — Standart’ta varsayılan 4, VIP’de kurucunun seçtiği 2-12 arası değer) tutarlı olmalıdır; burada yalnızca sayfa geçişleri gösteriliyor, tetikleme koşulunun kesin kuralı ilgili modül dosyasında tanımlıdır.

---

## Client / Server Component Kuralları ⭐

Varsayılan olarak tüm componentler **Server Component** kabul edilir.

Aşağıdaki durumlarda `"use client"` kullanılır:

- SignalR
- useState
- useEffect
- Event Handler
- Store (Zustand vb.)
- Tarayıcı API'leri

Bunun dışındaki componentler Server Component olarak bırakılır.

Gereksiz yere tüm sayfaya `"use client"` eklenmez.

---

## Store ve Veri Sahipliği ⭐

Her modül yalnızca kendi state'ini yönetir.

Örnek:

- Lobby → Lobby Store
- Game → Game Store
- Payment → Payment Store

Bir store başka modülün state'ini doğrudan değiştirmez.

Ortak bilgi gerekiyorsa API veya SignalR üzerinden yeniden alınır.

Bu kural `02-architecture.md` dosyasındaki "Modüller arası izolasyon" ilkesinin frontend karşılığıdır.

---

## Route Yönlendirme Kuralları ⭐

Bir route artık geçerli değilse kullanıcı uygun ekrana yönlendirilir.

Örnekler:

- Süresi dolmuş invoice → `/lobi`
- Tamamlanmış ödeme → `/lobi`
- Bitmiş maç → Sonuç ekranı veya Finished State
- Bulunamayan maç → `404`
- Yetkisiz erişim → `403`

Yönlendirme yalnızca kullanıcı deneyimi amaçlıdır.
Sunucu doğrulaması her zaman devam eder.

---

## Sayfa Performans Kuralları ⭐

İlk yüklemede yalnızca gerekli veriler alınır.

Aşağıdaki kurallar uygulanır:

- Büyük listeler sayfalama veya lazy loading kullanır.
- Kullanılmayan veri ilk yüklemede indirilmez.
- Gerçek zamanlı olmayan veriler tekrar tekrar fetch edilmez.
- SignalR yalnızca gerçekten ihtiyaç duyulan sayfalarda başlatılır.

---

## API Çağrı Kuralları ⭐

GET istekleri gerektiğinde tekrar denenebilir.

POST / PUT / DELETE işlemleri otomatik tekrar edilmez.

State değiştiren işlemler idempotency kurallarına uygun olmalıdır.

Yazma işlemleri kullanıcı haberi olmadan ikinci kez gönderilmez.

Bu kural `06-coding-standards.md` dosyasındaki Idempotency bölümüyle birlikte uygulanır.

---

## Sayfa Dosya Yapısı ⭐

Her route mümkün olduğunca aşağıdaki yapıyı takip eder.

```
app/

game/
    layout.tsx
    [matchId]/
        page.tsx
        loading.tsx
        error.tsx

lobi/
    page.tsx
    loading.tsx

cuzdan/
    page.tsx
    loading.tsx

profil/
    page.tsx
```

Gereksiz dosya oluşturulmaz.

Her route yalnızca ihtiyaç duyduğu Next.js özel dosyalarını içerir.

---

## Suspense Kullanımı ⭐

Server Component veri yüklemelerinde mümkün olduğunda React Suspense kullanılır.

İlk yükleme için `loading.tsx`,
sonraki client güncellemeleri için component içi loading state'i tercih edilir.

İkisi birbirinin yerine değil,
birlikte kullanılabilir.

---

## Claude İçin Sayfa Oluşturma Sırası ⭐

Yeni bir route yazarken aşağıdaki sıraya uyulur.

1. Route oluşturulur.
2. Layout belirlenir.
3. Auth kontrolü eklenir.
4. Veri kaynağı belirlenir (REST / SignalR).
5. Store bağlantısı yapılır.
6. Loading / Error / Empty state'leri hazırlanır.
7. Component'ler oluşturulur.
8. Gerçek zamanlı bağlantılar eklenir.
9. Route build edilip test edilir.
10. Sonraki route'a geçilir.

Bir sonraki route'a geçmeden önce mevcut route çalışır durumda olmalıdır.

---

## Sayfa Bazlı Veri Kaynağı ⭐

| Sayfa                                                                       | REST                                                       | SignalR                                                                                                                                   | Cache (client-side)                                      |
| --------------------------------------------------------------------------- | ---------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| `/`                                                                         | ❌ (statik içerik)                                         | ❌                                                                                                                                        | ❌                                                       |
| `/lobi`                                                                     | ✔ (ilk yükleme — açık maç listesi)                         | ✔ (canlı güncelleme)                                                                                                                      | ❌ (gerçek zamanlı veri cache'lenmez)                    |
| `/lobi/[inviteToken]`                                                       | ✔ (token doğrulama, parola, katılım)                       | ❌ (bekleme ekranı bu sayfada değil — bkz. 🔔 Route Geçiş Akışı notu; katılım sonrası `/game/[matchId]`'e yönlenir, SignalR orada başlar) | ❌                                                       |
| `/odeme/[invoiceId]`                                                        | ✔ (invoice oluşturma/sorgulama)                            | ❌ (webhook → REST polling veya server-sent güncelleme, sürekli SignalR bağlantısı gerekmez — bkz. `05-payment.md`)                       | ❌                                                       |
| `/game/[matchId]`                                                           | ❌ (maça katılım sonrası tüm state SignalR üzerinden akar) | ✔                                                                                                                                         | ❌                                                       |
| `/cuzdan`                                                                   | ✔ (bakiye, işlem geçmişi)                                  | ✔ (bakiye değişimi anlık yansısın isteniyorsa)                                                                                            | ❌                                                       |
| `/profil`, `/hesap-ayarlari`, `/gecmis`                                     | ✔                                                          | ❌                                                                                                                                        | ✔ (nadiren değişen veri, kısa süreli client cache uygun) |
| `/kurallar`, `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`, `/sss` | ❌ (statik/markdown içerik)                                | ❌                                                                                                                                        | ✔ (statik)                                               |
| `/destek`                                                                   | ✔ (form gönderimi)                                         | ❌                                                                                                                                        | ❌                                                       |
| `/admin`, `/admin/*`                                                        | ✔                                                          | ✔ (bekleyen taleplerin canlı listesi)                                                                                                     | ❌                                                       |

Bu tablo, `store.ts`'nin (bkz. `02-architecture.md`) hangi veri için REST fetch, hangi veri için SignalR subscription kullanacağını netleştirir — Claude'un veri çekme kararını sayfa yazarken kendi kafasına göre vermesini engeller.

---

## Route Parametre Kuralları ⭐

Dinamik route parametreleri (`matchId`, `invoiceId` vb.) hiçbir zaman güvenilir veri olarak kabul edilmez.

Sunucu tarafında her zaman aşağıdaki doğrulamalar yapılır:

- Parametre formatı doğrulanır.
- İlgili kayıt gerçekten var mı kontrol edilir.
- Kullanıcının o kaynağa erişim yetkisi doğrulanır.
- Geçersiz parametre → `404`
- Yetkisiz erişim → `403`
- Geçersiz durum geçişi → uygun hata mesajı

Client tarafındaki route koruması yalnızca kullanıcı deneyimi içindir.
Gerçek doğrulama her zaman backend tarafından yapılır.

## ❓ Müşteriden Doğrulanması Gereken Noktalar

- ~~Giriş ücreti Euro/Dolar belirsizliği~~ 🛠️ **Çözüldü:** Müşterinin WinToWar mesajı "Bakiye Birimi: USD ($)" olarak net karar vermiş, tüm sayfalarda USD kullanılır, bu madde artık açık değil.
- ~~Şifreli/özel davet odalara davet linki mi, oda kodu mu~~ 🛠️ **Çözüldü (v2 — v1'deki karar hatalıydı, düzeltildi):** İlk kararda yalnızca davet linki (parolasız) uygulanmıştı, bu müşterinin "şifreli" kelimesini atlıyordu. Artık her ikisi birlikte: oda herkese açık listede görünmez (davet linkiyle bulunur) **ve** o linkte de ayrıca parola istenir (bkz. `03-game-rules.md` Bölüm 2.2 "DÜZELTME").
- Auth yöntemi (email/parola mı, yalnızca cüzdan adresi ile giriş mi) belirtilmemiş — bu netleşmeden `/sifremi-unuttum` akışının gerekip gerekmediği de netleşmez (cüzdan-bağlama ile girişte şifre kavramı olmayabilir).
- `/admin`, `/admin/odemeler`, `/admin/maclar`, `/admin/kullanicilar` sayfaları ve admin rolünün varlığı müşteriden talep edilmedi, öneri olarak eklendi — onay gerekiyor.
- `/bakim` sayfası aynı şekilde öneri — onay gerekiyor.
- Admin'in `/game/[matchId]`'i izleyici modunda görüp göremeyeceği netleşmemiş.
- Standart odanın oyuncu sayısının gerçekten 4 mü olması gerektiği (bkz. `03-game-rules.md` Bölüm 14, madde 1) — bu netleşmeden `/lobi`'deki Standart sekme metni geçici varsayımla ilerliyor.
- VIP oda giriş ücretinin alt/üst sınırı var mı (kurucu $0.01 veya $10.000 gibi uç bir değer girerse ne olur) — belirtilmedi, ❓ netleşmeli.
- 🛠️ **Yeni eklenenler (gerçek para/kripto platformu olduğu için gerekli görüldü, müşteri hiç değinmedi):**
  - `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler` içeriği kimden gelecek (hukuk danışmanı/müşteri) — Claude bu metinleri **yazmaz**, yalnızca placeholder route hazırlar.
  - Kimlik doğrulama (KYC) gerekiyor mu — özellikle belirli bir çekim tutarının üzerinde. Şu an tasarımda yok, ❓ launch öncesi netleşmeli.
  - Bölgesel/ülke kısıtlaması var mı (bazı ülkelerde gerçek parayla strateji/beceri oyunları düzenlemeye tabidir) — bu bir sayfa kararı değil, bir iş/hukuk kararıdır, müşteriyle ayrıca konuşulmalı.
  - `/destek` formunun e-posta bildirimi mi yoksa yalnızca `/admin`'de görünür bir kayıt mı olacağı — ilk sürümde ikincisiyle başlanıyor (YAGNI), e-posta entegrasyonu ayrı bir görev.
