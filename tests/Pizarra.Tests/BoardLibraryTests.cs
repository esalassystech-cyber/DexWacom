using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class BoardLibraryTests
{
    [Fact]
    public void NextNameUsesTheFirstFreeNumber()
    {
        Assert.Equal("Tablero 1", BoardLibrary.NextName("Tablero", []));
        Assert.Equal("Tablero 2", BoardLibrary.NextName("Tablero", ["Tablero 1", "Tablero 3"]));
        Assert.Equal("Tablero 2", BoardLibrary.NextName("Tablero", ["tablero 1"]));
    }

    [Fact]
    public void UniqueBoardPathNeverOverwritesAnotherBoardButKeepsItsOwnPath()
    {
        var dir = Directory.CreateTempSubdirectory("pizarra-lib-").FullName;
        try
        {
            var first = BoardLibrary.UniqueBoardPath(dir, "Ideas");
            File.WriteAllText(first, "{}");

            Assert.Equal(Path.Combine(dir, "Ideas (2).flowboard"), BoardLibrary.UniqueBoardPath(dir, "Ideas"));
            Assert.Equal(first, BoardLibrary.UniqueBoardPath(dir, "Ideas", currentPath: first));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SafeNameReplacesCharactersWindowsRejects()
    {
        Assert.Equal("Plan-Q1", BoardLibrary.SafeName("Plan/Q1"));
        Assert.Equal("Tablero", BoardLibrary.SafeName("   "));
    }
}
