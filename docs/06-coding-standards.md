# 06 — Kod Yazım Standartları

Bu dosya, `02-architecture.md`'deki "hangi dosya nereye gider" sorusundan ayrı olarak, **kodun kendi içindeki** yazım disiplinini tanımlar. Tüm modüller için geçerlidir.

## Loglama

- Beklenmeyen durumlarda `ILogger<T>` kullan; `Console.WriteLine` **asla** kullanma.
- Debug amaçlı geçici log satırları bırakma.
- Bir modülün kendi loglama scope kuralları varsa (ör. ödeme modülünde `InvoiceId`/`MatchId`/`PlayerId` scope'u) o modülün kendi dosyasındaki kural esas alınır, bu genel kuralın üzerine eklenir.

## Exception ve Guard

- Beklenen hata durumları (yetersiz bakiye, geçersiz state geçişi vb.) exception fırlatmak yerine sonuç tipi/hata kodu ile döndürülür; exception yalnızca gerçekten istisnai/beklenmeyen durumlar için kullanılır.
- Negatif/geçersiz değer üretebilecek her azaltma/çıkarma işleminde sunucu tarafı guard olur.
- Guard clause deseni tercih edilir (erken `return`/erken hata), iç içe `if` yığınları yerine.

## Thread Safety / Concurrency

- Aynı state'e (bir `Match`, bir `PaymentInvoice` vb.) eşzamanlı erişim söz konusu olduğunda: thread-safe koleksiyonlar (`ConcurrentDictionary` vb.) veya `lock`/transaction ile kritik bölge koruması kullanılır.
- Bir state'e iki farklı akışın (arka plan tick'i + kullanıcı aksiyonu, veya iki eşzamanlı webhook) aynı anda korumasız yazması **asla** olmaz.

## Idempotency

