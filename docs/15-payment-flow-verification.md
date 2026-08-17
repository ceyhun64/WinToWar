# 15 — Ödeme Akışlarının Uçtan Uca Doğrulanması ve Düzeltilmesi (Müşteri + Admin/Sistem)

> **Dosya adı notu:** Bu dosyayı `docs/15-payment-flow-verification.md` olarak kaydedin ve `CLAUDE.md`'nin
> "Her görevde önce oku" tablosuna şu satırı ekleyin:
> _"Ödeme akışının fiilen çalışıp çalışmadığının denetimi/düzeltilmesi (müşteri ödeme alma + admin/sistem
> ödeme yapma) → `docs/15-payment-flow-verification.md`."_

Bu görev, `docs/14-payment-sandbox.md`'nin kurulum görevinden **bağımsız** bir doğrulama ve düzeltme görevidir.
Sandbox mevcut değilse veya önceki bir çalıştırmadan sonra silinmişse, kullanıcıdan (müşteriden) kişisel bir
BTCPay hesabı açması **istenmez**; self-hosted **regtest** sandbox kendi otomasyonuyla yeniden kurulur/ayağa
kaldırılır. Sandbox tamamen proje geliştirici ortamının bir parçasıdır, kişiye bağlı bir kaynak değildir.

## İlişki — bu dosya neyi değiştiriyor, neyi değiştirmiyor

- **14 = Altyapı:** "BTCPay sandbox'ı kur, otomatikleştir, kullanılabilir hale getir."
- **15 = Uygulama (bu dosya):** "Bu sandbox üzerinde WinToWar'ın gerçekten para alıp gönderebildiğini kanıtla;
  çalışmıyorsa düzelt."
- `14-payment-sandbox.md` Bölüm 4'teki doğrulama akışını temel alır ama iki noktada ondan **farklıdır**:
  1. `14-payment-sandbox.md` yalnızca **müşteri → sistem** yönünü (top-up) merkeze alır; bu dosya **iki yönü
     birden** zorunlu kılar: müşterinin ödeme yapabilmesi (para yatırma/top-up) **ve** sistemin/admin'in
     müşteriye ödeme yapabilmesi (çekim/payout).
  2. `14-payment-sandbox.md`'de "`PayoutService` kaynak kodu hiç değiştirilmeden" ifadesi geçer — o dosya
     yalnızca **ortam/config** kurulumunu kapsadığı için kod değişikliğini bilerek dışarıda bırakır. **Bu
     dosyada durum tersine döner:** eğer denetim sonucunda müşteri veya admin akışlarından biri gerçekten
     çalışmıyorsa, kök nedene göre `PaymentService`/`WalletService`/`PayoutService`/`RefundService`/
     `IPaymentProvider`/`BtcPayGreenfieldProvider`/`PaymentWebhooksController`/`WebhookSignatureValidator`
     dosyalarında düzeltme yapmak bu görevin **kapsamı içindedir** — ama yalnızca bulunan hatayı düzeltmek
     amacıyla; alakasız refactor/"daha temiz kod" yasağı (`01-workflow-rules.md` Bölüm 0.2/0.7) yine geçerlidir.
- `05-payment.md`'deki 🔒 müşteri talimatları (sağlayıcı, para birimi, komisyon/limit değerleri, confirmation
  eşiği vb.) bu görev kapsamında **değiştirilemez**. Bir "düzeltme" hiçbir zaman `05-payment.md`'nin 🔒 bir
  kararını değiştirerek yapılmaz — sorun gerçekten kodda ise (state geçişi eksik, webhook imza kontrolü hatalı,
  yanlış tutar hesaplama vb.) düzeltme oradan yapılır.

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ)**
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR)** — netleştirilmemiş noktalarda makul varsayımla
  ilerle, gerekçelendir, **asla soru sorup bekleme.**
- ❓ Müşteriden ileride doğrulanması gereken nokta.

---

## 0. Ön koşul kontrolü ve sandbox seçimi 🛠️

Göreve başlamadan önce şunlar doğrulanır (varsayılmaz):

1. `PaymentConfig.UseFakeProviderInDevelopment` gerçekten `false` mu, yoksa görev `FakePaymentProvider` üzerinden
   ilerler ve "gerçek doğrulama" sayılmaz.
2. Bir sandbox instance'ı (regtest veya testnet.demo) o an ayakta ve bağlanabilir mi.
3. Store'da cüzdan bağlı mı, API key'in gerekli izinleri var mı, webhook tanımlı mı.

