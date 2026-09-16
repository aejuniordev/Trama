using Trama.Models;

namespace Trama.Services;

/// <summary>Modelo pronto, com linhas em unidades de grade (0..Grid) para se adaptar a qualquer quadro.</summary>
public sealed record PatternTemplate(string Name, string Description, int Grid, double[][] Lines)
{
    public List<Segment> Create(double w, double h) => Lines
        .Select(l => new Segment
        {
            X1 = l[0] * w / Grid,
            Y1 = l[1] * h / Grid,
            X2 = l[2] * w / Grid,
            Y2 = l[3] * h / Grid,
        })
        .ToList();
}

public static class PatternTemplates
{
    public static readonly IReadOnlyList<PatternTemplate> All = new[]
    {
        new PatternTemplate("Diagonal", "Linhas a 45°, uma por quadro.", 12, new[]
        {
            new double[] { 0, 0, 12, 12 },
        }),
        new PatternTemplate("Diagonal cruzada", "Duas famílias a 45° e 135°.", 12, new[]
        {
            new double[] { 0, 0, 12, 12 },
            new double[] { 0, 12, 12, 0 },
        }),
        new PatternTemplate("Grade", "Quadriculado do tamanho do quadro.", 12, new[]
        {
            new double[] { 0, 0, 12, 0 },
            new double[] { 0, 0, 0, 12 },
        }),
        new PatternTemplate("Tijolo amarrado", "Fiadas com juntas desencontradas pela metade.", 12, new[]
        {
            new double[] { 0, 0, 12, 0 },
            new double[] { 0, 6, 12, 6 },
            new double[] { 0, 0, 0, 6 },
            new double[] { 6, 6, 6, 12 },
        }),
        new PatternTemplate("Cesteiro", "Blocos de três réguas alternando direção.", 12, new[]
        {
            new double[] { 0, 0, 12, 0 },
            new double[] { 0, 6, 12, 6 },
            new double[] { 0, 2, 6, 2 },
            new double[] { 0, 4, 6, 4 },
            new double[] { 6, 8, 12, 8 },
            new double[] { 6, 10, 12, 10 },
            new double[] { 0, 0, 0, 12 },
            new double[] { 6, 0, 6, 12 },
            new double[] { 8, 0, 8, 6 },
            new double[] { 10, 0, 10, 6 },
            new double[] { 2, 6, 2, 12 },
            new double[] { 4, 6, 4, 12 },
        }),
        new PatternTemplate("Tracejado alternado", "Traços horizontais desencontrados.", 12, new[]
        {
            new double[] { 0, 0, 6, 0 },
            new double[] { 6, 6, 12, 6 },
        }),
    };
}
