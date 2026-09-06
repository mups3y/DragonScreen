// Tests for pure/AlertActivity.cs — [[S133]] / QC `H-05`: Frame 58's ALERT ACTIVITY panel.
//
// ⛔ WHAT THIS SUITE IS ACTUALLY GUARDING. The panel is built out of [[S137]]'s `AlertList`, so the
// BANDS are already pinned by that suite and re-checking them here would be a second copy of the same
// assertions. What is new is the SELECTION and the ORDER — which rows reach a whole-vehicle panel, and
// in what sequence — plus the two things a list like this gets wrong in service: a quiet vehicle and a
// dead feed reading alike, and an overflow that hides the row that mattered.
using System;
using DragonScreen;

public static class AlertActivityTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen ALERT ACTIVITY (S133 / QC H-05)");
        checks = 0; failures = 0;

        AlertItem[] buf = new AlertItem[AlertActivity.Max];

        // ---- a quiet vehicle lists nothing, and says so in words -------------------------------
        PageState nom = Nominal();
        Check("S133 a quiet vehicle really is quiet, or the check below proves nothing",
              Alarms.SystemSeverity(nom) < Severity.Caution,
              "severity is " + Alarms.SystemSeverity(nom));
        Check("S133 ...and the panel lists no rows", AlertActivity.Build(nom, buf) == 0,
              "got " + AlertActivity.Build(nom, buf));
        Check("S133 ...and says NO ALERT ACTIVITY", AlertActivity.EmptyText(nom) == "NO ALERT ACTIVITY",
              AlertActivity.EmptyText(nom));

        // ---- ⚠ A DEAD FEED IS NOT A QUIET VEHICLE ----------------------------------------------
        PageState dead = new PageState();
        Check("S133 a dead feed lists no rows either", AlertActivity.Build(dead, buf) == 0, "");
        Check("S133 ...but must NOT say 'no alert activity' over a feed it cannot read",
              AlertActivity.EmptyText(dead) != AlertActivity.EmptyText(nom),
              "both say '" + AlertActivity.EmptyText(dead) + "'");
        Check("S133 ...it dashes", AlertActivity.EmptyText(dead) == Dashes.None,
              AlertActivity.EmptyText(dead));

        // ---- ⭐ ALARMS BEFORE CAUTIONS, AND NOTHING NOMINAL AT ALL ------------------------------
        PageState bad = Nominal();
        bad.Cabin.Co2MmHg = 6.5;      // alarm
        bad.Power01 = 0.06;           // alarm
        bad.Cabin.Ppo2Psia = 2.1;     // caution
        bad.DragonProp01 = 0.11;      // caution
        int n = AlertActivity.Build(bad, buf);
        Check("S133 a vehicle with four problems lists four rows", n == 4, "got " + n);

        bool ordered = true, anyNominal = false;
        for (int i = 0; i < n; i++)
        {
            if (buf[i].Sev < Severity.Caution) anyNominal = true;
            if (i > 0 && buf[i].Sev > buf[i - 1].Sev) ordered = false;
        }
        Check("S133 the list is worst-first", ordered, Describe(buf, n));
        // ⛔ a green row in an ALERT list is noise competing with what the crew is looking for
        Check("S133 nothing nominal is listed — this is an ALERT list, not a vehicle dump",
              !anyNominal, Describe(buf, n));
        Check("S133 the two alarms come before the two cautions",
              n == 4 && buf[0].Sev == Severity.Alarm && buf[1].Sev == Severity.Alarm
              && buf[2].Sev == Severity.Caution && buf[3].Sev == Severity.Caution,
              Describe(buf, n));

        // ---- every listed row must carry a label AND a value ------------------------------------
        bool complete = true;
        for (int i = 0; i < n; i++)
            if (string.IsNullOrEmpty(buf[i].Label) || string.IsNullOrEmpty(buf[i].Value)) complete = false;
        Check("S133 every row names itself and reads something", complete, Describe(buf, n));

        // ---- ⭐ THE PANEL AGREES WITH THE VEHICLE'S OWN VERDICT ---------------------------------
        // The worst row must BE the vehicle's severity. If they could differ, the page would be
        // contradicting the bottom bar [[S130]] tints from — one vehicle, two answers, which is the
        // defect S137 fixed one page over.
        Severity worst = Severity.Nominal;
        for (int i = 0; i < n; i++) if (buf[i].Sev > worst) worst = buf[i].Sev;
        Check("S133 the worst row equals the vehicle's own SystemSeverity",
              worst == Alarms.SystemSeverity(bad),
              "list says " + worst + ", Alarms says " + Alarms.SystemSeverity(bad));

        // ---- ⚠ STABILITY: the same state twice gives the same list, in the same order ------------
        AlertItem[] again = new AlertItem[AlertActivity.Max];
        int n2 = AlertActivity.Build(bad, again);
        bool same = (n == n2);
        for (int i = 0; i < n && same; i++)
            if (again[i].Label != buf[i].Label || again[i].Sev != buf[i].Sev) same = false;
        Check("S133 a steady vehicle gives a steady list — no reshuffle between frames", same,
              Describe(buf, n) + "  vs  " + Describe(again, n2));

        // ---- ⛔ AND A SMALL BUFFER DROPS THE QUIETEST ROWS, NEVER THE LOUDEST -------------------
        // The panel clips by height, so an overflowing list loses its tail. Worst-first is what makes
        // that safe; if the order ever inverted, the clip would hide the alarm and keep the caution.
        AlertItem[] tiny = new AlertItem[2];
        int nt = AlertActivity.Build(bad, tiny);
        Check("S133 a two-row buffer takes two rows", nt == 2, "got " + nt);
        Check("S133 ...and they are the two ALARMS, not the two cautions",
              nt == 2 && tiny[0].Sev == Severity.Alarm && tiny[1].Sev == Severity.Alarm,
              Describe(tiny, nt));

        // ---- the scope list must cover every AlertScope, or a subsystem silently never reports ---
        int scopes = 0;
        foreach (AlertScope sc in Enum.GetValues(typeof(AlertScope))) scopes++;
        Check("S133 the panel's capacity covers every scope at its maximum",
              AlertActivity.Max == AlertList.Max * scopes,
              "Max " + AlertActivity.Max + ", scopes " + scopes + ", per-scope " + AlertList.Max);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (822 px of nothing now lists the vehicle, worst first)");
        return failures;
    }

    static string Describe(AlertItem[] a, int n)
    {
        string s = "";
        for (int i = 0; i < n; i++) s += a[i].Label + "/" + a[i].Sev + " ";
        return s.Length == 0 ? "(empty)" : s;
    }

    /// <summary>A valid feed with nothing wrong. ⚠ The cabin numbers are load-bearing — a default
    /// `CabinReadout` is all zeros and reads as ALARM (see [[S130]]'s suite, which learned it the
    /// hard way), so a fixture that set only `Valid` would make every check below vacuous.</summary>
    static PageState Nominal()
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Power01 = 0.9; s.DragonProp01 = 0.9; s.GForce01 = 0.05;
        s.Cabin.Ppo2Psia = 3.0; s.Cabin.Co2MmHg = 1.6; s.Cabin.PressPsia = 14.7;
        s.Cabin.CabinTempC = 21.8; s.Cabin.LoopAC = 26.4; s.Cabin.LoopBC = 20.1;
        return s;
    }
}
