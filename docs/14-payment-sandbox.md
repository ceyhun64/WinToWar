# 0X — Ödeme Sandbox/Testnet Kurulumu (BTCPay gerçek anahtarlar öncesi)

> **Dosya adı notu:** Bu dosyayı kaydederken projenizdeki `docs/` klasöründe gerçekte kullanılmayan bir sıradaki
> numarayla kaydedin (ör. mevcut en yüksek numaralı dosyanın bir fazlası) ve `CLAUDE.md`'nin "Her görevde önce oku"
> tablosuna bir satır ekleyin: _"Ödeme sandbox/ortam işi → `docs/0X-payment-sandbox.md`."_

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla ilerle,
  kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ❓ Müşteriden ileride doğrulanması gereken nokta.

## 0. Amaç ve kapsam 🔒

Şu an `Development` ortamında `FakePaymentProvider` kullanılıyor (`Program.cs`) — bu, ödeme akışının kod
seviyesinde çalıştığını doğrular ama **gerçek BTCPay Greenfield API'sine hiç dokunmaz**: gerçek invoice
oluşturma, gerçek webhook imza doğrulama, gerçek confirmation bekleme gibi adımlar test edilmiyor.

Amaç: gerçek bir BTCPay Server'a karşı **gerçek para harcamadan** uçtan uca test edebileceğimiz bir ortam
kurmak, sonra müşteri gerçek bir BTCPay hesabı/mağaza açtığında **yalnızca config değerlerini değiştirerek**
production'a geçebilmek — kod tarafında ek bir değişiklik gerekmemeli.

**Kapsam dışı:** `PaymentService`, `WalletService`, `PayoutService`, `RefundService`, `IPaymentProvider`
arayüzünün kendisi — bunların iş mantığı bu görevle **değişmez**. Bu görev yalnızca *hangi BTCPay instance'ına
bağlanıldığını* ve *hangi ortamda hangi provider'ın seçildiğini* ilgilendirir.

## 1. Sandbox seçeneği — birincil aday, göreve başlamadan doğrulanmalı ❓🛠️

BTCPay Server'ın kendi barındırdığı genel bir testnet instance'ı var: **`testnet.demo.btcpayserver.org`**
(ücretsiz kayıt, **uptime garantisi yok** — resmi olarak yalnızca test amaçlı, kalıcı bir bağımlılık olarak
düşünülmemeli). Bu, benim (görev dokümanını hazırlarken) bir web araması ile doğruladığım genel bir bilgi;
ama **anlık kullanılabilirliği ve o an LTC (testnet) desteğinin fiilen açık olup olmadığı zamanla değişebilir.**

🛠️ **Zorunlu ön-adım — agent göreve başlamadan bunu kontrol eder, "muhtemelen çalışır" diye varsaymaz:**
1. `testnet.demo.btcpayserver.org`'un o an erişilebilir olduğunu doğrula (basit bir HTTP isteğiyle) — bu yalnızca
   instance'ın ayakta olduğunu gösterir, tek başına "kullanılabilir" anlamına gelmez; asıl doğrulama bir
   sonraki maddedir.
2. Kayıt/store oluşturma adımında **LTC (testnet)** seçeneğinin gerçekten sunulduğunu doğrula — sunulmuyorsa
   veya instance erişilemezse aşağıdaki regtest alternatifine geç, bu görevi "sandbox instance bulunamadı" diye
   yarım bırakma.

NBXplorer'ın testnet'te genel olarak BTC/LTC birlikte desteklendiği bilinen bir teknik gerçek, ama bunun bu
spesifik demo instance'ında **şu an** aktif olduğu ayrı bir doğrulama gerektirir — ikisini karıştırma.

