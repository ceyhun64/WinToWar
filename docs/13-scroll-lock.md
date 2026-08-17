# Claude Code Görev Talimatı: Global Scroll Kilidi ve İçerik Kartları — v2

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla ilerle, kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ⚙️ **ÇALIŞMA DAVRANIŞI KURALLARI:** Süreç kuralları.
- ❓ Müşteriden ileride doğrulanması gereken nokta — asla "dur ve sor" anlamına gelmez, yanında zaten uygulanmış bir 🛠️ taşır.

> 🔒 **Müşteri talimatı (bu turda verildi):** (1) **Hiçbir sayfa** `body`/`window` seviyesinde aşağı kaydırılabilir olmayacak — sayfa/tarayıcı hiç scroll etmeyecek. (2) **Metin ağırlıklı sayfalarda** içerik bir **kart (card)** içinde gösterilecek ve kaydırma o kartın **içinde** olacak — dış sayfa sabit kalır, yalnızca kart içeriği kayar.
>
> 🔒 **v2 — "Web oyunu viewport kuralı" olarak netleştirildi:** Tarayıcı viewport'u (`html`/`body`/`window`) **hiçbir route'ta** bir scroll container değildir — ne dikey ne yatay. Taşan içerik yalnızca bilinçli olarak belirlenmiş **iç panel/kartlarda** kayar. Bu, v1'deki landing (`/`) istisnasının kaldırılması dahil (Bölüm 1.3), tüm siteye istisnasız uygulanır.

Bu, `04-style.md`'nin genel visual dilini bozmaz — yalnızca sayfa iskeletinin (layout shell) scroll davranışını değiştirir. Sayfa metinleri/içerik blokları (`08-page-content.md`) bu görevle **değişmez**, yalnızca hangi kapsayıcının içine yerleştirildiği değişir.

---

## 0. ÇALIŞMA DAVRANIŞI KURALLARI ⚙️

### 0.0 Önce mevcut sistemi analiz et — tekrar üretme

- `web/app/globals.css` — mevcut `html`/`body` kuralları.
- `web/app/layout.tsx` (`RootLayout`) — `Header`/`Footer`'ın şu an nasıl yerleştirildiği, sabit yükseklikli mi.
- `web/components/layout/Header.tsx`, `Footer.tsx` — 🛠️ **v2'de netleştirildi:** gerçek yükseklikleri **doğrulanır** (sabit `h-*`/`py-*` mi, yoksa içerik/satır sayısına göre büyüyebilen bir yapı mı — ör. footer'daki link listesi mobilde satır sarıyorsa yüksekliği değişir). Bölüm 2.2'deki iskelet, `Header`/`Footer`'ın **sabit** kaldığı varsayımına dayanır; büyüyebilen bir yapı bulunursa bu, `main`'in kalan alanını (`flex-1`) yanlış hesaplamasına yol açar — böyle bir durum tespit edilirse (mevcut tasarım/metin değiştirilmeden) `Header`/`Footer`'a sabit bir `min-h`/`h` verilir ya da içerik taşması `Header`/`Footer`'ın kendi içinde `truncate`/responsive düzenlemeyle çözülür.
- `web/components/ui/card.tsx` — **shadcn `Card`/`CardHeader`/`CardContent` zaten mevcut**; Bölüm 2.3'teki kart ihtiyacı için **bu component kullanılır, yeni bir kart component'i icat edilmez** (`01-workflow-rules.md` Bölüm 0.2 duplicate yasağı).
- Bölüm 1.2'deki her route'un mevcut `page.tsx`'i — içeriğin şu an nasıl saramladığı (`PageBackground.tsx`, `PageHero.tsx`, `LegalPage.tsx` gibi mevcut layout component'leri varsa bunlar korunur, içine kart eklenir).
- `web/app/(site)/{giris,kayit,...}/layout.tsx` — SEO görevinde (`12-seo.md`) eklenen "companion layout" deseni; bu görev o deseni bozmaz, üzerine inşa eder.

### 0.1 Sıra

