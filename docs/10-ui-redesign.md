# 10 — UI/UX Görsel Yenileme (Soft Modern Pastel Refresh) 🔒 + 🛠️

Müşteri: **"oyunun mantıksal altyapısı çalışıyor, arka planı bozmadan sadece görsel katmanı (UI/UX) tamamen yenilenecek — daha yüksek kaliteli, akıcı, pastel bir görünüm."** 🔒

**Revizyon notu:** Bu dokümanın ilk taslağında ton "pastel/playful ('tatlı')" olarak adlandırılmıştı (müşterinin kendi ilk mesajındaki "tatlı (playful)" ifadesinden). Değerlendirme sonrası müşteri tonu **"soft modern, ama tamamen resmi değil"** olacak şekilde netleştirdi — "playful/tatlı" kelimesi kaldırıldı, karakter/oyunsuluk çağrışımı azaltıldı, ama "kurumsal dashboard kadar soğuk da değil" dengesi korundu. Aşağıdaki tüm bölümler bu güncel tona göre yazılmıştır.

## ⚠️ `04-style.md` ile ilişki — açık üzerine yazma

`04-style.md` Bölüm 1'de 🔒 karar **"sade, basit dashboard hissi"** ve açıkça **"Referans olmayan görünüm: Warcraft, Age of Empires, Clash of Clans, mobil oyun arayüzleri"** yazıyor. Bu doküman, müşterinin **bu görevdeki** yeni talimatı gereği o konumlandırmayı kısmen günceller:

- **Değişen:** Genel ton artık salt "kurumsal dashboard" değil, **"soft modern — yumuşak yüzeyler, yuvarlatılmış kartlar, ince gradientler, sakin boşluk kullanımı"** yönüne kayıyor. `04-style.md` Bölüm 1 ve Bölüm 2'nin **estetik** yönü bu doküman lehine güncellenmiştir. Referans hissi: **modern bir SaaS dashboard'u ile casual bir strateji oyunu arasında bir yer** — Linear/Stripe'ın netliği, ama daha yumuşak yüzeyler ve pastel vurgularla.
- **Değişmeyen (hâlâ 🔒, bu doküman de bunlara uyar):** `04-style.md`'deki **fonksiyonel/yapısal** kurallar geçerliliğini korur — 12 oyuncu kimlik rengi sistemi, Danger'ın kırmızı ailesinde ve oyuncu renklerinden tamamen ayrı olması kuralı, pastel zemin üzerinde beyaz metin kullanılmaması, tipografi ölçeğinde en fazla 2 font ağırlığı, "bir panelde en fazla 5-7 ana bilgi" yoğunluk kuralı, Component Usage Rules (Bölüm 5), Pattern Library (Bölüm 6). Bu doküman bunları **iptal etmez**, üzerine soft-modern bir görsel dil giydirir.
- ✅ `CLAUDE.md`'nin "Her görevde önce oku" tablosuna şu satır eklenmiştir (denetim sırasında eksik bulunup düzeltilmiştir): _"Görsel yenileme/re-skin işi → `docs/10-ui-redesign.md` (+ hâlâ geçerli olan `04-style.md` fonksiyonel kuralları)."_ Öncelik sırasında bu dosya, `04-style.md` ile aynı kademede (4) sayılır; ikisi çelişirse **estetik/renk konusunda bu dosya**, **fonksiyon/yapı konusunda `04-style.md`** kazanır.

---

## 0. Kapsam ve Mutlak Sınır 🔒

Bu görev **yalnızca görsel katmanı** kapsar. `01-workflow-rules.md`'deki genel "kapsam dışı dosyaya dokunma" kuralı burada özellikle şu şekilde uygulanır:

**İzinli (kapsam içi):**

- `web/app/globals.css`, Tailwind config/token tanımları.
- `web/components/ui/*` (shadcn) bileşenlerinin görsel varyantları (className, stil, spacing, renk).
- Sayfa/bileşen dosyalarındaki **JSX yapısı ve className'ler** (yeniden düzenleme, kart/panel görünümü, hover/transition, layout).
- Yeni **salt-görsel** yardımcı bileşen eklemek (ör. bir `StatBadge`, `PulseIndicator` gibi state almayan, prop olarak veri alan bileşenler).

**Kesinlikle yasak (kapsam dışı — `01-workflow-rules.md` 0.2 aynen geçerli):**

