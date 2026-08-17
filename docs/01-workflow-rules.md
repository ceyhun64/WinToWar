# 01 — Çalışma Davranışı Kuralları ⚙️

Bu dosya, Claude Code'un **tüm görevlerde** (oyun motoru, ödeme sistemi, ileride eklenecek her modül) uyacağı süreç kurallarını içerir. Modül-spesifik dosyalar (`03-game-rules.md`, `05-payment.md` vb.) bu dosyayı referans alır; burada geçen kurallar tüm modüller için geçerlidir, aksi modül dosyasında açıkça belirtilmedikçe.

> **Kural çakışması önceliği:** İki farklı dosya aynı konuda farklı şey söylüyorsa hangisinin kazanacağı `CLAUDE.md`'nin "Öncelik Sırası (çakışma durumunda)" bölümünde tanımlıdır (kullanıcının o anki mesajı → bu dosya → ilgili modül dosyası → `02-architecture.md` → `06-coding-standards.md` → `04-style.md`). Bu sıra burada tekrarlanmaz, tek doğruluk kaynağı `CLAUDE.md`'dir.

---

## 0.1 Tek seferde her şeyi yazmaya ÇALIŞMA — Aşamalı ilerle

Tüm sistemi tek commit'te/tek seferde yazmaya çalışma. Context dolabilir, iş yarım kalabilir. İlgili modül dosyasındaki (`03-game-rules.md`, `05-payment.md` vb.) aşama sırasını izle, **her aşamayı bitirip build aldıktan sonra bir sonrakine geç**.

Her aşama sonunda `dotnet build` / `npm run build` çalıştır, geçmeden bir sonraki aşamaya geçme.

## 0.2 Kapsam dışı hiçbir dosyaya dokunma — Mutlak Yasaklar

Aşağıdakiler **kesinlikle yasak**:

- Mevcut dosyaları yeniden organize etme (reorganize/restructure).
- Rename yapma.
- Dosya/klasör taşıma (move).
- "Daha temiz mimari" gerekçesiyle mevcut kodu refactor etme.
- Dosya silme.
- Mevcut kodun stilini/formatını değiştirme.
- Solution genelinde formatter/linter/otomatik cleanup çalıştırma.
- Global rename veya global refactor.
- Bu görevle ilgisi olmayan hiçbir dosyada değişiklik yapma.
- **Yeni bir modül eklerken, o modülü mevcut üst-düzey klasör yapısının (`Models/`, `Services/`, `Controllers/`, `components/`, `lib/`) DIŞINDA, kendi ayrı üst-düzey klasöründe organize etme.** "Ayrı bir katman" ifadesi sorumluluk/mimari ayrımı anlamına gelir, ayrı bir dizin ağacı anlamına gelmez — yeni modülün dosyaları, ilgili modül dosyasında (ör. `02-architecture.md`, `05-payment.md`) açıkça bir dosya ağacı verilmişse ona, verilmemişse mevcut klasörlerin altına alt klasör (`Models/<ModulAdi>/`, `Services/<ModulAdi>/` gibi) olarak eklenir.

Sadece görev için **gerekli olan yeni dosyaları ekle** ve mevcut dosyalara **sadece gerekli minimal satırları** (ör. `Program.cs`'e servis/hub kaydı, `appsettings.json`'a config bölümü) ekle. Mevcut dosyanın geri kalanına dokunma.

## 0.3 Build disiplini — Kendi kendine düzelt, onay bekleme

`dotnet build` veya `npm run build` başarısız olursa:

1. Hata mesajını analiz et.
2. Hatayı kendin düzelt.
3. Tekrar build al.
4. Başarılı olana kadar bu döngüyü tekrarla.

Kullanıcıdan onay isteme, "böyle mi düzelteyim?" diye sorma. Build yeşil olana kadar dur, sonra devam et.

## 0.4 TODO / Placeholder / Mock YASAK

Aşağıdakileri **asla** üretme:

