# 21 — Ödeme Sistemi: Sandbox Ortamı + Uçtan Uca Doğrulama ve Düzeltme

> **Dosya adı notu:** Bu dosyayı `docs/21-payment-sandbox-e2e.md` olarak kaydedin.
>
> ⚠️ **Numara seçimi bilinçlidir:** `18`, `19`, `20` numaraları `03-game-rules.md`/`04-style.md` içinde geçmiş/harici
> oyun belgelerine atıf olarak kullanılmış durumda (bkz. `CLAUDE.md`'deki "Dosya numarası çakışması uyarısı").
> Yeni bir çakışma yaratmamak için bu dosya `21` alır.
>
> `CLAUDE.md`'nin "Her görevde önce oku" tablosunda **`docs/14-payment-sandbox.md` ve `docs/15-payment-flow-verification.md`
> satırları bu dosyanın satırıyla değiştirilir:**
> _"Ödeme sandbox ortamı + ödeme akışının uçtan uca doğrulanması/düzeltilmesi → `docs/21-payment-sandbox-e2e.md`
> (14 ve 15'in yerine geçer; `05-payment.md`'nin üzerine inşa eder, oradaki 🔒 iş kurallarını değiştirmez)."_

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla ilerle,
  kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ❓ Müşteriden ileride doğrulanması gereken nokta.

---

## 0. Bu dosya neden var — 14 ve 15'in yerine geçiyor 🔒

`docs/14-payment-sandbox.md` (ortam kurulumu) ve `docs/15-payment-flow-verification.md` (akış doğrulama) aynı işi
iki parçaya bölmüştü, ama **üç noktada birbiriyle çelişiyorlardı** ve ikisi de henüz uygulanmamıştı. Bu dosya
çelişkileri çözerek ikisini birleştirir:

| Çelişki | `14` ne diyordu | `15` ne diyordu | 🔒 **Bu dosyadaki karar** |
| --- | --- | --- | --- |
| Birincil sandbox | `testnet.demo.btcpayserver.org`, regtest yedek | Self-hosted **regtest** birincil | **Regtest birincil** (gerekçe Bölüm 2) |
| Kullanıcı katılımı | "Kayıt ol, store aç, API key üret" (elle) | "Kullanıcıdan hesap açması **istenmez**, agent kurar" | **Agent kurar, kullanıcıdan hiçbir hesap/kayıt istenmez** |
| Kod değişikliği | `PaymentService`/`PayoutService`'e **dokunulmaz** | Bug bulunursa **düzeltilir** | **Bug bulunursa düzeltilir** (Bölüm 7 sınırlarıyla) |

Bu üç karar, müşterinin bu görevi tarif ederken kullandığı iyzico analojisinden türer (Bölüm 1) — dolayısıyla
🔒'dır, "daha kolay" gerekçesiyle değiştirilemez.

## 1. Müşterinin hedefi — iyzico modeli 🔒

Müşterinin daha önceki projelerdeki çalışma şekli aynen korunacak, yalnızca sağlayıcı iyzico yerine BTCPay:

1. **Geliştirme/test:** Sandbox ortamı. Gerçek para yok, ama **gerçek sağlayıcıyla gerçek protokol üzerinden**
   konuşuluyor. iyzico'da bu "sahte test kartları"ydı; BTCPay'de karşılığı **regtest ağı ve "fake payment" /
   "mine block" butonları**dır.
2. **Yayına alma:** Kodda **tek satır değişmeden**, yalnızca config/API bilgileri gerçek hesapla değiştirilerek
   production'a geçiş.

🔒 **Bu görevin başarı ölçütü bu iki maddedir.** Sandbox'ın "kurulmuş olması" yetmez; sandbox üzerinde para
**alındığı ve gönderildiği** fiilen kanıtlanmalı, ve production'a geçişin gerçekten config-only olduğu
Bölüm 8'deki testle gösterilmelidir.

## 2. Neden regtest — testnet değil 🛠️

`14-payment-sandbox.md` birincil olarak `testnet.demo.btcpayserver.org`'u öneriyordu. Bu, müşterinin iyzico
sandbox beklentisini **karşılamıyor**, çünkü:

- Uptime garantisi yok (resmî olarak yalnızca test amaçlı) — CI'da/her geliştirici makinesinde tekrarlanabilir değil.
- Faucet'e bağımlı: testnet LTC bulmak dış bir servise, onun anlık durumuna ve bekleme süresine bağlı. iyzico'da
  test kartı her zaman elinizin altındaydı; faucet öyle değil.
- Blok süresi gerçek: confirmation beklemek dakikalar alır, deterministik değil.
- Manuel kayıt/store açma gerektirir — müşteriden istenmeyecek olan tam da bu.

🔒 **Karar: birincil ve varsayılan sandbox, projeye ait self-hosted BTCPay regtest instance'ıdır.** Regtest'te
"fake payment" ve "mine block" butonları, iyzico'nun test kartlarının birebir karşılığıdır: anında, deterministik,
dış bağımlılıksız, sınırsız tekrarlanabilir.

🛠️ `testnet.demo` yalnızca bu ortamda Docker/regtest **fiilen kurulamıyorsa** ikincil alternatiftir; bu duruma
düşülürse gerekçesi rapora yazılır ve sonuç en fazla PARTIAL olur (Bölüm 9).

---

## 3. Aşama 1 — Mevcut durumun denetimi (kod yazmadan önce) 🛠️

`01-workflow-rules.md` Bölüm 0.1 gereği aşamalı ilerlenir. Bu ilk aşamada **hiçbir kod değiştirilmez**, yalnızca
mevcut durum tespit edilir ve rapora yazılır:

1. `Program.cs`'de provider seçimi hangi koşula bağlı, `FakePaymentProvider` ne zaman devreye giriyor?
2. `BtcPayGreenfieldProvider.cs` gerçekte hangi Greenfield endpoint'lerini çağırıyor? (Invoice oluşturma,
   invoice sorgulama, payout oluşturma/işleme — tam liste çıkarılır; Bölüm 4'teki API key izinleri buna göre
   belirlenecek.)