🛠️ **Sandbox önceliği — bu görevin `14-payment-sandbox.md`'den farkı burada:**

- **Birincil tercih: self-hosted regtest** (`btcpayserver-docker`, `--network=regtest`). Regtest hiçbir kişisel
  hesap açılışı, faucet'e bağımlılık veya dış uptime riski gerektirmez; "fake payment" / "fake mine block"
  butonlarıyla tamamen izole ve deterministik test sağlar. Bu görev **öncelikle** bu yolu dener.
- Eğer projede zaten çalışır durumda bir regtest instance'ı **yoksa**, agent bunu kendi başına ayağa kaldırır
  (Docker Compose ile) — kullanıcıdan bir BTCPay hesabı açmasını **istemez**, bir URL/kayıt bilgisi
  **beklemez**. Önceki bir çalıştırmada kurulan sandbox silinmiş/kaybolmuşsa, bu adım otomasyon üzerinden
  **yeniden** kurulur.
- `testnet.demo.btcpayserver.org` yalnızca regtest'in Docker/VPS kaynağı nedeniyle bu ortamda gerçekten
  kurulamadığı durumda ikincil bir alternatif olarak değerlendirilir — bu durumda dahi kayıt işlemi agent
  tarafından otomatik yapılır, kullanıcıdan manuel hesap açması istenmez.

4. Webhook (`api/webhooks/btcpay`) local test için tünelleniyor mu (regtest'te de dışarıdan `localhost`'a
   erişilemiyorsa ngrok/Cloudflare Tunnel gerekir) ve BTCPay panelindeki/config'teki webhook URL'i güncel mi.

Bu ön koşullardan biri (sandbox otomasyonu dahil) tamamlanamıyorsa, bu görev "başarısız" değil **BLOCKED**
(bkz. Bölüm 5) olarak sınıflandırılır — rapora net biçimde yazılır, tahmini/varsayımsal bir "PASS" verilmez.

---

## 1. Yön A — Müşteri ödeme yapabiliyor mu (top-up / para yatırma) 🛠️

Gerçek bir akış üzerinden, kod okuyarak "muhtemelen çalışır" denmez, fiilen çalıştırılıp doğrulanır:

1. `/cuzdan` (veya güncel karşılığı) sayfasından bir top-up başlat → sandbox'ta gerçek bir invoice oluşuyor mu?
   (BTCPay dashboard'unda invoice ID görünmeli.)
2. Frontend'e dönen ödeme adresi/QR/checkout linki doğru mu, kullanıcı deneyimi kırık değil mi (yükleniyor
   durumunda takılma, hatalı state gösterimi vb.)?
3. Regtest'in "fake payment" / "fake mine block" özelliğiyle (veya testnet ise faucet ile) ödeme gönder →
   webhook gerçekten `PaymentWebhooksController`'a ulaşıyor mu, `WebhookSignatureValidator` imzayı geçerli
   kabul ediyor mu?
4. **Duplicate webhook senaryosu:** Aynı webhook isteği kasıtlı olarak (ör. aynı payload'ı elle iki kez
   göndererek) tekrar tetiklenirse `Wallet.BalanceUsd` iki kez artıyor mu? Artıyorsa bu bir hata,
   `06-coding-standards.md`'deki idempotency kuralının ihlalidir, düzeltilir ve senaryo tekrar koşulur.
