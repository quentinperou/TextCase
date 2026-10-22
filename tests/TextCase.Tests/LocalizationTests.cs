using System.Text.RegularExpressions;
using System.Xml.Linq;
using TextCase.Core;

namespace TextCase.Tests;

/// <summary>Keeps the English and French UI strings (src/TextCase/Properties/*.resx) in step with each other and with the code.</summary>
public class LocalizationTests
{
    private static readonly string AppDir = Path.Combine(FindRepositoryRoot(), "src", "TextCase");

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TextCase.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (TextCase.slnx) not found.");
    }

    private static Dictionary<string, string> ReadStrings(string fileName) =>
        XDocument.Load(Path.Combine(AppDir, "Properties", fileName)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    private static HashSet<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).ToHashSet();

    [Fact]
    public void FrenchTranslation_HasExactlyTheEnglishKeys()
    {
        Assert.Equal(
            ReadStrings("Strings.resx").Keys.Order(),
            ReadStrings("Strings.fr.resx").Keys.Order());
    }

    [Fact]
    public void Translations_UseTheSamePlaceholders()
    {
        var french = ReadStrings("Strings.fr.resx");
        foreach (var (key, english) in ReadStrings("Strings.resx"))
            Assert.True(Placeholders(english).SetEquals(Placeholders(french[key])), $"Placeholders differ for \"{key}\".");
    }

    [Fact]
    public void EveryKeyUsedByTheApp_Exists()
    {
        var keys = ReadStrings("Strings.resx").Keys.ToHashSet();
        var sources = Directory.EnumerateFiles(AppDir, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText)
            .ToList();
        var used = sources
            .SelectMany(text => Regex.Matches(text, @"\{local:Tr (\w+)\}|Strings\.(?:Get|Format|Find)\(""(\w+)""[,)]|SetResourceReference\(\w+, ""(\w+)""\)"))
            .Select(m => m.Groups.Values.Skip(1).First(g => g.Success).Value)
            // Theme resources read the same way (styles, brushes), not strings.
            .Where(key => !key.EndsWith("Style") && !key.EndsWith("Bar") && !key.EndsWith("Card") && !key.EndsWith("Brush"))
            .ToList();
        // Strings.Get(condition ? "X" : "Y") inside a call.
        used.AddRange(sources
            .SelectMany(text => Regex.Matches(text, @"Strings\.(?:Get|Format)\(\w+ \? ""(\w+)"" : ""(\w+)""\)"))
            .SelectMany(m => new[] { m.Groups[1].Value, m.Groups[2].Value }));

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void KeysBuiltAtRunTime_Exist()
    {
        var keys = ReadStrings("Strings.resx").Keys.ToHashSet();
        // The names of the cases, Strings.Get("Mode" + mode): the popup, the settings.
        foreach (var mode in Enum.GetValues<CaseMode>())
            Assert.Contains("Mode" + mode, keys);
    }
}
