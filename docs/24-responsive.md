# GÖREV: Mobil layout + dokunmatik girdi düzeltmesi

İki ayrı problem var. **A ve B farklı problem sınıflarıdır** — B'yi CSS/responsive düzeltmesiyle çözmeye çalışma, o bir girdi (input event) hatasıdır.

**Problem A — Küçük ekranlarda layout bozuluyor.**
390–430px genişliğindeki telefonlarda görünüm şu anda düzgün. 320–375px aralığında içerik taşıyor, fixed/absolute elemanlar üst üste biniyor.

**Problem B — Telefonda oyun ekranında çekerek asker gönderme çalışmıyor.**
Masaüstünde (fare ile) bölgeden bölgeye sürükleyerek asker gönderme çalışıyor; dokunmatik cihazda çalışmıyor. Bu **fonksiyonel bir hatadır**, öncelikli olarak düzeltilir.

**Bu bir tasarım yenileme görevi değildir.** Amaç, mevcut tasarımı küçük ekranlarda kullanılabilir ve oyunu dokunmatikte oynanabilir hâle getirmektir.

---

## 0. Önce oku (zorunlu)

`CLAUDE.md`'deki "Her görevde önce oku" sırasını uygula: `docs/01-workflow-rules.md`, `docs/02-architecture.md`, `docs/06-coding-standards.md`.

Bu görev UI/layout işi olduğu için ayrıca: `docs/04-style.md`, `docs/10-ui-redesign.md`, `docs/07-pages.md`, `docs/08-page-content.md`, `docs/13-scroll-lock.md`.

`/game/[matchId]` veya `components/game/*` üzerinde çalışacaksan: `docs/23-game-visual-refresh.md` — **Bölüm 10'daki kontrol listesi, ekrana dokunmadan önce okunur.** (`docs/23-game-ui-refresh-v2.md` arşivdir, görev kaynağı olarak kullanılmaz.)

Problem B için ayrıca `docs/03-game-rules.md`'nin dispatch (asker gönderme) bölümü okunur — **kural olarak değil, doğru davranışı bilmek için.** Gönderilen asker oranı, komşuluk kısıtı, dispatch batching gibi kurallar bu görevde **değiştirilmez**; yalnızca girdi katmanı düzeltilir. Fonksiyon/yapı konusunda `04-style.md`, estetik/renk konusunda `23-game-visual-refresh.md` kazanır.

Çakışmada `CLAUDE.md`'deki öncelik sırası geçerlidir.

## 1. Regresyon kuralı — en önemli madde

| Genişlik | Beklenti                           |
| -------- | ---------------------------------- |
| 320px    | Düzelecek                          |
| 360px    | Düzelecek                          |
| 375px    | Düzelecek                          |
| 390px    | **Mevcut görünüm aynen korunacak** |
| 430px    | **Mevcut görünüm aynen korunacak** |
| ≥768px   | **Mevcut görünüm aynen korunacak** |

390–430px şu an çalışan referans tasarımdır. Küçük ekranı düzeltirken bu aralığı bozan hiçbir değişiklik kabul edilmez.

### Değiştirme

