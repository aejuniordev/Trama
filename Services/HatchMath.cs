using Trama.Models;

namespace Trama.Services;

/// <summary>
/// Direção de uma linha expressa em quadros inteiros: a linha anda I quadros em X
/// enquanto anda J quadros em Y até cair exatamente no mesmo ponto de um quadro vizinho.
/// </summary>
public readonly record struct LatticeDirection(int I, int J, double ErrorDegrees);

public sealed record HatchFamily(PatLine Line, int I, int J, double Period, double ErrorDegrees, bool Continuous);

/// <summary>
/// Converte uma linha desenhada dentro de um quadro W×H numa família de linhas do .PAT.
///
/// Os quadros repetidos formam uma rede gerada por (W,0) e (0,H). Uma linha só se repete
/// sem emendas se sua direção for u = (I·W, J·H), com I e J inteiros primos entre si.
/// Então:
///   ângulo  = atan2(J·H, I·W)
///   período = |u|                      (distância até o próximo ponto igual na mesma reta)
///   offset  = W·H / |u|                (distância perpendicular entre retas vizinhas)
///   shift   = projeção de v sobre u    (v = (p·W, q·H) com I·q − J·p = 1, via Euclides estendido)
/// Como {u, v} é uma base da rede, a família cobre exatamente todas as cópias da linha.
/// </summary>
public static class HatchMath
{
    public const double Eps = 1e-9;

