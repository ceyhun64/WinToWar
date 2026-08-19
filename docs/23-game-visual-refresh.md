# 23 — Oyun Ekranı Görsel Yenileme (`/game/[matchId]`) 🔒 + 🛠️

Bu dosya, `docs/23-game-ui-refresh-v2.md` görev talimatının **uygulanmış sonucudur**. Talimat dosyası "ne yapılacak"tır ve arşiv olarak durur; bu dosya "ne yapıldı, hangi değer nerede, neden öyle"dir ve oyun ekranına dokunan sonraki her görevin **tek doğruluk kaynağıdır**.

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanır, sayılar/kurallar değiştirilmez.
- 🛠️ **MÜHENDİSLİK VARSAYIMI:** Netleştirilmemiş noktada verilmiş, gerekçeli karar.
- ❓ Müşteriden ileride doğrulanması gereken nokta — **asla "dur ve sor" anlamına gelmez**, yanında zaten uygulanmış bir 🛠️ taşır.

## Kapsam

Yalnızca **oyun içi maç ekranı**: `web/app/game/**`, `web/components/game/**`, `web/lib/game/colors.ts`, `web/lib/game/arrow.ts` ve `web/app/globals.css`'in `--game-*` bloğu.

🔒 **Değişmeyen ve bu dosya tarafından değiştirilemeyecek olan:** oyun mekaniği, `store.ts`, `signalr-client.ts`, `api.ts`, `types.ts`, `useArmyAnimation.ts`, SignalR event isimleri/payload'ları, DTO alanları, ekonomi tick'i, saldırı/dispatch mantığı, route yapısı, backend, ödeme, authentication.

## `04-style.md` / `10-ui-redesign.md` ile ilişki

Bu dosya, oyun ekranının **estetik/renk** kararlarında o iki dosyanın önüne geçer (bkz. `CLAUDE.md` öncelik sırası — bu dosya `04-style.md` ile aynı kademede, oyun ekranı özelinde daha spesifik olan kazanır). **Fonksiyon/yapı** kararlarında `04-style.md` kazanmaya devam eder.

`04-style.md`'nin oyun ekranı için geçerliliğini koruyan kuralları: odak göstergesi hiçbir yerde kaldırılmaz (Bölüm 13), Fog of War'ın "keşfedilmemiş alan" kavramı (Bölüm 10), hover'da sahiplik renginin değişmemesi (Bölüm 9).

---

## 1. Stil yönü 🔒

🔒 Müşteri talimatı: **modern-playful.** Canlı, temiz, hafif oyuncak hissi veren ama profesyonel ve hızlı.

- **Kullanılan:** canlı ama yumuşak takım renkleri, yuvarlak geometri, kontrollü radius, kısa mikro animasyonlar, hafif gölge, güçlü tipografik hiyerarşi, net hizalama.
- **Kaçınılan:** hacker/neon estetiği, ağır gradient, texture, kalın border, particle efektleri, aşırı glow, ikon kalabalığı, her elementi karta koymak, "template dashboard" görünümü.
- **Temel prensip:** ciddiyet = tipografi + hizalama + spacing; eğlence = renk + hareket + şekil.

### 1.1 🔒 En önemli UX kuralı — asker sayısı

Bir bölgeye bakıldığında **ilk görülen şey kaç asker olduğudur.** Asker sayıları yüksek kontrastlı, `tabular-nums`, öngörülebilir genişlikte, arka plandan net ayrışan ve küçük ekranda okunabilir olmalıdır. **Hiçbir görsel süsleme bu okunabilirliği azaltamaz.**

Bu kural, aşağıdaki her tasarım kararında tie-breaker olarak kullanılmıştır.

### 1.2 🔒 Öncelik zinciri

Çakışmada bu sıra karar verir:

```
oynanabilirlik > okunabilirlik > performans > görsel efekt
asker sayıları > harita > aksiyonlar > HUD > dekorasyon
```

Bir efekt bu sıradan herhangi birini geriletiyorsa **efekt gider.**

---

## 2. Token tablosu

### 2.1 İş bölümü — hangi değer nerede 🛠️

| Kaynak | İçerik | Neden |
|---|---|---|
| `web/lib/game/colors.ts` | **Takım renkleri**, nötr, fog, rozet tonları | Bu renkler üzerinde çalışma zamanında matematik yapılıyor (asker sayısına göre koyulaşma/açılma, rozet tonunun türetilmesi). Bir CSS değişkeni bunu yapamaz. |
| `web/app/globals.css` → `--game-*` | Yüzeyler, durum halkaları, ölçekler, hareket, sabit renkler | CSS'in kendi kullandıkları. |

⚠️ Aynı değerin iki kaynağı **yoktur ve olmamalıdır.** Takım renkleri CSS'e kopyalanmaz; yüzey/ölçek değerleri de `colors.ts`'e taşınmaz.

⚠️ `globals.css`'te bu görev kapsamında **yalnızca ekleme** yapılmıştır. Mevcut global token değerleri (`--background`, `--card`, `--radius`, `--primary`, `--destructive`, `.dark`, `.admin-theme`) **değiştirilmemiştir** ve değiştirilmeyecektir.

### 2.2 Yüzeyler

| Token | Değer | Kullanım |
|---|---|---|
| `--game-bg` | `#0a1524` | Oyun kabuğunun zemini |
| `--game-map-bg` | `#0d1b30` | Haritanın zemini — tüm takım renklerinin kontrastı **bu değere karşı** ölçülmüştür (min 4.78:1) |
| `--game-map-edge` | `rgba(7,14,26,0.5)` | Bölgeler arası sınır — bilinçli DÜŞÜK kontrast |
| `--game-panel` | `rgba(13,26,46,0.82)` | HUD/bant yüzeyi |
| `--game-panel-solid` | `#101f38` | Opak panel (hata mesajı — bkz. §6.3) |
| `--game-panel-border` | `rgba(255,255,255,0.08)` | |
| `--game-panel-border-strong` | `rgba(255,255,255,0.16)` | |

