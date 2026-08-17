# Claude Code Görev Talimatı: Ödeme Sistemi (LTC / BTCPay Entegrasyonu) — v10 (WinToWar'a göre güncellendi)

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla ilerle, kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ⚙️ **ÇALIŞMA DAVRANIŞI KURALLARI:** Süreç kuralları.

> ⚠️ **v10 — kritik değişiklik (2026-08-08):** Kullanıcı, `/lobi`'de her oda girişinde (ve top-up sırasında) LTC adresi sorulmasının kötü bir mimari olduğunu belirtip şu hedef mimariyi verdi: **(1)** oyuna girişte hiçbir zaman LTC adresi istenmez — bakiye yeterliyse `Wallet.BalanceUsd`'den doğrudan düşülür, yetmezse sunucu otomatik bir top-up invoice'ı açar (BTCPay'in kendi ürettiği adres/QR ile, kullanıcıdan adres istenmeden); **(2)** kazananın parası artık on-chain LTC olarak gönderilmez, doğrudan `Wallet.BalanceUsd`'ye kredi olarak işlenir; **(3)** LTC adresi **yalnızca** `/cuzdan`'daki para çekme (`WithdrawalRequest.DestinationLtcAddress`) sırasında, oyuncu kendi isteğiyle girer. Bunun doğal sonucu olarak Payout/Refund artık BTCPay'e giden ayrı bir on-chain gönderim değil, senkron bir bakiye kredisidir — bu yüzden `PaymentInvoice.PayoutAddress`, `Wallet.PayoutAddress`, `PayoutRecipient.PayoutAddress/NetworkFeeLtc/BtcPayTransactionId`, `Refund.AmountLtc/BtcPayTransactionId`, tüm payout/refund retry+reconciliation makinesi (`ReconciliationService`, `ReconciliationLock`) ve buna bağlı `PayoutStatus`/`RefundStatus` state machine'leri **tamamen kaldırıldı**. Aşağıdaki Bölüm 1.5, 1.9 (RoomEntryOutcome notu), 2.2, 2.6, 3.2 ve 5.2'nin on-chain-gönderim odaklı içeriği bu v10 kararıyla **süpersede edilmiştir** — güncel davranış için `api/Services/Payments/PayoutService.cs` ve `RefundService.cs`'in kod-içi yorumları tek doğruluk kaynağıdır. v9'un geri kalanı (değişken havuz, yuvarlama disiplini, çoklu-kazanan veri modeli, `LobbyFillTimeoutSeconds=300`) değişmeden geçerlidir.

Müşterinin verdiği tek gerçek karar kalemleri: **(1) ödeme LTC ile yapılacak, (2) komisyon %10 (kazananın havuzundan), (3) Standart oda giriş ücreti kişi başı $1 USD, (4) VIP odada giriş ücreti ve oyuncu sayısını (2-12) kurucu belirler, (5) bakiye arayüzde USD gösterilir, (6) eşleşme zaman aşımı 5 dakika + kullanıcı seçimi (iptal/bekle), (7) ücretsiz Practice modu vardır.** `Payout` veri modeli çoklu-kazanan (beraberlik) senaryosunu destekler — maç-bazlı `Payout` (agregatör) + kazanan-bazlı `PayoutRecipient` (1-N) ayrımıyla (bkz. Bölüm 2.2). Yuvarlama yalnızca tek seferlik, persist anında yapılır (Bölüm 2.3); network fee yalnızca gerçekleşen (actual) değerle kalıcı kayda geçer (Bölüm 2.6).

Bu modül, ana oyun motorundan (`docs/01-workflow-rules.md`, `docs/02-architecture.md`, `docs/03-game-rules.md`) **tamamen ayrı** bir katman olarak inşa edilecek.

---

## 0. ÇALIŞMA DAVRANIŞI KURALLARI ⚙️

### 0.1 Ana projedeki tüm kurallar geçerli

`CLAUDE.md` / `docs/01-workflow-rules.md` içindeki kurallar bu modül için de aynen geçerlidir.

### 0.2 Sıra

1. `PaymentConfig` (Bölüm 2.4)
2. `TimeProvider` entegrasyonu (Bölüm 2.8)
3. Domain modelleri + `ProcessedWebhookEvent` (Bölüm 8.4)
4. State machine'ler + **monotonluk guard'ı** (Bölüm 5)
5. `IPriceOracle` + cache + stale-cache politikası + single-flight + fallback zinciri (Bölüm 1.2)
6. `PaymentService` (invoice oluşturma, expiry senkronu, webhook doğrulama + event idempotency + monotonluk kontrolü, transaction disiplini, BIP-21 üretimi)
7. `PayoutService` (network fee — yalnızca actual persist, retry+backoff+jitter)
8. `RefundService` (retry+backoff+jitter)
9. Reconciliation job (distributed lock + tarama penceresi)
10. SignalR event entegrasyonu
11. Oyun akışına entegrasyon
12. Frontend
13. Testler (Bölüm 9)

Her aşama sonunda build al, bir sonrakine geçme.

### 0.3 Gerçek para — ekstra dikkat kuralı

- Hiçbir ödeme/payout/refund işlemi idempotent olmadan yazılmayacak.
- Mainnet'e geçiş yalnızca müşterinin ayrı onayıyla.
- Bölüm 5'teki state machine'ler dışında hiçbir durum geçişi yazılmayacak, **ve hiçbir geçiş state'i geriye almayacak** (Bölüm 5.4).
- 🛠️ **Ön koşul — test ortamı:** BTCPay regtest/testnet erişilebilirliği bu görevin ön koşuludur; yoksa sahte `IPaymentProvider` ile ilerlenir, rapora yazılır.
- Tüm parasal hesaplamalarda sapma sıfır toleranslıdır; tüm decimal↔string dönüşümlerinde `CultureInfo.InvariantCulture` zorunludur.
- Zaman her yerde `TimeProvider` üzerinden alınır, `DateTime.UtcNow` doğrudan çağrılmaz.
- 🛠️ **Yuvarlama yalnızca kalıcılaştırma sınırında yapılır** (Bölüm 2.3) — bu kural tüm servisler için istisnasızdır.

---

## 1. İŞ KURALLARI — MÜŞTERİ KARARLARI (BİREBİR) 🔒

### 1.1 Giriş ücreti ve komisyon 🛠️ **WinToWar'a göre baştan yazıldı — sabit 12-oyuncu/$10.8 modeli geçersiz**

> ⚠️ Bu bölüm önceki "Porsuk Savaşları" kararına (sabit 12 oyuncu, sabit $12 havuz, sabit $10.8 payout) dayanıyordu. Müşteri nihai kararını WinToWar yönünde verdiği için havuz artık **oda tipine göre değişkendir** — aşağıdaki içerik bunu yansıtacak şekilde güncellendi.

- Giriş ücreti oda tipine göre değişir:
  - **Standart oda:** sabit **$1 USD**, sabit **4 oyuncu** (bkz. `03-game-rules.md` Bölüm 2.1 — likidite riski gerekçesiyle küçültüldü).
  - **VIP oda:** kurucunun belirlediği giriş ücreti (`Room.EntryFeeUsd`) ve oyuncu sayısı (`Room.MaxPlayers`, 2-12 arası).
  - **Practice (ücretsiz) oda:** giriş ücreti her zaman **$0**, `PaymentInvoice` akışı hiç tetiklenmez (bkz. Bölüm 1.8).
