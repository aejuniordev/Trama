using System.Globalization;
using System.Text;
using Trama.Models;

namespace Trama.Services;

/// <summary>
/// Formatação do .PAT. Sempre usa ponto decimal (cultura invariante):
/// num servidor em pt-BR, "8,485" seria lido como dois valores pelo Revit/AutoCAD.
/// </summary>
public static class PatFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Decimals(double v, int decimals)
    {
        if (Math.Abs(v) < Math.Pow(10, -decimals) / 2) v = 0;
        var s = Math.Round(v, decimals).ToString("0." + new string('#', decimals), Inv);
        return s == "-0" ? "0" : s;
    }

    public static string Significant(double v, int digits)
    {
        if (v == 0 || Math.Abs(v) < 1e-12) return "0";
        var intDigits = (int)Math.Floor(Math.Log10(Math.Abs(v))) + 1;
        return Decimals(v, Math.Clamp(digits - intDigits, 0, digits));
    }

    /// <summary>Número curto para exibição na interface.</summary>
    public static string Short(double v) => Decimals(v, 4);

    public static string FormatLine(PatLine line, TargetApp target)
    {
        var values = new List<double> { line.Angle, line.X, line.Y, line.Shift, line.Offset };
        values.AddRange(line.Dashes);

        if (target == TargetApp.Revit)
            return string.Join(", ", values.Select(v => Decimals(v, 9)));

        // AutoCAD limita cada linha do arquivo a 80 caracteres: valores mais compactos.
        return string.Join(",", values.Select(v => Significant(v, 8)));
    }

    public static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Nome aceito no cabeçalho *nome. AutoCAD não aceita espaços.</summary>
    public static string SanitizeName(string name, TargetApp target)
    {
        var clean = new StringBuilder();
        foreach (var c in RemoveDiacritics(name ?? ""))
        {
            if (c < 128 && (char.IsLetterOrDigit(c) || c is '-' or '_')) clean.Append(c);
            else if (char.IsWhiteSpace(c)) clean.Append(target == TargetApp.AutoCad ? '_' : ' ');
        }
        var result = string.Join(' ', clean.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (target == TargetApp.AutoCad) result = result.Trim('_');
        return result.Length == 0 ? (target == TargetApp.AutoCad ? "sem_titulo" : PatternDocument.DefaultName) : result;
    }

    public static string SanitizeDescription(string description)
    {
        var sb = new StringBuilder();
        foreach (var c in RemoveDiacritics(description ?? ""))
        {
            if (c is '\r' or '\n' or '\t') sb.Append(' ');
            else if (c >= 32 && c < 127) sb.Append(c);
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string FileName(string name)
    {
        var s = SanitizeName(name, TargetApp.AutoCad);
        return s + ".pat";
    }
}
