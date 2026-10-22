namespace TextCase.Core;

/// <summary>
/// The rules of the "smart" cases (Title Case, Sentence case): the words kept as written whatever the case of the
/// selection (EOS, iPhone, mm…), edited by the user, and whether the small words of a title stay in lowercase.
/// </summary>
public sealed class CaseRules
{
    /// <summary>
    /// The list a new install starts with: common acronyms and units, brands written in a special way, and the photo
    /// brands and mounts the author's shop sells ("CANON EOS R6 MARK II" → "Canon EOS R6 Mark II"). Words that are also
    /// common words ("is", "go", "os") are left out: they would be changed in every sentence.
    /// </summary>
    public static IReadOnlyList<string> DefaultKeepWords { get; } =
    [
        // Acronyms
        "AF", "AI", "API", "APS-C", "CPU", "DSLR", "EVF", "GPS", "GPU", "HD", "HDMI", "HDR", "IBIS", "ISO", "JPEG",
        "LCD", "LED", "LUT", "NFC", "OIS", "OLED", "PC", "PDF", "RAW", "RGB", "SD", "SDHC", "SDXC", "SSD", "TTL", "TV",
        "UHS", "URL", "USB", "USB-C", "UV", "VR", "Wi-Fi", "4K", "8K", "CFexpress", "microSD",
        // French acronyms
        "CB", "HT", "RIB", "SAV", "SIRET", "TTC", "TVA",
        // Units
        "mm", "cm", "km", "kg", "mAh", "fps", "dB", "Hz", "kHz", "MHz", "GHz", "KB", "MB", "GB", "TB",
        // Names written in a special way
        "iPhone", "iPad", "iMac", "iOS", "macOS", "MacBook", "YouTube", "TikTok", "LinkedIn", "WhatsApp", "PayPal", "eBay",
        // Photo brands
        "Canon", "Nikon", "Sony", "Fujifilm", "Panasonic", "Lumix", "Leica", "Olympus", "Pentax", "Ricoh", "Sigma",
        "Tamron", "Samyang", "Tokina", "Zeiss", "Hasselblad", "DJI", "GoPro", "Insta360", "SanDisk", "Lexar", "Kingston",
        "Manfrotto", "Gitzo", "Benro", "Godox", "Profoto", "Elinchrom", "Lowepro", "Hoya", "Kenko", "Laowa", "Viltrox",
        "Epson", "Kodak", "Ilford", "Polaroid", "NIKKOR",
        // Mounts and lens series
        "EOS", "RF", "RF-S", "EF", "EF-S", "EF-M", "PowerShot", "IXUS", "DX", "FX", "FE", "GFX", "XF", "XC", "USM",
        "STM", "OSS", "WR", "HSM", "DG", "DN",
    ];

    /// <summary>Small words of a title kept in lowercase (not the first word), French and English.</summary>
    private static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "à", "au", "aux", "avec", "chez", "dans", "de", "des", "du", "en", "et", "la", "le", "les", "ou", "par", "pour",
        "sans", "sous", "sur", "un", "une", "vers",
        "a", "an", "and", "as", "at", "but", "by", "for", "in", "nor", "of", "on", "or", "per", "the", "to", "via", "vs",
    };

    /// <summary>French elisions: after "l'" a new word starts (l'Appareil), after "don'" it does not (Don't).</summary>
    private static readonly HashSet<string> Elisions = new(StringComparer.OrdinalIgnoreCase)
    {
        "c", "d", "j", "l", "m", "n", "s", "t", "qu", "jusqu", "lorsqu", "puisqu", "quoiqu", "presqu",
    };

    // Roman numerals of model names (Mark II, Gen IV): not I, V, X alone, which are letters too.
    private static readonly HashSet<string> RomanNumerals = new(StringComparer.OrdinalIgnoreCase)
    {
        "II", "III", "IV", "VI", "VII", "VIII", "IX", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX",
    };

    private readonly Dictionary<string, string> _keep = new(StringComparer.OrdinalIgnoreCase);

    public CaseRules(IEnumerable<string> keepWords, bool smallWordsLowercase = true, bool smart = true)
    {
        foreach (var word in keepWords)
        {
            string w = word.Trim();
            if (w.Length > 0)
                _keep.TryAdd(w, w);
        }
        SmallWordsLowercase = smallWordsLowercase;
        Smart = smart;
    }

    public static CaseRules Default { get; } = new(DefaultKeepWords);

    /// <summary>The rules of a simple Title Case and Sentence case: every letter changes, nothing is kept.</summary>
    public static CaseRules Simple { get; } = new([], smallWordsLowercase: false, smart: false);

    public bool SmallWordsLowercase { get; }

    /// <summary>
    /// The smart cases (the author's "mode intelligent", a switch of the popup): off, Title Case capitalizes every word
    /// and Sentence case only the start of each sentence, the rest in lowercase, whatever the list.
    /// </summary>
    public bool Smart { get; }

    /// <summary>The spelling of <paramref name="word"/> in the list, whatever its case.</summary>
    public bool TryKeep(string word, out string spelling)
    {
        if (_keep.TryGetValue(word, out var found))
        {
            spelling = found;
            return true;
        }
        spelling = word;
        return false;
    }

    internal static bool IsSmallWord(string word) => SmallWords.Contains(word);

    internal static bool IsElision(string word) => Elisions.Contains(word);

    internal static bool IsRomanNumeral(string word) => RomanNumerals.Contains(word);

    /// <summary>
    /// The words of a list typed by the user: one per line, or separated by commas, semicolons or spaces.
    /// </summary>
    public static List<string> ParseWords(string text) =>
        [.. text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
}