- Adım 1: `testnet.demo.btcpayserver.org`'a kayıt ol, bir **store** oluştur.
- Adım 2: Store ayarlarında **LTC (testnet)** için bir cüzdan bağla — gerçek bir wallet gerekmiyor, BTCPay'in
  kendi "hot wallet" oluşturma seçeneği yeterli (seed'i bir yere not et, gerçek para taşımayacak).
- Adım 3: Store → **API Keys** altından bir Greenfield API key üret. ❓🛠️ **İzinler kesinleştirilmemiştir —
  agent göreve başlamadan BTCPay'in resmi Greenfield API dokümantasyonundan (permission listesi, her endpoint'in
  kendi sayfasında belirttiği gerekli izin) hangi endpoint'lerin bu projede gerçekten çağrıldığını
  (`BtcPayGreenfieldProvider.cs` içindeki gerçek çağrılar — invoice oluşturma, invoice sorgulama, payout
  oluşturma/işleme) tek tek doğrulayıp **yalnızca o endpoint'lerin gerektirdiği minimum izinle** bir API key
  oluşturur. Örnek bilinen asgari küme `btcpay.store.canviewinvoices` + `btcpay.store.cancreateinvoice`'dır, ama
  payout akışı için ek bir izin (ör. payout yönetimi) gerekebilir — bunu **varsaymak yerine dokümantasyondan
  doğrula**. `canmodifystoresettings` gibi geniş kapsamlı bir izin, yalnızca gerçekten store ayarlarını API
  üzerinden değiştiren bir çağrı varsa eklenir; "belki webhook için lazımdır" gibi bir gerekçeyle **fazladan izin
  verilmez** (asgari yetki prensibi — BTCPay'in kendi dokümantasyonu da bunu özellikle vurguluyor).
- Adım 4: Store → **Webhooks** altından `PaymentWebhooksController`'ın uç noktasına (`api/webhooks/btcpay`,
  local test için bir tünelleme aracı — ör. ngrok/Cloudflare Tunnel — gerekecek çünkü BTCPay dışarıdan
  `localhost`'a ulaşamaz) **BTCPay yönetim panelinden elle** bir webhook tanımla, secret'ı kopyala. Bu adım
  API key üzerinden değil, BTCPay web arayüzünden yapılır — dolayısıyla API key'in webhook yönetimi için ayrı
  bir izne ihtiyacı yoktur.
- Adım 5: Testnet LTC almak için bir faucet kullan (ör. bir LTC testnet faucet'i ara/bul — anlık olarak
  değişebileceğinden görev sırasında güncel bir faucet adresi web'den doğrulanmalı, burada sabit bir URL
  verilmiyor).

🛠️ **Alternatif (birincil seçenek çalışmazsa/uptime sorunu olursa):** Kendi self-hosted **regtest** BTCPay
instance'ını (`btcpayserver-docker`, `--network=regtest`) ayağa kaldırmak. Regtest'in avantajı: checkout
sayfasında "fake payment" ve "fake mine block" butonları var, hiçbir faucet'e bağımlı değil, tamamen izole.
Dezavantajı: kurulum daha uzun (kendi VPS/Docker gerekiyor). Bu görev birincil olarak `testnet.demo` ile
ilerler; regtest yalnızca birincil seçenek pratikte kullanılamaz hale gelirse devreye alınır.

## 2. Ortam ayrımı — kod tarafı 🛠️

Şu an `Program.cs`'de yalnızca iki dal var: `Development` → `FakePaymentProvider`, değilse → gerçek
`BtcPayGreenfieldProvider`. Sandbox'ı test edebilmek için üçüncü bir durum gerekiyor: **"Development ortamında
çalış ama sahte değil gerçek (testnet) BTCPay'e bağlan."**

🛠️ **Karar:** Yeni bir ortam eklemek yerine (YAGNI, `ASPNETCORE_ENVIRONMENT` şişirmemek için), `PaymentConfig`'e
tek bir boolean bayrak eklenir: `UseFakeProviderInDevelopment` (varsayılan `true`). `Program.cs`'deki koşul şu
hale gelir: `Development` **ve** bu bayrak `true` ise `FakePaymentProvider`; aksi her durumda (Production veya
bayrak `false` yapılmış Development) gerçek `BtcPayGreenfieldProvider`. Bu sayede:
- Günlük geliştirme: `appsettings.Development.json`'da bayrak `true` (varsayılan) → hâlâ hızlı, ağsız
  `FakePaymentProvider`.
- Sandbox testi: yerelde `appsettings.Development.json`'a geçici olarak (veya bir `appsettings.Development.local.json`/
  user-secrets ile, **commit edilmeden**) `UseFakeProviderInDevelopment=false` + testnet
  `BtcPayBaseUrl`/`BtcPayApiKey`/`BtcPayStoreId`/`WebhookSecret` girilir → aynı `Development` ortamında gerçek
  testnet BTCPay'e bağlanır.
