# 24 — Küçük Ekran Yerleşimi ve Dokunmatik Girdi 🛠️

Bu dosya, `docs/24-responsive.md` görev talimatının **uygulanmış sonucudur**. Talimat dosyası "ne yapılacak"tır ve arşiv olarak durur; bu dosya "ne yapıldı, hangi değer nerede, neden öyle"dir ve küçük ekran yerleşimine / dokunmatik girdiye dokunan sonraki her görevin **tek doğruluk kaynağıdır**.

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanır, sayılar/kurallar değiştirilmez.
- 🛠️ **MÜHENDİSLİK VARSAYIMI:** Netleştirilmemiş noktada verilmiş, gerekçeli karar.
- ❓ Müşteriden ileride doğrulanması gereken nokta — **asla "dur ve sor" anlamına gelmez**, yanında zaten uygulanmış bir 🛠️ taşır.

## Kapsam ve öncelik

Yalnızca **yerleşim/boyutlandırma ve girdi katmanı**: genişlik/yükseklik, spacing, breakpoint davranışı, overflow, fixed/absolute konumlandırma, safe-area, dokunma hedefi boyutları, pointer/touch olay akışı.

🔒 **Bu dosyanın hiçbir hükmü olmayan alanlar:** `api/` altındaki hiçbir şey, oyun motoru, tick servisi, `GameHub` dispatch sözleşmesi, `PaymentService`/`WalletService`/`PayoutService`/`RefundService`, `WithdrawalRequest` state machine, `GameConfig.cs`, oyun kuralları (asker oranı, komşuluk, batching), renk paleti, marka dili, tipografi ölçeği, içerik sırası.

**Öncelik sırasındaki yeri:** `CLAUDE.md` kademe **4** — yani `04-style.md`, `10-ui-redesign.md` ve `23-game-visual-refresh.md` ile aynı kademede. Çakışmada:

- **yerleşim / boyutlandırma / girdi → bu dosya (24)**
- **estetik / renk / marka → `10-ui-redesign.md` ve `23-game-visual-refresh.md`**
- **fonksiyon / yapı (oyun ekranı dışı) → `04-style.md`**

---

## 1. 🔒 Regresyon kuralı — bu dosyanın en önemli maddesi

| Genişlik | Durum |
| -------- | ----- |
| 320px | Düzeltildi |
| 360px | Düzeltildi |
| 375px | Düzeltildi |
| 390px | **Referans tasarım — birebir korunur** |
| 430px | **Referans tasarım — birebir korunur** |
| ≥768px | **Birebir korunur** |

390–430px çalışan referans tasarımdır. Küçük ekranı düzelten hiçbir değişiklik bu aralığı değiştiremez. Bu dosyadaki her teknik karar bu kısıttan doğmuştur.

🛠️ **TEK İSTİSNA — admin kabuğu (Bölüm 6).** Gerekçesi ve sınırı orada.

---

## 2. Akışkan ölçek — neden breakpoint yerine `clamp()`

**Problem.** Tailwind'in en küçük breakpoint'i `sm:` = **640px**. Yani `px-4 sm:px-8` gibi klasik bir mobile-first yama, 390–430px'i de "küçük" sayar ve referans tasarımı bozar. Özel bir breakpoint eklemek ise `04-style.md` Bölüm 11'in 🛠️ **"standart Tailwind breakpoint'leri kullanılır, özel breakpoint eklenmez"** kararıyla çelişir.

🛠️ **Çözüm: breakpoint yerine akışkan ölçek.** Formül 390px'te **tam olarak** mevcut değeri üretir ve yalnızca ondan dar ekranlarda küçülür:

```
clamp(<alt sınır>, calc(<mevcut değer> + (100vw - 390px) * k), <mevcut değer>)
```

100vw ≥ 390px olduğunda üst sınır devrededir → değer değişmez. 100vw < 390px olduğunda değer doğrusal olarak küçülür, alt sınır içeriğin kenara yapışmasını engeller.

### 2.1 Token tablosu — `app/globals.css`

| Token | Karşılığı | 320px | 360px | 375px | **390px** | 430px |
|---|---|---|---|---|---|---|
| `--gutter-6` | `px-6` (kart/panel iç boşluğu) | 12.0 | 19.5 | 21.8 | **24.0** | 24.0 |
| `--gutter-8` | `px-8` (sayfa kenar boşluğu) | 18.0 | 26.0 | 29.0 | **32.0** | 32.0 |
| `--gutter-16` | `py-16` (dikey ortalanan kısa formlar) | 39.5 | 53.5 | 58.8 | **64.0** | 64.0 |
| `--landing-gap` | Landing dikey ritmi (`gap-6`) | 13.5 | 19.5 | 21.8 | **24.0** | 24.0 |
| `--landing-cta-h` | Landing CTA yüksekliği (`h-16`) | 46.5 | 56.5 | 60.3 | **64.0** | 64.0 |
| `--landing-stat-icon` | Landing istatistik ikonu (`size-10`) | 32.0 | 36.4 | 38.2 | **40.0** | 40.0 |
| `--header-gutter` | Paylaşılan `Header` `px-4`/`gap-4` (bkz. §9.2) | 8.0 | 12.6 | 14.3 | **16.0** | 16.0 |
| `--header-nav-min` | `Header` nav taban genişliği (§9.2) | 52.0 | 52.0 | 52.0 | **0.0** | 0.0 |
| `--header-actions-min` | `Header` sağ blok taban genişliği (§9.2) | 235.0 | 239.6 | 241.3 | **0.0** | 0.0 |
| `--header-brand-gap` | `Header` logo ↔ gezinme asgari ayrımı (§9.2) | 12.0 | 12.0 | 7.5 | **0.0** | 0.0 |