### 2.3 Tipografi renkleri

| Token | Değer | Ölçülen kontrast |
|---|---|---|
| `--game-text` | `#eaf1fb` | 16.13:1 (oyun zemini) |
| `--game-text-muted` | `#93a6c1` | 7.39:1 (oyun zemini), 7.11:1 (panel) |
| `--game-text-on-badge` | `#ffffff` | ≥7.7:1 (her rozet tonunda, garanti — bkz. §3.2) |

### 2.4 Durum halkaları — sahipliğin **ikinci kanalı** 🛠️

Renk körlüğünde 12 takım rengi tam ayrışamaz (ölçüldü, bkz. §3.1). Bu yüzden sahiplik **asla yalnızca renkle** gösterilmez; aşağıdaki halkalar **akromatik** (renkten tamamen bağımsız) bir ikinci kanaldır.

| Token | Değer | Anlam |
|---|---|---|
| `--game-ring-own` / `-width` | `rgba(255,255,255,0.92)` / `2px` | Benim bölgem (kalıcı) |
| `--game-ring-selected` / `-width` | `#ffffff` / `3px` | Seçili / sürükleme kaynağı |
| `--game-ring-hover` / `-width` | `#ffffff` / `3.5px` | Sürüklerken nişan alınan hedef |
| `--game-ring-target` / `-width` | `rgba(255,255,255,0.32)` / `1.25px` | Geçerli hedef (yalnızca sürükleme sırasında) |
| `--game-edge-width` | `1px` | Nötr/rakip varsayılan sınırı |
| `--game-badge-ring` / `-width` | `rgba(240,246,253,0.94)` / `2px` | Rozeti bölge dolgusundan ayıran halka |

⚠️ Bu kalınlıklar `vectorEffect="non-scaling-stroke"` ile çizilir; yani **harita ölçeğinden bağımsız gerçek ekran pikselidir.** 360px'te de 1440px'te de aynı kalınlıkta görünür — aksi halde mobilde sahiplik halkası görünmeyecek kadar incelirdi.

### 2.5 Ölçekler

| Grup | Tokenlar |
|---|---|
| Radius | `--game-radius-xs` 6px · `-sm` 10px · `-md` 14px · `-lg` 20px · `-pill` 999px |
| Gölge | `--game-shadow-sm` · `-md` · `-lift` · `-inset` |
| Boşluk | `--game-space-1` 4px · `-2` 8 · `-3` 12 · `-4` 16 · `-5` 24 · `-6` 32 |

### 2.6 Hareket

| Token | Değer |
|---|---|
| `--game-dur-instant` | 90ms |
| `--game-dur-fast` | 130ms |
| `--game-dur-base` | 170ms |
| `--game-dur-slow` | 220ms |
| `--game-ease-out` | `cubic-bezier(0.22, 1, 0.36, 1)` |
| `--game-ease-in-out` | `cubic-bezier(0.65, 0, 0.35, 1)` |
| `--game-ease-pop` | `cubic-bezier(0.34, 1.4, 0.64, 1)` |

### 2.7 Bileşen-özel sabit renkler

| Token | Değer | Kullanım |
|---|---|---|
| `--game-capture-wash` / `--game-capture-edge` | `#f2f6fc` / `#ffffff` | Ele geçirme darbesi |
| `--game-arrow-halo` / `--game-arrow-invalid` | `rgba(6,13,24,0.55)` / `#dce5f2` | Sürükleme oku |
| `--game-label-ink` / `--game-label-halo` | `#0e1726` / `rgba(255,255,255,0.72)` | Harita üstündeki bölge adı |
| `--game-troop-ring` | `rgba(240,246,253,0.85)` | Asker ikonunun ayırıcı halkası |
| `--game-dot-ring` | `rgba(0,0,0,0.35)` | HUD/panel kimlik noktalarının konturu |
| `--game-chip-own` | `rgba(255,255,255,0.09)` | HUD'da kendi çipimin yüzeyi |
| `--game-status-ok` / `--game-status-warn` | `#5fd198` / `#efc05a` | Bağlantı durumu (kırmızı bilinçli olarak paylaşılan `--destructive`) |

---

## 3. Takım rengi sistemi (`colors.ts`)

### 3.1 Palet nasıl seçildi 🛠️

Renk körlüğü ve kontrast **gözle değil ölçülerek** değerlendirilmiştir. Yöntem: her renk çifti için CIE-Lab ΔE, hem normal görüşte hem üç renk körlüğü tipinde (döteranopi/protanopi/tritanopi) simüle edilerek; ayrıca **güç-bazlı koyulaşma/açılma aralığının her kombinasyonu dahil edilerek** (yani ekranda gerçekten oluşabilen en kötü durum, sabit renkler değil).

Her modun en kötü renk çifti:

| Mod | Önce | Sonra |
|---|---|---|
| Practice (2) | 31.2 | **65.2** |
| Standart (4) | **1.8** | **6.9** |
| VIP (12) | **0.6** | **5.3** |
| Nötr ↔ en yakın oyuncu | 8.0 | 6.8 |
| Harita zeminine min kontrast | 4.14 | **4.78** |

Standart odadaki eski **1.8** değeri, döteranopide mavi ve morun **pratik olarak aynı renk** olduğu anlamına geliyordu.