- Production: değişiklik yok, bayrak zaten etkisiz (Development değil).

Bu, `01-workflow-rules.md` Bölüm 0.2'deki "kapsam dışı dosyaya dokunma" kuralına uygun minimal bir değişikliktir:
yalnızca `PaymentConfig.cs`'ye bir alan, `Program.cs`'de bir koşula `&&` eklenir — başka hiçbir dosya değişmez.

## 3. Secrets yönetimi 🔒

Testnet API key/webhook secret'ı bile **kod içine hardcode edilmez** — `06-coding-standards.md`'nin "Secrets /
Hassas Bilgi Yönetimi" kuralı testnet için de geçerlidir (gerçek para taşımasa da, aynı disiplin korunur). Yerel
testte `dotnet user-secrets` kullanılır; committen çıkarılmış bir `appsettings.Development.local.json`
alternatifi de kabul edilebilir ama `.gitignore`'a eklenmesi zorunludur.

## 4. Doğrulama akışı (bu görevin "bitti" sayılması için) 🛠️

Aşağıdaki uçtan uca akış **gerçekten** testnet üzerinden çalıştırılıp doğrulanmadan görev tamamlanmış sayılmaz
(bkz. `01-workflow-rules.md` Bölüm 0.8-0.9 "runtime'ı da doğrula"):

