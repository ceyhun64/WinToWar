# WinToWar — Oyun Ekranı Görsel Yenileme (v2)

## Görev

Mevcut WinToWar projesinde **yalnızca oyun içi maç ekranının** (`/game/[matchId]`) görsel/UI kalitesini yenile. Hedef: profesyonel, modern, playful, temiz, oynanası bir maç ekranı.

State.io yalnızca **akıcılık ve sadelik hissi** açısından referanstır; hiçbir oyunun UI'ı birebir kopyalanmaz.

Bu bir **presentation/UI refresh** görevidir. Oyun mekaniği, state yönetimi, SignalR davranışı, ekonomi, saldırı/dispatch mantığı ve API değişmez.

---

## 0) Önce kuralları oku

`CLAUDE.md` → `docs/01-workflow-rules.md` → `docs/02-architecture.md` → `docs/06-coding-standards.md` → `docs/04-style.md` → `docs/10-ui-redesign.md` → `docs/13-scroll-lock.md` → `docs/03-game-rules.md`.

`03-game-rules.md` yalnızca mevcut mekanikleri anlamak ve onlara **dokunmamak** için okunur.

Çelişki halinde `CLAUDE.md`'deki öncelik sırası geçerlidir. Estetik/renk kararlarında bu görev tanımı, fonksiyon/yapı kararlarında `04-style.md` kazanır.

### Aşama sonunda durma — açık istisna

`01-workflow-rules.md` Bölüm 0.5 "asla soru sorup bekleme" kuralı **karar soramazsın** demektir; bu görevde geçerliliğini korur. Buna ek olarak, bu görevde **her aşamanın sonunda durup rapor vereceksin ve bir sonraki aşamaya benim talimatımla geçeceksin.** Bu, hub'ın öncelik sırasındaki 1. madde (kullanıcının o anki açık talimatı) uyarınca bilinçli bir istisnadır ve çelişki sayılmaz.

Yani: **karar sorma, karar ver ve raporla — ama aşama sınırında dur.**

---

## 1) Git disiplini (zorunlu)

Kod değiştirmeye başlamadan önce:

```bash
git status                       # temiz olmalı; değilse bana bildir ve dur
git checkout -b game-ui-refresh
```

Her aşama sonunda, build yeşilse:

```bash
git add -A && git commit -m "game-ui: asama-N — <kısa özet>"
```

Commit mesajını raporda belirt. Aşamalar arasında `git reset --hard`, `git checkout .`, force push veya branch silme **yapma**.

---

## 2) Kesin sınırlar

**Dokunabileceğin dosyalar:**

- `web/app/game/layout.tsx`
- `web/app/game/[matchId]/page.tsx`, `loading.tsx`, `error.tsx`
- `web/components/game/*` (ActionPanel, ArmyLayer, DevFpsOverlay, GameMap, Hud, RegionNode, TerritoryControlBar, TroopMarker)
- `web/lib/game/colors.ts`, `web/lib/game/arrow.ts` — yalnızca görsel/geometri; state veya kural mantığı yok
- `web/app/globals.css` — **yalnızca ekleme**, yeni tokenlar `--game-*` namespace'inde. Mevcut global token değerleri değişmez.
- `web/components/ui/*` — mevcutları kullan, **yeni component oluşturma**

**Kesinlikle dokunma:** `api/**`, `web/lib/game/store.ts`, `signalr-client.ts`, `api.ts`, `types.ts`, `web/lib/payments/**`, `auth/**`, `admin/**`, `landing/**`, `lobby/**`, `GameConfig.cs`, migration'lar, ödeme, authentication, backend, database.

**Sızıntı kontrolü (kritik):** `web/lib/game/colors.ts` ve `arrow.ts` kapsam dışı sayfalardan da import ediliyor olabilir. Değiştirmeden önce import edenleri tara:

```bash
cd web && grep -rn "lib/game/colors\|lib/game/arrow" --include=*.ts --include=*.tsx .
```

Oyun ekranı dışından import eden bir yer varsa: mevcut export imzalarını ve varsayılan davranışı **koru**, yeni görsel değerleri ek export olarak ver. Kapsam dışı bir sayfanın görünümünü değiştirmek bu görevde ihlaldir; böyle bir durum çıkarsa raporda ayrıca belirt.

**Mevcut çalışan UI/state mimarisini çöpe atıp yeniden kurma.** Bu bir re-skin'dir, rewrite değil. Bir componenti baştan yazmak zorunda kaldığın her durumu gerekçesiyle raporla.

---

## 3) Değişmeyecek davranışlar

SignalR event isimleri ve payload'ları, DTO alanları, store davranışı, saldırı ve dispatch mantığı, ekonomi tick'i, asker sayıları, bölge sahipliği mantığı, maç state'i, route yapısı, oyun kuralları.

