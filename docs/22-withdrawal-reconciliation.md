# 22 — `WithdrawalRequest` Mutabakatı (dar kapsam)

> **Dosya adı notu:** Bu dosyayı `docs/22-withdrawal-reconciliation.md` olarak kaydedin.
>
> `CLAUDE.md`'nin "Her görevde önce oku" tablosuna şu satır eklenir:
> _"Çekim (withdrawal) mutabakat işi → `docs/22-withdrawal-reconciliation.md` (`05-payment.md`'nin üzerine
> inşa eder, oradaki 🔒 iş kurallarını ve v10 kararını değiştirmez)."_

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI / DEĞİŞTİRİLEMEZ KARAR:** Birebir uygulanır.
- 🛠️ **MÜHENDİSLİK VARSAYIMI:** Makul varsayımla ilerle, gerekçelendir, **asla soru sorup bekleme.**
- ❓ Müşteriden ileride doğrulanması gereken nokta.

---

## 0. Bu görev neden var 🔒

`docs/21-payment-sandbox-e2e.md` denetiminde, v10 kararının (2026-08-08) bıraktığı **tek gerçek fonksiyonel
boşluk** tespit edildi:

`WalletService.ApproveWithdrawalAsync` şu sırayı izler:

```
Pending --(atomik UPDATE)--> Approved --> Sent --(SaveChanges/commit)-->
    IPaymentProvider.SendPayoutAsync(...)   ← BTCPay'e gerçek on-chain gönderim
        başarılı  -> Completed
        hata      -> Failed + bakiye iadesi
    --> ProcessedAt + SaveChanges
```

`Sent` yazımı ile `Completed`/`Failed` yazımı **arasında** süreç ölürse (uygulama çökmesi, container restart,
deploy, makine kapanması), kayıt kalıcı olarak `Sent`'te asılı kalır:

- Oyuncunun bakiyesi zaten düşülmüştür (talep anında düşülür, bkz. `05-payment.md` Bölüm 1.9).
- On-chain gönderimin gerçekleşip gerçekleşmediği **bilinmez** — hem "gitti ama yazılamadı" hem "hiç gitmedi"
  mümkündür.
- v10'da `ReconciliationService` kaldırıldığı için bu kaydı kapatacak **hiçbir otomatik mekanizma yoktur**;
  yalnızca elle veritabanı müdahalesiyle çözülür.

`/cuzdan`'daki "Bekleyen Transferler" kartı bu kaydı sonsuza kadar "Gönderildi" olarak gösterir
(`WalletService.ListForPlayerAsync` `Pending`/`Approved`/`Sent` durumlarını listeler) — yani kullanıcı için de
kalıcı bir belirsizliktir.

## 1. Kapsam sınırı — NE YAPILMAYACAK 🔒

Bu görev, v10 kararını **hiçbir şekilde geri almaz**. Aşağıdakiler **kesinlikle kapsam dışıdır ve geri
getirilmez**:

- ❌ `Payout.Status` / `PayoutRecipient.Status` state machine'leri (v10'da kaldırıldı — maç ödülü senkron bir
  `Wallet.BalanceUsd` kredisidir, ara durumu yoktur).
- ❌ `Refund.Status` state machine'i (aynı gerekçe; `Refund` yalnızca bir denetim kaydıdır).
- ❌ Payout/refund için retry + backoff + jitter altyapısı ve ilgili `PaymentConfig` alanları
  (`PayoutRetry*`, `RefundRetry*`).
- ❌ `ReconciliationService` (genel amaçlı tarayıcı), `ReconciliationLock` entity'si/tablosu ve
  `ReconciliationIntervalSeconds`/`ReconciliationLockTimeoutSeconds`/`ReconciliationScanWindowMinutes`
  config alanları.
- ❌ Kazananın parasının tekrar on-chain gönderilmesi.
- ❌ `NetworkFeeLtc` ve türevleri.

🔒 **Gerekçe:** Bu mekanizmaların var olma sebebi, payout/refund'un bir **dış sisteme** (BTCPay) yapılan
asenkron gönderim olmasıydı. v10'dan sonra ikisi de kendi veritabanımızdaki senkron bir bakiye hareketidir;
mutabakat edilecek bir dış durum yoktur. Bunları geri getirmek, müşterinin 2026-08-08 kararını geri almak
anlamına gelir ve `01-workflow-rules.md` Bölüm 0.5 gereği mühendislik varsayımıyla yapılamaz.

