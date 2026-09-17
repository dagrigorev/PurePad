using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace PurePad.View.Controls;

/// <summary>
/// Parses an SVG path <c>d</c> string into a <see cref="GraphicsPath"/>. Supports the full command
/// set (M L H V C S Q T A Z, absolute and relative); elliptical arcs are converted to cubic Béziers.
/// Enough to render the single-path Material Design Icons the editor bundles.
/// </summary>
internal static class SvgPath
{
    public static GraphicsPath Parse(string d)
    {
        var path = new GraphicsPath(FillMode.Winding);
        var t = new Tokenizer(d);
        PointF current = default, start = default, prevCtrl = default;
        char prevCmd = ' ';

        char cmd;
        while ((cmd = t.NextCommand()) != '\0')
        {
            bool rel = char.IsLower(cmd);
            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                    current = Pt(t, rel, current);
                    start = current;
                    path.StartFigure();
                    while (t.HasNumber) // extra pairs are implicit line-to
                    {
                        PointF next = Pt(t, rel, current);
                        path.AddLine(current, next);
                        current = next;
                    }

                    break;

                case 'L':
                    do
                    {
                        PointF next = Pt(t, rel, current);
                        path.AddLine(current, next);
                        current = next;
                    }
                    while (t.HasNumber);
                    break;

                case 'H':
                    do
                    {
                        float x = t.Number() + (rel ? current.X : 0);
                        var next = new PointF(x, current.Y);
                        path.AddLine(current, next);
                        current = next;
                    }
                    while (t.HasNumber);
                    break;

                case 'V':
                    do
                    {
                        float y = t.Number() + (rel ? current.Y : 0);
                        var next = new PointF(current.X, y);
                        path.AddLine(current, next);
                        current = next;
                    }
                    while (t.HasNumber);
                    break;

                case 'C':
                    do
                    {
                        PointF c1 = Pt(t, rel, current), c2 = Pt(t, rel, current), end = Pt(t, rel, current);
                        path.AddBezier(current, c1, c2, end);
                        prevCtrl = c2;
                        current = end;
                    }
                    while (t.HasNumber);
                    break;

                case 'S':
                    do
                    {
                        PointF c1 = char.ToUpperInvariant(prevCmd) is 'C' or 'S' ? Reflect(prevCtrl, current) : current;
                        PointF c2 = Pt(t, rel, current), end = Pt(t, rel, current);
                        path.AddBezier(current, c1, c2, end);
                        prevCtrl = c2;
                        current = end;
                        prevCmd = cmd;
                    }
                    while (t.HasNumber);
                    break;

                case 'Q':
                    do
                    {
                        PointF cc = Pt(t, rel, current), end = Pt(t, rel, current);
                        AddQuad(path, current, cc, end);
                        prevCtrl = cc;
                        current = end;
                    }
                    while (t.HasNumber);
                    break;

                case 'T':
                    do
                    {
                        PointF cc = char.ToUpperInvariant(prevCmd) is 'Q' or 'T' ? Reflect(prevCtrl, current) : current;
                        PointF end = Pt(t, rel, current);
                        AddQuad(path, current, cc, end);
                        prevCtrl = cc;
                        current = end;
                        prevCmd = cmd;
                    }
                    while (t.HasNumber);
                    break;

                case 'A':
                    do
                    {
                        float rx = t.Number(), ry = t.Number(), rot = t.Number();
                        bool large = t.Flag(), sweep = t.Flag();
                        PointF end = Pt(t, rel, current);
                        AddArc(path, current, rx, ry, rot, large, sweep, end);
                        current = end;
                    }
                    while (t.HasNumber);
                    break;

                case 'Z':
                    path.CloseFigure();
                    current = start;
                    break;
            }

            prevCmd = cmd;
        }

