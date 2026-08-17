# 04 — Tasarım Rehberi (UI) 🔒 + 🛠️

Müşteri: **"tasarım olarak farklılaştıracağız, daha sade ve basit olacak."** 🔒 Bu, aşağıdaki somut kurallarla uygulanır. Projeye eklenecek **her yeni arayüz** için geçerlidir — tek kaynak, tutarlı görsel dil.

> 🔔 **İstisna — yalnızca `/` (Landing), 2026-08-07 kullanıcı kararıyla:** Kullanıcı, Landing sayfasının ilk izlenimde "kripto/Web3 platformu" gibi hissettirdiğini, oyun atmosferi taşımadığını belirtti ve bu dosyadaki "sade dashboard" yönünün **yalnızca `/` route'u için** bir oyun atmosferi lehine gözden geçirilmesini açıkça istedi (bkz. bu dosyanın sonundaki **"Landing (`/`) — Oyun Atmosferi İstisnası"** bölümü). Bu, `01-workflow-rules.md` Bölüm 0.5'teki "müşteri kararı sessizce değiştirilmez" sınırının **bilinçli, açık bir istisnasıdır** — kullanıcı o anki mesajında net talimat vermiştir (`CLAUDE.md` Öncelik Sırası madde 1). Bu istisna **yalnızca Landing'in görsel katmanına** (Background, Hero, dekoratif sahne, HUD kartları, Navbar'ın Landing'e özel görünümü) uygulanır — `/game/*`, `/lobi`, `/cuzdan`, `/odeme/*`, `/admin` gibi gerçek para/bakiye taşıyan veya oyun mantığı içeren hiçbir ekran bu istisnanın kapsamında değildir, onlar bu dosyanın geri kalanındaki "sade dashboard" kurallarına aynen tabidir. Aşağıdaki Bölüm 1-14 ve "Yapılmayacaklar" listesi, Landing dışındaki **her yerde** hâlâ tam geçerlidir.

