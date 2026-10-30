using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class BoardRepositoryTests
{
    [Fact]
    public async Task SaveAndLoadPreserveTextInkShapesAndConnections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.flowboard");
        var noteId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var board = new BoardDocument
        {
            Id = Guid.NewGuid(),
            Name = "Checkout",
            Elements =
            [
                new StickyNote
                {
                    Id = noteId,
                    X = 10,
                    Y = 20,
                    Width = 220,
                    Height = 180,
                    Title = "Entrada",
                    Text = "Validar pedido",
                    Strokes =
                    [
                        new InkStroke
                        {
                            Points = [new InkPoint(12.25, 18.5, 0.75), new InkPoint(15.5, 22, 0.35)]
                        }
                    ]
                },
                new WorkflowShape
                {
                    Id = processId,
                    X = 320,
                    Y = 40,
                    Width = 170,
                    Height = 100,
                    Kind = WorkflowShapeKind.Decision,
                    Label = "¿Pago aprobado?"
                }
            ],
            Connectors = [new WorkflowConnector { FromElementId = noteId, ToElementId = processId }],
            FreehandStrokes = [new InkStroke { Points = [new InkPoint(560, 310, .2), new InkPoint(581.5, 324.25, .93)] }]
        };

        try
        {
            var repository = new BoardRepository();
            await repository.SaveAsync(board, path);
            var loaded = await repository.LoadAsync(path);

            Assert.Equal(board.Id, loaded.Id);
            Assert.Equal(board.Name, loaded.Name);
            var loadedNote = Assert.IsType<StickyNote>(loaded.Elements[0]);
            Assert.Equal("Validar pedido", loadedNote.Text);
            Assert.Equal(0.35, loadedNote.Strokes[0].Points[1].Pressure, 3);
            var loadedShape = Assert.IsType<WorkflowShape>(loaded.Elements[1]);
            Assert.Equal(WorkflowShapeKind.Decision, loadedShape.Kind);
            Assert.Equal(noteId, loaded.Connectors[0].FromElementId);
            Assert.Equal(processId, loaded.Connectors[0].ToElementId);
            Assert.Equal(new InkPoint(581.5, 324.25, .93), loaded.FreehandStrokes.Single().Points[1]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task RejectsUnknownSchemaAndDoesNotOverwriteExistingBoard()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.flowboard");
        var repository = new BoardRepository();
        var board = new BoardDocument { Id = Guid.NewGuid(), Name = "Safe", Elements = [], Connectors = [] };

        try
        {
            await repository.SaveAsync(board, path);
            var original = await File.ReadAllBytesAsync(path);
            await File.WriteAllTextAsync(path, "{\"schemaVersion\":99,\"id\":\"00000000-0000-0000-0000-000000000001\",\"name\":\"future\"}");

            await Assert.ThrowsAsync<InvalidDataException>(() => repository.LoadAsync(path));
            await File.WriteAllBytesAsync(path, original);

            var invalid = board with
            {
                Connectors = [new WorkflowConnector { FromElementId = Guid.NewGuid(), ToElementId = Guid.NewGuid() }]
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(invalid, path));

            var after = await repository.LoadAsync(path);
            Assert.Equal(board.Id, after.Id);
            Assert.Equal("Safe", after.Name);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadsVersionOneBoardAndMigratesItInMemoryWithoutLosingContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.flowboard");
        var boardId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var json = $$"""
        {
          "schemaVersion": 1,
          "id": "{{boardId}}",
          "name": "Legado",
          "elements": [
            { "$kind": "sticky-note", "id": "{{noteId}}", "x": 3, "y": 4, "width": 200, "height": 140, "title": "Idea", "text": "No perder", "color": "amarillo", "strokes": [{ "points": [{ "x": 0.2, "y": 0.3, "pressure": 0.7 }] }] },
            { "$kind": "workflow-shape", "id": "{{processId}}", "x": 250, "y": 10, "width": 180, "height": 100, "kind": 0, "label": "Proceso" }
          ],
          "connectors": [{ "id": "{{Guid.NewGuid()}}", "fromElementId": "{{noteId}}", "toElementId": "{{processId}}" }]
        }
        """;

        try
        {
            await File.WriteAllTextAsync(path, json);
            var loaded = await new BoardRepository().LoadAsync(path);

            Assert.Equal(BoardDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal(boardId, loaded.Id);
            Assert.Empty(loaded.FreehandStrokes);
            Assert.Equal("No perder", Assert.IsType<StickyNote>(loaded.Elements[0]).Text);
            Assert.Equal(noteId, loaded.Connectors.Single().FromElementId);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
