namespace api;

/// <summary>
/// "WinToWar" oyun motorunun tüm sayısal sabitleri (docs/03-game-rules.md Bölüm 12 —
/// konsolide GameConfig tablosu tek doğruluk kaynağıdır). Kod içinde magic number
/// kullanılmaz; her değer buradan okunur.
///
/// Müşteri talimatında birebir verilen değerler 🔒, müşterinin netleştirmediği ve
/// mühendislik kararıyla kesinleştirilen değerler 🛠️ olarak işaretlenmiştir — ayrıntılı
/// gerekçeler docs/03-game-rules.md içindedir, burada tekrarlanmaz.
/// </summary>
public static class GameConfig
{
    // ---- Harita ----
    // 🔒 12 şehir/bölge, her biri 3 komşuluk bağlantısına sahip (map.json).
    public const int RegionCount = 12;
    public const int NeighborsPerRegion = 3;

    // ---- Oda türleri ----
    // 🛠️ Standart oda: likidite riski gerekçesiyle 12 değil 4 oyunculu (Bölüm 2.1).
    public const int StandardRoomPlayerCount = 4;
    public const bool StandardRoomFogOfWar = false;
    // 🔒 Müşteri kararı: sahipsiz/nötr bölgelerin başlangıç savunması artık 10 (bkz. Bölüm 4).
    public const int StandardRoomGreyRegionDefenseCount = 10;
    // 🔒 Giriş ücreti: sabit $1 USD (docs/03-game-rules.md Bölüm 2.1 / docs/05-payment.md Bölüm 1.1).
    public const decimal StandardRoomEntryFeeUsd = 1.00m;

    // 🔒 VIP: gri bölge savunması 1-7, oyuncu sayısı 2-12 arası kurucu tarafından seçilir.
    public const int GreyRegionDefenseMin = 1;
    public const int GreyRegionDefenseMax = 7;
    public const int VipRoomMinPlayers = 2;
    public const int VipRoomMaxPlayers = 12;

    // 🛠️ Practice: tek paylaşılan otomatik eşleşme kuyruğu (Bölüm 7).
    public const int PracticeRoomDefaultPlayerCount = 2;
    public const int PracticeLobbyFillTimeoutSeconds = 60;
    // 🔒 Müşteri kararı: sahipsiz/nötr bölgelerin başlangıç savunması artık 10 (bkz. Bölüm 4).
    public const int PracticeGreyRegionDefenseCount = 10;
    public const bool PracticeFogOfWar = false;

    // 🔒 Kullanıcı talimatı: Practice modunda hiç beklenmez — bot masaya ANINDA gelir
    // ve maç geri sayım tutmadan başlar. Practice bir eşleşme değil, bir alıştırma
    // ortamıdır; "rakip bekleniyor" ekranı burada bir bilgi taşımaz, yalnızca oyuncuyu
    // oyuna girmeden önce ~20-25 sn bekletiyordu (10-15 sn bot bekleme + 10 sn geri sayım).
    //
    // ⚠️ SONUÇ — bilinçli kabul edilen ödün: bot anında masaya geldiği için Practice'te
    // artık iki gerçek oyuncu BİRBİRİYLE EŞLEŞEMEZ. Bekleme penceresi, canlı eşleşmeye
    // şans tanıyan mekanizmaydı (bkz. Bölüm 7 🔒 "bir süre beklenir"); Practice için
    // kullanıcı kararıyla kaldırılmıştır. Standart ve VIP odalarda bu pencere AYNEN
    // durur — para riski taşıyan modlarda gerçek rakip eşleşmesi korunur.
    public const int PracticeBotMatchWaitSeconds = 0;
    public const int PracticeLobbyCountdownSeconds = 0;

    // ---- Lobi ----
    // 🔒 5 dakika — süre dolduğunda otomatik iptal YOK, oyuncuya İptal Et/Beklemeye
    // Devam Et seçimi sunulur (bkz. Bölüm 7, docs/05-payment.md Bölüm 1.6).
    public const int LobbyFillTimeoutSeconds = 300;