⚠️ **VIP'te 12 rengin renk körlüğünde tam ayrışması matematiksel olarak mümkün değildir**; 5.3 ulaşılabilir tavana yakındır. Bu yüzden §2.4'teki akromatik ikinci kanal bir "ek özellik" değil, sistemin **zorunlu parçasıdır** — kaldırılamaz.

### 3.2 Palet

🔒 **Müşteri kararı korunmuştur:** Standart (4 kişilik) oda mavi-kırmızı-mor-yeşil, Practice (2 kişilik) mavi-kırmızı. Slot sırası ve renk kimlikleri aynıdır; yalnızca tonlar ölçüme göre canlandırılmıştır. Kırmızı hâlâ `--destructive`'ten ayrı, daha sıcak/mercan bir tondur.

| Slot | Renk | Ad |
|---|---|---|
| 0 | `#4F9BE0` | mavi |
| 1 | `#E8705C` | mercan |
| 2 | `#C77BEE` | menekşe |
| 3 | `#5FD198` | yeşil |
| 4 | `#E4E07E` | limon |
| 5 | `#C9822C` | kehribar |
| 6 | `#9EE891` | yaprak |
| 7 | `#8079E8` | indigo |
| 8 | `#E891DB` | orkide |
| 9 | `#CEC046` | hardal |
| 10 | `#91E8CA` | nane |
| 11 | `#E56CA8` | gül |

İlk 4 slot, en çok kullanılan modlar olduğu için **bilinçli olarak en fazla ayrışan dörtlüdür.**

- **Nötr:** `#B9C2CE` — düşük doygunlukta, hiçbir takım renginin ailesinde değil.
- **Fog of War:** `#3A4A68` — zeminden ~1.9:1 ayrışan koyu ton. 🛠️ Önceki değer (`#C7C7C7`) koyu haritada nötr bölgeden **daha parlaktı**, yani "bilinmiyor" durumu "sahipsiz"den daha çok dikkat çekiyordu.

### 3.3 🛠️ Rozet tonları türetilir, el ile yazılmaz

`toBadgeTone()` her takım renginden, hedef bağıl parlaklığa (`0.085`) ikili aramayla inerek rozet tonunu **türetir.** Böylece:

- Paletteki bir renk değiştiğinde rozet tonu otomatik takip eder.
- Beyaz asker sayısının kontrastı hiçbir slotta yanlışlıkla bozulamaz: **her slotta ≥7.7:1** (WCAG AAA, küçük metin eşiği 7:1).

AA (4.5) değil **AAA** hedeflenmiştir çünkü asker sayısı ekrandaki en önemli bilgi ve küçük puntodur.

**⚠️ Yeni bir renk eklenirken el ile bir "dark" dizisi YAZILMAZ** — `PLAYER_COLORS`'a eklemek yeterlidir.

### 3.4 🔒 Güç-bazlı renk modülasyonu

🔒 Müşteri kararı korunmuştur: bölge dolgusu sahiplik kimliğinin yanı sıra **o bölgedeki güncel asker sayısını da yansıtır** — sahipli bölgede asker arttıkça hafifçe koyulaşır, nötr toprakta savunma azaldıkça açılır.

🛠️ Aralık daraltıldı: koyulaşma tavanı `0.15 → 0.12`, nötr açılma `0.6 → 0.55`. Gerekçe ölçüme dayanır — bu aralık bir oyuncunun rengini başkasınınkine yaklaştırabiliyor; palet zaten aralığın tamamı hesaba katılarak seçilmiş olsa da daraltma en kötü çapraz çifti ölçülebilir şekilde iyileştirir ve efekt gözle görülür kalır.

---

## 4. Component sorumlulukları

| Dosya | Sorumluluk |
|---|---|
| `app/game/layout.tsx` | Header/Footer'sız kabuk + `noindex` metadata. Pass-through. |
| `app/game/[matchId]/page.tsx` | Sayfa iskeleti (§5), durum katmanları, menü, store bağlantısı. `GameShell` / `StatusBanner` / `OverlayPanel` / `CenteredMessage` **dosya-içi yerel yardımcılardır**, `components/ui/` altına hiçbir şey eklenmemiştir. |
| `components/game/GameMap.tsx` | SVG viewBox, katman sırası, sürükle-bırak, polygon hit-test, **etiket çapası hesabı** (§4.1), sahip değişimi flash'ı, varış geri sayımı state'i, renk çözümü |
| `components/game/RegionNode.tsx` | `RegionShape` (dolgu + durum kenarlığı) ve `RegionLabel` (rozet + bölge adı + varış geri sayımı) |
| `components/game/ArmyLayer.tsx` | Yapısal katman: hangi sevkiyatlar var, her birine hangi renk/nokta |
| `components/game/TroopMarker.tsx` | Tek bir sevkiyatın asker ikonları — tamamen imperatif `requestAnimationFrame` |
| `components/game/Hud.tsx` | Oyuncu şeridi (kaydırılabilir) + üretim/durum |
| `components/game/TerritoryControlBar.tsx` | Toprak oranı hapı |
| `components/game/ActionPanel.tsx` | Seçili bölge **bilgi** paneli (aksiyon içermez) |
| `components/game/DevFpsOverlay.tsx` | Dev-only FPS sayacı (sol alt) |
| `lib/game/colors.ts` | §3 |
| `lib/game/arrow.ts` | Sürükleme okunun saf geometrisi |

### 4.1 🛠️ Etiket çapası (pole of inaccessibility) — kritik

`map.json`'daki `region.x/y` **sınırlayıcı kutu merkezidir** ve içbükey bölgelerde kenara çok yakın düşer. Ölçüldü: Mersch'te merkezin en yakın kenara uzaklığı **6 birim**, Differdange'da 10.