Bu dosya bir **tasarım rehberidir, implementasyon dokümanı değildir**: hangi görsel kararın verildiğini söyler, o kararın hangi kod satırıyla yazılacağını söylemez (bu, Claude Code'un normal işidir, projenin mevcut Tailwind/shadcn kurulumuyla zaten tutarlı üretir). İstisna: Bölüm 2'deki token tablosu, tekrar tekrar aynı rengi/ölçeği farklı yerlerde tarif etmemek için tek kaynak olarak somut değer içerir.

Müşterinin verdiği tek gerçek karar: _"sade, basit, farklılaştırılmış."_ Altındaki her somut değer 🛠️ mühendislik/tasarım kararıdır. Karşılığı olmayan bir durumla karşılaşılırsa en yakın maddeye göre yorumlanır, yeni bir stil icat edilmez.

**Teknik kısıt (değişmez):** Projede Tailwind derleyicisi yok, yalnızca base utility class'lar çalışıyor. Hiçbir kural özel config veya arbitrary value gerektirmez.

---

## 1. Tasarım Felsefesi

- Minimal, flat, **"sade bir dashboard"** hissi — ağır/dekoratif oyun arayüzü değil.
- Fonksiyon önce gelir: bir eleman bir bilgiyi/aksiyonu netleştirmiyorsa eklenmez.
- Tutarlılık özgünlükten önce gelir — her yeni ekran mevcut ekranlarla aynı dili konuşur.

**Referans hissi:** Linear, Stripe Dashboard, Vercel Dashboard, GitHub UI. Oyun ekranı bir oyun launcher'ı gibi değil, profesyonel bir strateji paneli gibi hissettirmelidir.
**Referans olmayan görünüm:** Warcraft, Age of Empires, Clash of Clans, mobil oyun arayüzleri.

---

## 2. Design Tokens (Tek Kaynak)

🛠️ Pastel, sade ve modern bir görünüm sağladığı için tercih edilir. Pastel tonlar yalnızca kimlik ve durum renklerinde kullanılır. Birincil aksiyonlar ve metinler yüksek kontrastlı nötr tonlarda kalır.

| Kategori                                                                                                                                                                              | İzin verilen değerler                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Nötr zemin/metin                                                                                                                                                                      | Sıcak nötr ton (saf gri değil, hafif bej/kahve alt tonlu)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Oyuncu kimlik renkleri (1-12) 🛠️ **güncellendi — WinToWar'da maç başına oyuncu sayısı değişken (2-12), harita her zaman 12 bölge olduğundan renk paleti maksimum 12 için hazırlanır** | 12 ayrı, birbirinden net ayrışan **soluk/desatüre** pastel hue: mavi, mor, yeşil, sarı/amber-dışı altın, turkuaz, pembe, lavanta, camgöbeği, hardal, açık kahve/toprak, gök mavisi-koyu, zeytin yeşili. Kırmızı/gül tonu bu 12'nin **hiçbirinde kullanılmaz** — proje ödeme modülünü de kapsıyor, Danger de kırmızı ailesinde olduğundan "oyuncu kimliği" ile "hata/red durumu" aynı hue'da çakışırsa özellikle ödeme ekranlarında (reddedilen işlem, yetersiz bakiye) karışma riski doğar. Bu 12 hue + Danger + Success + Warning + Accent, hiçbiri birbiriyle paylaşılmayan ayrı tonlar olarak `GameConfig`/frontend token dosyasında tek yerden tanımlanır (`PlayerColors[0..11]` gibi bir dizi/harita, sayfa/bileşen içinde tek seferlik renk icat edilmez). Haritada aynı anda 12 rengin okunabilir kalması için tüm tonlar aynı doygunluk/parlaklık bandında tutulur (Bölüm "Yapılmayacaklar"daki desature kuralıyla tutarlı). |
| Accent (birincil aksiyon, odak vurgusu)                                                                                                                                               | Koyu nötr, **pastel değil** — buton/CTA okunabilirliği kimlik renklerinden ayrı tutulur                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| Danger                                                                                                                                                                                | Kırmızı/gül tonu — oyuncu renklerinden tamamen ayrı bir hue, yalnızca form hatası/reddedilen işlem gibi sistem mesajlarında, harita üzerinde asla                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| Success                                                                                                                                                                               | Yeşil tonu, yalnızca metin/ikon, asla arka plan                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| Warning                                                                                                                                                                               | Amber/turuncu tonu, yalnızca metin/ikon, asla arka plan                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| Nötr bölge (harita)                                                                                                                                                                   | Açık, sıcak nötr — oyuncu renklerinden belirgin şekilde farklı                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| Spacing                                                                                                                                                                               | Varsayılan Tailwind ölçeği içinden yalnızca `3`, `4`, `6` (padding/gap için). Bunun dışı kullanılmaz.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| Border-radius                                                                                                                                                                         | Tek değer: küçük/orta (`md` karşılığı). İstisna: gerçekten dairesel olması gereken ikon/avatar.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| Tipografi ölçeği                                                                                                                                                                      | 5 kademe: sayfa başlığı, panel başlığı, gövde, yardımcı metin, sayısal vurgu (Bölüm 3). Aradaki değerler kullanılmaz.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| Font ağırlığı                                                                                                                                                                         | Normal (gövde) ve semibold/bold (başlık, sayısal vurgu) — ikiden fazla ağırlık kullanılmaz.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| Animasyon süresi                                                                                                                                                                      | Tek değer: hızlı (~150ms). Daha yavaş bir süre kullanılmaz.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |

**Kontrast kuralı:** Pastel dolgular her zaman açık olduğundan üzerlerine beyaz metin konmaz — pastel zemin üzerindeki metin/ikon her zaman aynı ailenin koyu tonu ya da nötr koyu ton olur.

Yeni bir token ihtiyacı doğarsa bu tabloya eklenir, sayfa/bileşen içinde tek seferlik bir değer icat edilmez.

🛠️ **Tek istisna — QR kod arka planı (docs/09-eksik-tarama.md denetimi, Faz 7):** `/odeme/[invoiceId]` sayfasındaki LTC ödeme QR kodunun arka planı gerçek/ham beyazdır (`bg-white`, token değil) — QR okuyucuların güvenilir taraması için gerçek beyaz zemin + koyu modül kontrastı teknik bir zorunluluktur, `--card` gibi tema-bağımlı bir token (dark modda koyulaşır) taramayı bozar. Bu, palet dışına çıkan başka hiçbir yerde tekrarlanmayan, gerekçeli tek istisnadır.

---

## 3. Tipografi

| Seviye                | Kullanım                                                                                         |
| --------------------- | ------------------------------------------------------------------------------------------------ |
| Sayfa başlığı         | Route seviyesi tek başlık, en büyük/kalın                                                        |
| Panel başlığı         | Kart/panel üst başlığı (HUD, ActionPanel vb.), orta boy, yarı kalın                              |
| Gövde metni           | Standart içerik, normal ağırlık                                                                  |
| Yardımcı/etiket metni | Input label, alt bilgi, zaman damgası — küçük, soluk renk                                        |
| Sayısal vurgu         | Asker/bölge sayacı gibi öne çıkan sayılar — büyük, kalın, rakamlar hizasız zıplamayan bir stille |

---

## 4. Layout

- Panel/kart iç boşluğu: Bölüm 2'deki spacing ölçeğinden sabit bir değer, tüm panellerde aynı.
- Maksimum içerik genişliği: oyun/ödeme ekranları ortalanmış, geniş bir sınır içinde; formlar (tek sütun) daha dar bir sınır içinde.
- Kart yerleşimi grid tabanlı; manuel/mutlak konumlandırma kullanılmaz (harita SVG'si hariç — o koordinat tabanlı çalışır).

**Görsel yoğunluk:**

- Bir panelde aynı anda en fazla 5-7 ana bilgi gösterilir; fazlası ikincil sayılır.
- İkincil bilgiler gerektiğinde açılan bir panel/modalda gösterilir, ana ekrana sıkıştırılmaz.
- Boş alan (whitespace) tasarımın bir parçasıdır — her alanı bir kart/bileşenle doldurma dürtüsüne karşı direnilir.

---

## 5. Component Usage Rules

**Button**

- Primary: sayfada/ekranda **en fazla 1 tane** (ana aksiyon — ör. "Asker Üret", "Ödeme Yap").
- Secondary: ikincil aksiyonlar (ör. "İptal", "Geri").
- Ghost: araç çubuğu/düşük öncelikli aksiyonlar.
- Destructive: geri alınamaz/riskli aksiyonlar (ör. maçtan çekilme).
- Her buton bir eylem tetikler; salt bilgi göstermek için buton kullanılmaz (onun için Badge kullanılır).

**Badge**

- Yalnızca **durum göstermek** için (`Pending`, `Confirmed`, sahiplik durumu vb.).
- Tıklanabilir değildir, buton yerine kullanılmaz.
- 🔒 **Bot rozeti (docs/03-game-rules.md Bölüm 7 DÜZELTME):** Rakip bir bot olduğunda, adının yanında her zaman görünür bir "Bot" rozeti bulunur — hem masa/lobi listesinde hem maç içi HUD'da. Rozet gizlenemez, opsiyonel yapılamaz, rakibin insan olduğu izlenimini verecek şekilde belirsizleştirilemez; bu, müşterinin "canlı görünsün ama yanıltmasın" ayrımını koruyan geri alınamaz bir kuraldır.

**Card**

- İçinde en az bir başlık bulunur.
- Kart içine kart konulmaz (iç içe kart yasak — görsel karmaşayı artırır).

**Input**

- Her input bir label ile eşleşir; placeholder tek başına label yerine geçmez.

**İkon**

- Yalnızca metni desteklemek için kullanılır; ikon tek başına anlam taşımaz (yanında her zaman bir metin/etiket olur).
- Her bilgi alanında ikon kullanmak zorunlu değildir.
- Bir satırda birden fazla ikon kullanılmaz.

---

## 6. Pattern Library

Yeni bir ekran/panel tasarlanırken önce bu şablonlardan biri uygulanır; şablonların dışında serbest bir yapı icat edilmez.

**Standart Kart**

```
Başlık
(opsiyonel) Açıklama
İçerik
(opsiyonel) Alt aksiyon alanı
```

Tüm paneller (HUD, ActionPanel, ödeme özeti vb.) bu yapıyı kullanır.

**Standart Form**

```
Label
Input
Helper Text (opsiyonel)
Error (varsa, Helper Text'in yerini alır)
Button (gönderim)
```

**Standart Liste**

```
[İkon] Başlık + Alt açıklama  ...........  [Sağda aksiyon/durum]
```

Tek satır, sol tarafta kimlik bilgisi, sağ tarafta aksiyon veya durum rozeti.

**Standart Modal**

```
Başlık
Açıklama
İçerik
─────────────
İptal | Onay (sağa hizalı, Onay her zaman en sağda)
```

🛠️ **WinToWar'a özgü kullanım — yeni bir şablon icat edilmez, mevcutlar uygulanır (v2 — Practice ve şifreli oda modeli güncellendi):** Lobi'deki Standart/VIP oda listesi **Standart Liste** şablonunu kullanır (sol: oda adı + oyuncu sayısı "X/N", sağ: giriş ücreti rozeti + "Katıl" butonu). Şifreli odalar bu listede **hiç görünmez** (özel davet — bkz. `03-game-rules.md` Bölüm 2.2), dolayısıyla listede bir "kilit ikonu" göstermeye gerek yoktur; kilit/parola ekranı yalnızca `/lobi/[inviteToken]` sayfasında, odaya davet linkiyle gidildiğinde karşılaşılan ayrı bir modal/formdur (**Standart Form** şablonu, tek bir parola input'u). VIP oda kurma da **Standart Form** şablonunu kullanır (gri bölge savunması sayısal input 1-7, Fog of War açık/kapalı toggle, giriş ücreti input, oyuncu sayısı 2-12 arası seçim, opsiyonel parola alanı — doldurulursa oda şifreli olur). Practice, bir oda listesi/sekmesi **değildir** — `/lobi`'de tek bir "Pratik Oyna" butonudur, tıklanınca doğrudan eşleşme kuyruğuna eklenir (bkz. `03-game-rules.md` Bölüm 7).

---

## 7. Form Standartları

- Hata durumunda: input görsel olarak vurgulanır (danger tonuyla), kısa hata metni gösterilir — ekstra ikon eklenmez.
- Yetersiz bakiye/geçersiz işlem gibi durumlarda ilgili buton devre dışı bırakılır ve kısa bir gerekçe (native tooltip yeterli, ayrı bir tooltip kütüphanesi eklenmez).
- Para/sayı girişleri sağa hizalı, rakamlar hizasız zıplamaz.

---

## 8. Veri Gösterimi

- Tablo/liste içindeki sayısal değerler sağa hizalı.
- **Para formatı:** LTC değerleri kullanıcıya en fazla 6 anlamlı basamakla gösterilir (tam 8 basamak hassasiyeti yalnızca tooltip/detay görünümünde). USD değerleri 2 ondalık, `$` işaretiyle.
- **Durum rozetleri:** renk eşlemesi Bölüm 2'deki paletle birebir (`Pending` → nötr, `Confirmed`/`Completed` → success tonu, `Failed`/`Expired` → danger tonu).
- **Sayaçlar** (maç süresi, geri sayım): rakamlar hizasız zıplamaz; saniye güncellemesinde animasyon/geçiş yok.

---

## 9. Harita Standartları (Oyun Modülü)

Harita ekranın ana odak noktasıdır; HUD ve paneller onu destekler, haritanın önüne geçmez. Harita mümkün olduğunca büyük görünür.

- **Seçili bölge:** kenarlık kalınlaşır. ⚠️ **GÜNCELLENDİ (docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 5/18/19, docs/20-state-io-army-gorsel-fark-giderme.md §2.A.6 denetiminde senkronlandı):** Bu satır önceden "nötr tonda, renk değişmez" diyordu — doc 18 bunu kasıtlı olarak değiştirip seçim/hedef/drag-hover vurgusunu sabit/nötr bir tondan sürükleyen oyuncunun kendi kimlik rengine (`playerAccentColor`) taşıdı, kod (`RegionNode.tsx` `RegionShape`) zaten bu son karara göre çalışıyordu — bu yalnızca metnin koda yetişmesidir, docs/20 kapsamında koda dokunulmadı. Bölge dolgusunun kendisi (sahiplik rengi) yine değişmez, yalnızca kenarlık kalınlaşır/renklenir.
- **Hover:** hafif opaklık azalması + işaretçi değişimi; sahiplik rengi karışmasın diye renk değişmez.
- **Sahiplik dolgusu:** Bölüm 2'deki oyuncu renkleri ~~— Standart/Practice/VIP'te aynı 12'lik palet~~ **GÜNCELLENDİ:** artık oda tipine göre ayrı bir palet var — Standart (4 kişilik) mavi/kırmızı/mor/yeşil, Practice (2 kişilik) mavi/kırmızı, VIP genel 12'lik paletle değişmedi (bkz. `03-game-rules.md` Bölüm 2.1, `colors.ts`) — 🛠️ **düzeltme:** bu satır önceden "bkz. Bölüm 16 Bölüm 7" gibi bozuk/anlamsız bir referans taşıyordu (muhtemelen `03-game-rules.md`'nin kendi içinde harici/tarihsel bir "16-state.io-gorsel-referans.md" belgesine yaptığı atıfla karışmıştı — bkz. aşağıdaki numara çakışması notu); doğru kaynak oda tipine göre oyuncu renk paletinin tanımlandığı `03-game-rules.md` Bölüm 2.1'dir. Nötr bölge ayrı bir nötr tonda. 🔒 **GÜNCELLEME (müşteri kararı):** Dolgu artık sabit de değil — sahipli bir bölgede asker arttıkça renk **hafifçe** koyulaşır (🔒 "tatlı renkler olsun, çok koyu olmasın" — koyulaşma kasıtlı olarak ölçülü tutulur, pastel his hiçbir asker sayısında kaybolmaz); fethedilmeyen (nötr) bir toprakta savunma azaldıkça (üstüne asker gelip savunma düştükçe) renk açılır, savunma yeniden dolunca koyulaşır. Müşteri örneği: fethedilmeyen toprak için savunma 10 iken en koyu, 0 iken en açık (bkz. `03-game-rules.md` Bölüm 4, `colors.ts` `regionFillColorByStrength`).
- **Komşuluk çizgileri:** ince, düşük kontrastlı — sahiplik rengiyle karışmaz.
- **Bölge etiketleri:** her zaman görünür. 🛠️ **GÜNCELLENDİ (docs/20-state-io-army-gorsel-fark-giderme.md §2.B.1 — video kıyaslaması, "bölge adı ile army sayısı görsel ağırlıkça çok yakın, karışıyor"):** bilgi hiyerarşisi artık **army sayısı/rozet > sahiplik rengi > bölge adı** — rozet büyütüldü (owner renginin koyu varyantıyla dolu, `RegionNode.tsx` `RegionLabel`), asker sayısı büyük/kalın (fontSize 15/weight 700), bölge adı küçük/ikincil (fontSize 8.5/weight ≤500) kalır — **kaldırılmaz**, yalnızca görsel ağırlığı azalır. Bu büyütme **her bölgeye eşit** uygulanır — 🔒 `03-game-rules.md` Bölüm 3 "kale gibi bir alan olmayacak" kısıtı gereği hiçbir bölge (başlangıç bölgesi dahil) diğerinden daha büyük/farklı bir rozetle ayrışmaz. Tüm bölge dolguları pastel/açık tonda olduğundan etiket metni her zaman **koyu nötr** — zemine göre metin rengi değiştirme karmaşasına gerek kalmaz. Rozetteki asker sayısı, state güncellemesiyle birlikte doğrudan/anında güncellenir (Bölüm 12'deki "sayı güncellemelerinde animasyon yok" ilkesiyle tutarlı — bir ara talimatla kısa bir sayaç geçişi denenmiş, sonra müşterinin "bugün yaptığın işlemleri geri al" talimatıyla (2026-08-11) geri alınmıştır; docs/20 §2.B.3 bu kararın genel kural olarak **korunduğunu** teyit eder — rozet büyütmesi yalnızca boyut/tipografi, sayı normalde hâlâ animasyonsuz/anlık günceller).

🔒 **Dar istisna (2026-08-12, docs/20-state-io-army-gorsel-fark-giderme.md §2.B.2/§2.B.3 — gerçek bir state.io ekran kaydıyla doğrulandı):** Yukarıdaki "animasyonsuz/anlık" kuralı yalnızca genel/rutin durum için geçerlidir. Bir sevkiyat hedefine ulaştığı (`ArmyArrived`) an, **yalnızca o bölgenin rozetinde**, mevcut sayıdan sonuca ~0.5-1 sn'lik hızlı bir geri sayımla iner (ör. 43→24→15→14) ve rozet dolgusu aynı anda kısaca kendi renginin daha açık tonuna parlar, sonra normale döner (`RegionNode.tsx` `RegionLabel`, `arrivalCountdown` prop — GameMap tarafından tetiklenir). Bu, 2026-08-11'deki genel geri alma kararının sessizce geri getirilmesi **değildir** — kapsam kesin olarak dar: yalnızca `ArmyArrived` anında, yalnızca hedef bölgede. Üretim tik'lerinde, transit sırasında (sevkiyat yolda giderken kaynak/hedef rozetinde hiçbir ekstra görsel yok) veya başka hiçbir yerde sayı hâlâ anında günceller.

- ~~**Hareket animasyonu:** ordu hareketi yalnızca bir ilerleme göstergesiyle temsil edilir; haritada gerçek zamanlı hareket eden bir ikon/nokta animasyonu **yok** (dekoratif animasyon yasağının somutlaşmış hali).~~ **GEÇERSİZ — bkz. `15-asker-hareketi-performans.md` Bölüm 1/5.** Müşteri "hiç oyun gibi durmuyor, askerler gözükmeli" geri bildirimiyle bu kararın tam tersini açıkça istemiştir (`CLAUDE.md` Öncelik Sırası madde 1). Haritada artık gerçek zamanlı hareket eden, owner renginde sayı rozetli bir "asker grubu" işareti vardır (pop-in/bobbing/çarpışma animasyonu dahil); bu, Bölüm 8'deki dekoratif efekt yasağıyla (ağır gradient/glow/neon/parçacık) çelişmez — playful his hareketten gelir, dekoratif efektten değil. Somut tasarım/uygulama detayı `15-asker-hareketi-performans.md` Bölüm 5-6'dadır.
- **Yakınlaştırma:** 🛠️ Müşteri belirtmedi. Varsayım: **yok** — 12 bölge her zaman tek görünümde sabit kalır (10-15 dakikalık hızlı maç hedefiyle, zoom/pan gereksiz bir etkileşim katmanı ekler, YAGNI).

---

## 10. Dashboard/HUD Standartları

- 🛠️ **WinToWar'a göre güncellendi:** HUD ekranın üstünde sabit, her zaman görünür: **asker sayısı** (mevcut havuz), **bölge sayısı** (sahip olunan), **üretim hızı** (10 sn'de kaç asker), süre/maç durumu. Önceki "altın, general sayısı, yuva/kale seviyesi" alanları WinToWar'da **karşılığı olmadığı için kaldırılmıştır** (bkz. `03-game-rules.md` — bu oyunda ayrı bir Altın kaynağı, General birimi veya Kale seviyesi yoktur).
- HUD'daki her değer değiştiğinde yalnızca metin güncellenir, vurgu/geçiş animasyonu yok.
- 🛠️ **GÜNCELLENDİ — state.io incelemesi sonrası (önceki input/slider tasarımı geri çekildi):** Asker gönderme artık ayrı bir sayısal input/slider ile değil, doğrudan **haritada sürükle-bırak** ile yapılır (bkz. `03-game-rules.md` Bölüm 6/15) — bu hem referans alınan state.io'nun kendi etkileşimidir hem de müşterinin "daha sade basit olacak" talimatına input/slider'dan daha uygundur. Etkileşim: kendi bölgene tıkla/dokun (Bölüm "Seçili bölge" stiliyle vurgulanır) → haritadaki herhangi bir bölgeye sürükle (komşu olma zorunluluğu **yok**, bkz. `03-game-rules.md` Bölüm 3/15-D.1) → bırak → gönderim tetiklenir; gönderilecek miktar için ayrı bir onay adımı/input **yoktur** (sunucu otomatik hesaplar, bkz. Bölüm 6). Masaüstünde fare ile drag, mobilde dokunmatik sürükleme; her ikisi de aynı `RegionNode.tsx` pointer-event mantığını kullanır, ayrı bir masaüstü/mobil bileşeni yazılmaz.
- ActionPanel bu değişiklikle birlikte artık bir **aksiyon** değil, **bilgi** paneline dönüşür: masaüstünde haritayla yan yana, mobilde harita altına iner; seçili bölgenin mevcut asker sayısını, üretim hızını ve (VIP açık haritada) komşu bölgelerin savunma/asker sayısını salt-okunur gösterir. Hedef bölgenin savunma sayısı (`GreyRegionDefenseCount`) veya (fetih edilmişse) o an tuttuğu asker sayısı, ayrıca bölgenin kendi üzerinde **her zaman görünen bir rozet** olarak gösterilir (state.io'daki gibi) — ayrı bir tıklama/tooltip'e gizlenmez, Fog of War açıkken yalnızca görünür bölgelerde bu rozet gösterilir (bkz. aşağıdaki Fog of War maddesi).
- 🛠️ **Fog of War (VIP odaya özel):** Sisli mod açıkken, oyuncunun kendi bölgeleri ve bunlara doğrudan komşu bölgeler haritada net görünür; daha uzak bölgeler soluk/gri bir "keşfedilmemiş alan" dolgusuyla gösterilir (asker sayısı/sahip bilgisi gizlenir, yalnızca arazi şekli görünür). Açık Harita modunda tüm bölgeler her zaman net görünür. Bu ayrım tek bir CSS/state bayrağıyla (`Room.FogOfWar`) yönetilir, ayrı bir harita bileşeni yazılmaz.

---

## 11. Responsive Davranış

🛠️ Proje öncelikli olarak masaüstü deneyimi hedefler (gerçek zamanlı stratejik karar hızı gerektirir), ama temel kullanılabilirlik mobilde bozulmaz. Standart Tailwind breakpoint'leri kullanılır, özel breakpoint eklenmez.

- Masaüstü: HUD üstte tek satır, ActionPanel harita ile yan yana.
- Mobil: ActionPanel harita altına iner, HUD gerekirse iki satıra sarar — ayrı bir sidebar kavramı yok.
- Ödeme ekranı (tek sütun form) mobil/masaüstü aynı düzeni kullanır, ekstra bir responsive kural gerekmez.

---

## 12. Animasyon Standardı

Yalnızca **durum değişikliklerinde işlevsel geçiş** kabul edilir, dekoratif animasyon yasaktır.

- Süre: Bölüm 2'deki tek "hızlı" değer.
- Ne zaman kullanılır: hover/focus geçişleri, panel açılış/kapanışı, disabled↔enabled geçişi.
- Ne zaman kullanılmaz: sayı güncellemeleri, harita üzerindeki sahiplik değişimi (anlık), "dikkat çekici" hiçbir efekt (parlama, zıplama, ölçek büyütme).

---

## 13. Erişilebilirlik

- Metin/arka plan kontrastı standart Tailwind tonlarının sağladığı okunabilirlik seviyesinin altına düşmez. Pastel dolgular üzerinde **her zaman koyu metin/ikon** kullanılır (Bölüm 2'deki kontrast kuralı) — açık pastel zemin üzerinde açık/beyaz metin asla kullanılmaz.
- Tüm etkileşimli elemanlar (buton, input, harita üzerindeki bölge seçimi dahil) klavye ile erişilebilir olmalı.
- Odak göstergesi (focus ring) hiçbir yerde tamamen kaldırılmaz.
- Devre dışı butonlarda kısa gerekçe gösterilir (Bölüm 7).

---

## 14. Empty / Error / Loading States

- **Loading:** Spinner + kısa metin ("Yükleniyor…") tercih edilir; skeleton yalnızca tam sayfa/liste yüklemesinde, mevcut shadcn primitive'i üzerinden — yeni bir skeleton sistemi icat edilmez. Buton içi işlemlerde (ör. "Ödeme Yap" tıklandıktan sonra) buton kendisi loading göstergesine döner, sayfa genelinde ayrı bir yükleniyor katmanı açılmaz.
- **Empty (boş durum):** Kısa açıklayıcı metin + (varsa) tek bir aksiyon butonu. Dekoratif illüstrasyon eklenmez (Bölüm 1 — sade felsefeyle tutarlı).
- **Error (hata durumu):** Kısa, kullanıcının ne yapması gerektiğini söyleyen bir mesaj (Bölüm 7'deki form hata standardıyla aynı ton); teknik hata detayı (stack trace, hata kodu) kullanıcıya gösterilmez, yalnızca loglanır.

---

## Yapılmayacaklar (Yasaklar) 🛠️ **düzeltildi — bunlar birebir müşteri talimatı değil, "sade/basit" talebinden türetilmiş mühendislik kararlarıdır**

> ⚠️ Bu liste önceden 🔒 (değiştirilemez müşteri talimatı) olarak işaretlenmişti — ama müşterinin verdiği tek gerçek karar "tasarım olarak farklılaştıracağız, daha sade ve basit olacak" cümlesidir (bkz. Bölüm 1); "gradient kullanma", "glassmorphism kullanma" gibi somut maddelerin hiçbirini müşteri birebir söylemedi. Bunlar bu cümleden türetilmiş, gerekçeli mühendislik kararlarıdır — CLAUDE.md'deki işaretleme sistemine göre doğru etiket 🛠️'dır. Müşteri ileride "aslında hafif bir gradient istiyorum" derse, bu **tek bir maddenin değişmesidir**, tüm dokümanın "müşteri talimatını ihlal ettiği" anlamına gelmez.

- Gradient kullanma.
- Neon/parlak renk kullanma.
- Glassmorphism / cam efekti kullanma.
- Glow/parlama efekti kullanma.
- Gereksiz/aşırı animasyon (Bölüm 12'nin kapsamı dışına çıkan hiçbir animasyon).
- Emoji kullanma — ikon ihtiyacı projede zaten kullanılan ikon kütüphanesiyle (lucide-react, shadcn'in varsayılanı) karşılanır; farklı bir kütüphane eklenmez. Her buton için ikon zorunlu değildir, yalnızca bilgi katan yerlerde kullanılır.
- Bölüm 2'deki paletin dışında serbest/rastgele renk kullanma.
- Bölüm 2'deki pastel tonların doygunluğunu artırma (parlak/"candy" pastel kullanma) — palet **soluk/desatüre** kalmalı, aksi "oyuncak" hissine kayar.
- Özel Tailwind config değişikliği veya arbitrary value kullanma.

---

## Metin Dili

- Kullanıcıya görünen tüm metinler **Türkçe** (değişken/fonksiyon isimleri İngilizce kalabilir).

---

## Landing (`/`) — Oyun Atmosferi İstisnası 🔔 (yalnızca bu route, 2026-08-07)

Bu bölüm yukarıdaki tüm dosyayı **geçersiz kılmaz** — yalnızca `/` route'unun görsel katmanı için, yukarıdaki Bölüm 1-14 ve "Yapılmayacaklar" listesindeki maddelerin **yerini alan** bir alt-küme tanımlar. Diğer her route (`/lobi`, `/game/*`, `/cuzdan`, `/odeme/*`, `/admin`, `/kurallar` vb.) yukarıdaki genel kurallara aynen tabidir.

**Gerekçe:** Kullanıcı, mevcut Landing'in ilk 2-3 saniyede "kripto/Web3/yatırım platformu" hissi verdiğini, bir strateji oyununun ana ekranı gibi hissettirmediğini belirtti (aşırı glassmorphism, tek düz mavi-gri palet, görünmeyen arka plan videosu, dekoratif ama oyunla ilgisiz bir "node grafiği", hareket eksikliği). Bu his, müşterinin asıl "farklılaştıracağız" kararıyla da çelişiyordu — sade dashboard hissi bir **oyun** landing sayfası için yanlış hedefti.

**Landing'e özel token/kararlar:**

- **Referans hissi (yalnızca Landing):** Yukarıdaki Bölüm 1'in tersine, burada Clash Royale/Brawl Stars tarzı "oyun ana ekranı" atmosferi **hedeftir** — Linear/Stripe referansı Landing dışı sayfalar için geçerliliğini korur.
- **Palet:** Mevcut mavi (`#38BDF8`) birincil kalır. Ek olarak yalnızca Landing'de: altın/amber (`#F5B942` — ödül/kazanç vurgusu) ve kırmızı takım tonu (`#F2495C` — yalnızca dekoratif "rakip takım" temsili, **asla** bir form/işlem hata rengi olarak kullanılmaz, Bölüm 2'deki uygulama genelindeki `Danger` tonuyla karıştırılmaz). Bu iki renk yalnızca `web/components/landing/*` içinde kullanılır.
- **Glassmorphism azaltılır, kaldırılmaz:** Panel/kartlarda `backdrop-blur` yerine daha **çok opak, "HUD paneli" gibi düz katı zemin** (koyu lacivert + ince kenarlık) tercih edilir — kullanıcının "çok fazla cam efekti" eleştirisi burada karşılanır.
- **Glow:** Yalnızca birincil CTA ve HUD rozetlerinde, ölçülü biçimde kullanılabilir (tamamen yasak değil, Landing dışı sayfalarda hâlâ yasak).
- **Dekoratif animasyon:** Yalnızca Landing'de, Bölüm 12'nin aksine izinlidir — arka plan videosu daha görünür (daha az koyu tint/blur), sağdaki dekoratif "node grafiği" yerine askerler/kale/ok temalı basit bir SVG savaş sahnesi animasyonu, CTA hover'da hafif zıplama. Bu animasyonlar performansı gözetir (yalnızca `lg`+ genişlikte tam sahne, `prefers-reduced-motion` saygı görür).
- **Emoji:** Yine kullanılmaz — bunun yerine `lucide-react`'teki oyun temalı ikonlar (`Swords`, `Castle`, `Trophy`, `Coins`, `Users`, `Play`) daha büyük/renkli rozet arka planlarıyla "HUD ikonu" hissi verir. Emoji kütüphane tutarlılığı (Bölüm "Yapılmayacaklar") burada da geçerli, yalnızca ikonların görsel ağırlığı artırılır.
- **Başlık fontu:** Yalnızca Landing H1'i için, `next/font/google` üzerinden tek bir ek "display" font (Space Grotesk) — gövde metni ve site genelindeki font (Geist/Inter) değişmez.
- **İçerik/metin kuralları değişmez:** `08-page-content.md` Bölüm 3.1'deki Landing içerik iskeleti (tek H1, kazanç formülü, tek birincil CTA, Türkçe metin) aynen geçerli — yalnızca görsel dil değişiyor, bilgi mimarisi değişmiyor.