    // 🛠️ Lobi dolduğunda maç anında başlamaz, kısa bir geri sayım gösterilir
    // (müşteri belirtmedi, doküman bu konuda sessiz — çelişki yaratmayan bir UX kararı).
    public const int LobbyCountdownSeconds = 10;

    // 🛠️ Kazanan belli olduktan sonra sonuç ekranının gösterim süresi.
    public const int ResultScreenDurationSeconds = 15;

    // ---- Ekonomi ----
    // 🔒 Müşteri kararı: "kale gibi bir alan olmayacak" — hiçbir bölge diğerlerinden
    // ayrıcalıklı değildir. Sahip olunan HER bölge (ilk verilen toprak + fethedilen
    // topraklar, hepsi eşit) saniyede 1 asker üretir (bkz. docs/03-game-rules.md Bölüm 4).
    public const int BaseProductionPerInterval = 1;
    public const int ProductionIntervalSeconds = 1;

    // 🔒 Müşteri kararı: sahipsiz/nötr bölge iyileşmesi "her saniye +1" (bkz. Bölüm 4
    // "kendiliğinden iyileşme") — üretimle (ProductionIntervalSeconds) aynı değere sahip
    // olsalar da kasıtlı olarak AYRI bir sabittir (iki büyüme oranı kasıtlı farklıdır,
    // biri diğerine bağlı değişmemeli).
    public const int NeutralRegenIntervalSeconds = 1;

    // 🛠️ Kullanıcı talimatı: sevkiyat gruplarının (docs/19-army.md Dispatch) gerçek
    // ayrılışı yalnızca bu tick'te işlendiğinden, tick aralığı sevkiyatlar arasındaki
    // görünür "adım" süresini belirliyordu (1000ms'de bariz bir sıçrama hissi
    // veriyordu) — daha akıcı, daha sık gözlemlenebilir kademeli çıkış için düşürüldü.
    // Production/nötr iyileşme kendi ayrı interval sabitleriyle (yukarıda) gerçek geçen
    // zamana göre çalıştığından bu değişiklikten etkilenmez, yalnızca daha sık kontrol edilirler.
    public const int GameTickMs = 250;

    // 🔒 Müşteri kararı: oyuncuların başlangıç (Ana Kale) bölgesi artık 0 değil 10 askerle başlar.
    public const int StartingSoldierCount = 10;

    // ---- Hareket ----
    // 🛠️ Hareket anlık değil, sabit süre (❓ müşteriye doğrulatılmalı, Bölüm 6).
    public const int MovementDurationSeconds = 5;

    // 🔒 Müşteri kararı (önceki ❓ 1 varsayımının yerini aldı): sürükle-bırak gönderiminde
    // kaynak bölgenin GÜNCEL asker sayısının TAMAMI gönderilir, kaynakta hiçbir asker
    // garrison olarak bırakılmaz (0). Müşterinin verdiği somut örnek: "20 birikti, 10
    // savunmalı yere saldırınca orada 20-10=10 askerim olmalı" — eski `1` değeri bu
    // hesaba bir asker eksik veriyordu (19-10=9), ayrıca kaynağı sürekli 1'de kilitleyip
    // art arda saldırı göndermeyi imkansız kılıyordu ("bölge sürüklenemez" — soldierCount
    // hep tam MinGarrisonPerSend'e eşit kalıyordu). `docs/03-game-rules.md` Bölüm 5'teki
    // "+2 Asker Kuralı" (hedef bölgede kalan garrison = gönderilen - savunma) bu
    // değişiklikten etkilenmez — o kural her zaman HEDEFTEKİ garrison'u tarif eder,
    // kaynakta bırakılan miktarı değil.
    public const int MinGarrisonPerSend = 0;

    // 🛠️ docs/19-army.md: bir AttackRegion çağrısı artık kaynaktan anında değil,
    // gerçek geçen zamana göre kademeli gruplar (batch) halinde ayrılır — "80-150ms
    // aralığını test edebilirsin" (§4) aralığının ortası.
    public const int DispatchBatchIntervalMs = 120;

