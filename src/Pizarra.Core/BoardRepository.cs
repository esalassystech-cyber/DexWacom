using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pizarra.Core;

public sealed class BoardRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task SaveAsync(BoardDocument board, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Validate(board);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("El tablero debe guardarse dentro de una carpeta.", nameof(path));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, board, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<BoardDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var board = await JsonSerializer.DeserializeAsync<BoardDocument>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("El archivo no contiene un tablero.");
            board = Migrate(board);
            Validate(board);
            return board;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("El archivo de tablero está dañado o usa un formato desconocido.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("El tablero contiene datos inválidos.", ex);
        }
    }

    private static BoardDocument Migrate(BoardDocument board) => board.SchemaVersion switch
    {
        1 => board with { SchemaVersion = BoardDocument.CurrentSchemaVersion, FreehandStrokes = [] },
        BoardDocument.CurrentSchemaVersion => board,
        _ => throw new InvalidDataException($"La versión de tablero {board.SchemaVersion} no es compatible.")
    };

    private static void Validate(BoardDocument board)
    {
        if (board.SchemaVersion != BoardDocument.CurrentSchemaVersion)
            throw new InvalidDataException($"La versión de tablero {board.SchemaVersion} no es compatible.");
        if (board.Id == Guid.Empty || string.IsNullOrWhiteSpace(board.Name))
            throw new InvalidDataException("El tablero necesita un identificador y un nombre.");

        var ids = new HashSet<Guid>();
        foreach (var element in board.Elements)
        {
            if (element.Id == Guid.Empty || !ids.Add(element.Id))
                throw new InvalidDataException("Los elementos deben tener identificadores únicos.");
            if (!double.IsFinite(element.X) || !double.IsFinite(element.Y) ||
                !double.IsFinite(element.Width) || !double.IsFinite(element.Height) ||
                element.Width <= 0 || element.Height <= 0)
                throw new InvalidDataException("El tamaño o la posición de un elemento no es válido.");

            if (element is StickyNote note && note.Strokes.Any(stroke => stroke.Points.Any(point =>
                    !double.IsFinite(point.X) || !double.IsFinite(point.Y) ||
                    !double.IsFinite(point.Pressure) || point.Pressure is < 0 or > 1)))
                throw new InvalidDataException("Un trazo contiene puntos o presión fuera de rango.");
        }

        if (board.FreehandStrokes.Any(stroke => stroke.Points.Any(point =>
                !double.IsFinite(point.X) || !double.IsFinite(point.Y) ||
                !double.IsFinite(point.Pressure) || point.Pressure is < 0 or > 1)))
            throw new InvalidDataException("Un trazo del lienzo contiene puntos o presión fuera de rango.");

        foreach (var connector in board.Connectors)
        {
            if (connector.Id == Guid.Empty || !ids.Contains(connector.FromElementId) || !ids.Contains(connector.ToElementId))
                throw new InvalidDataException("Una flecha apunta a un elemento que no existe.");
        }
    }
}
