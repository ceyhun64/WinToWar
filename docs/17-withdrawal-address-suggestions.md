# 17 — Para Çek: Son Kullanılan Adresleri Öner

> **Dosya adı notu:** Bu dosyayı `docs/17-withdrawal-address-suggestions.md` olarak kaydedin ve `CLAUDE.md`'nin
> "Her görevde önce oku" tablosuna şu satırı ekleyin:
> _"Çekim adresi önerisi/UX işi → `docs/17-withdrawal-address-suggestions.md` (+ `05-payment.md`'nin üzerine
> inşa eder, `PaymentService`/`WalletService`/`PayoutService`/`RefundService` iş mantığını ve
> `WithdrawalRequest` state makinesini değiştirmez)."_

## NASIL OKUNMALI

- 🔒 **MÜŞTERİ TALİMATI (DEĞİŞTİRİLEMEZ):** Birebir uygulanacak kurallar.
- 🛠️ **MÜHENDİSLİK VARSAYIMI (SEN KARAR VER, GEREKÇELENDİR):** Netleştirilmemiş noktalar. Makul varsayımla
  ilerle, kısa yorum/commit mesajıyla gerekçelendir. **Asla soru sorup bekleme.**
- ❓ Müşteriden ileride doğrulanması gereken nokta.

## 0. Amaç ve kapsam 🔒

`/cuzdan` sayfasındaki "Para Çek" panelinde kullanıcı, daha önce **kendi hesabından** çekim talebi oluşturduğu
LTC adreslerini görüp tek tıkla adres alanına doldurabilecek ("Son kullanılan adresler" önerisi). Amaç: yanlış/
eksik adrese kripto gönderme riskini azaltmak ve tekrarlayan kullanıcılar için adresi elle tekrar yazma/
yapıştırma zahmetini kaldırmak.

**Kapsam dışı:** Yeni bir tablo, adres ekleme/silme/etiketleme UI'ı **bu görevin parçası değil** — yalnızca
mevcut `WithdrawalRequest` geçmişinden türetilen, salt-okunur bir öneri listesi yapılır. Yeni bir migration/tablo
gerekmez. `PaymentService`, `WalletService`, `PayoutService`, `RefundService`, `WithdrawalRequest` state
makinesi bu görevle **değişmez**. Bu görev, ileride daha zengin bir "adres defteri" özelliğine genişletilmeyi
**hedeflemez** — böyle bir ihtiyaç ortaya çıkarsa ayrı bir görev/doküman olarak ele alınır; bu görev kapsamında
"ileride genişletilebilir olsun" gerekçesiyle fazladan soyutlama/katman/tablo eklenmez (YAGNI,
`01-workflow-rules.md` Bölüm 0.10).

## 1. Güvenlik ilkesi 🔒

Önerilen adres seçildiğinde form alanı **doldurulur, hiçbir şekilde otomatik gönderim tetiklenmez.** Kullanıcı
her durumda "Çekim Talebi Oluştur" butonuna kendi elleriyle basmak zorundadır — bu, öneri seçiminin kullanıcı
adına otomatik bir çekim başlatmasını engelleyen zorunlu kullanıcı-onay katmanıdır ve hiçbir gerekçeyle
atlanamaz. (Backend'deki adres doğrulama/bakiye kontrolü gibi diğer güvenlik katmanları bu görevle değişmez,
zaten mevcut `PaymentService`/`WalletService` akışında yer alıyor.)

## 2. Backend — geçmiş adresleri döndüren endpoint 🛠️

🛠️ **Karar:** `WalletController.cs`'e yeni bir `GET /api/wallet/withdrawal-addresses` endpoint'i eklenir (mevcut
auth middleware ile korunur, yalnızca giriş yapmış kullanıcının **kendi** geçmişini döndürür — başka bir
kullanıcının adresleri asla sızmaz).

- 🔒 **Durum filtresi netleştirmesi:** Yalnızca fiilen **zincire gönderilmiş** bir çekimin adresi "kullanılmış
  adres" sayılır — `Approved` durumu (talep onaylandı ama henüz on-chain gönderilmedi) **yeterli değildir** ve bu
  görevde "kullanılmış" olarak sayılmaz, çünkü henüz gerçekleşmemiş bir işlemin adresini kullanıcıya "daha önce
  buraya gönderdin" gibi sunmak yanıltıcı olur. Agent göreve başlamadan `Payments/PaymentEnums.cs` içindeki
  gerçek `WithdrawalRequest.Status` (veya ilişkili `Payout.Status`) değerlerini okuyup, **"zincire gönderildi"
  anlamına gelen en ileri durumu** (muhtemelen `Sent` — ama projedeki gerçek enum adı farklıysa ona göre) tek
  filtre olarak kullanır. `Pending`/`Approved`/reddedilmiş/iptal edilmiş hiçbir durum bu listeye dahil edilmez.
  Emin olunamayan bir nokta varsa ❓ olarak rapora not düşülür, ama karar yine de bu ilkeye göre **şimdi**
  verilir — soru sorup beklenmez.