1. `globals.css` — global scroll kilidi (Bölüm 2.1).
2. `RootLayout` — flex iskelet (`header` sabit / `main` esnek / `footer` sabit) (Bölüm 2.2).
3. Bölüm 1.2'deki "metin ağırlıklı" sayfalara `Card` sarmalayıcı ekleme (Bölüm 2.3) — sayfa sayfa, her birinde build alarak ilerle (`01-workflow-rules.md` Bölüm 0.1 aşamalı ilerleme).
4. "Uygulama ekranları" grubunda (Bölüm 1.4) mevcut yapının zaten uyumlu olup olmadığını doğrula, gerekiyorsa yalnızca `main`'e `overflow-y-auto` ekle.
5. Mobil/klavye/responsive testleri (Bölüm 4).
6. Build + görsel doğrulama.

### 0.2 Ana projedeki kurallar geçerli

`CLAUDE.md` / `01-workflow-rules.md` / `04-style.md` / `06-coding-standards.md` aynen geçerlidir. Bu görev sayfa **metnini** değiştirmez, yalnızca kapsayıcı/layout yapısını değiştirir.

---

## 1. KAPSAM — HANGİ SAYFA HANGİ DAVRANIŞI ALACAK

### 1.1 Global kural — tüm site

🔒 Route ayrımı yapılmaksızın **her sayfada** `body`/`window` scroll'u kapalıdır (Bölüm 2.1). Aşağıdaki ayrım yalnızca "taşan içerik nereye/nasıl kaydırılacak" sorusunu cevaplar — "kaydırılabilir mi" sorusunu değil, o zaten hep "hayır (dışarıda)".

### 1.2 "Metin ağırlıklı" sayfalar — içerik `Card` içine alınır

🛠️ Bu grup, `12-seo.md`'nin Bölüm 1'deki "Indexlenir" route listesiyle örtüşür (o dosyada zaten "herkese açık, içerik taşıyan sayfalar" olarak tanımlanmıştı — aynı kategori burada da geçerli, ayrı bir liste icat edilmedi):

`/kurallar`, `/sss`, `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`, `/destek`

