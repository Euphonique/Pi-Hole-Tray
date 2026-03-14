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
        ["client_enabled"] = new(
            Color.FromArgb(149, 193, 31),
            "M223.4,40.3c-29.3-12-58.5-24.5-87.6-36.9-2.7-1.1-5.2-1.7-7.8-1.7v-.2c-2.5,0-5,.5-7.5,1.7-7.7,3.5-15.6,6.6-23.3,9.9-23.7,10.1-47.4,19.9-70.9,30.4-11.6,5.1-17.3,15-17.1,27.5.6,39.1,10.1,76.1,29.9,110,18.2,31.1,43.6,54.7,76.4,70,4.3,2,8.7,3.1,13,3.1s8.4-1,12.4-3.2c11.5-6.4,23.4-12.6,33.6-20.8,49.2-39.2,68.7-92.9,72.8-153.6,1.3-21-9.4-30.4-23.9-36.2ZM224.5,75.1v1c-2.2,29.4-7.9,54.3-17.6,76-10.7,24-26,43.7-46.7,60.2-7.8,6.2-17.3,11.4-27.3,16.9-1,.6-2.1,1.1-3.1,1.7-.6.3-1,.4-1.5.4-.9,0-2.1-.3-3.4-1-28.1-13.1-50.4-33.6-66.4-60.9-17.2-29.5-26.2-62.8-26.8-98.9,0-4.4,1.3-5.4,3.7-6.4,16.9-7.5,34.3-14.8,51.1-22,6.5-2.7,13-5.5,19.5-8.2,2.6-1.1,5.3-2.2,7.9-3.3,4.6-1.9,9.4-3.9,14.2-6,3.7,1.6,7.4,3.2,11.1,4.8,24.7,10.5,50.2,21.4,75.5,31.8,5.3,2.2,7.5,4,8.3,5.2,1.2,1.6,1.7,4.5,1.5,8.7Z",
            ["M156.8,86.1c-10.1,13.6-20,27.3-30,40.9-4,5.5-8.1,11-12.7,17.3h0c-4.6-4.8-8.5-9.3-12.8-13.4-5.5-5.2-12.2-5.2-17-.4s-5.1,11.3,0,16.9c7,7.7,14.1,15.2,21.4,22.7,6.2,6.3,14.7,5.6,20-1.5,4.5-6.1,8.9-12.2,13.4-18.4,12-16.6,24-33.1,35.9-49.7,4.3-6,4.3-10.4.6-14.9-5.6-6.7-13.5-6.6-18.8.5Z"],
            FillMode.Alternate,
            SymbolInShieldColor: true
        ),
        ["client_disabled"] = new(
            Color.FromArgb(227, 6, 19),
            "M223,39.9c-29-12.3-58.1-24.5-87.1-36.8-2.6-1.1-5.1-1.7-7.7-1.7-2.5,0-5,.6-7.6,1.7-29.5,12.6-59,25.2-88.7,37.4-16,6.6-24.2,17.6-23.3,27,2.7,49,13.1,87.4,35.8,121.8,18.1,27.5,42,48.2,72.1,61.8,3.8,1.7,7.8,2.6,11.7,2.6s8-.9,11.9-2.7c30.9-14,55.3-35.6,73.6-64,21.2-33,31.4-69.5,33.7-108.3,1.3-21.3-4.7-30.5-24.3-38.9ZM224.7,77.4c-2.2,37.5-12.1,69.3-30.1,97.4-16.1,25-37.6,43.7-63.9,55.6-.9.4-1.8.6-2.5.6s-1.5-.2-2.3-.6c-25.7-11.6-46.7-29.7-62.5-53.7-19.5-29.5-29.4-63.3-32.1-109.4,1-1.3,3.8-3.8,9.3-6.1,28.2-11.6,56.8-23.8,84.3-35.5l3.3-1.4c18.2,7.7,36.6,15.5,54.5,23,10.5,4.4,21,8.9,31.5,13.3,7.8,3.3,9.4,5.4,9.6,5.7.2.4,1.4,2.6.9,10.9Z",
            ["M149,116c4.4-4,8.5-8.4,12.8-12.7,3.1-3.1,4.2-6.9,3.1-11.2-2.3-9.2-12.7-12.2-20-5.4-5.5,5.1-10.5,10.9-17,17.9h0c-2.3-3.1-3.5-5.1-5.1-6.8-3.9-4.1-7.8-8.2-12.1-11.8-7.4-6.1-17.2-2.5-19.3,6.7-1,4.6.8,8.3,4,11.4,5.3,5.1,10.6,10.1,16.6,15.8-6,5.8-11.5,10.3-16,15.6-2.5,3-4.3,7.3-4.6,11.1-.3,4.6,2.9,8.1,7.6,9.7,4.9,1.8,9,.4,12.6-3,5.5-5.3,10.9-10.8,17.1-16.9,5.5,5.8,10.4,11.1,15.4,16.2,5.7,5.8,12.7,6.1,17.8,1.1,4.7-4.6,4.5-12.1-.7-17.5-4-4.2-8-8.5-12.4-12.3-3.5-3-3-5.1.2-8Z"],
            FillMode.Alternate,
            SymbolInShieldColor: true
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

    private record IconDef(Color ShieldColor, string ShieldPath, string[] SymbolPaths,
                           FillMode ShieldFill = FillMode.Winding,
                           bool SymbolInShieldColor = false);

    // ── Multi-color paths (shield_multi.svg — mixed-status icon) ─────────────
    // Paths are listed in SVG document order so layers render correctly.

    private static readonly Color MultiYellow = Color.FromArgb(0xF9, 0xB2, 0x33);
    private static readonly Color MultiGreen  = Color.FromArgb(0x95, 0xC1, 0x1F);
    private static readonly Color MultiRed    = Color.FromArgb(0xE3, 0x06, 0x13);

    private static readonly (string Path, Color Color)[] MultiPaths =
    [
        // cls-1 yellow (stripe group 1)
        ("M73.4,23L9.6,86.8c0,.3,0,.5,0,.8L74.8,22.4c-.5.2-1,.4-1.4.6Z", MultiYellow),
        ("M170.9,18.1s0,0-.1,0L28.2,160.6s0,0,0,.1L170.9,18.1Z", MultiYellow),
        ("M194.9,28.2c-.3-.1-.6-.3-1-.4L39.5,182.3c.2.3.3.6.5.8L194.9,28.2Z", MultiYellow),
        // cls-2 green (stripe group 1)
        ("M121.2,3c-8.2,2.6-16,6.5-23.9,9.9-7.5,3.2-15,6.3-22.5,9.5L9.7,87.6c.7,8.5,1.9,16.9,3.5,25.1L123.4,2.4c-.8.2-1.5.3-2.2.6Z", MultiGreen),
        ("M171.4,18.3L28.5,161.2c3.1,6.8,6.5,13.4,10.3,19.9.2.4.5.8.7,1.2L193.9,27.8c-7.5-3.2-15-6.4-22.6-9.5Z", MultiGreen),
        ("M247.1,69.8c-.5-8.3-3.3-15.3-8.8-20.6L68.5,219.1c5,4.8,10.3,9.3,15.9,13.5L247.1,69.8Z", MultiGreen),
        // cls-3 red
        ("M29.1,41.7c-13.6,5.8-20.8,16-20.3,30.9.1,4.8.4,9.5.8,14.2l63.8-63.8c-14.7,6.2-29.5,12.5-44.2,18.7Z", MultiRed),
        ("M149,9L19.7,138.3c2.4,7.6,5.3,15,8.6,22.3L170.8,18c-7.3-3-14.5-6.1-21.8-9.1Z", MultiRed),
        ("M227,41.9c-3.1-1.3-6.3-2.7-9.4-4L53.2,202.2c4.7,6,9.8,11.6,15.2,16.8L238.3,49.2c-3-2.9-6.8-5.4-11.3-7.3Z", MultiRed),
        ("M243.8,105.6L104,245.3c3.7,2,7.4,3.9,11.3,5.7,4,1.8,8,2.9,12.1,3l105.3-105.3c5.1-13.8,8.7-28.2,11.1-43.2Z", MultiRed),
        // cls-1 yellow (stripe group 2)
        ("M134.4,2.9c-3.1-1.3-7.3-1.3-11-.5L13.2,112.7c1.7,8.7,3.8,17.2,6.5,25.6L149,9c-4.9-2-9.7-4-14.6-6Z", MultiYellow),
        ("M53.1,202.1L217.4,37.8c-7.5-3.2-15-6.4-22.5-9.6L40,183.1c4,6.7,8.4,13,13.1,19Z", MultiYellow),
        ("M243.8,105.2c1.5-9.4,2.6-19.1,3.2-28.9h0c.2-2.3.2-4.4,0-6.5L84.4,232.6c6.1,4.6,12.6,8.8,19.5,12.6L243.8,105.2Z", MultiYellow),
        // cls-2 green (final cap)
        ("M140.3,251c11.6-6.3,23.5-12.5,33.7-20.7,28.6-22.8,47.2-50.4,58.6-81.4l-105.2,105.2c4.4.1,8.7-.8,12.9-3.1Z", MultiGreen),
        // cls-4 white (exclamation mark)
        ("M143.6,74c.1-6.6-6.8-14-15.4-14.2-8.7-.2-16.2,7.3-15.9,16.3.2,5.6.8,11.2,1.2,16.9.8,11.4,1.4,22.9,2.5,34.3.8,7.7,5.6,11.9,12.5,11.7,6.4-.1,10.9-4.5,11.5-12.1,1.4-16.9,2.4-33.8,3.7-52.8h0Z", Color.White),
        ("M127.8,186.7c8.8,0,16.1-7,16.1-15.5,0-8.7-7.6-16.3-16.3-16.1-8.7.2-15.6,7.2-15.6,16,0,8.9,6.8,15.6,15.8,15.6h0Z", Color.White),
    ];

    // ── Icon cache ───────────────────────────────────────────────────────────

    private static readonly Dictionary<(string, int), Icon> _cache = new();

    public static Icon GetIcon(string state, int size = 64)
    {
        var key = (state, size);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        using var bmp = state == "mixed" ? RenderMulti(size) : Render(state, size);
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
            _bmpCache[key] = bmp = state == "mixed" ? RenderMulti(size) : Render(state, size);
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

            // Shield – for outlined client icons, cut out the inner shape via Region
            using var shieldBrush = new SolidBrush(def.ShieldColor);
            if (def.ShieldFill == FillMode.Alternate
                && def.ShieldPath.Contains("ZM", StringComparison.Ordinal))
            {
                var splitIdx = def.ShieldPath.IndexOf("ZM", StringComparison.Ordinal);
                var outerD   = def.ShieldPath[..(splitIdx + 1)];      // up to and including Z
                var innerD   = "M" + def.ShieldPath[(splitIdx + 2)..]; // from M onward
                using var outerGp = BuildPath(outerD, sc);
                using var innerGp = BuildPath(innerD, sc);
                using var region  = new Region(outerGp);
                region.Exclude(innerGp);
                g.FillRegion(shieldBrush, region);
            }
            else
            {
                using var shield = BuildPath(def.ShieldPath, sc);
                shield.FillMode = def.ShieldFill;
                g.FillPath(shieldBrush, shield);
            }

            // Symbol(s) – use shield colour for client icons, white otherwise
            using var symBrush = new SolidBrush(def.SymbolInShieldColor ? def.ShieldColor : Color.White);
            foreach (var sym in def.SymbolPaths)
            {
                using var symPath = BuildPath(sym, sc);
                g.FillPath(symBrush, symPath);
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

    /// <summary>Renders the multi-color shield (shield_multi.svg) for mixed-status display.</summary>
    private static Bitmap RenderMulti(int size)
    {
        int big  = size * 4;
        float sc = big / 256f;

        using var bigBmp = new Bitmap(big, big, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bigBmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            foreach (var (path, color) in MultiPaths)
            {
                using var brush = new SolidBrush(color);
                using var gp    = BuildPath(path, sc);
                gp.FillMode     = FillMode.Winding;
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
