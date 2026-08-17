# 16 — Cüzdan Bakiyesi Anlık Senkronizasyonu (Header/Navbar)

> **Dosya adı notu:** Bu dosyayı projenizdeki `docs/` klasörüne `16-wallet-balance-sync.md` olarak kaydedin ve
> `CLAUDE.md`'nin "Her görevde önce oku" tablosuna şu satırı ekleyin:
> _"Cüzdan bakiyesi/anlık state senkronizasyon işi → `docs/16-wallet-balance-sync.md` (+ ilgili olduğu için
> `05-payment.md`'nin üzerine inşa eder, `PaymentService`/`WalletService`/`PayoutService`/`RefundService`
> iş mantığını değiştirmez)."_

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla
  ilerle, kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ❓ Müşteriden ileride doğrulanması gereken nokta.

## 0. Sorun ve kapsam 🔒

Şu an cüzdan bakiyesi (`Wallet.BalanceUsd`) değiştiğinde (top-up confirm, withdrawal onayı, refund, oda giriş
ücreti düşme, maç ödülü ekleme) bu değişiklik yalnızca **o an bakiyeyi fetch eden component'te** görünüyor.
Header (`components/layout/Header.tsx`) ve landing Navbar (`components/landing/Navbar.tsx`) kendi bağımsız
fetch/state'ine sahip; cüzdan sayfasındaki (`app/(site)/cuzdan/page.tsx`) bir değişiklik bunlara **hiçbir
şekilde** yansımıyor, kullanıcı sayfayı manuel yenilemek zorunda kalıyor. Ayrıca bakiye değişikliklerinin önemli
bir kısmı (webhook ile gelen top-up confirm'i gibi) **kullanıcının hiçbir aksiyonu olmadan arka planda**
gerçekleşiyor — bu nedenle yalnızca "component mount olduğunda fetch et" veya "aksiyon sonrası local state
güncelle" modeli yeterli değil, gerçek zamanlı bir push mekanizması gerekiyor.

**Amaç:** Bakiyeyi gösteren **tüm** yerler (Header, Navbar, `/cuzdan` sayfası, ileride eklenebilecek her yer)
tek bir kaynaktan beslenecek ve backend'de bakiye her değiştiğinde bu kaynak **anlık olarak** (sayfa
yenilenmeden) güncellenecek.

**Kapsam dışı:** `PaymentService`, `WalletService`, `PayoutService`, `RefundService` içindeki bakiye
hesaplama/iş mantığı bu görevle **değişmez** — yalnızca bakiye değiştikten **sonra** bunun nasıl
yayınlanacağı/tüketileceği değişir. `PaymentInvoice`/`Refund`/`Payout` state makinesine dokunulmaz.

## 1. Backend — bakiye değişimini yayınlama 🛠️

🛠️ **Karar:** Yeni bir SignalR hub açmak yerine (YAGNI — `02-architecture.md`'nin "gereksiz katman/dosya
üretme" ilkesiyle uyumlu), mevcut `GameHub` altyapısı bağlantı yönetimi için örnek alınır ama **oyun ile ödeme
modülünü karıştırmamak için** (`01-workflow-rules.md` Bölüm 0.13 — modüller arası izolasyon) ayrı, ince bir
`WalletHub : Hub` eklenir (`Hubs/WalletHub.cs`). Gerekçe: `GameHub` maç/bölge state'i taşıyor; bakiye bilgisini
oraya karıştırmak SRP ihlali olurdu, ama sıfırdan ağır bir altyapı da gerekmiyor — tek metotlu, sadece
kullanıcıyı kendi grubuna ekleyen minimal bir hub yeterli.

- `WalletHub.OnConnectedAsync()`: kimliği doğrulanmış kullanıcıyı (mevcut auth middleware/JWT üzerinden)
  `$"wallet:{userId}"` grubuna ekler. Kullanıcı bazlı grup — başka bir kullanıcının bakiyesi asla sızmaz.
- `PaymentEventNotifier.cs` (zaten var, muhtemelen ödeme durum bildirimleri için kullanılıyor — dosyayı önce
  incele, aynı desene uy) genişletilir: yeni bir `NotifyWalletBalanceChangedAsync(userId, balanceUsd)` metodu
  eklenir, bu `Clients.Group($"wallet:{userId}").SendAsync("WalletBalanceUpdated", new { balanceUsd })` çağırır.
- `WalletService.cs` içinde bakiyeyi değiştiren **her** metodun sonunda (top-up confirm, withdrawal
  onay/red+refund, refund, oda giriş ücreti düşme, maç ödülü ekleme — mevcut tüm çağrı noktaları) bu bildirim
  tetiklenir. **Önemli:** bildirim, ilgili transaction/`SaveChangesAsync` **commit olduktan sonra** gönderilir —
  aksi halde rollback olan bir işlem için yanlış bakiye yayınlanmış olur (bkz. `06-coding-standards.md`
  "Thread Safety/Concurrency" ve "Idempotency" bölümleri, aynı disiplin burada da geçerli).
- DTO: mevcut bakiye DTO'su varsa (`Payments/Dtos/PaymentDtos.cs` içinde) o kullanılır, yoksa tek alanlı minimal
  bir `WalletBalanceUpdateDto { string BalanceUsd }` eklenir — `06-coding-standards.md`'deki "parasal alanlar
  string" kuralına uyar.

## 2. Frontend — tek global bakiye kaynağı 🛠️

🛠️ **Karar:** `lib/payments/` altına `WalletProvider.tsx` eklenir (React Context). Root'a en yakın, auth'lu
kullanıcıyı saran layout'a (muhtemelen `app/(site)/layout.tsx` — mevcut auth guard'ın nerede olduğunu kontrol
et, `AuthGuard.tsx` ile aynı seviyede/onun içinde sarılmalı ki giriş yapmamış kullanıcıda gereksiz bağlantı
açılmasın) **tek bir kez** eklenir.