- `lib/game/*`, `lib/payments/*`, `lib/admin/*` içindeki iş mantığı, API çağrıları, SignalR client, store (Zustand/vb.) — **hiçbiri değiştirilmez**.
- Bileşenlerin **prop interface'lerini/tiplerini** değiştirmek (yeni bir görsel prop eklemek dışında — ör. `variant` eklemek serbest, mevcut veri prop'unu kaldırmak/yeniden adlandırmak yasak).
- Backend (`api/`) — bu görev tamamen frontend'de kalır, hiçbir `.cs` dosyasına dokunulmaz.
- Dosya/klasör taşıma, yeniden adlandırma, route değişikliği.
- Component mantığını (state, effect, veri çekme) yeniden yazmak — bir bileşen görsel olarak değiştirilirken içindeki `useEffect`/`useState`/API çağrısı satırı aynı kalmalı.
- Yeni bir animasyon/ikon kütüphanesi eklemek — proje zaten bir kütüphane kullanıyorsa (ör. `lucide-react`) onunla devam edilir, gerekçesiz yeni bağımlılık eklenmez (`06-coding-standards.md`).

**Kontrol testi (her dosya değişikliğinden önce sor):** _"Bu satırı sildiğimde/değiştirdiğimde oyunun davranışı (veri, state, aksiyon sonucu) değişir mi?"_ Cevap evetse o satır bu görevin kapsamı dışındadır, dokunulmaz.

---

## 1. Tasarım Yönü (bu dosyanın 🔒 kararı)