5. BTCPay'in gerçek settlement davranışına göre `InvoiceSettled` event'i geldiğinde `PaymentInvoice.Status`
   doğru enum değerine geçiyor mu (string karşılaştırma değil — bkz. `06-coding-standards.md` "Enum ve State
   Yönetimi"), `Wallet.BalanceUsd` gerçekten ve doğru miktarda artıyor mu (before/after değeri kaydedilir)?
   ⚠️ **Önemli — geriye dönüş yok:** `RequiredConfirmations` bir iş kuralı olarak `05-payment.md`'de kalır, ama
   gerçek BTCPay webhook payload'ında bir `confirmations` alanı **gelmez** — bu, önceki gerçek bir BTCPay E2E
   testinde doğrulanmış bir bulgudur ve kod bu nedenle settlement sinyali olarak `InvoiceSettled` event'ini
   kullanacak şekilde tasarlanmıştır. Bu görev **kesinlikle** webhook payload'ında var olmayan bir
   `confirmations` alanını okuyup ona göre eşik kontrolü yapan bir tasarıma geri dönmez/döndürmez — mevcut
   `InvoiceSettled`-tabanlı davranış korunur; yalnızca bu davranışın (settlement öncesi state ile
   `InvoiceSettled` sonrası state farkının) gerçekten tutarlı çalıştığı doğrulanır.
6. Süresi dolan (expired) veya settle olmadan iptal edilen (invalid/expired event) bir invoice doğru state'e
   düşüyor mu, kullanıcıya doğru şekilde yansıyor mu (SignalR üzerinden)? `RequiredConfirmations` iş kuralının
   gerçek BTCPay webhook modeliyle (yani `InvoiceSettled`/`InvoiceInvalid`/`InvoiceExpired` event tipleri
   üzerinden, olmayan bir `confirmations` alanı üzerinden değil) nasıl uygulandığı burada netleştirilir.

**Bulgu:** Bu adımlardan herhangi biri çalışmıyorsa, kök neden `PaymentService`/`BtcPayGreenfieldProvider`/
`PaymentWebhooksController`/`WebhookSignatureValidator`/`WalletService`'ten hangisindeyse orada düzeltilir,
düzeltme sonrası 1–6 baştan tekrar çalıştırılır (regresyon kontrolü). Bu düzeltme **hiçbir koşulda** settlement
sinyalini `InvoiceSettled`'den olmayan bir `confirmations` alanına geri taşımaz.

7. **Ödeme tutarı doğrulaması:** Gerçek BTCPay invoice'ı için beklenen tutar (kurdan hesaplanan `AmountLtc`)
   ile gerçek settlement tutarının WinToWar tarafından nasıl hesaplandığı doğrulanır. Özellikle mevcut
   `PaidAmountLtc` (veya benzeri) alanının gerçek BTCPay webhook payload'ında fiilen bulunup bulunmadığı kontrol
   edilir — önceki gerçek E2E'de bu alanın payload'da gelmediği bulunmuştu; kod hâlâ bu alanın var olduğunu
   varsayıyorsa bu bir bug'dır, kök nedeni düzeltilir. Overpayment/refund mantığı varsa (bkz. `05-payment.md`
   `RefundOverpaymentThresholdUsd`/`PaymentToleranceRate`) gerçek BTCPay davranışıyla (invoice'ın gerçekte hangi
   alanları döndürdüğü) uyumlu olup olmadığı doğrulanır. Bu kontrol sırasında `05-payment.md`'deki komisyon/
   limit/tutar/tolerans kuralları **kesinlikle değiştirilmez** — yalnızca bu kuralların gerçek payload'a göre
   doğru uygulanıp uygulanmadığı düzeltilir.

---

## 2. Yön B — Admin/Sistem ödeme yapabiliyor mu (çekim / payout) 🛠️

1. Yeterli bakiyeli bir test hesabından bir çekim (withdrawal) talebi oluştur.
2. Admin panelinden bu talebi onayla → `PayoutService` gerçekten sandbox'a bir on-chain işlem gönderiyor mu?
   (BTCPay dashboard'unda payout ID/tx hash görünmeli.)
3. **Concurrency senaryosu:** Onay öncesi bakiye doğru şekilde "rezerve"/düşülmüş mü — bu, **mümkünse gerçekten
   eşzamanlı iki approval isteğiyle** (iki gerçek paralel HTTP request/thread, sırayla gönderilen iki istek
   değil) test edilir; sadece kodu okuyup "thread-safe görünüyor" diye varsayılmaz. Testin finansal durumu
   bozmaması için önceden yeterli test bakiyesi regtest üzerinde hazırlanır. Aynı bakiyeyle iki çekim talebi
   gerçekten eşzamanlı onaylanırsa negatif bakiyeye düşüyor mu? Düşüyorsa bu, `06-coding-standards.md`'nin
   "Thread Safety/Concurrency" kuralının ihlalidir, guard/lock eklenerek düzeltilir ve senaryo tekrar koşulur.
4. Payout gerçekten on-chain'e düşünce (BTCPay tarafında confirm olunca) çekim kaydının durumu doğru enum'a
   geçiyor mu?
5. **Failed payout senaryosu:** Payout BTCPay tarafında başarısız olursa sistem bunu doğru şekilde işaretliyor
   mu, kullanıcı bakiyesi hatalı şekilde düşük kalmıyor mu (geri alınması gerekiyorsa geri alınıyor mu)? Bu
   senaryo **kontrollü** şekilde üretilir — gerçek regtest wallet/fonlarını gereksiz yere riske atacak veya
   sandbox'ın genel durumunu bozacak rastgele/keyfi işlemler (ör. rastgele geçersiz adrese gerçek gönderim
   denemesi) yapılmaz; BTCPay'in kendi test araçlarıyla (varsa) veya kontrollü bir hata enjeksiyonuyla
   üretilir. Başarısız bir payout güvenli şekilde üretilemiyorsa, bu adım gerçek E2E ile değil mevcut kod/
   testler üzerinden **failure-path analizi** olarak yapılır ve raporda "gerçek E2E ile doğrulanamadı, kod
   analiziyle değerlendirildi" şeklinde açıkça belirtilir — bu durum PASS'i engeller, en fazla PARTIAL'a
   götürür, ama BLOCKED değildir (diğer akışlar hâlâ gerçek E2E ile doğrulanabiliyorsa).