(değerler px)

🛠️ Son dört token formülün **ters yönlü** kullanımıdır: değer 390px'te **sıfırdır** (yani kural yokmuş gibi davranır, referans tasarım birebir korunur) ve yalnızca daha dar ekranlarda büyür. Deseni bozmaz — aynı "390px'te mevcut değeri üret, sadece altında değiş" kuralının bir taban (`min-width`) üzerinde uygulanmış hâlidir.

🛠️ `--landing-cta-h`'nin alt sınırı bilinçli olarak **2.75rem = 44px**: dokunma hedefi hiçbir genişlikte eşiğin altına inmez.

### 2.2 🛠️ Ne zaman token, ne zaman `min-w-0`

Akışkan token **son çaredir**. Taşmanın en yaygın kök nedeni boşluk değil, **flex çocuklarının varsayılan `min-width: auto` değeridir** — bu, bir çocuğun içeriğinden dar olmasını engeller ve satırı kapsayıcının dışına taşırır.

Önce bu sorulur: *"Bu satır daralabiliyor mu?"* Cevap hayırsa çözüm `min-w-0` + `truncate`'tir; bu, 390px ve üzerinde içerik zaten sığdığı için **hiçbir görsel etki yaratmaz** ve regresyon kuralına doğal olarak uyar. Boşluk küçültme yalnızca daralma payı tükendiğinde devreye girer.

