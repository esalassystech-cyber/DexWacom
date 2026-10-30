namespace Pizarra.Core;

/// <summary>Automatic names and file paths for projects and boards, so creating one never needs a dialog.</summary>
public static class BoardLibrary
{
    public const string BoardExtension = ".flowboard";

    /// <summary>"Tablero 1", "Tablero 2"…: the first number not already used, ignoring case.</summary>
    public static string NextName(string prefix, IEnumerable<string> existingNames)
    {
        var taken = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        for (var number = 1; ; number++)
        {
            var candidate = $"{prefix} {number}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    public static string SafeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        name = name.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(name) ? "Tablero" : name;
    }

    /// <summary>A path for <paramref name="name"/> in <paramref name="directory"/> that no other file uses; <paramref name="currentPath"/> may keep its own.</summary>
    public static string UniqueBoardPath(string directory, string name, string? currentPath = null)
    {
        var baseName = SafeName(name);
        for (var copy = 1; ; copy++)
        {
            var fileName = copy == 1 ? baseName : $"{baseName} ({copy})";
            var path = Path.Combine(directory, fileName + BoardExtension);
            if (!File.Exists(path) || string.Equals(Path.GetFullPath(path), currentPath is null ? null : Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase))
                return path;
        }
    }

    public static string UniqueDirectoryPath(string parent, string name, string? currentPath = null)
    {
        var baseName = SafeName(name);
        for (var copy = 1; ; copy++)
        {
            var path = Path.Combine(parent, copy == 1 ? baseName : $"{baseName} ({copy})");
            if (!Directory.Exists(path) || string.Equals(Path.GetFullPath(path), currentPath is null ? null : Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase))
                return path;
        }
    }
}
