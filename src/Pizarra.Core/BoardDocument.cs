using System.Text.Json.Serialization;

namespace Pizarra.Core;

public sealed record BoardDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Nuevo tablero";
    public List<BoardElement> Elements { get; init; } = [];
    public List<WorkflowConnector> Connectors { get; init; } = [];
    public List<InkStroke> FreehandStrokes { get; init; } = [];
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(StickyNote), "sticky-note")]
[JsonDerivedType(typeof(WorkflowShape), "workflow-shape")]
public abstract record BoardElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 200;
    public double Height { get; init; } = 120;
}

public sealed record StickyNote : BoardElement
{
    public string Title { get; init; } = "Nota";
    public string Text { get; init; } = string.Empty;
    public string Color { get; init; } = "amarillo";
    public List<InkStroke> Strokes { get; init; } = [];
}

public sealed record WorkflowShape : BoardElement
{
    public WorkflowShapeKind Kind { get; init; }
    public string Label { get; init; } = string.Empty;
}

/// <summary>Stored as numbers: append new kinds at the end so saved boards keep their meaning.</summary>
public enum WorkflowShapeKind
{
    Process,
    Decision,
    Terminal,
    InputOutput
}

public sealed record WorkflowConnector
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid FromElementId { get; init; }
    public Guid ToElementId { get; init; }
}

public sealed record InkStroke
{
    public List<InkPoint> Points { get; init; } = [];
}

public readonly record struct InkPoint(double X, double Y, double Pressure);

public readonly record struct PointD(double X, double Y);

public readonly record struct RectD(double X, double Y, double Width, double Height);
