using System.Text.Json;
using Trama.Models;

namespace Trama.Data;

public static class ProjectMapper
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Project ToEntity(PatternDocument doc, int id, string patText) => new()
    {
        Id = id,
        Name = doc.Name,
        Description = doc.Description,
        Target = doc.Target,
        Units = doc.Units,
        Kind = doc.Kind,
        TileWidth = doc.TileWidth,
        TileHeight = doc.TileHeight,
        GridDivisions = doc.GridDivisions,
        MaxComplexity = doc.MaxComplexity,
        SegmentsJson = JsonSerializer.Serialize(doc.Segments, Json),
        SegmentCount = doc.Segments.Count,
        IsManual = doc.IsManual,
        ManualText = doc.ManualText,
        PatText = patText,
    };

    public static PatternDocument ToDocument(Project p)
    {
        List<Segment> segments;
        try
        {
            segments = JsonSerializer.Deserialize<List<Segment>>(p.SegmentsJson, Json) ?? new();
        }
        catch (JsonException)
        {
            segments = new();
        }

        return new PatternDocument
        {
            Name = p.Name,
            Description = p.Description,
            Target = p.Target,
            Units = p.Units,
            Kind = p.Kind,
            TileWidth = p.TileWidth > 0 ? p.TileWidth : 12,
            TileHeight = p.TileHeight > 0 ? p.TileHeight : 12,
            GridDivisions = p.GridDivisions > 0 ? p.GridDivisions : 12,
            MaxComplexity = p.MaxComplexity > 0 ? p.MaxComplexity : 24,
            Segments = segments,
            IsManual = p.IsManual,
            ManualText = p.ManualText,
        };
    }
}