Sonuç: asker sayısı rozeti bu bölgelerde (eski, daha küçük rozet boyutunda **bile**) kendi bölgesinin dışına taşıp komşunun alanına giriyordu — yani "bu sayı hangi bölgenin?" sorusu belirsizleşiyordu. §1.1 gereği kabul edilemez.

**Çözüm:** `labelAnchorForPolygon()` (GameMap), polygon'un **içinde kenarlardan en uzak** noktayı hesaplar.

| | Önce | Sonra |
|---|---|---|
| En düşük kaçış payı | 6 birim | **31 birim** |
| Rozet taşması | 3/12 (yeni boyut), 2/12 (eski boyut) | **0/12** |

Maliyet: harita başına **bir kez**, ölçüldü **5.3 ms**.

⚠️ **Bu tamamen bir SUNUM hesabıdır.** `map.json` değişmez, `region.x/y` olduğu gibi durur, oyun mantığı (komşuluk, hit-test, hareket süresi) etkilenmez.

⚠️ Çapa **sevkiyat uçları, sürükleme oku ve nabız halkası için de** kullanılır. Rozet bir yere, ordu başka yere gitseydi varış geri sayımı rozette oynarken askerler bazı bölgelerde 38 birim uzakta kaybolurdu.

### 4.2 ⚠️ Memo karşılaştırıcıları

Sunucu her tick'te **tüm** `MatchState`'i yeni obje referansıyla gönderir; varsayılan `React.memo` bu yüzden işe yaramaz. `RegionShape` ve `RegionLabel` **elle yazılmış** karşılaştırıcılar kullanır.

🔴 **Bu bileşenlere yeni bir görsel prop eklenirse karşılaştırıcıya da EKLENMELİDİR.** Unutulursa o prop değiştiğinde bölge yeniden çizilmez ve **bayat kalır** — sessiz, fark edilmesi zor bir hata.

### 4.3 ⚠️ Sürüklemede stale closure

`GameMap`'in sürükleme handler'ları içinde okunan her şey **`useRef` üzerinden** okunmalıdır, `useState` üzerinden değil. Kaynak `RegionShape`, memo karşılaştırıcısı callback'leri yok saydığı için sürükleme boyunca yeniden render olmaz; `useState` ile okunan bir değer sürüklemenin başladığı andaki değerde **donar**.

---

## 5. Sayfa iskeleti

```
GameShell            (h-full min-h-0 flex-col, --game-bg, safe-area padding, data-game-shell)
├── header           (shrink-0)  → Hud + menü butonu + TerritoryControlBar
├── main             (relative min-h-0 flex-1)
│   ├── div.absolute.inset-2 → GameMap
│   ├── StatusBanner    (overlay, üst)     → yeniden bağlanıyor / elendin
│   ├── ipucu pili      (overlay, alt)     → ilk saldırıya kadar
│   └── OverlayPanel    (overlay, ortada)  → lobi/geri sayım · maç bitti · iptal · bağlantı koptu
├── ActionPanel      (bottom sheet, max 52dvh)
└── hata toast'ı     (fixed, z-60)
```

🔒 **Değişmez kural: hiçbir durum göstergesi akışa girmez.** Hepsi harita alanının **üstüne** biner. Bir bağlantı uyarısının harita alanına mal olması §1.2'deki öncelik zincirine aykırıdır.

⚠️ **`min-h-0` zinciri kritiktir.** `body` `overflow-hidden`'dır (`13-scroll-lock.md`); `min-h-0` olmadan içerik kabı taşırır ve taşan kısım **sessizce kırpılır, erişilemez olur.**

⚠️ **Harita kabı `absolute inset-2`'dir, `h-full` değil.** Yüzde yükseklik, yüksekliği flex algoritmasından gelen bir öğenin içinde bazı tarayıcılarda **0'a çözülür.**

🛠️ Harita `preserveAspectRatio="xMidYMin meet"` ile **dikeyde üste** hizalanır. Harita ~kare olduğundan dikey boşluk yalnızca portre ekranlarda oluşur; boşluğun altta toplanması, `ActionPanel` alttan açıldığında haritanın daha büyük kısmının görünür kalması demektir.

### 5.1 🛠️ HUD'da yatay kaydırma

Oyuncu şeridi kendi içinde yatay kaydırılabilir. Bu, `13-scroll-lock.md` ile **çelişmez**: oradaki 🔒 kural viewport'un (`html`/`body`) kaymamasıdır, "taşan içerik bilinçli olarak belirlenmiş iç panellerde kayar" der.

Elenen alternatifler: *sarma* 12 oyuncuda HUD'u 4 satıra çıkarıp haritadan yer çalardı; *rosteri menüye taşımak* 🔒 "bot her zaman açıkça belirtilir" kuralını zayıflatırdı (bot etiketi menü arkasında kalırdı).

---

## 6. Bölge durum matrisi

Kenarlık **tek bir hiyerarşi** olarak çözülür, ilk eşleşen kazanır:

| Sıra | Durum | Kenarlık | Ek |
|---|---|---|---|
| 1 | Sürüklenen hedef | `--game-ring-hover` 3.5px | — |
| 2 | Seçili / sürükleme kaynağı | `--game-ring-selected` 3px | kaynak `opacity 0.82` + nabız halkası |
| 3 | **Benim bölgem** | `--game-ring-own` 2px | kalıcı |
| 4 | Geçerli hedef | `--game-ring-target` 1.25px | **yalnızca aktif sürükleme sırasında** |
| 5 | Nötr / rakip | `--game-map-edge` 1px | nötr ayrıca desatüre dolgu |
| — | Fog of War | `--game-map-edge` 1px | dolgu `--game-fog`, **rozet ve isim render edilmez** |
| — | Hover | kenarlık değişmez | `opacity 0.9` |
| — | Pressed | kenarlık değişmez | `opacity 0.86` |
| — | Klavye odağı | `focus-visible:outline-ring` | `04-style.md` Bölüm 13 |

