using Trama.Models;

namespace Trama.Services;

public enum DrawTool { Select, Line, Erase }

public sealed class CanvasOptions
{
    /// <summary>Ajusta o ângulo ao soltar a linha para que ela se repita sem arredondamento.</summary>
    public bool AutoFix { get; set; } = true;
    public bool Ortho { get; set; }
    public bool GridSnap { get; set; } = true;
    public bool LineSnap { get; set; } = true;
}

/// <summary>
/// Estado de uma tela de edição. Uma instância por aba/circuito;
/// os componentes assinam <see cref="Changed"/> para se redesenhar.
/// </summary>
public sealed class EditorState
{
    private sealed record Snapshot(List<Segment> Segments, double W, double H, int Grid, PatUnits Units);

    private const int MaxHistory = 200;
    private readonly List<Snapshot> _undo = new();
    private readonly List<Snapshot> _redo = new();
    private IReadOnlyList<PatLine> _previewLines = Array.Empty<PatLine>();

    public EditorState() => Regenerate();

    public event Action? Changed;

    public PatternDocument Doc { get; private set; } = new();
    public int? ProjectId { get; private set; }
    public DateTime? SavedAt { get; private set; }
    public bool IsDirty { get; private set; }
    public Guid? SelectedId { get; private set; }
    public DrawTool Tool { get; private set; } = DrawTool.Line;
    public CanvasOptions Options { get; } = new();

    /// <summary>Incrementa sempre que o conteúdo do padrão muda.</summary>
    public int Version { get; private set; }

    /// <summary>Incrementa quando o texto manual é substituído por fora do editor de texto.</summary>
    public int ManualSeedVersion { get; private set; }

    public GenerationResult Generated { get; private set; } = null!;
    public ParseResult? Parsed { get; private set; }

    public Segment? Selected => SelectedId is Guid id ? Doc.Segments.FirstOrDefault(s => s.Id == id) : null;
    public bool CanUndo => _undo.Count > 0 && !Doc.IsManual;
    public bool CanRedo => _redo.Count > 0 && !Doc.IsManual;
    public string CurrentPatText => Doc.IsManual ? Doc.ManualText : Generated.Text;
    public IReadOnlyList<PatLine> PreviewLines => _previewLines;

    public PatLine? SelectedLine => Doc.IsManual || SelectedId is null
        ? null
        : Generated.Families.FirstOrDefault(f => f.SegmentId == SelectedId)?.Line;

    // ---------- ciclo de vida ----------

    public void Load(PatternDocument doc, int? projectId, DateTime? savedAt)
    {
        Doc = doc;
        ProjectId = projectId;
        SavedAt = savedAt;
        IsDirty = false;
        SelectedId = null;
        _undo.Clear();
        _redo.Clear();
        ManualSeedVersion++;
        Regenerate();
        Raise();
    }

    public void MarkSaved(int projectId, DateTime savedAt)
    {
        ProjectId = projectId;
        SavedAt = savedAt;
        IsDirty = false;
        Raise();
    }

    /// <summary>Desvincula do projeto do banco (ex.: ele foi excluído), mantendo o desenho.</summary>
    public void Detach()
    {
        ProjectId = null;
        SavedAt = null;
        IsDirty = true;
        Raise();
    }

    public void MarkDirty()
    {
        IsDirty = true;
        Raise();
    }

    /// <summary>Chamar depois de alterar <see cref="Doc"/> diretamente.</summary>
    public void Commit()
    {
        IsDirty = true;
        Regenerate();
        Raise();
    }

    public void NotifyView() => Raise();

    // ---------- histórico ----------

