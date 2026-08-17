# 08 — Sayfa İçerik Mimarisi (Ne Gösterilir / Ne Gösterilmez) 🛠️

> **Bu dosya neyi çözer:** `07-pages.md` bir sayfanın **nerede** olduğunu, hangi state'lere sahip olduğunu ve hangi veri kaynağından beslendiğini tanımlar. `04-style.md` bir sayfanın **nasıl göründüğünü** (renk, boşluk, bileşen) tanımlar. Bu ikisi arasında boş bir alan vardı: bir sayfanın **içinde, yukarıdan aşağı hangi sırayla hangi bilgi/metin/aksiyon bloğunun bulunacağı** hiçbir dosyada net değildi — bu yüzden state.io referansıyla üretilen sayfalar ya boş hissettiriyor (bilgi eksik/gerekçesiz) ya da gereksiz hissettiriyordu (kullanıcının o an karar vermesi için gerekmeyen bilgi doldurulmuş). Bu dosya o boşluğu kapatır.
>
> **Doküman önceliği:** Çakışma durumunda tek doğruluk kaynağı `CLAUDE.md`'deki sıradır. Bu dosya o sırada `07-pages.md` ile aynı katmandadır (görev dosyası) — ikisi çelişmez, `07-pages.md` "hangi route, hangi state" der, bu dosya "o state'in içi ne" der. İş mantığı rakamları (`03-game-rules.md`) ve ödeme rakamları (`05-payment.md`) bu dosyada **tekrar yazılmaz**, yalnızca referans verilir — tek kaynak ilkesi bozulmaz.
>
> **Bu dosyanın kapsamadığı şey:** Hukuki metinler (`/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`) — bu dosyadaki içerik prensipleri o sayfaların **iskeletine** (başlık, bölüm sırası) uygulanır ama gövde metnini Claude yazmaz (bkz. `07-pages.md`, `01-workflow-rules.md` Bölüm 0.4 istisnası).

---

## 0. Neden bu dosya gerekli oldu — kök neden

State.io'dan esinlenen bir arayüz kopyalanırken genelde iki hata birlikte oluşur:

1. **Boş hissi:** Sayfa yalnızca bir aksiyon (buton, form) içerir, kullanıcının o aksiyonu **neden** vereceğine dair hiçbir bağlam yoktur. Ör. çıplak bir "Katıl" butonu — ne kazanacağını, riskin ne olduğunu göstermez.
2. **Gürültü hissi:** Sayfa, o anki karar için gerekmeyen bilgiyle doldurulur. Ör. lobi ekranında oyunun tüm kurallarının yeniden anlatılması, ya da her sayfada tekrar eden uzun bir "WinToWar nedir" paragrafı.

Bu iki hata da aynı kökten gelir: **"bu sayfa hangi tek karar için var?"** sorusu net cevaplanmadan içerik eklenmiştir. Bu dosyadaki her sayfa bloğu önce o soruyla başlar (**Sayfanın Tek İşi**), sonra o işe hizmet eden minimum içerik listelenir.

---

## 1. Genel İçerik Prensipleri (tüm sayfalar için)

### 1.1 "Sayfanın Tek İşi" kuralı 🛠️

Her sayfanın kullanıcıya sorduğu/beklediği **tek bir birincil karar** vardır (bir CTA). Aşağıdaki sayfa bloklarının her biri bu kararla başlar. Bir sayfada birden fazla eşit ağırlıkta CTA varsa (ör. "Katıl" ve "Oda Kur" aynı görsel ağırlıkta yan yana), bu bir tasarım hatasıdır — biri birincil (dolu buton, `04-style.md` Buton Hiyerarşisi), diğerleri ikincil/metin link olmalıdır.

### 1.2 Üç katmanlı bilgi modeli 🛠️

Her sayfa içeriği üç katmana ayrılır, bu sıra korunur:

1. **Karar için zorunlu bilgi** — kullanıcı bu olmadan CTA'ya basamaz/basmamalı (ör. giriş ücreti, mevcut bakiye).
2. **Kararı kolaylaştıran bağlam** — zorunlu değil ama tereddüdü azaltır (ör. "kaç oyuncu bekleniyor", örnek kazanç formülü).
3. **İkincil/keşif bilgisi** — sayfanın asıl işiyle ilgisiz ama başka bir sayfaya yönlendirme (ör. footer linkleri, "Kurallara göz at").

Katman 3 hiçbir zaman Katman 1'den önce, hiçbir zaman Katman 1'le aynı görsel ağırlıkta gösterilmez. Bir sayfada Katman 2 üç maddeden fazla büyürse, o içerik ayrı bir sayfaya (`/kurallar` gibi) taşınır — bu, "boş hissi"nin tam tersi olan "her şeyi tek sayfaya sıkıştırma" hatasını önler.

### 1.3 "Gereksiz bilgi" testi — somut kriter 🛠️

Bir metin/bileşen bir sayfaya eklenmeden önce şu soru sorulur: **"Kullanıcı bu bilgi olmadan bir sonraki adımı atamaz mı, ya da yanlış bir beklentiyle atar mı?"** Cevap hayırsa (kullanıcı zaten biliyor, başka sayfada zaten gördü, ya da sayfanın işiyle ilgisiz), o içerik eklenmez. Somut yasak örnekler:

