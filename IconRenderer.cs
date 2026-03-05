using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;

namespace PiHoleTray;

static class IconRenderer
{
    // ── SVG path data (identical to Python source) ──────────────────────────

    private static readonly Dictionary<string, IconDef> Icons = new()
    {
        ["enabled"] = new(
            Color.FromArgb(149, 193, 31),
            "M247.19,76.44c-4.14,60.71-23.64,114.33-72.8,153.58-10.23,8.16-22.07,14.42-33.6,20.78-8.11,4.48-17.01,4.1-25.46.17-32.82-15.28-58.21-38.86-76.37-70-19.77-33.91-29.23-70.85-29.86-110-.2-12.53,5.57-22.37,17.13-27.51,23.49-10.43,47.23-20.28,70.88-30.35,7.78-3.31,15.65-6.42,23.35-9.91,5.2-2.35,10.04-2.17,15.25.05,29.15,12.44,58.26,24.97,87.6,36.95,14.43,5.89,25.15,15.2,23.87,36.23Z",
            ["M114.1,144.26c-4.58-4.81-8.5-9.28-12.79-13.35-5.5-5.22-12.19-5.23-16.97-.43-4.99,5.02-5.12,11.34,0,16.92,7.01,7.66,14.12,15.25,21.37,22.68,6.18,6.33,14.73,5.62,20.04-1.51,4.52-6.07,8.91-12.24,13.35-18.38,11.97-16.56,23.98-33.09,35.87-49.7,4.29-5.99,4.34-10.45.58-14.92-5.6-6.68-13.51-6.63-18.81.47-10.11,13.55-20.01,27.27-29.99,40.91-4.03,5.5-8.05,11.01-12.66,17.31Z"]
        ),
        ["disabled"] = new(
            Color.FromArgb(227, 6, 19),
            "M8.59,67.42c-.88-9.44,7.36-20.47,23.33-27.05C61.57,28.16,91.07,15.57,120.58,3c5.23-2.23,10.04-2.21,15.27.02,29.01,12.37,58.1,24.55,87.15,36.84,19.67,8.32,25.58,17.53,24.32,38.86-2.29,38.84-12.46,75.33-33.67,108.29-18.29,28.42-42.7,49.99-73.57,64.02-7.74,3.52-15.82,3.57-23.57.07-30.03-13.59-53.99-34.37-72.08-61.84C21.7,154.78,11.29,116.45,8.59,67.42Z",
            ["M127.83,104.67c-2.3-3.12-3.48-5.14-5.06-6.78-3.92-4.05-7.77-8.24-12.11-11.8-7.41-6.08-17.19-2.55-19.29,6.72-1.04,4.58.8,8.26,4.04,11.37,5.3,5.08,10.62,10.14,16.62,15.85-5.99,5.75-11.48,10.27-15.96,15.63-2.49,2.98-4.34,7.28-4.61,11.12-.33,4.62,2.91,8.05,7.57,9.74,4.92,1.78,9.04.39,12.58-3.03,5.53-5.34,10.92-10.82,17.06-16.92,5.49,5.78,10.37,11.1,15.45,16.22,5.74,5.78,12.68,6.1,17.83,1.06,4.69-4.59,4.47-12.06-.71-17.54-4-4.22-8.01-8.47-12.38-12.3-3.47-3.04-2.98-5.13.17-7.98,4.45-4.01,8.53-8.42,12.77-12.66,3.13-3.14,4.17-6.92,3.12-11.16-2.28-9.19-12.68-12.16-20.03-5.4-5.55,5.11-10.47,10.91-17.05,17.86Z"]
        ),
        ["unknown"] = new(
            Color.FromArgb(249, 178, 51),
            "M247.15,75.83c-4.05,60.91-23.7,114.67-73.11,153.99-10.24,8.15-22.14,14.35-33.7,20.68-7.97,4.37-16.72,3.9-24.99.06-32.87-15.24-58.28-38.83-76.49-69.95-19.56-33.42-28.87-69.86-30-108.44-.43-14.92,6.72-25.15,20.35-30.93,22.71-9.63,45.44-19.2,68.16-28.81,7.96-3.36,15.73-7.27,23.92-9.91,4.03-1.3,9.37-1.62,13.16-.07,30.99,12.66,61.79,25.8,92.58,38.94,15.07,6.43,21.31,18.21,20.11,34.43Z",
            [
                "M143.65,70.61c.15-6.56-6.8-13.99-15.38-14.19-8.73-.2-16.2,7.3-15.93,16.27.17,5.63.77,11.25,1.16,16.88.8,11.43,1.36,22.89,2.49,34.29.76,7.75,5.57,11.87,12.46,11.7,6.44-.15,10.86-4.47,11.47-12.1,1.36-16.87,2.4-33.77,3.72-52.85Z",
                "M127.93,183.25c8.84,0,16.1-6.98,16.11-15.5.01-8.73-7.6-16.27-16.27-16.11-8.74.16-15.6,7.18-15.64,16-.04,8.86,6.79,15.6,15.81,15.6Z",
            ]
        ),
    };

