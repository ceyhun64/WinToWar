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

(değerler px)

🛠️ `--landing-cta-h`'nin alt sınırı bilinçli olarak **2.75rem = 44px**: dokunma hedefi hiçbir genişlikte eşiğin altına inmez.

### 2.2 🛠️ Ne zaman token, ne zaman `min-w-0`

Akışkan token **son çaredir**. Taşmanın en yaygın kök nedeni boşluk değil, **flex çocuklarının varsayılan `min-width: auto` değeridir** — bu, bir çocuğun içeriğinden dar olmasını engeller ve satırı kapsayıcının dışına taşırır.

Önce bu sorulur: *"Bu satır daralabiliyor mu?"* Cevap hayırsa çözüm `min-w-0` + `truncate`'tir; bu, 390px ve üzerinde içerik zaten sığdığı için **hiçbir görsel etki yaratmaz** ve regresyon kuralına doğal olarak uyar. Boşluk küçültme yalnızca daralma payı tükendiğinde devreye girer.

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

### 3.1 ❓ Metin bağlantıları kriteri karşılamıyor

`<button>` öğelerinde ≥44px sağlandı. **Düz `<a>` metin bağlantılarında sağlanamadı** — footer linkleri (`Kurallar`, `SSS`, `Kullanım Şartları`…) ~17px, cümle içi linkler (`Kayıt olun`, `Şifremi unuttum`) ~16px yüksekliğinde.

🛠️ Bunlara dokunulmadı, çünkü 44px'e çıkarmak için gereken dikey nefes payı (footer'da `gap-y-1` → `gap-y-3` civarı) **390–430px'te de** görünür bir değişiklik üretirdi ve Bölüm 1'i ihlal ederdi. WCAG 2.2 "Target Size (Minimum)" da bir metin bloğu/cümle içindeki bağlantıları muaf tutar.

❓ **Müşteri kararı gerekiyor:** footer/inline metin bağlantılarının dokunma hedefi için (a) mevcut kompakt görünüm korunsun mu, yoksa (b) 390–430px dahil footer bir miktar büyüsün mü.

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
- **SVG koordinat dönüşümü:** `toSvgPoint` zaten doğru — `getScreenCTM().inverse()` + `createSVGPoint`. Ölçek sabit varsayan hesap yok.

### 4.5 `13-scroll-lock.md` ile ilişki — çelişki yok

O dosyanın Bölüm 1.5'teki 🔒 kısıtı ("bu görev oyun ekranındaki pointer/touch davranışına dokunmaz") düzeltmeyi **yasaklamaz, bu göreve bırakır**. Global `overflow: hidden` + `overscroll-behavior: none` jest sahiplenmesini azaltır ama `touch-action: auto` olduğu sürece ortadan kaldırmaz. Düzeltme `html`/`body`'ye değil yalnızca harita yüzeyine uygulandığı için v2 kararları bozulmadı.

### 4.6 ❓ Doğrulama durumu

Kök neden, düzeltme ve derlenmiş CSS çıktısı doğrulandı (`[data-game-map-surface]{touch-action:none;…}` üretilen CSS'te mevcut). **Gerçek dokunmatik cihazda uçtan uca maç testi yapılmadı** — bu, çalışan bir backend + gerçek bir maç oturumu gerektiriyor. Görev talimatı Bölüm 7 bunu açıkça istiyor.

❓ Gerçek cihazda doğrulanması gerekenler: parmakla bölgeden bölgeye sürükleyip asker gönderme, sürükleme sırasında sayfanın kaymaması/zoom olmaması, parmak kaynak bölgenin dışına çıkınca sürüklemenin kopmaması, ikinci parmağın sürüklemeyi bozmaması.

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
