namespace Pizarra.Core;

public enum BoardShortcutCommand { Save, Undo, Redo, Delete }

public static class BoardShortcutResolver
{
    public static BoardShortcutCommand? Resolve(string key, bool control, bool textInputFocused)
    {
        if (textInputFocused) return null;
        if (control)
        {
            return key.ToUpperInvariant() switch
            {
                "S" => BoardShortcutCommand.Save,
                "Z" => BoardShortcutCommand.Undo,
                "Y" => BoardShortcutCommand.Redo,
                _ => null
            };
        }
        return key.Equals("Delete", StringComparison.OrdinalIgnoreCase) ? BoardShortcutCommand.Delete : null;
    }
}