- Bir istemci isteğinin (SignalR mesajı, webhook, API çağrısı) ağ katmanı nedeniyle **iki kez** sunucuya ulaşması, aynı sonucu iki kez üretmemelidir. Bu yalnızca ödeme modülüne özgü bir kural değildir (bkz. `05-payment.md` — orada webhook'lar için idempotency zaten zorunlu tutulmuştur); tekrar eden bir isteğin state'i ikinci kez değiştirebileceği her yerde (ör. SignalR'ın aynı aksiyon paketini iki kez göndermesi, kullanıcının çift tıklamasıyla aynı isteğin iki kez gitmesi) geçerlidir.
- Pratikte: bir isteğin daha önce işlenip işlenmediği (istek id'si, mevcut state'in zaten hedef duruma ulaşmış olması vb.) kontrol edilmeden state değiştiren bir yazma işlemi yapılmaz.

## Enum ve State Yönetimi

- Durum/state değerleri (`Match.Status`, `PaymentInvoice.Status` vb.) **string literal ile karşılaştırılmaz** (`if (status == "running")` gibi kullanım yasak); tüm durumlar enum olarak tanımlanır ve enum değeriyle karşılaştırılır.
- Yeni bir modülde durum makinesi gerekiyorsa, o modülün durumları da aynı şekilde enum olarak modellenir (bkz. `03-game-rules.md` `Match.Status`, `05-payment.md` `PaymentInvoice.Status`/`Refund.Status` örnekleri).

## Kaynak Yönetimi

- `BackgroundService`'ler düzgün `Dispose`/`StopAsync` desteklemeli.
- Her async operasyonda, özellikle döngülerde, `CancellationToken` kullanılır.
- Memory leak veya asılı kalan task bırakılmaz.

## Performans

- Sıcak yollarda (her saniye/sık çalışan kod — tick döngüleri, webhook işleyicileri) LINQ kullanımını minimumda tut.
- Gereksiz allocation/boxing oluşturma; struct kullanılabilecek yerlerde değerlendir.
- Gerçek zamanlı iletişimde (SignalR) minimum payload gönder — tüm state yerine değişen kısımları (delta) yolla ya da makul throttle uygula.

## Bağımlılık Disiplini

- Yeni bir NuGet/npm paketi eklemeden önce gerçekten zorunlu olduğunu doğrula; eklerken nedenini kısa bir yorum/commit mesajıyla gerekçelendir.
- Tek kullanım noktası olan interface/abstraction üretme (YAGNI) — bkz. `01-workflow-rules.md` Bölüm 0.10.

## DTO ve Mapping

- Backend domain modeli hiçbir zaman doğrudan API/SignalR üzerinden yayınlanmaz; her zaman ayrı bir DTO'ya map'lenir.
- DTO'lar frontend-backend arasında tek doğruluk kaynağıdır; TypeScript tipleri DTO'larla birebir eşleşir.
- Parasal/hassas sayısal alanlar (bir modül bunu gerektiriyorsa) DTO'da açıkça uygun tipte tanımlanır (ör. ödeme modülünde `string` — bkz. `05-payment.md`).

## İsimlendirme

- Değişken/fonksiyon/sınıf isimleri İngilizce; kullanıcıya görünen metinler Türkçe (bkz. `04-style.md`).
- Bir değerin anlamı/yönü isimden belirsizse (ör. bir oran hangi yöne çeviriyor), isim bunu netleştirecek şekilde seçilir — belirsiz kısaltmalardan kaçınılır.

## Magic Number/String Yasağı

- Hiçbir sayısal sabit veya tekrar kullanılan string literal kod içine gömülmez; ilgili modülün config sınıfında (`GameConfig`, `PaymentConfig` vb.) veya sabitler dosyasında (`PaymentErrorCodes.cs`, `PaymentHubEvents.cs` gibi) tek yerden tanımlanır.

## Kod Tekrarını Önleme

- Aynı iş mantığı iki veya daha fazla yerde birebir/neredeyse birebir tekrar ediyorsa, bu mantık tek bir yerde toplanır (metot/servis) ve oradan çağrılır.
- **Ancak** bu, gereksiz abstraction üretmek için bir gerekçe değildir (bkz. `01-workflow-rules.md` Bölüm 0.10, YAGNI) — iki farklı modülde _tesadüfen benzer görünen ama aslında farklı_ iş kuralları paylaşılan bir soyutlamaya zorlanmaz; yalnızca gerçekten aynı mantık, aynı modül içinde tekrar ediyorsa birleştirilir.

## Secrets / Hassas Bilgi Yönetimi

- API key, private key, connection string, webhook signature secret gibi hiçbir gizli bilgi kod içine (kaynak dosyaya, commit'e) hardcode edilmez.
- Bu tür değerler yalnızca ortam değişkenleri (environment variables), `appsettings.json` (gerekirse `appsettings.Development.json`/User Secrets ile yerelde) veya bir secrets yöneticisi üzerinden okunur; `.gitignore` kapsamı dışına çıkmaz.
- Loglarda da bu değerler (tam haliyle) yer almaz — bkz. Loglama bölümü ve ilgili modülün kendi scope/masking kuralları (ör. `05-payment.md`).

## Veritabanı / Migration Disiplini (EF Core kullanan modüller için)

- Bir entity/model değişikliği yapıldığında (yeni alan, tip değişikliği, yeni tablo vb.) karşılığında bir EF Core migration oluşturulur; migration'sız şema değişikliği ile ilerlenmez.
- Migration dosyaları da diğer kod gibi commit'e dahil edilir, elle DB'de manuel şema değişikliği yapılmaz.
- 🛠️ **Test altyapısı istisnası:** CLAUDE.md'deki 🔒 "yalnızca PostgreSQL" kararı çalışma zamanı (`Program.cs`'deki gerçek `UseNpgsql` kaydı) içindir. `api.Tests/` her test çalıştırmasında izole, hızlı, paralel-güvenli bir veritabanı gerektirir (bkz. `TestSupport/PaymentDbContextFactory.cs`) — bu amaçla yalnızca test projesinde SQLite in-memory bağlantısı kullanılır. Bu, üretim/geliştirme ortamındaki gerçek veritabanı seçimini değiştirmez; migration'lar (`Migrations/Payments/`, `Migrations/GameEvents/`) yalnızca PostgreSQL'e karşı üretilir ve uygulanır.
