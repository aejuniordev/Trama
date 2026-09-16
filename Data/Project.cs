using Trama.Models;

namespace Trama.Data;

/// <summary>Projeto salvo no SQLite.</summary>
public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TargetApp Target { get; set; }
    public PatUnits Units { get; set; }
    public PatternKind Kind { get; set; }
    public double TileWidth { get; set; }
    public double TileHeight { get; set; }
    public int GridDivisions { get; set; }
    public int MaxComplexity { get; set; }

    /// <summary>Linhas do desenho em JSON.</summary>
    public string SegmentsJson { get; set; } = "[]";
    public int SegmentCount { get; set; }

    public bool IsManual { get; set; }
    public string ManualText { get; set; } = "";

    /// <summary>Último .PAT gerado, guardado para consulta e exportação direta pelo banco.</summary>
    public string PatText { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record ProjectSummary(
    int Id, string Name, TargetApp Target, PatUnits Units, int SegmentCount, bool IsManual, DateTime UpdatedAt);