    public static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }

    /// <summary>Retorna (g, x, y) com a·x + b·y = g, onde |g| = mdc(a, b).</summary>
    public static (long G, long X, long Y) ExtendedGcd(long a, long b)
    {
        long oldR = a, r = b, oldS = 1, s = 0, oldT = 0, t = 1;
        while (r != 0)
        {
            var q = oldR / r;
            (oldR, r) = (r, oldR - q * r);
            (oldS, s) = (s, oldS - q * s);
            (oldT, t) = (t, oldT - q * t);
        }
        return (oldR, oldS, oldT);
    }

    /// <summary>Encontra a direção em quadros inteiros mais próxima de (dx, dy).</summary>
    /// <param name="toleranceDegrees">
    /// Se maior que zero, prefere a direção mais simples (menos quadros) cujo erro fique dentro da tolerância.
    /// </param>
    public static LatticeDirection? FindDirection(double dx, double dy, double w, double h, int maxComplexity, double toleranceDegrees = 0)
    {
        if (!(w > 0) || !(h > 0)) return null;
        if (Math.Sqrt(dx * dx + dy * dy) < Eps) return null;

        var max = Math.Clamp(maxComplexity, 1, 500);
        var target = Math.Atan2(dy, dx);

        // Direção medida em quadros, normalizada para evitar problemas de escala.
        double a = dx / w, b = dy / h;
        var scale = Math.Max(Math.Abs(a), Math.Abs(b));
        a /= scale;
        b /= scale;

        int bestI = 0, bestJ = 0, bestSize = int.MaxValue;
        var bestErr = double.MaxValue;
        var tol = Math.Max(0, toleranceDegrees) * Math.PI / 180;

        void Consider(double iD, double jD)
        {
            if (Math.Abs(iD) > max || Math.Abs(jD) > max) return;
            int i = (int)iD, j = (int)jD;
            if (i == 0 && j == 0) return;
            var g = Gcd(i, j);
            i /= g;
            j /= g;
            var err = Math.Abs(AngleDiff(Math.Atan2(j * h, i * w), target));
            var size = Math.Max(Math.Abs(i), Math.Abs(j));
            bool better;
            if (tol > 0 && (err <= tol || bestErr <= tol))
            {
                // Dentro da tolerância, ganha a mais simples; empate, a mais precisa.
                better = err <= tol && (bestErr > tol || size < bestSize || (size == bestSize && err < bestErr - 1e-12));
            }
            else
            {
                better = err < bestErr - 1e-12 || (Math.Abs(err - bestErr) <= 1e-12 && size < bestSize);
            }

            if (better)
            {
                bestErr = err;
                bestI = i;
                bestJ = j;
                bestSize = size;
            }
        }

        if (Math.Abs(a) > 1e-12)
        {
            for (var k = 1; k <= max; k++)
            {
                var i = k * Math.Sign(a);
                Consider(i, Math.Round(i * b / a));
            }
        }

        if (Math.Abs(b) > 1e-12)
        {
            for (var k = 1; k <= max; k++)
            {
                var j = k * Math.Sign(b);
                Consider(Math.Round(j * a / b), j);
            }
        }

        if (bestErr == double.MaxValue) return null;
        return new LatticeDirection(bestI, bestJ, bestErr * 180 / Math.PI);
    }

    /// <summary>Monta a linha .PAT para o segmento p1→p2 usando a direção informada.</summary>
    public static HatchFamily BuildFamily(Vec p1, Vec p2, double w, double h, LatticeDirection dir)
    {
        double ux = dir.I * w, uy = dir.J * h;
        var period = Math.Sqrt(ux * ux + uy * uy);

        var angle = Math.Atan2(uy, ux) * 180 / Math.PI;
        if (angle < 0) angle += 360;
        if (angle >= 360 - 1e-10) angle = 0;

        // Resolve I·q − J·p = 1.
        var (g, x, y) = ExtendedGcd(dir.I, -dir.J);
        if (g < 0)
        {
            x = -x;
            y = -y;
        }
        long q = x, p = y;
        double vx = p * w, vy = q * h;

        var offset = w * h / period;
        var shift = (vx * ux + vy * uy) / period;
        shift -= Math.Floor(shift / period) * period;
        if (period - shift < 1e-9) shift = 0;

        var length = Vec.Distance(p1, p2);
        var continuous = length >= period - 1e-7;
        IReadOnlyList<double> dashes = continuous
            ? Array.Empty<double>()
            : new[] { length, -(period - length) };

        var line = new PatLine(angle, p1.X, p1.Y, shift, offset, dashes);
        return new HatchFamily(line, dir.I, dir.J, period, dir.ErrorDegrees, continuous);
    }

    /// <summary>
    /// Ajusta o ponto <paramref name="moving"/> para que a linha anchor→moving tenha
    /// uma direção que se repita exatamente, mantendo-o dentro do quadro.
    /// </summary>
    public static Vec FixDirection(Vec anchor, Vec moving, double w, double h, int maxComplexity, double toleranceDegrees = 1.0)
    {
        var d = moving - anchor;
        var dir = FindDirection(d.X, d.Y, w, h, maxComplexity, toleranceDegrees);
        if (dir is null || dir.Value.ErrorDegrees < 1e-9) return moving;

        var u = new Vec(dir.Value.I * w, dir.Value.J * h).Normalized();
        var t = d.Dot(u);
        if (t <= Eps) return moving;

        t = Math.Min(t, RayExit(anchor, u, w, h));
        return anchor + u * t;
    }

    /// <summary>Distância que um raio pode percorrer a partir de <paramref name="origin"/> sem sair do quadro.</summary>
    public static double RayExit(Vec origin, Vec dir, double w, double h)
    {
        var t = double.MaxValue;
        if (dir.X > 1e-12) t = Math.Min(t, (w - origin.X) / dir.X);
        else if (dir.X < -1e-12) t = Math.Min(t, -origin.X / dir.X);
        if (dir.Y > 1e-12) t = Math.Min(t, (h - origin.Y) / dir.Y);
        else if (dir.Y < -1e-12) t = Math.Min(t, -origin.Y / dir.Y);
        return Math.Max(0, t);
    }

    public static double AngleDiff(double a, double b)
    {
        var d = a - b;
        while (d > Math.PI) d -= 2 * Math.PI;
        while (d < -Math.PI) d += 2 * Math.PI;
        return d;
    }
}