Görsel bir değişiklik bunlardan birini değiştirmeyi gerektiriyorsa **mekaniği değiştirme**; mevcut davranışı koruyan görsel çözüm üret.

> Görsel karar verme özgürlüğü ≠ oyun mekaniğini değiştirme özgürlüğü.

---

## 4) Öncelik zinciri (çakışmada bu sıra karar verir)

**oynanabilirlik > okunabilirlik > performans > görsel efekt**

**asker sayıları > harita > aksiyonlar > HUD > dekorasyon**

Bir efekt bu sıradan herhangi birini geriletiyorsa efekt gider.

---

## 5) Stil yönü

**Karakter:** modern-playful. Canlı, temiz, hafif oyuncak hissi veren ama profesyonel ve hızlı.

**Kullan:** yumuşak pastel/canlı zeminler, yüksek kontrastlı takım renkleri, yuvarlak geometri, kontrollü radius, kısa mikro animasyonlar (120–220 ms), hafif gölge ve iç ışıma, güçlü tipografik hiyerarşi, net hizalama, temiz spacing.

**Kaçın:** hacker/neon ve cyberpunk estetiği, ağır gradient, aşırı texture, kalın border, particle efektleri, aşırı glow, ikon kalabalığı, her elementi karta koymak, "template dashboard" görünümü.

**Temel prensip:** ciddiyet = tipografi + hizalama + spacing; eğlence = renk + hareket + şekil.

### En önemli UX kuralı

Bir bölgeye bakıldığında ilk görülen şey **kaç asker olduğu** olmalı. Asker sayıları yüksek kontrastlı, `tabular-nums`, öngörülebilir genişlikte, arka plandan net ayrışan ve küçük ekranda okunabilir olmalı. Hiçbir görsel süsleme bu okunabilirliği azaltamaz.

### Sahiplik ve renk sistemi

12 oyuncuya kadar ayırt edilebilir, renk körlüğü açısından mümkün olduğunca güvenli, açık/koyu zeminde okunabilir bir takım paleti kur. Sahiplik **yalnızca renkle** gösterilmez — kenar, parlaklık, işaret/accent kombinasyonundan yararlan. Şu üç durum tek bakışta ayrışmalı: **benim bölgem / düşman bölgesi / nötr bölge.**

### Görsel hiyerarşi

- **Birincil:** harita, bölgeler, asker sayıları, seçili hedef, aktif dispatch
- **İkincil:** oyuncular, territory/skor, süre, ödül, action panel
- **Üçüncül:** bağlantı durumu, tooltip, küçük status mesajları, debug/FPS

HUD hiçbir zaman haritanın ve asker sayılarının önüne geçmez.

---

## 6) Aşamalı çalışma

Her aşamanın sonunda: build al → commit at → raporla → **DUR**.

```bash
cd web && NEXT_PUBLIC_SITE_URL=http://localhost:3000 npm run build
```

Build kırmızıysa: sonraki aşamaya geçme, düzelt, tekrar build al, sonucu raporla.

**Build yeşil olması ekranın iyi göründüğü anlamına gelmez.** Her aşama raporunun sonunda, ekranın hangi bölümüne bakmam gerektiğini ve neyi kontrol etmemi istediğini yaz (`npm run dev` ile bakacağım, ekran görüntüsüyle döneceğim). Gerekirse benim geri bildirimimle o aşamada revizyon yap, sonra ilerle.

### Aşama 0 — Envanter (kod değiştirme)

1. **Component sorumlulukları:** `web/components/game/*` altındaki her dosyanın hangi görsel/etkileşimsel öğeden sorumlu olduğu.
2. **Mevcut problemler:** ekranın neden amatör göründüğüne dair 8–12 **somut** madde. Her madde şu formatta: `dosya — element — problem — görsel/UX sonucu`. Genel ifade kullanma.
   *Örnek:* `RegionNode.tsx — asker sayısı — çevresindeki UI ile aynı görsel ağırlıkta — oyuncunun gözü önce neyi okuyacağını bilmiyor.`
3. **Görsel hiyerarşi:** mevcut ekranın birincil/ikincil/üçüncül katmanları.
4. **Sızıntı taraması:** `colors.ts` ve `arrow.ts`'yi oyun ekranı dışından import eden yerler (§2'deki grep sonucu).
5. **Risk analizi:** hangi componentler güvenle değiştirilebilir, hangilerinde state/mekanik davranışa yanlışlıkla dokunma riski var.

Build: çalıştırılmadı (salt analiz). Commit: yok. **DUR.**

### Aşama 1 — Görsel dil ve tokenlar