- 🛠️ **Formül — kesinleştirildi:** `TotalPoolUsd = Room.EntryFeeUsd × fiilen ödemesi onaylanmış oyuncu sayısı` (Standart odada normal senaryoda bu `MaxPlayers=4`'e eşittir, sonuç $4 çıkar). 🛠️ **Terim netleştirmesi (docs/09-eksik-tarama.md denetimi, Faz 5):** Bu değer `Room.MaxPlayers` (kapasite) ile HER ZAMAN aynı değildir — Bölüm 8'deki "VIP masa manuel başlatma" senaryosunda kurucu odayı kapasitesi dolmadan başlatabilir, bu durumda havuz yalnızca gerçekten ödeme yapmış oyunculardan oluşur. Değer artık `GameConfig`/`Room`'dan türetilir, sabit sayı olarak kod içine yazılmaz. `Payout = TotalPoolUsd × (1 − CommissionRate)`, **%10** komisyonla.
- 🔒 **Para birimi:** Bakiye arayüzde her zaman **USD ($)** gösterilir (müşterinin WinToWar mesajındaki net kararı — "Kafa karışıklığını önlemek için"). Önceki "1 Euro" ifadesi zaten geçersizdi, WinToWar mesajı bunu tekrar teyit eder; Euro hiçbir yerde kullanılmaz.
- 🔒 **Winner-take-all:** Müşteri komisyon modelini "kazananın havuzundan %10 kesilir" olarak tarif etti — havuzun tamamı **tek kazanana** gider (VIP'de N oyuncudan biri kazanır). `Payout = TotalPoolUsd × (1 − CommissionRate)` formülü bu şekilde uygulanır.
- 🛠️ **Çoklu-kazanan senaryosu — WinToWar'da ❓ netleşmemiş:** WinToWar'da kazanma koşulu "tek oyuncu ayakta kalana kadar" olduğundan normal senaryoda zaten tek kazanan çıkar; iki oyuncunun **aynı anda** elenmesi gibi bir uç durumda ortak kazanan modeli (havuzun eşit bölünmesi, `PayoutRecipient` 1-N ayrımı — bkz. Bölüm 2.2, veri modeli değişmedi) hazır tutulur ama bu senaryonun müşteri tarafından onaylanması gerekir (bkz. `03-game-rules.md` Bölüm 14, madde 5).
- Network fee ayrımı Bölüm 2.6'da.

### 1.2 Ödeme birimi, kur, cache, stale politikası, single-flight, timeout ve fallback zinciri

- Ödeme LTC üzerinden yapılacak.
- Kur alanı `LockedUsdPerLtc` — 1 LTC'nin kaç USD ettiğini taşır; `AmountLtc = AmountUsd / LockedUsdPerLtc`.
- 🛠️ **`PriceOracleSource` yalnızca gerçek sağlayıcı adını taşır** (`CoinGecko`/`CoinCap`), `"Cache"` bu alana asla yazılmaz — cache bir kaynak değil, bir teslimat/erişim yöntemidir. Ayrı bir `RateServedFromCache: bool` alanı bunu işaretler. Bu ayrım, kurun cache'ten dönmesi durumunda dahi hangi providerdan geldiği bilgisinin **kaybolmamasını** garanti eder (denetim/audit amaçlı kritik).
- 🛠️ **Cache stampede / single-flight:** Cache boşken/süresi dolmuşken eşzamanlı gelen istekler dış API'ye ayrı ayrı gitmez. `CompositePriceOracle` içinde bir **single-flight** mekanizması uygulanır: "aynı cache miss" için yalnızca **tek bir** dış çağrı yapılır (`SemaphoreSlim(1,1)` veya eşdeğer kilitli-görev deseni ile), aynı pencerede gelen diğer eşzamanlı istekler bu tek çağrının sonucunu paylaşarak bekler; hiçbir koşulda aynı cache miss için paralel ikinci bir dış API çağrısı tetiklenmez.
- 🛠️ **Stale-cache politikası — üç kademeli:**
  1. **Fresh** (`PriceCacheFreshSeconds`, öneri 30 sn): cache doğrudan kullanılır.
  2. **Stale ama kullanılabilir** (`PriceCacheStaleMaxSeconds`, öneri 300 sn): canlı sağlayıcılar (CoinGecko→CoinCap) önce denenir; ikisi de başarısızsa stale değer kullanılır, `warning` loglanır, `RateServedFromCache=true` + `RateAgeSecondsAtUse` kaydedilir.
  3. **Stale-max aşıldı + sağlayıcılar da başarısız:** `PRICE_ORACLE_UNAVAILABLE`, invoice oluşturulmaz.
- Sağlayıcı timeout: `PriceOracleTimeoutSeconds` (öneri 5 sn) her sağlayıcı için ayrı ayrı.
- `PriceQuoteValiditySeconds` (öneri 900 sn) invoice üzerinde sabitleme süresi — sistem geneli cache süresinden bağımsız.
- `PaymentToleranceRate` (öneri %1), LTC biriminde, invoice'ın kendi `AmountLtc`'sinin yüzdesi olarak.
- `RefundOverpaymentThresholdUsd`, invoice'ın kilitlediği `LockedUsdPerLtc` üzerinden LTC'ye çevrilerek karşılaştırılır.

### 1.3 Otomatik ödeme akışı

- BTCPay Server, Greenfield REST API v2 üzerinden doğrudan HTTP ile entegre edilir.
- Sunucu tüm ödeme durumu kararlarında otoriterdir; yalnızca doğrulanmış, daha önce işlenmemiş **ve state'i geriye almayan** webhook event'i (Bölüm 5.4, 8.4) esas alınır.

### 1.4 Confirmation eşiği

- `RequiredConfirmations`: regtest/testnet için 1; mainnet öncesi ayrıca gözden geçirilir.

### 1.5 Payout hedefi, doğrulama ve değişmezlik

> ⚠️ **v10'da süpersede edildi** (bkz. giriş bölümündeki v10 notu): `PaymentInvoice.PayoutAddress` kaldırıldığından bu bölümdeki immutability guard'ı artık yok. Adres doğrulama (`AddressValidator`) hâlâ geçerli ama artık yalnızca `WalletService.RequestWithdrawalAsync`'in `DestinationLtcAddress`'i için kullanılıyor.

- ~~Adres doğrulama: regex ön filtre + gerçek Base58Check/Bech32 checksum kontrolü.~~
- ~~`PayoutAddress` immutable: update endpoint yok, EF Core `SaveChanges` guard'ı var.~~

### 1.6 İptal / lobi dolmama senaryosu 🛠️❗ **WinToWar'a göre düzeltildi — önceki 120 sn/otomatik-iade kararı YANLIŞTI**

> ⚠️ **Düzeltme notu:** Önceki (Porsuk Savaşları) sürümde bu bölüm `LobbyFillTimeoutSeconds = 120` (2 dakika) ile "otomatik tam refund" kararını "kesinleşmiş" olarak işaretlemişti. Bu, müşterinin kendi WinToWar mesajındaki açık ifadesiyle ("5 dakika boyunca eşleşme olmazsa, oyuncuya İptal Et/Bakiyeyi İade Et veya Beklemeye Devam Et seçeneği sunulacak") **çelişiyordu** ve müşterinin verdiği somut bir sayının/karar mekanizmasının sessizce değiştirilmesiydi — bu, `01-workflow-rules.md` Bölüm 0.5'in yasakladığı bir müdahale. Aşağıda düzeltilmiştir.

- Maç, oda ayarındaki oyuncu sayısı (N) ödeyip lobiye katılana kadar başlamaz (Standart’ta N=4, VIP’de kurucunun seçtiği değer; bkz. `03-game-rules.md`).
- 🔒 **`GameConfig.LobbyFillTimeoutSeconds = 300`** (5 dakika — müşterinin verdiği kesin değer, değiştirilemez). `PaymentConfig` aynı değeri `GameConfig`'ten okur, tek kaynak oradadır.
- 🔒 **Otomatik iade YOKTUR:** 300 sn dolduğunda sistem oyuncuya otomatik refund tetiklemez; bunun yerine SignalR üzerinden bir seçim ekranı gösterilir — **"İptal Et / Bakiyeyi İade Et"** veya **"Beklemeye Devam Et"** (seçilirse süre sıfırlanmaz — bkz. aşağıdaki 🛠️ notu — lobi beklemeye devam eder, refund tetiklenmez). Bu seçim **oyuncu bazındadır** — aynı lobideki bir oyuncu iade alıp ayrılırken diğerleri beklemeye devam edebilir (Porsuk'taki "tüm lobiye toplu otomatik refund" modeli burada geçerli değildir, çünkü müşterinin tarif ettiği seçim ekranı bireysel bir karardır).
- 🛠️ **"İptal Et" implementasyonu — netleştirildi (docs/09-eksik-tarama.md denetimi, Faz 4):** Bu metin daha önce "standart `RefundService` akışı (Bölüm 3.3) tetiklenir" diyordu — ama Bölüm 1.9'daki Wallet modeliyle bu artık doğru değil. Bir oyuncu odaya katılırken (Bölüm 1.9 "Maça katılım akışı") giriş ücreti zaten doğrudan `Wallet.BalanceUsd`'den düşülmüştür — o an ayrıca yeni bir LTC işlemi/on-chain ödeme YAPILMAMIŞTIR. Dolayısıyla "İptal Et" seçildiğinde geri alınacak olan şey bir on-chain transfer değil, yalnızca bu iç bakiye düşümüdür — `RefundService`'in temsil ettiği "gerçekleşmiş bir on-chain ödemeyi kullanıcının payout adresine geri gönderme" senaryosu burada hiç oluşmamıştır. Bu yüzden kod (`GameHub.LeaveLobby`) doğrudan `WalletService.CreditAsync` çağırır — `RefundService`'i **kasıtlı olarak** atlar, bu bir eksiklik değildir.
- 🛠️ **"Beklemeye Devam Et" — netleştirildi:** Bu bildirim/seçim ekranı, `LobbyFillTimeoutSeconds` süresi dolduğunda oyuncu başına **tek seferlik** gösterilir; "Beklemeye Devam Et" seçilirse sayaç sıfırlanmaz ve bildirim bir daha tekrar tetiklenmez — oyuncu bekleme sırasında istediği an `LeaveLobby` ile ayrılabilir (bkz. Bölüm 1.7). Sabit, tekrar eden bir hatırlatma (ör. her 5 dakikada bir yeniden sorma) müşteri tarafından istenmedi, eklenmedi (YAGNI).
- Bir oyuncu iade alıp ayrıldığında lobideki sayaç 1 azalır, `LobbyFillTimeoutSeconds` sayacı diğer bekleyen oyuncular için sıfırlanmaz/kesintiye uğramaz.

### 1.7 Bireysel gönüllü vazgeçme — 5 dakikalık süre dolmadan önce ayrılma 🛠️

Bölüm 1.6'daki "İptal Et/Beklemeye Devam Et" seçimi yalnızca `LobbyFillTimeoutSeconds` (300 sn) **dolduğunda** sunulur. Aşağıdaki, oyuncunun bu süre dolmadan, kendi isteğiyle daha erken ayrılması durumudur — müşteri bunu ayrıca belirtmedi ama aynı `LeaveLobby` aksiyonuyla, tutarlı bir şekilde her zaman izin verilir:

- Oyuncu, `Match.Status = Lobby` iken (lobi henüz N kişiye ulaşmamışken veya `Countdown` başlamamışken) `LeaveLobby` aksiyonuyla istediği an ayrılabilir. `Match.Status` zaten `Countdown` veya `Playing`'e geçtiyse bu aksiyon **reddedilir** — geri sayım/maç başladıktan sonra vazgeçme yoktur (bkz. `AbandonmentTimeoutSeconds` mekanizması, o farklı bir senaryo — bağlantı kopması).
- `LeaveLobby` çağrıldığında, o oyuncunun `PaymentInvoice`'ı için **tam otomatik refund** tetiklenir (aynı `RefundService` akışı, Bölüm 3.3) — kısmi kesinti/ceza yoktur (henüz hiçbir oyun kaynağı harcanmadığı, maç başlamadığı için). Practice odalarda `LeaveLobby`'nin refund tarafı hiç tetiklenmez (zaten ödeme yok).
- Refund süresi, diğer refund senaryolarıyla aynı SLA'ya tabidir: `RefundRetryCount`/`RefundRetryBaseDelaySeconds`/`RefundRetryJitterSeconds` parametreleriyle yönetilen retry+backoff mekanizması (Bölüm 3.3), ayrı bir "gönüllü çıkış" için özel bir süre tanımlanmaz — tek bir refund state machine'i (Bölüm 5.3) tüm iade türlerinde kullanılır.
- Oyuncu ayrıldıktan sonra lobideki sayaç `(N-1)/N`'ye düşer; `LobbyFillTimeoutSeconds` sayacı sıfırlanmaz, kalan süre içinde yeni bir oyuncu boşalan slotu doldurabilir.
- Bu davranış ek `GameConfig` değeri gerektirmez (mevcut refund altyapısını kullanır), yalnızca `GameHub`'a `LeaveLobby` aksiyonu ve `MatchManager`'da "yalnızca `Lobby` durumunda izinli" guard'ı olarak eklenir.

### 1.9 Bakiye / Cüzdan Modeli 🛠️❗ **KRİTİK EKSİK — yeni eklendi**

> ⚠️ **Tespit edilen tutarlılık sorunu:** Müşteri WinToWar mesajında açıkça bir **bakiye/cüzdan** sistemi tarif ediyor — "Bakiye Birimi: USD gösterilecek", "Para yatırma ve çekim arka planda LTC ile otomatik ve anlık çalışacak (Min. yatırma $1.00)". `07-pages.md`'deki `/cuzdan` sayfası da zaten bunu varsayıyor ("Bakiye, yatırma adresi/QR, para çekme formu" — "Katıl butonu → bakiye giriş ücretine yetiyorsa direkt katılım"). Ama önceki v8 veri modelinde `PaymentInvoice.MatchId` **zorunlu** (nullable değil) — yani her ödeme doğrudan bir maça bağlıydı, genel bir bakiye yükleme/biriktirme kavramı **yoktu**, ayrı bir para çekme (withdrawal) entity'si de **tanımlanmamıştı**. Bu, müşterinin tarif ettiği modelle uyumsuzdu. Aşağıda düzeltilmiştir.

- 🛠️ **Yeni entity — `Wallet`:** `PlayerId` (PK/FK, 1-1), `BalanceUsd` (decimal(18,2), her zaman ≥0, guard ile). Oyuncunun tüm ödeme geçmişinden bağımsız, tek doğruluk kaynağı olan güncel bakiyesi.
- 🛠️ **`PaymentInvoice.MatchId` artık nullable:** `MatchId = null` → bu bir **genel bakiye yükleme** (top-up) invoice'ıdır, onaylandığında `Wallet.BalanceUsd`'ye eklenir. `MatchId` dolu → bu, doğrudan bir maça giriş için oluşturulmuş invoice'tır (Standart oda `/lobi` akışında bakiye yetersizse otomatik bu türde bir invoice açılır); onaylandığında tutar **doğrudan o maça giriş olarak işlenir**, `Wallet.BalanceUsd`'ye eklenip tekrar düşülmez (gereksiz ara adım, YAGNI).
- 🛠️ **Maça katılım akışı — kesinleştirildi:** Oyuncu bir odaya katılmak istediğinde, önce `Wallet.BalanceUsd ≥ Room.EntryFeeUsd` kontrolü yapılır. Yeterliyse **yeni bir LTC işlemi/invoice oluşturulmadan**, doğrudan `Wallet.BalanceUsd -= Room.EntryFeeUsd` düşülür ve oyuncu lobiye eklenir (bu, `07-pages.md`'deki "/lobi" akışıyla birebir örtüşür). Yetersizse `MatchId` dolu bir top-up-ve-katıl invoice'ı açılır — 🛠️ **tutar netleştirildi:** invoice yalnızca **eksik kısım** için oluşturulur (`AmountUsd = Room.EntryFeeUsd − Wallet.BalanceUsd`, mevcut kısmi bakiye boşa gitmez). Invoice onaylandığında `Wallet.BalanceUsd` bu tutar kadar artırılıp aynı anda `Room.EntryFeeUsd` kadar düşürülür (net etki: bakiye 0'a döner, giriş ücreti tam karşılanmış olur) ve oyuncu **sunucu tarafından otomatik olarak** o maçın lobisine eklenir — kullanıcı invoice onaylandıktan sonra ayrıca bir "katıl" aksiyonu tetiklemez (bkz. `07-pages.md` `/odeme/[invoiceId]` "Onay sonrası yönlendirme").
- 🛠️ **Ödeme bekleme süresince lobi slotu tutulmaz — netleştirildi (Gemini incelemesinde "lobi süresinin bu esnada nasıl etkileneceği somutlaşmamış" tespitine karşılık):** Bakiye yetersiz olduğu için `/odeme/[invoiceId]`'e yönlendirilen bir oyuncu, invoice onaylanana kadar **lobiye hiç eklenmez** — `Match`/`Room`'un oyuncu listesinde yer almaz, `LobbyFillTimeoutSeconds` sayacını başlatmaz, halihazırda lobide bekleyen diğer oyuncuların sayacını da hiçbir şekilde etkilemez. Bu, karmaşık bir "slot rezervasyonu tutma/serbest bırakma" mekanizmasına ihtiyaç bırakmaz (YAGNI) — oyuncu yalnızca ödemesi onaylandığı an, o anki güncel lobi durumuna (dolu/boş, kaç kişi bekliyor) göre eklenir. Invoice süresi dolarsa (`ExpiresAt`) oyuncu hiçbir yere eklenmemiş olduğu için ek bir "lobiden çıkarma" işlemine de gerek yoktur.
- 🛠️ **Lobi dolma yarış durumu (race condition) — kesinleştirildi (Gemini incelemesinde bulunan gerçek bir uç durum):** Yukarıdaki tasarımın doğal bir sonucu olarak, oyuncu ödeme yaparken lobi başka oyuncularla dolup maç `Countdown`/`Playing`'e geçebilir. Invoice onaylandığında sunucu oyuncuyu lobiye eklemeden **önce** `Room`'un hâlâ `Lobby` durumunda ve dolu olmadığını doğrular (`MatchManager.TryAddPlayer`, guard-clause). Bu kontrol başarısız olursa (oda dolmuş/maç başlamış): (1) oyuncu o maça **eklenmez**, (2) `PaymentInvoice`'ın onaylanan tutarı **otomatik olarak `Wallet.BalanceUsd`'ye eklenir** — havada kalmaz, para asla kaybolmaz (bu, Bölüm 1.9'daki "top-up-ve-katıl" invoice'ının katılım başarısız olursa sessizce saf bir top-up'a düştüğü anlamına gelir), (3) client'a `RoomFullAfterPayment` gibi açık bir event/mesaj gönderilir, kullanıcıya "bu oda doldu, bakiyeniz yüklendi, başka bir odaya katılabilirsiniz" bilgisi gösterilir (`/lobi`'ye yönlendirme). Bu davranış ayrı bir `Refund` kaydı **gerektirmez** — zaten hiç maça harcanmamış bir tutarın normal top-up'a dönüşmesidir, `RefundService`'in "harcanmış parayı geri alma" senaryosundan farklıdır.
- 🛠️ **Yeni entity — `WithdrawalRequest`:** Oyuncu-başlatımlı, kazanç/`Payout` akışından **bağımsız** bir para çekme talebidir (`Refund`'dan farklıdır — `Refund` sistem tarafından tetiklenen bir iade, `WithdrawalRequest` oyuncunun kendi isteğiyle bakiyesinden çekim talebidir). Alanlar: `Id`, `PlayerId`, `AmountUsd`, `AmountLtc` (talep anında kilitlenen kur ile), `DestinationLtcAddress`, `Status` (`Pending`→`Approved`→`Sent`→`Completed` / `Rejected`/`Failed` — v10'dan itibaren tek gerçek async/on-chain para hareketi bu olduğundan kendi ayrı state machine'ini korur, bkz. giriş bölümündeki v10 notu), `CreatedAt`, `ProcessedAt`. Talep oluşturulduğu an `Wallet.BalanceUsd`'den **düşülür** (çift harcamayı önlemek için — aynı bakiyeyle iki çekim talebi açılamaz), `Failed`/`Rejected` durumunda bakiyeye **geri eklenir**.
- 🛠️ **Minimum yatırma:** Müşterinin verdiği örnek ("$1.00 - 0.022 LTC") `MinDepositUsd = 1.00` olarak `PaymentConfig`'e eklenir — top-up invoice'ı bu tutarın altında oluşturulamaz. Minimum çekim tutarı müşteri tarafından verilmedi; 🛠️ `MinWithdrawalUsd = 1.00` (aynı değer, tutarlılık için) varsayılır, ❓ müşteriye doğrulatılmalı.
- Bu değişiklik `06-coding-standards.md`'deki Migration disiplinine tabidir — `Wallet` ve `WithdrawalRequest` için ayrı EF Core migration'ları oluşturulur.

### 1.8 Practice (ücretsiz) Mod 🔒 **yeni**

- Practice odalarda `Match.IsPracticeMatch = true`; `PaymentInvoice` akışı hiçbir aşamada tetiklenmez — oyuncu odaya katılırken bakiye kontrolü yapılmaz, maç bitince `Payout`/`PayoutRecipient` akışı hiç oluşturulmaz.
- 🛠️ **Kesinleştirilen guard:** `MatchManager`, `Room.IsPractice = true` ise katılım isteğini `PaymentService`'e hiç yönlendirmez (Practice ile ücretli akış tamamen ayrı kod yollarıdır, ücretli akışın içine "ücretsizse atla" gibi bir koşul eklenmez — bu, `06-coding-standards.md`'deki guard-clause tercihiyle ve modüller arası izolasyon ilkesiyle tutarlıdır).
- Practice maçlarında da `LobbyFillTimeoutSeconds`/"İptal Et veya Beklemeye Devam Et" akışı aynen çalışır, yalnızca "İptal Et" seçeneğinin refund tarafı boştur (iade edilecek bir ödeme olmadığından, oyuncu doğrudan lobiden çıkarılır).
- 🛠️ **Bot politikası ve eşleşme mimarisi — kesinleştirildi (v2 — kök neden düzeltmesi):** Bot politikası Practice'te de geçerlidir — proje genelinde "bot yok" kuralı istisnasızdır (bkz. `03-game-rules.md` Bölüm 7). Önceki sürümde Practice, Standart/VIP gibi bir "oda listesi" modeliyle tasarlanmıştı; bu, oyuncuları gereksiz yere ayrı odalara dağıtıp eşleşmeyi zorlaştırıyordu. **v2'de Practice, tek bir paylaşılan otomatik eşleşme kuyruğuna dönüştürüldü** (`DefaultPracticeRoomId` — kurucusu olmayan, herkesin katıldığı tek havuz), varsayılan hedef oyuncu sayısı `GameConfig.PracticeRoomDefaultPlayerCount = 2`, zaman aşımı `GameConfig.PracticeLobbyFillTimeoutSeconds = 60`. Bu, "bot yok" kuralını korurken eşleşme olasılığını maksimize eder; sistemde o an başka pratik yapan kimse yoksa yine de gerçek bir insan gerektiği için bekleme süresi sıfırlanamaz — bu, kuralın kaçınılmaz bir sonucudur. Farklı ayarlarla özel bir Practice odası kurmak isteyenler için VIP-tarzı opsiyonel oda kurma da ayrıca desteklenir, ama varsayılan giriş noktası tek kuyruktur.

---

## 2. VERİ MODELİ VE KONFİGÜRASYON 🔒

### 2.1 `PaymentInvoice`

| Alan                   | Tip                   | Açıklama                                                                                                                                                                                                                                                                                                                           |
| ---------------------- | --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Id`                   | Guid (PK)             |                                                                                                                                                                                                                                                                                                                                    |
| `PlayerId`             | Guid (FK)             |                                                                                                                                                                                                                                                                                                                                    |
| `MatchId`              | Guid? (FK, nullable)  | 🛠️ **v9'da nullable yapıldı** — `null` ise genel bakiye yükleme (top-up), dolu ise doğrudan maça giriş invoice'ı (bkz. Bölüm 1.9)                                                                                                                                                                                                  |
| `BtcPayInvoiceId`      | string, unique index  | idempotency anahtarı                                                                                                                                                                                                                                                                                                               |
| `AmountUsd`            | decimal(18,2)         | 1.00                                                                                                                                                                                                                                                                                                                               |
| `AmountLtc`            | decimal(18,8)         | Kalıcılaştırma anında yuvarlanmış nihai değer (Bölüm 2.3)                                                                                                                                                                                                                                                                          |
| `LockedUsdPerLtc`      | decimal(18,8)         | 1 LTC = X USD                                                                                                                                                                                                                                                                                                                      |
| `PriceOracleSource`    | string                | Yalnızca `CoinGecko`/`CoinCap`                                                                                                                                                                                                                                                                                                     |
| `RateServedFromCache`  | bool                  |                                                                                                                                                                                                                                                                                                                                    |
| `RateAgeSecondsAtUse`  | int                   |                                                                                                                                                                                                                                                                                                                                    |
| `Status`               | enum (Bölüm 5.1)      | `Pending`, `Confirmed`, `Expired`, `Refunded`, `Failed`                                                                                                                                                                                                                                                                            |
| `StatusRank`           | int (computed/mapped) | Bölüm 5.4'teki monotonluk kontrolü için her state'in sabit bir sıra değeri                                                                                                                                                                                                                                                         |
| `ExpiresAt`            | DateTime              |                                                                                                                                                                                                                                                                                                                                    |
| `CurrentConfirmations` | int                   | 🛠️ **yeni — `/odeme/[invoiceId]` UI'ının canlı onay ilerlemesi göstermesi için eklendi** (bkz. `07-pages.md`). Webhook her onay geldiğinde günceller, `RequiredConfirmations`'a ulaşınca `Status=Confirmed`'e geçilir. Yalnızca gösterim amaçlı, hiçbir hesaplamada kullanılmaz (Bölüm 1.4'teki asıl eşik kontrolü ayrıca yapılır) |
| `CreatedAt`            | DateTime              |                                                                                                                                                                                                                                                                                                                                    |
| `ConfirmedAt`          | DateTime?             |                                                                                                                                                                                                                                                                                                                                    |

### 2.2 `Payout` (maç-bazlı agregatör) ve `PayoutRecipient` (kazanan-bazlı) — v8'de düzeltildi 🔒

> ⚠️ **v10'da süpersede edildi:** 1-N (agregatör/kazanan) yapısı hâlâ geçerli, ama alanlar artık on-chain değil USD-kredi odaklı: `Payout.TotalPoolLtc/CommissionLtc` → `TotalPoolUsd/CommissionUsd`, `PayoutRecipient.PayoutAddress/AmountLtc/NetworkFeeLtc/BtcPayTransactionId/Status/RetryCount/NextRetryAt` → yalnızca `AmountUsd` + `CreatedAt` (senkron kredi, ayrı bir gönderim/retry adımı yok). Aşağıdaki alan listesi tarihsel bağlam için korunmuştur, güncel şema için `api/Models/Payments/Payout.cs`/`PayoutRecipient.cs`'e bakılmalıdır.

🛠️ **Netleştirme — v7'deki hata:** v7'de `Payout` tablosu tek bir `WinnerPlayerId` ve `MatchId` üzerinde **unique index** taşıyordu. Bu, `03-game-rules.md`'nin "beraberlik durumunda `Match.Winners` birden fazla eleman içerir, havuz eşit bölünür" kuralıyla (bkz. `03-game-rules.md` "NİHAİ VE TEK KAYNAK" maddesi, ve bu dosyada Bölüm 1.1) doğrudan çelişiyordu: unique index bir maça yalnızca **tek** bir ödeme satırı yazılmasına izin veriyor, oysa N ortak kazanan varsa N farklı adrese N ayrı on-chain işlem gerekir. v8'de bu, maç seviyesinde tek bir agregatör (`Payout`) ile kazanan seviyesinde 1-N ilişkili bir alt tablo (`PayoutRecipient`) ayrılarak çözüldü. Normal (tek kazanan) senaryoda bu, tek bir `PayoutRecipient` satırı üretir — davranış dışarıdan aynıdır, yalnızca N=1 olduğunda de facto eski modele indirgenir.

**`Payout`** — maç başına tam olarak bir satır (idempotency anahtarı burada kalır):

| Alan            | Tip                     | Açıklama                                                                                                                                                                                                                               |
| --------------- | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Id`            | Guid (PK)               |                                                                                                                                                                                                                                        |
| `MatchId`       | Guid (FK), unique index | idempotency anahtarı — maça yalnızca bir `Payout` (agregatör) satırı yazılır                                                                                                                                                           |
| `TotalPoolLtc`  | decimal(18,8)           | Fiilen ödemesi onaylanmış N oyuncunun toplam girişi (Standart’ta normal senaryoda N=4, VIP’de erken başlatılmadıysa `Room.MaxPlayers`'a eşit — bkz. Bölüm 1.1 terim netleştirmesi; yuvarlanmamış ara değerden tek seferde yuvarlanmış) |
| `CommissionLtc` | decimal(18,8)           | `TotalPoolLtc * CommissionRate`                                                                                                                                                                                                        |
| `WinnerCount`   | int                     | `Match.Winners.Count` — normal senaryoda 1, beraberlikte N                                                                                                                                                                             |
| `Status`        | enum (Bölüm 5.2)        | Agregatör durumu — **tüm** `PayoutRecipient` satırları `Completed` olduğunda `Completed`'e geçer, herhangi biri `Failed`'de kalırsa `Failed` (bkz. Bölüm 5.2)                                                                          |
| `CreatedAt`     | DateTime                |                                                                                                                                                                                                                                        |
| `CompletedAt`   | DateTime?               | Tüm `PayoutRecipient`'lar tamamlandığında set edilir                                                                                                                                                                                   |

**`PayoutRecipient`** — kazanan başına bir satır (`Payout` ile 1-N, N=1 normal senaryoda):

| Alan                  | Tip              | Açıklama                                                                                                                                                                                                                                                                                    |
| --------------------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Id`                  | Guid (PK)        |                                                                                                                                                                                                                                                                                             |
| `PayoutId`            | Guid (FK)        |                                                                                                                                                                                                                                                                                             |
| `WinnerPlayerId`      | Guid (FK)        |                                                                                                                                                                                                                                                                                             |
| `PayoutAddress`       | string           | Kazananın `PaymentInvoice.PayoutAddress`'inden invoice-immutable kuralıyla aynen kopyalanır (Bölüm 1.5), bu satırda da değiştirilemez                                                                                                                                                       |
| `AmountLtc`           | decimal(18,8)    | Bu kazanana fiilen gönderilen tutar — `GrossPerWinnerLtc - estimatedFee` (N=1 ve N>1 için aynı formül, bkz. Bölüm 2.2 düzeltilmiş formül; `GrossPerWinnerLtc = (TotalPoolLtc - CommissionLtc) / WinnerCount`, N=1'de `WinnerCount=1` olduğundan doğal olarak eski tekil formüle indirgenir) |
| `NetworkFeeLtc`       | decimal(18,8)?   | 🛠️ **Yalnızca gerçekleşen (actual) fee kalıcı kayda geçer** — `null` olarak başlar, o kazanana ait on-chain işlem doğrulandıktan sonra doldurulur (Bölüm 2.6). Her `PayoutRecipient` kendi ayrı on-chain işlemine sahip olduğundan, fee de kazanan bazında ayrı ayrı tutulur.               |
| `Status`              | enum (Bölüm 5.2) | Bu kazanana özel durum — bir kazanana giden işlem `Failed`/retry döngüsündeyken diğerleri bundan etkilenmez                                                                                                                                                                                 |
| `BtcPayTransactionId` | string?          |                                                                                                                                                                                                                                                                                             |
| `RetryCount`          | int              |                                                                                                                                                                                                                                                                                             |
| `NextRetryAt`         | DateTime?        |                                                                                                                                                                                                                                                                                             |
| `CompletedAt`         | DateTime?        |                                                                                                                                                                                                                                                                                             |

- Unique constraint: `(PayoutId, WinnerPlayerId)` — aynı kazanan için aynı `Payout`'a iki kez satır yazılamaz (idempotency, ikinci katman).
- 🛠️ **N>1 durumunda pay hesaplaması — düzeltildi (Gemini incelemesinde bulunan gerçek bir matematiksel çelişki):** Önceki formül (`PerWinnerAmountLtc = (TotalPoolLtc - CommissionLtc - Σ NetworkFeeLtc) / WinnerCount`) yanlıştı çünkü **hiçbir on-chain işlem broadcast edilmeden önce tüm kazananların toplam fee'sinin bilinmesini varsayıyordu** — bu, Bölüm 2.6'daki "fee yalnızca kendi işlemi tamamlandıktan sonra bilinir" kuralıyla doğrudan çelişiyordu. Doğru, iki aşamalı hesap:
  1. **Fee'den bağımsız brüt pay** (havuz bölünmesi anında hesaplanır, hiçbir on-chain veriye ihtiyaç duymaz): `GrossPerWinnerLtc = (TotalPoolLtc - CommissionLtc) / WinnerCount`. Bu değer tüm kazananlar için **eşittir** ve `Payout` oluşturulduğu an belli olur.
  2. **Kazanana özel net gönderim** (her kazananın kendi ayrı işlemi anında, Bölüm 2.6 ile birebir tutarlı): `PayoutRecipient.AmountLtc = GrossPerWinnerLtc - EstimatedNetworkFeeLtc` (gönderim anında geçici bir tahminle hesaplanır, DB'ye yazılmaz) → işlem broadcast edilir → gerçek fee döndüğünde `PayoutRecipient.NetworkFeeLtc` bu gerçek değerle **bir kez** doldurulur, `AmountLtc` geriye dönük değiştirilmez (Bölüm 2.6'daki kural aynen uygulanır).
  - Gerekçe: bu tasarımda her kazanan yalnızca **kendi** fee'sine ihtiyaç duyar, başka bir kazananın işleminin tamamlanmasını beklemez — N>1 senaryosunda kazananlardan biri `Failed`/retry döngüsündeyken diğerlerinin işlemi bundan etkilenmeyeceği kuralıyla (bkz. `PayoutRecipient.Status` açıklaması) da tutarlıdır. "Σ NetworkFeeLtc" ifadesi tamamen kaldırılmıştır, hiçbir yerde kullanılmaz.
- `PayoutService.ProcessPayout(matchId)` artık `Match.Winners` listesinin tamamını gezip her biri için ayrı bir `PayoutRecipient` INSERT eder ve her biri için **ayrı** bir BTCPay payout çağrısı yapar (bkz. Bölüm 3.2'nin güncellenmiş akışı).

### 2.2.1 `Refund`

(v6 ile aynı şema — `Refund` her zaman tek bir oyuncuya aittir, çoklu-kazanan sorunuyla ilgisi yoktur, bu nedenle burada bir değişiklik gerekmedi.)

### 2.3 Precision, Rounding ve Hesaplama Sırası Kuralları 🔒

- LTC tutarları `decimal(18,8)`, USD tutarları `decimal(18,2)`.
- 🛠️ **Netleştirme — yuvarlama yalnızca kalıcılaştırma sınırında (v7'de eklendi):**
  - Bir hesaplama zinciri (ör. `AmountUsd / LockedUsdPerLtc`, veya `TotalPoolLtc - CommissionLtc - NetworkFeeLtc`) birden fazla ara adımdan oluşuyorsa, **her ara adımda ayrı ayrı `Round` çağrılmaz.** C#'ın `decimal` tipi zaten yüksek hassasiyetli olduğundan, ara değerler doğal hassasiyetleriyle taşınır.
  - **Yuvarlama (`MidpointRounding.ToEven`, 8 ondalık basamağa) yalnızca değer veritabanına yazılmadan hemen önce, bir kez** uygulanır.
  - Gerekçe: her ara adımda yuvarlama yapmak (ör. önce `AmountLtc`'yi yuvarla, sonra ondan `CommissionLtc`'yi hesaplayıp tekrar yuvarla) kümülatif yuvarlama hatası biriktirir; bu, özellikle çok adımlı payout hesaplamasında (havuz → komisyon → fee → net) anlamlı bir sapmaya yol açabilir. Tek seferlik son yuvarlama bu riski ortadan kaldırır.
  - `PaymentMath.cs` bu kuralı somutlaştırır: iç hesaplama metotları (ör. `CalculatePayoutAmount(...)`) yuvarlanmamış `decimal` döner; yalnızca entity'nin `Status`/persist edilecek alanına atanmadan hemen önce `PaymentMath.RoundForPersistence(value)` çağrılır.

### 2.4 `PaymentConfig` — Tam Alan Listesi

(v6 ile aynı liste + 🛠️ WinToWar'ın oda modeli için güncellenen `PlayersPerMatch` (Standart’ta =4 sabit, VIP’de `Room.MaxPlayers`'tan okunur — artık `PaymentConfig`'te sabit bir sayı değil, `Room` entity'sinden gelen bir parametre) ve `LobbyFillTimeoutSeconds` (=300, `GameConfig`'teki değerle aynı, tek kaynaktan okunur) — `EntryFeeUsd` de aynı şekilde Standart’ta sabit $1, VIP'de `Room.EntryFeeUsd`'den okunur — `PriceCacheFreshSeconds`, `PriceCacheStaleMaxSeconds`, `PriceQuoteValiditySeconds`, `PriceOracleTimeoutSeconds`, `PaymentToleranceRate`, `RefundOverpaymentThresholdUsd`, `RequiredConfirmations`, `MatchmakingTimeoutSeconds`, `WebhookSignatureHeader`, `WebhookMaxAgeSeconds`, `PayoutRetryCount`, `PayoutRetryBaseDelaySeconds`, `PayoutRetryJitterSeconds`, `RefundRetryCount`, `RefundRetryBaseDelaySeconds`, `RefundRetryJitterSeconds`, `ReconciliationIntervalSeconds`, `ReconciliationLockTimeoutSeconds`, `ReconciliationScanWindowMinutes`, `NetworkFeeResponsibility`, `WebhookEventRetentionDays`. `sha256=` prefix config'de değil, sabit kod içinde.)

### 2.5 Kültür ve Serileştirme 🔒

(v6 ile aynı — DTO'larda parasal alanlar açıkça `string`.)

### 2.6 Network Fee Sorumluluğu — Yalnızca Actual Persist Edilir (v7'de sadeleştirildi, v8'de `PayoutRecipient` bazına taşındı) 🔒

> ⚠️ **v10'da süpersede edildi:** Payout artık on-chain gönderim olmadığından (Wallet kredisi) network fee kavramı payout'ta yok. Bu bölüm yalnızca tarihsel bağlam için korunmuştur. `PaymentConfig.NetworkFeeResponsibility` alanı hâlâ mevcut ama artık yalnızca dokümantasyon amaçlı bir etiket.

- `PayoutRecipient.AmountLtc` (o kazanana net) `= GrossPerWinnerLtc - PayoutRecipient.NetworkFeeLtc` (bkz. Bölüm 2.2'deki düzeltilmiş N>1 formülü — N=1 durumunda `GrossPerWinnerLtc = TotalPoolLtc - CommissionLtc`, formül aynı kalır); havuzdan düşülür.
- 🛠️ **Netleştirme (v7'de v6'daki `NetworkFeeSource` enum'ı kaldırılıp basitleştirildi; v8'de alan `Payout`'tan `PayoutRecipient`'a taşındı çünkü her kazananın kendi ayrı on-chain işlemi ve dolayısıyla kendi ayrı gerçek fee'si vardır):** `PayoutRecipient.NetworkFeeLtc` alanına **yalnızca BTCPay'in o kazanana ait payout işlemi broadcast edildikten/tamamlandıktan sonra döndürdüğü gerçek (actual) on-chain fee** kalıcı olarak yazılır. Tahmini (estimated) fee:
  - Yalnızca payout gönderilmeden **önce**, o kazanana ne kadar net gönderileceğini hesaplamak için **geçici bir değişken** olarak kullanılır (DB'ye yazılmaz).
  - `PayoutRecipient` satırı ilk oluşturulduğunda `NetworkFeeLtc = null`, `AmountLtc` bu geçici tahminle hesaplanıp gönderilir (cüzdanın işlemi oluşturabilmesi için bir tahmine ihtiyacı olduğundan bu kaçınılmazdır).
  - BTCPay işlemi tamamlayıp gerçek fee'yi raporladığında (webhook veya reconciliation ile), `NetworkFeeLtc` bu gerçek değerle **bir kez** doldurulur. `AmountLtc` (kazanana zaten gönderilmiş olan net tutar) **geriye dönük değiştirilmez** — yalnızca `NetworkFeeLtc` denetim kaydı olarak tamamlanır.
  - Gerekçe: "estimated" ve "actual" olarak iki ayrı alan/enum tutmak (v6'daki yaklaşım) gereksiz karmaşıklık ekliyordu; asıl ihtiyaç, **kalıcı kayıtta yalnızca gerçek değerin bulunmasını** garanti etmekti. Bu, `NetworkFeeLtc`'yi nullable yaparak ve "doldurulana kadar null kalır, doldurulduğunda gerçek değerdir" kuralıyla daha basit şekilde sağlanır. Bunun `Payout`'ta değil `PayoutRecipient`'ta tutulması, N>1 senaryosunda her kazananın işleminin farklı bir zamanda/farklı bir fee ile onaylanabilmesinin (birinin fee'si gelip diğerininki gelmeyebilir) doğal sonucudur.

### 2.7 BTCPay Invoice Expiry Senkronu 🔒

(v6 ile aynı.)

### 2.8 Zaman Soyutlaması — `TimeProvider` 🔒

(v6 ile aynı.)

---

## 3. AKIŞ DİYAGRAMLARI 🔒

### 3.1 Giriş ödemesi akışı

> 🛠️ **Sıra düzeltmesi (docs/09-eksik-tarama.md denetimi, Faz 4):** Aşağıdaki
> diyagram önceden INSERT'i BTCPay çağrısından ÖNCE gösteriyordu; gerçek kod (bkz.
> `PaymentService.cs` kod içi yorumu) bunun tersini, teknik olarak zorunlu bir
> nedenle yapıyor: `BtcPayInvoiceId` (INSERT'in unique idempotency anahtarı)
> yalnızca BTCPay çağrısından SONRA bilinebilir; ayrıca bir DB transaction'ını bir
> dış HTTP çağrısı boyunca açık tutmak (BTCPay yavaş/zaman aşımına uğrarsa DB
> satırlarını gereksiz kilitler) yanlış olurdu. Diyagram kodun gerçek sırasına
> göre güncellenmiştir — davranışsal bir değişiklik değildir.

```
Oyuncu → POST /api/matches/{id}/payments
  │
  ▼
PaymentService.CreateInvoice()
  │  - IPriceOracle.GetRate() [single-flight]: fresh→stale(+warning)→sağlayıcılar→unavailable
  │  - AmountLtc = AmountUsd / LockedUsdPerLtc  [yuvarlanmamış ara değer]
  │  - (v10: adres doğrulama adımı yok — invoice hiçbir LTC adresi almaz)
  ▼
BTCPay: Create Invoice (expirationTime senkron) → BtcPayInvoiceId, BIP-21 URI
  ▼
PaymentMath.RoundForPersistence(AmountLtc) → PaymentInvoice(Status=Pending) atomic INSERT
  │  [transaction yalnızca bu tek INSERT'i kapsar — dış HTTP çağrısı transaction dışındadır]
  ▼
Frontend QR render → Oyuncu öder
  ▼
BTCPay Webhook → POST /api/webhooks/btcpay
  │  - imza doğrulanır (WebhookSignatureHeader + sha256= prefix)
  │  - EventId ProcessedWebhookEvents'te var mı? → varsa no-op
  │  - PaymentInvoice bulunur
  │  - MONOTONLUK KONTROLÜ (Bölüm 5.4): webhook'un bildirdiği state, mevcut state'ten
  │    daha düşük StatusRank'e sahipse → yok sayılır, ILogger'a "geriye dönük webhook
  │    yok sayıldı" olarak loglanır, event yine de ProcessedWebhookEvents'e yazılır
  │  - tolerans kontrolü, RequiredConfirmations kontrolü
  │  - ileri geçiş geçerliyse: Status = Confirmed
  ▼
SignalR "PaymentConfirmed" → Oyuncu maça alınır
```

### 3.2 Maç sonu payout akışı (v8'de çoklu-kazanan için güncellendi)

> ⚠️ **v10'da süpersede edildi:** Aşağıdaki diyagramdaki "BTCPay Payout çağrısı" ve "reconciliation" adımları artık yok — `PayoutService.ProcessPayoutAsync` havuzu hesaplar, `Payout`+`PayoutRecipient` satırlarını yazar VE aynı transaction içinde `WalletService.CreditAsync` ile kazananların bakiyesine kredi uygular, tek adımda biter. `PayoutRecipient.Status`/retry/reconciliation kavramları kalktı.

```
Match Finished (Match.Winners = [p1] VEYA [p1, p2, ...])
  │
  ▼
PayoutService.ProcessPayout(matchId)  [transaction içinde]
  │  - Payout(MatchId=X) var mı? → no-op (agregatör seviyesinde idempotency)
  │  - Confirmed invoice'lardan TotalPoolLtc [yuvarlanmamış]
  │  - CommissionLtc = TotalPoolLtc * CommissionRate  [yuvarlanmamış ara değer]
  │  - WinnerCount = Match.Winners.Count
  │  - PaymentMath.RoundForPersistence(...) ile Payout(WinnerCount, Status=PayoutPending) INSERT
  │
  │  - Match.Winners içindeki HER oyuncu için (döngü, WinnerCount=1 ise tek iterasyon):
  │      - estimatedFee = BTCPay'den alınan tahmini fee  [yalnızca hesaplama için, persist edilmez]
  │      - GrossPerWinnerLtc = (TotalPoolLtc - CommissionLtc) / WinnerCount  [yuvarlanmamış, fee'den bağımsız, tüm kazananlar için eşit]
  │      - AmountLtc = GrossPerWinnerLtc - estimatedFee  [yuvarlanmamış, bu kazanana özel]
  │      - PayoutRecipient(PayoutId, WinnerPlayerId, PayoutAddress, AmountLtc, Status=PayoutPending, NetworkFeeLtc=null) INSERT
  ▼
Her PayoutRecipient için BAĞIMSIZ: BTCPay Payout çağrısı (estimatedFee ile gönderilir) → TransactionId → Status = PayoutSent
  ▼
Reconciliation: her PayoutRecipient için ayrı ayrı on-chain doğrulama, BTCPay gerçek fee'yi raporlar
  │  - PayoutRecipient.NetworkFeeLtc = actualFee (bir kez, yuvarlanarak doldurulur)
  │  - PayoutRecipient.Status = Completed
  ▼
Tüm PayoutRecipient'lar Completed olduğunda → Payout.Status = Completed → SignalR "PayoutCompleted"
   (bir PayoutRecipient Failed/retry döngüsündeyken diğerleri bundan etkilenmez ve bağımsız ilerler)
```

### 3.3 Refund akışı

(v6 ile aynı yapı.)

---

## 4. ÇİFTE ÖDEME / DUPLICATE WEBHOOK KORUMASI 🔒

- `Status == Pending` guard + unique constraint'ler (`Payout.MatchId` maç seviyesinde, `(PayoutId, WinnerPlayerId)` kazanan seviyesinde — bkz. Bölüm 2.2) + `ProcessedWebhookEvents` (Bölüm 8.4) + **monotonluk guard'ı** (Bölüm 5.4) — dört katmanlı koruma.

---

## 5. STATE MACHINE'LER 🔒

### 5.1 `PaymentInvoice.Status`

```
Pending(rank=0) → Confirmed(rank=1)
   │
   ├──→ Expired(terminal)
   ├──→ Refunded(terminal)
   └──→ Failed(terminal)
```

### 5.2 `PayoutRecipient.Status` (kazanan bazında) ve `Payout.Status` (maç bazında agregatör) — v8'de netleştirildi

> ⚠️ **v10'da süpersede edildi:** Bu state machine tamamen kaldırıldı — `Payout`/`PayoutRecipient` artık `Status` alanı taşımıyor. Kredi tek bir transaction içinde senkron uygulandığından ara durum (Pending/Sent) hiç oluşmuyor, dolayısıyla bir state machine'e ihtiyaç yok.

`PayoutRecipient.Status` — her kazanan için bağımsız ilerler:

```
PayoutPending(rank=0) → PayoutSent(rank=1) → Completed(rank=2)
       │                       │
       │                       └──→ Failed → (retry) → PayoutSent
       └──→ Failed → (retry) → PayoutPending
```

`Payout.Status` (agregatör) — kendi rank'i yok, doğrudan webhook almaz; yalnızca kendi `PayoutRecipient` çocuklarının durumundan **türetilir**:

- Tüm `PayoutRecipient`'lar `Completed` → `Payout.Status = Completed`, `Payout.CompletedAt` set edilir.
- En az bir `PayoutRecipient` `Failed` (retry hakları tükenmiş, bkz. Bölüm 10) ve geri kalanlar `Completed`/`PayoutSent` → `Payout.Status = Failed` (kısmi tamamlanma; hangi kazananın etkilendiği yalnızca `PayoutRecipient` satırlarından okunur, `Payout` bunu ayrıca tekrar tutmaz).
- Diğer tüm durumlarda (en az biri hâlâ `PayoutPending`/`PayoutSent`) → `Payout.Status = PayoutPending` (agregatör hâlâ işleniyor).
- Bu türetme `PayoutService` içinde her `PayoutRecipient` güncellemesinden sonra tek bir yerden (ör. `RecalculatePayoutAggregateStatus(payoutId)`) yapılır — birden fazla yerde elle tekrar hesaplanmaz (bkz. `06-coding-standards.md` "Kod Tekrarını Önleme").
- N=1 (normal, tek kazanan) senaryoda bu iki state machine'in davranışı pratikte birebir örtüşür — agregasyon katmanı yalnızca N>1 olduğunda fark yaratır.

### 5.3 `Refund.Status`

```
RefundPending(rank=0) → RefundSent(rank=1) → Completed(rank=2)
       │                       │
       │                       └──→ Failed → (retry) → RefundSent
       └──→ Failed → (retry) → RefundPending
```

### 5.4 Monotonluk Kuralı (v7'de eklendi) 🔒

- 🛠️ **Netleştirme:** BTCPay'in aynı invoice/payout/refund için webhook event'lerini **sırasız** (out-of-order) teslim etme ihtimaline karşı (ör. `Confirmed` bildiren bir event işlendikten sonra, ağ gecikmesi nedeniyle geç kalmış bir `Processing`/daha erken durumu bildiren event gelmesi), her state machine için bir **`StatusRank`** (sabit, artan bir tam sayı sıralaması) tanımlanır.
- Kural: **Bir webhook, mevcut kaydın `StatusRank`'inden daha düşük veya eşit bir rank'e karşılık gelen bir state'i bildiriyorsa, bu geçiş uygulanmaz.** Yalnızca mevcut rank'ten **daha yüksek** bir rank'e geçiş kabul edilir (terminal state'lere — `Expired`/`Refunded`/`Failed`/`Completed` — ulaşıldıktan sonra hiçbir geçiş kabul edilmez, bunlar en yüksek rank'e sahip kabul edilir).
- Reddedilen (geriye dönük) bir webhook, hata olarak değil **beklenen bir durum** olarak `ILogger` ile bilgi seviyesinde loglanır ("stale/out-of-order webhook ignored, current=X incoming=Y") ve yine de `ProcessedWebhookEvents`'e event id'siyle kaydedilir (aynı event'in tekrar tekrar bu kontrolden geçmesini önlemek için).
- Bu kural, Bölüm 4'teki idempotency korumasından **ayrı ve tamamlayıcı** bir katmandır: idempotency "aynı event'i iki kez işleme" sorusuna, monotonluk ise "farklı event'ler sırasız geldiğinde state'i asla geriye almama" sorusuna cevap verir.

---

## 6. API RESPONSE FORMATI 🔒

(v6 ile aynı.)

---

## 7. DTO VE SignalR EVENT LİSTESİ 🔒

(v6 ile aynı — `AmountLtc` ve benzeri parasal alanlar DTO'larda açıkça `string`.) 🛠️ **v8 eklemesi:** `PayoutDto` artık tekil bir kazanan alanı yerine `PayoutRecipientDto[] Recipients` alanı taşır (her biri `WinnerPlayerId`, `PayoutAddress`, `AmountLtc: string`, `Status`, `BtcPayTransactionId` içerir); `Match.Winners` tek elemanlıysa bu dizi tek elemanlıdır, birden fazla ortak kazanan varsa dizi de o kadar eleman içerir. SignalR `"PayoutCompleted"` event'i bu güncellenmiş `PayoutDto`'yu yayınlar; frontend `web/lib/payments/types.ts` bu değişikliği (tekil alanlar yerine `recipients` dizisi) yansıtacak şekilde güncellenir.

---

## 8. GÜVENLİK, EŞZAMANLILIK VE GÖZLEMLENEBİLİRLİK 🔒

### 8.1 Güvenlik

(v6 ile aynı — `WebhookSignatureHeader` configurable, `sha256=` prefix sabit kod içinde, sabit zamanlı karşılaştırma, replay koruması.)

### 8.2 Loglama standardı

(v6 ile aynı, artık monotonluk reddi de bu scope kurallarıyla loglanır.)

### 8.3 Transaction ve eşzamanlılık disiplini

(v6 ile aynı — `READ COMMITTED` + unique-violation-as-no-op.)

### 8.4 Webhook Event Idempotency Tablosu

(v6 ile aynı — `ProcessedWebhookEvents(EventId, ProcessedAt, PaymentInvoiceId)`, Bölüm 5.4'teki monotonluk kontrolüyle birlikte çalışır.)

---

## 9. TEST SENARYOLARI 🔒

v6'daki tüm senaryolara ek olarak:

- [ ] Sırasız webhook: önce `Confirmed`'e geçiren event işlenir, ardından daha düşük rank'li (ör. yeniden `Pending` bildiren) bir event gelir — state `Confirmed`'de kalır, geçiş reddedilir ve loglanır
- [ ] Terminal state'e (`Expired`/`Failed`/`Completed`) ulaşmış bir kayda **hiçbir** yeni webhook state değiştiremiyor
- [ ] Reddedilen geriye dönük webhook yine de `ProcessedWebhookEvents`'e yazılıyor (aynı event tekrar gelirse ikinci kez "ignored" logu üretmiyor, direkt no-op)
- [ ] Çok adımlı bir payout hesaplamasında (`TotalPoolLtc → CommissionLtc → GrossPerWinnerLtc → AmountLtc(tahmini fee ile) → PayoutRecipient.NetworkFeeLtc(gerçek fee, sonradan) → PayoutRecipient.AmountLtc(değişmez)`) ara adımlarda `Round` çağrılmadığı, yalnızca son persist adımında yuvarlandığı — bilinen bir girdi/çıktı çiftiyle doğrulanıyor (ara değerlerin tam hassasiyetle taşındığı bir birim testiyle)
- [ ] `PayoutRecipient.NetworkFeeLtc` ilk oluşturulduğunda `null`, yalnızca o kazanana ait reconciliation'dan gelen gerçek fee ile bir kez dolduruluyor; hiçbir zaman tahmini bir değerle DB'ye yazılmıyor
- [ ] **Çoklu kazanan (N>1) senaryosu uçtan uca çalışıyor:** `Match.Winners` 2+ oyuncu içerdiğinde, `Payout` altında 2+ `PayoutRecipient` satırı oluşuyor, her biri kendi `PayoutAddress`'ine, kendi ayrı BTCPay işlemiyle, havuzun eşit bölünmüş payını alıyor; `(PayoutId, WinnerPlayerId)` unique constraint'i ihlal edilmiyor
- [ ] Bir `PayoutRecipient` `Failed`/retry döngüsündeyken, aynı `Payout`'a bağlı diğer `PayoutRecipient` satırları bundan etkilenmeden bağımsız ilerliyor; `Payout.Status` yalnızca tüm çocuklar `Completed` olduğunda `Completed`'e geçiyor
- [ ] Single-flight: eşzamanlı 50 istek, boş cache — dış API'ye yalnızca 1 çağrı (v6'dan devam)
- [ ] `PriceOracleSource` cache'ten dönen kurlarda dahi gerçek provider adını koruyor (v6'dan devam)

---

## 10. UÇ DURUMLAR VE HATA YÖNETİMİ 🔒

(v6 ile aynı — retry+jitter, reconciliation kapsamı, son çare senaryosu. Ek olarak: reconciliation job'un `PayoutRecipient.NetworkFeeLtc`'yi doldurma adımı da artık yalnızca `null` olan kayıtlar için, **kazanan bazında** çalışır — zaten dolu bir kayıt üzerine tekrar yazma yapılmaz, bu da kendi başına bir idempotency garantisidir; N>1 senaryoda reconciliation her `PayoutRecipient`'ı ayrı ayrı tarar, biri dolduruldu diye diğerleri atlanmaz.)

- 🛠️ **Maç ortasında disconnect/terk eden oyuncunun bakiyesi (08-eksik-alan.md'den taşındı):** Bu sorunun ekonomik cevabı zaten Bölüm 1 ve `03-game-rules.md`'deki mekaniklerin doğal bir sonucudur, ayrı bir refund akışı **gerekmez**: giriş ücreti maça başlarken tahsil edilip `TotalPool`'a girmiştir; terk eden oyuncu `03-game-rules.md` Bölüm 10'daki `AbandonmentTimeoutSeconds` kuralıyla elenmiş sayılır, bölgeleri nötrleşir (bkz. Bölüm 8'e eklenen not), ödediği giriş ücreti **iade edilmez** ve havuzda kalıp maçı bitiren kazanana gider — tıpkı savaşarak elenen bir oyuncu gibi muamele görür, "terk etme" ayrı bir finansal durum değildir. ❓ Müşteriye doğrulatılmalı: kasıtlı terk ile bağlantı sorunu nedeniyle terk arasında (ör. `AbandonmentTimeoutSeconds` içinde geri dönerse) bir ayrım/kısmi iade isteniyor mu, yoksa bu basit "iade yok" kuralı yeterli mi?
- 🚩❓ **Teknik arıza kaynaklı iade politikası (08-eksik-alan.md'den taşındı):** Sunucu çökmesi, BTCPay kesintisi vb. platform kaynaklı bir arıza maçı/ödemeyi etkilerse — 🛠️ **varsayım:** otomatik iade **yapılmaz**, `SupportTicket` üzerinden (bkz. `07-pages.md` `/destek`) manuel admin onaylı iade akışı işletilir (`AdminPaymentsController` zaten mevcut `Refund` altyapısını kullanır, bkz. Bölüm 2). Gerekçe: platform arızasının gerçekten oyuncudan kaynaklanmadığını otomatik ayırt edecek güvenilir bir sinyal yok; yanlış otomatik iade, kaybeden bir oyuncunun "bağlantım koptu" diye sahte iddiada bulunmasına kapı aralar. Bu bir mühendislik tercihi değil, müşterinin risk iştahına bağlı bir iş kararıdır — ❓ doğrulanmalı.

## 10.1 Yasal/Finansal Uyum — Mühendislik İçin Ayrılmış Alanlar 🚩 (08-eksik-alan.md'den taşındı)

> Bu bölümdeki maddeler **mühendislik kararı değildir** — müşterinin bir avukat/mali müşavirle netleştirmesi gereken iş/hukuk kararlarıdır. Aşağıda yalnızca, müşteri bu kararları verdiğinde geliştirmenin bloklanmaması için **veri modelinde şimdiden ayrılan yer** listelenmiştir; hiçbiri şu an aktif olarak zorunlu kılınmaz/uygulanmaz.

- 🚩❓ **KYC/AML zorunluluğu.** Gerçek para + kripto + rekabetli oyun kombinasyonu birçok ülkede kimlik doğrulama/kara para aklamayı önleme mevzuatına girebilir. 🛠️ Şimdilik yalnızca `Wallet`/`Player` modelinde ileride doldurulabilecek nullable bir `KycStatus` alanı (enum: `NotRequired`, `Pending`, `Verified`, `Rejected` — varsayılan `NotRequired`) ayrılır; üçüncü parti bir KYC sağlayıcı entegrasyonu **şimdi yazılmaz**.
- 🚩❓ **Coğrafi/yaş kısıtlaması.** Parayla beceri oyunu birçok yargı alanında kumar mevzuatına girer, bazı ülkelerde tamamen yasaktır; 18 yaş altı erişimi engellenmelidir. 🛠️ Şimdilik yalnızca kayıt formunda (bkz. `07-pages.md` `/kayit`) bir "18 yaşından büyüğüm" beyan onayı (checkbox, `Player.AgeConfirmedAt`) zorunlu tutulur — bu bir yaş **doğrulaması** değil, yalnızca bir beyan/sorumluluk kaydıdır. IP bazlı coğrafi engelleme (geo-blocking) altyapısı **şimdi kurulmaz**; müşteri hedef ülke listesini netleştirdiğinde `Program.cs`'e middleware seviyesinde eklenebilecek şekilde mimari buna açık bırakılır (bkz. `02-architecture.md` katman kuralları).
- ❓ **Sorumlu oyun (responsible gambling) önlemleri.** Bazı yargı alanlarında günlük yatırma limiti/kendini oyundan hariç tutma gibi özellikler yasal zorunluluktur. 🛠️ Şimdilik uygulanmaz (YAGNI — müşteri istemedi, hedef ülkeler netleşmeden hangi önlemin gerekli olduğu da bilinemez); `PaymentConfig`'e ileride bir `DailyDepositLimitUsd` (nullable) alanı eklenmesi kolaydır, şimdiden bir yer tutucu **eklenmez** (henüz kullanılmayan bir alan, YAGNI'yi ihlal eder).

---

## 11. KABUL KRİTERLERİ (DEFINITION OF DONE)

- [ ] `dotnet build` / `npm run build` hatasız.
- [ ] Bölüm 3'teki tüm akışlar regtest/testnet üzerinde uçtan uca doğrulanmış.
- [ ] Bölüm 9'daki tüm test senaryoları geçiyor.
- [ ] **Aynı cache miss sırasında yalnızca tek dış API çağrısı yapılır** (single-flight doğrulanmış).
- [ ] **Payment state machine geriye dönmez** — `StatusRank` kontrolü tüm webhook işleme kodunda uygulanmış.
- [ ] **`PriceOracleSource` cache nedeniyle değişmez; gerçek provider saklanır** — `"Cache"` değeri hiçbir yerde oluşmuyor.
- [ ] **`PayoutRecipient.NetworkFeeLtc` olarak yalnızca gerçekleşen (actual) fee persist edilir** — tahmini değer hiçbir zaman DB'ye yazılmıyor, yalnızca geçici hesaplama girdisi olarak kullanılıyor.
- [ ] Yuvarlama yalnızca kalıcılaştırma sınırında, tek seferde yapılıyor; ara adımlarda `Round` çağrısı yok.
- [ ] `ProcessedWebhookEvents` tablosu var, monotonluk kontrolüyle birlikte çalışıyor.
- [ ] Oyuncu `Match.Status = Lobby` iken `LeaveLobby` çağırdığında tam otomatik refund tetikleniyor; `Countdown`/`Playing` durumunda bu aksiyon reddediliyor.
- [ ] **WinToWar — `LobbyFillTimeoutSeconds = 300` (5 dakika) doğru çalışıyor**, süre dolduğunda otomatik refund **tetiklenmiyor**, bunun yerine oyuncu başına "İptal Et/Bakiyeyi İade Et" veya "Beklemeye Devam Et" seçimi sunuluyor; "İptal Et" seçilirse yalnızca o oyuncu için refund tetikleniyor, diğer oyuncular etkilenmiyor.
- [ ] **VIP oda havuzu doğru hesaplanıyor:** `TotalPoolUsd = Room.EntryFeeUsd × Room.MaxPlayers` farklı giriş ücreti/oyuncu sayısı kombinasyonlarıyla (ör. $5 × 6 kişi, $0.5 × 10 kişi) doğru sonuç veriyor, sabit $12 varsayımıyla kodlanmamış.
- [ ] **Practice odalarda ödeme akışı hiç tetiklenmiyor** — `PaymentInvoice`/`Payout`/`PayoutRecipient` satırı oluşmuyor, katılımda bakiye kontrolü yapılmıyor.
- [ ] **Wallet modeli çalışıyor:** Bir top-up invoice (`MatchId=null`) onaylandığında `Wallet.BalanceUsd` artıyor; odaya katılırken bakiye yeterliyse yeni bir LTC işlemi açılmadan doğrudan bakiyeden düşülüyor; `WithdrawalRequest` oluşturulduğunda bakiye anında düşüyor, `Failed`/`Rejected` olduğunda geri ekleniyor; `Wallet.BalanceUsd` hiçbir senaryoda negatife düşmüyor.
- [ ] Ödeme modülünde hiçbir yerde doğrudan `DateTime.UtcNow` çağrısı yok.
- [ ] Retry gecikmeleri jitter içeriyor.
- [ ] Parasal DTO alanları açıkça `string` tipinde.
- [ ] Mainnet'e geçilmemiş; rapor bu durumu belirtiyor.
- [ ] Seçilen tüm 🛠️ varsayımların gerekçeleriyle özeti raporlanmış.

---

## 12. ÇALIŞMA YÖNTEMİ (ÖZET)

Bölüm 0.2'deki sırayı izle. v8'de özellikle dikkat edilecek yeni madde: **`Payout`/`PayoutRecipient` ayrımı** (Bölüm 2.2) — `PayoutService` her zaman `Match.Winners` listesinin tamamını gezip kazanan başına ayrı bir `PayoutRecipient` ve ayrı bir BTCPay çağrısı üretmeli; tekil-kazanan varsayımıyla kodlanmamalı (N=1 özel durum değil, genel akışın N=1 hali olarak ele alınmalı). v7'den devam eden ve hâlâ kritik olanlar: state machine'lerin `StatusRank` ile monotonluğu (Bölüm 5.4) — webhook'lar sırasız gelebilir, hiçbir geçiş state'i geriye almamalı; yuvarlamanın yalnızca persist anında, tek seferde yapılması (Bölüm 2.3); `PayoutRecipient.NetworkFeeLtc`'nin yalnızca gerçekleşen fee ile doldurulması, asla tahminle yazılmaması (Bölüm 2.6); cache stampede'e karşı single-flight ve `PriceOracleSource`'un cache'ten bağımsız kalması (Bölüm 1.2). Mainnet'e asla otomatik geçme. Sonunda Bölüm 11'deki checklist'i doğrula ve özet raporla.

---

## 13. DOSYA YAPISI (SADECE EKLENECEKLER) 🔒

**KRİTİK:** Ödeme modülü "ayrı bir katman"dır — bu, **sorumluluk/mimari ayrımı** anlamına gelir, **ayrı bir üst dizin** anlamına GELMEZ. Aşağıdaki dosyalar mevcut `api/Models/` ve `api/Services/` klasörlerinin **içine**, `Payments` alt klasörü olarak eklenir. Kesinlikle `api/Payments/` gibi yeni bir üst-düzey klasör açılmaz.

```
api/
├── PaymentConfig.cs                          ← mevcut GameConfig.cs ile aynı seviyede
├── Models/                                   ← MEVCUT klasör, İÇİNE ekleniyor
│   ├── Player.cs                             ← (mevcut, dokunulmaz)
│   ├── Match.cs                              ← (mevcut, dokunulmaz)
│   ├── ...                                   ← (mevcut diğer dosyalar, dokunulmaz)
│   └── Payments/                             ← YENİ alt klasör, Models/ İÇİNDE
│       ├── PaymentInvoice.cs
│       ├── Wallet.cs                         ← v9: YENİ, bkz. Bölüm 1.9
│       ├── WithdrawalRequest.cs              ← v9: YENİ, bkz. Bölüm 1.9
│       ├── Payout.cs                         ← v8: maç-bazlı agregatör (bkz. Bölüm 2.2)
│       ├── PayoutRecipient.cs                ← v8: YENİ, kazanan-bazlı (1-N, çoklu-kazanan desteği)
│       ├── Refund.cs
│       ├── ProcessedWebhookEvent.cs
│       └── Dtos/
│           ├── CreatePaymentRequest.cs
│           ├── CreatePaymentResponse.cs
│           ├── PaymentStatusDto.cs
│           ├── WalletDto.cs                  ← v9: YENİ
│           ├── WithdrawalRequestDto.cs       ← v9: YENİ
│           ├── PayoutDto.cs                  ← v8: artık PayoutRecipientDto listesi de içerir
│           ├── PayoutRecipientDto.cs         ← v8: YENİ
│           ├── RefundDto.cs
│           └── WebhookDto.cs
├── Services/                                 ← MEVCUT klasör, İÇİNE ekleniyor
│   ├── EconomyTickService.cs                 ← (mevcut, dokunulmaz)
│   ├── MatchManager.cs                       ← (mevcut, dokunulmaz)
│   ├── GameEngine/                           ← (mevcut, dokunulmaz)
│   │   └── ...
│   └── Payments/                             ← YENİ alt klasör, Services/ İÇİNDE
│       ├── IPriceOracle.cs
│       ├── CompositePriceOracle.cs
│       ├── CoinGeckoPriceOracle.cs
│       ├── CoinCapPriceOracle.cs
│       ├── IPaymentProvider.cs
│       ├── BtcPayPaymentProvider.cs
│       ├── PaymentService.cs
│       ├── PayoutService.cs
│       ├── RefundService.cs
│       ├── PaymentMath.cs
│       └── LtcAddressValidator.cs
├── Controllers/                              ← MEVCUT klasör, İÇİNE ekleniyor
│   ├── MatchesController.cs                  ← (mevcut, dokunulmaz)
│   ├── PaymentsController.cs                 ← YENİ dosya, klasörün İÇİNE
│   └── PaymentWebhookController.cs           ← YENİ dosya, klasörün İÇİNE
└── (mevcut api.csproj, Program.cs, appsettings.json'a SADECE gerekli satırlar eklenir)

web/
├── components/
│   └── payments/                             ← YENİ alt klasör, components/ İÇİNDE
│       ├── EntryFeePanel.tsx
│       ├── PayoutAddressInput.tsx
│       └── PaymentStatusBadge.tsx
└── lib/
    └── payments/                             ← YENİ alt klasör, lib/ İÇİNDE
        ├── signalr events, types.ts, ...
```

**Bu bölüm, dokümanın önceki bölümlerinde geçen tüm "Models/Payments/", "Services/Payments/" gibi kısa referansların tam ve bağlayıcı açılımıdır.** Herhangi bir belirsizlik durumunda bu bölüm esas alınır.
