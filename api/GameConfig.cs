namespace api;

/// <summary>
/// "Porsuk Savaşları" oyun motorunun tüm sayısal sabitleri. Kod içinde magic
/// number kullanılmaz; her değer buradan okunur.
///
/// Müşteri talimatında birebir verilen değerler yorumla işaretlenmiştir.
/// Müşterinin netleştirmediği değerler (mühendislik varsayımı) ayrıca
/// gerekçesiyle belirtilmiştir; hepsi tek yerden değiştirilebilir.
/// </summary>
public static class GameConfig
{
    // ---- Harita ----
    // 🔒 Müşteri talimatı: 12 şehir/bölge, her biri 3 komşuluk/giriş rotasına sahip.
    public const int RegionCount = 12;
    public const int NeighborsPerRegion = 3;

    // ---- Oyuncu / Maç ----
    // 🛠️ Varsayım: varsayılan 1v1, ama Match.Players liste olduğu için N oyuncuya genişletilebilir.
    public const int DefaultPlayerCount = 2;

    // 🛠️ Varsayım: maç süresi hedefi 10-15 dk olduğundan üst sınır 15 dk olarak sabitlendi.
    // Süre dolduğunda en çok bölge/yapıya sahip oyuncu kazanır (sudden-death).
    public const int MatchDurationSeconds = 15 * 60;

    // 🛠️ Varsayım: nötr bölgelerin başlangıç garnizonu. Düşük tutuldu ki erken oyunda
    // yayılma stratejisi (expansion) mümkün olsun; tek bir asker bile taşıyan ilk
    // saldırı nötr bölgeyi düşürebilir ama savunmasız da değil.
    public const int NeutralRegionDefenseSoldiers = 2;

    // ---- Ekonomi tick ----
    // 🛠️ Varsayım: "dakikada X" üretim sunucuda saniyelik tick'e bölünüp kesirli
    // birikimle işlenir (60 saniyede 1 tam asker gibi) — kesirli asker oluşmaz.
    public const int EconomyTickIntervalSeconds = 1;

    // ---- Porsuk Yuvası / Kale seviyeleri ----
    // 🔒 Müşteri talimatı (Bölüm 3.3) — dakika başına üretim.
    public const int NestLevel1GoldPerMinute = 1000;
    public const int NestLevel1SoldiersPerMinute = 1;

    public const int NestLevel2GoldPerMinute = 2500;
    public const int NestLevel2SoldiersPerMinute = 3;
    public const int NestLevel2ArcherCount = 1; // Seviye 2'de toplam okçu sayısı

    public const int NestLevel3GoldPerMinute = 5000;
    public const int NestLevel3SoldiersPerMinute = 5;
    public const int NestLevel3ArcherCount = 3; // Seviye 3'te toplam okçu sayısı

    public const int MaxNestLevel = 3;

    // 🛠️ Varsayım: müşteri upgrade maliyetlerini belirtmedi. Seviye başına dakikalık
    // altın üretiminin yaklaşık 2x'i (seviye 2) ve seviye 3 üretiminin ~2.4x'i olacak
    // şekilde, oyuncunun üretime mi yükseltmeye mi yatırım yapacağına karar vermesini
    // gerektirecek kadar anlamlı bir maliyet seçildi (10-15 dk'lık maç temposuna uygun).
    public const int NestUpgradeToLevel2Cost = 5000;
    public const int NestUpgradeToLevel3Cost = 12000;

    // 🛠️ Varsayım: yükseltme anlık uygulanır (inşa süresi yok). Kısa maç temposunda
    // (10-15 dk) zamanlama baskısı zaten "ne zaman harcarım" kararından geliyor;
    // ayrıca bir inşa süresi eklemek müşteri tarafından istenmedi.
    public const int NestUpgradeDurationSeconds = 0;

    // 🛠️ Varsayım: savunmada okçuların kattığı ekstra güç katsayısı. Bir okçu,
    // savunma gücü hesabında normal bir askerin 2 katı ağırlığa sahiptir.
    public const double ArcherDefenseMultiplier = 2.0;

    // ---- Asker / General ----
    // 🔒 Müşteri talimatı: Normal Asker 1.000 Altın, General Porsuk 1.000 Altın.
    public const int SoldierCost = 1000;
    public const int GeneralCost = 1000;

    // 🛠️ Varsayım: General ölümünde otomatik yeniden basım, standart General
    // üretimiyle aynı maliyet ve süreye tabidir (bedava/anlık değildir). Ayrı
    // parametreler olarak tanımlandı ki ileride "ölüm cezası olmasın" denirse
    // GeneralRespawnCost = 0 yapılabilsin.
    public const int GeneralRespawnCost = GeneralCost;
    public const int GeneralRespawnTimeSeconds = 30;

    // 🛠️ Varsayım: müşteri üst sınır belirtmedi, dengeli olması için 2 ile sınırlı
    // (2. General ile 2 farklı şehre eş zamanlı saldırı kuralıyla uyumlu).
    public const int MaxGeneralsPerPlayer = 2;

    // ---- Hareket / Seyahat süresi ----
    // 🛠️ Varsayım: bölgeler arası hareket anlık değil; harita JSON'undaki her
    // komşuluk bağlantısının bir "distanceUnits" değeri var. Seyahat süresi bu
    // mesafeyle orantılı hesaplanır ve 5-15 saniye aralığına sıkıştırılır (clamp),
    // böylece 10-15 dakikalık maç hedefiyle uyumlu kalır.
    public const double TravelSecondsPerDistanceUnit = 1.5;
    public const int MinTravelTimeSeconds = 5;
    public const int MaxTravelTimeSeconds = 15;

    // ---- SignalR / network ----
    // 🛠️ Varsayım: state broadcast throttle aralığı (Bölüm 6.1 performans notu).
    public const int StateBroadcastThrottleMs = 500;
}
