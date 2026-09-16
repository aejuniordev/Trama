namespace Trama.Models;

/// <summary>Uma linha de dados do .PAT: angle, x, y, shift, offset [, dash, space, ...].</summary>
public sealed record PatLine(double Angle, double X, double Y, double Shift, double Offset, IReadOnlyList<double> Dashes);

public enum LineStatus { Ok, Warning, Error }

/// <summary>Linha de texto do arquivo, com status opcional para o painel.</summary>
public sealed record TextLine(string Text, LineStatus? Status = null, string? Message = null, Guid? SegmentId = null);