    private record IconDef(Color ShieldColor, string ShieldPath, string[] SymbolPaths);

    // ── Icon cache ───────────────────────────────────────────────────────────

    private static readonly Dictionary<(string, int), Icon> _cache = new();

    public static Icon GetIcon(string state, int size = 64)
    {
        var key = (state, size);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        using var bmp = Render(state, size);
        var icon     = BitmapToIcon(bmp);
        _cache[key] = icon;
        return icon;
    }

    // ── Button icon rendering ─────────────────────────────────────────────────

    /// <summary>Renders one or more SVG path strings (all same colour) to a square bitmap.</summary>
    public static Bitmap RenderSvgBitmap(string[] paths, Color color, int size)
    {
        int big  = size * 4;
        float sc = big / 256f;

        using var bigBmp = new Bitmap(big, big, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bigBmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            foreach (var p in paths)
            {
                using var gp = BuildPath(p, sc);
                g.FillPath(brush, gp);
            }
        }

        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(bigBmp, 0, 0, size, size);
        }
        return result;
    }

    // ── Shared bitmap cache + convenience helpers ─────────────────────────────

    internal const string StarPath =
        "M249.72,100.16c-1.59,2.5-2.79,5.37-4.82,7.43-15.14,15.45-30.38,30.8-45.74,46.03-2.52,2.5-3,4.81-2.43,8.25," +
        "3.63,21.76,7.18,43.54,10.18,65.39.4,2.9-1.68,7.04-3.95,9.21-3.51,3.35-8.27,2.96-12.66.73-14.55-7.38-29.11-14.75-43.66-22.14," +
        "-4.57-2.32-9.18-4.6-13.64-7.12-3.02-1.71-5.64-1.56-8.72.03-18.51,9.54-37.09,18.95-55.69,28.31-2.18,1.1-4.59,2.14-6.97,2.34," +
        "-7.27.61-12.3-4.73-11.21-11.95,3.23-21.35,6.56-42.69,10.08-63.99.64-3.89.05-6.64-2.86-9.52-15.04-14.88-29.83-30.02-44.8-44.98," +
        "-3.46-3.45-5.48-7.29-4.04-12.12,1.13-3.81,3.69-6.22,7.94-6.9,22.08-3.57,44.11-7.45,66.23-10.78,4.15-.62,5.4-3.14,6.86-5.97," +
        "9.41-18.24,18.8-36.48,28.09-54.78,2.35-4.63,4.85-8.82,10.9-8.64,5.83.17,8.63,4.05,10.99,8.73,9.45,18.73,19.06,37.38,28.51,56.1," +
        "1.38,2.73,3.05,4.05,6.27,4.56,21.8,3.4,43.55,7.09,65.31,10.73,6.2,1.04,8.94,4.35,9.82,11.07Z";

    private static readonly Dictionary<string, Bitmap> _bmpCache = new();

    /// <summary>Returns a cached status-shield bitmap (same graphics as the tray icon).</summary>
    public static Bitmap GetStatusBitmap(string state, int size = 16)
    {
        var key = $"status_{state}_{size}";
        if (!_bmpCache.TryGetValue(key, out var bmp))
            _bmpCache[key] = bmp = Render(state, size);
        return bmp;
    }

    /// <summary>Returns a cached star bitmap in the requested colour.</summary>
    public static Bitmap GetStarBitmap(Color color, int size = 16)
    {
        var key = $"star_{color.ToArgb()}_{size}";
        if (!_bmpCache.TryGetValue(key, out var bmp))
            _bmpCache[key] = bmp = RenderSvgBitmap([StarPath], color, size);
        return bmp;
    }

    /// <summary>Returns a cached close-X bitmap drawn with round-capped lines.</summary>
    public static Bitmap GetCloseBitmap(Color color, int size = 14)
    {
        var key = $"close_{color.ToArgb()}_{size}";
        if (!_bmpCache.TryGetValue(key, out var bmp))
            _bmpCache[key] = bmp = RenderCloseX(color, size);
        return bmp;
    }

    private static Bitmap RenderCloseX(Color color, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        float m = size * 0.18f;
        float e = size - m;
        float w = Math.Max(1.5f, size * 0.12f);
        using var pen = new Pen(color, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, m, m, e, e);
        g.DrawLine(pen, e, m, m, e);
        return bmp;
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    public static Bitmap Render(string state, int size)
    {
        if (!Icons.TryGetValue(state, out var def))
            def = Icons["unknown"];

        // Supersample at 4× for smooth edges
        int big  = size * 4;
        float sc = big / 256f;

        using var bigBmp = new Bitmap(big, big, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bigBmp))
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Shield
            using var shield = BuildPath(def.ShieldPath, sc);
            g.FillPath(new SolidBrush(def.ShieldColor), shield);

            // Symbol(s)
            using var whiteBrush = new SolidBrush(Color.White);
            foreach (var sym in def.SymbolPaths)
            {
                using var symPath = BuildPath(sym, sc);
                g.FillPath(whiteBrush, symPath);
            }
        }

        // Downscale with high quality
        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(bigBmp, 0, 0, size, size);
        }
        return result;
    }

    // ── SVG path parser ──────────────────────────────────────────────────────

    private static readonly Regex _tokenRx =
        new(@"[MmCcSsLlHhVvZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?",
            RegexOptions.Compiled);

    private static GraphicsPath BuildPath(string d, float scale)
    {
        var gp     = new GraphicsPath();
        var tokens = _tokenRx.Matches(d);
        int i      = 0;
        float cx = 0, cy = 0, sx = 0, sy = 0;
        float cpx = float.NaN, cpy = float.NaN; // last control point

        float Num()
        {
            while (i < tokens.Count && char.IsLetter(tokens[i].Value[0])) i++;
            return i < tokens.Count ? float.Parse(tokens[i++].Value,
                System.Globalization.CultureInfo.InvariantCulture) : 0f;
        }

        bool IsNum() => i < tokens.Count && !char.IsLetter(tokens[i].Value[0]);

        void AddBez(float x0, float y0, float x1, float y1,
                    float x2, float y2, float x3, float y3)
        {
            gp.AddBezier(
                x0 * scale, y0 * scale,
                x1 * scale, y1 * scale,
                x2 * scale, y2 * scale,
                x3 * scale, y3 * scale);
        }

        while (i < tokens.Count)
        {
            if (!char.IsLetter(tokens[i].Value[0])) { i++; continue; }
            char cmd = tokens[i].Value[0]; i++;

            switch (cmd)
            {
                case 'M':
                    cx = Num(); cy = Num(); sx = cx; sy = cy;
                    gp.StartFigure();
                    gp.AddLine(cx * scale, cy * scale, cx * scale, cy * scale);
                    cpx = float.NaN;
                    while (IsNum()) { cx = Num(); cy = Num(); gp.AddLine(cx * scale, cy * scale, cx * scale, cy * scale); }
                    break;
                case 'm':
                    cx += Num(); cy += Num(); sx = cx; sy = cy;
                    gp.StartFigure();
                    gp.AddLine(cx * scale, cy * scale, cx * scale, cy * scale);
                    cpx = float.NaN;
                    while (IsNum()) { cx += Num(); cy += Num(); gp.AddLine(cx * scale, cy * scale, cx * scale, cy * scale); }
                    break;
                case 'C':
                    while (IsNum())
                    {
                        float x1 = Num(), y1 = Num(), x2 = Num(), y2 = Num(), x3 = Num(), y3 = Num();
                        AddBez(cx, cy, x1, y1, x2, y2, x3, y3);
                        cpx = x2; cpy = y2; cx = x3; cy = y3;
                    }
                    break;
                case 'c':
                    while (IsNum())
                    {
                        float d1 = Num(), d2 = Num(), d3 = Num(), d4 = Num(), d5 = Num(), d6 = Num();
                        float x1 = cx+d1, y1 = cy+d2, x2 = cx+d3, y2 = cy+d4, x3 = cx+d5, y3 = cy+d6;
                        AddBez(cx, cy, x1, y1, x2, y2, x3, y3);
                        cpx = x2; cpy = y2; cx = x3; cy = y3;
                    }
                    break;
                case 'S':
                    while (IsNum())
                    {
                        float x1 = float.IsNaN(cpx) ? cx : 2*cx - cpx;
                        float y1 = float.IsNaN(cpy) ? cy : 2*cy - cpy;
                        float x2 = Num(), y2 = Num(), x3 = Num(), y3 = Num();
                        AddBez(cx, cy, x1, y1, x2, y2, x3, y3);
                        cpx = x2; cpy = y2; cx = x3; cy = y3;
                    }
                    break;
                case 's':
                    while (IsNum())
                    {
                        float x1 = float.IsNaN(cpx) ? cx : 2*cx - cpx;
                        float y1 = float.IsNaN(cpy) ? cy : 2*cy - cpy;
                        float d2 = Num(), d3 = Num(), d4 = Num(), d5 = Num();
                        float x2 = cx+d2, y2 = cy+d3, x3 = cx+d4, y3 = cy+d5;
                        AddBez(cx, cy, x1, y1, x2, y2, x3, y3);
                        cpx = x2; cpy = y2; cx = x3; cy = y3;
                    }
                    break;
                case 'L':
                    while (IsNum()) { cx = Num(); cy = Num(); gp.AddLine(cx*scale, cy*scale, cx*scale, cy*scale); cpx = float.NaN; }
                    break;
                case 'l':
                    while (IsNum()) { cx += Num(); cy += Num(); gp.AddLine(cx*scale, cy*scale, cx*scale, cy*scale); cpx = float.NaN; }
                    break;
                case 'H':
                    while (IsNum()) { cx = Num(); gp.AddLine(cx*scale, cy*scale, cx*scale, cy*scale); cpx = float.NaN; }
                    break;
                case 'h':
                    while (IsNum()) { cx += Num(); gp.AddLine(cx*scale, cy*scale, cx*scale, cy*scale); cpx = float.NaN; }
                    break;
                case 'V':
                    while (IsNum()) { cy = Num(); gp.AddLine(cx*scale, cy*scale, cx*scale, cy*scale); cpx = float.NaN; }
                    break;
                case 'v':
                    while (IsNum()) { cy += Num(); gp.AddLine(cx*scale, cy*scale, cx*scale, cy*scale); cpx = float.NaN; }
                    break;
                case 'Z':
                case 'z':
                    gp.CloseFigure(); cx = sx; cy = sy; cpx = float.NaN;
                    break;
            }
        }

        gp.FillMode = FillMode.Winding;
        return gp;
    }

    // ── Bitmap → Icon conversion ─────────────────────────────────────────────

    private static Icon BitmapToIcon(Bitmap bmp)
    {
        using var ms   = new MemoryStream();
        // Write a proper .ico with one image
        WriteIco(ms, bmp);
        ms.Position = 0;
        return new Icon(ms);
    }

    private static void WriteIco(Stream s, Bitmap bmp)
    {
        int sz = bmp.Width;
        using var bmpMs = new MemoryStream();
        bmp.Save(bmpMs, ImageFormat.Png);
        byte[] pngBytes = bmpMs.ToArray();

        using var w = new BinaryWriter(s, System.Text.Encoding.UTF8, leaveOpen: true);
        // ICONDIR
        w.Write((short)0);     // reserved
        w.Write((short)1);     // type: 1 = icon
        w.Write((short)1);     // count
        // ICONDIRENTRY
        w.Write((byte)(sz >= 256 ? 0 : sz));
        w.Write((byte)(sz >= 256 ? 0 : sz));
        w.Write((byte)0);      // color count
        w.Write((byte)0);      // reserved
        w.Write((short)1);     // planes
        w.Write((short)32);    // bit count
        w.Write(pngBytes.Length);
        w.Write(6 + 16);       // offset = ICONDIR(6) + ICONDIRENTRY(16)
        w.Write(pngBytes);
    }
}