**Kapsam yalnızca şudur:** sistemden para çıkaran **tek** gerçek async/on-chain akış olan
`WithdrawalRequest`'in `Sent` durumunda asılı kalan kayıtlarını BTCPay'e sorup kapatmak.

## 2. Hedef davranış 🔒

`Sent` durumunda ve belirli bir süredir (`WithdrawalStuckAfterSeconds`) güncellenmemiş her
`WithdrawalRequest` için:

1. BTCPay'e "bu adrese, bu tutarda bir işlem gerçekten gitti mi?" diye sorulur.
2. **Gittiyse** → `Completed`. Bakiye zaten düşülmüştür, **hiçbir bakiye hareketi yapılmaz**.
3. **Gitmediyse** → `Failed` + tutar oyuncunun bakiyesine **geri eklenir** (`05-payment.md` Bölüm 1.9'daki
   "`Failed`/`Rejected` durumunda bakiyeye geri eklenir" kuralı — `ApproveWithdrawalAsync`'in hata yolunda
   uygulanan davranışın aynısı) ve `WalletService.NotifyBalanceChangedAsync` ile bildirilir.
4. **Belirsizse** (BTCPay'e ulaşılamıyor, yanıt kesin değil) → **hiçbir şey yapılmaz**, kayıt `Sent`'te kalır,
   bir sonraki turda tekrar denenir. 🔒 **Tahminle karar verilmez** — yanlış bir `Failed` kararı, zincire çıkmış
   bir ödemeyle birlikte ikinci kez bakiye kredisi demektir (çift ödeme).

🔒 **Asla yapılmayacak:** Asılı kalmış bir kayıt için gönderimi **yeniden denemek**. Bu iş bir "retry" değil,
bir **durum tespitidir**. Gönderimin gerçekten gitmediği kesinleşirse kayıt `Failed` olur ve oyuncu isterse
yeni bir talep açar; sistem kendiliğinden ikinci bir on-chain gönderim yapmaz.

## 3. Tespit yöntemi 🛠️

`IPaymentProvider`'a tek bir yeni uç eklenir — mevcut soyutlama korunur (`06-coding-standards.md` DTO/mapping
ve YAGNI kuralları):

```csharp
/// Bir çekim gönderiminin BTCPay tarafında gerçekten oluşup oluşmadığını belirler.
/// Kesin "oluştu"/"oluşmadı" ayrımı yapılamıyorsa null döner (kararsız) —
/// çağıran bu durumda state'i DEĞİŞTİRMEZ.
Task<bool?> HasOutgoingTransferAsync(
    string destinationAddress, decimal amountLtc, DateTimeOffset sentAfterUtc, CancellationToken ct);
```

- **`BtcPayGreenfieldProvider`:** store cüzdanının işlem listesi okunur
  (`GET /api/v1/stores/{storeId}/payment-methods/LTC-CHAIN/wallet/transactions`) ve `sentAfterUtc`'den sonraki
  **giden** işlemler arasında hedef adres + tutar eşleşmesi aranır. Bu uç, `docs/21`'de üretilen API key'in
  zaten sahip olduğu `btcpay.store.canviewwallet` izniyle çağrılabilir — **yeni izin gerekmez.**
  HTTP hatası/zaman aşımı → `null`.
- **`FakePaymentProvider`:** her zaman `true` döner (ağsız `Fake` modda gönderim hep başarılı sayılır, mevcut
  `SendPayoutAsync` davranışıyla tutarlı).