- Her sayfanın başında oyunun ne olduğunu yeniden anlatan bir paragraf (bu yalnızca `/`'de, bir kez vardır).
- Zaten `/kurallar`'da anlatılan üretim/savaş formüllerinin başka sayfalarda (ör. lobi) yeniden özetlenmesi — bunun yerine tek satırlık bir "Kurallara göz at" linki yeterlidir.
- Kullanıcının o an değiştiremeyeceği/etkileyemeyeceği bir bilgi (ör. `/lobi`'de "Standart oda 4 kişilik olarak sabitlenmiştir, ileride değişebilir" gibi iç gerekçe notları — bunlar `03-game-rules.md`'nin ❓ notlarıdır, **kullanıcıya asla gösterilmez**, yalnızca dokümantasyon içindir).
- Aynı sayıyı/durumu birden fazla bileşende tekrar eden dolgu metni (ör. hem başlıkta hem alt başlıkta "Ödeme Bekleniyor" yazması).

### 1.4 "Boş hissi" testi — somut kriter 🛠️

Bir sayfa/state için karşıt soru: **"Bu ekranda kullanıcının kendisiyle ilgili, o anki durumunu yansıtan somut bir veri var mı, yoksa yalnızca jenerik bir metin mi var?"** Boş hisseden state'ler genelde jenerik metin + tek butondur. Kural: mümkün olan her yerde jenerik metnin yanına **gerçek/canlı bir sayı** eklenir:

- "Odaya katıl" değil → "Odaya katıl · 3/4 oyuncu bekliyor".
- "Ödeme bekleniyor" değil → "Ödeme bekleniyor · 1/2 onay" (bkz. `07-pages.md` `/odeme/[invoiceId]`).
- "Bakiyeniz" değil → gerçek `Wallet.BalanceUsd` değeri, yüklenene kadar skeleton (asla "—" gibi anlamsız placeholder değil).
- Boş liste durumunda dahi (ör. hiç aktif oda yoksa) sade bir açıklama + tek aksiyon yeterlidir (`04-style.md` Empty State kuralı) — ama bu, "hiç veri gösterilmiyor" ile karıştırılmaz: liste boşsa bu ayrı bir durumdur, listenin kendisi eksik/yarım render edilmiş gibi görünmemelidir.

Bu madde `04-style.md` Bölüm 14 (Empty/Error/Loading States) ile çelişmez, onu **içerik seviyesinde** tamamlar — 04, "nasıl görünür" der (spinner, kısa metin), bu dosya "içine ne yazılır" der.

**Genel kural (tek cümle):** Aynı bilgi hem statik metin hem canlı/gerçek veri olarak gösterilebiliyorsa, canlı veri tercih edilir — "3 oyuncu bekleniyor" değil, kimlerin katıldığını gösteren `Ali / Mehmet / Can · 3/4` gibi somut bir liste (bkz. Bölüm 3.4 bekleme ekranı örneği).

### 1.6 Sayfa başına blok sınırı 🛠️

Header/Footer hariç, bir sayfada aynı anda görünen bağımsız içerik bloğu (kart, bölüm, form grubu) sayısı **mümkün olduğunca 6'yı geçmez**. Bu, Bölüm 1.2'deki üç katmanlı modelin somut bir üst sınırıdır — Katman 1+2+3 toplamı sürekli büyürse sayfa "her şeyi tek ekrana sıkıştırma" hatasına döner (Bölüm 0'daki "gürültü hissi"). Bu, `04-style.md` Bölüm 4'teki "bir panelde aynı anda en fazla 5-7 ana bilgi gösterilir" kuralıyla aynı ilkenin sayfa seviyesindeki karşılığıdır — 04, tek bir panelin **içindeki** yoğunluğu sınırlar, bu madde sayfanın **tamamındaki** bağımsız blok sayısını sınırlar; ikisi çelişmez, farklı granülaritede aynı disiplini uygular. Yeni bir blok ihtiyacı doğduğunda önce şu sırayla değerlendirilir: (1) mevcut bloklardan biri gerçekten hâlâ gerekli mi, kaldırılabilir mi; (2) yeni bilgi bir mevcut bloğun içine (ör. bir kartın alt satırı olarak) sığdırılabilir mi; (3) hiçbiri olmuyorsa, bu aslında sayfanın "Tek İşi"nin (Bölüm 1.1) genişlediğinin işaretidir ve yeni bir route değerlendirilir (`07-pages.md`'ye danışılarak). Blok sayısını aşan bir ekleme sessizce yapılmaz.

### 1.7 Hata içeriğinin yapısı 🛠️

`04-style.md` Bölüm 14 hata durumunun **nasıl** göründüğünü (kısa, aksiyona yönlendiren mesaj; teknik detay kullanıcıya gösterilmez, yalnızca loglanır) zaten tanımlıyor. Bu madde onun **içerik iskeletini** netleştirir — her hata mesajı şu üç parçadan oluşur, sırayla:

1. **Ne oldu** — kısa, teknik olmayan bir cümle ("İşlem tamamlanamadı").
2. **Tek çözüm/aksiyon** — kullanıcının yapabileceği tek bir şey ("Tekrar deneyin" / "Destek ile iletişime geçin").
3. **(Varsa) tek CTA butonu** — bu çözümü tetikleyen buton.

"Beklenmeyen hata. Kod: 500" gibi teknik/kod içeren metinler hiçbir zaman kullanıcıya gösterilmez (bkz. `04-style.md` Bölüm 14) — hata kodu yalnızca loglanır, gerekiyorsa `/destek` formunda arka planda referans olarak taşınır (bkz. Bölüm 3.16), ekranda görünmez.

### 1.8 Sistem mesajlarında aktif dil 🛠️

Sistem tarafından üretilen kısa bildirim metinleri (toast, onay mesajı, durum başlığı) edilgen/jenerik değil, **kullanıcıya yönelik ve aktif** dille yazılır — "Oda oluşturuldu" değil "VIP odanız hazır", "Bağlantı sağlandı" değil "Bağlantı yeniden kuruldu". Bu, `04-style.md`'deki "sade dashboard" hissiyle (Bölüm 1) tutarlıdır — Stripe/Linear tarzı ürünler durum bildirimini sistem merkezli değil kullanıcı merkezli kurar. Bu madde `04-style.md`'nin Metin Dili bölümüyle (Türkçe zorunluluğu) çelişmez, onu ton açısından tamamlar.

### 1.9 Tekrarlanan bileşenler tek yerden tanımlanır 🛠️

Header, footer, ve sayfa geneli uyarı bantları (ör. bakım modu, KYC uyarısı) `web/components/layout/` altında tek bileşendir (bkz. `02-architecture.md`), her sayfa kendi kopyasını yazmaz. Bu dosyadaki sayfa bloklarında Header/Footer tekrar listelenmez, yalnızca bir kez aşağıda (Bölüm 3) tanımlanır.

### 1.10 Sayfa başlığı (H1) standardı 🛠️

Her route **tek** bir H1 içerir, H1 sayfanın Bölüm 1.1'deki "Tek İşi"ni tek kelime/kısa öbekle söyler ("Lobi", "Cüzdan", "Profil", "Kurallar") — pazarlama üslubunda ikinci bir büyük başlık ("Bugün Kazan!" gibi) eklenmez. Kart/bölüm başlıkları H2/H3'tür, H1 ile aynı görsel ağırlıkta olamaz. İstisna: `/` Landing, ürünün ne olduğunu anlatan tek cümlelik alt başlığı H1'in hemen altında taşıyabilir (bkz. 3.1) — bu ayrı bir ikinci H1 değildir.

### 1.11 CTA isimlendirme standardı 🛠️

Birincil/ikincil tüm CTA buton metinleri **fiille başlar** ve somuttur: "Katıl", "Oda Kur", "Para Çek", "Tekrar Oyna", "Gönder". İsimleşmiş/soyut buton metinleri ("Katılım", "İşlem", "Başlatma") kullanılmaz — bir buton neye tıklandığını değil, tıklanınca ne olacağını söylemelidir.

### 1.12 Boş liste standardı 🛠️

`04-style.md` Bölüm 14'teki Empty State kuralının (kısa açıklama + tek CTA) tüm listelenebilir içerikler için ortak şablonu: **kısa açıklama → tek CTA**. Örnek: hiç VIP oda yoksa "Şu an açık VIP oda yok" + "Oda Kur" butonu. Aynı şablon `/gecmis` (boş geçmiş), `/destek` taleplerinin admin tarafı, ve `/admin` alt route'larındaki boş tablolar için de geçerlidir — her biri için ayrı bir boş-durum metni icat edilmez, yalnızca açıklama cümlesi ve varsa CTA değişir.

### 1.13 Sayfa giriş sırası (şablon) 🛠️

Header/Footer hariç, bir sayfanın ana içerik alanı yukarıdan aşağı şu sabit sırayı izler — bu, Bölüm 1.2'deki katman modelinin sayfa üzerindeki dikey karşılığıdır, sayfadan sayfaya rastgele değişmez:

`H1` → _(varsa)_ tek satırlık açıklama → Katman 1 blokları → Katman 2 blokları

Katman 3 (ör. "Kurallara göz at" linki) ayrı bir bölüm değildir, genelde Katman 2'nin içine gömülü tek bir satırdır (bkz. Bölüm 1.2).

### 1.14 Gerçek veri > tahmini veri > placeholder 🛠️

Bölüm 1.4'teki "canlı veri tercih edilir" ilkesinin veri **doğruluğu** boyutu ayrıca burada netleşir: gösterilen her sayı, sunucudan (API/SignalR/DB) gelen **gerçek** bir değer olmalıdır. Yaklaşık/yuvarlanmış/uydurma ifadeler ("100+ oyuncu", "yaklaşık $50", "ortalama kazanç") **hiçbir yerde kullanılmaz** — proje gerçek zamanlı bir altyapıya (SignalR/API/PostgreSQL) sahip olduğundan, her sayı zaten sorgulanabilir durumdadır; "yaklaşık" bir ifade kullanmak gerçek veriyi gizlemek anlamına gelir. Veri henüz yüklenmediyse skeleton gösterilir (Bölüm 1.4), asla tahmini bir sayı değil.

### 1.15 Terminoloji tutarlılığı 🛠️

Aynı kavram, projenin her yerinde **birebir aynı kelimeyle** anılır — bir işlem/eylem için sayfadan sayfaya farklı isim türetilmez (ör. bir yerde "Para Yatır", başka bir yerde "Bakiye Yükle"; bir yerde "Oda Kur", başka bir yerde "Yeni Oyun"). Bu, Bölüm 1.11 (CTA isimlendirme) ve 1.8'in (aktif dil) tek bir kelime seviyesindeki toplu karşılığıdır — o ikisi _nasıl_ yazılacağını (fiil, aktif ton), bu madde _hangi kelimenin_ kullanılacağını sabitler. Bu dosyada geçen ve projede tekrar eden temel terimler (referans, yeni bir terim türetilmeden kullanılır):

| Kavram                                           | Sabit terim      |
| ------------------------------------------------ | ---------------- |
| Bakiye artırma                                   | "Para Yatır"     |
| Bakiye azaltma / çıkış                           | "Para Çek"       |
| Bir odaya/kuyruğa girme                          | "Katıl"          |
| VIP oda oluşturma                                | "Oda Kur"        |
| Aksiyon gönderme (destek formu, VIP kurulum vb.) | "Gönder"         |
| Maç bitince yeni bir maça yönlenme               | "Tekrar Oyna"    |
| Maç bitince lobiye dönme                         | "Lobiye Dön"     |
| Bağlantı denemesini tetikleme                    | "Yeniden Bağlan" |

Yeni bir terime ihtiyaç doğarsa önce bu tablo genişletilir, sonra kullanılır — sayfa/bileşen içinde tek seferlik bir kelime icat edilmez (bkz. `04-style.md` Bölüm 2'deki design token tablosunun aynı disiplini).

---

## 2. Genel Navigasyon İçeriği (Header / Footer)

> ⚠️ **İstisna:** `/game/[matchId]` bu bölümün kapsamı dışındadır — `07-pages.md`'deki Navigasyon tablosu ve `GameLayout` tanımı gereği bu sayfada Header/Footer **hiç render edilmez**, yalnızca minimal bir bağlantı durumu göstergesi bulunur (bkz. Bölüm 3.8). Aşağıdaki 2.1/2.2 diğer tüm sayfalar için geçerlidir.

### 2.1 Header

- **Girişsiz kullanıcı:** Logo + "Giriş Yap" + "Kayıt Ol" (birincil buton kayıt, çünkü ürünün amacı yeni oyuncu kazanmaktır).
- **Girişli kullanıcı:** Logo + Bakiye özeti (`Wallet.BalanceUsd`, tıklanınca `/cuzdan`) + basit bir kullanıcı menüsü (Profil, Hesap Ayarları, Çıkış). Bakiyenin header'da her zaman görünür olması Katman 1 bilgisidir — kullanıcı her an "elimde ne var" bilmeden bir odaya girme kararı vermemelidir.
- Header'da oyun kuralları/asker sayıları gibi Katman 2/3 bilgisi **olmaz** — header yalnızca kimlik + bakiye + navigasyon taşır.

### 2.2 Footer

- Tek satır, sade: Kurallar · Kullanım Şartları · Gizlilik Politikası · Sorumlu Oyun · Destek — bu beş link `07-pages.md`'deki "Footer içeriği — kesinleştirildi" kararıyla birebir aynıdır, buraya farklı/eksik bir liste yazılmaz. (`/cerezler` ve `/sss` ayrı route'lar olarak var ama bu sabit footer listesinde değiller — `/cerezler`'e `/gizlilik` sayfası içinden, `/sss`'e ise ilgili sayfaların Katman 2/3'ünden bağlanılır, bkz. Bölüm 3.13/3.15.)
- Yasal metin dışında footer'a **hiçbir pazarlama/dolgu metni eklenmez** (ör. sosyal medya ikonları, "hakkımızda" paragrafı) — müşteri bunları hiç talep etmedi, ürün bir dashboard hissi vermeli (`04-style.md` Bölüm 1), pazarlama sitesi değil.

---

## 3. Sayfa Bazlı İçerik Blueprint'i

> Format: **Sayfanın Tek İşi** → **Zorunlu (Katman 1)** → **Bağlam (Katman 2)** → **Bu sayfada OLMAYACAK**. Rakamlar/kurallar burada tekrar üretilmez, kaynak dosyaya referans verilir.

### 3.1 `/` — Landing

**Tek işi:** Girişsiz bir ziyaretçiyi ya kayda ya da (girişliyse) lobiye götürmek.

- **Katman 1:** Tek cümlelik ne-olduğu tanımı ("gerçek zamanlı bölge ele geçirme oyunu, gerçek parayla") + tek birincil CTA (girişsiz → Kayıt Ol, girişli → Lobiye Git, bkz. `07-pages.md`).
- **Katman 2:** Kazanç formülü (`Havuz = Giriş Ücreti × Oyuncu Sayısı`, `Kazanç = Havuz × %90` — bkz. `07-pages.md` Landing notu, sabit tutar yazılmaz), 3 adımlık "nasıl oynanır" özeti (Katıl → Bölge Fethet → Kazan), tek bir örnek ekran görüntüsü/statik önizleme.
- **Bu sayfada OLMAYACAK:** Üretim/savaş formülleri (bunlar `/kurallar`'da), oda türleri detayı (Standart vs VIP farkları — bu `/lobi`'de zaten görselleşiyor, Landing'de metinle tekrar anlatılmaz), fiyatlandırma tablosu (giriş ücreti tek bir örnekle geçilir, VIP'nin serbest fiyatı Landing'in konusu değil).

### 3.2 `/giris`, `/kayit`

**Tek işi:** Kimlik doğrulama/oluşturma — başka hiçbir şey.

- **Katman 1:** Form alanları + tek CTA buton. `/kayit`'te 18 yaş + KVKK/Şartlar onay kutusu (bkz. `07-pages.md`).
- **Katman 2:** Diğer forma geçiş linki ("Hesabın yok mu? Kayıt Ol"), `/giris`'te "Şifremi Unuttum" linki.
- **Bu sayfada OLMAYACAK:** Oyun tanıtımı, kazanç örnekleri, herhangi bir pazarlama metni — bu sayfalar tamamen fonksiyoneldir, kullanıcı zaten `/`'den ikna olmuş buraya gelmiştir, aynı ikna metnini tekrarlamak Katman 3 gürültüsüdür.

### 3.3 `/sifremi-unuttum`, `/sifre-sifirla/[token]`

**Tek işi:** Şifre kurtarma — tek adım, tek form.

- **Katman 1:** E-posta alanı (ilk sayfa) / yeni şifre + tekrar alanı (ikinci sayfa).
- **Bu sayfada OLMAYACAK:** Herhangi bir ek bağlam; bu akış olabildiğince kısa olmalı, kullanıcı zaten stres altında (şifresini unutmuş) bir durumda ekstra okuma yükü istemez.

### 3.4 `/lobi`

**Tek işi:** Kullanıcıyı bir maça sokmak (Practice / Standart / VIP).

- **Katman 1:** Üstte "Pratik Oyna" birincil aksiyonu (bkz. `07-pages.md` — tek tık, kuyruk), altında Standart/VIP sekmeleri, her oda satırında `04-style.md` Bölüm 6'daki **Standart Liste** şablonu uygulanır: sol tarafta oda kimliği + oyuncu sayısı, sağ tarafta giriş ücreti rozeti + tek "Katıl" butonu.
- **Oda kimliği içeriği 🛠️ — netleştirildi (gerçek bir boşluk dolduruldu):** `03-game-rules.md` Bölüm 2.2'deki VIP oda kurma alanları arasında ayrı bir "oda adı" alanı **yok**, ama `04-style.md`'nin Standart Liste şablonu satırın solunda bir kimlik metni bekliyor. Bu iki dosya arasındaki boşluk şöyle kapatılır: oda kimliği kurucunun kullanıcı adından türetilir (ör. "Ali'nin Odası") — yeni bir form alanı **eklenmez** (`03-game-rules.md`'nin verdiği alan listesi genişletilmez), yalnızca mevcut `Room.CreatedByUserId`'den bir görüntüleme metni üretilir.
- **Katman 2:** Sekme başlıklarının yanında tek satırlık ayrım ("Standart: sabit $1, hızlı eşleşme" / "VIP: kendi kuralını belirle") — kullanıcı sekmeler arasında neden geçeceğini anlamalı, ama bu VIP'nin tüm ayarlarının burada yeniden anlatılması demek değildir.
- **Bu sayfada OLMAYACAK:** Oda kurma formunun kendisi (ayrı route, `/lobi/vip-olustur`), üretim/savaş kuralları, ödeme akışının detaylı açıklaması (kullanıcı "Katıl"a basınca zaten `/odeme/[invoiceId]`'e yönlenip orada görür — burada önceden anlatılmaz).

**Bekleme (dolum) durumunun içeriği 🔔 — konum düzeltildi (docs/09-eksik-tarama.md denetimi, Faz 6):** Bu madde önceden bekleme ekranının `/lobi`'de kaldığını varsayıyordu. Gerçek kod, "Katıl" sonrası oyuncuyu **doğrudan `/game/[matchId]`'e** yönlendirir (bkz. `07-pages.md` "Route Geçiş Akışı" 🔔 notu) — aşağıdaki içerik listesi hâlâ geçerlidir, ama artık `/lobi`'nin değil, `/game/[matchId]`'in `Lobby`/`Countdown` state'inin içeriğidir (bkz. Bölüm 3.8 "Katman 1 — üç ayrı yüzey"). Bu, ayrı bir route değildir, Waiting Room için yeni bir sayfa/route açılmamıştır (YAGNI korunmuştur, yalnızca hangi mevcut route'un içeriği olduğu değişmiştir). İçerik:

- Katılan oyuncuların kullanıcı adı listesi (`X/N`, dolan slotlar isimle, boş slotlar "Bekleniyor…" ile) — Bölüm 1.4/1.14'ün doğrudan uygulaması.
- Son 1 slot kaldığında tek satırlık vurgu ("Son oyuncu bekleniyor").
- Oda dolduğunda (bkz. `03-game-rules.md` Bölüm 7/10) sabit bir "geri sayım" mekaniği (3-2-1) müşteri tarafından tanımlanmadı, burada da icat edilmez (YAGNI). Yerine, sunucu `Countdown` state'ine geçtiğinde gerçek `countdownRemainingSeconds` sayacı gösterilir (bkz. Bölüm 3.8) — bu, dokümanın öngördüğü "kısa geçiş metni"nden daha bilgilendirici, gerçek veriye dayalı bir çözümdür (Bölüm 1.4 "gerçek veri tercih edilir" ilkesiyle tutarlı). ❓ Görsel bir geri sayım (3-2-1 animasyonu) istenirse müşteriye doğrulatılmalı.
- 5 dakikalık dolum süresi dolarsa gösterilen "İptal Et / Beklemeye Devam Et" modalı zaten `07-pages.md`'de tanımlı — burada tekrar edilmez.

### 3.5 `/lobi/vip-olustur`

**Tek işi:** VIP oda parametrelerini toplayıp odayı kurmak (ki bu aynı zamanda kurucunun kendi katılımı ve ödemesidir — bkz. `03-game-rules.md` Bölüm 2.2).

- **Katman 1:** Form alanları (gri bölge savunması 1-7, görüş modu, giriş ücreti, oyuncu sayısı 2-12, opsiyonel parola) + tek "Oda Kur ve Katıl" butonu — buton metni **açıkça** ödemenin de bu adımda gerçekleştiğini belirtir, kullanıcı "kur" ile "öde"nin ayrı adımlar olduğunu sanmamalı (bkz. `03-game-rules.md` Bölüm 2.2 kurucu-katılım kuralı — içerik bu iş kuralını gizlemez, aksine öne çıkarır).
- **Katman 2:** Her alanın yanında tek satırlık açıklama (ör. gri bölge savunması slider'ının yanında "Yüksek değer = daha zor fetih"), toplam giriş maliyetinin canlı önizlemesi (bakiyeden düşülecek/eksik kalacak tutar, form doldukça güncellenir).
- **Katman 2 — canlı havuz önizlemesi 🛠️ eklendi:** Giriş ücreti ve oyuncu sayısı alanları değiştikçe, `/` Landing'de zaten tanımlı olan formülle (`Havuz = Giriş Ücreti × Oyuncu Sayısı`, `Kazanç = Havuz × %90`, bkz. `05-payment.md` `CommissionRate=0.10`) **3 satırlık** bir önizleme güncellenir: Toplam Havuz / Komisyon (%10) / Kazanana Düşen. Bu, kurucunun kurduğu odanın ekonomik sonucunu form doldururken görmesini sağlar — kararı kolaylaştıran bağlam (Katman 2), zorunlu değil ama VIP'nin serbest fiyatlandırması nedeniyle özellikle burada değer katar. Rakamlar burada **yeniden tanımlanmaz**, yalnızca `05-payment.md`'deki formülün canlı hesaplanmış hâlidir — tek kaynak ilkesi bozulmaz.
- **Bu sayfada OLMAYACAK:** Diğer oyuncuların nasıl davet edileceğine dair uzun bir anlatım (oda kurulduktan sonra link zaten üretilir ve gösterilir, kurulmadan önce anlatılmaz — kullanıcı henüz orada değil).

### 3.6 `/lobi/[inviteToken]`

**Tek işi:** Davet linkiyle gelen kullanıcıyı parola sonrası bekleme ekranına sokmak.

- **Katman 1:** Parola giriş alanı (token geçerliyse) + tek CTA; token geçersizse tek açıklayıcı Empty state.
- **Bu sayfada OLMAYACAK:** Oda kurma formu, oda ayarlarının listesi (parola girilmeden oda detayları gösterilmez — bu hem gereksiz bilgi hem de "özel davet" ilkesine aykırı bir bilgi sızıntısıdır).

### 3.7 `/odeme/[invoiceId]`

**Tek işi:** Kullanıcıyı ödeme durumu hakkında bilgilendirmek, başka hiçbir aksiyon beklenmez.

- **Katman 1:** Ödenecek tutar, LTC adresi/QR, canlı onay ilerlemesi ("1/2 onay" — bkz. `07-pages.md`), durum (Bekleniyor/Onaylandı/Süresi Doldu).
- **Katman 2:** Bu ödemenin ne için olduğu (top-up mı, maça giriş mi — tek cümle), onaylandıktan sonra ne olacağının tek cümlelik önizlemesi ("Onaylanınca otomatik olarak lobiye eklenirsiniz").
- **Bu sayfada OLMAYACAK:** Komisyon oranı/oyun kuralları anlatımı, başka ödeme yöntemi seçenekleri (LTC dışında yöntem yok, bunun için ayrı bir seçim ekranı gösterilmez).

### 3.8 `/game/[matchId]`

**Tek işi:** Maçın kendisi — bu sayfa zaten uygulanmış, bu dosya yalnızca HUD/panel metninin yoğunluğuna dair prensip ekler.

> ⚠️ **Terminoloji uyarısı:** WinToWar'da **Altın (para birimi), General ve Upgrade/Yuva-seviyesi kavramları yoktur** — bunlar projenin geçersiz kılınmış eski konsepti "Porsuk Savaşları"na aitti. Bu düzeltme aslında yeni değil: `04-style.md` Bölüm 10 zaten "önceki 'altın, general sayısı, yuva/kale seviyesi' alanları WinToWar'da karşılığı olmadığı için kaldırılmıştır" diyor (bkz. `03-game-rules.md` Bölüm 4/5) — aşağıdaki içerik yalnızca o kararın metin karşılığıdır, yeni bir kısıt getirmez.

- **Katman 1 — üç ayrı yüzey, `04-style.md` Bölüm 10'daki yapıyla birebir:** İçerik bu üç yüzeye **doğru dağıtılmalı**, tek bir "HUD" listesine karıştırılmamalı:
  - **Üst HUD çubuğu (her zaman sabit):** asker sayısı, bölge sayısı, üretim hızı (`03-game-rules.md` Bölüm 4 formülüyle canlı), süre/maç durumu. 🛠️ **Düzeltme — "Ana Kale'deki mevcut havuz" ifadesi kaldırıldı:** Bu madde önceden asker sayısını "Ana Kale'deki mevcut havuz" olarak tarif ediyordu; bu, `03-game-rules.md` Bölüm 4'teki DÜZELTME'den (tek-kaynaklı Ana Kale modelinin çok-kaynaklı/bölge-bazlı üretime geçmesi — artık tek bir merkezi "havuz" yok, her bölge kendi askerini kendi biriktiriyor) önceki bir tanımdı ve güncellenmemişti. Üst HUD'daki "asker sayısı" artık oyuncunun **sahip olduğu tüm bölgelerdeki asker sayılarının toplamı**, "üretim hızı" da bu bölgelerin **toplam üretim hızı**dır (`Σ bölge üretimi`, her biri `10 sn'de N asker` formülüyle) — seçili tek bir bölgenin değeri değil, genel durum özeti. Seçili bölgeye özgü değerler zaten ayrı olarak ActionPanel'de gösterilir (aşağıdaki madde). `MaxAccumulatedTroops` tavanına (bkz. `03-game-rules.md` Bölüm 4) yaklaşan bir bölge varsa (bkz. `03-game-rules.md` Bölüm 4) "asker sayısı" alanının yanında tek satırlık bir uyarı ("Kapasiteye yaklaşıyor") — bu yeni bir alan değil, mevcut alanın bir alt-durumudur.
  - **ActionPanel (salt-okunur bilgi paneli, artık aksiyon almaz — bkz. `04-style.md` Bölüm 10):** Seçili bölgenin mevcut asker sayısı, üretim hızı, (VIP açık haritada) komşu bölgelerin savunma/asker sayısı.
  - **Harita üzerinde bölge rozeti (her zaman görünür, ayrı tıklama gerekmez):** O bölgenin o anki asker sayısı — Fog of War açıkken yalnızca görünür bölgelerde (bkz. `04-style.md` Bölüm 10 Fog of War maddesi).
- **Elenen oyuncu ekranı 🛠️ eklendi (mevcut kuralın içerik karşılığı, bkz. `03-game-rules.md` Bölüm 9):** Bu bir spectator modu **değildir** — oyuncu kendi maçının sonucunu izlemeye devam eder. İçerik: "Elendin" başlığı + salt-okunur harita (ActionPanel artık bilgi amaçlı kalsa da, sürükle-bırak saldırı etkileşimi bu oyuncu için tamamen kapatılır) + maçın kalan durumunu takip edebileceğine dair tek satırlık not. Genel/üçüncü şahıs izleyici modu bu projede **bulunmaz** (bkz. `03-game-rules.md` Bölüm 11) — bu sayfa hiçbir zaman "başka bir maçı izle" gibi bir aksiyon içermez, admin'in ayrı ve hâlâ ❓ onay bekleyen izleyici erişimiyle karıştırılmaz (bkz. `07-pages.md`).
- **Bağlantı durumu içeriği 🛠️ eklendi (state'lerin kendisi zaten `07-pages.md`'de tanımlı, burada içeriği netleşiyor):**
  - `Reconnecting`: küçük, sayfayı bloklamayan bir bant — "Bağlantı kesildi, yeniden bağlanılıyor…". Maç sunucu-otoriter olduğu için (`02-architecture.md`) bu state harita/HUD'u gizlemez, üzerine hafif bir gösterge eklenir.
  - `Disconnected` (deneme tükendi): tam ekran değil ama net bir uyarı + "Yeniden Bağlan" butonu — "Maçınız sunucuda devam ediyor, bağlantınızı yeniden kurun."
  - Bu iki state için de oyun kuralı/ikna metni **eklenmez** (Bölüm 1.3) — yalnızca bağlantı durumu ve tek bir aksiyon.
- **Katman 1 (Finished state) — kazanma/kaybetme ayrımı 🛠️ genişletildi:** Bu, ayrı bir route değildir (`07-pages.md`'deki karar korunur — "ekstra navigasyon karmaşası yaratmaz"), ama içerik kazanan/kaybeden için ayrışır, tek bir nötr "Maç Bitti" metni yeterli değildir:
  - **Kazanan:** "Kazandın" + net kazanç tutarı (brüt ödül − %10 komisyon, bkz. `05-payment.md`) + LTC transfer durumu ("Transfer hazırlanıyor" / "Bakiyenize eklendi").
  - **Kaybeden/diğer oyuncular:** "Maç Bitti" (kaybeden için "Kazandın" tonuyla çelişmeyen nötr bir başlık — "Kaybettin" gibi doğrudan olumsuz bir vurgu yerine, oyunun "sade/dashboard" hissiyle tutarlı sakin bir bilgilendirme, bkz. `04-style.md` Bölüm 1) + kazananın kim olduğu.
  - Her iki durumda da aynı iki ikincil aksiyon: "Tekrar Oyna" (→ `/lobi`) ve "Lobiye Dön".
- **Maç sonu ek istatistikler (ör. en çok bölge, en uzun savunma, toplam üretilen asker) — ❓ kapsam dışı, opsiyonel:** Tekrar oynanabilirliği artırabilecek bir fikir ama müşteri tarafından hiç talep edilmedi ve mevcut veri modelinde (`MatchEventLog`) bu tür özet istatistiklerin ayrıca hesaplanıp saklanması gerekir — bu **yeni bir veri/hesaplama işi**dir, yalnızca içerik kararı değildir. Bu yüzden ilk sürüm kapsamına **eklenmez** (YAGNI, `01-workflow-rules.md` Bölüm 0.10); istenirse ayrı bir görev olarak `02-architecture.md`/`03-game-rules.md`'ye önce veri modeli tarafında eklenmesi gerekir, bu dosya yalnızca o zaman içerik bloğu ekler.
- **Bu sayfada OLMAYACAK:** Oyun kuralı hatırlatmaları (bir HUD tooltip'i formül anlatmaz, `/kurallar`'a link yeterlidir), reklam/promosyon içeriği, genel/üçüncü şahıs izleyici (spectator) girişi.

### 3.9 `/cuzdan`

**Tek işi:** Bakiye görmek, para yatırmak/çekmek.

- **Katman 1:** Güncel bakiye (büyük, net), "Para Yatır" (Primary) ve "Para Çek" (Secondary) CTA'ları. **Düzeltme:** Önceki taslakta bu ikisi "eşit ağırlıklı" olarak tanımlanmıştı — bu, `04-style.md` Bölüm 5'teki "Primary: sayfada/ekranda en fazla 1 tane" kuralıyla doğrudan çelişiyordu, düzeltildi. Para Yatır primary seçildi çünkü ürünün öncelik sırası "çalışan/aktif oyuncu tabanı"dır (bkz. Header'daki kayıt-önceliği gerekçesi, Bölüm 2.1) ve bir kullanıcının oyuna devam edebilmesi bakiyeye bağlıdır; Para Çek secondary'dir, aynı satırda ama daha düşük görsel ağırlıkla durur. Bu 🛠️ bir tasarım tercihidir, müşteriden birebir gelen bir karar değildir.
- **Katman 1 — "Bekleyen Transferler" kartı 🛠️ eklendi:** `WithdrawalRequest.Status` henüz `Completed` olmayan (bkz. `05-payment.md` state modeli) çekim talepleri, bakiyenin hemen altında ayrı bir kart olarak listelenir (durum + tutar + oluşturulma zamanı). Bu Katman 1'dir, Katman 2 değildir — LTC transferleri anlık onaylanmadığından "param nerede" belirsizliği (bkz. Bölüm 1.4, `/odeme/[invoiceId]`'deki aynı gerekçe) burada da geçerlidir.
- **Katman 2:** Son işlemlerin kısa özeti — 🛠️ ilk sürümde son **5** kayıt (geçici varsayım, tek satırlık config; tam geçmiş `/gecmis`'te).
- **Bu sayfada OLMAYACAK:** Komisyon hesap detayları/formülleri (yalnızca `/kurallar`'da), maç geçmişi (o `/gecmis`'in işi).

### 3.10 `/profil`, `/gecmis`

**Tek işi:** Geçmişe bakmak — salt okunur.

- **Katman 1:** Kullanıcı bilgisi (profil), maç/ödeme geçmişi tablosu (gecmis).
- **Bu sayfada OLMAYACAK:** Herhangi bir yazma aksiyonu (şifre değiştirme vb. → `/hesap-ayarlari`, bkz. `07-pages.md` ayrımı) — bu sayfaların salt-okunur kalması, kullanıcının "bir şey bozar mıyım" endişesi taşımadan geçmişine bakabilmesi içindir.

### 3.11 `/hesap-ayarlari`

**Tek işi:** Hesapla ilgili yazma işlemleri.

- **Katman 1:** E-posta/şifre değiştirme formu, "Hesabımı Sil" aksiyonu (onay modalı ile, bkz. `07-pages.md`).
- **Bu sayfada OLMAYACAK:** Geçmiş/istatistik verisi (bu `/profil`'in işi, tekrar edilmez).

### 3.12 `/kurallar`

**Tek işi:** Oyunun tüm kural/formül detaylarının **tek** kaynağı — diğer hiçbir sayfa bunu tekrar etmez, bu yüzden bu sayfanın kapsamlı olması "gürültü" sayılmaz.

- **Katman 1:** Üretim formülü, savaş mantığı, Standart/VIP farkları, giriş ücreti/kazanç dağılımı — `03-game-rules.md`'deki rakamlarla birebir (bkz. `07-pages.md`).
- **Katman 2:** Alt kısımda interaktif "Dene" bölümü (bkz. `07-pages.md`, `03-game-rules.md` Bölüm 7).
- Diğer tüm sayfalardaki "kural hatırlatma" ihtiyacı bu sayfaya **link** ile çözülür, metin kopyalanarak değil (bkz. Bölüm 1.3).

### 3.13 `/sss`

**Tek işi:** Tekrar eden pratik soruları tek yerde cevaplamak, `/destek`'e düşecek bilet sayısını azaltmak (bkz. `07-pages.md`).

- **Katman 1:** Soru/cevap çiftleri (LTC yatırma/çekim süresi, komisyon nasıl hesaplanır, maç iptal olursa ne olur vb.) — `/kosullar` ile aynı statik şablon.
- **Bu sayfada OLMAYACAK:** Oyun kural detayları (bu `/kurallar`'ın işi, tekrar edilmez), destek formu (bu `/destek`'in işi — sayfanın sonunda yalnızca "Sorunun cevabını bulamadın mı?" tarzı tek satırlık bir link yeterlidir, form burada tekrar kurulmaz).

### 3.14 `/mac/[matchId]`

**Tek işi:** Biten bir maçın kalıcı, salt-okunur özet kaydını göstermek — `/game/[matchId]`'in **canlı** oynanış sayfasıyla karıştırılmaz, bu ayrı ve statik bir kayıt sayfasıdır (bkz. `07-pages.md`, State Matrisi'nde bu sayfa için Connecting/Reconnecting yoktur — SignalR bağlantısı gerektirmez).

- **Katman 1:** Son durum haritası (hamle hamle replay **değil** — `07-pages.md`'deki Non-Goals kararı korunur), kazanan, süre, net ödül.
- **Katman 2:** "İtiraz Et" linki (`/destek?matchId=...` — bkz. Bölüm 3.16) — bu sayfanın `07-pages.md`'de tanımlanan asıl var oluş nedenlerinden biridir, itiraz eden kullanıcı önce kendi maçının kaydını burada görür.
- **Bu sayfada OLMAYACAK:** Hamle/aksiyon geçmişinin dökümü, istatistik/performans analizi (`07-pages.md`'nin açık Non-Goals kararı — "ayrı bir istatistik/performans sayfası değildir"), canlı bağlantı göstergeleri (bu sayfa SignalR'a bağlı değildir).

### 3.15 `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`

**Tek işi:** Yasal bilgilendirme — içerik hukuktan gelir, bu dosya yalnızca iskeleti tanımlar.

- **Katman 1:** Başlık + tek sütun statik metin alanı (yer tutucu, bkz. `07-pages.md` placeholder istisnası).
- **Bu sayfada OLMAYACAK:** Herhangi bir CTA, form, pazarlama unsuru — bu sayfalar tamamen bilgilendirme amaçlıdır.

### 3.16 `/destek`

**Tek işi:** Tek bir destek talebi oluşturmak.

- **Katman 1:** Konu, açıklama, opsiyonel maç/işlem ID alanı (query param'dan otomatik dolabilir, bkz. `07-pages.md`), tek "Gönder" butonu.
- **Katman 2:** Maç itirazında `?matchId=...` ile açıldıysa, üstte ilgili maçın `/mac/[matchId]` kaydına tek satırlık bir link (bkz. Bölüm 3.14) — kullanıcı formu doldurmadan önce kendi maçının kaydını tekrar görebilir.
- **Bu sayfada OLMAYACAK:** SSS listesi (bu `/sss`'in işi, karıştırılmaz), canlı sohbet gibi müşteri hiç talep etmemiş bir kanal.

### 3.17 `/admin`, `/admin/odemeler`, `/admin/maclar`, `/admin/kullanicilar`, `/admin/destek`, `/admin/loglar`

**Tek işi:** Operasyonel görünürlük — burada "boş hissi" testi tersine döner: bu ekranlar için asıl risk **fazla dekoratif** olmalarıdır, saf veri yoğunluğu burada bir kusur değil, amaçtır.

- **Katman 1:** `/admin` özet metrikleri (bekleyen çekim sayısı, aktif maç sayısı, günlük hacim); alt route'larda ilgili tablo + satır bazlı aksiyon butonları (`/admin/odemeler`, `/admin/maclar`, `/admin/kullanicilar`, `/admin/destek`, `/admin/loglar` — her biri kendi tek işine sahiptir, bkz. `07-pages.md`).
- **Bu sayfada OLMAYACAK:** Landing/lobi tarzı açıklayıcı/ikna edici metin — admin kullanıcı zaten ne yaptığını biliyor, ona "nasıl kullanılır" anlatılmaz.

### 3.18 `/durum`

**Tek işi:** Kritik altyapı bileşenlerinin (BTCPay, SignalR, API) o anki durumunu göstermek — güven inşa eden basit bir sağlık göstergesi (bkz. `07-pages.md`).

- **Katman 1:** Her bileşen için tek satır: bileşen adı + yeşil/kırmızı durum (Çalışıyor/Kesinti) — teknik detay/log içeriği gösterilmez (bu Bölüm 1.7'deki "teknik detay kullanıcıya gösterilmez" ilkesiyle aynı gerekçe).
- **Bu sayfada OLMAYACAK:** Geçmiş kesinti kayıtları, ayrıntılı monitoring grafiği — basit bir monitoring sistemi kurulmaz (YAGNI, bkz. `07-pages.md`), yalnızca o anki durum gösterilir.

### 3.19 `/bakim`

**Tek işi:** Sistem planlı/plansız kapalıyken auth'lu her route'un yönlendiği tek ekran (bkz. `07-pages.md`).

- **Katman 1:** Kısa açıklama ("Sistem bakımda, kısa süre içinde geri döneceğiz") — bu bir **Error** değildir, ayrı ve sakin bir tondadır (Bölüm 1.7'deki hata yapısıyla karıştırılmaz: burada "neden/çözüm/CTA" üçlüsü yoktur, çünkü kullanıcının yapabileceği bir aksiyon yoktur).
- **Bu sayfada OLMAYACAK:** Herhangi bir CTA/form — kullanıcı bekletilir, yönlendirilmez.

---

### 3.20 Lobi Filtreleri — ileriye dönük not 🛠️ (opsiyonel, şimdilik uygulanmaz)

Müşteri bir filtre istemedi ve oda sayısı azken (launch sonrası ilk dönem) bir filtreye gerek yoktur (YAGNI, bkz. `01-workflow-rules.md` Bölüm 0.10). VIP oda sayısı gerçekten çoğaldığında `/lobi` VIP sekmesine **oyuncu sayısı** bazlı bir filtre (2/4/8/12) eklenebilir. **Şifreli/şifresiz filtresi eklenmez** — şifreli odalar zaten herkese açık listede hiç görünmüyor (bkz. `03-game-rules.md` Bölüm 2.2 "DÜZELTME"), yani listede zaten yalnızca şifresiz odalar var; onları ayrıca "şifresiz" diye filtrelemenin bir anlamı yok.

### 3.21 Mobil İçerik Önceliği 🛠️ (eklendi)

Proje mobil tarayıcıda da oynanacağından (`03-game-rules.md` Bölüm 15-A "mobil-öncelikli arayüz"), dar ekranda HUD'un hangi sırayla daraltılacağı bir içerik kararıdır, yalnızca bir CSS/layout kararı değildir — hangi bilginin "kaybolabilir" (Katman 2/3) hangisinin "asla kaybolamaz" (Katman 1) olduğu bu dosyanın Bölüm 1.2'sinden türer:

1. **Asla gizlenmez:** Seçili bölgenin asker sayısı, toplam üretim hızı (🛠️ düzeltme — "Ana Kale üretim hızı" değil; bkz. Bölüm 1.2/3.10'daki güncel HUD tanımı, çok-kaynaklı ekonomide tek bir merkezi üretim değeri yoktur), toplam bölge sayısı, birincil CTA'lar (sürükle-bırak saldırı etkileşiminin kendisi), bakiye (header'da).
2. **Önce gizlenir/daraltılır:** Yardımcı açıklama metinleri, ikincil istatistikler (ör. kalan rakip sayısının detaylı dökümü tek bir sayıya iner), tooltip'ler.
3. **Ekran genişliğine göre bileşene dönüşür, kaybolmaz:** Örn. oyuncu listesi (Bölüm 3.4 bekleme ekranı) dar ekranda accordion/scroll içine alınabilir ama tamamen kaldırılmaz.

Bu sıralama `04-style.md`'nin görsel/responsive kararlarıyla çelişmez, onu içerik önceliği açısından tamamlar — `04-style.md` "nasıl daraltılır" (spacing, breakpoint) der, bu madde "hangi bilgi önce feda edilir" der.

## 4. Uygulama Notu

Bu dosya, `07-pages.md`'deki route tablosunu **genişletmez**, yeni bir route eklemez — yalnızca zaten var olan her route'un içindeki metin/bilgi yoğunluğuna karar verir. Yeni bir sayfa/route ihtiyacı doğarsa önce `07-pages.md` güncellenir, bu dosyaya o zaman karşılık gelen bir blok eklenir.

## 5. Yeni Sayfa Kontrol Listesi

Yeni bir sayfa/state eklenmeden önce, bu dosyanın prensiplerinin uygulandığından emin olmak için:

- [ ] Sayfanın Tek İşi (Bölüm 1.1) tek cümleyle tanımlandı mı?
- [ ] Katman 1 (zorunlu bilgi) net mi, kararı etkilemeyen hiçbir şey içermiyor mu?
- [ ] Katman 2 üç maddeyi geçiyor mu — geçiyorsa ayrı bir sayfaya taşınmalı mı (Bölüm 1.2)?
- [ ] "Gereksiz bilgi" testi (Bölüm 1.3) geçildi mi — her madde gerçekten gerekli mi?
- [ ] "Boş hissi" testi (Bölüm 1.4) geçildi mi — jenerik metin yerine gerçek veri mi kullanıldı?
- [ ] Aynı bilgi/terim başka bir sayfada zaten var mı (Bölüm 1.3, 1.15) — varsa link ile mi çözüldü, tekrar mı ediliyor?
- [ ] Header/Footer hariç blok sayısı 6'yı geçiyor mu (Bölüm 1.6)?
- [ ] Yeni bir CTA/terim gerekiyorsa Bölüm 1.11/1.15'teki tabloya eklendi mi, yoksa tek seferlik bir kelime mi icat edildi?
- [ ] Bu gerçekten yeni bir sayfa mı, yoksa mevcut bir route'un yeni bir state'i mi (Bölüm 4 — `07-pages.md` önce güncellenmeden yeni route açılmaz)?

## ❓ Müşteriden Doğrulanması Gereken Noktalar

- Landing'deki "nasıl oynanır" 3 adımının statik metin mi yoksa küçük bir animasyon/görsel mi olacağı belirtilmedi — 🛠️ ilk sürümde statik, `04-style.md`'deki "gereksiz animasyon yasağı" ile tutarlı bir varsayım.
- Oda dolduktan sonra maç başlamadan önce sabit görsel bir geri sayım (3-2-1) istenip istenmediği — bu dosyada kısa bir geçiş metniyle ("Oda doldu, maç başlıyor…") ilerlendi, sabit sayaç bir mekanik eklemesi olurdu (bkz. 3.4), müşteri isterse tek noktadan genişletilebilir.
- Kaybeden oyuncuya gösterilecek başlığın tonu ("Maç Bitti" mi, doğrudan "Kaybettin" mi) — bu dosyada nötr ton tercih edildi (bkz. 3.8), müşteri daha doğrudan bir ton isteyebilir.
- Maç sonu ek istatistikler (en çok bölge, en uzun savunma vb.) — bkz. 3.8, kapsam dışı bırakıldı, yeni veri modeli işi gerektirir, müşteri isterse ayrı bir görev.
- Admin dışında herhangi bir izleyici (spectator) modu istenmediği bu dosyada kesin varsayıldı (`03-game-rules.md` Bölüm 11 ile tutarlı) — proje ilerledikçe bu tekrar gündeme gelmemeli, gelirse bu bilinçli bir kapsam genişletmesi sayılır.
