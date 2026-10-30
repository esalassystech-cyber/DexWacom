using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class BoardShortcutResolverTests
{
    [Theory]
    [InlineData("N")]
    [InlineData("P")]
    [InlineData("Q")]
    [InlineData("D")]
    [InlineData("S")]
    [InlineData("C")]
    [InlineData("E")]
    public void SingleLetterKeysNeverCreateOrChangeCanvasObjects(string key)
        => Assert.Null(BoardShortcutResolver.Resolve(key, control: false, textInputFocused: false));

    [Fact]
    public void TextEntrySuppressesEvenCanvasShortcuts()
    {
        Assert.Null(BoardShortcutResolver.Resolve("P", control: true, textInputFocused: true));
        Assert.Null(BoardShortcutResolver.Resolve("Delete", control: false, textInputFocused: true));
    }

    [Fact]
    public void KeepsOnlyExplicitStandardEditingShortcuts()
    {
        Assert.Equal(BoardShortcutCommand.Save, BoardShortcutResolver.Resolve("S", control: true, textInputFocused: false));
        Assert.Equal(BoardShortcutCommand.Undo, BoardShortcutResolver.Resolve("Z", control: true, textInputFocused: false));
        Assert.Equal(BoardShortcutCommand.Redo, BoardShortcutResolver.Resolve("Y", control: true, textInputFocused: false));
        Assert.Equal(BoardShortcutCommand.Delete, BoardShortcutResolver.Resolve("Delete", control: false, textInputFocused: false));
    }
}
