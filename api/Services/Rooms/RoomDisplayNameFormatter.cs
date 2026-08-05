using api.Models.Rooms;

namespace api.Services.Rooms;

/// <summary>
/// docs/08-page-content.md Bölüm 3.4 "Oda kimliği içeriği": `/lobi` oda listesinde
/// gösterilen kimlik metni, docs/03-game-rules.md'nin verdiği alan listesine yeni
/// bir "oda adı" formu eklemeden, VIP kurucusunun mevcut görünen adından türetilir
/// (ör. "Ali'nin Odası"). Standart odada kurucu kavramı yoktur (Room.CreatorPlayerId
/// boş), bu yüzden sabit bir etiket kullanılır.
/// </summary>
public static class RoomDisplayNameFormatter
{
    public static string Format(RoomType roomType, string? creatorDisplayName)
    {
        if (roomType != RoomType.Vip || string.IsNullOrWhiteSpace(creatorDisplayName))
        {
            return roomType == RoomType.Vip ? "VIP Oda" : "Standart Oda";
        }

        return $"{creatorDisplayName}{GenitiveSuffix(creatorDisplayName)} Odası";
    }

    /// <summary>
    /// Türkçe iyelik/tamlayan eki (genitive): ünlü uyumuna göre "'in/'ın/'un/'ün";
    /// ad sesli harfle bitiyorsa araya "n" tampon harfi girer ("'nin/'nın/'nun/'nün").
    /// </summary>
    private static string GenitiveSuffix(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "'ın";
        }

        var endsInVowel = VowelGroup(trimmed[^1]) != '\0';
        var suffixVowel = FindLastVowelGroup(trimmed);
        var buffer = endsInVowel ? "n" : "";
        return $"'{buffer}{suffixVowel}n";
    }

    private static char FindLastVowelGroup(string name)
    {
        for (var i = name.Length - 1; i >= 0; i--)
        {
            var group = VowelGroup(name[i]);
            if (group != '\0')
            {
                return group;
            }
        }

        // Tanınabilir bir Türkçe ünlü içermeyen adlar için nötr varsayılan.
        return 'ı';
    }

    /// <summary>Karakteri dört ünlü uyumu grubundan birine eşler; Türkçe kültüre bağlı ToLower() çağrılmaz (İ/I hatalarından kaçınmak için).</summary>
    private static char VowelGroup(char c) => c switch
    {
        'e' or 'E' or 'i' or 'İ' => 'i',
        'a' or 'A' or 'ı' or 'I' => 'ı',
        'o' or 'O' or 'u' or 'U' => 'u',
        'ö' or 'Ö' or 'ü' or 'Ü' => 'ü',
        _ => '\0'
    };
}
