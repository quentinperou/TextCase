using TextCase.Core;

namespace TextCase.Tests;

public class CaseConverterTests
{
    private const string Canon = "CANON EOS R6 MARK II BOÎTIER NU";

    private static string Convert(string text, CaseMode mode) => CaseConverter.Convert(text, mode);

    // The cases of the Chrome extension, on the author's kind of text (a supplier's product name in capitals).
    [Theory]
    [InlineData(CaseMode.Upper, "CANON EOS R6 MARK II BOÎTIER NU")]
    [InlineData(CaseMode.Lower, "canon eos r6 mark ii boîtier nu")]
    [InlineData(CaseMode.Title, "Canon EOS R6 Mark II Boîtier Nu")]
    [InlineData(CaseMode.Sentence, "Canon EOS R6 mark II boîtier nu")]
    [InlineData(CaseMode.Camel, "canonEosR6MarkIiBoîtierNu")]
    [InlineData(CaseMode.Pascal, "CanonEosR6MarkIiBoîtierNu")]
    [InlineData(CaseMode.Constant, "CANON_EOS_R6_MARK_II_BOÎTIER_NU")]
    [InlineData(CaseMode.Param, "canon-eos-r6-mark-ii-boîtier-nu")]
    [InlineData(CaseMode.Snake, "canon_eos_r6_mark_ii_boîtier_nu")]
    [InlineData(CaseMode.Dot, "canon.eos.r6.mark.ii.boîtier.nu")]
    [InlineData(CaseMode.Toggle, "canon eos r6 mark ii boîtier nu")]
    [InlineData(CaseMode.Alternating, "cAnOn EoS r6 MaRk Ii BoÎtIeR nU")]
    [InlineData(CaseMode.NoAccents, "CANON EOS R6 MARK II BOITIER NU")]
    [InlineData(CaseMode.NoCase, "canon eos r6 mark ii boîtier nu")]
    [InlineData(CaseMode.PlainText, Canon)]
    public void EveryMode_OnAProductName(CaseMode mode, string expected) =>
        Assert.Equal(expected, Convert(Canon, mode));

    [Theory]
    [InlineData("le nouveau boîtier de canon", "Le Nouveau Boîtier de Canon")]
    [InlineData("guide de l'appareil photo", "Guide de l'Appareil Photo")]
    [InlineData("l'objectif d'un pro", "L'Objectif d'un Pro")]
    [InlineData("don't stop: it's here", "Don't Stop: It's Here")]
    [InlineData("aujourd'hui à paris", "Aujourd'hui à Paris")]
    [InlineData("saint-jean-de-luz", "Saint-Jean-de-Luz")]
    [InlineData("the lord of the rings", "The Lord of the Rings")]
    [InlineData("objectif rf 24-70MM f/2.8L", "Objectif RF 24-70mm F/2.8L")]
    [InlineData("mon iphone et mon IPAD", "Mon iPhone et Mon iPad")]
    [InlineData("le McDonald de la PowerShot", "Le McDonald de la PowerShot")]
    [InlineData("canon – nouveautés", "Canon – Nouveautés")]
    public void Title_IsSmart(string text, string expected) =>
        Assert.Equal(expected, Convert(text, CaseMode.Title));

    [Fact]
    public void Title_KeepsTheAcronymsOfALineNotInCapitals() =>
        Assert.Equal("Le Boîtier EOS et le SAV FNAC", Convert("le boîtier EOS et le SAV FNAC", CaseMode.Title));

    [Fact]
    public void Title_SmallWordsCanBeCapitalizedToo() =>
        Assert.Equal("Le Nouveau Boîtier De Canon",
            CaseConverter.Convert("le nouveau boîtier de canon", CaseMode.Title, new CaseRules(CaseRules.DefaultKeepWords, smallWordsLowercase: false)));

    [Theory]
    [InlineData("BONJOUR. CECI EST UN TEST ! ET LÀ ?", "Bonjour. Ceci est un test ! Et là ?")]
    [InlineData("première ligne\nDEUXIÈME LIGNE", "Première ligne\nDeuxième ligne")]
    [InlineData("I THINK I'M RIGHT", "I think I'm right")]
    [InlineData("l'IPHONE 15 PRO MAX", "L'iPhone 15 pro max")]
    [InlineData("iphone en stock", "iPhone en stock")]
    [InlineData("Le Nouveau Boîtier", "Le nouveau boîtier")]
    public void Sentence_IsSmart(string text, string expected) =>
        Assert.Equal(expected, Convert(text, CaseMode.Sentence));

    [Fact]
    public void Sentence_ALineInCapitalsIsConvertedEvenNextToANormalOne() =>
        Assert.Equal("Titre du chapitre\r\nLe texte du SAV.", Convert("TITRE DU CHAPITRE\r\nLe texte du SAV.", CaseMode.Sentence));