Renk rampaları, takım renkleri, radius, shadow, spacing, animasyon süresi/easing, map/node accent değerleri tek yerde tanımlanır (`colors.ts` + `globals.css` içinde `--game-*`). Mevcut global değişkenler değişmez. Bilgi taşımayan animasyon eklenmez. Renk körlüğü ve kontrast kontrol edilir. Build → commit → rapor → **DUR.**

### Aşama 2 — RegionNode + GameMap

Bölge düğümleri şu durumları net göstermeli: nötr, benim, rakibin, seçili, geçerli hedef, geçersiz hedef, komşu değil, hover, pressed, fog-of-war.

Node'lar kart gibi değil, haritanın doğal parçası gibi görünmeli; ağır border kullanılmamalı. Bağlantı çizgileri okunabilir ama dominant olmamalı, takım renkleriyle karışmamalı. Dokunmatik hedefler ≥ 44px. Build → commit → rapor → **DUR.**

### Aşama 3 — Etkileşim ve geri bildirim

`arrow.ts`, `ArmyLayer.tsx`, `TroopMarker.tsx`. Sürükleme oku: yön, kalınlık, uç, geçerli/geçersiz hedef ayrımı net; mevcut sürükleme davranışı değişmez. İptal yolu açık olmalı. Dispatch, hareket, çarpışma, varış, ele geçirme ve elenme için kısa, bilgi taşıyan mikro animasyonlar. Çok elemanlı katmanlarda CSS `transform`/`opacity` tercih edilir. Build → commit → rapor → **DUR.**

### Aşama 4 — HUD / ActionPanel / kabuk

`Hud.tsx`, `TerritoryControlBar.tsx`, `ActionPanel.tsx`, `app/game/layout.tsx`, `page.tsx`, `loading.tsx`, `error.tsx`.

HUD haritayı kapatmadan önemli bilgiyi sürekli erişilebilir tutmalı. Mobilde 360px genişlikte kontrol; safe-area/notch dikkate alınır; alt aksiyon alanı haritayı kapatmaz; scroll davranışı `13-scroll-lock.md` ile uyumlu kalır.

Görsel olarak ele alınacak state'ler: lobi/bekleme, bağlı, yeniden bağlanıyor, bağlantı koptu, elendin, maç bitti, ödül/ödeme bildirimi. Build → commit → rapor → **DUR.**

### Aşama 5 — Cila + performans

`prefers-reduced-motion`, 360px mobil, 1440px masaüstü, kontrast AA, overflow, z-index, touch target, animasyon performansı. Animasyonlarda öncelik `transform`/`opacity`; layout animasyonu üretme. 250 ms tick altında gereksiz React re-render oluşturma; ağır listelerde memo/CSS yaklaşımı. `ArmyLayer`'da çok sayıda öğe için framer-motion yerine CSS transform. `DevFpsOverlay` ile 60 FPS kontrolü. Final build → commit → final rapor.

---

## 7) Tasarım kararlarında özgürlük

Renk seçimi, radius, shadow, spacing, node şekli, HUD yerleşimi, easing, ikon kullanımı gibi detaylar için soru sorup bekleme. 🛠️ makul kararı ver, gerekçesini raporda açıkla. Bu özgürlük §3'teki davranış sınırlarını kapsamaz.

## 8) Yeni dependency yok

Mevcut stack yeterli: Tailwind v4, framer-motion, lucide-react, `@base-ui/react`, mevcut shadcn componentleri.

## 9) Dokümantasyon (tüm aşamalar bittikten sonra)

`docs/23-game-visual-refresh.md` oluştur; diğer docs dosyalarının biçimini ve 🔒/🛠️/❓ işaret sistemini birebir izle. İçerik: stil yönü, token tablosu, component sorumlulukları, bölge durum matrisi, animasyon envanteri ve süreleri, alınan 🛠️ kararlar, ❓ ile ileride doğrulanacak noktalar.

Sonra `CLAUDE.md`'nin "Her görevde önce oku" listesine ekle:
`Oyun ekranı görsel yenileme → docs/23-game-visual-refresh.md`
Öncelik: estetik/renk → 23, fonksiyon/yapı → `04-style.md`.

## 10) Aşama raporu formatı

```
1. Değişen dosyalar
2. Ne değişti (görsel olarak ne fark edilecek)
3. 🛠️ Varsayımlar + gerekçe
4. Build sonucu (komut / sonuç / hata) + commit hash
5. Benden kontrol etmemi istediğin şeyler (hangi ekran, ne bakılacak)
6. Sonraki aşama
```

Kod dökümü verme.

---

**Şimdi yalnızca AŞAMA 0 — ENVANTER ile başla. Kod değiştirme.**