    // 🛠️ docs/19-army.md §18'deki "visual unit" ölçekleme tablosunun aynısı, burada
    // toplam sevkiyat miktarına göre kaç GRUP halinde (tek tek asker değil) kademeli
    // ayrılacağını belirlemek için kullanılır — büyük bir orduyu tek tek 100ms
    // aralıklarla göndermek (ör. 200 asker × 120ms = 24 sn) oyunu ağırlaştırır (§4
    // "çok yavaş olursa gameplay ağırlaşır"); bunun yerine grup SAYISI ölçeklenir,
    // her grup orantılı bir asker payı taşır.
    public const int DispatchBatchScaleTinyMax = 5;
    public const int DispatchBatchScaleTinyBatches = 3;
    public const int DispatchBatchScaleSmallMax = 20;
    public const int DispatchBatchScaleSmallBatches = 8;
    public const int DispatchBatchScaleMediumMax = 100;
    public const int DispatchBatchScaleMediumBatches = 15;
    public const int DispatchBatchScaleLargeBatches = 25;

    // ---- Maç sonu / terk etme ----
    // 🛠️ Süre sınırı yok — maç yalnızca tek oyuncu kalınca biter (❓ Bölüm 8).
    public const int AbandonmentTimeoutSeconds = 90;

    // 🛠️ Turtling'i (sonsuza kadar biriktirme) anlamsız kılan asker tavanı — artık
    // Ana Kale özelinde değil, üretim yapan HER bölge için ayrı ayrı uygulanır (bölge
    // bazlı üretime geçişle birlikte, bkz. Bölüm 4). Üretim bir bölgenin kendi sınırına
    // ulaşınca durur, sınırın altına inince (asker gönderilince) otomatik devam eder.
    public const int MaxAccumulatedTroops = 200;

    // 🔒 Müşteri kararı (docs/03-game-rules.md Bölüm 3/14 madde 8/15-D.1'deki açık ❓
    // sorusunu çözdü): saldırı artık komşulukla sınırlı değil, gerçek state.io'daki gibi
    // haritadaki herhangi bir bölgeye doğrudan gönderim yapılabilir.
    public const bool AttackAdjacencyOnly = false;

    // ---- Güvenlik ----
    // 🛠️ Oyuncu başına saniyede en fazla aksiyon sayısı (spam koruması, Bölüm 11).
    public const int PlayerActionRateLimitPerSecond = 5;

    // ---- Bot politikası (Bölüm 7 DÜZELTME — "bot yok" kararı geri alındı) ----
    // 🔒 Müşterinin verdiği "10-15 saniye" aralığı: lobiye ilk gerçek oyuncu
    // girdikten sonra bu kadar süre canlı eşleşme olmazsa bot(lar) masaya eklenir.
    public const int BotMatchWaitMinSeconds = 10;
    public const int BotMatchWaitMaxSeconds = 15;

    // 🛠️ Bot zorluk dağılımı — varsayılan %60 Normal/%25 Kolay/%15 Zor (Bölüm 7),
    // toplamı 100 olmalı. Belirli bir oyuncuya/sıraya göre manipüle edilmez.
    public const int BotDifficultyEasyWeight = 25;
    public const int BotDifficultyNormalWeight = 60;
    public const int BotDifficultyHardWeight = 15;

    // 🛠️ Bot AI karar aralığı — müşteri belirtmedi, zorluk profiline göre bot
    // ne sıklıkla saldırı fırsatı değerlendirir (Zor daha sık/optimal, Kolay
    // daha seyrek/pasif). Gerçek zamanlı, sonuç önceden belirlenmez (Bölüm 7).
    public const int BotDecisionIntervalSecondsEasy = 12;
    public const int BotDecisionIntervalSecondsNormal = 7;
    public const int BotDecisionIntervalSecondsHard = 4;

    // 🛠️ Kolay bot yalnızca büyük bir asker fazlası birikince saldırır (pasif profil).
    public const int BotEasyMinSurplusToAttack = 6;

    // ---- Denetim kaydı (docs/02-architecture.md "Maç Denetim Kaydı") ----
    // ❓ Saklama süresi müşteriden netleştirilmeli, 🛠️ varsayılan 90 gün.
    public const int MatchEventLogRetentionDays = 90;
}