3. `PaymentWebhooksController` + `WebhookSignatureValidator` hangi event tiplerini işliyor, imzayı nasıl
   doğruluyor?
4. `PaymentInvoice.Status` / `Payout.Status` / `PayoutRecipient.Status` / `Refund.Status` / `WithdrawalRequest`
   state makineleri kodda `05-payment.md` Bölüm 5'teki tanımla birebir örtüşüyor mu?
5. `api.Tests/` altında ödeme testleri var mı, geçiyorlar mı?
6. **Bilinen açık:** README, `BtcPayGreenfieldProvider`'ın canlı bir BTCPay instance'ına karşı **hiç
   doğrulanmadığını** belirtiyor. Bu görev tam olarak bu açığı kapatır — dolayısıyla "zaten çalışıyordur"
   varsayımıyla hiçbir adım atlanmaz.

Bu aşamanın çıktısı: mevcut durumun kısa bir envanteri + "kodu okuyunca şüpheli görünen" noktaların listesi. Bu
liste bir bulgu değildir, yalnızca Aşama 5-6'da özellikle test edilecek adayları işaretler — kod okuyarak
"çalışıyor/çalışmıyor" **hükmü verilmez**.

## 4. Aşama 2 — Sandbox altyapısı 🔒

🛠️ **Karar — sandbox proje varlığıdır, kişisel bir hesap değil:** BTCPay regtest instance'ı `docker-compose` ile,
depo içinde versiyonlanan bir tanımdan ayağa kaldırılır. Kullanıcıdan hesap açması, URL vermesi veya bir kayıt
bilgisi girmesi **istenmez**. Sandbox silinmiş/kaybolmuşsa aynı tanımdan **yeniden** kurulur.

- Dosya konumu 🛠️: `sandbox/btcpay/` (repo kökünde). Bu, `02-architecture.md`'deki "yeni modül üst-düzey klasör
  açmaz" kuralının ihlali **değildir** — o kural uygulama kodu (`api/`, `web/`) içindir; bu bir uygulama modülü
  değil, geliştirme ortamı altyapısıdır ve `api/`/`web/` ağacına karışması yanlış olurdu. Gerekçe commit
  mesajına yazılır.
- İçerik: `docker-compose.yml` (BTCPay + NBXplorer + Litecoin regtest node), kurulum/sıfırlama betiği, ve bu
  ortamın nasıl ayağa kaldırılacağını anlatan kısa bir `README.md`.