- `TODO`, `FIXME` yorumları
- Placeholder implementasyon
- Fake/mock servisler (test dosyaları hariç)
- Boş metot gövdeleri
- `throw new NotImplementedException()`
- Dummy/sabit test verisi üretim koduna karışmış halde
- "Daha sonra yapılacak" notları

Yazdığın her özellik **gerçekten çalışır** durumda olmalı. Bir özelliği o an tam bitiremiyorsan, kapsamı daralt ama bitirdiğin kısmı eksiksiz ve çalışır bırak — yarım/sahte kod bırakma.

**İstisna:** Bir modül dosyası (ör. `05-payment.md`) açıkça bir dış servisin (BTCPay gibi) test ortamında erişilemez olabileceğini ve bu durumda gerekçeli bir sahte implementasyonla ilerlenmesini öngörüyorsa, bu istisna geçerlidir — ama yalnızca o modül dosyasının açıkça izin verdiği kapsamda, ve rapora açıkça yazılarak.

## 0.5 Soru sormayı tamamen bırak

Belirsizlik varsa: karar ver → gerekçelendir (kısa bir yorum/not olarak) → devam et.

Şunları **asla** yazma:

- "Şunu nasıl yapayım?"
- "Şu seçeneklerden hangisini tercih edersiniz?"
- "Bu uygun mu?"

Kullanıcıdan onay bekleyen hiçbir soru sorma; görev tamamlanana kadar ilerle.

**Önemli sınır:** Yukarıdaki "karar ver, gerekçelendir, devam et" kuralı yalnızca **müşterinin hiç belirtmediği** noktalar için geçerlidir. Müşteri bir konuda **açıkça teknik bir karar vermişse** (ör. "ödeme PayTR ile olacak", "LTC kullanılacak", "12 oyuncu"), bu karar hiçbir gerekçeyle (daha kolay/daha hızır/daha yaygın vb.) **değiştirilemez, başka bir teknolojiyle/değerle ikame edilemez** — bu tür bir değişiklik "varsayım" değil, müşteri talimatının ihlalidir. Varsayım mekanizması yalnızca müşterinin **hiç değinmediği** boşluklar içindir; müşterinin verdiği bir kararın "daha iyi bir alternatifi" olduğu düşünülse bile sessizce değiştirilmez.

## 0.6 Commit disiplini (varsa git kullanılıyorsa)

- Her büyük modül tamamlandığında `git diff` ile değişiklikleri incele.
- Kapsam dışı/istenmeyen bir değişiklik varsa geri al.
- Tek dev bir commit yerine, mantıklı, küçük, aşama bazlı commitler oluştur (ör. "feat: domain models eklendi", "feat: payment service eklendi").

## 0.7 Öncelik sırası: Çalışan kod > Doğru mekanik > Performans > Temiz kod

Karar anlarında bu sırayı uygula:

1. **Çalışan kod** — önce çalışsın.
2. **Doğru iş mantığı** — ilgili modül dosyasındaki kurallarla birebir eşleşsin. **"Çalışıyor ama kurala uymuyor" bir kod, çalışmıyor sayılır** — derlenip build'i geçse ve hatasız çalışsa bile, ilgili modülün 🔒/🛠️ kurallarından biriyle çelişiyorsa görev tamamlanmış sayılmaz; bu durum "performans" veya "temiz kod" gerekçesiyle göz ardı edilemez.
3. **Performans** — ilgili modül dosyasındaki performans beklentileri.
4. **Temiz kod / "daha iyi" görünüm** — en düşük öncelik.

Zaten çalışan bir sistemi sırf daha "temiz" görünsün diye yeniden yazma. "Bunu daha iyi yazabilirim" dürtüsüyle vakit harcama.

## 0.8 Build başarılı ≠ görev tamam — Runtime'ı da doğrula

Build'in yeşil olması yeterli değildir. Her aşama sonunda mümkünse uygulamayı gerçekten çalıştır (`dotnet run` / `npm run dev`) ve şunları kontrol et:

- `NullReferenceException`, `InvalidOperationException` gibi runtime hataları
- SignalR bağlantı hataları
- Dependency Injection (DI) kayıt hataları (servis bulunamadı vb.)