```
lib/payments/WalletProvider.tsx   (yeni)
lib/payments/wallet-signalr-client.ts   (yeni — lib/game/signalr-client.ts'deki bağlantı/reconnect
                                          desenini örnek al, kod kopyalanmaz, aynı pattern uygulanır)
```

- `WalletProvider`: mount olunca REST ile ilk bakiyeyi çeker (mevcut `lib/payments/api.ts` fonksiyonu), sonra
  `WalletHub`'a bağlanır ve `WalletBalanceUpdated` event'ini dinler, gelen değerle state'i günceller.
- Bağlantı koptuğunda otomatik reconnect (mevcut `lib/game/signalr-client.ts`'deki reconnect stratejisiyle
  aynı yaklaşım — kod tekrarını önlemek için ortak bir küçük yardımcı çıkarmak istersen `06-coding-standards.md`
  "Kod Tekrarını Önleme" bölümüne bakarak karar ver, ama bu zorunlu değil, iki modül farklı state taşıdığı için
  ayrı tutulması da kabul edilebilir).
- `Header.tsx` ve `Navbar.tsx` (landing) kendi bakiye fetch/state mantığını **tamamen kaldırır**, `useWallet()`
  hook'undan okur. `app/(site)/cuzdan/page.tsx` da aynı context'i kullanır — kendi lokal state'i sadece
  sayfaya özel UI durumları (yükleniyor/hata) için kalabilir, bakiye değeri context'ten gelir.
- Reconnect sırasında/bağlantı hiç kurulamazsa **stale veri gösterilmemesi** için: bağlantı durumu da context'te
  tutulur (`isConnected`), UI bakiyeyi göstermeye devam eder ama ❓ (isteğe bağlı, bloklamayan) küçük bir
  "güncelleniyor" göstergesi eklenebilir — bu görev bunu zorunlu kılmaz, ekleyip eklememek 🛠️ bir tercih.

## 3. Idempotency / sıra garantisi 🛠️

SignalR mesajları teorik olarak sırasız/tekrar gelebilir (ağ katmanı). `WalletBalanceUpdated` event'i **mutlak
bakiye değerini** taşır (delta değil) — bu yüzden ek bir idempotency kontrolüne gerek yoktur: aynı mesaj iki kez
gelse bile state aynı değere set edilir, yanlış sonuç üretmez. Bu, `06-coding-standards.md`'deki idempotency
gereksinimini "delta değil, son durum" tasarımıyla doğal olarak karşılar — ayrıca bir request-id/sequence
mekanizması eklenmez (YAGNI).

## 4. Doğrulama akışı (görev "bitti" sayılması için) 🛠️

`01-workflow-rules.md` Bölüm 0.8-0.9 gereği aşağıdakiler gerçekten çalıştırılıp doğrulanmadan görev tamamlanmış
sayılmaz:

1. İki farklı sekmede aynı kullanıcıyla oturum aç (veya Header + `/cuzdan` sayfasını aynı anda görecek şekilde).
   `/cuzdan` sayfasında bir top-up'ı `FakePaymentProvider` ile confirm'e düşür (Development ortamı) → Header'daki
   bakiye **sayfa yenilenmeden** güncelleniyor mu?
2. Bir withdrawal talebi oluştur, admin onayından geçir → hem cüzdan sayfası hem Header aynı anda güncelleniyor
   mu?
3. SignalR bağlantısını (dev tools → network → WS) kes/bağlantıyı düşür → reconnect gerçekten oluyor mu, kopma
   sırasında eski bakiye yanlışlıkla "0" veya boş gösterilmiyor mu?
4. Giriş yapmamış bir kullanıcı için `WalletHub`'a bağlanma denemesi **reddediliyor** mu (auth kontrolü çalışıyor
   mu) — başka bir kullanıcının grubuna yanlışlıkla erişim yok mu?

## 5. Definition of Done

- [ ] `Hubs/WalletHub.cs` eklendi — yalnızca kullanıcıyı kendi `wallet:{userId}` grubuna ekliyor, başka iş
      mantığı taşımıyor.
- [ ] `PaymentEventNotifier.cs`'e `NotifyWalletBalanceChangedAsync` eklendi, `WalletService.cs`'deki her
      bakiye-değiştiren metodun sonunda (commit sonrası) çağrılıyor.
- [ ] `lib/payments/WalletProvider.tsx` ve `wallet-signalr-client.ts` eklendi.
- [ ] `Header.tsx`, `Navbar.tsx` (landing), `cuzdan/page.tsx` ortak `useWallet()` kaynağını kullanıyor; eski
      bağımsız fetch/state kodları kaldırıldı.
- [ ] Root layout'a `WalletProvider` yalnızca **bir kez**, auth'lu kullanıcı kapsamında ekleniyor (giriş
      yapmamış kullanıcıda gereksiz bağlantı açılmıyor).
- [ ] Bölüm 4'teki 4 senaryo gerçekten çalıştırılıp doğrulandı, sonuçlar rapora yazıldı.
- [ ] `PaymentService`/`WalletService`/`PayoutService`/`RefundService` içindeki bakiye hesaplama mantığında
      **tek satır bile değişmedi** — yalnızca yayın (notification) çağrısı eklendi.
- [ ] `git diff` yalnızca beklenen dosyaları içeriyor (yeni hub, notifier eklentisi, yeni frontend dosyaları,
      Header/Navbar/cuzdan sayfası, `Program.cs`'e hub route kaydı).
