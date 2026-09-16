using System.Globalization;
using Trama.Models;

namespace Trama.Services;

public sealed class ParseResult
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public PatUnits? Units { get; set; }
    public PatternKind? Kind { get; set; }
    public bool HasRevitHeaders { get; set; }
    public int PatternCount { get; set; }
    public List<PatLine> Lines { get; } = new();

    /// <summary>Marcações por número de linha do arquivo (base 1).</summary>
    public SortedDictionary<int, (LineStatus Status, string Message)> Marks { get; } = new();

    public List<string> Issues { get; } = new();
}

/// <summary>Lê .PAT no formato AutoCAD/Revit. Só o primeiro padrão do arquivo é usado.</summary>
public static class PatParser
{
    public static ParseResult Parse(string text)
    {
        var r = new ParseResult();
        var rows = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var inFirst = false;

        for (var n = 0; n < rows.Length; n++)
        {
            var ln = n + 1;
            var line = rows[n].Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith(';'))
            {
                var directive = line.Replace(" ", "").ToUpperInvariant();
                if (directive.StartsWith(";%UNITS="))
                {
                    r.HasRevitHeaders = true;
                    var u = directive[";%UNITS=".Length..];
                    if (u.StartsWith("MM")) r.Units = PatUnits.Millimeters;
                    else if (u.StartsWith("INCH")) r.Units = PatUnits.Inches;
                    else r.Marks[ln] = (LineStatus.Warning, "Unidade desconhecida. Use INCH ou MM.");
                }
                else if (directive.StartsWith(";%TYPE=") && r.PatternCount <= 1)
                {
                    r.HasRevitHeaders = true;
                    var t = directive[";%TYPE=".Length..];
                    if (t.StartsWith("MODEL")) r.Kind = PatternKind.Model;
                    else if (t.StartsWith("DRAFTING")) r.Kind = PatternKind.Drafting;
                    else r.Marks[ln] = (LineStatus.Warning, "Tipo desconhecido. Use MODEL ou DRAFTING.");
                }
                else if (directive.StartsWith(";%VERSION="))
                {
                    r.HasRevitHeaders = true;
                }
                continue;
            }

            if (line.StartsWith('*'))
            {
                r.PatternCount++;
                if (r.PatternCount == 1)
                {
                    inFirst = true;
                    var header = line[1..];
                    var comma = header.IndexOf(',');
                    r.Name = (comma >= 0 ? header[..comma] : header).Trim();
                    r.Description = comma >= 0 ? header[(comma + 1)..].Trim() : null;
                    if (string.IsNullOrWhiteSpace(r.Name))
                        r.Marks[ln] = (LineStatus.Error, "O cabeçalho precisa de um nome logo após o asterisco.");
                }
                else
                {
                    if (inFirst)
                        r.Marks[ln] = (LineStatus.Warning, "O arquivo tem mais de um padrão. Só o primeiro aparece na pré-visualização.");
                    inFirst = false;
                }
                continue;
            }

            if (!inFirst)
            {
                if (r.PatternCount == 0)
                    r.Marks[ln] = (LineStatus.Error, "Linha de dados antes do cabeçalho *nome.");
                continue;
            }

            ParseDataLine(line, ln, r);
        }

        if (r.PatternCount == 0) r.Issues.Add("Nenhum cabeçalho encontrado. Todo padrão começa com uma linha *nome.");
        else if (r.Lines.Count == 0) r.Issues.Add("O padrão não tem linhas de dados válidas.");

        return r;
    }

    private static void ParseDataLine(string line, int ln, ParseResult r)
    {
        var comment = line.IndexOf(';');
        if (comment >= 0) line = line[..comment];

        var parts = line.Split(',').Select(p => p.Trim()).ToList();
        while (parts.Count > 0 && parts[^1].Length == 0) parts.RemoveAt(parts.Count - 1);

        var values = new List<double>(parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v))
            {
                r.Marks[ln] = (LineStatus.Error, $"O valor {i + 1} (\"{parts[i]}\") não é um número. Use ponto como separador decimal.");
                return;
            }
            values.Add(v);
        }

        if (values.Count < 5)
        {
            r.Marks[ln] = (LineStatus.Error, "Faltam valores. O mínimo é: angle, x, y, shift, offset.");
            return;
        }

        if (Math.Abs(values[4]) < 1e-9)
        {
            r.Marks[ln] = (LineStatus.Error, "Offset zero faz as linhas se sobreporem infinitamente.");
            return;
        }

        var dashes = values.Skip(5).ToArray();
        var pat = new PatLine(values[0], values[1], values[2], values[3], values[4], dashes);
        r.Lines.Add(pat);

        if (dashes.Length > 0 && dashes.Sum(Math.Abs) < 1e-9)
        {
            r.Marks[ln] = (LineStatus.Warning, "Os traços somam zero; a linha será desenhada contínua.");
        }
        else if (dashes.Where((d, i) => i % 2 == 1 && d > 0).Any())
        {
            r.Marks[ln] = (LineStatus.Warning, "Os espaços (posições pares depois do offset) devem ser negativos.");
        }
        else
        {
            r.Marks[ln] = (LineStatus.Ok, "");
        }
    }
}