Bu tür hatalar çözülmeden görevi/ilgili aşamayı bitmiş sayma.

## 0.9 Her servisi gerçek bir akışla doğrula

Bir servis/metot yazdıktan sonra sadece derlenmesi yeterli değildir. O servisi kullanan **en az bir uçtan uca akışı** gerçekten çalıştırıp doğrula. Hiçbir yerden çağrılmayan "ölü kod" bırakma.

## 0.10 Gereksiz abstraction üretme (YAGNI)

- Tek kullanım noktası olan interface üretme (`IXService`, `IXServiceFactory`, `AbstractXService`, `BaseXService`, `XServiceProvider` gibi gereksiz katmanlar YOK).
- "İleride lazım olur" diye kullanılmayan soyutlama/katman ekleme.
- YAGNI prensibini uygula: sadece şu an ihtiyaç duyulanı yaz.

## 0.11 Loglama ve bağımlılık disiplini

🛠️ **Birleştirme (denetimde bulundu):** Loglama kuralının kendisi (`ILogger` kullan, `Console.WriteLine` yasak, debug log bırakma) önceden burada, `02-architecture.md` ve `06-coding-standards.md`'de üç ayrı yerde tekrarlanıyordu; tek doğruluk kaynağı artık `06-coding-standards.md`'nin "Loglama" bölümüdür, burada tekrarlanmaz — bu bölümün süreç kuralı olarak eklediği tek şey, bağımlılık (paket) disiplinidir:

- Yeni bir NuGet veya npm paketi ekleme; sadece gerçekten zorunluysa ekle ve neden gerekli olduğunu kısa bir yorum/commit mesajıyla gerekçelendir.

## 0.12 Context yetersiz kalırsa öncelik sırası

Uzun görev sırasında context dolmaya yaklaşırsa, kaliteyi düşürmek yerine ilgili modül dosyasındaki önceliğe göre kapsam daralt. Genel kural: çekirdek iş mantığı/motor asla eksik bırakılmaz, görsellik/ince detay en sona feda edilir.

## 0.13 Modüller arası izolasyon

Proje birden fazla bağımsız modülden oluşabilir (oyun motoru, ödeme sistemi, ileride eklenecekler). Bir modül üzerinde çalışırken:

- Başka bir modülün dosyalarına, o modülün görevi açıkça gerektirmedikçe dokunulmaz.
- Bir modülün domain modeli/state'i başka bir modülün sorumluluğunu taşımaz (SRP) — ör. ödeme modülü oyunun maç durumunu, oyun motoru ödeme durumunu kendi içinde tutmaz; ihtiyaç halinde yalnızca sorgu seviyesinde bir araya getirilir.
- Yeni bir modül eklenirken önce bu dosyayı (`01-workflow-rules.md`) ve `02-architecture.md`'yi oku, ardından ilgili modül dosyasını (ör. `05-payment.md`) oku.

## 0.14 Görev sonu raporu

Her görev (veya görevin bir aşaması bittiğinde, özellikle çok sayıda dosya değiştiyse) kısa bir özet rapor sunulur. Rapor şunları içerir:

- **Değişen/eklenen dosyalar** — kısa liste (yol + tek satırlık açıklama).
- **Neden değişti** — her değişikliğin hangi görev/kural gerekçesiyle yapıldığı (uzun anlatım değil, madde madde kısa gerekçe).
- **Build sonucu** — `dotnet build`/`npm run build` çıktısının durumu (geçti/geçmedi, geçmediyse ne düzeltildi).
- **Test sonucu** — çalıştırılan unit/entegrasyon testleri ve sonucu.
- **Varsayım yapılan alanlar** — müşterinin belirtmediği, 🛠️ etiketiyle karar verilmiş noktaların kısa listesi.
- **Müşteriden doğrulanması gereken alanlar** — varsa ❓ etiketli, bloklamayan ama ileride teyit gerektiren noktalar.

Bu rapor, görevin kendisi kadar önemlidir — özellikle çok sayıda dosyanın değiştiği büyük görevlerde, neyin neden değiştiğinin izlenebilir olması için atlanmaz.