`api/` altındaki hiçbir şey · backend · game engine · tick servisi · `GameHub` dispatch sözleşmesi (metod adı, parametreler, SignalR payload'ı) · `PaymentService` / `WalletService` / `PayoutService` / `RefundService` · `WithdrawalRequest` state machine · `GameConfig.cs` oyun değerleri · oyun kuralları (asker oranı, komşuluk, batching) · renk paleti · marka dili · tipografi ölçeği · içerik sırası · ilgisiz refactor · yeni dependency (dokunmatik için gesture kütüphanesi **dahil**) · kütüphane değişimi · dosya taşıma/yeniden adlandırma.

"Yolu geçmişken" başka problem düzeltme.

### Değiştir

Layout · genişlik/yükseklik · spacing · breakpoint davranışı · overflow · fixed/absolute positioning · safe-area · dokunma hedefi boyutları.

## 2. Başarı kriteri

320px genişlikte ve landscape modda, adres çubuğu açıkken de kapalıyken de:

- yatay scroll yok
- içerik kırpılmıyor
- elementler üst üste binmiyor
- metinler container dışına taşmıyor
- modal/drawer ekran dışına çıkmıyor, iç scroll'u çalışıyor
- fixed HUD/alt panel içerikle çakışmıyor
- butonlar erişilebilir, dokunma hedefi ≥44×44px

Ayrıca oyun ekranında, gerçek dokunmatik cihazda:

- bir bölgeden komşusuna parmakla sürükleyerek asker gönderilebiliyor
- sürükleme sırasında sayfa kaymıyor/zoom olmuyor, metin seçimi veya uzun basma menüsü açılmıyor
- sürükleme önizlemesi (ok/hedef vurgusu) fare ile aynı şekilde çalışıyor
- parmak kaynak bölgenin dışına çıktığında sürükleme kopmuyor
- kısa dokunuş (tap) ile sürükleme birbirine karışmıyor
- masaüstünde fare ile mevcut davranış aynen korunuyor

## 3. Problem B — Dokunmatik sürükleme hatası

Bunu CSS ile çözmeye çalışma. Kök nedeni bulana kadar düzeltme yazma. Kontrol edilecek dosyalar: `components/game/GameMap.tsx`, `RegionNode.tsx`, `ArmyLayer.tsx`, `TroopMarker.tsx`, `ActionPanel.tsx`, `Hud.tsx`, `lib/game/arrow.ts`, `lib/game/store.ts`, `lib/game/useArmyAnimation.ts`, `app/globals.css`.

Olası kök nedenler — **hepsini sırayla ele, hangisinin geçerli olduğunu raporda belirt**:

1. **Fare-özel event handler'lar.** `onMouseDown` / `onMouseMove` / `onMouseUp` kullanılıyorsa dokunmatikte hiç tetiklenmez. Çözüm: Pointer Events'e geçiş (`onPointerDown/Move/Up/Cancel`) — tek kod yolu hem fareyi hem dokunmatiği karşılar. `onTouchStart` ekleyerek ikinci bir paralel kod yolu **açma**.
2. **`touch-action` eksikliği.** Sürüklenen yüzeyde `touch-action: none` yoksa tarayıcı hareketi scroll/pan olarak yorumlar ve pointer event akışını `pointercancel` ile keser. Bu, semptomun en yaygın nedenidir. Yalnızca harita/etkileşim katmanına uygula, tüm sayfaya değil.
3. **Pointer capture yok.** Parmak kaynak elemanın hit alanından çıkınca event akışı kesilir. `setPointerCapture(e.pointerId)` ile sürükleme sahiplenilmeli, bitişte `releasePointerCapture`.
4. **Üstteki katmanlar dokunuşu yutuyor.** `ArmyLayer`, ok önizlemesi, HUD, `ActionPanel` gibi overlay'ler haritanın üstünde duruyorsa ve `pointer-events: none` taşımıyorsa dokunuş haritaya hiç ulaşmaz. Küçük ekranda overlay'ler büyüdüğü için bu Problem A ile birleşip **sadece telefonda** görünür hâle gelebilir.
5. **Passive listener.** `addEventListener('touchmove'/'pointermove', ...)` doğrudan kullanılıyorsa varsayılan passive'dir ve `preventDefault()` sessizce çalışmaz. Gerekiyorsa `{ passive: false }`.
6. **SVG koordinat dönüşümü.** `clientX/clientY` → SVG koordinatı dönüşümü `getScreenCTM().inverse()` veya `getBoundingClientRect()` oranıyla yapılmıyorsa, harita küçük ekranda ölçeklendiğinde hedef bölge yanlış hesaplanır: sürükleme "çalışır" ama yanlış bölgeye gider ya da hiçbir hedefe denk gelmez. Ölçek sabit varsayan bir hesap varsa düzelt.
7. **Tap/drag ayrımı ve parmak titremesi.** Dokunmatikte parmak sabit durmaz; eşik yoksa tap yanlışlıkla drag sayılır ya da tersi. ~8–10px hareket eşiği (slop) uygula.
8. **Çoklu dokunuş.** İkinci parmak sürüklemeyi bozmamalı: `e.isPrimary` kontrolü ve aktif `pointerId` takibi.
9. **Metin seçimi / uzun basma menüsü.** Harita üzerinde `user-select: none` ve `-webkit-touch-callout: none` yoksa iOS'ta sürükleme seçim moduna düşer.
10. **`13-scroll-lock.md` etkileşimi.** Scroll-lock uygulaması dokunuş olaylarını engelliyor olabilir. O dosyanın v2 kararlarını **bozmadan** çöz; çelişki varsa raporda belirt.

🛠️ Sürükleme mekaniği korunur; bunun yerine "bölgeye dokun → hedefe dokun" gibi alternatif bir girdi şemasına **kendi başına geçme**. Sürükleme teknik olarak çalıştırılamıyorsa nedenini raporla, ❓ ile işaretle ve mevcut şemayı bozmadan öneri sun.

## 4. Aşama 1 — Sadece denetim (KOD YAZMA)

**Kapsamı grep ile daralt, dosya dosya okuma.** Önce şu kalıpları `web/app/` ve `web/components/` altında ara, sadece eşleşen dosyaları aç:

- **Sabit boyut:** `w-\[.*px\]`, `h-\[.*px\]`, `min-w-\[`, `min-w-(48|56|64|72|80|96)`, `max-w-`
- **Grid/flex:** `grid-cols-[2-9]` (yanında `sm:`/`md:` yoksa), `flex-nowrap`, `flex-row` (uzun içerikli satırlarda), `min-w-0` **eksikliği**
- **Overflow:** `whitespace-nowrap`, `overflow-x`, `<table`, `truncate`/`break-all` olmayan adres/hash alanları
- **Viewport:** `100vh`, `h-screen`, `100vw`, `w-screen`, `fixed`, `absolute inset`
- **Spacing:** `p-(8|10|12|16)`, `gap-(8|10|12)` (mobil varyantı olmayanlar)
- **Girdi (Problem B):** `onMouse(Down|Move|Up|Enter|Leave)`, `onDrag`, `draggable`, `addEventListener\((.touch|.pointer|.mouse)`, `touch-action`, `pointer-events`, `setPointerCapture`, `preventDefault`, `getScreenCTM`, `getBoundingClientRect`, `clientX`

Bunlara ek olarak **her durumda** şu dosyalar elle incelenir (grep eşleşmese de):

`app/layout.tsx` · `app/(site)/layout.tsx` · `app/globals.css` · `components/layout/Header.tsx` · `Footer.tsx` · `PageHero.tsx` · `components/game/Hud.tsx` · `ActionPanel.tsx` · `TerritoryControlBar.tsx` · `GameMap.tsx` · `RegionNode.tsx` · `ArmyLayer.tsx`

Oyun ekranında özellikle: HUD çakışması, alt aksiyon panelinin ekran dışına taşması, SVG `viewBox` ölçeklenmesi, bölge label taşması, dokunma hedeflerinin birbirine girmesi.

Cüzdan/ödeme akışında özellikle: uzun kripto adresleri, hash'ler, tutar/tarih tabloları.

## 4. Aşama 1 raporu

Kod yazmadan şu formatta rapor ver:

| Dosya | Sorun | Kırıldığı genişlik | Önem |
| ----- | ----- | ------------------ | ---- |

Önem dereceleri: **Kritik** (kullanılabilirliği bozuyor) · **Orta** (layout bozuk ama kullanılabilir) · **Kozmetik**.

Raporun **başında** Problem B için ayrı bir bölüm olsun: yukarıdaki 10 maddeden hangilerinin gerçekten geçerli olduğu, hangi dosyanın hangi satırında, ve önerilen düzeltmenin ne olduğu. Kök nedeni tespit edemediysen bunu açıkça yaz — tahmine dayalı düzeltme yazma.

Ardından önerilen düzeltme sırasını yaz. **Raporu sunduktan sonra dur, hiçbir dosyaya dokunma.**

## 5. Aşama 2 — Düzeltme (onay sonrası)

Sıra: **1) Problem B — dokunmatik sürükleme** (fonksiyonel hata, en öncelikli, tek başına bir grup) → **2)** Global layout iskeleti → **3)** Game ekranı layout → **4)** Lobi + landing → **5)** Cüzdan + ödeme → **6)** Auth → **7)** Profil/geçmiş/ayarlar/destek/durum → **8)** Legal/statik → **9)** Admin (en düşük öncelik).