1. `/cuzdan` sayfasından bir top-up başlat → gerçek bir testnet LTC invoice'ı BTCPay'de oluşuyor mu (BTCPay
   dashboard'unda görünüyor mu)?
2. Faucet'ten testnet LTC gönder → webhook gerçekten `PaymentWebhooksController`'a ulaşıyor mu, imza doğrulaması
   (`WebhookSignatureValidator`) geçiyor mu?
3. `RequiredConfirmations` eşiğine ulaşınca `PaymentInvoice.Status=Confirmed`'e geçip `Wallet.BalanceUsd`
   gerçekten artıyor mu?
4. Bir çekim (withdrawal) talebi oluşturup admin onayından geçirerek, **`PayoutService`'in kaynak kodu hiç
   değiştirilmeden**, mevcut implementasyonun testnet'e gerçek bir on-chain işlem gönderdiği doğrulanır mı
   (BTCPay dashboard'unda payout görünür mü)? Bu adım Bölüm 0'daki kapsam-dışı kuralıyla çelişmez — kod
   dokunulmaz, yalnızca zaten var olan davranış testnet üzerinde gözlemlenip rapor edilir.

Görev sonu raporunda bu 4 adımın her biri için **gerçekten çalıştırıldığına dair kanıt** (ekran görüntüsü
tarif etmek yerine, BTCPay dashboard'undaki invoice/payout ID'leri, log satırları) yazılır — "muhtemelen
çalışıyor" gibi doğrulanmamış ifade kullanılmaz.

## 5. Definition of Done — bu görev ancak hepsi ✔ olunca biter

- [ ] `PaymentConfig.UseFakeProviderInDevelopment` alanı eklendi, `Program.cs`'deki koşul güncellendi.
- [ ] Testnet BTCPay bağlantı bilgileri (`BtcPayBaseUrl`/`ApiKey`/`StoreId`/`WebhookSecret`) yalnızca
      user-secrets veya `.gitignore`'lu bir dosyada — commit içinde **değil**.
- [ ] Gerçek bir testnet LTC invoice'ı BTCPay dashboard'unda oluşturuldu (invoice ID rapora yazıldı).
- [ ] Webhook gerçekten `PaymentWebhooksController`'a ulaştı, `WebhookSignatureValidator` imzayı geçerli kabul etti.
- [ ] Confirmation eşiğine ulaşınca `Wallet.BalanceUsd` gerçekten arttı (öncesi/sonrası değer rapora yazıldı).
- [ ] Bir payout testnet'te gerçekleşti (payout ID/tx hash rapora yazıldı) — `PayoutService` kaynak kodu
      değişmedi, yalnızca davranışı gözlemlendi.
- [ ] Testnet bağlantı bilgilerinin tutulduğu dosya (`appsettings.Development.local.json` kullanıldıysa) gerçekten
      `.gitignore` tarafından ignore edildiği **doğrulandı** (ör. `git status`/`git check-ignore` ile kontrol
      edilip sonucu rapora yazıldı) — "muhtemelen ignore edilmiştir" varsayımıyla geçilmedi.
- [ ] `FakePaymentProvider` ile normal `Development` akışı (bayrak varsayılan `true` iken) hâlâ eskisi gibi
      çalışıyor — regresyon yok.
- [ ] Production (`Development` dışı, bayraktan bağımsız) davranışı değişmedi.
- [ ] `git diff` yalnızca beklenen dosyaları içeriyor (`PaymentConfig.cs`, `Program.cs`, ilgili appsettings/
      user-secrets şablonu — `PaymentService`/`WalletService`/`PayoutService`/`RefundService`/
      `IPaymentProvider`/`BtcPayGreenfieldProvider` dosyalarında **tek satır bile yok**).
- [ ] Kullanılan API key'in izinleri, gerçekten çağrılan endpoint'lerle minimum eşleşecek şekilde doğrulandı
      (Bölüm 1, Adım 3) — gereğinden geniş izin verilmedi.

## 6. Production'a geçiş (bu görev bitince, ayrı bir adım — ❓)

Kod tarafında hedeflenen production geçişi, mevcut implementasyon açısından yalnızca config değerlerinin
(`Payment:BtcPayBaseUrl`, `Payment:BtcPayApiKey`, `Payment:BtcPayStoreId`, `Payment:WebhookSecret`) gerçek
mainnet mağazasının değerleriyle değiştirilmesiyle mümkün olmalıdır — bu görev boyunca kodda başka bir bağımlılık
oluşturulmaz. **Ancak bu, bu görev sırasında uçtan uca kanıtlanmış bir şey değildir** (testnet'te doğrulanan akış
mainnet'i değil testnet'i kapsar); mainnet'e geçmeden önce en azından şunlar **ayrıca** doğrulanmalıdır:

- BTCPay store'un mainnet wallet'ının gerçekten bağlı ve doğru olduğu,
- webhook yapılandırmasının (URL, secret) production ortamına göre yeniden kurulduğu — testnet webhook'u
  mainnet store'a otomatik taşınmaz,
- payout için kullanılan API key izinlerinin mainnet store'da da doğru tanımlandığı,
- `UseFakeProviderInDevelopment` bayrağının production'da zaten etkisiz olduğu (Development değil) — ayrıca
  ayarlanmasına gerek yok, ama bunun varsayım değil kontrol edilmiş bir gerçek olduğu teyit edilmeli.

❓ Mainnet'e geçmeden önce `RequiredConfirmations`'ın da testnet'teki "1" değerinden gerçek bir mainnet güven
eşiğine (müşteriyle netleştirilmeli — README'de zaten "regtest/testnet için 1, mainnet öncesi ayrıca gözden
geçirilir" notu var) yükseltilmesi gerekiyor; bu ayrı bir onay maddesidir, bu görevin kapsamında değiştirilmez.
Kısacası: **production'a geçiş bu görevin bir parçası değildir, bu görev yalnızca geçişi config-only hale
getirecek zemini hazırlar** — geçişin kendisi ayrı bir görev/onay adımı olarak ele alınmalıdır.