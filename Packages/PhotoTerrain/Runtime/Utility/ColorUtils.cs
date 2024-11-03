using UnityEngine;

namespace Hollow
{
internal static class ColorUtils
{
    public static Color FromHEX(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var col);
        return col;
    }

    public static string ToHEX(Color col)
    {
        int r = ((Mathf.RoundToInt(Mathf.Clamp01(col.r) * 255)) << 16) & 0xFF0000;
        int g = ((Mathf.RoundToInt(Mathf.Clamp01(col.g) * 255)) << 8)  & 0x00FF00;
        int b = ((Mathf.RoundToInt(Mathf.Clamp01(col.b) * 255)) << 0)  & 0x0000FF;

        int hexColor = r | g | b;
        return $"{hexColor:X6}";
    }

    public static string ToHEXA(Color col)
    {
        uint r = (((uint)Mathf.RoundToInt(Mathf.Clamp01(col.r) * 255)) << 24)  & 0xFF000000;
        uint g = (((uint)Mathf.RoundToInt(Mathf.Clamp01(col.g) * 255)) << 16)  & 0x00FF0000;
        uint b = (((uint)Mathf.RoundToInt(Mathf.Clamp01(col.b) * 255)) << 8)   & 0x0000FF00;
        uint a = (((uint)Mathf.RoundToInt(Mathf.Clamp01(col.a) * 255)) << 0)   & 0x000000FF;

        uint hexColor = r | g | b | a;
        return $"{hexColor:X8}";
    }

    /// <summary>
    /// Color format 0xRR_GG_BB
    /// </summary>
    public static Color FromHEX(int hex)
    {
        float r = ((hex & 0xFF0000) >> 16) / 255f;
        float g = ((hex & 0x00FF00) >> 8)  / 255f;
        float b = ((hex & 0x0000FF) >> 0)  / 255f;
        return new Color(r, g, b, 1f);
    }

    /// <summary>
    /// Color format 0xRR_GG_BB_AA
    /// </summary>
    public static Color FromHEX(uint hex)
    {
        float r = ((hex & 0xFF000000) >> 24) / 255f;
        float g = ((hex & 0x00FF0000) >> 16) / 255f;
        float b = ((hex & 0x0000FF00) >> 8)  / 255f;
        float a = ((hex & 0x000000FF) >> 0)  / 255f;
        return new Color(r, g, b, a);
    }

    public static Color AddBrightness(this Color col, float amount)
    {
        return new Color(Mathf.Max(0, col.r + amount), Mathf.Max(0, col.g + amount), Mathf.Max(0, col.b + amount), col.a);
    }

    public static Color WithAlpha(this in Color i, float alpha)
    {
        Color res = i;
        res.a = alpha;

        return res;
    }

    public static Color Grey(float v, float alpha = 1f)
    {
        return new(v, v, v, alpha);
    }

    public static string Paint(string s, Color col)
    {
        var color = ColorUtils.ToHEXA(col);
        return $"<color=#{color}>{s}</color>";
    }

    public static Color GammaToLinear(Color color)
    {
        static float gammaToLinear(float v) => Mathf.Pow(v, 1 / 2.2f);

        color.r = gammaToLinear(color.r);
        color.g = gammaToLinear(color.g);
        color.b = gammaToLinear(color.b);
        return color;
    }
}
}