    [Fact]
    public void KeepWords_ComeFromTheUserList()
    {
        var rules = new CaseRules(["Mark", "ProShop"]);
        Assert.Equal("Canon eos R6 Mark II", CaseConverter.Convert("CANON EOS R6 MARK II", CaseMode.Sentence, rules));
        Assert.Equal("Chez ProShop", CaseConverter.Convert("CHEZ PROSHOP", CaseMode.Title, rules));
    }

    // The smart mode off (the switch of the popup): every letter changes, nothing is kept.
    [Theory]
    [InlineData("CANON EOS R6 MARK II BOÎTIER NU", "Canon Eos R6 Mark Ii Boîtier Nu")]
    [InlineData("le nouveau boîtier de canon", "Le Nouveau Boîtier De Canon")]
    [InlineData("saint-jean-de-luz noir/blanc", "Saint-Jean-De-Luz Noir/Blanc")]
    [InlineData("l'objectif d'un pro (sigma)", "L'objectif D'un Pro (Sigma)")]
    [InlineData("OC SIGMA 35MM F1.4 DG DN", "Oc Sigma 35mm F1.4 Dg Dn")]
    [InlineData("mon iPhone", "Mon Iphone")]
    public void Title_Simple(string text, string expected) =>
        Assert.Equal(expected, CaseConverter.Convert(text, CaseMode.Title, CaseRules.Simple));

    [Theory]
    [InlineData("OC SIGMA 35MM F1.4 DG DN SONY 03/26", "Oc sigma 35mm f1.4 dg dn sony 03/26")]
    [InlineData("BONJOUR. CECI EST UN TEST ! ET LÀ ?", "Bonjour. Ceci est un test ! Et là ?")]
    [InlineData("première ligne\r\nDEUXIÈME LIGNE", "Première ligne\r\nDeuxième ligne")]
    [InlineData("mon iPhone EOS", "Mon iphone eos")]
    public void Sentence_Simple(string text, string expected) =>
        Assert.Equal(expected, CaseConverter.Convert(text, CaseMode.Sentence, CaseRules.Simple));

    [Fact]
    public void Lower_IsAlwaysLowerFirstLetterIncluded() =>
        Assert.Equal("oc sigma 35mm f1.4 dg dn sony 03/26 57414544", Convert("Oc sigma 35mm f1.4 dg dn sony 03/26 57414544", CaseMode.Lower));

    [Theory]
    [InlineData("HTTPServer error", "httpServerError")]
    [InlineData("already_snake-or kebab", "alreadySnakeOrKebab")]
    [InlineData("  indented line  ", "  indentedLine  ")]
    [InlineData("l'appareil photo", "lAppareilPhoto")]
    public void Camel_SplitsWordsLikeChangeCase(string text, string expected) =>
        Assert.Equal(expected, Convert(text, CaseMode.Camel));

    [Fact]
    public void CodeFormats_WorkLineByLine() =>
        Assert.Equal("first_line\r\n\r\nsecond_line\n", Convert("First line\r\n\r\nSecond Line\n", CaseMode.Snake));

    [Theory]
    [InlineData("someCamelCase", "some camel case")]
    [InlineData("SNAKE_CASE_TEXT", "snake case text")]
    [InlineData("dot.case.text", "dot case text")]
    public void NoCase_UndoesTheCodeFormats(string text, string expected) =>
        Assert.Equal(expected, Convert(text, CaseMode.NoCase));

    [Theory]
    [InlineData("Élève à l'été, ça coûte", "Eleve a l'ete, ca coute")]
    [InlineData("Œuvre, ægis, straße, Łódź, Ørsted", "OEuvre, aegis, strasse, Lodz, Orsted")]
    [InlineData("Ελληνικά, кириллица", "Ελληνικά, кириллица")]
    public void NoAccents(string text, string expected) =>
        Assert.Equal(expected, Convert(text, CaseMode.NoAccents));

    [Fact]
    public void Toggle_SwapsEachLetter() =>
        Assert.Equal("tOGGLE cASE 42", Convert("Toggle Case 42", CaseMode.Toggle));

    [Theory]
    [InlineData("  Trop   d'espaces\tici  ", "Trop d'espaces ici")]
    [InlineData("\n\nLigne 1  \n\n\n\n  Ligne 2\n", "Ligne 1\n\nLigne 2\n")]
    [InlineData("a\r\n\r\n\r\nb", "a\r\n\r\nb")]
    [InlineData("Espace insécable gardé", "Espace insécable gardé")]
    public void CleanSpaces(string text, string expected) =>
        Assert.Equal(expected, Convert(text, CaseMode.CleanSpaces));