        return path;
    }

    private static PointF Pt(Tokenizer t, bool rel, PointF cur)
    {
        float x = t.Number(), y = t.Number();
        return rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
    }

    private static PointF Reflect(PointF ctrl, PointF cur) => new(2 * cur.X - ctrl.X, 2 * cur.Y - ctrl.Y);

    private static void AddQuad(GraphicsPath path, PointF p0, PointF c, PointF p1)
    {
        // Elevate the quadratic to a cubic Bézier.
        var c1 = new PointF(p0.X + 2f / 3f * (c.X - p0.X), p0.Y + 2f / 3f * (c.Y - p0.Y));
        var c2 = new PointF(p1.X + 2f / 3f * (c.X - p1.X), p1.Y + 2f / 3f * (c.Y - p1.Y));
        path.AddBezier(p0, c1, c2, p1);
    }

    private static void AddArc(GraphicsPath path, PointF p0, float rx, float ry, float rotDeg, bool large, bool sweep, PointF p1)
    {
        if (rx == 0 || ry == 0 || (p0 == p1))
        {
            path.AddLine(p0, p1);
            return;
        }

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        double phi = rotDeg * Math.PI / 180.0;
        double cosP = Math.Cos(phi), sinP = Math.Sin(phi);

        // Endpoint → centre parameterization (per SVG spec).
        double dx = (p0.X - p1.X) / 2.0, dy = (p0.Y - p1.Y) / 2.0;
        double x1p = cosP * dx + sinP * dy, y1p = -sinP * dx + cosP * dy;

        double lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        if (lambda > 1)
        {
            double s = Math.Sqrt(lambda);
            rx *= (float)s;
            ry *= (float)s;
        }

        double sign = large == sweep ? -1 : 1;
        double num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
        double coef = sign * Math.Sqrt(Math.Max(0, num) / (rx * rx * y1p * y1p + ry * ry * x1p * x1p));
        double cxp = coef * rx * y1p / ry, cyp = -coef * ry * x1p / rx;
        double cx = cosP * cxp - sinP * cyp + (p0.X + p1.X) / 2.0;
        double cy = sinP * cxp + cosP * cyp + (p0.Y + p1.Y) / 2.0;

        double theta1 = Angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        double delta = Angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!sweep && delta > 0) delta -= 2 * Math.PI;
        else if (sweep && delta < 0) delta += 2 * Math.PI;

        int segments = (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2));
        double segAngle = delta / segments;
        double alpha = 4.0 / 3.0 * Math.Tan(segAngle / 4.0);

        PointF from = p0;
        double angle = theta1;
        for (int i = 0; i < segments; i++)
        {
            double a2 = angle + segAngle;
            PointF e = OnArc(cx, cy, rx, ry, cosP, sinP, a2);
            PointF c1 = Tangent(cx, cy, rx, ry, cosP, sinP, angle, alpha, forward: true, from);
            PointF c2 = Tangent(cx, cy, rx, ry, cosP, sinP, a2, alpha, forward: false, e);
            path.AddBezier(from, c1, c2, e);
            from = e;
            angle = a2;
        }
    }

    private static PointF OnArc(double cx, double cy, double rx, double ry, double cosP, double sinP, double t)
    {
        double x = rx * Math.Cos(t), y = ry * Math.Sin(t);
        return new PointF((float)(cosP * x - sinP * y + cx), (float)(sinP * x + cosP * y + cy));
    }

    private static PointF Tangent(double cx, double cy, double rx, double ry, double cosP, double sinP, double t, double alpha, bool forward, PointF at)
    {
        double dx = -rx * Math.Sin(t), dy = ry * Math.Cos(t);
        double tx = cosP * dx - sinP * dy, ty = sinP * dx + cosP * dy;
        double sign = forward ? 1 : -1;
        return new PointF((float)(at.X + sign * alpha * tx), (float)(at.Y + sign * alpha * ty));
    }

    private static double Angle(double ux, double uy, double vx, double vy)
    {
        double dot = ux * vx + uy * vy;
        double len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        double a = Math.Acos(Math.Clamp(dot / len, -1, 1));
        return (ux * vy - uy * vx) < 0 ? -a : a;
    }

    /// <summary>Reads numbers, flags and command letters out of a path string.</summary>
    private sealed class Tokenizer
    {
        private readonly string _s;
        private int _i;

        public Tokenizer(string s) => _s = s;

        public char NextCommand()
        {
            SkipSep();
            if (_i >= _s.Length)
            {
                return '\0';
            }

            char c = _s[_i];
            if (char.IsLetter(c))
            {
                _i++;
                return c;
            }

            return '\0';
        }

        public bool HasNumber
        {
            get
            {
                int save = _i;
                SkipSep();
                bool has = _i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] is '.' or '-' or '+');
                _i = save;
                return has;
            }
        }

        public float Number()
        {
            SkipSep();
            int startPos = _i;
            if (_i < _s.Length && (_s[_i] is '-' or '+')) _i++;
            bool dot = false;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (char.IsDigit(c)) { _i++; }
                else if (c == '.' && !dot) { dot = true; _i++; }
                else if (c is 'e' or 'E') { _i++; if (_i < _s.Length && (_s[_i] is '-' or '+')) _i++; }
                else break;
            }

            return float.Parse(_s.AsSpan(startPos, _i - startPos), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>Arc flags are a single '0' or '1' with no separator required.</summary>
        public bool Flag()
        {
            SkipSep();
            char c = _s[_i];
            _i++;
            return c == '1';
        }

        private void SkipSep()
        {
            while (_i < _s.Length && (_s[_i] is ' ' or ',' or '\t' or '\n' or '\r'))
            {
                _i++;
            }
        }
    }
}
