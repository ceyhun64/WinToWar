# Küçük Ekran Mobile Responsive Görsel Doğrulama ve Düzeltme Döngüsü

Projeyi yalnızca **küçük mobil ekranlar** için responsive açıdan denetle ve düzelt.

## Önemli sınır

Masaüstü ve tablet tasarımları şu anda doğru çalışıyor. Bu nedenle:

* Desktop layout'u değiştirme.
* Tablet layout'u değiştirme.
* Mevcut 390x844 ve 430x932 görünümlerini başlangıç referansı olarak koru.
* Yapacağın CSS/layout değişiklikleri mümkün olduğunca küçük mobil breakpoint'lerine (`320`, `360`, `375` vb.) scoped olsun.
* Bir düzeltme 390x844 veya 430x932 görünümünü bozuyorsa o düzeltmeyi geri al.
* Mevcut tasarımı gereksiz yere yeniden tasarlama. Amacın yeni bir tasarım yapmak değil, küçük mobil ekranlardaki responsive hataları düzeltmek.

---

## 1. Playwright screenshot altyapısını oluştur

`/scripts/shot.mjs` dosyasını oluştur.

Script:

* verilen route'u açabilmeli,
* verilen viewport ile çalışmalı,
* tam sayfa screenshot almalı,
* screenshot'ı şu formatta kaydetmeli:

`/tmp/shots/<route>-<w>x<h>.png`

Viewport seti:

* 320x568
* 360x640
* 375x667
* 390x844
* 430x932
* 768x1024

Playwright ayarları:

* `deviceScaleFactor: 2`
* `hasTouch: true`
* `isMobile: true`

Gerekirse route adındaki `/`, `:`, `?` gibi dosya sistemi açısından problem yaratabilecek karakterleri güvenli bir isimlendirmeye dönüştür.

Script'in route ve viewport parametreleriyle tekrar tekrar çalıştırılabilir olmasını sağla.

---

## 2. Önce baseline oluştur

Düzeltme yapmadan ÖNCE mevcut uygulamanın referans görüntülerini oluştur.

Her route için:

* 390x844
* 430x932

screenshot al ve:

`/tmp/shots/baseline/`

altına kaydet.

Örnek:

`/tmp/shots/baseline/landing-390x844.png`

`/tmp/shots/baseline/landing-430x932.png`

Bu görüntüler mevcut tasarımın referansıdır.

**Baseline oluşturulduktan sonra bunları değiştirme.**

---

## 3. Projedeki tüm route'ları bul

Projeyi inceleyerek uygulamadaki bütün gerçek route'ları tespit et.

Her route için responsive doğrulama yap.

Route'ları varsayarak uydurma. Router/config/source code üzerinden gerçek route listesini çıkar.

Her route şu ekranlarda kontrol edilmeli:

### Kritik küçük mobil ekranlar

* 320x568
* 360x640
* 375x667

### Regresyon kontrolü

* 390x844
* 430x932

### Referans olarak

* 768x1024

---

## 4. Screenshot al ve GERÇEKTEN GÖRSEL OLARAK İNCELE

Özellikle 320, 360 ve 375 genişliklerindeki screenshot'ları aç ve görsel olarak incele.

Sadece JavaScript ölçümlerine veya DOM metriklerine güvenme.

Şunları görsel olarak kontrol et:

* Header/content overlap
* H1/H2 gibi başlıkların header altında kalması
* Üstten kırpılan içerik
* Yatay taşma
* Ekran dışına çıkan butonlar
* Çok uzun tek satır metinler
* Kartların viewport dışına taşması
* Görsellerin container'dan taşması
* Navigation'ın taşması
* Modal/dialog taşmaları
* Fixed/sticky elementlerin içeriğin üzerine binmesi
* `100vw` kaynaklı yatay overflow
* Sabit genişlik verilmiş elementler
* Negatif margin kaynaklı taşmalar
* Mobilde gereğinden fazla büyük padding/margin
* Text wrapping problemleri
* `position: fixed/sticky` kaynaklı sorunlar
* Safe-area problemleri
* İçeriğin viewport'un altında/üstünde kalması