- **Ton:** Modern, akıcı, **soft/samimi ama tamamen resmi değil** — rekabetçi bir strateji oyununun ciddiyetini yumuşak yüzeylerle taşır. Karikatürize/oyunsu bir yöne kaymaz, ama Linear/Stripe kadar da soğuk kalmaz.
- **Referans hissi:** Modern bir strateji oyununun kullanıcı deneyimi — kart düzeni ve bilgi mimarisi dashboard seviyesinde düzenli ve okunabilir, ama renk geçişleri, durum geri bildirimleri ve etkileşimler oyun kimliğini korur. Görsel dil kullanıcıya finans paneli değil, modern bir strateji oyunu oynadığını hissettirmelidir — soft surfaces, rounded cards, subtle gradients, calm spacing.
- **Referans olmayan görünüm:** Warcraft, Age of Empires, Clash of Clans, mobil oyun arayüzleri, aşırı karikatürize/3D mobil oyun estetiği (bu kısıt `04-style.md`'den korunur).
- **Rekabetçi netlik önceliklidir (kapsamı sınırlı) 🔒:** Bu kural yalnızca **HUD, harita, oyuncu kimlik renkleri, para/asker sayaçları** için geçerlidir — bu unsurlarda pastel palet asla okunabilirliği bozamaz. **Bu kural, landing/lobi/kart gibi genel içerik alanlarının renksiz/gri kalması için gerekçe olarak kullanılamaz** — v1 uygulamasında yaşanan hata tam olarak buydu: "netlik" kuralı yanlışlıkla tüm sayfaya uygulanıp her yer tek bir soluk tona indirgendi. Genel içerik alanlarında estetik ile netlik çatışmaz, çünkü oradaki bilgi oyun-kritiği değildir.
- **Oyun kimliği kaybolmaz 🔒:** Arayüz dashboard gibi organize edilir, ama oyuncu her ekranda bir strateji oyunu oynadığını hissetmelidir. Bu nedenle harita, oyuncu renkleri, maç durumu ve rekabet unsurları her zaman ekranın görsel odağı olmaya devam eder — "soft modern" ton bunları arka plana itip ekranı bir finans/SaaS paneline çevirmek için bir gerekçe değildir.

---

## 2. Design Tokens — Güncellenmiş Palet 🛠️

> **Düzeltme notu (v2):** İlk uygulamada bu bölümdeki pastel tonlar "kullanılabilir" olarak bırakıldığı için hiç kullanılmadı, sonuç tek tonlu/gri bir ekran oldu. Bu artık **zorunluluk** olarak yazılmıştır — "kullanılabilir" değil "kullanılır".

`04-style.md` Bölüm 2'deki tabloya **ek/güncelleme** olarak, `globals.css`'e eklenecek CSS değişkenleri:

| Token                                                                     | Amaç                                 | Yön                                                                                                                                                |
| ------------------------------------------------------------------------- | ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--surface-base`                                                          | Genel arka plan                      | Açık ama **belirgin şekilde renkli** lavanta beyazı (nötr gri/beyaz değil)                                                                         |
| `--surface-card`                                                          | Kart/panel zemini                    | Aşağıdaki 4 pastel tondan biri — **her kart nötr gri olamaz**                                                                                      |
| `--pastel-mint` / `--pastel-lavender` / `--pastel-peach` / `--pastel-sky` | Kart/bölüm zemini + dekoratif amaçlı | Belirgin, göz ile ayırt edilebilir doygunlukta (yaklaşık HSL doygunluk %35-55, açıklık %88-94 — "beyaza çok yakın soluk" değil, "açık ama renkli") |
| `--accent`                                                                | Birincil aksiyon (buton)             | `04-style.md`'deki "koyu nötr, pastel değil" kuralı **korunur** — CTA okunabilirliği için                                                          |
| `--radius-soft`                                                           | Kart/buton köşe yarıçapı             | `04-style.md`'deki tek `md` değerinden bir kademe daha yuvarlak                                                                                    |
| `--shadow-soft`                                                           | Kart gölgesi                         | Yumuşak, düşük opaklık                                                                                                                             |
| `--transition-soft`                                                       | Hover/tıklama geçişi                 | ~150-200ms, ease-out                                                                                                                               |

**Zorunlu kullanım noktaları (kaçamaksız) 🔒:**

- Yan yana/alt alta duran **kart grupları** (ör. landing'deki "1. Katıl / 2. Bölge Fethet / 3. Kazan" gibi 3'lü/4'lü bloklar), **her biri farklı bir pastel tonda** olur (mint/lavender/peach/sky sırayla) — üçü de aynı gri-lavanta olamaz. Bu, "Rekabetçi netlik önceliklidir" kuralının **ihlali değildir**; bu kural yalnızca HUD/harita/oyuncu kimliği için geçerlidir (bkz. Bölüm 1), genel içerik kartları için geçerli değildir.
- Sayfa hero/üst bölümünde en az bir **görünür** (soluk değil, fark edilir) radial/linear pastel gradient veya renkli dekoratif şekil (blob) bulunur.
- İkon rozetleri/numaralı adım göstergeleri (ör. "1", "2", "3") **renkli bir arka plan dairesi/kutusu** içinde gösterilir, düz metin olarak durmaz.
- Bir sayfada **en az 3 farklı, birbirinden ayırt edilebilir pastel ton** aynı anda görünür olmalı. Tek bir tonun (ör. yalnızca lavanta) tüm sayfayı kaplaması **kabul kriteri olarak başarısız sayılır.**
- "Kazanç formülü" gibi bilgi kutuları da nötr gri değil, pastel tonlardan biriyle (ör. sky veya peach) vurgulanır.

**Oyuncu kimlik renkleri (1-12), Danger, Success, Warning:** `04-style.md`'deki tanım **aynen** kullanılır, değiştirilmez.

**Kontrast zorunluluğu 🔒:** Her pastel zemin üzerindeki metin/ikon WCAG AA (en az 4.5:1) kontrastı sağlar. Para, asker sayısı, bölge sahiplik göstergesi gibi **oyun kritiği bilgiler** hiçbir pastel tonun kontrastından ödün vermez — ama bu kısıt yalnızca o bilgiler için geçerlidir, genel sayfa tasarımını gri-monoton bırakmak için gerekçe olarak kullanılmaz.

---

## 3. Tipografi (Somut Değerler) 🛠️

`04-style.md` Bölüm 3'teki 5 kademeli ölçeği (sayfa başlığı / panel başlığı / gövde / yardımcı metin / sayısal vurgu) somut değerlere bağlar. **`04-style.md`'nin "en fazla 2 font ağırlığı" kuralı korunur** — burada yalnızca normal ve semibold/bold kullanılır, üçüncü bir ağırlık eklenmez.

| Seviye                                  | Boyut                                     | Ağırlık                                        |
| --------------------------------------- | ----------------------------------------- | ---------------------------------------------- |
| Sayfa başlığı (H1)                      | 36px                                      | Semibold/Bold                                  |
| Panel başlığı (H2)                      | 28px                                      | Semibold                                       |
| Gövde                                   | 16px                                      | Normal                                         |
| Yardımcı/etiket metni                   | 13-14px                                   | Normal, soluk renk                             |
| Sayısal vurgu (asker/para/bölge sayacı) | 20-24px (bağlama göre; HUD'da daha büyük) | Semibold, `font-variant-numeric: tabular-nums` |

**Neden tabular-nums:** Sayı her değiştiğinde (ekonomi tick'i, savaş sonucu) rakamların genişliği sabit kalır, panel zıplamaz — bu doğrudan "en net şekilde görülebilir HUD" UX hedefini destekler.

---

## 4. İkonografi 🛠️

- Mevcut ikon kütüphanesi (`lucide-react` — proje zaten kullanıyorsa) korunur, yeni kütüphane eklenmez.
- **Stroke (outline) ikon** varsayılan; **filled ikon yalnızca** aktif/seçili durumu göstermek için kullanılır (ör. seçili bölge, aktif tab, seçili menü öğesi).
- Stroke width: tek sabit değer, proje genelinde değişmez.
- Boyut: yalnızca 2 sabit ölçek — küçük (buton/label yanı) ve orta (panel başlığı yanı); üçüncü bir boyut icat edilmez.
- Badge içindeki ikon yalnızca durumu pekiştirmek için kullanılır (ör. onay ikonlu "Confirmed" badge), dekoratif amaçlı değildir.
- `04-style.md` Bölüm 5 "İkon" kuralları (yalnızca metni desteklemek için, satırda tek ikon, tek başına anlam taşımaz) aynen geçerlidir — bu bölüm yalnızca stil/boyut ekler.

---

## 5. Gradient Kuralları 🛠️

Soft his büyük ölçüde buradan gelir ama disiplinli uygulanır:

- Gradient yalnızca şu iki yerde kullanılır: **(a)** dekoratif zemin (landing hero, boş-state arka planı — radial, çok düşük opaklık), **(b)** Primary CTA butonunun hover/active state'i (ince, tek yönlü).
- **HUD, harita, oyuncu kimlik renkleri, para/asker sayaçları üzerinde gradient kullanılmaz** — okunabilirlik risk altına girmez.
- Kart arka planında gradient yalnızca çok düşük opaklıkta (~%3-5) kullanılabilir, metin kontrastını asla etkilemez.

---

## 6. Surface / Derinlik Dili 🛠️

UI tamamen düz (flat) görünmez, ama neumorphism/aşırı 3D kabartma da kullanılmaz:

- Kartlar arka plandan **hafif** ayrışır (ince `--shadow-soft` ile) — belirgin, ağır bir gölge değil.
- Hover'da yalnızca küçük bir elevation artışı olur (gölge bir kademe derinleşir); büyük bir sıçrama/yükselme yok.
- "Floating panel" hissi vardır (kartlar zemine yapışık değil, üzerinde duruyor gibi hissettirir), ama neumorphism (basılı/kabartma görünümü) **kullanılmaz**.
- Cam efekti (glassmorphism, blur+opaklık) **yalnızca dekoratif alanlarda** ve düşük opaklıkta kullanılabilir (ör. landing hero üzerindeki dekoratif bir panel); **HUD ve oyun bilgileri (para, asker, bölge sayacı) üzerinde asla kullanılmaz** — Bölüm 5'teki gradient kısıtıyla aynı gerekçe: okunabilirlik risk altına girmez.

## 7. Motion Language 🛠️

`04-style.md`'deki tek "~150ms" değerini bu doküman somutlaştırır (bkz. `--transition-soft`):

- **Hover:** renk/gölge değişimi, ~150-200ms ease-out.
- **Press/click:** hafif scale (~%97-98), ~100ms.
- **Dialog/modal:** açılışta fade + hafif yukarı kayma (~200ms), kapanışta tersi.
- **Toast/bildirim:** kenardan kayarak girer, birkaç saniye sonra fade ile çıkar.
- **Sayfa geçişi:** route değişiminde ağır animasyon kullanılmaz (oyun/ekonomi verisi hızlı görünmeli) — yalnızca fade.
- **Loading:** veri bekleyen kart/panel alanlarında spinner değil **skeleton** kullanılır — `04-style.md` Bölüm 14 "Empty/Error/Loading States" ile uyumlu.
- **Mikro-eğlence / başarı hissi:** Başarı hissi oluşturan anlarda (maç kazanma, ödeme onayı, oda dolması, bölge ele geçirme gibi) küçük ama kontrollü görsel geri bildirimler kullanılır — tek, kısa, bir kerelik bir efekt (ör. ikon üzerinde tek pulse, kısa bir scale/parlama). Bu efektler **hiçbir zaman** dikkat dağıtıcı veya sürekli/döngüsel animasyona dönüşmez; amaç oyunu canlı tutmak, "soft modern" tonun oyunu duygusuzlaştırmasını engellemektir.
- Mevcut bir animasyon kütüphanesi (`framer-motion` vb.) projede zaten yoksa, bu geçişler CSS transition ile uygulanır; yeni bağımlılık eklenmez.

---

## 8. İllüstrasyon / Asset Kısıtları 🔒

Müşteri maskot istemedi; bu doküman bunu kapsamlı hale getirir:

- Maskot/karakter illüstrasyonu kullanılmaz.
- Emoji, gerçek fotoğraf, stok illüstrasyon, 3D render kullanılmaz.
- Oyuncu avatarı: cartoon/çizgi karakter değil, geometrik/baş harf tabanlı basit avatar.
- Boş state'lerde (ör. "henüz maç yok") yalnızca basit SVG çizgi ikon + kısa metin kullanılır, dekoratif illüstrasyon eklenmez.

---

## 9. Ekran Önceliklendirmesi ve Aşama Sırası 🛠️

`01-workflow-rules.md` Bölüm 0.1 uyarınca tek seferde her şey yapılmaz. Sıra:

1. **Aşama 1 — Token temeli:** `globals.css` + Tailwind token güncellemesi (renk, radius, shadow, transition). Build al, mevcut sayfaların kırılmadan yeni token'larla render olduğunu doğrula.
2. **Aşama 2 — Temel shadcn bileşenleri:** `Button`, `Card`, `Badge`, `Input`, `Dialog` görsel varyantları + tipografi/ikon/surface-derinlik kuralları. Bunlar en çok tekrar eden bileşenler olduğu için önce buradan geçince tüm ekranlara otomatik yansır.
3. **Aşama 3 — Oyun HUD'u** (`components/game/Hud.tsx`, `ActionPanel.tsx`): para/asker/bölge durumunun en net okunduğu panel, `tabular-nums` sayısal vurgu burada devreye girer.
4. **Aşama 4 — Ana menü / Lobi ekranları** (`/`, `/lobi`).
5. **Aşama 5 — Ekonomi paneli** (para akışı, cüzdan, ödeme durumu görselleri — `/cuzdan`, `/odeme/[invoiceId]` görsel katmanı, **iş mantığı hariç**).
6. **Aşama 6 — Skor tablosu / sonuç ekranları** (`/gecmis`, maç sonu paneli, success animasyonu burada devreye girer).

Her aşama sonunda `npm run build`, sonra tarayıcıda görsel doğrulama (`01-workflow-rules.md` 0.8/0.9 aynen geçerli).

---

## 10. Kabul Kriterleri (Definition of Done)

- Her aşama build hatasızdır ve önceki aşamaları bozmamıştır.
- Hiçbir `.cs` dosyası, hiçbir `lib/*` iş mantığı dosyası değişmemiştir (git diff ile doğrulanır — `01-workflow-rules.md` 0.6).
- Oyuncu kimlik renkleri, Danger/Success/Warning ayrımı, kontrast kuralları ve 2-ağırlık tipografi sınırı `04-style.md`'deki gibi korunmuştur.
- Para/asker/bölge sayıları her ekranda en az önceki kadar (tercihen daha) okunaklıdır.
- Görev sonunda `01-workflow-rules.md` 0.14 formatında rapor sunulur: değişen dosyalar, hangi token'ların yeni eklendiği, `04-style.md` ile hangi noktalarda kasıtlı farklılaştığı.

---

## ❓ Müşteriden Doğrulanması Gereken Noktalar

- Dark mode gerekli mi, yoksa yalnızca açık/pastel tema mi kalıcı olacak? 🛠️ Varsayım: şimdilik yok, yalnızca istenirse eklenir.
- Marka/logo rengi var mı — pastel paletin bir yerde bu renge sabitlenmesi gerekir mi? 🛠️ Varsayım: yok, palet Bölüm 2'deki gibi serbest pastel aile.
- Projede zaten bir animasyon kütüphanesi (`framer-motion` vb.) var mı? 🛠️ Varsayım: yok, Bölüm 6'daki geçişler CSS transition ile uygulanır.