- **Adrese göre gruplama:** Aynı adrese birden fazla çekim yapılmış olabilir; bu adres birden fazla satır olarak
  listelenmez. Backend'de: kullanıcının filtrelenmiş `WithdrawalRequest` kayıtları **adrese göre gruplanır**
  (`GroupBy(w => w.Address)`), her grup için **o gruptaki en son `CreatedAt`** alınır
  (`Max(w => w.CreatedAt)`), sonuç bu `LastUsedAt` değerine göre azalan sırada sıralanır
  (`OrderByDescending`), ve yalnızca ilk **5** kayıt döndürülür (`Take(5)`). Sıralama "son eklenen satır"a göre
  değil, **her adresin en son kullanıldığı tarihe** göre yapılmalıdır — aksi halde eski bir adrese yakın zamanda
  yapılmış ikinci bir çekim varsa sıralama yanlış çıkar.
- DTO: `WithdrawalAddressSuggestionDto { string Address, DateTime LastUsedAt }` — yalnızca bu iki alan, başka
  hiçbir kullanıcı/işlem bilgisi taşımaz (`Payments/Dtos/PaymentDtos.cs` içine eklenir — `06-coding-standards.md`
  "domain model doğrudan yayınlanmaz, DTO'ya map'lenir" kuralına uyar).
- 🔒 **Kullanıcı izolasyonu:** Sorgu **yalnızca** kimlik doğrulamasından (JWT/auth context) gelen kullanıcı
  kimliğine göre filtrelenir. Endpoint hiçbir şekilde client'tan (query param, body vb.) bir `userId`/
  `playerId` kabul etmez — kullanıcı kimliği tamamen sunucu tarafında, auth middleware'den okunur. Bu, başka
  bir kullanıcının adreslerinin sızmasına karşı tek ve yeterli koruma katmanıdır.
- Performans: bu endpoint sık çağrılan bir "sıcak yol" değildir (sayfa açılışında bir kez), ekstra
  cache/optimizasyon gerekmez (YAGNI).

## 3. Frontend — öneri listesi UI'ı 🛠️

- `lib/payments/api.ts`'e `getWithdrawalAddressSuggestions()` fonksiyonu eklenir.
- `/cuzdan` sayfasındaki "Para Çek" kartında (`CuzdanPageContent`, `app/(site)/cuzdan/page.tsx`), sayfa mount
  olunca bu liste çekilir (`getPendingWithdrawals`/`getInvoiceHistory` ile aynı `useEffect` deseninde, ayrı bir
  state: `addressSuggestions`).
- "Hedef LTC adresi" input'unun **altına**, liste boş değilse küçük bir etiket + tıklanabilir chip'ler eklenir,
  ör.:

  ```tsx
  {
    addressSuggestions.length > 0 ? (
      <div className="flex flex-col gap-1.5">
        <span className="text-xs text-muted-foreground">
          Son kullanılan adresler
        </span>
        <div className="flex flex-wrap gap-1.5">
          {addressSuggestions.map((s) => (
            <button
              key={s.address}
              type="button"
              onClick={() => setWithdrawAddress(s.address)}
              className="rounded-md border px-2 py-1 font-mono text-xs text-muted-foreground hover:border-foreground/40 hover:text-foreground"
            >
              {truncateAddress(s.address)}
            </button>
          ))}
        </div>
      </div>
    ) : null;
  }
  ```

  🛠️ Adres uzun olduğu için chip üzerinde tam adres yerine kısaltılmış gösterim (`ltc1q...a9f2` gibi baş/son
  birkaç karakter) kullanılır. Tam adresin görünmesi için ayrı bir `title` tooltip'i **eklenmez** — chip'e
  tıklandığında zaten tam adres `withdrawAddress` input'unu doldurur ve kullanıcı tam adresi orada, normal metin
  olarak görür; bu yeterlidir ve ekstra bir tooltip katmanı gereksizdir. Kısaltma için `lib/utils.ts`'e küçük bir
  `truncateAddress` yardımcı fonksiyonu eklenebilir (zaten benzer bir yardımcı varsa onu kullan, tekrar yazma —
  bkz. `06-coding-standards.md` "Kod Tekrarını Önleme").

