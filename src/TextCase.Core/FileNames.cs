namespace TextCase.Core;

/// <summary>
/// The case of a file or folder name, for the menu of File Explorer: the name of a file changes, not its extension
/// ("CANON EOS R6.JPG" → "Canon EOS R6.JPG"); a folder's whole name changes.
/// </summary>
public static class FileNames
{
    /// <summary>The new name, or null when the case changes nothing (or would leave no name).</summary>
    public static string? Convert(string name, bool isFolder, CaseMode mode, CaseRules? rules = null)
    {
        // ".gitignore" has no name before its extension: nothing to change.
        int dot = isFolder ? -1 : name.LastIndexOf('.');
        string stem = dot > 0 ? name[..dot] : name;
        string extension = dot > 0 ? name[dot..] : "";
        if (!isFolder && dot == 0)
            return null;

        string converted = CaseConverter.Convert(stem, mode, rules).Trim();
        if (converted.Length == 0 || converted.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;
        string result = converted + extension;
        return result == name ? null : result;
    }
}
