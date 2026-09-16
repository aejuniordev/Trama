using Trama.Models;

namespace Trama.Services;

public sealed record SegmentFamily(Guid SegmentId, PatLine Line, LineStatus Status, string? Message);

public sealed class GenerationResult
{
    public required string Text { get; init; }
    public required IReadOnlyList<TextLine> Lines { get; init; }
    public required IReadOnlyList<SegmentFamily> Families { get; init; }
    public required IReadOnlyDictionary<Guid, (LineStatus Status, string? Message)> StatusBySegment { get; init; }

    public int Warnings => Lines.Count(l => l.Status == LineStatus.Warning);
    public int Errors => Lines.Count(l => l.Status == LineStatus.Error);

    public (LineStatus Status, string? Message) StatusFor(Guid id) =>
        StatusBySegment.TryGetValue(id, out var s) ? s : (LineStatus.Ok, null);
}

public static class PatGenerator
{
    /// <summary>A partir deste tamanho (em quadros) a linha fica tão próxima das vizinhas que o Revit pode recusar.</summary>
    private const int DenseThreshold = 10;

    public static GenerationResult Generate(PatternDocument doc)
    {
        var lines = new List<TextLine>();
        var families = new List<SegmentFamily>();
        var statuses = new Dictionary<Guid, (LineStatus, string?)>();
        var revit = doc.Target == TargetApp.Revit;
        var mm = doc.Units == PatUnits.Millimeters;

        var name = PatFormat.SanitizeName(doc.Name, doc.Target);
        var description = PatFormat.SanitizeDescription(doc.Description);
        var header = "*" + name + (description.Length > 0 ? ", " + description : "");
        if (header.Length > 80) header = header[..80].TrimEnd();

        if (revit)
        {
            lines.Add(new TextLine($";%UNITS={(mm ? "MM" : "INCH")}"));
            lines.Add(new TextLine(";%VERSION=3.0"));
            lines.Add(new TextLine(";"));
            lines.Add(new TextLine(header));
            lines.Add(new TextLine($";%TYPE={(doc.Kind == PatternKind.Drafting ? "DRAFTING" : "MODEL")}"));
            lines.Add(new TextLine(";"));
            lines.Add(new TextLine(";angle, x, y, shift, offset, dash, space"));
        }
        else
        {
            lines.Add(new TextLine($";; Unidades: {(mm ? "milimetros (use acadiso.pat / MEASUREMENT=1)" : "polegadas")}"));
            lines.Add(new TextLine(header));
        }

        double w = doc.TileWidth, h = doc.TileHeight;

        if (doc.Segments.Count == 0)
        {
            lines.Add(new TextLine(";; Desenhe ao menos uma linha no quadro.", LineStatus.Warning,
                "Um padrão precisa de pelo menos uma linha de dados."));
        }

        foreach (var s in doc.Segments)
        {
            var d = s.P2 - s.P1;
            if (d.Length < HatchMath.Eps)
            {
                Fail(s, "Linha sem comprimento. Apague ou redesenhe.");
                continue;
            }

            var dir = HatchMath.FindDirection(d.X, d.Y, w, h, doc.MaxComplexity);
            if (dir is null)
            {
                Fail(s, "Não foi possível calcular a direção desta linha.");
                continue;
            }

            var family = HatchMath.BuildFamily(s.P1, s.P2, w, h, dir.Value);
            var status = LineStatus.Ok;
            var messages = new List<string>();

            if (family.ErrorDegrees > 1e-6)
            {
                status = LineStatus.Warning;
                messages.Add($"Ângulo arredondado em {PatFormat.Decimals(family.ErrorDegrees, 4)}° para a linha se repetir. " +
                             "Ligue a correção automática ou o encaixe na grade para evitar.");
            }

            var size = Math.Max(Math.Abs(family.I), Math.Abs(family.J));
            if (size >= DenseThreshold)
            {
                status = LineStatus.Warning;
                messages.Add($"Linha densa: só volta ao mesmo ponto após {Math.Abs(family.I)}×{Math.Abs(family.J)} quadros " +
                             $"(offset {PatFormat.Short(family.Line.Offset)} {doc.UnitLabel}). O Revit pode recusar.");
            }

            var text = PatFormat.FormatLine(family.Line, doc.Target);
            if (!revit && text.Length > 80)
            {
                status = LineStatus.Warning;
                messages.Add("A linha passa de 80 caracteres, limite do AutoCAD.");
            }

            var message = messages.Count > 0 ? string.Join(" ", messages) : null;
            lines.Add(new TextLine(text, status, message, s.Id));
            families.Add(new SegmentFamily(s.Id, family.Line, status, message));
            statuses[s.Id] = (status, message);
        }

        if (!revit) lines.Add(new TextLine(""));

        return new GenerationResult
        {
            Text = string.Join("\r\n", lines.Select(l => l.Text)) + "\r\n",
            Lines = lines,
            Families = families,
            StatusBySegment = statuses,
        };

        void Fail(Segment s, string message)
        {
            lines.Add(new TextLine(";; linha ignorada", LineStatus.Error, message, s.Id));
            statuses[s.Id] = (LineStatus.Error, message);
        }
    }
}