    [Theory]
    [InlineData("Une phrase\r\ncoupée en\r\ntrois lignes", "Une phrase coupée en trois lignes")]
    [InlineData("la photo-\ngraphie\n\nnouveau paragraphe\n", "la photo-graphie nouveau paragraphe\n")]
    [InlineData("une seule ligne", "une seule ligne")]
    public void JoinLines(string text, string expected) =>
        Assert.Equal(expected, Convert(text, CaseMode.JoinLines));

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    [InlineData("   ")]
    [InlineData("1234 - 5678")]
    public void NoMode_FailsOnTextWithoutWords(string text)
    {
        foreach (var mode in CaseModes.All)
            _ = Convert(text, mode);
    }

    [Fact]
    public void ParseWords_AcceptsLinesCommasAndSpaces() =>
        Assert.Equal(["EOS", "RF", "iPhone", "Wi-Fi"], CaseRules.ParseWords("EOS, RF\r\niPhone;  Wi-Fi\n eos"));

    [Fact]
    public void Keys_OfThePopupRows()
    {
        Assert.Equal('1', CaseModes.KeyOf(0));
        Assert.Equal('9', CaseModes.KeyOf(8));
        Assert.Equal('A', CaseModes.KeyOf(9));
        Assert.Equal(16, CaseModes.IndexOfKey('h'));
        Assert.Equal(-1, CaseModes.IndexOfKey('0'));
        Assert.All(Enumerable.Range(0, 30), i => Assert.Equal(i, CaseModes.IndexOfKey(CaseModes.KeyOf(i)!.Value)));
    }

    [Fact]
    public void Groups_FollowTheExtensionsMenu()
    {
        Assert.Equal(CaseGroup.Case, CaseModes.GroupOf(CaseMode.Sentence));
        Assert.Equal(CaseGroup.Code, CaseModes.GroupOf(CaseMode.Constant));
        Assert.Equal(CaseGroup.Separators, CaseModes.GroupOf(CaseMode.Dot));
        Assert.Equal(CaseGroup.Other, CaseModes.GroupOf(CaseMode.NoCase));
        Assert.Equal(CaseGroup.Cleanup, CaseModes.GroupOf(CaseMode.PlainText));
    }
}

public class ShortcutTests
{
    [Theory]
    [InlineData("Ctrl+Alt+C", ShortcutModifiers.Ctrl | ShortcutModifiers.Alt, 0x43)]
    [InlineData("ctrl + shift + F5", ShortcutModifiers.Ctrl | ShortcutModifiers.Shift, 0x74)]
    [InlineData("Win+Num7", ShortcutModifiers.Win, 0x67)]
    [InlineData("Alt+Space", ShortcutModifiers.Alt, 0x20)]
    [InlineData("Ctrl+VK_BA", ShortcutModifiers.Ctrl, 0xBA)]
    [InlineData("F13", ShortcutModifiers.None, 0x7C)]
    public void Parse(string text, ShortcutModifiers modifiers, int key) =>
        Assert.Equal(new Shortcut(modifiers, key), Shortcut.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Hyper+C")]
    [InlineData("Ctrl+Whatever")]
    public void Parse_RejectsUnknownText(string text) => Assert.True(Shortcut.Parse(text).IsEmpty);

    [Theory]
    [InlineData("Ctrl+Alt+C")]
    [InlineData("Ctrl+Shift+Win+F12")]
    [InlineData("Alt+Num0")]
    [InlineData("Ctrl+PageDown")]
    public void ToString_RoundTrips(string text) => Assert.Equal(text, Shortcut.Parse(text).ToString());

    [Theory]
    [InlineData("Ctrl+Alt+C", true)]
    [InlineData("Win+U", true)]
    [InlineData("F9", true)]
    [InlineData("Shift+C", false)]
    [InlineData("C", false)]
    public void IsValid_NeedsCtrlAltWinOrAnFKey(string text, bool valid) => Assert.Equal(valid, Shortcut.Parse(text).IsValid);

    [Fact]
    public void IsValid_NeverAModifierAlone() =>
        Assert.False(new Shortcut(ShortcutModifiers.Ctrl, 0xA2).IsValid);
}

public class DoublePressTests
{
    [Fact]
    public void TwoPressesInTime_TriggerOnce()
    {
        var detector = new DoublePress { DelayMs = 400 };
        Assert.False(detector.Press(1000));
        Assert.True(detector.Press(1300));
        // A third press starts again.
        Assert.False(detector.Press(1500));
        Assert.True(detector.Press(1800));
    }

    [Fact]
    public void TooSlow_StartsAgain()
    {
        var detector = new DoublePress { DelayMs = 400 };
        Assert.False(detector.Press(1000));
        Assert.False(detector.Press(1500));
        Assert.True(detector.Press(1700));
    }

    [Fact]
    public void Reset_ForgetsTheFirstPress()
    {
        var detector = new DoublePress();
        detector.Press(1000);
        detector.Reset();
        Assert.False(detector.Press(1100));
    }

    [Fact]
    public void ClockGoingBack_IsNotADoublePress()
    {
        var detector = new DoublePress();
        detector.Press(5000);
        Assert.False(detector.Press(4900));
    }
}