- 🔒 **Tekrarlanabilirlik zorunlu:** Tek bir komutla (ör. `./sandbox/btcpay/up.sh`) sıfırdan çalışır hale gelmeli;
  "elle şu ekrandan şunu tıkla" gibi belgelenmemiş manuel adım kalmamalı. Store oluşturma, hot wallet bağlama ve
  API key üretimi Greenfield API üzerinden betikle otomatikleştirilir. Yalnızca BTCPay'in API'den yapılmasına izin
  vermediği bir adım kalırsa, o adım betikte açık bir komutla veya belgelenmiş tek bir manuel adım olarak
  bırakılır ve rapora **neden otomatikleştirilemediği** yazılır.
- Webhook erişimi: BTCPay konteynerden `localhost`'a ulaşamayacağı için tünelleme (ngrok/Cloudflare Tunnel) veya
  compose ağı içinde API'ye doğrudan erişilebilir bir host adı kullanılır. 🛠️ Hangisinin seçildiği ve neden
  seçildiği rapora yazılır.

### 4.1 API key izinleri — asgari yetki 🔒

API key, Aşama 1'de çıkarılan **gerçekten çağrılan endpoint listesine** göre üretilir. Her endpoint'in gerektirdiği
izin BTCPay'in resmî Greenfield dokümantasyonundan **doğrulanır, varsayılmaz**. "Belki lazım olur" gerekçesiyle
fazladan izin (özellikle `canmodifystoresettings` gibi geniş kapsamlılar) verilmez. Webhook tanımı API key
üzerinden değil BTCPay panelinden/betikten yapıldığı için webhook yönetimi izni gerekmez — gerekiyorsa neden
gerektiği rapora yazılır.

### 4.2 Secrets 🔒

`06-coding-standards.md`'nin "Secrets / Hassas Bilgi Yönetimi" kuralı sandbox için de **istisnasız** geçerlidir
(gerçek para taşımaması bir gerekçe değildir). Regtest API key/webhook secret'ı kod içine, `appsettings.json`'a
veya commit'e girmez; `dotnet user-secrets` veya `.gitignore`'lu bir dosya kullanılır ve ignore edildiği
`git check-ignore` ile **fiilen doğrulanır** ("muhtemelen ignore edilmiştir" kabul edilmez).

## 5. Aşama 3 — Ortam anahtarı: üç mod 🛠️

`14-payment-sandbox.md` bunun için `UseFakeProviderInDevelopment` adlı bir boolean öneriyordu. 🛠️ **Bu karar
değiştirildi** (14 bir 🛠️ mühendislik varsayımıydı, 🔒 müşteri kararı değil — dolayısıyla değiştirilebilir):

Boolean, aslında **üç** durumu olan bir dünyayı ifade edemiyor ve "Development'ta fake kullan" adı ortam ile modu
birbirine yapıştırdığı için sandbox'ı ortamdan bağımsız çalıştırmayı zorlaştırıyor. Bunun yerine tek bir enum:

```
PaymentConfig.Mode : PaymentProviderMode { Fake, Sandbox, Live }
```

- `Fake` — `FakePaymentProvider`. Ağa hiç çıkmaz. Günlük geliştirmenin varsayılanı, hızlı kalır.
- `Sandbox` — gerçek `BtcPayGreenfieldProvider`, regtest BTCPay'e bağlı. **Müşterinin istediği iyzico-sandbox karşılığı.**
- `Live` — gerçek `BtcPayGreenfieldProvider`, mainnet store'a bağlı.

Kurallar:

- 🔒 **`Sandbox` ve `Live` arasındaki tek fark config değerleridir** (`BtcPayBaseUrl`/`ApiKey`/`StoreId`/`WebhookSecret`).
  Kodda `if (mode == Sandbox)` şeklinde **davranış dallanması yazılmaz** — yazılırsa production'a geçiş artık
  config-only olmaz ve Bölüm 1'deki hedef çöker. Tek istisna provider seçiminin kendisidir (`Fake` mi gerçek mi).
- 🔒 **Fail-fast:** `Live` modunda BTCPay config alanlarından biri boşsa uygulama **başlamaz**, açık hata fırlatır.
  Sessiz fallback (ör. eksik config'de `Fake`'e düşmek) **kesinlikle yasaktır** — gerçek para taşıyan bir sistemde
  sahte provider'la sessizce prod'a çıkmak en tehlikeli senaryodur.
