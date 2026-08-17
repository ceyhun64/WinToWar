# 02 — Bağlam ve Mimari 🔒

> **Doküman önceliği:** Bu dosya genel mimari kurallarını tanımlar. İlgili modül dosyası (`03-game-rules.md`, `05-payment.md` vb.) aynı konuda daha özel/farklı bir kural içeriyorsa, modül dosyası önceliklidir — bu dosya yalnızca modül dosyasının değinmediği alanlarda uygulanır. Genel çakışma önceliği sırası için tek doğruluk kaynağı `CLAUDE.md`'deki "Öncelik Sırası (çakışma durumunda)" bölümüdür, burada tekrarlanmaz.

## Proje Yapısı

Proje mevcut bir monorepo yapısına sahip. Aşağıdaki mimariyi **KESİNLİKLE BOZMA**:

```
. (Root)
├── api/                        ← .NET backend (mevcut yapı korunacak)
│   ├── api.csproj
│   ├── Program.cs
│   └── ...
└── web/                        ← Next.js frontend (mevcut yapı korunacak)
    ├── app/
    ├── components/ui/
    ├── lib/
    └── ...
```

- Backend .NET, frontend Next.js olarak kalacak. Farklı bir framework, dil veya proje yapısı önerme/oluşturma.
- `components/ui/` altındaki shadcn tabanlı sistemi koru, yeni UI bileşenlerini bu sistemin üzerine inşa et.
- Kod ve UI metinleri **Türkçe** olacak (değişken/fonksiyon isimleri İngilizce kalabilir, kullanıcıya görünen metinler Türkçe).

## Yeni Modül Ekleme Kuralı (Genel)

Projeye yeni bir modül (ödeme, matchmaking, ranking, authentication, vb.) eklenirken:

