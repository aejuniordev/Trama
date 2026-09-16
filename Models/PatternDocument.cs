using System.Text.Json.Serialization;

namespace Trama.Models;

public enum TargetApp { Revit, AutoCad }
public enum PatUnits { Inches, Millimeters }
public enum PatternKind { Model, Drafting }

/// <summary>Linha desenhada dentro do quadro de repetição (unidades do padrão, eixo Y para cima).</summary>
public sealed class Segment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }

    [JsonIgnore] public Vec P1 => new(X1, Y1);
    [JsonIgnore] public Vec P2 => new(X2, Y2);

    public Segment Clone() => new() { Id = Id, X1 = X1, Y1 = Y1, X2 = X2, Y2 = Y2 };
}

/// <summary>Estado completo de um padrão em edição.</summary>
public sealed class PatternDocument
{
    public const string DefaultName = "sem-titulo";

    public string Name { get; set; } = DefaultName;
    public string Description { get; set; } = "";
    public TargetApp Target { get; set; } = TargetApp.Revit;
    public PatUnits Units { get; set; } = PatUnits.Inches;
    public PatternKind Kind { get; set; } = PatternKind.Model;

    /// <summary>Largura do quadro que se repete.</summary>
    public double TileWidth { get; set; } = 12;

    /// <summary>Altura do quadro que se repete.</summary>
    public double TileHeight { get; set; } = 12;

    public int GridDivisions { get; set; } = 12;

    /// <summary>
    /// Maior número de quadros que uma linha inclinada pode atravessar antes de se repetir.
    /// Valores altos aceitam ângulos "quebrados", mas geram linhas muito próximas.
    /// </summary>
    public int MaxComplexity { get; set; } = 24;

    public List<Segment> Segments { get; set; } = new();

    /// <summary>Quando verdadeiro, o .PAT é o texto de <see cref="ManualText"/> e não o gerado pelo desenho.</summary>
    public bool IsManual { get; set; }

    public string ManualText { get; set; } = "";

    public string UnitLabel => Units == PatUnits.Millimeters ? "mm" : "pol";
}