6. **Rejected withdrawal senaryosu:** Admin bir çekim talebini reddederse (`RefundService` veya ilgili red
   akışı) bakiye doğru şekilde geri ekleniyor mu, aynı talep ikinci kez reddedilirse tekrar bakiye eklenmiyor
   mu (idempotency)?
7. Yetersiz bakiye/limit aşımı gibi beklenen hata durumları exception değil sonuç tipi/hata koduyla mı
   dönüyor (`06-coding-standards.md` "Exception ve Guard")?

**Bulgu:** Bu adımlardan herhangi biri çalışmıyorsa (ör. payout hiç tetiklenmiyor, admin onayı state'i
değiştirmiyor, bakiye kontrolü eksik), kök neden `PayoutService`/`RefundService`/`WalletService`/
`BtcPayGreenfieldProvider`'dan hangisindeyse orada düzeltilir, 1–7 baştan tekrar doğrulanır.

---

## 3. Security / Secret kontrolü 🔒

E2E test sırasında ve sonrasında aşağıdakiler kontrol edilir — bulunursa **hiçbiri raporda açık metin olarak
yazılmaz**, yalnızca "bulundu / bulunmadı ve temizlendi" şeklinde belirtilir:

- API key Git geçmişine (herhangi bir commit'e) girmiş mi?
- Webhook secret Git'e girmiş mi?
- Herhangi bir parola/connection string Git'e girmiş mi?
- Docker/compose config dosyalarında production secret'ı var mı (sandbox config'i ile production config'i
  karışmış mı)?