- Bir chip'e tıklamak **yalnızca** `withdrawAddress` state'ini doldurur (`onClick={() => setWithdrawAddress(...)}`)
  — Bölüm 1'deki güvenlik ilkesi gereği `handleWithdraw()` burada **kesinlikle çağrılmaz**.
- Görsel stil `04-style.md`'deki genel dile uyar — yeni bir renk/komponent sistemi icat edilmez, mevcut
  `Badge`/`Button` varyantları veya sade bir `border` + `text-muted-foreground` yeterlidir (yukarıdaki örnek
  gibi).

## 4. Boş durum 🛠️

Kullanıcının hiç çekim geçmişi yoksa (yeni kullanıcı) bu bölüm **hiç render edilmez** — boş bir "henüz kayıtlı
adres yok" mesajı göstermek gereksiz UI gürültüsü olur, panel eskisi gibi sade kalır.

## 5. Doğrulama akışı 🛠️

`01-workflow-rules.md` Bölüm 0.8-0.9 gereği aşağıdakiler gerçekten çalıştırılıp doğrulanmadan görev tamamlanmış
sayılmaz:

1. Hiç çekim geçmişi olmayan bir kullanıcıyla `/cuzdan` açılır → öneri bölümü görünmüyor mu (Bölüm 4)?
2. En az iki farklı adrese daha önce çekim yapmış bir kullanıcıyla `/cuzdan` açılır → en fazla 5 benzersiz adres,
   en son kullanılan en üstte mi listeleniyor?
3. Bir chip'e tıklanır → yalnızca input dolduruyor mu, **hiçbir şekilde** talep otomatik oluşmuyor mu (network
   tab'da chip tıklamasıyla eşzamanlı bir `POST /withdrawal` çağrısı **olmamalı**)?
4. Başka bir kullanıcının hesabıyla giriş yapılır → önceki kullanıcının adresleri **kesinlikle** görünmüyor mu
   (auth/kullanıcı izolasyonu doğrulaması)?

## 6. Definition of Done

- [ ] `GET /api/wallet/withdrawal-addresses` endpoint'i eklendi — yalnızca fiilen zincire gönderilmiş
      (`Sent` veya projedeki gerçek "zincire gönderildi" eşdeğeri — Bölüm 2'deki 🔒 kurala göre doğrulanmış)
      çekimlerden, adrese göre gruplanmış, her adresin **en son kullanım tarihine** göre azalan sırada en fazla
      5 benzersiz adresi döndürüyor.
- [ ] Kullanıcı kimliği yalnızca sunucu tarafında auth context'ten okunuyor; endpoint client'tan `userId` kabul
      etmiyor.
- [ ] `WithdrawalAddressSuggestionDto` eklendi, yalnızca `Address` ve `LastUsedAt` alanlarını içeriyor, domain
      model doğrudan dönmüyor.
- [ ] `lib/payments/api.ts`'e `getWithdrawalAddressSuggestions()` eklendi.
- [ ] `/cuzdan` sayfasında "Para Çek" panelinde öneri chip'leri eklendi, boşsa hiç render edilmiyor.
- [ ] Chip tıklaması yalnızca `withdrawAddress` state'ini dolduruyor, otomatik gönderim **yok** (Bölüm 5, madde 3
      gerçekten doğrulandı).
- [ ] Kullanıcı izolasyonu doğrulandı (Bölüm 5, madde 4).
- [ ] `PaymentService`/`WalletService`/`PayoutService`/`RefundService`/`WithdrawalRequest` state makinesinde
      **tek satır bile değişmedi**.
- [ ] Yeni migration **yok** (mevcut `WithdrawalRequest` tablosundan salt-okunur sorgu).
- [ ] `dotnet build` ve `npm run build` geçti.
- [ ] `git diff` yalnızca beklenen dosyaları içeriyor (`CLAUDE.md` — "Her görevde önce oku" tablosuna eklenen
      satır, `WalletController.cs`, `PaymentDtos.cs`, `lib/payments/api.ts`, `cuzdan/page.tsx`, gerekirse
      `lib/utils.ts`'e küçük bir ekleme).