⚠️ **Sınırı (§9.2'de bulundu):** `min-w-0` yalnızca içeriği gerçekten kırpılabilen (`truncate`) ya da sarabilen çocuklar için doğrudur. Kırpılamayan bir çocukta (ör. tek kelimelik bir nav bağlantısı) `min-w-0`, kutuyu içeriğinden dar bırakır ve metin kutunun dışına taşıp komşusunun üstüne biner — yani taşmayı çözmez, **görünür kılar**. `min-w-0` vermeden önce her zaman "bu çocuk kırpılabiliyor mu?" diye sorulur.

### 2.3 🛠️ `min-[22rem]` istisnası

İki yerde (`/profil` ve `/mac/[matchId]` istatistik ızgaraları) daralma yetmedi; `grid-cols-2` 320px'te hücreyi ~138px'e düşürüyor ve serbest metni okunamaz hâle getiriyordu. Orada `grid-cols-1 min-[22rem]:grid-cols-2` kullanıldı — **22rem = 352px** eşiği tam olarak 375 ile 390 arasına düşer.

Bu, `04-style.md` Bölüm 11'in yasağını ihlal etmez: yasaklanan şey theme'e **kalıcı bir özel breakpoint** eklemektir, tek seferlik bir arbitrary variant değil.

⚠️ `auto-fit`/`minmax` bilinçli olarak **kullanılmadı**: `/mac`'teki üçüncü kart `col-span-2` taşıyor ve sütun sayısı akışkan olunca tek sütunlu düzende implicit bir ikinci sütun doğurup taşmaya yol açıyor. Aynı sebeple `col-span-2` de `min-[22rem]:col-span-2` yapıldı.

### 2.4 🔒 Yatay taşma gizlenmez

`overflow-x-hidden` bir çözüm değildir, semptomu gizler. Bu görevde hiçbir yere eklenmedi; her taşma kaynağında düzeltildi. Global `html`/`body` `overflow-x: hidden` (`13-scroll-lock.md` Bölüm 2.1) yerinde durur ama bir **güvenlik ağıdır**, taşmayı meşrulaştırmaz.

---

## 3. Dokunma hedefi — görünüm değişmeden 44×44

**Problem.** Paylaşılan buton ölçeği (`components/ui/button.tsx`) en fazla 36px verir: `default` h-8 (32), `sm` h-7 (28), `lg` h-9 (36), `icon` 32×32. Hepsi 44px eşiğinin altında. O dosya bu görevde 🔒 **değiştirilmez** (görev talimatı Bölüm 6) ve butonların görünür boyutunu büyütmek 390–430px referans tasarımını bozar.

🛠️ **Çözüm: görünmez pseudo-eleman.** Görünen kutu birebir aynı kalır, yalnızca vurulabilir alan büyür. `app/globals.css`:

```css
@layer base {
  @media (pointer: coarse) {
    [data-slot="button"] { position: relative; }
    [data-slot="button"]::after {
      content: "";
      position: absolute;
      top: 50%; left: 50%;
      translate: -50% -50%;
      width: max(100%, 44px);
      height: max(100%, 44px);
    }
  }
}
```

Üç karar, üç gerekçe:

- **`@media (pointer: coarse)`** — yalnızca dokunmatik girdide devreye girer; fare ile gelen masaüstü davranışı (hover alanı dahil) hiç değişmez.
- **`width/height: max(100%, 44px)`** — geniş metin butonlarında yatayda **hiç** büyüme olmaz, yani yan yana duran butonların hedefleri birbirine girmez; yalnızca 44px'den kısa kenarlar tamamlanır (pratikte yükseklik, ve icon butonlarda genişlik).
- **`@layer base`** — utility'ler bu katmanı ezer. Böylece `absolute` konumlandırılmış butonlar (ör. sheet kapatma butonu) ve kendi `after:*` desenini taşıyan call-site'lar bozulmaz.

Bu desen daha önce oyun ekranındaki menü butonunda yerel olarak vardı; buraya taşınıp genelleştirildi ve yerel kopya kaldırıldı (tek kaynak — `06-coding-standards.md` "Kod Tekrarını Önleme").

**Ölçüldü** (Chromium, 320×568, `pointer: coarse`): `Giriş Yap` butonu görünür 238×32, `::after` 236×**44**, merkezden 21px yukarıdaki hit-test butonun kendisine düşüyor.

### 3.1 Metin bağlantıları

`<button>` öğelerinde ≥44px her genişlikte sağlandı. Düz `<a>` bağlantıları o kuralın kapsamı dışındaydı ve ~17px yükseklikte kalıyordu; iki ayrı karar verildi.

**Footer gezinme bağlantıları — 🛠️ 24×24 (WCAG 2.2 AA), yalnızca 390px altında.**

44px'e çıkarmak satır arasının ~27px açılmasını gerektirir; iki satırlık footer navigasyonunda bu ~54px'lik bir büyüme demektir. Dahası `gap-y-1` (4px) korunurken hedefleri 44px'e zorlamak komşu satırların hedeflerini **üst üste bindirir** — yani yanlış bağlantıya basma ihtimalini artırır, kullanılabilirliği düşürür.

Bunun yerine WCAG 2.2 AA "Target Size (Minimum)" eşiği olan 24×24 CSS px sağlandı (dikey 6px + yatay 4px iç boşluk → ölçülen **28.5×28.5**). Bağlantıların arka planı/çerçevesi olmadığı için bu, ekranda yalnızca satır aralığının bir miktar açılması olarak görünür.

🔒 Kural `@media (pointer: coarse) and (max-width: 389.98px)` ile sınırlandırıldı: ölçümde bu iç boşluk footer'ı 73px'ten 109px'e çıkarıyor ve Bölüm 1'in regresyon kuralı 390–430px'te böyle bir büyümeye izin vermiyor. Görev talimatının başarı ölçütü de zaten "320px genişlikte ve landscape modda" başlığı altında tanımlanmıştır.

**Cümle içi bağlantılar — 🛠️ kapsam dışı.** ("Hesabınız yok mu? **Kayıt olun**", "**Şifremi unuttum**".) WCAG 2.2 bir metin bloğu/cümle içindeki bağlantıları bu ölçütten açıkça muaf tutar; büyütmek metin akışını bozar.

❓ 390px ve üzeri telefonlarda da aynı hedef büyütmesi isteniyorsa `max-width` koşulu kaldırılır — bu, müşteri onayı gerektiren görünür bir tasarım değişikliğidir.

---

## 4. Problem B — dokunmatik sürükleyerek asker gönderme

Masaüstünde fare ile çalışan sürükle-bırak, dokunmatik cihazda hiç çalışmıyordu. Bu **fonksiyonel bir hataydı**, CSS/responsive sorunu değil.

### 4.1 🔒 Kök neden: `touch-action` SVG alt elemanında etkisiz

`touch-none` sınıfı `RegionNode.tsx`'te bir SVG `<g>` elemanına veriliyordu. **`touch-action`, Chromium ve WebKit'te SVG alt elemanlarında (`<g>`, `<polygon>`) uygulanmaz** — hit-test edilen SVG layout nesnesi bu değeri taşımaz.

Sonuç: sınıf kodda "var" görünüyordu ama harita yüzeyinin gerçek `touch-action` değeri `auto` kalıyordu. Tarayıcı tek parmak hareketini pan adayı sayıp pointer akışını `pointercancel` ile kesiyordu. Fare bu yoldan hiç geçmediği için sorun **yalnızca dokunmatikte** görünüyordu.

**Kural artık HTML tarafındaki harita yüzeyinde:**

```css
[data-game-map-surface] {
  touch-action: none;
  -webkit-user-select: none;
  user-select: none;
  -webkit-touch-callout: none;
}
```

Attribute yalnızca `app/game/[matchId]/page.tsx`'teki harita sarmalayıcısında (`absolute inset-2`) bulunur.

🔒 **Kapsam bilinçli olarak dar.** `touch-action: none` **asla** sayfa geneline, `html`/`body`'ye veya kaydırılması gereken kaplara verilmez — özellikle `Hud.tsx`'teki yatay oyuncu şeridine. Salt-okunur harita gösterimi (`/mac/[matchId]` maç özeti) bu attribute'u **taşımaz**, dolayısıyla orada parmakla sayfa kaydırma çalışmaya devam eder.

`user-select`/`-webkit-touch-callout`: harita gerçek `<text>` düğümleri içerir (bölge adı + asker sayısı); bunlar olmadan iOS'ta sürükleme metin seçimine düşüyor ve uzun basmada callout menüsü açılıyordu.

**Kök neden gerçek tarayıcıda doğrulandı.** Çalışan bir Practice maçında, dokunmatik bir viewport'ta ölçülen `getComputedStyle` değerleri:

| Eleman | `touch-action` |
|---|---|
| `[data-game-map-surface]` (HTML sarmalayıcı) | `none` |
| `g[role="button"]` (SVG bölge — eski kuralın verildiği yer) | **`auto`** |

SVG `<g>` `none` değerini ne kendisi alıyor ne de atasından miras alıyor — yani eski kod oraya `touch-none` verse bile etkisiz kalıyordu. Düzeltmenin neden HTML katmanında olması gerektiğinin doğrudan kanıtı budur.

### 4.2 🔒 İkinci, bağımsız hata: `pointercancel` saldırı gönderiyordu

`onPointerCancel` ile `onPointerUp` **aynı** handler'a bağlıydı ve o handler koşulsuz `onAttack(...)` çağırıyordu.

`pointercancel` bir **bırakma değildir** — tarayıcı jesti sahiplendiğinde ya da pointer geçersizleştiğinde gelir. Yani:

- iptal hareketin başında gelirse → hedef = kaynak → hiçbir şey olmaz (kullanıcının gördüğü semptom),
- iptal parmak başka bir bölgenin üstündeyken gelirse → **oyuncunun hiç bırakmadığı, geri alınamaz bir sevkiyat gönderilir** (para riski taşıyan bir maçta ciddi).

🔒 **Kural:** iptalin kendi yolu vardır (`handleDragCancel`) ve **asla saldırı göndermez**; yalnızca capture'ı bırakır, eşik aşılmışsa ardından gelebilecek sahte click'i bastırır ve state'i temizler.

### 4.3 Girdi state makinesi — `GameMap.tsx`

| Kural | Uygulama | Neden |
|---|---|---|
| Tek pointer sahipliği | `activePointerIdRef` + `e.isPrimary` | İkinci parmak `dragFromRegionId`'yi eziyordu; ilk parmağın `pointerup`'ı başka bir elemana düşüp state makinesini bozuyordu. |
| Sahiplik bırakma | `releaseDragCapture` — önce `hasPointerCapture` kontrolü | Örtük bırakmaya güvenmek yerine açık; eleman DOM'dan kalkmışsa `NotFoundError` fırlatılmasını da önler. |
| Tap/drag ayrımı | `CLICK_VS_DRAG_THRESHOLD_PX = 10` (eski: 4) | 4px parmak titremesine açıktı; 10px dokunmatik arayüzlerde yerleşik "slop" aralığıdır. |
| **Saldırı eşiğe bağlı** | `hasDraggedRef.current && targetId !== kaynak` | Eskiden saldırı **hiçbir** eşiğe bağlı değildi: eşik altındaki bir dokunuş, parmak komşu bölgeye kaysa bile geri alınamaz bir sevkiyat başlatabiliyordu. Artık eşik altı yalnızca bir "tap"tır ve bölgeyi seçer. |

🛠️ **`arrow.ts`'teki `MIN_DISTANCE = 12` ile hizalama yapılmadı, kasıtlı.** İkisi farklı birimde ve farklı amaçtadır: eşik **ekran pikselidir ve girdi niyetini** ölçer, `MIN_DISTANCE` ise **SVG birimidir ve geometrik okunabilirliği** korur (çok kısa bir okun çirkin görünmesini engeller). Saldırıyı yöneten girdi eşiğidir; ok eşiği yalnızca görseldir.

### 4.4 Elenen olasılıklar (görev talimatı Bölüm 3'ün 10 maddesi)

Aşağıdakiler denetlendi ve **geçerli çıkmadı** — ileride aynı semptom görülürse burada tekrar aranmasın:

- **Fare-özel handler:** `components/game` + `lib/game` altında tek bir `onMouse*` yok.
- **Üst katmanlar dokunuşu yutuyor:** tüm overlay'ler `pointerEvents="none"` taşıyor (`ArmyLayer`, capture flash, ok/nabız, `RegionLabel`, `StatusBanner`, ipucu bandı, `DevFpsOverlay`).
- **Passive listener:** oyun kodunda manuel `addEventListener('pointer*'/'touch*')` yok; her şey React synthetic.
- **SVG koordinat dönüşümü:** `toSvgPoint` zaten doğru — `getScreenCTM().inverse()` + `createSVGPoint`. Ölçek sabit varsayan hesap yok. (Bölüm 4.6'daki testte 390px'e ölçeklenmiş haritada hedef bölge doğru bulundu.)

### 4.5 `13-scroll-lock.md` ile ilişki — çelişki yok

O dosyanın Bölüm 1.5'teki 🔒 kısıtı ("bu görev oyun ekranındaki pointer/touch davranışına dokunmaz") düzeltmeyi **yasaklamaz, bu göreve bırakır**. Global `overflow: hidden` + `overscroll-behavior: none` jest sahiplenmesini azaltır ama `touch-action: auto` olduğu sürece ortadan kaldırmaz. Düzeltme `html`/`body`'ye değil yalnızca harita yüzeyine uygulandığı için v2 kararları bozulmadı.

### 4.6 Doğrulama — çalışan bir maçta ölçüldü

Görev talimatı Bölüm 7, DevTools cihaz emülasyonunu yetersiz sayıp "en azından touch girdi simülasyonu açıkken" doğrulama istiyor. Test, **çalışan backend + gerçek Practice maçı** üzerinde, Chromium'a CDP `Input.dispatchTouchEvent` ile **gerçek dokunma girdisi** gönderilerek yapıldı. (Playwright'ın fare API'si `touch-action` kararını test etmez; CDP touch olayları tarayıcının jest sahiplenme/`pointercancel` akışından gerçekten geçer.)

Saldırının gidip gitmediği, asker sayısı karşılaştırmasıyla değil — oyun canlı ve her saniye üretim var — **SignalR WebSocket çerçevelerindeki `AttackRegion` çağrıları sayılarak** ölçüldü.

| Talimat Bölüm 7 ölçütü | Sonuç |
|---|---|
| Parmakla bölgeden bölgeye sürükleyip asker gönderilebiliyor | ✅ 390px'te sürükleme yapıldı, asker gönderildi |
| Sürükleme sırasında sayfa kaymıyor / zoom olmuyor | ✅ scroll deltası `{x:0, y:0, main:0}` |
| Metin seçimi / uzun basma menüsü açılmıyor | ✅ seçim 0 karakter |
| Sürükleme önizlemesi fare ile aynı çalışıyor | ✅ nabız halkası + hedef vurgusu göründü |
| Parmak kaynak bölgenin dışına çıkınca sürükleme kopmuyor | ✅ haritanın uzak ucuna sürüklendi, `pointercancel` **0** |
| Hedef bölge doğru hesaplanıyor (ölçekli haritada) | ✅ doğru hedefe isabet |
| Kısa dokunuş sürükleme sayılmıyor | ✅ 3px titremeli tap → `AttackRegion` **0**, bilgi paneli açıldı |
| Sürükleme dokunuş sayılmıyor | ✅ sürükleme sonrası panel açılmadı |
| İkinci parmak sürüklemeyi bozmuyor | ✅ ikinci parmak eklendiğinde sürükleme sürdü, toplam `AttackRegion` **1** (0 veya 2 değil), `pointercancel` 0 |
| **Masaüstünde fare davranışı değişmemiş** | ✅ fare sürükleme → `AttackRegion` **1**, önizleme var, panel açılmadı; fare tek tık → `AttackRegion` **0**, panel açıldı |

⚠️ **Test aracının sınırı:** `AuthConfig.RegisterRateLimitPerHour = 5` olduğu için ardışık senaryolarda kayıt yerine giriş kullanıldı; test hesabı ikinci bir API instance'ı (aynı veritabanı, ayrı port) üzerinden oluşturuldu. Bu, uygulamanın değil test kurgusunun sınırlamasıdır; geliştiricinin çalışan sunucularına dokunulmadı.

❓ Gerçek fiziksel cihazda (iOS Safari / Android Chrome) teyit yine de önerilir — özellikle iOS'un callout/seçim davranışı, Chromium'un touch simülasyonuyla birebir aynı değildir.

---

## 5. Safe-area — hangi eleman neden `env()` alır

`GameShell` zaten `env(safe-area-inset-*)` padding taşıyor, ama bu **portal ile render edilen veya `fixed` konumlanan** öğeleri kapsamaz:

| Eleman | Sorun | Çözüm |
|---|---|---|
| `ActionPanel` (bottom sheet) | Portal ile doğrudan `body`'ye render edilir; `GameShell`'in padding'i uygulanmaz. Alt içerik home indicator'ın altında kalıyordu. | `pb-[max(1.5rem,env(safe-area-inset-bottom))]` |
| Oyun ekranı hata bandı | `fixed` her zaman viewport'a göre konumlanır, ata padding'i etkilemez. | `bottom-[max(1rem,env(safe-area-inset-bottom))]` |

🛠️ Her ikisinde de `max()` kullanıldı: safe-area bildirmeyen cihazlarda mevcut değer **birebir** korunur, yalnızca gerçekten inset bildiren cihazlarda büyür.

---

## 6. Sayfa bazlı kararlar

### 6.1 Landing (`/`) — 🛠️ iç scroll güvenlik ağı

`Landing.tsx`'in `main`'i ve `(site)/layout.tsx`'in chromeless dalı **üst üste** `overflow-hidden` veriyordu; `justify-center` ile birlikte, sığmayan içerik hem üstten hem alttan kırpılıp **erişilemez** kalıyordu (metin/CTA silinmiş gibi görünüyordu).

**Ölçüm:** 320×568'de Hero yığını (başlık + paragraf + iki CTA + adım şeridi + 2×2 istatistik kartı) ~527px yer istiyor; navbar ve footer düşüldükten sonra ~386px kalıyor. Akışkan ölçek açığı 375px'e kadar kapatıyor, daha darda kapatmıyor.

🛠️ Bu yüzden her iki katman da `overflow-y-auto` yapıldı. `13-scroll-lock.md` Bölüm 1.3 bu "son çare iç scroll"u zaten öngörmüştü. **390px ve üzerinde içerik zaten sığdığı için scroll hiç devreye girmez, görünüm birebir aynıdır.** `html`/`body` scroll kilidi (aynı dosyanın 🔒 asıl talimatı) bozulmadı — kaydırma `main`'in içinde kalır.

❓ 320–375px'te landing'in kaydırılabilir olması müşteri açısından kabul edilebilir mi.

### 6.2 `/sss` ve `/kurallar` — yetim `CardContent`

İki sorun bir aradaydı: (1) `<CardContent>` bir `<Card>` olmadan kullanılıyordu — görsel kart üretmiyor, yalnızca 24px yatay padding ekliyordu; sayfanın `px-4`'ü ile 320px'te 80px yatay alan gidiyordu. (2) `h-full`, `flex-1` taşımayan bir kapta belirsiz bir değere çözülüyordu, yani `13-scroll-lock.md` Bölüm 1.2'nin istediği kart-içi scroll fiilen çalışmıyordu.

🛠️ Çözüm bilinçli olarak bir `<Card>` **eklemek değil** — bugün ekranda görsel bir kart yok ve eklemek 390–430px görünümünü değiştirirdi. Yetim `CardContent` düz bir kaba çevrildi, padding'i `--gutter-6` ile verildi (390px'te birebir aynı 24px) ve `min-h-0 flex-1` ile iç scroll gerçekten çalışır hâle geldi.