Her grup ayrı adımdır. Grup bitince: `cd web && npm run build`, TypeScript hatası kontrolü, yapılan değişikliklerin özeti, sonra sonraki gruba geç.

## 6. Teknik kurallar

- **Mobile-first.** Base sınıflar en küçük ekrana göre; büyütme `sm:`/`md:`/`lg:` ile. Küçük ekranı düzeltmek için `max-*` yamaları üretme.
- Sabit `px` yerine `clamp()`, `min()`, `rem`, `grid-cols-1 → sm:grid-cols-2`, `flex-wrap`.
- **`min-w-0`**: flex/grid child'larının varsayılan `min-width: auto` değeri yatay taşmanın en yaygın kök nedenidir. Taşan flex satırlarında önce bunu kontrol et.
- `100vh`/`h-screen` yerine gerektiğinde `100dvh`/`100svh` — ancak `docs/13-scroll-lock.md`'nin v2 kararlarını bozmadan.
- Fixed UI elemanlarında `env(safe-area-inset-top/bottom)`.
- **Yatay taşmayı `overflow-x-hidden` ile gizleme.** Önce kaynağını bul ve düzelt. Gerçekten gerekliyse 🛠️ ile gerekçelendir.
- Uzun adres/hash için wrapping + truncation; kopyalama davranışı korunur.
- `web/hooks/use-mobile.ts` mevcut — ikinci bir mobil tespit mekanizması yazma.
- `components/ui/*` (shadcn) bileşenlerini mümkünse değiştirme, sorunu call-site'ta çöz. Zorunluysa etkilenen tüm kullanımları listele.
- **Girdi katmanı:** fare ve dokunmatik için **tek** kod yolu (Pointer Events). `isMobile` gibi bir bayrağa göre dallanan iki ayrı sürükleme implementasyonu yazma — `use-mobile.ts` layout içindir, girdi mantığı için değil.
- `touch-action: none` yalnızca sürüklenen etkileşim yüzeyine verilir; sayfa geneline veya scroll edilmesi gereken konteynerlere **verilmez**.
- Overlay katmanlarında `pointer-events: none` varsayılan olsun; yalnızca gerçekten tıklanabilir elemanlar `pointer-events: auto` alsın.