- 🛠️ Eşleşme toleransı: tutar karşılaştırması `decimal` üzerinde **tam eşitlik** ile yapılır (gönderilen tutar
  `WithdrawalRequest.AmountLtc`'dir, fee alıcıdan değil cüzdandan düşer). Aynı adrese aynı tutarda birden fazla
  eşleşme bulunursa (nadir ama mümkün) → `true` (en az bir gönderim gerçekleşmiştir).

## 4. Çalıştırma biçimi 🛠️

- `Services/Payments/WithdrawalReconciliationService.cs` — bir `BackgroundService`.
  `Program.cs`'e tek satır `AddHostedService<WithdrawalReconciliationService>()` eklenir.
- `PaymentConfig`'e **iki** yeni alan (fazlası eklenmez):
  - `WithdrawalReconciliationIntervalSeconds` (🛠️ öneri **300**) — tarama sıklığı.
  - `WithdrawalStuckAfterSeconds` (🛠️ öneri **180**) — bir kaydın "asılı" sayılması için `Sent`'te geçirmesi
    gereken en az süre. Normal bir gönderim saniyeler sürer; bu eşik, hâlâ devam eden bir gönderime
    müdahale etmeyi önler.
- 🛠️ **Dağıtık kilit (`ReconciliationLock`) EKLENMEZ.** `02-architecture.md` "Ölçeklenebilirlik" bölümündeki
  🛠️ **tek-instance** varsayımı geçerlidir. Bunun yerine, iki instance kazara çalışsa bile güvenli olması için
  aynı desen kullanılır: durum geçişi **atomik `UPDATE ... WHERE Status = 'Sent'`** ile yapılır (bkz.
  `WalletService.ApproveWithdrawalAsync`'teki `ExecuteUpdateAsync` deseni) — ikinci akış 0 satır günceller ve
  hiçbir şey yapmaz. Bu, kilit altyapısı olmadan çift kredilemeyi yapısal olarak imkânsız kılar.
- `CancellationToken` her async çağrıda taşınır; `Task.Delay` kullanılır, `Thread.Sleep` **yasak**
  (`02-architecture.md` async disiplini).
- Zaman her yerde `TimeProvider` üzerinden alınır; `DateTime.UtcNow` doğrudan çağrılmaz
  (`05-payment.md` Bölüm 0.3).

## 5. Loglama ve gözlemlenebilirlik 🔒

`ILogger<WithdrawalReconciliationService>` ile, `05-payment.md` Bölüm 8.2 scope kurallarına uygun:

- Asılı kayıt bulunduğunda: `WithdrawalRequestId`, `PlayerId`, `Sent`'te geçen süre.
- Karar verildiğinde: hangi karara (`Completed`/`Failed`) hangi kanıtla (TXID veya "eşleşme yok") varıldığı.
- Kararsız (`null`) sonuç: `warning` seviyesinde, "bir sonraki turda tekrar denenecek" notuyla.
- 🔒 Adres/tutar loglanabilir (gizli bilgi değildir); API key/webhook secret **asla** loglanmaz.

## 6. Aşama sırası ⚙️

`01-workflow-rules.md` Bölüm 0.1 gereği aşamalı ilerlenir, her aşama sonunda `dotnet build`:

1. `PaymentConfig`'e iki yeni alan.
2. `IPaymentProvider.HasOutgoingTransferAsync` + iki implementasyon (`BtcPayGreenfieldProvider`,
   `FakePaymentProvider`) + test sahtelerinin (`api.Tests/TestSupport/`) güncellenmesi.
3. `WithdrawalReconciliationService` + `Program.cs` kaydı.
4. Testler (Bölüm 7).
5. Sandbox doğrulaması (Bölüm 8).

## 7. Test senaryoları 🔒

`api.Tests/` altına (mevcut `WalletServiceTests` desenine uygun, SQLite in-memory ile):

- [ ] `Sent`'te ve eşiği aşmış bir kayıt + provider `true` → `Completed`, **bakiye değişmez**.
- [ ] `Sent`'te ve eşiği aşmış bir kayıt + provider `false` → `Failed`, bakiye **tam olarak bir kez** geri eklenir.
- [ ] `Sent`'te ve eşiği aşmış bir kayıt + provider `null` → durum `Sent` kalır, bakiye değişmez, bir sonraki
      turda tekrar denenir.
- [ ] `Sent`'te ama eşiği **aşmamış** kayıt → hiç dokunulmaz (devam eden gönderime müdahale yok).
- [ ] `Pending`/`Approved`/`Completed`/`Failed`/`Rejected` durumundaki kayıtlar → hiç dokunulmaz.
- [ ] Aynı kayıt için mutabakat **iki kez** çalıştırılırsa (eşzamanlı veya art arda) bakiye **iki kez
      eklenmez** — atomik `UPDATE ... WHERE Status='Sent'` guard'ı doğrulanır.
- [ ] Mutabakat, gönderimi **yeniden denemez** — `SendPayoutAsync` hiçbir senaryoda çağrılmaz (sahte provider
      üzerinden çağrı sayısı 0 olarak doğrulanır).

## 8. Sandbox doğrulaması 🔒

`docs/21-payment-sandbox-e2e.md`'nin kurduğu regtest ortamında (`sandbox/btcpay/up.ps1`), **kod okunarak değil
fiilen çalıştırılarak**:

1. Bir çekim talebi oluştur, admin onayla, gönderim gerçekleşsin; ardından kaydı elle `Sent`'e çek (gönderim
   sonrası çökmeyi simüle eder) → mutabakat **`Completed`** yapmalı, bakiye değişmemeli.
2. Bir çekim talebini `Sent`'e çek ama **hiç gönderim yapma** → mutabakat **`Failed`** yapmalı, bakiye geri
   eklenmeli (before/after yazılır).
3. BTCPay'i durdur (`docker compose stop btcpayserver`) → mutabakat kaydı `Sent`'te bırakmalı, `warning`
   loglamalı, bakiye değişmemeli; BTCPay geri geldiğinde bir sonraki turda doğru kararı vermeli.

Her adımın kanıtı (WithdrawalRequestId, TXID, before/after bakiye, log satırı) rapora yazılır.
🔒 **"Muhtemelen çalışıyor" kabul edilmez** (aynı kural, `docs/21` Bölüm 9).

## 9. Kabul kriterleri (Definition of Done)

- [ ] `dotnet build` hatasız, mevcut testler dahil tüm testler geçiyor.
- [ ] Bölüm 7'deki 7 senaryonun tamamı için test eklendi ve geçiyor.
- [ ] Bölüm 8'deki 3 sandbox senaryosu fiilen çalıştırıldı, kanıtlarıyla raporlandı.
- [ ] `PaymentConfig`'e **yalnızca 2** yeni alan eklendi; Bölüm 1'deki yasak alanların hiçbiri geri gelmedi.
- [ ] `git diff`'te `Payout`/`PayoutRecipient`/`Refund` state alanları, retry/backoff kodu veya
      `ReconciliationLock` **yok**.
- [ ] Yeni bir NuGet paketi eklenmedi.
- [ ] Mutabakat hiçbir koşulda ikinci bir on-chain gönderim tetiklemiyor (test ile kanıtlandı).
- [ ] `05-payment.md` Bölüm 10'daki "v10'un bıraktığı bilinen boşluk" notu, bu görev tamamlandığında
      "kapatıldı, bkz. `docs/22`" olarak güncellendi.

## 10. Açık noktalar ❓

- ❓ **Belirsiz (`null`) durumda üst sınır:** Bir kayıt BTCPay'e defalarca sorulmasına rağmen kararsız kalmaya
  devam ederse ne olmalı? 🛠️ **Varsayım:** süresiz olarak `Sent`'te kalır ve taranmaya devam eder; admin
  panelinde görünür olması yeterlidir, otomatik bir "vazgeç" kararı verilmez (yanlış karar gerçek para
  kaybettirir). ❓ Müşteri belirli bir süre sonra otomatik `Failed`+iade isterse bu ayrıca kararlaştırılmalıdır.
- ❓ **Admin görünürlüğü:** `/admin/odemeler` sayfasına "asılı çekimler" için ayrı bir liste isteniyor mu?
  🛠️ **Varsayım:** bu görevde **eklenmez** (YAGNI + `docs/21` Bölüm 10 "UI modüllerine dokunulmaz"); mevcut
  bekleyen-çekim listesi ve loglar yeterlidir.
- ❓ **Aynı adres+tutarın tekrar kullanımı:** Bir oyuncu aynı adrese aynı tutarda iki ayrı çekim yaparsa,
  eşleşme tespiti ikisini ayırt edemez. 🛠️ **Varsayım:** `sentAfterUtc` filtresi pratikte yeterlidir; kesin
  ayrım için `WithdrawalRequest`'e gönderim anında TXID yazmak gerekir — bu, `ApproveWithdrawalAsync`'i
  değiştirmeyi gerektirdiğinden **bu görevin kapsamı dışında** bırakıldı. ❓ Gerekirse ayrı bir görevle
  (`WithdrawalRequest.BtcPayTransactionId` alanı + migration) ele alınır ve tespit kesinleşir.
