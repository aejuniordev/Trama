using System.Globalization;
using System.Text;
using Trama.Models;

namespace Trama.Services;

public readonly record struct Viewport(double MinX, double MinY, double MaxX, double MaxY);

public sealed record RenderOutput(string PathData, int Primitives, bool Truncated);

/// <summary>
/// Desenha as linhas de um .PAT exatamente como um programa CAD faria:
/// cada família é uma reta repetida a cada (shift, offset) no sistema girado pelo ângulo,
/// com o padrão de traços reiniciando no ponto base de cada reta.
/// </summary>
public static class PatRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static RenderOutput Render(IEnumerable<PatLine> lines, Viewport vp, Func<Vec, Vec> toScreen, int maxPrimitives)
    {
        var sb = new StringBuilder();
        var count = 0;
        var truncated = false;
        Vec[] corners =
        {
            new(vp.MinX, vp.MinY), new(vp.MaxX, vp.MinY), new(vp.MaxX, vp.MaxY), new(vp.MinX, vp.MaxY),
        };

        foreach (var line in lines)
        {
            if (truncated) break;
            if (!Finite(line)) continue;
            if (Math.Abs(line.Offset) < 1e-9) continue;

            var rad = line.Angle * Math.PI / 180;
            var u = new Vec(Math.Cos(rad), Math.Sin(rad));
            var n = new Vec(-u.Y, u.X);
            var origin = new Vec(line.X, line.Y);
            var step = u * line.Shift + n * line.Offset;

            double nMin = double.MaxValue, nMax = double.MinValue;
            foreach (var c in corners)
            {
                var v = (c - origin).Dot(n) / line.Offset;
                nMin = Math.Min(nMin, v);
                nMax = Math.Max(nMax, v);
            }

            var k0 = Math.Ceiling(nMin);
            var k1 = Math.Floor(nMax);
            if (k1 - k0 + count > maxPrimitives)
            {
                truncated = true;
                break;
            }

            var period = line.Dashes.Sum(Math.Abs);
            var solid = line.Dashes.Count == 0 || period < 1e-9;

            for (var k = k0; k <= k1; k++)
            {
                var b = origin + step * k;
                double tMin = double.MaxValue, tMax = double.MinValue;
                foreach (var c in corners)
                {
                    var t = (c - b).Dot(u);
                    tMin = Math.Min(tMin, t);
                    tMax = Math.Max(tMax, t);
                }

                if (solid)
                {
                    Emit(b + u * tMin, b + u * tMax);
                    continue;
                }

                var m0 = (long)Math.Floor(tMin / period);
                var m1 = (long)Math.Ceiling(tMax / period);
                if ((m1 - m0) * line.Dashes.Count + count > maxPrimitives)
                {
                    truncated = true;
                    break;
                }

                for (var m = m0; m <= m1; m++)
                {
                    var pos = m * period;
                    foreach (var dash in line.Dashes)
                    {
                        var len = Math.Abs(dash);
                        if (dash > 0)
                        {
                            double a = Math.Max(pos, tMin), e = Math.Min(pos + len, tMax);
                            if (e > a) Emit(b + u * a, b + u * e);
                        }
                        else if (dash == 0 && pos >= tMin && pos <= tMax)
                        {
                            EmitDot(b + u * pos);
                        }
                        pos += len;
                    }
                }
            }
        }

        return new RenderOutput(sb.ToString(), count, truncated);

        void Emit(Vec a, Vec b)
        {
            var sa = toScreen(a);
            var sbp = toScreen(b);
            sb.Append('M').Append(N(sa.X)).Append(' ').Append(N(sa.Y))
              .Append('L').Append(N(sbp.X)).Append(' ').Append(N(sbp.Y));
            count++;
        }

        void EmitDot(Vec p)
        {
            var s = toScreen(p);
            sb.Append('M').Append(N(s.X)).Append(' ').Append(N(s.Y)).Append("h0.01");
            count++;
        }
    }

    private static bool Finite(PatLine l) =>
        double.IsFinite(l.Angle) && double.IsFinite(l.X) && double.IsFinite(l.Y) &&
        double.IsFinite(l.Shift) && double.IsFinite(l.Offset) && l.Dashes.All(double.IsFinite);

    private static string N(double v) => Math.Round(v, 2).ToString("0.##", Inv);
}
