using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// Fren Meter share codes: look and layout in one string.
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
            ["barsolid"] = c.MeterBarSolid,
            ["maxrows"] = c.MeterMaxRows,
            ["shadow"] = c.MeterTextShadow,
            ["hideooc"] = c.MeterHideOutOfCombat,
            ["bdicons"] = c.MeterBreakdownIcons,
            ["bdcolors"] = c.MeterBreakdownColors,
            ["always"] = c.MeterAlwaysShow,
            ["deathtotal"] = c.MeterFooterDeaths,
            ["lbrow"] = c.MeterLimitBreakRow,
            ["split"] = c.MeterSplitHealing,
            ["refresh"] = c.MeterRefreshSeconds,
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

            // Parse everything first, apply after: a bad code must change nothing at all.
            var apply = new System.Collections.Generic.List<Action<Configuration>>();

            if (o["pos"] is JArray { Count: 2 } p)
            { var v = new Vector2(Clamp01(F(p[0])), Clamp01(F(p[1]))); apply.Add(cc => cc.MeterPosition = v); }
            if (o["size"] is JArray { Count: 2 } s)
            { var v = new Vector2(Math.Clamp(F(s[0]), 230f, 2000f), Math.Clamp(F(s[1]), 84f, 1600f)); apply.Add(cc => cc.MeterSize = v); }
            if (o["mode"] is { } m) { var v = Math.Clamp((int)m, 0, 3); apply.Add(cc => cc.MeterMode = v); }
            if (o["cols"] is JArray cols) { var v = ParseColumns(cols); apply.Add(cc => { cc.MeterColumns.Clear(); cc.MeterColumns.AddRange(v); }); }
            if (o["healcols"] is JArray heal) { var v = ParseColumns(heal); apply.Add(cc => { cc.MeterHealColumns.Clear(); cc.MeterHealColumns.AddRange(v); }); }
            if (o["header"] is { } h) { var v = Math.Clamp((int)h, 0, 2); apply.Add(cc => cc.MeterHeaderStyle = v); }
            if (o["colhead"] is { } ch) { var v = (bool)ch; apply.Add(cc => cc.MeterColumnHeader = v); }
            if (o["rank"] is { } rk) { var v = (bool)rk; apply.Add(cc => cc.MeterShowRank = v); }
            if (o["icons"] is { } ic) { var v = (bool)ic; apply.Add(cc => cc.MeterShowJobIcons = v); }
            if (o["names"] is { } ns) { var v = Math.Clamp((int)ns, 0, 2); apply.Add(cc => cc.MeterNameStyle = v); }
            if (o["you"] is { } yo) { var v = (bool)yo; apply.Add(cc => cc.MeterYou = v); }
            if (o["raid"] is { } rd) { var v = (bool)rd; apply.Add(cc => cc.MeterShowRaidTotal = v); }
            if (o["font"] is { } f) { var v = f.ToString(); apply.Add(cc => cc.MeterFontFamily = v); }
            if (o["bold"] is { } b) { var v = (bool)b; apply.Add(cc => cc.MeterFontBold = v); }
            if (o["italic"] is { } it) { var v = (bool)it; apply.Add(cc => cc.MeterFontItalic = v); }
            if (o["px"] is { } px) { var v = Math.Clamp(F(px), 11f, 26f); apply.Add(cc => cc.MeterFontSizePx = v); }
            if (o["barh"] is { } bh) { var v = Math.Clamp(F(bh), 16f, 44f); apply.Add(cc => cc.MeterBarHeight = v); }
            if (o["gap"] is { } g) { var v = Math.Clamp(F(g), 0f, 10f); apply.Add(cc => cc.MeterBarGap = v); }
            if (o["round"] is { } ro) { var v = Math.Clamp(F(ro), 0f, 14f); apply.Add(cc => cc.MeterRounding = v); }
            if (o["bars"] is { } br) { var v = Math.Clamp((int)br, 0, 4); apply.Add(cc => cc.MeterBarStyle = v); }
            if (o["btns"] is { } bt) { var v = (bool)bt; apply.Add(cc => cc.MeterButtons = v); }
            if (o["healtab"] is { } ht) { var v = (bool)ht; apply.Add(cc => cc.MeterHealingTab = v); }
            if (o["tabdmg"] is { } td && td.ToString() is { Length: > 0 and <= 24 } tdn) apply.Add(cc => cc.MeterTabNameDamage = tdn);
            if (o["tabheal"] is { } th && th.ToString() is { Length: > 0 and <= 24 } thn) apply.Add(cc => cc.MeterTabNameHealing = thn);
            if (o["hlyou"] is { } hy) { var v = (bool)hy; apply.Add(cc => cc.MeterHighlightYou = v); }
            if (o["jobcol"] is { } jc) { var v = (bool)jc; apply.Add(cc => cc.MeterJobColors = v); }
            if (o["accent"] is { } ac) { var v = (uint)ac; apply.Add(cc => cc.MeterAccentColor = v); }
            if (o["text"] is { } tx) { var v = (uint)tx; apply.Add(cc => cc.MeterTextColor = v); }
            if (o["sub"] is { } su) { var v = (uint)su; apply.Add(cc => cc.MeterSubColor = v); }
            if (o["bg"] is { } bg) { var v = (uint)bg; apply.Add(cc => cc.MeterBgColor = v); }
            if (o["rows"] is { } rw) { var v = (uint)rw; apply.Add(cc => cc.MeterRowColor = v); }
            if (o["youcol"] is { } yc) { var v = (uint)yc; apply.Add(cc => cc.MeterYouColor = v); }
            if (o["timercol"] is { } tc) { var v = (uint)tc; apply.Add(cc => cc.MeterTimerColor = v); }
            if (o["hlcol"] is { } hc) { var v = (uint)hc; apply.Add(cc => cc.MeterHighlightColor = v); }
            if (o["titlecol"] is { } ttc) { var v = (uint)ttc; apply.Add(cc => cc.MeterTitleColor = v); }
            if (o["bordercol"] is { } bc) { var v = (uint)bc; apply.Add(cc => cc.MeterBorderColor = v); }
            if (o["hlstyle"] is { } hs) { var v = Math.Clamp((int)hs, 0, 3); apply.Add(cc => cc.MeterHighlightStyle = v); }
            if (o["hlstr"] is { } hst) { var v = Math.Clamp(F(hst), 0.2f, 2.5f); apply.Add(cc => cc.MeterHighlightStrength = v); }
            if (o["barop"] is { } bo) { var v = Math.Clamp(F(bo), 0.2f, 1.6f); apply.Add(cc => cc.MeterBarOpacity = v); }
            if (o["barsolid"] is { } bs) { var v = (bool)bs; apply.Add(cc => cc.MeterBarSolid = v); }
            if (o["maxrows"] is { } mr) { var v = Math.Clamp((int)mr, 0, 24); apply.Add(cc => cc.MeterMaxRows = v); }
            if (o["shadow"] is { } sh) { var v = (bool)sh; apply.Add(cc => cc.MeterTextShadow = v); }
            if (o["hideooc"] is { } ho) { var v = (bool)ho; apply.Add(cc => cc.MeterHideOutOfCombat = v); }
            if (o["bdicons"] is { } bi) { var v = (bool)bi; apply.Add(cc => cc.MeterBreakdownIcons = v); }
            if (o["bdcolors"] is { } bdc) { var v = (bool)bdc; apply.Add(cc => cc.MeterBreakdownColors = v); }
            if (o["always"] is { } aw) { var v = (bool)aw; apply.Add(cc => cc.MeterAlwaysShow = v); }
            if (o["deathtotal"] is { } dt) { var v = (bool)dt; apply.Add(cc => cc.MeterFooterDeaths = v); }
            if (o["lbrow"] is { } lb) { var v = (bool)lb; apply.Add(cc => cc.MeterLimitBreakRow = v); }
            if (o["split"] is { } sp) { var v = (bool)sp; apply.Add(cc => cc.MeterSplitHealing = v); }
            if (o["refresh"] is { } rf) { var v = Math.Clamp(F(rf), 0f, 3f); apply.Add(cc => cc.MeterRefreshSeconds = v); }

            // Nothing above touched the config; land it all at once.
            foreach (var a in apply) a(c);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static System.Collections.Generic.List<string> ParseColumns(JArray keys)
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var t in keys)
            if (t?.ToString() is { Length: > 0 } key && !list.Contains(key))
                list.Add(key);
        return list;
    }

    // A share code carries no NaN; one non-finite number rejects the whole code.
    private static float F(JToken t)
    {
        var v = (float)t;
        return float.IsFinite(v) ? v : throw new InvalidDataException("non-finite number");
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
