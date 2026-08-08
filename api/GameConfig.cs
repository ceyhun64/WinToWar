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
    public const int StandardRoomGreyRegionDefenseCount = 1;
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
    public const int PracticeGreyRegionDefenseCount = 1;
    public const bool PracticeFogOfWar = false;

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
    // 🔒 Ana Kale üretimi: 10 saniyede 4 asker, fethedilen her bölge +1 asker/10sn.
    public const int BaseProductionPerInterval = 4;
    public const int ProductionIntervalSeconds = 10;
    public const int ProductionBonusPerRegion = 1;

    public const int GameTickMs = 1000;

    public const int StartingSoldierCount = 0;

    // ---- Hareket ----
    // 🛠️ Hareket anlık değil, sabit süre (❓ müşteriye doğrulatılmalı, Bölüm 6).
    public const int MovementDurationSeconds = 5;

    // 🛠️ state.io incelemesi sonrası eklendi (Bölüm 6/15): sürükle-bırak gönderiminde
    // client asker sayısı belirtmez, sunucu kaynak bölgenin mevcut askerinden bu kadarını
    // her zaman garrison olarak bırakır, kalanının tamamını gönderir (❓ doğrulanmalı).
    public const int MinGarrisonPerSend = 1;

    // ---- Maç sonu / terk etme ----
    // 🛠️ Süre sınırı yok — maç yalnızca tek oyuncu kalınca biter (❓ Bölüm 8).
    public const int AbandonmentTimeoutSeconds = 90;

    // 🛠️ Turtling'i (sonsuza kadar biriktirme) anlamsız kılan Ana Kale asker tavanı
    // (Bölüm 4/12, state.io incelemesi sonrası eklendi, ❓ doğrulanmalı). Üretim bu
    // sınıra ulaşınca durur, sınırın altına inince otomatik devam eder.
    public const int MaxAccumulatedTroops = 200;

    // 🛠️ Saldırı yalnızca doğrudan komşu bölgeye yapılabilir — state.io'nun serbest-hedef
    // mekaniğinden bilinçli bir sapma (Bölüm 3/12/15-D.1, ❓ doğrulanmalı).
    public const bool AttackAdjacencyOnly = true;

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
