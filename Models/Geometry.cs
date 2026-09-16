namespace Trama.Models;

/// <summary>Vetor/ponto 2D imutável.</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double Dot(Vec o) => X * o.X + Y * o.Y;
    public double Cross(Vec o) => X * o.Y - Y * o.X;

    public Vec Normalized()
    {
        var l = Length;
        return l < 1e-15 ? new Vec(0, 0) : new Vec(X / l, Y / l);
    }

    public static double Distance(Vec a, Vec b) => (a - b).Length;

    public static double DistanceToSegment(Vec p, Vec a, Vec b)
    {
        var ab = b - a;
        var len2 = ab.Dot(ab);
        if (len2 < 1e-18) return Distance(p, a);
        var t = Math.Clamp((p - a).Dot(ab) / len2, 0, 1);
        return Distance(p, a + ab * t);
    }
}
