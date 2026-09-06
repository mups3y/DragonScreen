// Tests for [[S130]] / audit H7 — the Figma UI's alarm channel, which had no consumer at all.
//
// ⛔ WHAT WAS WRONG: `Alarms.Mask` and `Alarms.SystemSeverity` fold G-force, propellant, power and the
// whole FDIR spine every frame, and every Figma page threw the result away. The crew's home page could
// not show a caution. So the checks here are about ROUTING, not about the bands — `Alarms`' own suites
// already pin what counts as a caution; what was missing was anything carrying the answer to the glass.
//
// ⭐ THE CHECKS READ THE RENDER. `BottomBar.Draw` is built into a DisplayList and the CURRENT STATE
// command's colour is read back out, so what is asserted is the ink a crew would see — not a helper
// re-deriving the same expression the source uses.
using System;
using DragonScreen;

public static class CoverAlarmTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const int W = 2560, H = 1406;

    public static int Run()
    {
        Console.WriteLine("DragonScreen Cover alarm channel (S130 / H7)");
        checks = 0; failures = 0;

        // ---- a nominal feed: the bar reads white, exactly as it always has ----------------------
        PageState nom = Nominal();
        Check("S130 a nominal feed leaves CURRENT STATE white",
              SameRgba(StateColour(nom), DragonPalette.White),
              "got " + Describe(StateColour(nom)));
        Check("S130 ...and the fixture really is nominal, or the check above proves nothing",
              Alarms.SystemSeverity(nom) < Severity.Caution,
              "severity is " + Alarms.SystemSeverity(nom));

        // ---- ⭐ A CAUTION REACHES THE GLASS. This is the whole of H7. -----------------------------
        PageState caut = Nominal();
        caut.Power01 = 0.05;                       // a low bus is a real caution the mask already folds
        Check("S130 the fixture's low power really is a caution or worse",
              Alarms.SystemSeverity(caut) >= Severity.Caution,
              "severity is " + Alarms.SystemSeverity(caut));
        Check("S130 ...and CURRENT STATE stops being white when it is",
              !SameRgba(StateColour(caut), DragonPalette.White),
              "still " + Describe(StateColour(caut)));
        Check("S130 ...and takes the colour Alarms gives that severity — one mapping, not two",
              SameRgba(StateColour(caut), Alarms.Colour(Alarms.SystemSeverity(caut))),
              "got " + Describe(StateColour(caut))
              + ", Alarms says " + Describe(Alarms.Colour(Alarms.SystemSeverity(caut))));

        // ---- ⚠ A DEAD FEED IS NOT A QUIET ONE ---------------------------------------------------
        PageState dead = new PageState();          // Valid == false
        Check("S130 no feed reads as no feed, not as nominal and not as an alarm",
              SameRgba(StateColour(dead), DragonPalette.Text6),
              "got " + Describe(StateColour(dead)));
        Check("S130 ...and the text is a dash, so the colour is not carrying it alone",
              BottomBar.CurrentState(dead) == Dashes.None,
              "got '" + BottomBar.CurrentState(dead) + "'");

        // ---- the TEXT is not overloaded: it still says the phase, whatever the severity ----------
        Check("S130 a cautioned bar still READS the phase — only the ink changed",
              BottomBar.CurrentState(caut) == BottomBar.CurrentState(nom)
              && BottomBar.CurrentState(caut) != Dashes.None,
              "nominal '" + BottomBar.CurrentState(nom) + "' vs caution '" + BottomBar.CurrentState(caut) + "'");

        // ---- ⛔ BIT 2 (NAV) IS EMPTY ON PURPOSE — pinned so it is a decision, not an oversight ----
        // If someone sets it, this fails and they have to say what NAV condition it means. That is the
        // point: an alarm channel is worth nothing if a light in it can appear without a meaning.
        int mask = Alarms.Mask(caut);
        Check("S130 bit 2 (NAV) is not set — no NAV alarm condition is modelled",
              ((mask >> 2) & 1) == 0, "mask " + mask);
        Check("S130 ...while the bits that DO have conditions can be set",
              Alarms.Mask(caut) != 0, "mask is 0 on a cautioned feed");

        // and each of the three live bits is reachable, so "bit 2 is the only empty one" is measured
        PageState dock = Nominal(); dock.HasTarget = true; dock.ClosingFast = true;
        Check("S130 bit 3 (DOCKING) is reachable", ((Alarms.Mask(dock) >> 3) & 1) == 1,
              "mask " + Alarms.Mask(dock));
        Check("S130 bit 0 (FLIGHT) is reachable", ((Alarms.Mask(caut) >> 0) & 1) == 1,
              "mask " + Alarms.Mask(caut));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (the channel has a consumer now; NAV's bit is empty by decision)");
        return failures;
    }

    /// <summary>
    /// A valid feed with nothing wrong with it.
    ///
    /// ⚠ THE CABIN VALUES ARE NOT DECORATION. A first version of this fixture set only `Valid`, the
    /// phase and the three 0..1 gauges - and `SystemSeverity` came back **Alarm**, because a default
    /// `CabinReadout` is all zeros and zero PPO2 at zero pressure is exactly what the bands are for.
    /// The "nominal" check was passing a red bar. ⭐ So the fixture carries real cabin numbers, and the
    /// check below asserts it IS nominal before asserting what colour nominal draws - otherwise the
    /// first assertion proves nothing about the second.
    /// </summary>
    static PageState Nominal()
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Phase = "ORBITING";
        s.Power01 = 0.9; s.GForce01 = 0.05;
        // ⛔ `DragonProp01`, NOT `Propellant01`. `Alarms.PropellantSeverity` bands the FORMER; the
        // latter is the FLIGHT page's dial. Setting only the dial left the fixture at Alarm while
        // reading as fully fuelled - the "banded one field, printed another" shape [[S137]] hit for
        // real. Found by probing each component of `SystemSeverity` rather than by guessing which.
        CabinReadout c = new CabinReadout();
        c.Ppo2Psia = 3.0;      // caution below 2.5
        c.Co2MmHg = 1.6;       // caution above 4.0
        c.PressPsia = 14.7;    // caution below 13.0
        c.CabinTempC = 21.8;   // caution above 30.0
        c.LoopAC = 26.4; c.LoopBC = 20.1;   // caution above 45.0
        s.Cabin = c;
        s.DragonProp01 = 0.9;
        return s;
    }

    /// <summary>The colour the bar ACTUALLY drew CURRENT STATE in — read off the command, not
    /// recomputed. The bar emits one Text command and that is it.</summary>
    static Rgba StateColour(PageState s)
    {
        DisplayList dl = new DisplayList(64);
        BottomBar.Draw(dl, W, H, s);
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text) return dl.At(i).Colour;
        return new Rgba(0f, 0f, 0f, 0f);
    }

    static bool SameRgba(Rgba a, Rgba b)
    {
        return Math.Abs(a.R - b.R) < 1e-4f && Math.Abs(a.G - b.G) < 1e-4f
            && Math.Abs(a.B - b.B) < 1e-4f && Math.Abs(a.A - b.A) < 1e-4f;
    }

    static string Describe(Rgba c)
    { return "rgba(" + c.R.ToString("0.00") + "," + c.G.ToString("0.00") + "," + c.B.ToString("0.00") + ")"; }
}
