using TextCase.Core;

namespace TextCase.Tests;

public class FileNamesTests
{
    [Theory]
    [InlineData("CANON EOS R6.JPG", CaseMode.Lower, "canon eos r6.JPG")]
    [InlineData("photo de vacances.jpeg", CaseMode.Upper, "PHOTO DE VACANCES.jpeg")]
    [InlineData("rapport annuel 2026.docx", CaseMode.Snake, "rapport_annuel_2026.docx")]
    [InlineData("archive.tar.gz", CaseMode.Upper, "ARCHIVE.TAR.gz")]
    [InlineData("Élève  été.txt", CaseMode.NoAccents, "Eleve  ete.txt")]
    [InlineData("sans extension", CaseMode.Upper, "SANS EXTENSION")]
    public void File_TheNameChanges_NotTheExtension(string name, CaseMode mode, string expected) =>
        Assert.Equal(expected, FileNames.Convert(name, isFolder: false, mode));

    [Fact]
    public void Folder_TheWholeNameChanges() =>
        Assert.Equal("PHOTOS.2026", FileNames.Convert("photos.2026", isFolder: true, CaseMode.Upper));

    [Theory]
    [InlineData(".gitignore", CaseMode.Upper)]
    [InlineData("DEJA.TXT", CaseMode.Upper)]
    [InlineData("   .txt", CaseMode.Upper)]
    public void NothingToChange_GivesNull(string name, CaseMode mode) =>
        Assert.Null(FileNames.Convert(name, isFolder: false, mode));

    [Fact]
    public void TitleCase_UsesTheRules() =>
        Assert.Equal("Canon EOS R6 Mark II.cr3",
            FileNames.Convert("CANON EOS R6 MARK II.cr3", isFolder: false, CaseMode.Title, CaseRules.Default));
}