### 6.1 🔒 "Geçersiz hedef" ve saldırı kuralı

🔒 `GameConfig.AttackAdjacencyOnly = false` **değişmemiştir ve değiştirilemez** — kaynak dışındaki her bölge geçerli bir gönderim hedefidir. Tek geçersiz hedef kaynağın kendisidir (ve hiçbir bölgede olmayan boşluk).

🛠️ Değişen yalnızca **gösterim**dir:

1. **Tetikleyici tıklama değil, aktif sürükleme.** Tıklamak yalnızca bilgi panelini açar, saldırı başlatmaz — yani vurgu yanlış anda çıkıyordu. Üstelik "her yer hedef" bilgisi her zaman doğru olduğu için tek başına hiçbir şey söylemez, yalnızca gürültü üretir.
2. **Kesikli aksan kenarlık kaldırıldı.** Eskiden bir bölge seçilir seçilmez diğer 11 bölge kesikli çizgiye boğuluyor, asker sayıları bu tarama deseninin altında kayboluyordu. Artık kesiksiz, çok daha soluk ve tek seviyeli.
3. **Geçersizlik okta gösterilir:** bırakma bir saldırıya dönüşmeyecekse ok kesikli, soluk ve ucu içi boş olur. 🔒 Görev tanımındaki "iptal yolu açık olmalı" maddesi böyle karşılanmıştır — zaten var olan iptal yolu (kaynağın üstüne ya da boşluğa bırakmak) **görünür** hale getirilmiştir; yeni bir iptal mekanizması eklenmemiş, mevcut sürükleme davranışı değiştirilmemiştir.

### 6.2 Asker sayısı rozeti

| Özellik | Değer | Gerekçe |
|---|---|---|
| Şekil | Sabit genişlikte hap, 48×31 birim | Sayı 1/2/3 haneli olsun rozet **aynı** boyutta kalır — "öngörülebilir genişlik" (§1.1). Bölgeden bölgeye zıplayan rozet gözü yorar, varış geri sayımında titremeye yol açar. |
| Sayı | 21 punto, weight 800, `tabular-nums` | Mobilde ~9px'ten **~13px'e** çıktı |
| Renk | `--game-text-on-badge` beyaz, rozet tonu türetilmiş | ≥7.7:1 garanti (§3.2) |
| Halka | `--game-badge-ring` 2px | Rozet/dolgu kontrastı bazı slotlarda 2.2:1'e düşebiliyor; halka ayrışmayı **renkten bağımsız** garanti eder |

Boyut, en küçük bölgenin sınırlayıcı kutusuna sığacak şekilde seçilmiştir. Ölçüm §6.4 sonrası yenilendi: en küçük bölge artık **73×90 birim** (Vianden silueti), etiket çapasının kenara en kısa uzaklığı **23.6 birim** — 48×31 hap bu bölgede de kendi sınırları içinde kalır (rozetin yuvarlatılmış köşeleri sayesinde en geniş noktada ~0.4 birimlik pay yeterlidir).

### 6.4 Bölge geometrisi — gerçek kanton sınırları 🔒

