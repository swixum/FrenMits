using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// Fren Meter share codes: the meter's whole look and layout in one string.
public static class MeterProfile
{
    private const string Prefix = "FMMETER1.";

    public static string Export(Configuration c)
    {
        var o = new JObject
        {
            ["pos"] = new JArray(c.MeterPosition.X, c.MeterPosition.Y),
            ["size"] = new JArray(c.MeterSize.X, c.MeterSize.Y),
            ["mode"] = c.MeterMode,
            ["cols"] = new JArray(c.MeterColumns),
            ["healcols"] = new JArray(c.MeterHealColumns),
            ["header"] = c.MeterHeaderStyle,
            ["colhead"] = c.MeterColumnHeader,
            ["rank"] = c.MeterShowRank,
            ["icons"] = c.MeterShowJobIcons,
            ["names"] = c.MeterNameStyle,
            ["you"] = c.MeterYou,
            ["raid"] = c.MeterShowRaidTotal,
            ["font"] = c.MeterFontFamily,
            ["bold"] = c.MeterFontBold,
            ["italic"] = c.MeterFontItalic,
            ["px"] = c.MeterFontSizePx,
            ["barh"] = c.MeterBarHeight,
            ["gap"] = c.MeterBarGap,
            ["round"] = c.MeterRounding,
            ["bars"] = c.MeterBarStyle,
            ["btns"] = c.MeterButtons,
            ["healtab"] = c.MeterHealingTab,
            ["tabdmg"] = c.MeterTabNameDamage,
            ["tabheal"] = c.MeterTabNameHealing,
            ["hlyou"] = c.MeterHighlightYou,
            ["jobcol"] = c.MeterJobColors,
            ["accent"] = c.MeterAccentColor,
            ["text"] = c.MeterTextColor,
            ["sub"] = c.MeterSubColor,
            ["bg"] = c.MeterBgColor,
            ["rows"] = c.MeterRowColor,
            ["youcol"] = c.MeterYouColor,
            ["timercol"] = c.MeterTimerColor,
            ["hlcol"] = c.MeterHighlightColor,
            ["titlecol"] = c.MeterTitleColor,
            ["bordercol"] = c.MeterBorderColor,
            ["hlstyle"] = c.MeterHighlightStyle,
            ["hlstr"] = c.MeterHighlightStrength,
            ["barop"] = c.MeterBarOpacity,
            ["maxrows"] = c.MeterMaxRows,
            ["shadow"] = c.MeterTextShadow,
            ["hideooc"] = c.MeterHideOutOfCombat,
            ["bdicons"] = c.MeterBreakdownIcons,
            ["bdcolors"] = c.MeterBreakdownColors,
        };
        var raw = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(o));
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.SmallestSize))
            gz.Write(raw, 0, raw.Length);
        return Prefix + Convert.ToBase64String(ms.ToArray());
    }

    public static bool Import(Configuration c, string code)
    {
        try
        {
            code = code.Trim();
            if (!code.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            using var ms = new MemoryStream(Convert.FromBase64String(code[Prefix.Length..]));
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(gz, Encoding.UTF8);
            var o = JObject.Parse(reader.ReadToEnd());

            if (o["pos"] is JArray { Count: 2 } p)
                c.MeterPosition = new Vector2(Clamp01((float)p[0]), Clamp01((float)p[1]));
            if (o["size"] is JArray { Count: 2 } s)
                c.MeterSize = new Vector2(Math.Clamp((float)s[0], 230f, 2000f), Math.Clamp((float)s[1], 84f, 1600f));
            if (o["mode"] is { } m) c.MeterMode = Math.Clamp((int)m, 0, 3);
            if (o["cols"] is JArray cols) FillColumns(c.MeterColumns, cols);
            if (o["healcols"] is JArray heal) FillColumns(c.MeterHealColumns, heal);
            if (o["header"] is { } h) c.MeterHeaderStyle = Math.Clamp((int)h, 0, 2);
            if (o["colhead"] is { } ch) c.MeterColumnHeader = (bool)ch;
            if (o["rank"] is { } rk) c.MeterShowRank = (bool)rk;
            if (o["icons"] is { } ic) c.MeterShowJobIcons = (bool)ic;
            if (o["names"] is { } ns) c.MeterNameStyle = Math.Clamp((int)ns, 0, 2);
            if (o["you"] is { } yo) c.MeterYou = (bool)yo;
            if (o["raid"] is { } rd) c.MeterShowRaidTotal = (bool)rd;
            if (o["font"] is { } f) c.MeterFontFamily = f.ToString();
            if (o["bold"] is { } b) c.MeterFontBold = (bool)b;
            if (o["italic"] is { } it) c.MeterFontItalic = (bool)it;
            if (o["px"] is { } px) c.MeterFontSizePx = Math.Clamp((float)px, 11f, 26f);
            if (o["barh"] is { } bh) c.MeterBarHeight = Math.Clamp((float)bh, 16f, 44f);
            if (o["gap"] is { } g) c.MeterBarGap = Math.Clamp((float)g, 0f, 10f);
            if (o["round"] is { } ro) c.MeterRounding = Math.Clamp((float)ro, 0f, 14f);
            if (o["bars"] is { } br) c.MeterBarStyle = Math.Clamp((int)br, 0, 4);
            if (o["btns"] is { } bt) c.MeterButtons = (bool)bt;
            if (o["healtab"] is { } ht) c.MeterHealingTab = (bool)ht;
            if (o["tabdmg"] is { } td && td.ToString() is { Length: > 0 and <= 24 } tdn) c.MeterTabNameDamage = tdn;
            if (o["tabheal"] is { } th && th.ToString() is { Length: > 0 and <= 24 } thn) c.MeterTabNameHealing = thn;
            if (o["hlyou"] is { } hy) c.MeterHighlightYou = (bool)hy;
            if (o["jobcol"] is { } jc) c.MeterJobColors = (bool)jc;
            if (o["accent"] is { } ac) c.MeterAccentColor = (uint)ac;
            if (o["text"] is { } tx) c.MeterTextColor = (uint)tx;
            if (o["sub"] is { } su) c.MeterSubColor = (uint)su;
            if (o["bg"] is { } bg) c.MeterBgColor = (uint)bg;
            if (o["rows"] is { } rw) c.MeterRowColor = (uint)rw;
            if (o["youcol"] is { } yc) c.MeterYouColor = (uint)yc;
            if (o["timercol"] is { } tc) c.MeterTimerColor = (uint)tc;
            if (o["hlcol"] is { } hc) c.MeterHighlightColor = (uint)hc;
            if (o["titlecol"] is { } ttc) c.MeterTitleColor = (uint)ttc;
            if (o["bordercol"] is { } bc) c.MeterBorderColor = (uint)bc;
            if (o["hlstyle"] is { } hs) c.MeterHighlightStyle = Math.Clamp((int)hs, 0, 3);
            if (o["hlstr"] is { } hst) c.MeterHighlightStrength = Math.Clamp((float)hst, 0.2f, 2.5f);
            if (o["barop"] is { } bo) c.MeterBarOpacity = Math.Clamp((float)bo, 0.2f, 1.6f);
            if (o["maxrows"] is { } mr) c.MeterMaxRows = Math.Clamp((int)mr, 0, 24);
            if (o["shadow"] is { } sh) c.MeterTextShadow = (bool)sh;
            if (o["hideooc"] is { } ho) c.MeterHideOutOfCombat = (bool)ho;
            if (o["bdicons"] is { } bi) c.MeterBreakdownIcons = (bool)bi;
            if (o["bdcolors"] is { } bdc) c.MeterBreakdownColors = (bool)bdc;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void FillColumns(System.Collections.Generic.List<string> list, JArray keys)
    {
        list.Clear();
        foreach (var t in keys)
            if (t?.ToString() is { Length: > 0 } key && !list.Contains(key))
                list.Add(key);
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
