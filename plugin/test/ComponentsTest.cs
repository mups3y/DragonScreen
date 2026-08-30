/*
 * Tests for the Phase-6 pure display components (NumericReadout, StatusIndicator, TargetReticle).
 *
 * These pin what each widget EMITS into the DisplayList — command count, kinds, the honesty
 * placeholder, and the state→colour mappings — so a page built from them can't drift (rule S4/§62).
 */
using DragonScreen;
using System;

public static class ComponentsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static bool Same(Rgba a, Rgba b) { return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A; }

    public static int Run()
    {
        Console.WriteLine("DragonScreen display components (NumericReadout / StatusIndicator / TargetReticle) tests");

        var dl = new DisplayList(64);

        // ---- NumericReadout.Value ----
        dl.Clear();
        NumericReadout.Value(dl, 10f, 20f, "RANGE", "202.6 m", DragonPalette.Go, Typography.Value);
        Check("Value emits caption + value (2 cmds)", dl.Count == 2, "count=" + dl.Count);
        Check("Value caption text", dl.At(0).Str == "RANGE" && dl.At(0).Kind == DrawKind.Text, "");
        Check("Value value text + colour", dl.At(1).Str == "202.6 m" && Same(dl.At(1).Colour, DragonPalette.Go), "");

        // honesty placeholder
        dl.Clear();
        NumericReadout.Value(dl, 0f, 0f, "RATE", null, DragonPalette.AccentDim, Typography.Value);
        Check("null value → em-dash placeholder", dl.At(1).Str == NumericReadout.Blank, "got=" + dl.At(1).Str);
        Check("Show(\"5\")=5, Show(null)=—",
              NumericReadout.Show("5") == "5" && NumericReadout.Show(null) == NumericReadout.Blank, "");

        // ---- NumericReadout.Paired (green correction / blue rate) ----
        dl.Clear();
        NumericReadout.Paired(dl, 0f, 0f, "PITCH", "-20.0°", "0.0 °/s");
        Check("Paired emits 3 cmds", dl.Count == 3, "count=" + dl.Count);
        Check("Paired correction is GREEN", dl.At(1).Str == "-20.0°" && Same(dl.At(1).Colour, DragonPalette.Go), "");
        Check("Paired rate is BLUE", dl.At(2).Str == "0.0 °/s" && Same(dl.At(2).Colour, DragonPalette.AccentDim), "");

        // ---- StatusIndicator.Badge ----
        dl.Clear();
        StatusIndicator.Badge(dl, 5f, 5f, 120f, 40f, "AUTO", DragonPalette.Go);
        Check("Badge emits 6 cmds (plate + box(4) + word)", dl.Count == 6, "count=" + dl.Count);
        Check("Badge plate is Panel", dl.At(0).Kind == DrawKind.Rect && Same(dl.At(0).Colour, DragonPalette.Panel), "");
        Check("Badge word text + colour", dl.At(5).Kind == DrawKind.Text && dl.At(5).Str == "AUTO"
              && Same(dl.At(5).Colour, DragonPalette.Go), "");

        // ---- StatusIndicator mode colours (rule C6) ----
        Check("AUTO green", Same(StatusIndicator.Colour(ControlMode.Auto), DragonPalette.Go), "");
        Check("MANUAL cyan", Same(StatusIndicator.Colour(ControlMode.Manual), DragonPalette.Accent), "");
        Check("RECOVERY amber", Same(StatusIndicator.Colour(ControlMode.Recovery), DragonPalette.Caution), "");
        Check("ABORT red", Same(StatusIndicator.Colour(ControlMode.Abort), DragonPalette.Alarm), "");
        Check("IDLE dim", Same(StatusIndicator.Colour(ControlMode.Idle), DragonPalette.Text7), "");

        // ---- StatusIndicator.Lamp ----
        dl.Clear();
        StatusIndicator.Lamp(dl, 0f, 0f, "GNC", "MANUAL", DragonPalette.Accent);
        Check("Lamp emits 2 cmds", dl.Count == 2, "count=" + dl.Count);
        Check("Lamp word colour", dl.At(1).Str == "MANUAL" && Same(dl.At(1).Colour, DragonPalette.Accent), "");

        // ---- TargetReticle ----
        dl.Clear();
        TargetReticle.Crosshair(dl, 100f, 100f, 20f, DragonPalette.Accent);
        Check("Crosshair emits ring + 2 lines (3 cmds)", dl.Count == 3, "count=" + dl.Count);
        Check("Crosshair ring is an ArcBand", dl.At(0).Kind == DrawKind.ArcBand, "");
        Check("Crosshair arms are Lines", dl.At(1).Kind == DrawKind.Line && dl.At(2).Kind == DrawKind.Line, "");

        dl.Clear();
        TargetReticle.Marker(dl, 50f, 50f, 8f, DragonPalette.Accent);
        Check("Marker emits a 4-line diamond", dl.Count == 4
              && dl.At(0).Kind == DrawKind.Line && dl.At(3).Kind == DrawKind.Line, "count=" + dl.Count);

        // ---- AttitudeHud (the LIVE navball + docking overlay) ----
        dl.Clear();
        var att = new AttitudeHudState {
            Valid = true, RollErr = "15.0°", RollRate = "0.0 °/s",
            PitchErr = "-20.0°", PitchRate = "0.0 °/s", YawErr = "-10.0°", YawRate = "0.0 °/s",
            OffX = "22.7 m", OffY = "0.1 m", OffZ = "0.0 m", Range = "202.6 m", Rate = "-0.25 m/s", Closing = true };
        AttitudeHud.Draw(dl, 640f, 360f, 120f, att);
        Check("AttitudeHud draws the LIVE navball as cmd 0",
              dl.At(0).Kind == DrawKind.Image && dl.At(0).Image == ImageId.NavBallLive, "");
        Check("AttitudeHud emits the overlay (many cmds)", dl.Count > 15, "count=" + dl.Count);
        bool rangeGreen = false;
        for (int i = 0; i < dl.Count; i++)
        { var c = dl.At(i); if (c.Kind == DrawKind.Text && c.Str == "202.6 m" && Same(c.Colour, DragonPalette.Go)) rangeGreen = true; }
        Check("AttitudeHud RANGE value is GREEN", rangeGreen, "");

        dl.Clear();
        AttitudeHud.Draw(dl, 640f, 360f, 120f, new AttitudeHudState { Valid = false });
        bool noTarget = false;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Str == "NO TARGET") noTarget = true;
        Check("AttitudeHud invalid → live navball + NO TARGET", dl.At(0).Image == ImageId.NavBallLive && noTarget, "");

        // ---- null-safety (must not throw) ----
        NumericReadout.Value(null, 0, 0, "x", "y", DragonPalette.Go, 20f);
        StatusIndicator.Badge(null, 0, 0, 1, 1, "x", DragonPalette.Go);
        TargetReticle.Crosshair(null, 0, 0, 10, DragonPalette.Go);
        AttitudeHud.Draw(null, 0, 0, 10, att);
        Check("null DisplayList is a no-op (no throw)", true, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