- 🛠️ `Mode` tanımsızsa varsayılan `Fake`'tir (en güvenli varsayılan).
- Enum, `06-coding-standards.md`'nin "Enum ve State Yönetimi" kuralına uyar — string karşılaştırma yapılmaz.

Bu aşama sonunda `dotnet build` alınır, `Fake` modunda mevcut akışın hâlâ eskisi gibi çalıştığı doğrulanır
(regresyon), sonra sonraki aşamaya geçilir.

## 6. Aşama 4 — Yön A: Müşteri para yatırabiliyor mu (top-up) 🔒

Aşağıdakiler kod okunarak değil, **fiilen çalıştırılarak** doğrulanır. Her adımın kanıtı (invoice ID, TXID, log
satırı, before/after değer) rapora yazılır.

1. `/cuzdan` üzerinden top-up başlat → regtest BTCPay'de gerçek bir invoice oluşuyor mu (dashboard'da ID görünüyor mu)?
2. Frontend'e dönen adres/QR/checkout linki doğru mu; yükleniyor durumunda takılma, yanlış state gösterimi var mı?
3. Regtest "fake payment" + "mine block" ile ödeme gönder → webhook `PaymentWebhooksController`'a ulaşıyor mu,
   `WebhookSignatureValidator` imzayı geçerli kabul ediyor mu?
4. **Geçersiz imza:** Kasten bozulmuş imzalı bir webhook **reddediliyor** mu? (Geçerli imza kadar önemli — imza
   doğrulaması "her şeyi kabul et" şeklinde çalışıyorsa test 3 de yanlışlıkla geçer.)
5. `InvoiceSettled` geldiğinde `PaymentInvoice.Status` doğru enum değerine geçiyor, `Wallet.BalanceUsd` doğru
   miktarda artıyor mu? (before/after yazılır.)
6. **Duplicate webhook:** Aynı payload iki kez gönderilirse bakiye iki kez artıyor mu? Artıyorsa bu
   `06-coding-standards.md` idempotency kuralının ihlalidir → düzeltilir → senaryo tekrar koşulur.
7. **Sıra dışı/eski webhook:** Geriye dönük (stale/out-of-order) bir event geldiğinde `05-payment.md` Bölüm 5.4
   "Monotonluk Kuralı" gerçekten uygulanıyor mu — state geriye gidiyor mu?
8. **Expired/invalid invoice:** Süresi dolan veya settle olmadan geçersizleşen invoice doğru state'e düşüyor,
   kullanıcıya SignalR üzerinden doğru yansıyor mu?
9. **Tutar doğrulaması:** Kurdan hesaplanan beklenen tutar ile gerçek settlement tutarı nasıl karşılaştırılıyor?
   Overpayment/underpayment toleransı (`PaymentToleranceRate`, `RefundOverpaymentThresholdUsd`) gerçek BTCPay
   payload'ıyla uyumlu çalışıyor mu?

### 6.1 İki bilinen tuzak — bunlara geri dönülmez 🔒

Önceki gerçek bir BTCPay E2E denemesinde bulunmuş, **kanıtlanmış** iki bulgu. Bu görev bunları yeniden keşfetmeye
çalışmaz ve **kesinlikle** eski tasarıma geri dönmez:

1. **Webhook payload'ında `confirmations` alanı gelmez.** `RequiredConfirmations` bir iş kuralı olarak
   `05-payment.md`'de kalır, ama settlement sinyali olarak `InvoiceSettled` event'i kullanılır. Kod hiçbir koşulda
   payload'da var olmayan bir `confirmations` alanını okuyup eşik kontrolü yapan tasarıma döndürülmez.
2. **`PaidAmountLtc` (veya benzeri) alanının payload'da geldiği varsayılamaz.** Kod hâlâ bu alanın var olduğunu
   varsayıyorsa bu bir bug'dır; tutar, invoice'ın gerçekten döndürdüğü alanlardan türetilecek şekilde düzeltilir.

## 7. Aşama 5 — Yön B: Sistem para gönderebiliyor mu (çekim/payout) 🔒

`14-payment-sandbox.md` payout'u yalnızca "gözlemlenecek" bir davranış sayıyordu ve `PayoutService`'e dokunmayı
yasaklıyordu. **Bu dosyada o yasak kaldırılmıştır** — çekim tarafı müşterinin parasının sistemden **çıktığı**
yöndür, doğrulanmadan sistem çalışıyor sayılamaz.