    public void Checkpoint()
    {
        _undo.Add(TakeSnapshot());
        if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Add(TakeSnapshot());
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Add(TakeSnapshot());
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    private Snapshot TakeSnapshot() =>
        new(Doc.Segments.Select(s => s.Clone()).ToList(), Doc.TileWidth, Doc.TileHeight, Doc.GridDivisions, Doc.Units);

    private void Restore(Snapshot s)
    {
        Doc.Segments = s.Segments.Select(x => x.Clone()).ToList();
        Doc.TileWidth = s.W;
        Doc.TileHeight = s.H;
        Doc.GridDivisions = s.Grid;
        Doc.Units = s.Units;
        if (Selected is null) SelectedId = null;
        Commit();
    }

    // ---------- desenho ----------

    public void SetTool(DrawTool tool)
    {
        Tool = tool;
        Raise();
    }

    public void Select(Guid? id)
    {
        if (SelectedId == id) return;
        SelectedId = id;
        Raise();
    }

    public Segment AddSegment(Vec a, Vec b)
    {
        Checkpoint();
        var s = new Segment { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y };
        Doc.Segments.Add(s);
        SelectedId = s.Id;
        Commit();
        return s;
    }

    public void DeleteSegment(Guid id)
    {
        var index = Doc.Segments.FindIndex(s => s.Id == id);
        if (index < 0) return;
        Checkpoint();
        Doc.Segments.RemoveAt(index);
        if (SelectedId == id) SelectedId = null;
        Commit();
    }

    public void DeleteSelected()
    {
        if (SelectedId is Guid id && !Doc.IsManual) DeleteSegment(id);
    }

    public void ClearSegments()
    {
        if (Doc.Segments.Count == 0) return;
        Checkpoint();
        Doc.Segments.Clear();
        SelectedId = null;
        Commit();
    }

    public void ApplyTemplate(PatternTemplate template)
    {
        Checkpoint();
        Doc.GridDivisions = template.Grid;
        Doc.Segments = template.Create(Doc.TileWidth, Doc.TileHeight);
        SelectedId = null;
        Commit();
    }

    // ---------- configurações ----------

    public void SetName(string? value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0) value = PatternDocument.DefaultName;
        if (value == Doc.Name) return;
        Doc.Name = value;
        Commit();
    }

    public void SetDescription(string? value)
    {
        value = (value ?? "").Trim();
        if (value == Doc.Description) return;
        Doc.Description = value;
        Commit();
    }

    public void SetTarget(TargetApp target)
    {
        if (Doc.Target == target) return;
        Doc.Target = target;
        Commit();
    }

    public void SetKind(PatternKind kind)
    {
        if (Doc.Kind == kind) return;
        Doc.Kind = kind;
        Commit();
    }

    /// <summary>Troca a unidade convertendo o quadro e as linhas, para o desenho manter o tamanho real.</summary>
    public void SetUnits(PatUnits units)
    {
        if (Doc.Units == units) return;
        Checkpoint();
        var f = units == PatUnits.Millimeters ? 25.4 : 1 / 25.4;
        Doc.Units = units;
        Doc.TileWidth = Math.Round(Doc.TileWidth * f, 6);
        Doc.TileHeight = Math.Round(Doc.TileHeight * f, 6);
        ScaleSegments(f, f);
        Commit();
    }

    /// <summary>Muda o tamanho do quadro escalando as linhas junto, para que continuem se repetindo.</summary>
    public void SetTileSize(double w, double h)
    {
        if (!(w > 0) || !(h > 0) || w > 1e6 || h > 1e6) return;
        var fx = w / Doc.TileWidth;
        var fy = h / Doc.TileHeight;
        if (Math.Abs(fx - 1) < 1e-12 && Math.Abs(fy - 1) < 1e-12) return;
        Checkpoint();
        ScaleSegments(fx, fy);
        Doc.TileWidth = w;
        Doc.TileHeight = h;
        Commit();
    }

    public void SetGridDivisions(int n)
    {
        n = Math.Clamp(n, 1, 200);
        if (Doc.GridDivisions == n) return;
        Doc.GridDivisions = n;
        Commit();
    }

    public void SetMaxComplexity(int n)
    {
        n = Math.Clamp(n, 1, 200);
        if (Doc.MaxComplexity == n) return;
        Doc.MaxComplexity = n;
        Commit();
    }

    private void ScaleSegments(double fx, double fy)
    {
        foreach (var s in Doc.Segments)
        {
            s.X1 = Math.Round(s.X1 * fx, 9);
            s.Y1 = Math.Round(s.Y1 * fy, 9);
            s.X2 = Math.Round(s.X2 * fx, 9);
            s.Y2 = Math.Round(s.Y2 * fy, 9);
        }
    }

    // ---------- modo texto ----------

    /// <summary>Converte o resultado atual em texto editável ("assar" o padrão).</summary>
    public void EnterManualMode()
    {
        if (Doc.IsManual) return;
        Doc.ManualText = Generated.Text;
        Doc.IsManual = true;
        SelectedId = null;
        ManualSeedVersion++;
        Commit();
    }

    public void ExitManualMode()
    {
        if (!Doc.IsManual) return;
        Doc.IsManual = false;
        Commit();
    }

    public void SetManualText(string text)
    {
        if (text == Doc.ManualText) return;
        Doc.ManualText = text;
        Commit();
    }

    // ---------- interno ----------

    private void Regenerate()
    {
        Version++;
        Generated = PatGenerator.Generate(Doc);
        Parsed = Doc.IsManual ? PatParser.Parse(Doc.ManualText) : null;
        _previewLines = Doc.IsManual
            ? Parsed!.Lines
            : Generated.Families.Select(f => f.Line).ToList();
    }

    private void Raise() => Changed?.Invoke();
}