Özellikle şu mevcut problemi doğrula:

Landing sayfasında:

**`FETHET. SAVUN. KAZAN.`**

H1'i 375x667 görünümünde header'ın altında kalıyor ve üst kısmı kırpılıyor.

Bu problemi yalnızca overflow veya horizontal scroll metriğiyle tespit etmeye çalışma. Görsel olarak da doğrula.

---

## 5. Örtüşme tespiti ekle

Responsive test altyapısına header/content overlap kontrolü ekle.

Her route için:

1. `position: sticky` veya `position: fixed` olan header elementlerini tespit et.
2. Header'ın `getBoundingClientRect()` değerini al.
3. Header'ın hemen altındaki ilk gerçek içerik bloğunu tespit et.
4. Bunun da `getBoundingClientRect()` değerini al.
5. İki rectangle'ın kesişip kesişmediğini kontrol et.

Örneğin mantık olarak:

```js
const overlaps =
  headerRect.bottom > contentRect.top &&
  headerRect.top < contentRect.bottom &&
  headerRect.right > contentRect.left &&
  headerRect.left < contentRect.right;
```

Ancak yalnızca bu basit kontrolle yetinme.

Header `fixed` veya `sticky` ise ve içerik header'ın altında başlayacak şekilde offset/padding alması gerekirken almıyorsa bunu ayrıca raporla.

Özellikle şu durumu hata kabul et:

* Header viewport üzerinde duruyor.
* Hemen altındaki H1/content header'ın arkasına giriyor.
* Content'in üst kısmı görsel olarak header tarafından kapatılıyor.

Bu hata horizontal overflow olmadığı halde gerçek bir responsive bug'dır.

---

## 6. Mevcut layout'u koruyarak düzelt

Bir hata bulduğunda önce problemin gerçek kaynağını tespit et.

Rastgele CSS değerleri değiştirme.

Öncelikli olarak şunları araştır:

* header height
* top padding/margin
* mobile breakpoint
* fixed/sticky positioning
* `100vh` / `100dvh`
* `100vw`
* container max-width
* fixed width
* flex/grid davranışı
* `overflow`
* negative margin
* absolute positioning
* typography / line-height
* mobile-specific spacing

Mümkünse en küçük değişiklikle problemi çöz.

Örneğin yalnızca küçük mobil ekranlarda problem varsa desktop/tablet stilini değiştirmek yerine uygun bir mobile media query kullan.

---

## 7. Her değişiklikten sonra doğrulama yap

Her düzeltmeden sonra:

1. Değişiklik yapılan route'un 320x568 screenshot'ını al.
2. 360x640 screenshot'ını al.
3. 375x667 screenshot'ını al.
4. 390x844 screenshot'ını al.
5. 430x932 screenshot'ını al.
6. Gerekirse 768x1024 screenshot'ını al.
7. Screenshot'ları gerçekten açıp görsel olarak incele.
8. Overlap testini tekrar çalıştır.
9. Horizontal overflow testini tekrar çalıştır.
10. Baseline ile regresyon karşılaştırması yap.

---

## 8. Baseline regresyon kontrolü

390x844 ve 430x932 görüntülerini mevcut baseline ile karşılaştır.

Amaç birebir piksel eşitliği zorlamak değil; mevcut tasarımın beklenmedik şekilde değişip değişmediğini tespit etmek.

Eğer değişiklik:

* layout'u bozuyorsa,
* önemli bir elementin yerini değiştiriyorsa,
* typography'yi gereksiz değiştiriyorsa,
* desktop/tablet davranışını etkiliyorsa,
* 390 veya 430 görünümünü belirgin şekilde bozuyorsa,

değişikliği geri al.

Ancak küçük mobil bugını çözmek için 390/430'da yalnızca beklenen ve zararsız bir piksel değişikliği oluşuyorsa bunu bağlama göre değerlendir.

---

## 9. Değişiklikleri route bazında takip et

Her tur sonunda şu bilgileri raporla:

### Tur X

**Route:** `/landing`

**Ekran:** `375x667`

**Gördüğüm problem:**
H1 header'ın arkasına giriyor ve üst kısmı kırpılıyor.

