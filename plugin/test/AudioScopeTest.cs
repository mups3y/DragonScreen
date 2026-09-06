// Tests for [[S134c]] / QC `A-01` — the audio page's five scopes, four of which were unreachable.
//
// ⛔ WHAT WAS WRONG: `Build`'s `sel` picks one of five scopes and every caller passed the literal 2.
// `CABIN AUDIO` was the only heading the page could ever show, the four `SEAT n AUDIO` layouts were
// written and correct and dead, and the page drew five selectable-LOOKING seats of which none was.
//
// ⚠ AND WHAT THIS SUITE DELIBERATELY ALSO PINS: that selecting a seat does not start LYING. QC's own
// warning was that switching the heading over one shared set of numbers is "worse than one honest
// heading". [[S135]] settled that four of the channels are the GAME's global audio layers, so there is
// nothing per-seat behind them — and the page must say so when a seat is chosen.
using System;
using DragonScreen;

public static class AudioScopeTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const int W = 2560, H = 1406;
    const float RefW = 3427f, RefH = 2112f;

    public static int Run()
    {
        Console.WriteLine("DragonScreen audio page scopes (S134c / QC A-01)");
        checks = 0; failures = 0;

        Check("S134c the page has five scopes", SettingsAudioPage.Scopes == 5,
              "got " + SettingsAudioPage.Scopes);
        Check("S134c CABIN is the MIDDLE one, not the first",
              SettingsAudioPage.CabinScope == 2, "got " + SettingsAudioPage.CabinScope);
        Check("S134c a screen opens on CABIN — exactly what the page drew before it could be selected",
              PageControls.Default.AudioSeat == SettingsAudioPage.CabinScope,
              "got " + PageControls.Default.AudioSeat);

        // ---- ⭐ THE ROUND TRIP: every drawn seat is hit at its own centre ------------------------
        for (int i = 0; i < SettingsAudioPage.Scopes; i++)
        {
            float px, py;
            Centre(i, out px, out py);
            Check("S134c scope " + i + "'s own centre hits scope " + i,
                  SettingsAudioPage.SeatHitTest(px, py, W, H) == i,
                  "got " + SettingsAudioPage.SeatHitTest(px, py, W, H));
        }
        // and at a second aspect, because a projection bug is invisible at one (S134a/S134b's lesson)
        for (int i = 0; i < SettingsAudioPage.Scopes; i++)
        {
            float rx, ry, rw, rh;
            SettingsAudioPage.SeatRect(i, out rx, out ry, out rw, out rh);
            const int W2 = 4416, H2 = 1406;
            float px = (rx + rw * 0.5f) * W2 / RefW, py = (ry + rh * 0.5f) * H2 / RefH;
            Check("S134c @" + W2 + " scope " + i + " still hits itself",
                  SettingsAudioPage.SeatHitTest(px, py, W2, H2) == i, "");
        }

        // ---- the seats do not overlap, or a tap would be ambiguous ------------------------------
        for (int i = 1; i < SettingsAudioPage.Scopes; i++)
        {
            float ax, ay, aw, ah, bx, by, bw, bh;
            SettingsAudioPage.SeatRect(i - 1, out ax, out ay, out aw, out ah);
            SettingsAudioPage.SeatRect(i, out bx, out by, out bw, out bh);
            Check("S134c scope " + (i - 1) + " and " + i + " do not overlap", ax + aw <= bx,
                  (ax + aw) + " vs " + bx);
        }
        {
            float px, py;
            // between two seats
            float ax, ay, aw, ah, bx2, by2, bw2, bh2;
            SettingsAudioPage.SeatRect(0, out ax, out ay, out aw, out ah);
            SettingsAudioPage.SeatRect(1, out bx2, out by2, out bw2, out bh2);
            px = (ax + aw + (bx2 - ax - aw) * 0.5f) * W / RefW;
            py = (ay + ah * 0.5f) * H / RefH;
            Check("S134c the gap between two seats is inert",
                  SettingsAudioPage.SeatHitTest(px, py, W, H) < 0, "");
            Check("S134c a touch well above the seats is inert",
                  SettingsAudioPage.SeatHitTest(px, 10f, W, H) < 0, "");
        }

        // ---- ⛔ AND THE HEADING ACTUALLY CHANGES, read off the render ---------------------------
        // Before this line every render said CABIN AUDIO. Five renders, five headings.
        for (int i = 0; i < SettingsAudioPage.Scopes; i++)
        {
            string want = (i == SettingsAudioPage.CabinScope) ? "CABIN AUDIO" : "SEAT " + (i + 1) + " AUDIO";
            Check("S134c scope " + i + " draws the heading " + want, HasText(i, want),
                  "no '" + want + "' in the render");
        }

        // ---- ⚠ AND A SEAT SCOPE MARKS WHAT IT DOES NOT CHANGE -----------------------------------
        // QC: "five headings over one set of numbers is worse than one honest heading." The heading
        // moves; the numbers cannot, because they are the game's global audio layers (S135 / Q6). So
        // the page says which it is showing rather than implying a scope it does not have.
        Check("S134c the CABIN scope draws no per-seat caveat — there is nothing to caveat",
              !HasTextContaining(SettingsAudioPage.CabinScope, "PER-SEAT"), "");
        for (int i = 0; i < SettingsAudioPage.Scopes; i++)
        {
            if (i == SettingsAudioPage.CabinScope) continue;
            Check("S134c scope " + i + " says the levels are the vehicle's, not that seat's",
                  HasTextContaining(i, "PER-SEAT"), "no caveat drawn on scope " + i);
        }

        // ---- the CVR channel names the scope, and a miss names nothing --------------------------
        Check("S134c a scope press names itself", CrewControlIds.AudioScope(3) == "audio.scope3",
              CrewControlIds.AudioScope(3));
        Check("S134c ...on the same surface as the page's other controls",
              CrewControlIds.AudioScope(0).StartsWith(CrewControlIds.AudioPrefix), "");
        Check("S134c a miss names nothing", CrewControlIds.AudioScope(-1) == null, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (five scopes reachable, and the four seats say what they do not change)");
        return failures;
    }

    static void Centre(int i, out float px, out float py)
    {
        float rx, ry, rw, rh;
        SettingsAudioPage.SeatRect(i, out rx, out ry, out rw, out rh);
        px = (rx + rw * 0.5f) * W / RefW;
        py = (ry + rh * 0.5f) * H / RefH;
    }

    static bool HasText(int scope, string want)
    {
        DisplayList dl = new DisplayList(512);
        PageState s = new PageState(); s.Valid = true;
        SettingsAudioPage.Build(dl, W, H, scope, s);
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str == want) return true;
        return false;
    }

    static bool HasTextContaining(int scope, string fragment)
    {
        DisplayList dl = new DisplayList(512);
        PageState s = new PageState(); s.Valid = true;
        SettingsAudioPage.Build(dl, W, H, scope, s);
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str != null
                && dl.At(i).Str.IndexOf(fragment, StringComparison.Ordinal) >= 0) return true;
        return false;
    }
}