## 7. Doğrulama (her grup sonunda)

320 / 360 / 375 / 390 / 430px + landscape kontrolü:

- [ ] yatay scroll yok
- [ ] içerik kırpılmıyor, elementler üst üste binmiyor
- [ ] dokunma hedefleri yeterli
- [ ] modal/drawer ekran dışına çıkmıyor
- [ ] fixed UI safe-area ile uyumlu
- [ ] 390–430px görünüm bozulmadı
- [ ] ≥768px görünüm bozulmadı
- [ ] `npm run build` başarılı, TypeScript hatası yok, yeni warning yok

Problem B için ayrıca (tarayıcı DevTools cihaz emülasyonu **yeterli değildir** — gerçek dokunmatik cihazda veya en azından "touch" girdi simülasyonu açıkken doğrula):

- [ ] parmakla bölgeden komşusuna sürükleyip asker gönderilebiliyor
- [ ] sürükleme sırasında sayfa kaymıyor, zoom olmuyor, seçim/uzun basma menüsü açılmıyor
- [ ] parmak kaynak bölgenin dışına çıktığında sürükleme kopmuyor
- [ ] hedef bölge doğru hesaplanıyor (harita küçük ekranda ölçeklendiğinde de)
- [ ] kısa dokunuş sürükleme sayılmıyor, sürükleme dokunuş sayılmıyor
- [ ] ikinci parmak sürüklemeyi bozmuyor
- [ ] masaüstünde fare davranışı **değişmemiş**

## 8. Varsayımlar

Soru sorup bekleme (`01-workflow-rules.md` Bölüm 0.5). Karar gerekiyorsa makul teknik varsayımı yap ve `🛠️ Varsayım: ...` formatında belirt. Sonradan müşteriye doğrulatılması gereken nokta varsa `❓ Doğrulama: ...` ekle — ama ❓ karar erteleme gerekçesi değildir, önce uygulanabilir çözümü üret.

## 9. Dokümantasyon (görev sonunda)

`docs/24-responsive-small-screens.md` oluştur: tespit edilen problemler, uygulanan çözümler, responsive/viewport/safe-area kararları, game HUD kararları. 🔒/🛠️/❓ işaretleme sistemine uy.

> **Neden 24?** 18/19/20 numaraları `03-game-rules.md` ve `04-style.md` içindeki tarihsel atıflarla karışıyor (bkz. `CLAUDE.md`'deki dosya numarası çakışması uyarısı). 24 güvenli.

`CLAUDE.md`'nin "Her görevde önce oku" listesine bu dosyayı ekle: öncelik sırasında `04-style.md` ile aynı kademede (4); çakışmada **yerleşim/boyutlandırma → 24**, **estetik/renk/marka → 10 ve 23**.

---

**ŞİMDİ BAŞLA: Sadece Aşama 1. Önce dokümanları oku, sonra grep tabanlı denetimi yap, raporu sun ve bekle. Kod değiştirme.**