### 6.3 🛠️ Admin kabuğu — Bölüm 1'in TEK istisnası

`AdminSidebar` her genişlikte `w-48 shrink-0` (192px) idi: 320px'te ana içeriğe 128px, `p-4` sonrası 96px kalıyordu.

🛠️ Eşik olarak `md:` (768px) seçildi, yani sidebar **390–430px'te de** yatay kaydırılabilir bir şeride döner. Bu, Bölüm 1'in regresyon kuralına bilinçli ve tek istisnadır: o kuralın amacı **çalışan** bir referans tasarımı korumaktır, admin'de ise 390px'te korunacak çalışan bir düzen yoktu (192px sidebar + ~198px içerik zaten kırıktı). Oyuncu tarafındaki hiçbir sayfa etkilenmez — admin kabuğu `app/admin/layout.tsx` ile tamamen ayrıdır.

🛠️ Yeni bir drawer/hamburger mekanizması **icat edilmedi**: yatay şerit aynı sorunu yeni state, yeni bileşen ve yeni bir açma/kapama etkileşimi olmadan çözer (YAGNI).

❓ Admin'in 390–430px'teki yeni görünümü (üstte yatay şerit) onaylanmalı.

### 6.4 Dokunulmayanlar ve nedenleri

- **Oyun ekranı ipucu bandı.** Denetimde "haritayı örtüyor" diye işaretlenmişti; geometri hesabı bunu **çürüttü**. Harita `preserveAspectRatio="xMidYMin meet"` ile üste hizalı olduğundan portrede altta 170px (320×568) – 423px (430×932) boşluk kalıyor, band o boşluğa düşüyor. Değişiklik yapılmadı.
- **`app/(site)/kayit/page.tsx`.** Zaten `py-6 sm:py-16` şeklinde ayrı bir çözüm taşıyor; mevcut hâli korundu.
- **Kısa durum/hata mesajı dalları** (`/odeme`, `/mac`, `/lobi/[inviteToken]` içindeki birkaç satırlık bloklar). Zaten rahatça sığıyorlar; "yolu geçmişken düzeltme" yasağı.
- **Scrollbar genişliği** (`globals.css`, 10px). Gerçek bir taşmaya yol açmıyor ve `Hud`'un şeridinde zaten gizlenmiş.
- **`components/ui/*`.** 🔒 Hiçbiri değiştirilmedi; her sorun call-site'ta çözüldü.