1. Yeterli bakiyeli test hesabından bir çekim talebi oluştur.
2. Admin panelinden onayla → `PayoutService` regtest'e gerçek bir on-chain işlem gönderiyor mu (payout ID/TXID)?
3. `WithdrawalRequest` ve `Payout`/`PayoutRecipient` state'leri `05-payment.md` Bölüm 5.2'deki makineye uygun
   ilerliyor mu; `Wallet.BalanceUsd` doğru düşüyor mu (before/after)?
4. **Çift onay (concurrency):** Aynı talep aynı anda iki kez onaylanırsa iki kez gönderim oluyor mu? Oluyorsa
   idempotency/kritik bölge koruması eksiktir → düzeltilir.
5. **Reddedilen çekim:** Admin reddederse bakiye doğru geri ekleniyor mu; aynı talep ikinci kez reddedilirse
   bakiye tekrar eklenmiyor mu?
6. **Başarısız payout:** Kontrollü şekilde üretilir (BTCPay'in kendi araçlarıyla veya hata enjeksiyonuyla).
   Rastgele geçersiz adrese gerçek gönderim denemesi gibi sandbox'ın durumunu bozacak keyfi işlemler yapılmaz.
   Güvenli şekilde üretilemiyorsa kod/test üzerinden **failure-path analizi** yapılır ve raporda "gerçek E2E ile
   doğrulanamadı, kod analiziyle değerlendirildi" diye açıkça yazılır — bu PASS'i engeller, PARTIAL'a götürür.
7. **Yetersiz bakiye/limit aşımı:** Exception değil, sonuç tipi/hata koduyla mı dönüyor
   (`06-coding-standards.md` "Exception ve Guard")?
8. **Reconciliation:** `05-payment.md`'deki mutabakat/arka plan işi varsa, sandbox'ta gerçekten çalışıp askıda
   kalmış bir invoice/payout'u doğru şekilde kapatıyor mu?

## 8. Aşama 6 — Production'a geçişin config-only olduğunun kanıtı 🔒

Müşterinin iyzico akışındaki asıl beklenti bu; **kanıtlanmadan görev bitmez**:

- [ ] `Sandbox` → `Live` geçişinde değişmesi gereken **tam config listesi** rapora yazılır (alan adı + nereden alınacağı).
- [ ] `git grep` ile kodda sandbox'a özgü hiçbir dallanma/hardcoded regtest değeri kalmadığı doğrulanır.
- [ ] `Live` modunda eksik config ile uygulamanın **başlamadığı** fiilen test edilir (Bölüm 5 fail-fast kuralı).
- [ ] ❓ Mainnet'e geçmeden önce ayrıca doğrulanması gerekenler rapora **madde madde** yazılır: mainnet wallet'ın
      store'a bağlı olduğu, webhook'un production URL/secret ile **yeniden** kurulduğu (testnet/regtest webhook'u
      taşınmaz), API key izinlerinin mainnet store'da da tanımlı olduğu, ve `RequiredConfirmations`'ın regtest için
      uygun olan `1` değerinden gerçek bir mainnet güven eşiğine yükseltilmesi gerektiği. **Bu görev
      `RequiredConfirmations`'ı değiştirmez** — yalnızca değiştirilmesi gerektiğini işaretler.

## 9. Sonuç sınıflandırması 🔒

- **PASS** — iki yön de (yatırma + çekim) gerçek sandbox üzerinde, Bölüm 6-7'deki kenar senaryolarıyla birlikte
  doğrulandı; Bölüm 8 kanıtlandı.
- **PARTIAL** — bir yön veya belirli bir kenar senaryosu doğrulanamadı/düzeltilemedi.
- **BLOCKED** — sandbox altyapısı (Docker/regtest/tünelleme) bu ortamda hiç kurulamadı, gerçek E2E yapılamadı.

🔒 **"Muhtemelen çalışıyor" hiçbir koşulda kabul edilmez.** Kanıtı olmayan hiçbir adım PASS sayılmaz; kanıt
yoksa doğrudan PARTIAL/BLOCKED yazılır. Uydurulmuş ID/TXID/log satırı yazmak, görevin başarısızlığından **daha
ağır** bir hatadır.

## 10. Kapsam sınırı 🔒

- `05-payment.md`'deki 🔒 iş kuralları (sağlayıcı, LTC, %10 komisyon, giriş ücretleri, limitler, tolerans,
  confirmation eşiği) **değiştirilemez**. Bir "düzeltme" asla bir 🔒 kuralı değiştirerek yapılmaz — kural doğru,
  kod yanlıştır; düzeltme koddan yapılır.