- İçerik, mevcut `LegalPage.tsx`/sayfa içi mevcut düzenin **içine** bir `Card` > `CardContent` ile sarmalanır (`components/ui/card.tsx`); kartın kendisi `flex-1 min-h-0 overflow-y-auto` alır, sayfanın geri kalanı (varsa `PageHero.tsx` başlık bloğu) kartın **dışında**, sabit kalır — yalnızca gövde metni kartın içinde kayar. Bu, uzun bir yasal metnin başlığının sürekli görünür kalmasını sağlar.
- `/destek`: form + varsa `SupportTicket` geçmişi aynı kart deseniyle sarmalanır; formun kendisi (mevcut `use client` yapısı, `12-seo.md`'de zaten "companion layout" ile ele alınmıştı) bu görevle bozulmaz. 🛠️ **v2'de eklenen guard — iç içe kart yasağı:** Mevcut form zaten kendi `Card`'ı içindeyse (`components/ui/card.tsx` ile sarmalanmışsa), bu görevle **ikinci bir dış `Card` eklenmez** — mevcut `Card`, Bölüm 2.3'teki `flex-1 min-h-0` + `CardContent`'in `overflow-y-auto` deseniyle **uyarlanır**, `Card > Card` gibi iç içe bir yapı oluşturulmaz.

### 1.3 Landing (`/`) — 🛠️ v2'de istisna kaldırıldı

> ⚠️ **v1'deki hata:** `/` için "kendi `main`'i içinde normal şekilde kaysın" istisnası, Bölüm 1.1'deki "hiçbir sayfa aşağı kaydırılmayacak" 🔒 talimatıyla **çelişiyordu** — kullanıcı mouse wheel ile `/`'i kaydırdığında, teknik olarak `body` değil `main` kaysa da, kullanıcı deneyimi açısından "sayfa hâlâ kayıyor" hissi aynen kalıyordu. v2 bunu düzeltiyor.

- 🛠️ **v2 karar:** `/` de Bölüm 1.1'deki genel kurala tabidir — `main` de dahil, hiçbir seviyede scroll olmaz. Landing içeriği (`Hero.tsx`, `BattleScene.tsx`, `FloatingCards.tsx`, `StatsPanel.tsx`, `Navbar.tsx`) viewport'a **sığacak şekilde** düzenlenir (yükseklikler `dvh`/`%` bazlı, `flex`/`grid` ile paylaştırılır).
- 🛠️ **Önce ölç, sonra karar ver:** Claude Code, `/`'in mevcut (kart-öncesi) toplam içerik yüksekliğini gerçek tarayıcıda/DevTools'ta **ölçer**. İçerik viewport'u makul bir marjin dahilinde aşıyorsa (ör. bölümler arası boşluk/padding küçültülerek sığdırılabiliyorsa) içerik **kesilmeden**, yalnızca spacing/boyutlandırma ayarlanarak viewport'a oturtulur — metin/görsel/CTA **silinmez veya gizlenmez** (`08-page-content.md`'deki onaylı blok sırası korunur). Eğer içerik gerçekten viewport'a sığdırılamayacak kadar uzunsa (ör. çok sayıda bölüm, mobilde stack olan kartlar), bu durumda **tek olası istisna** landing'in `main`'ine iç scroll verilmesidir — ama bu, "tasarım gereği" değil "içerik gerçekten sığmıyor" kanıtına dayanan, son çare bir 🛠️ karardır ve görev sonu raporunda hangi ölçümün bu karara yol açtığı **belirtilir**.
- ❓ Bu ölçüm sonucu `/`'in içeriğinin daraltılması/yeniden düzenlenmesi gerekirse (ör. mobilde `FloatingCards` bölümünün küçültülmesi), bu görsel bir tasarım kararına dönüşebilir — mevcut tasarım onaylıysa (`10-ui-redesign.md`) müşteriye bu spesifik değişiklik ayrıca doğrulatılmalı.

### 1.4 "Uygulama ekranları" — kart gerekmez, mevcut panel yapısı korunur

🛠️ `/lobi`, `/lobi/vip-olustur`, `/lobi/[inviteToken]`, `/cuzdan`, `/profil`, `/hesap-ayarlari`, `/gecmis`, `/mac/[matchId]`, `/odeme/[invoiceId]`, `/admin`, `/admin/*` — bu sayfalar zaten uzun düz metin değil, panel/liste/form tabanlı UI'lar (`GameCard.tsx`, `RoomCard.tsx`, `InvoiceRow.tsx` vb. mevcut component'ler). Bunları ayrıca bir `Card` içine sarmalamak (özellikle `admin/*`'teki tabloların kendi iç scroll'u zaten olabilir) gereksiz bir katman olurdu (YAGNI). Bunun yerine yalnızca sayfanın `main` alanı Bölüm 2.2'deki `flex-1 min-h-0 overflow-y-auto` deseniyle kendi içinde kayar — sarmalayıcı `Card` **eklenmez.**

### 1.5 `/game/[matchId]` — zaten header/footer'sız

Bu sayfa `07-pages.md`'ye göre zaten header/footer taşımıyor, tam ekran HUD (`100vw × 100dvh` mantığında). Bölüm 2.2'deki global iskelet bu sayfaya uygulanmaz (zaten kendi tam ekran düzeni var); yalnızca Bölüm 2.1'deki `html`/`body` kilidi (zaten global) burada da geçerli, ek bir değişiklik gerekmez.

🔒 **v2'de eklenen kısıt:** Bu görev, `GameMap.tsx`/`RegionNode.tsx`/`ActionPanel.tsx`/`Hud.tsx` içindeki mevcut pointer/touch/drag davranışını (harita sürükleme, bölge tıklama, dokunmatik hareketler) **değiştirmez**. `overscroll-behavior`/`overflow` değişiklikleri global (`html`/`body`) seviyede kalır — oyun canvas/HUD alanının kendi `touch-action`/pointer event handler'larına **dokunulmaz**; global scroll kilidinin oyun input'larını yanlışlıkla bozmadığı ayrıca test edilir (Bölüm 3).

### 1.6 `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla/[token]`, `/bakim`

🛠️ Bunlar kısa formlar; normal şartlarda viewport'a sığar, taşma riski düşüktür. Tutarlılık için Bölüm 2.2'deki genel iskelet (`main` `flex-1 min-h-0 overflow-y-auto`) uygulanır ama ayrı bir `Card` sarmalayıcı **eklenmez** (form zaten kendi içinde bir kart görünümünde olabilir, mevcut tasarımı — `11-auth.md` görevinde oluşturulan/genişletilen form UI'ları — bozmayacak şekilde, yalnızca dış scroll davranışı standartlaştırılır). 🔒 **v2'de netleştirilen hedef:** Bu sayfalarda `main`'in `overflow-y-auto`'su yalnızca bir güvenlik ağıdır (normal şartlarda hiç tetiklenmemesi beklenir) — asıl hedef, formun **hiç scroll gerektirmeden** viewport'a sığmasıdır; Claude Code bu sayfaları da viewport'a sığacak şekilde düzenler, "zaten sığıyor, dokunmaya gerek yok" varsayımıyla atlamaz.

---

## 2. TEKNİK UYGULAMA

### 2.1 `web/app/globals.css` — global kilit

```css
html,
body {
  height: 100dvh; /* 100vh değil — mobilde adres çubuğu kaymasını önler */
  overflow-x: hidden;
  overflow-y: hidden;
  overscroll-behavior: none; /* iOS "rubber band" kaydırmayı da engeller */
}
```

🛠️ **v2'de netleştirildi:** `overflow: hidden` kısayolu yerine `overflow-x`/`overflow-y` **ayrı ayrı** yazılır — müşterinin talimatı yalnızca dikey ("aşağı kaydırma") kaydırmayı hedeflese de, taşan bir görsel/component yanlışlıkla yatay bir scrollbar da oluşturabilir (ör. bir kartın `padding`/`margin` hesaplaması viewport genişliğini aşarsa); bu görevde **her iki yön de** kapatılır, tutarlı bir "viewport hiçbir yönde kaymaz" ilkesi sağlanır.

🛠️ `100dvh` kullanımı — `100vh` mobilde tarayıcı UI'ı (adres çubuğu) açılıp kapandıkça yanlış hesaplanır, içeriğin altını keser; `dvh` (dynamic viewport height) bunu düzeltir. Next.js'in desteklediği tarayıcı hedef kitlesinde (`04-style.md`'nin "mobil uyumlu" talimatıyla tutarlı) güncel bir CSS birimi olarak güvenle kullanılabilir.

### 2.2 `RootLayout` — flex iskelet

```tsx
// web/app/layout.tsx
<body className="h-dvh overflow-hidden flex flex-col">
  <Header /> {/* sabit yükseklik */}
  <main className="flex-1 min-h-0 overflow-y-auto">{children}</main>
  <Footer /> {/* sabit yükseklik */}
</body>
```

- `min-h-0` **kritiktir** — flex child'ın içeriği taşırıp `overflow: hidden`'ı etkisiz kılmasını önleyen standart Flexbox tuzağı düzeltmesi; atlanırsa Bölüm 2.1'deki kilit pratikte çalışmaz.
- 🛠️ **Çifte scroll çakışması — netleştirildi:** Bölüm 1.2'deki kart-sarmalı sayfalarda, `main` **kendisi** `overflow-y-auto` **kalmaya devam eder** (bu, kart taşmadığı sürece sorun çıkarmaz — kart zaten `main`'e sığacak şekilde `h-full`/`flex-1` alır) **ama** gerçek kaydırma pratikte kartın `CardContent`'inde gerçekleşir çünkü kartın dışında (başlık bloğu hariç) kaydıracak başka içerik yoktur. İki ayrı scroll container'ın **aynı anda, aynı yönde, görünür şekilde** aktif olmadığından (yalnızca biri gerçekten taşıyor) emin olunur — test edilir (Bölüm 4).

### 2.3 Kart deseni (Bölüm 1.2 sayfaları için)

```tsx
import { Card, CardContent } from "@/components/ui/card";

<div className="flex flex-col h-full">
  <PageHero title="..." /> {/* sabit, kartın dışında */}
  <Card className="flex-1 min-h-0">
    <CardContent className="h-full overflow-y-auto">
      {/* mevcut sayfa metni/blokları — değişmeden buraya taşınır */}
    </CardContent>
  </Card>
</div>;
```

Mevcut `components/ui/card.tsx`'in `Card`/`CardContent` export'ları kullanılır; yeni bir stil/varyant **icat edilmez** (`04-style.md`'nin "Component Usage Rules" ilkesiyle tutarlı — dokümanda tanımlı bileşen varken özel bir tane yazılmaz).

---

## 3. TEST / KABUL KRİTERLERİ

- [ ] Hiçbir sayfada `body`/`window` scroll edilebilir değil (tarayıcı DevTools'ta `document.scrollingElement.scrollHeight === document.scrollingElement.clientHeight` kontrolü ile doğrulanabilir) — **hem dikey hem yatay** yönde.
- [ ] Bölüm 1.2'deki her sayfada içerik bir `Card` içinde, kart kendi içinde kayıyor, sayfa başlığı (varsa) sabit kalıyor.
- [ ] Bölüm 1.4'teki uygulama ekranlarında `main` kendi içinde kayıyor, ek bir kart katmanı **eklenmemiş.**
- [ ] `/` (landing) **v2'de değişti:** `body`/`main` hiçbirinde scroll yok; içerik viewport'a sığdırılmış. Sığdırılamadığı için iç scroll bırakıldıysa, bu karar ve dayandığı ölçüm görev sonu raporunda **açıkça belirtilmiş.**
- [ ] `/game/[matchId]` davranışı değişmemiş (zaten header/footer'sız, tam ekran) **ve** harita/HUD'daki pointer/touch/drag input'ları global scroll kilidinden **etkilenmemiş** (gerçek bir maçta harita sürükleme/bölge tıklama test edilerek doğrulanmış).
- [ ] `/destek`'te (veya mevcut `Card` kullanan başka bir sayfada) iç içe (`Card > Card`) bir yapı oluşmamış.
- [ ] Mobilde (gerçek cihaz veya DevTools mobil emülasyonu) klavye açıldığında `/giris`/`/kayit` form alanları viewport dışında kalmıyor, `overflow: hidden` input'a scroll ile ulaşmayı engellemiyor.
- [ ] `overscroll-behavior: none` sonrası iOS'ta sayfa kenarlarında "rubber band" efekti kalmamış.
- [ ] Çifte scrollbar (aynı anda hem `main` hem `Card` görünür şekilde kayan) hiçbir sayfada yok.
- [ ] `Header`/`Footer` gerçekten sabit yükseklikte (mobil genişlikte de) — değilse Bölüm 0.0'daki düzeltme uygulanmış.
- [ ] `npm run build` geçiyor.

---

## 4. ❓ MÜŞTERİDEN DOĞRULANMASI GEREKEN NOKTALAR

- Landing (`/`) içeriği ölçüm sonucu viewport'a sığdırılamayıp bir iç scroll bırakılmak zorunda kalınırsa (Bölüm 1.3), bu durumun kabul edilebilir olup olmadığı.
- Landing'in viewport'a sığdırılması için mevcut onaylı tasarımda (`10-ui-redesign.md`) spacing/boyut küçültmesi gerekirse, bu görsel değişikliğin onaylanması.
- `/admin/*`'teki tabloların (zaten kendi iç scroll'u olabilir, ör. `table.tsx`) ayrıca bir `Card` sarmalayıcıya ihtiyacı olup olmadığı — v1'de "gerekmez" (Bölüm 1.4) varsayıldı, admin ekranları için farklı bir tercih varsa ayrıca belirtilmeli.