---

## 7. Doğrulama — nasıl ölçüldü

Playwright + Chromium ile 19 route × 7 viewport (320×568, 360×640, 375×667, 390×844, 430×932, yatay 812×375, masaüstü 1280×800), dokunmatik viewport'larda `hasTouch`/`isMobile` açık (`pointer: coarse` doğrulandı).

| Ölçüt | Sonuç |
|---|---|
| Yatay scroll (`scrollWidth > clientWidth`) | **0** — hiçbir viewport/route'ta |
| `body`/`window` dikey scroll | **0** — `13-scroll-lock.md` 🔒 talimatı korunuyor |
| `overflow:hidden` ile sessizce kırpılan içerik | **0** |
| `npm run build` + TypeScript | Geçti, yeni warning yok |

**Dokunma hedefi ölçümü** (en küçük kenar, px):

| Viewport | Buton | Footer bağlantısı | Footer yüksekliği |
|---|---|---|---|
| 320×568 (dokunmatik) | **44.0** | **28.5** | 109 |
| 390×844 (dokunmatik) | **44.0** | 16.5 | 85 |
| 430×932 (dokunmatik) | **44.0** | 16.5 | 64 |
| 1280×800 (fare) | 32.0 | 16.5 | 42 |

Masaüstü satırı, kuralların yalnızca `pointer: coarse` altında çalıştığını doğrular: fare ile gelen görünüm ve hedefler **hiç değişmemiştir**. 390/430 satırındaki footer değeri de Bölüm 3.1'deki 🔒 sınırlamanın çalıştığını gösterir.