- Bulunan bug'ların düzeltilmesi kapsam içidir; **alakasız refactor/rename/yeniden düzenleme kapsam dışıdır**
  (`01-workflow-rules.md` Bölüm 0.2/0.7). `git diff` yalnızca gerçekten hatalı bulunup düzeltilen dosyaları
  içermelidir.
- Oyun motoru, auth ve UI modüllerine dokunulmaz.
- Mainnet'e fiilen geçiş bu görevin parçası değildir (Bölüm 8 yalnızca geçişi config-only hâle getirir).

## 11. Definition of Done — hepsi ✔ olmadan bitmez

- [ ] Aşama 1 denetimi yapıldı, mevcut durum envanteri rapora yazıldı.
- [ ] `sandbox/btcpay/` tek komutla ayağa kalkıyor; kullanıcıdan hiçbir hesap/kayıt istenmedi.
- [ ] API key izinleri, gerçekten çağrılan endpoint'lere göre asgari yetkiyle üretildi (dokümantasyondan doğrulandı).
- [ ] `PaymentConfig.Mode` (`Fake`/`Sandbox`/`Live`) eklendi; `Sandbox` ile `Live` arasında **kod farkı yok**.
- [ ] `Live` modunda eksik config ile uygulama başlamıyor (fail-fast fiilen test edildi).
- [ ] Bölüm 6'daki 9 adımın **her biri** gerçekten çalıştırıldı, kanıtlarıyla rapora yazıldı.
- [ ] Bölüm 7'deki 8 adımın **her biri** gerçekten çalıştırıldı, kanıtlarıyla rapora yazıldı.
- [ ] Bulunan her bug için: kök neden + düzeltilen dosya + düzeltme sonrası yeniden doğrulama sonucu yazıldı.
- [ ] Bulunan her bug için `api.Tests/` altına regresyon testi eklendi (test eklenmeden bug kapatılmış sayılmaz).
- [ ] `Fake` modunda eski akış hâlâ çalışıyor (regresyon yok); `dotnet build` ve `npm run build` geçti; mevcut
      testler geçiyor.
- [ ] Secrets kontrolü yapıldı: API key/webhook secret/connection string git geçmişine girmemiş, `.gitignore`
      kapsamı `git check-ignore` ile doğrulandı. **Secret değerlerinin kendisi rapora yazılmaz**, yalnızca
      "bulundu/bulunmadı, temizlendi" yazılır.
- [ ] Bölüm 8 (config-only geçiş) kanıtlandı, mainnet öncesi ❓ maddeleri listelendi.
- [ ] Sonuç PASS/PARTIAL/BLOCKED olarak gerekçesiyle sınıflandırıldı.
- [ ] `CLAUDE.md`'deki 14 ve 15 satırları bu dosyanın satırıyla değiştirildi.

## 12. Rapor formatı 🔒

`01-workflow-rules.md` Bölüm 0.14'e ek olarak, bu görevin raporu şu başlıkları **bu sırayla** içerir:

1. Mevcut durum envanteri (Aşama 1)
2. Sandbox nasıl kuruldu (regtest mi, ikincil alternatif mi, neden)
3. Yön A sonucu — invoice ID, TXID, webhook event, imza sonucu, balance before/after, tutar doğrulaması
4. Yön B sonucu — withdrawal ID, payout ID, TXID, state geçişleri, balance before/after
5. Kenar senaryoları — duplicate webhook, geçersiz imza, stale event, expired invoice, çift onay, başarısız
   payout, reddedilen çekim (her biri için: çalıştırıldı mı, sonuç ne)
6. Bulunan bug'lar ve kök nedenleri
7. Yapılan minimum düzeltmeler + eklenen testler
8. Regresyon sonuçları (`Fake` akışı, build, mevcut testler)
9. Secrets kontrolü sonucu
10. Production'a geçiş: değişecek config listesi + mainnet öncesi ❓ maddeleri
11. Değişen dosyalar / dokunulmadığı doğrulanan kritik dosyalar
12. Kalan problemler
13. **PASS / PARTIAL / BLOCKED**

Her finansal hareket için gerçek ID/TXID ve before/after değerleri yazılır.