🔒 **Kullanıcı talimatı (1. tur):** "game içindeki harita tam olarak bu harita gibi olsun ama renk vs hiçbir şeyi değiştirme, sadece şehirlerin şeklini değiştir" (referans: Lüksemburg'un 12 kantonunu gösteren standart idari harita).

🔒 **Kullanıcı talimatı (2. tur, aynı referans harita):** "bu gamemap haritası sana gönderdiğim png dosyasındakinin aynısı olsun ama sadece şehirler bu şekilde gözüksün başka hiç bir şeyi değiştirme renkler vb aynı kalacak sadece şehir çevreleri ve harita tam olarak böyle olacak" — talimat 1. turla aynı yöndedir, ama "**tam olarak**" vurgusu üç eksiği kapatmayı gerektirdi:

1. **Harita hiç yüklenmiyordu.** `MapProvider` bölge başına 4-8 köşe dayatıyordu (aşağıya bakın); gerçek kanton sınırları bunun çok üstünde olduğu için 1. turun geometrisi dosyada duruyor ama uygulama açılışta `InvalidOperationException` ile düşüyordu. Yani oyunda hâlâ eski, elle çizilmiş çokgenler görünüyordu.
2. **Siluet sadakati:** sadeleştirme toleransı 2.5 → **1.2 birim**.
3. **Şehir adları** referans haritadakilerle hizalanmadığı için harita "aynı" okunmuyordu (kuzeydeki kanton Clervaux'dur, ama oyunda "Wiltz" yazıyordu).

**Kaynak veri.** geoBoundaries gbOpen **LUX ADM1** (12 kanton, OSM türevi). `api/Data/map.json` bu veriden üretildi:

1. Eşdikdörtgen izdüşüm (`x = lon·cos(lat₀)`, `y = -lat`), **en/boy oranı korunarak** eski haritanın dikey uzanımına (`y` 6.2–570.1) ölçeklendi ve yatayda ortalandı.
2. Sadeleştirme **topolojiye duyarlı**: her köşe noktasının hangi kantonlara ait olduğu çıkarıldı, ortak sınırlar tek bir "yay" olarak bir kez Douglas–Peucker'den (tolerans **1.2 birim**) geçirildi ve iki komşuya da aynı nokta dizisi verildi. Bu yüzden bölgeler arasında **boşluk/örtüşme yoktur** — bağımsız sadeleştirme yapılsaydı ortak sınırlar birbirinden ayrışır ve aralarında zemin sızardı.
3. Toplam **1423 nokta** (bölge başına 69–170), `map.json` ≈ 40 KB. Tolerans 2.5'ten 1.2'ye indirildi: 2.5 birim yalnızca haritanın 360px'e indiği mobil genişlikte 1 pikselin altında kalıyordu, masaüstünde (~900px, birim başına ~2.1px) **~5 piksellik** görünür bir köşeleşme üretiyordu. 1.2 birim masaüstünde de ~2.5 pikselin altına iner; daha düşük tolerans dosyayı ikiye katlar, kazancı ise ekranda ölçülemez.
4. `region.x/y` alanı anlamını korur (sınırlayıcı kutu merkezi) ve yeni geometriden yeniden hesaplandı. Etiket çapası hâlâ **çalışma zamanında** hesaplanır (§4.1); `map.json` bu değeri taşımaz.

**Doğrulama (ölçüldü, tahmin değil).** 975×1400 örnekleme ızgarasında her noktanın kaç bölgenin içinde kaldığı sayıldı: **örtüşme 0 piksel**, ülke içi **boşluk 1 piksel** (758.361 dolu pikselde, yani %0.0001 — tam sınır çizgisine düşen tek örnek noktası). Etiket çapası ölçümleri değişmedi: en küçük bölge **73×90 birim** (Vianden), en kısa çapa payı **23.6 birim** → 48×31 rozet 12 bölgenin hepsinde kendi sınırları içinde kalır (§6.2).

**viewBox.** Gerçek ülke silueti eskisi gibi ~kare değil dikeydir (en/boy ≈ 0.70). Yeni bbox `[120.1, 6.2]–[512.9, 570.1]`, `GameMap.tsx`'teki viewBox sabitleri (`105 / -9 / 423 / 594`) **değişmedi** — geometri bilinçli olarak eski sınırlayıcı kutuya oturtuldu, böylece bu yenileme frontend'de tek satır bile gerektirmedi.

**⚠️ `MapProvider` köşe sayısı guard'ı: 4-8 → 4-500.** Eski sınır, harita elle çizilmiş basit çokgenlerden oluşurken konmuştu (docs/14-game-map-redesign.md Bölüm 3). Gerçek kanton sınırları nehir/vadi izlediği için bölge başına 69–170 köşe taşır; guard gevşetilmeden bu 🔒 talimat **uygulanamaz** (uygulama açılışta durur). Alt sınır korundu (kapalı bir yüzey için en az 3 köşe gerekir, güvenli pay ile 4), üst sınır bozuk/şişmiş veriye karşı emniyet freni olarak 500'e alındı. Komşuluk sayısı ve simetri doğrulamalarına **dokunulmadı**.

**🔒 Bölge adları — referans haritayla hizalandı.** `map.json`'daki `id` ve `neighbors` alanlarına **dokunulmadı** (id'ler testlerde ve komşuluk grafiğinde geçiyor, `GameConfig.NeighborsPerRegion = 3` 🔒 kuralı bağlayıcı); değişen yalnızca oyuncuya görünen `name` alanıdır. Hangi bölgenin hangi kanton siluetini alacağı 1. turdaki eşlemedir (**mevcut komşuluk grafiğini en çok koruyan** eşleme, dal-budamalı tam arama) — yalnızca artık her bölge taşıdığı siluetin adını yazıyor:

| Bölge (`id`) | Kanton silueti = görünen ad | Eski ad (referansla uyumsuzdu) |
|---|---|---|
| `luxembourg-city` | Capellen | Luxembourg Şehri |
| `esch-sur-alzette` | Redange | Esch-sur-Alzette |
| `differdange` | Wiltz | Differdange |
| `dudelange` | Diekirch | Dudelange |
| `mersch` | Mersch | Mersch |
| `steinfort` | Luxembourg | Steinfort |
| `ettelbruck` | Esch-sur-Alzette | Ettelbruck |
| `diekirch` | Vianden | Diekirch |
| `wiltz` | Clervaux | Wiltz |
| `echternach` | Echternach | Echternach |
| `grevenmacher` | Grevenmacher | Grevenmacher |
| `remich` | Remich | Remich |

⚠️ Bu tablo yüzünden `id` ile görünen ad artık 8 bölgede birbirinden farklıdır (ör. `wiltz` id'li bölge ekranda **Clervaux** yazar). Bilinçli bir kabul: id'yi de değiştirmek 4 test dosyasına ve komşuluk grafiğine dokunmayı gerektirirdi, oysa talimat "başka hiçbir şeyi değiştirme" diyor. Bölge id'lerini okurken **her zaman bu tabloya bakılır**, id'den kanton adı tahmin edilmez. En uzun ad ("Esch-sur-Alzette", 16 karakter) eskisiyle aynı uzunluktadır — §6.3'teki taşma ölçümü değişmedi.

**⚠️ Neden komşulukta tam uyum imkânsız.** Oyun grafiği 3-düzenlidir (her bölge tam 3 komşu), gerçek kanton komşuluğu ise 2–6 arasında değişir; **Vianden'in yalnızca 2 gerçek komşusu vardır**. Dolayısıyla 18 komşuluk kenarının tamamının ortak sınıra karşılık gelmesi matematiksel olarak mümkün değildir. Kullanılan eşlemede **3 kenar** ortak sınır paylaşmaz (ad hizalaması bu sayıyı değiştirmez, çünkü siluet↔`id` eşlemesi aynı kaldı):

- `esch-sur-alzette` ↔ `diekirch` (Redange / Vianden)
- `ettelbruck` ↔ `diekirch` (Esch-sur-Alzette / Vianden)
- `wiltz` ↔ `echternach` (Clervaux / Echternach)

Bu üç çift oyun içinde birbirine saldırabilir ama haritada bitişik görünmez. Saldırı zaten komşulukla sınırlı olmadığı için (`GameConfig.AttackAdjacencyOnly = false`) oynanışta hiçbir etkisi yoktur. ❓ Müşteriye bırakılan tek kalan karar: bu üç kenar kabul edilir (mevcut durum) ya da `NeighborsPerRegion = 3` 🔒 kuralı gevşetilip komşuluk gerçek sınırlardan türetilir.

### 6.3 Bölge adı

11 punto, weight 500/600, `--game-label-ink` + `--game-label-halo` (`paint-order: stroke`).

🛠️ **Halo neden var:** ölçümde isim mürekkebi bazı takım renkleri üzerinde AA eşiğini (4.5:1) tutturamıyordu (en kötü durum indigo dolguda **3.96**). Halo bu bağımlılığı tamamen kaldırır — metin artık halonun üstünde okunur.

🛠️ **Neden silinmedi:** iki ölçüm çelişiyordu — okunabilir boyut (11 punto) 3 bölgede taşırıyor, taşırmayan boyut (9.5 punto) mobilde ~6px'e düşürüp okunamaz kılıyordu. Taşmanın gerçek büyüklüğü ölçüldü: **en fazla 6.5 birim ≈ 4 ekran pikseli** — son harfin kuyruğu sınırı geçiyor, görünmez. Bu yüzden içerik silinmedi, yalnızca ikincil tipografik **register**e alındı (sayı: koyu zeminde beyaz/ağır/büyük; ad: açık zeminde koyu/ince/küçük — farklı dil konuştukları için göz bölünmez).

---

## 7. Animasyon envanteri

| Animasyon | Süre | Teknik | Bilgi taşıyor mu |
|---|---|---|---|
| Hover opaklığı | `--game-dur-fast` 130ms | CSS transition | Hayır (geri bildirim) |
| Kontrol barı segment genişliği | `--game-dur-base` 170ms | CSS transition | Evet (denge kaydı) |
| Sürükleme nabız halkası | 1.15s döngü | CSS `@keyframes game-drag-ping` | Evet (kaynak neresi) |
| Ele geçirme darbesi | **420ms** | CSS `game-capture-wash` + `game-capture-edge` | Evet (bura el değiştirdi) |
| Varış geri sayımı + rozet parlaması | 700ms | Imperatif rAF (`RegionLabel`) | Evet (yeni asker sayısı) |
| Asker pop-in | 220ms | Imperatif rAF | Evet (sevkiyat çıktı) |
| Çarpışma darbesi (kazanan) | 260ms | Imperatif rAF | Evet (çarpışma oldu) |
| Ölüm/varış sönümü | 320ms | Imperatif rAF | Evet (asker kayboldu) |

### 7.1 🛠️ SMIL değil CSS — zamanlama tuzağı

Ele geçirme flash'ı ve nabız halkası SVG SMIL (`<animate>`) kullanıyordu. **SMIL'de `begin="0s"`, elemanın DOM'a eklendiği ana değil SVG BELGESİNİN zaman çizelgesine görelidir.** Maçın 30. saniyesinde eklenen tek seferlik bir animasyon için bu "başlangıç zamanı çoktan geçti" demektir; `fill="freeze"` ile animasyon doğrudan bitiş değerine atlar — yani **efekt hiç görünmeyebilir.**

CSS animasyonu elemanın eklendiği anda başlar. **Oyun ekranında dinamik olarak eklenen elemanlar için SMIL kullanılmamalıdır.**

### 7.2 🛠️ `animate-ping` SVG'de güvenilir değil

Tailwind'in `animate-ping`'i CSS `transform: scale()` üretir; SVG'de dönüşümün referans kutusu (`transform-box`) tarayıcıya göre değişir — verilmezse halka daireden değil **viewBox köşesinden** büyür. `transform-box: view-box` + açık `transform-origin` **zorunludur.**

### 7.3 🔒 `prefers-reduced-motion`

**İlke: bilgi taşıyan hareket KORUNUR, dekoratif/döngüsel hareket durur.**

Ordu hareketi, varış geri sayımı ve ele geçirme parlaması oyunun **durumunu** anlatır; kaldırmak hareket hassasiyeti olan bir oyuncuyu bilgiden mahrum bırakır, erişilebilirliği artırmaz.

| Kaldırılan | Kalan |
|---|---|
| Nabız halkasının büyüyüp sönme döngüsü (sabit halka olur) | Halkanın kendisi (kaynak işareti) |
| Dekoratif geçişler (hover, bar genişliği) | — |
| Asker pop-in overshoot ("bounce") ve çarpışma darbesi | Askerlerin gerçekten yürümesi |

⚠️ Kapsam **`[data-game-shell]` ile sınırlıdır** — bu görev sitenin geri kalanının hareket davranışını değiştirmez.

⚠️ CSS'te `animation` **bilinçli olarak hariç tutulmuştur**: süresi sıfırlanırsa `forwards` yüzünden ele geçirme parlaması doğrudan bitiş değerine atlar ve bir bilgi kaybolur.

⚠️ `TroopMarker` imperatif rAF kullandığı için tercih **JS tarafında** okunur (`prefersReducedMotion()`), CSS media query'siyle susturulamaz.

---

## 8. Erişilebilirlik ve performans

### 8.1 Kontrast (ölçülmüş)

| Öğe | Kontrast |
|---|---|
| Asker sayısı (beyaz / rozet) | **≥7.72:1** — AAA |
| `--game-text` / oyun zemini | 16.13:1 |
| `--game-text-muted` / panel | 7.11:1 |
| HUD kendi çipi | 5.84:1 |
| `muted-foreground` / overlay kartı | 6.84:1 |
| Hata mesajı (`--destructive` / `--game-panel-solid`) | **4.61:1** |

🛠️ Hata mesajının zemini bilinçli olarak `--game-panel-solid`'dir, `bg-popover` değil: ölçümde `--destructive` popover zemininde **4.48:1** veriyordu — AA eşiğinin (4.5) kıl payı altında. Paylaşılan `--destructive` token'ına **dokunulmamıştır.**

### 8.2 Dokunma hedefleri

- Bölge polygonları: en küçük bölge 360px genişlikte **~71px** ✓
- Menü butonu: görsel 28px, hedef görünmez pseudo-elemanla **44px** ✓
- Panel CTA'ları: `min-h-11` (44px) ✓

⚠️ Paylaşılan `components/ui/button.tsx` ölçeği en fazla 36px verir ve bu görevde **değiştirilmemiştir**; yükseklik yalnızca oyun ekranı için, kullanım yerinde yükseltilmiştir.

### 8.3 Performans

- Oyun ekranında **framer-motion kullanılmaz.** Çok elemanlı sevkiyat katmanı imperatif rAF ile doğrudan DOM'a yazar, React state/re-render tetiklemez.
- `RegionShape`/`RegionLabel`/`TroopMarker` memoize edilmiştir; sunucu tick'i bunları yeniden render etmez (§4.2).
- Etiket çapası harita başına bir kez hesaplanır (5.3 ms).
- `DevFpsOverlay` (dev-only, sol alt) ile FPS izlenir.

---

## 9. ❓ Müşteriden doğrulanması gereken noktalar

Hepsi **bloklamaz** — her birinin yanında zaten uygulanmış bir 🛠️ karar vardır.

- ❓ **Sevkiyat ikonlarının boyut ölçeği.** 🔒 Müşteri kararı "sayı rozetiyle DEĞİL, ikon adediyle" idi; ancak ikon adedi `MAX_VISIBLE_TROOPS = 10`'da tavan yaptığı için 10 asker ile 300 asker **birebir aynı** görünüyordu — §1.2'nin en tepesindeki bilgi için gerçek bir kayıp. 🛠️ Sayı geri getirilmeden ikon yarıçapı logaritmik olarak 3.1 → 4.4 birim büyütüldü (~%40). Bu, müşterinin kararına eklenen bir kanaldır; istenmezse `TROOP_ICON_MAX_RADIUS` değeri `TROOP_ICON_MIN_RADIUS`'a eşitlenerek geri alınır.
- ❓ **VIP'te 12 rengin renk körlüğü ayrışması** ölçülebilir tavana yakın (ΔE 5.3) ama mükemmel değil. 🛠️ Akromatik ikinci kanal (§2.4) bu boşluğu kapatır. Müşteri VIP'te daha az oyuncu ya da farklı bir ayrım kanalı isterse yeniden ele alınır.
- ❓ **Bölge adının haritada kalması.** 🛠️ Ölçülen taşma ≤4 piksel olduğu için ad silinmedi (§6.3). Müşteri haritanın tamamen isimsiz olmasını tercih ederse `RegionLabel`'daki ikinci `<text>` kaldırılır; ad zaten `<title>` (tooltip/ekran okuyucu) ve `ActionPanel`'de mevcuttur.
- ❓ **Ele geçirme darbesinin görünürlüğü.** §7.1'deki SMIL tuzağı nedeniyle önceki uygulamanın hiç görünmemiş olma ihtimali vardır; bu, canlı bir maçta doğrulanamadı. Müşteri "bu efekt yeni mi?" derse cevabı budur.

---

## 10. Bu ekrana dokunan bir sonraki görev için kontrol listesi

- [ ] Yeni bir renk mi ekliyorsun? Yalnızca `PLAYER_COLORS`'a ekle — "dark" dizisi **türetilir** (§3.3). Ekledikten sonra §3.1'deki ölçümü tekrarla.
- [ ] `RegionShape`/`RegionLabel`'a yeni bir görsel prop mu ekledin? **Memo karşılaştırıcısına da ekle** (§4.2).
- [ ] Sürükleme handler'ı içinde bir değer mi okuyorsun? **`useRef` kullan** (§4.3).
- [ ] Yeni bir durum göstergesi mi ekliyorsun? **Akışa değil, overlay olarak** ekle (§5).
- [ ] Dinamik olarak eklenen bir SVG animasyonu mu? **SMIL değil CSS** (§7.1).
- [ ] Yeni bir animasyon mu? `prefers-reduced-motion` altında ne olacağına karar ver (§7.3) ve §7'deki envantere ekle.
- [ ] SVG'de token mu kullanıyorsun? `var()` presentation attribute'unda güvenilir çözülmez — **`style` üzerinden ver.**
- [ ] Bölge geometrisine mi dokunuyorsun? Ortak sınırlar **tek yay** olarak sadeleştirilmelidir (§6.4), her bölge **3 komşu** taşımaya devam etmelidir (`MapProvider` aksi halde açılışta durur) ve `GameMap.tsx`'teki viewBox sabitleri yeni bbox'a göre güncellenmelidir.
- [ ] Yeni bir sabit renk mi? `globals.css`'teki `--game-*` bloğuna ekle, JSX'e gömme (§2.7).