⚠️ **Build notu:** `npm run build` `NEXT_PUBLIC_SITE_URL` ortam değişkeni ister (`docs/12-seo.md` Bölüm 3). `.env`'de tanımlı değil; doğrulama sırasında komut satırından verildi. Bu, bu görevle ilgisiz, önceden var olan bir ortam koşuludur — `.env` değiştirilmedi.

**Ölçülmeyen:** gerçek dokunmatik cihazda uçtan uca maç (bkz. Bölüm 4.6 ❓).

---

## 8. Bu ekranlara dokunan bir sonraki görev için kontrol listesi

- [ ] Yatay taşma mı düzeltiyorsun? **Önce `min-w-0`** — flex/grid çocuklarının `min-width: auto` değeri en yaygın kök nedendir (§2.2). Boşluk küçültme son çaredir.
- [ ] Boşluk/boyut küçültmen mi gerekti? **`sm:` KULLANMA** — 390–430px'i bozar. `app/globals.css`'teki akışkan token'lardan birini kullan, yoksa aynı formülle yenisini ekle (§2).
- [ ] Yeni bir token mu ekliyorsun? Formülün 390px'te **tam olarak** mevcut değeri verdiğini hesapla ve §2.1 tablosuna ekle.
- [ ] Yatay taşmayı `overflow-x-hidden` ile mi kapatıyorsun? **Yapma** — kaynağını bul (§2.4).
- [ ] Yeni bir sürükleme/pointer etkileşimi mi? `touch-action`'ı **SVG içine değil**, HTML sarmalayıcıya ver (§4.1). Tek kod yolu: Pointer Events; `isMobile`'a göre dallanan ikinci bir implementasyon yazma.
- [ ] `pointercancel` mi ele alıyorsun? **`pointerup` ile aynı handler'a bağlama** — iptal bir bırakma değildir, state değiştiren hiçbir işlem tetiklemez (§4.2).
- [ ] `fixed` ya da portal ile render edilen yeni bir UI mı? `env(safe-area-inset-*)` gerekir — ata padding'i onu kapsamaz (§5).
- [ ] Yeni bir buton mu? Dokunma hedefi otomatik gelir (§3) — `components/ui/button.tsx`'e dokunma, yerel bir `after:*` kopyası da yazma.
- [ ] Bir kaydırma kabında içerik mi ortalıyorsun? **`justify-center` DEĞİL, `justify-center-safe`** — taşma olduğunda üst yarı erişilemez hâle gelir (§9.1).
- [ ] Bir flex çocuğuna `min-w-0` mı veriyorsun? O çocuğun içeriği **kırpılabiliyor mu** (`truncate`/sarma) diye sor. Kırpılamıyorsa `min-w-0` taşmayı görünür kılar, gizlemez (§9.2).
- [ ] Örtüşme mi arıyorsun? DOM sırasına güvenme; `scripts/shot.mjs` hit-test denetimini kullan (§9.4).

---