**Kök neden:**
Örneğin fixed header yüksekliği kadar üst spacing verilmemiş.

**Yaptığım değişiklik:**
Örneğin küçük mobil breakpoint'te content top padding'i düzeltildi.

**Sonuç:**

* 320x568: geçti / kaldı
* 360x640: geçti / kaldı
* 375x667: geçti / kaldı
* 390x844: baseline regresyon yok / var
* 430x932: baseline regresyon yok / var
* overlap: geçti / kaldı
* horizontal overflow: geçti / kaldı

Sonra bir sonraki route'a geç.

---

## 10. ÖNEMLİ: Sadece bilinen hatayı düzeltip durma

Landing'deki H1 problemi başlangıç örneğidir.

Bütün route'ları taramaya devam et.

Her route'ta küçük mobil ekranlarda:

* overlap
* clipping
* overflow
* wrapping
* spacing
* fixed/sticky element problemleri
* viewport dışına taşma

olup olmadığını kontrol et.

Ancak gerçek bir problem görmeden tasarımı değiştirme.

---

## 11. Final doğrulama

Tüm düzeltmeler bittikten sonra bütün route'ları tekrar test et.

Her route için:

* 320x568
* 360x640
* 375x667
* 390x844
* 430x932
* 768x1024

screenshot'larını oluştur.

Son olarak:

* küçük mobil ekranlarda görsel hata kalmadığını,
* overlap kalmadığını,
* horizontal overflow kalmadığını,
* 390/430 baseline'larında istenmeyen regresyon olmadığını,
* tablet/desktop layout'unun etkilenmediğini

kontrol et.

---

## 12. Bu tura özel ek kurallar

- §6'daki "mobile media query" yaklaşımı GEÇERSİZDİR. Düzeltmeler
  mevcut clamp() akışkan token mimarisiyle yapılır; yeni breakpoint
  veya max-width media query eklenmez (04-style.md Bölüm 11).

- §8 gevşetmesi GEÇERSİZDİR. 390x844 ve 430x932 baseline ile piksel
  olarak aynı kalmalı. Fark çıkarsa değişikliği geri al ve raporla.

- Screenshot kararlılığı: context'i reducedMotion:'reduce' ile aç,
  networkidle + document.fonts.ready bekle, CSS animasyon/transition'ları
  test sırasında devre dışı bırak. Yoksa piksel diff'i gürültülü olur.

- Modal/drawer'lar için ayrı bir akış yaz: tetikleyici elemana tıkla,
  modal açıkken 320x568 ve 375x667 screenshot al. En az şu bileşenler:
  [projedeki modal/dialog/sheet kullanan yerleri router'dan çıkar].

- Auth/backend gerektiren route'ları (özellikle /game/[matchId]) hangi
  yöntemle render ettiğini raporla; render edilemiyorsa "test edilmedi"
  yaz, "geçti" YAZMA.

- Örtüşme tespitinde "header'ın altındaki ilk içerik bloğu" sezgisi
  zayıf. Bunun yerine: görünür her metin/etkileşimli elemanın üst-orta
  noktasında document.elementFromPoint() çağır; dönen eleman o elemanın
  kendisi veya alt öğesi değilse ÖRTÜŞME olarak işaretle. Bu, DOM
  sırasından bağımsız olarak gerçek görsel kapanmayı yakalar.

- Tur bitiminde docs/24-responsive-small-screens.md'ye eklenen
  kararları yaz (örtüşme kök nedeni, uygulanan çözüm, header offset
  yaklaşımı).

- Bu tur dokunmatik sürüklemeyi doğrulamaz. Problem B'nin gerçek
  cihaz/sentetik touch testi ayrı bir görev olarak açık kalır —
  "tamamlandı" raporunda bunu açıkça belirt.

**Özellikle screenshot'ları gerçekten açıp görsel olarak incelemeden "tamamlandı" deme.**

Amaç:

> Desktop ve tablet tasarımını koruyarak yalnızca küçük mobil ekranlardaki responsive sorunları bulmak, görsel olarak doğrulamak, düzeltmek ve her düzeltmeden sonra regresyon testi yapmaktır.