- Kodun/config'in yanlışlıkla production/mainnet endpoint'ine işaret ettiği bir durum var mı (`05-payment.md`
  🔒 kararlarının aksine, sandbox testi production'a sızmamalı)?
- Yerel `user-secrets` veya `.local.json` dosyası testten sonra temizlenmesi gerekiyorsa temizlendi mi,
  `.gitignore` kapsamında olduğu `git check-ignore` ile gerçekten doğrulandı mı?

Bu bölüm herhangi bir sorun bulmasa da rapora "Security / secret kontrolü" başlığı altında **yapıldığına dair**
kısa bir sonuç yazılır — atlanmaz.

---

## 4. Genel regresyon kontrolü 🛠️

Yön A ve Yön B'de yapılan düzeltmelerden sonra:

- `FakePaymentProvider` ile normal `Development` akışı (`UseFakeProviderInDevelopment=true` iken) hâlâ eskisi
  gibi çalışıyor mu — regresyon yok mu?
- `dotnet build` / `npm run build` geçiyor mu?
- Mevcut ödeme testleri (`api.Tests/` altında varsa) hâlâ geçiyor mu; bulunan hatalar için (idempotency, guard,
  state geçişi gibi) yeni bir test eklenmeden görev bitmiş sayılmaz.

---

## 5. Sonuç sınıflandırması — üç sonuçtan biri kullanılır

- **PASS** — iki yön de (müşteri → sistem, sistem/admin → müşteri) gerçek E2E ile, ilgili kenar senaryolarıyla
  (duplicate webhook, failed payout, rejected withdrawal, concurrency) birlikte başarıyla doğrulandı.
- **PARTIAL** — bir yön çalışıyor, diğer yön veya belirli bir kenar senaryosu (ör. concurrency guard) hâlâ
  çalışmıyor/düzeltilemedi.
- **BLOCKED** — sandbox/altyapı nedeniyle (regtest kurulamadı, testnet erişilemez, tünelleme yapılamadı vb.)
  gerçek E2E hiç yapılamadı.

**"Muhtemelen çalışıyor" hiçbir koşulda kabul edilmez** — PASS yalnızca yukarıdaki kanıtlar (Bölüm 6) fiilen
elde edildiğinde verilir.

---

## 6. Definition of Done — bu görev ancak hepsi ✔ olunca biter

- [ ] Ön koşullar ve sandbox seçimi (Bölüm 0) doğrulandı/kuruldu; kullanıcıdan kişisel BTCPay hesabı açması
      istenmedi.
- [ ] Yön A (müşteri ödeme alma) adımlarının **her biri**, duplicate webhook senaryosu dahil, gerçekten
      çalıştırılıp kanıtlarıyla rapora yazıldı.
- [ ] Yön B (admin/sistem ödeme yapma) adımlarının **her biri**, concurrency/failed payout/rejected withdrawal
      senaryoları dahil, gerçekten çalıştırılıp kanıtlarıyla rapora yazıldı.
- [ ] Bulunan her hata için: kök neden, hangi dosyada düzeltildiği, düzeltme sonrası yeniden doğrulama sonucu
      rapora yazıldı.
- [ ] Herhangi bir düzeltme `05-payment.md`'nin 🔒 bir kararını değiştirmedi.
- [ ] Security/secret kontrolü (Bölüm 3) yapıldı ve sonucu rapora yazıldı (secret değerlerinin kendisi
      **yazılmadan**).
- [ ] Regresyon kontrolü (Bölüm 4) tamamlandı — `FakePaymentProvider` akışı, build, mevcut testler hâlâ geçiyor.
- [ ] `git diff` yalnızca gerçekten hatalı bulunup düzeltilen dosyaları içeriyor — alakasız refactor/rename yok.
- [ ] Sonuç PASS / PARTIAL / BLOCKED olarak açıkça sınıflandırıldı, gerekçesiyle.
- [ ] Görev sonu raporu Bölüm 7'deki başlık sırasına uygun sunuldu.

---

## 7. Final rapor formatı

Rapor **mutlaka** şu başlıkları, bu sırayla içerir:

1. **Ön koşullar** — sandbox nasıl sağlandı (regtest kuruldu mu / zaten var mıydı / testnet mi kullanıldı).
2. **Müşteri → Sistem sonucu** — invoice ID, `PaymentInvoice` ID, regtest TXID, webhook event (`InvoiceSettled`
   dahil), signature sonucu, settled sonucu, balance before/after, ödeme tutarı doğrulaması (`PaidAmountLtc`
   alanının payload'da bulunup bulunmadığı, overpayment/refund hesaplaması varsa sonucu).
3. **Sistem/Admin → Müşteri sonucu** — withdrawal ID, payout ID, TXID, confirmation, withdrawal state, balance
   before/after.
4. **Bulunan bug'lar**
5. **Kök nedenler**
6. **Yapılan minimum düzeltmeler**
7. **Regresyon testleri**
8. **Gerçek E2E kanıtları** (duplicate webhook, failed payout, rejected withdrawal, concurrency sonuçları dahil)
9. **Security / secret kontrolü**
10. **Değişen dosyalar**
11. **Değişmeyen kritik dosyalar** (ör. `05-payment.md`'nin 🔒 kararlarının dokunulmadığı doğrulanan dosyalar)
12. **Kalan problemler** (varsa)
13. **PASS / PARTIAL / BLOCKED**

Her finansal hareket için gerçek ID/TXID ve before/after değerleri yazılır — **gerçek E2E başarılı olmadan
PASS raporlanmaz.**

---

## 8. Kapsam dışı (bu görev bunları yapmaz)

- Sandbox'ın ilk manuel kurulum kararları (`14-payment-sandbox.md`'nin konusu) — ancak sandbox eksikse bu
  görev, otomasyon üzerinden regtest'i **kendi başına yeniden kurar** (bkz. Bölüm 0), bunun için ayrı bir
  görev beklemez.
- Mainnet/production'a geçiş (`14-payment-sandbox.md` Bölüm 6'nın konusu).
- `05-payment.md`'de tanımlı iş kurallarının (sağlayıcı seçimi, limitler, komisyon oranları vb.) değiştirilmesi
  — yalnızca bu kurallara **uymayan kod** düzeltilir, kuralın kendisi değil.