## 9. `docs/25-responsive-v2.md` turu — görsel doğrulama ve düzeltmeler

Bu tur `scripts/shot.mjs` (Playwright) ile 29 route × 6 viewport'u hem oturumlu hem oturumsuz gezdi, screenshot'ları **görsel olarak** inceledi ve iki gerçek küçük-ekran hatasını düzeltti. 🔒 Turun bağlayıcı kısıtı: **390×844 ve 430×932 baseline ile piksel olarak aynı kalacak.** Bu kısıt hiçbir yerde esnetilmedi; sağlanamayan düzeltmeler uygulanmadı ve §9.3'te listelendi.

### 9.1 🔒 Landing H1'in üstten kırpılması — `justify-center-safe`

**Belirti.** `/` sayfasında `FETHET. SAVUN. KAZAN.` başlığının üst kısmı kesiliyordu: 320px'te **45px**, 360px'te **34px**, 375px'te **31px**; 390px ve üzerinde 0px.

**Kök neden.** §6.1'de `overflow-hidden` → `overflow-y-auto` yapılan `main`, `justify-center` taşıyor. Bir kaydırma kabında `justify-content: center`, içerik kaptan uzun olduğunda taşmayı **iki uçtan birden** üretir; üstteki yarı `scrollTop = 0`'ın **yukarısına** düşer ve tarayıcıda negatif kaydırma olmadığı için kaydırılarak dahi erişilemez. Yani §6.1'in eklediği iç scroll, alttaki taşmayı kurtarmış ama üsttekini kurtarmamıştı.

**Çözüm.** `components/landing/Landing.tsx` → `justify-center` yerine `justify-center-safe` (`justify-content: safe center`, Tailwind 4.3). `safe`, **yalnızca taşma varken** hizayı `start`'a düşürür; taşma yoksa `center` ile birebir aynıdır — bu yüzden 390/430 piksel olarak değişmez (ölçüldü: fark 0).

🛠️ Alternatif olan "`justify-center` kaldır + çocuğa `m-auto`" desenine gidilmedi: `safe` anahtar kelimesi niyeti doğrudan ifade eder ve tek kelimelik bir değişikliktir.

### 9.2 🔒 Paylaşılan `Header`'da gezinme ↔ buton örtüşmesi

**Belirti.** Oturumsuz durumda, `Header` kullanan **her** sayfada `Kurallar` bağlantısı `Giriş Yap` bağlantısının üstüne biniyordu ("KurallarGiriş Yap") — 320, 360 ve 375px'te okunamaz ve yanlış hedefe basılabilir hâlde.

**Kök neden (ölçüldü, Chromium `/giris`).** Header'ın doğal içerik genişliği **384px**; kullanılabilir alan 320px'te 256px, 375px'te 311px. Hem logo bloğu hem nav `min-w-0` taşıdığı için daralma ikisi arasında **oransal olarak paylaşılıyordu**. Logo `truncate` ile güvenli kırpılıyor, ama nav'ın tek kelimelik çocuğu (`Kurallar`) kırpılamıyor: nav kutusu daralınca metin kutunun **dışına taşıp** komşusunun üstüne biniyordu. Ölçülen nav genişliği / min-content: 320→0/50, 360→1/50, 375→11/50, 390→21/50, 430→47/50, 768→50/50.

> Bu, §2.2'nin ("önce `min-w-0`") **sınırıdır** ve oraya bir soru olarak eklendi: `min-w-0` yalnızca içeriği gerçekten kırpılabilen (truncate/sarma) çocuklar için doğrudur. Kırpılamayan bir çocukta `min-w-0`, taşmayı çözmez — **görünür** kılar.

**Çözüm.** Daralma tamamen `truncate`'li logo bloğuna yönlendirildi. Dört akışkan token (§2.1), hepsi 390px'te **sıfır etki**:

| Token | Ne yapar |
|---|---|
| `--header-nav-min` | nav'a min-content tabanı verir (3.25rem), daralmayı durdurur |
| `--header-actions-min` | nav + boşluk + giriş/kayıt bloğunu kapsayan sağ bloğa taban verir; değerini sabit varsaymaz, bileşenlerinden `calc()` ile hesaplar |
| `--header-gutter` | `px-4`/`gap-4`'ü 320px'te 0.5rem'e indirir; taban genişlikler ancak bu boşlukla sığıyor |
| `--header-brand-gap` | `justify-between` garanti boşluk vermediği için logo ile gezinme arasına asgari ayrım koyar |

**Sonuç.** 320px'te header temiz bir "ikon + Kurallar + Giriş Yap + Kayıt Ol" satırına döner (marka yazısı en dar ekranda tamamen kırpılır, ikon kalır). 390/430/768'de **ölçülen kutu değerleri ve pikseller birebir aynı** kaldı.

⚠️ `components/landing/Navbar.tsx` aynı yapıyı taşır ama gezinme linkleri `hidden md:flex` olduğu için dar ekranda çakışma **üretmiyor** — bilinçli olarak dokunulmadı ("yolu geçmişken düzeltme" yasağı).

### 9.3 ❓ Bulunan ama 🔒 kısıt yüzünden UYGULANMAYAN düzeltmeler

Aşağıdakiler gerçek hatadır, kök nedeni tespit edilmiştir ve **düzeltmeleri hazırdır**; ancak hepsi 390px **ve/veya** 430px'te de mevcut olduğu için düzeltme o genişlikleri de değiştirir ve turun 🔒 kısıtını ihlal ederdi. Kısıt kaldırılırsa hepsi tek satırlık müdahalelerdir.