- Modülün dosyaları **mevcut üst-düzey klasörlerin içine**, modül adını taşıyan bir alt klasör olarak eklenir: `api/Models/<Modül>/`, `api/Services/<Modül>/`, `api/Controllers/` (Controller'lar için ayrı alt klasör açılmaz, dosyalar doğrudan `Controllers/` içine eklenir), `web/components/<modül>/`, `web/lib/<modül>/`.
- **Asla** `api/<Modül>/Models/`, `api/<Modül>/Services/` gibi kendi başına bir üst-düzey klasör açılmaz. "Modül ayrı bir katmandır" ifadesi her zaman _sorumluluk/mimari_ ayrımı anlamına gelir, _dizin ağacı_ ayrımı anlamına gelmez.
- Her modül dosyası (ör. `05-payment.md`) kendi Bölüm başlığı altında (genelde "Dosya Yapısı") bu genel kuralın somut halini — hangi dosyanın nereye gideceğini — açıkça listeler. Modül dosyasında somut bir dosya ağacı varsa o esas alınır; yoksa yukarıdaki genel kural uygulanır.

## Katman Bağımlılık Kuralları

Bağımlılık yönü tek yönlüdür, tersine çevrilemez:

```
Controller ──► Service ──► Model
Hub        ──► Service ──► Model
```

- `Model` hiçbir katmana bağımlı olmaz (başka bir katmanı referans almaz, iş mantığı çağırmaz).
- `Service`, hiçbir zaman `Controller` veya `Hub` referansı almaz — bağımlılık her zaman Controller/Hub'dan Service'e doğrudur, tersi olmaz.
- `Controller`'lar birbirini çağırmaz; iki controller'ın aynı mantığa ihtiyacı varsa bu mantık ortak bir `Service`'e taşınır.
- **Circular dependency yasaktır:** Hiçbir namespace/servis/entity çifti birbirini referans alacak şekilde tasarlanmaz (ör. `Player` → `Match` → `Player` gibi bir döngü). Bir ilişki iki yönlü gibi görünüyorsa, sahiplik tek yönde kurulur (ör. `Match`, `Player` listesini tutar; `Player` geriye `Match` referansı tutmaz, ihtiyaç halinde `MatchId` gibi bir id ile referans verir) veya sorgu seviyesinde (id üzerinden lookup) çözülür, doğrudan nesne referansı ile değil.

## Dosya ve Sınıf İsimlendirme

- Yeni bir dosya/sınıf adı, o dosyanın/sınıfın **yaptığı işi açıkça belirtmelidir** (ör. `CombatService`, `PaymentInvoiceRepository` değil `CombatUtils`, `GenericManager`).
- Genel-amaçlı, iş mantığını gizleyen isimler (`Helper`, `Utils`, `Common`, `Shared`) yeni dosya/sınıf/klasör oluştururken **kullanılmaz**.
- **İstisna — `Provider` ve `Manager`:** Mevcut, onaylanmış mimaride `MapProvider.cs` ve `MatchManager.cs` gibi kullanımlar zaten var ve bunlar değiştirilmez. Bunlar dar anlamda kullanılmıştır (`MapProvider` yalnızca harita verisini sağlar, `MatchManager` yalnızca maç yaşam döngüsünü/lobi-eşleştirmeyi yönetir) — genel-amaçlı, birden fazla ilgisiz sorumluluğu bir araya toplayan bir "catch-all" `Provider`/`Manager` (`GameProvider`, `DataManager` gibi) oluşturulmaz. Yeni bir `Provider`/`Manager` adı yalnızca aynı dar, tek-sorumluluklu kalıpla kullanılabilir.

## Backend (.NET — `api/`)

- `<Modül>Config.cs` (ör. `GameConfig.cs`, `PaymentConfig.cs`) — İlgili modülün **tüm** sayısal/sabit değerleri burada, tek yerde. **Kod içinde magic number kullanma.** 🛠️ **İleriye dönük kural (yeni config sınıfları için):** Bundan sonra eklenecek yeni config sınıfları `IOptions<T>` üzerinden okunur, `new <Modül>Config()` ile kod içinde elle oluşturulmaz. Zaten tamamlanmış `GameConfig`/`PaymentConfig`'in mevcut okunma şekli bu kural adına geriye dönük refactor edilmez (bkz. `01-workflow-rules.md` Bölüm 0.2) — yalnızca o modüllere dokunan yeni bir görev geldiğinde, o modülün kendi dosyasında ayrıca ele alınır.
- `Models/` — Domain entity'leri. Her modülün kendi entity'leri kendi alt klasöründe. **Bu projede entity'ler yalnızca veri ve durumu tutar** (ör. bir `Region` sınıfının içinde `Combat()`/`Upgrade()` gibi iş mantığı metotları olmaz) — iş mantığı her zaman `Service` katmanındadır. (Bu, projenin şu anki Service-ağırlıklı mimarisiyle uyumlu bir tercihtir; ileride DDD tarzı bir yaklaşıma geçilmek istenirse bu, ayrı bir mimari karar olarak ele alınır.)
- `Models/Dtos/` (veya `Models/<Modül>/Dtos/`) — SignalR/API üzerinden client'a giden **DTO'lar**. Backend domain modelini doğrudan SignalR/API üzerinden yayınlama; her zaman ayrı bir DTO'ya map'leyip onu gönder. DTO'lar frontend-backend arasında **tek doğruluk kaynağı** olsun; `web/lib/<modül>/types.ts` içindeki TypeScript tipleri bu DTO'larla birebir eşleşecek şekilde tanımlansın. **Mapping tek yerde yapılır** (ör. entity üzerinde bir `ToDto()` metodu veya ayrı bir mapper sınıfı) — aynı entity→DTO dönüşümü `Controller`/`Hub` içinde elle, birden fazla yerde tekrar tekrar yazılmaz.
- `Data/` — Statik/yapılandırma verileri (ör. harita JSON'u) kod içine hardcode edilmez, dosyadan okunur.
- `Services/<AltKlasör>/` — İş mantığı servisleri, modüle göre gruplanır (ör. `Services/GameEngine/`, `Services/Payments/`). **Repository katmanı eklenmez** — `DbContext`/EF Core doğrudan `Service` içinde kullanılır (mevcut modüllerde de bu şekilde, ayrı bir `Repository` soyutlaması yoktur; YAGNI). 🔒 **Veritabanı motoru: PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL` provider'ı) — bkz. `CLAUDE.md` "Genel proje bilgisi". Bu projede geçen her `DbContext`/EF Core/migration referansı PostgreSQL'e karşı çalışır; farklı bir provider (SQL Server, SQLite vb.) eklenmez. 🛠️ **İleriye dönük DI lifetime kuralı:** Yeni bir servis kaydedilirken lifetime şöyle seçilir — stateless/paylaşılan servisler `Singleton`, request/hub-bağlantısı bazlı servisler `Scoped`, kısa ömürlü yardımcılar `Transient`; karar verilemiyorsa `Scoped` tercih edilir. Bu da yalnızca yeni servisler içindir, mevcut DI kayıtları bu kural adına geriye dönük değiştirilmez.
- `Hubs/` — SignalR hub'ları: bağlantı yönetimi, oda/maç yönetimi, aksiyon mesajları ve sunucudan client'a **DTO** broadcast'i. **Event isimlendirmesi:** SignalR event/mesaj isimleri `PascalCase` ve fiil+nesne ya da nesne+geçmiş-zaman-fiil şeklinde, açıklayıcı olur (ör. `GameUpdated`, `ArmyMoved`, `MatchStarted`, `RegionCaptured`) — WinToWar'daki oyuncu aksiyonu `AttackRegion`'dır (`UpgradeNest`/`TrainGeneral` Porsuk Savaşları'na aitti, WinToWar'da **kullanılmaz**, asker üretimi tamamen otomatik ve General kavramı yok — bkz. `03-game-rules.md`), yeni event'ler de aynı `PascalCase` kalıbı izler.
- `Controllers/` — REST uçları. **HTTP status standardı:** başarılı yanıtlar `200`/`201`/`204`, hatalar duruma göre `400` (geçersiz istek), `401`/`403` (yetki), `404` (bulunamadı), `409` (state çakışması), `500` (beklenmeyen hata) ile döner. **Sorumluluk sınırı:** `Controller` yalnızca girdi doğrulama (DTO/model validation) ve `Service` çağrısı yapar; iş kuralı/karar mantığı controller içinde yazılmaz, her zaman `Service` katmanındadır.
- **Sunucu otoriter olmalı:** Her aksiyon sunucuda doğrulanır. Client'tan gelen veriye asla güvenme.
- **Loglama, Kaynak Yönetimi, Thread Safety/Concurrency, Performans, Async disiplini:** 🛠️ **Birleştirme (denetimde bulundu):** Bu beş konu önceden burada, `01-workflow-rules.md` Bölüm 0.8/0.11 ve `06-coding-standards.md`'nin ilgili bölümlerinde neredeyse birebir aynı ifadelerle **üç ayrı yerde** tekrarlanıyordu — bu, biri güncellenip diğer ikisinin unutulması riskini taşıyordu (gerçek bir tutarsızlık henüz oluşmamıştı ama üç kopyanın zamanla kaymaması garanti değildi). Bu kuralların **tek doğruluk kaynağı artık `06-coding-standards.md`**'dir (bkz. o dosyanın "Loglama", "Kaynak Yönetimi", "Thread Safety / Concurrency", "Performans" bölümleri) — burada tekrar edilmez, yalnızca uygulanır. Bu dosyaya özgü tek ek nokta: Async disiplini gereği `Thread.Sleep()` **kesinlikle kullanılmaz**, bekleme gereken yerlerde `CancellationToken` ile birlikte `Task.Delay()` kullanılır (bu, `06-coding-standards.md`'nin genel "her async operasyonda `CancellationToken`" kuralının IO/bekleme özelinde somutlaşmış hâlidir, ayrı bir kural değildir).
- **Transaction:** Bir işlem birden fazla entity'yi/tabloyu aynı anda değiştiriyorsa (ör. hem `Region` hem `Army` güncelleniyor, ya da hem `PaymentInvoice` hem `Match` state'i değişiyor), bu değişiklikler bir transaction içinde yapılır — yarım/tutarsız bir güncelleme durumu (bazı tablolar güncellenmiş, bazıları değil) asla bırakılmaz. (Ödeme modülünde bu zaten uygulanmıştır, bkz. `05-payment.md` Bölüm 8.3; bu, kuralın tüm modüller için genel hâlidir.)
- **JSON Serialization:** API/SignalR üzerinden giden JSON `camelCase` alan adlarıyla serialize edilir; `DateTime` alanları her zaman UTC olarak gönderilir; enum'lar sayısal değer olarak değil, **string** olarak serialize edilir (ör. `"Completed"`, `42` değil) — bu, C# tarafında enum'ların string ile karşılaştırılmaması kuralıyla (bkz. `06-coding-standards.md`) çelişmez, yalnızca telin üzerindeki format okunabilirlik için string'dir.
- Unit/entegrasyon testleri: her modülün kritik servisleri için (xUnit).

## Frontend (Next.js — `web/`)

- `app/<özellik>/` — Sayfa route'ları.
- `components/<modül>/` — Modüle özel React bileşenleri. **Component yapısı:** her component tek bir sorumluluğa sahip olur; render, iş mantığı (hesaplama/karar) ve network çağrısı aynı component içinde karıştırılmaz — iş mantığı/veri çekme bir hook'a (`use<Şey>.ts`) veya `lib/<modül>/` içindeki bir yardımcıya taşınır, component yalnızca render eder. Tek bir component'in aşırı büyümesi (yüzlerce satır, çok sayıda sorumluluk) yerine daha küçük, tek amaçlı alt component'lere bölünür. **Hook standardı:** React hook'ları yalnızca `useXxx` şeklinde isimlendirilir; bir component içinde tekrar kullanılabilir iş mantığı doğrudan component gövdesine yazılmaz, bir hook'a taşınır.
- `lib/<modül>/`:
  - `signalr-client.ts` (paylaşılan, oyun motorunda zaten mevcut) veya modüle özel API client'ı.
  - `types.ts` — Backend DTO'larıyla birebir eşleşen TypeScript tipleri.
  - `store.ts` — İstemci tarafı state yönetimi (hafif bir çözüm; gereksiz bağımlılık ekleme). **Tek kaynak ilkesi:** bir modülün client state'i tek bir store üzerinden yönetilir; aynı veri aynı anda hem `useState`, hem `Context`, hem `store.ts` içinde ayrı ayrı tutulmaz — bir veri parçasının "doğru" değeri her zaman tek bir yerden okunur.

## Gerçek Zamanlılık

- SignalR kullan. Yalnızca projeye kurulamayacağı somut bir engel varsa alternatif değerlendir, yoksa SignalR ile devam et.

## Uç Durumlar ve Hata Yönetimi (Genel İlkeler)

- Geçersiz/yetersiz kaynakla yapılan istekler → sunucu reddeder, client'a anlamlı hata mesajı döner.
- Bağlantı kopması → client yeniden bağlandığında SignalR üzerinden ilgili state resync edilir.
- Negatif değerler (altın, asker, bakiye vb.) asla oluşmaz — her azaltma işleminde sunucu tarafı guard olur.
- Modüle özel uç durumlar ilgili modül dosyasında (ör. `03-game-rules.md`, `05-payment.md`) ayrıca listelenir.

## Maç Denetim Kaydı (Audit Log) 🛠️ (08-eksik-alan.md'den taşındı)

Gerçek para transferi içeren her maçın sonucu, bir ödeme itirazında ("maç gerçekten bu şekilde bitti mi") kanıt olarak gösterilebilmelidir. Bu, ayrı bir "replay/izleme" özelliği (bkz. `03-game-rules.md` Bölüm 11 non-goals — replay UI kapsam dışıdır) ile **karıştırılmamalıdır**; burada istenen yalnızca destek/itiraz amaçlı ham bir kayıttır, kullanıcıya gösterilecek bir oynatıcı değildir.

- 🛠️ Her maç için `MatchEventLog` tablosu (`Models/MatchEventLog.cs`, `Services/MatchManager.cs` içinden yazılır): `MatchId`, `SequenceNo`, `EventType` (enum: `RegionAttacked`, `RegionCaptured`, `PlayerEliminated`, `MatchStarted`, `MatchEnded` vb.), `PayloadJson` (o event'e özgü minimal veri — saldıran/hedef bölge, asker sayısı, zaman damgası), `OccurredAt`.
- Kayıt, sıcak yol performansını etkilememesi için senkron değil **fire-and-forget** (arka planda toplu yazım/buffer) yapılır — bkz. `06-coding-standards.md` Performans bölümü ("sıcak yollarda LINQ/allocation minimumda").
- Sadece ödeme itirazı olan maçlar için `/admin/maclar` üzerinden sorgulanır; genel kullanıcıya (`/gecmis` sayfası) bu ham kayıt **gösterilmez** (bkz. `07-pages.md`).
- Saklama süresi ❓ müşteriden netleştirilmeli (ör. 90 gün) — 🛠️ varsayılan `MatchEventLogRetentionDays = 90`.

## Ölçeklenebilirlik ve Sunucu Kapasitesi ❓🛠️ (08-eksik-alan.md'den taşındı)

- ❓ Eşzamanlı desteklenecek maç/kullanıcı sayısı müşteri tarafından verilmedi. 🛠️ **Varsayım:** Lansmanda **tek instance** yeterli kabul edilir (SignalR + EF Core/PostgreSQL, `MatchManager` in-memory state tutar) — bu, projenin "sade, basit" talimatıyla tutarlı bir başlangıç noktasıdır, erken optimizasyon yapılmaz (YAGNI).
- 🛠️ İleride yatay ölçekleme gerekirse (birden fazla instance), SignalR için Redis backplane veya Azure SignalR Service gibi bir çözüm ve `MatchManager`'ın in-memory state'inin paylaşılan bir store'a taşınması gerekir — bu, **şimdi kodlanmaz**, yalnızca ileride bir mimari sınırlama olarak not düşülür; tek-instance varsayımı `Program.cs`'de veya bir yorumda açıkça belirtilir ki gelecekte bu sınır bilinçli aşılsın.

## Genel Dosya Yapısı Referansı (Oyun Motoru — WinToWar'a göre güncellendi) 🛠️

> ⚠️ **Not:** Aşağıdaki yapı önceki "Porsuk Savaşları" implementasyonuna aitti (`Nest.cs`, `General.cs`, `UpgradeService.cs`). Müşteri nihai kararı WinToWar yönünde olduğundan bu üç dosya/mekanik **kaldırılır**; yerine oda (`Room`) modeli ve WinToWar'ın General'siz hareket/otomatik üretim mantığı gelir (bkz. `03-game-rules.md`). `Player`, `Match`, `Region`, `Army`, `MapProvider`, `CombatService`, `MovementService`, `MatchesController`, `map.json` yapısı geçerliliğini korur — yalnızca içerikleri (ör. `Region`'da `Level`/`UpgradeState` alanları, `Army`'de `GeneralId` şartı) WinToWar'a göre sadeleştirilir.

```
api/
├── GameConfig.cs
├── Hubs/
│   └── GameHub.cs
├── Models/
│   ├── Player.cs
│   ├── Match.cs
│   ├── Region.cs
│   ├── Army.cs
│   ├── Rooms/
│   │   └── Room.cs              // VIP/Standart oda ayarları: GreyRegionDefenseCount, FogOfWar, EntryFeeUsd, MaxPlayers, RoomPasswordHash (nullable, dolu ise oda şifreli — bkz. `03-game-rules.md` Bölüm 2.2), InviteToken (nullable, opsiyonel kısayol linki, parolanın YERİNE geçmez) — ⚠️ v3'te düzeltildi: önceki iki turda sırasıyla "PasswordHash" → "yalnızca InviteToken (parola yok)" şeklinde değişmişti, ikincisi müşterinin "şifreli" kelimesini yanlış yorumluyordu, gerçek parola mekanizması geri getirildi
│   ├── MatchEventLog.cs         // Denetim/itiraz kaydı — bkz. yukarıdaki "Maç Denetim Kaydı" bölümü
│   └── Dtos/
├── Services/
│   ├── EconomyTickService.cs
│   ├── MatchManager.cs
│   ├── MapProvider.cs
│   ├── Rooms/
│   │   └── RoomService.cs       // VIP oda oluşturma/listeleme/şifreli giriş doğrulama, JoinIpAddress kaydı (bkz. `03-game-rules.md` Bölüm 11 multi-accounting notu)
│   ├── GameEngine/
│   │   ├── CombatService.cs
│   │   └── MovementService.cs
│   └── Matchmaking/
│       └── BotMatchService.cs   // Bot eşleştirme (lobi doldurma) + AI saldırı kararı (bkz. `03-game-rules.md` Bölüm 7 DÜZELTME — "bot yok" kararı geri alındı)
├── Controllers/
│   └── MatchesController.cs
└── Data/
    └── map.json

web/
├── app/game/[matchId]/page.tsx
├── components/game/
│   ├── GameMap.tsx
│   ├── RegionNode.tsx
│   ├── Hud.tsx
│   └── ActionPanel.tsx
├── components/rooms/
│   └── VipRoomForm.tsx          // VIP oda kurma formu (gri bölge savunması, Fog of War, giriş ücreti, oyuncu sayısı, şifre)
└── lib/game/
    ├── signalr-client.ts
    ├── types.ts
    └── store.ts
```

Yeni modüller (ödeme vb.) bu ağacın **içine**, aynı desenle (`Models/<Modül>/`, `Services/<Modül>/`, `components/<modül>/`, `lib/<modül>/`) eklenir — bkz. yukarıdaki "Yeni Modül Ekleme Kuralı".