| # | Yer | Belirti | Kök neden | Görüldüğü genişlikler |
|---|---|---|---|---|
| A | `Header` (oturumsuz) | `Kurallar` ↔ `Giriş Yap` teması/binmesi **390px'te sürüyor** | §9.2 ile aynı; 390px zaten daralma rejiminde (nav 21/50) | 390 |
| B | `Header` (oturumlu) | nav iki satıra sarıyor (`Lobi` / `Kurallar`), kullanıcı adı `A…`'ya iniyor | aynı genişlik açığı; `flex-wrap` sarmayı 390/430'da da tetikliyor | 320–430 |
| C | `/admin/odemeler` | Çekim kartındaki **`Onayla` / `Reddet` butonları tamamen kırpılıyor** — admin telefondan çekim onaylayamıyor | kart `overflow-hidden`; içerik 503px, kutu 288–398px | 320–430 |
| D | `/admin/loglar` | Log satırları sessizce kırpılıyor, mesaj okunamıyor | `whitespace-nowrap` + yatay scroll yok | 320–768 |
| E | `components/game/ActionPanel.tsx` | Sheet'in `absolute top-4 right-4` kapatma (×) butonu, sağ üstteki `text-3xl` asker sayısının üstüne biniyor | başlık satırı kapatma butonuna yer ayırmıyor (ör. sağ padding yok); `components/ui/sheet.tsx` §6.4 gereği değiştirilemez, çözüm çağrı yerinde | 320–430 |

🛠️ C ve D için not: §6.3 admin kabuğunu Bölüm 1'in tek istisnası ilan etmişti; bu turun görev talimatı ise istisnasız bir piksel kuralı koydu ve **bu tur için o daha spesifik talimat uygulandı**. Müşteri onayıyla C ve D §6.3 kapsamında düzeltilebilir.

### 9.4 Doğrulama altyapısı — `scripts/shot.mjs`

🛠️ Repoya eklenen tek yeni dosya. Route listesini **varsaymaz**, `web/app` altındaki `page.tsx`'leri gezerek üretir (29 route). Denetimleri:

- **Yatay taşma** — `scrollWidth > clientWidth` (doküman + kap bazlı; yatayda kırpan atalar elenir).
- **Örtüşme (hit-test).** DOM sırası veya "header'ın altındaki ilk blok" sezgisi yerine: görünür her metin/etkileşimli elemanın üst kenarı boyunca üç noktada `document.elementFromPoint()`. Dönen eleman elemanın kendisi, bir alt öğesi ya da bir **üst** öğesi değilse örtüşmedir. Üst öğe kabul edilir, çünkü §3'ün `[data-slot="button"]::after` dokunma hedefi originating element'i döndürür.
- **Üstten kırpılma.** Elemanın kutusu, **tüm kırpan atalarla** kesiştirilir; kesişimin üstü kendi üstünden aşağıdaysa erişilemez içerik demektir. §9.1 bu ölçütle yakalandı — yatay taşma metriğiyle **görünmezdi**.

🛠️ Kararlılık (piksel karşılaştırması gürültüsüz olsun diye): `reducedMotion: "reduce"`, `networkidle` + `document.fonts.ready`, CSS animasyon/geçiş sürelerinin 1ms'e indirilmesi (kaldırılması değil — eleman **bitiş** durumunda kalsın diye), `getAnimations().finish()` (framer-motion WAAPI kullanır, CSS süresi onu etkilemez) ve **video dondurma**: Landing'in arka planı `<video>`'dur ve dondurulmadan aynı kod iki turda ~780.000 piksel fark üretiyordu. Dondurduktan sonra kalan fark yalnızca video decode gürültüsüdür ve `-fuzz 5%` eşiğinde tam olarak 0'a düşer — regresyon ölçütü budur.

⚠️ **Harness'ta bir ürün hatası telafi ediliyor.** `web/lib/admin/api.ts:18` istek yolunun başına `NEXT_PUBLIC_API_URL` ekliyor, ardından `authFetch` (`web/lib/identity.ts:145`) aynı tabanı bir kez daha ekliyor → `http://host:portHTTP://host:port/api/...`. Bu geçersiz URL `fetch()` tarafından ağa çıkmadan reddediliyor, dolayısıyla **her `/admin/*` sayfası veriyi hiç yükleyemiyor**. Bu bir işlev hatasıdır, bu turun kapsamı dışındadır ve **düzeltilmemiştir**; `shot.mjs` yalnızca test sırasında `fetch`'i sarmalayarak URL'i normalleştirir, aksi hâlde admin sayfaları gerçek içerikle hiç denetlenemezdi.

### 9.5 Test edilemeyen durumlar

- **`/lobi/[inviteToken]`** — davet tokeni ne API'de ne UI'da dışa veriliyor (`Room.InviteToken` hiçbir DTO'da yok). Yalnızca **"Bu davet artık geçerli değil"** hata durumu denetlendi; dolu lobi görünümü **test edilmedi**.
- **`/odeme/[invoiceId]`** — test oturumuna ait bir fatura üretilemedi (`Payment.Mode = Fake` girişleri anında onaylıyor, eksik bakiye doğmuyor). Yalnızca **"Invoice bulunamadı"** durumu denetlendi; QR/adres/sayaç taşıyan asıl ödeme ekranı **test edilmedi**.
- **`/sifre-sifirla/[token]`** — geçersiz token ile form durumu denetlendi.
- **Dokunmatik sürükleme (Problem B).** Bu tur yalnızca yerleşimi doğrular; §4.6'daki ❓ gerçek cihaz doğrulaması **hâlâ açıktır**, bu turda kapanmadı.
- **`/game/[matchId]`** oturumlu ve **canlı bir Practice maçıyla** render edildi (`wintowar:match:<id>:playerId` fixture ile localStorage'a yazılır); yerleşimi altı viewport'ta da denetlendi.
