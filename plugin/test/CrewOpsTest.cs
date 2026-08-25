/*
 * Tests for the crew-in-the-loop stack: LifeSupport margins, MissionProfile, the CrewProcedure gate
 * machine, and the CrewGates catalog. The point of a pure procedure engine is exactly this - a headless
 * test drives a whole countdown + approach through it and asserts every transition, which is the coverage
 * a "the vehicle must not pass a gate without the crew's GO" requirement needs before it ever flies.
 */
using System;
using DragonScreen;

public static class CrewOpsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen crew-ops (gates + life support) tests");

        LifeSupportChecks();
        ProfileChecks();
        GateCatalogChecks();
        ProcedureMachineChecks();
        GateCardChecks();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // ---------------------------------------------------------------- LifeSupport
    static void LifeSupportChecks()
    {
        // An empty capsule consumes nothing, so a tank never runs dry: +inf days.
        Check("no crew -> infinite endurance",
              double.IsPositiveInfinity(LifeSupport.Days(100.0, LifeSupport.OxygenPerKerbalSec, 0)), "");

        // One day of oxygen for one kerbal is exactly rate*86400 units, so Days of that is ~1.
        double oneDayO2 = LifeSupport.OxygenPerKerbalSec * LifeSupport.SecPerDay;
        Check("one day of O2 reads ~1 day",
              Math.Abs(LifeSupport.Days(oneDayO2, LifeSupport.OxygenPerKerbalSec, 1) - 1.0) < 1e-6,
              LifeSupport.Days(oneDayO2, LifeSupport.OxygenPerKerbalSec, 1).ToString("F4"));

        // More crew drains it proportionally faster.
        Check("four crew drain O2 four times faster",
              Math.Abs(LifeSupport.Days(oneDayO2, LifeSupport.OxygenPerKerbalSec, 4) - 0.25) < 1e-6, "");

        // Margins pick the LIMITING consumable and gate on mission + reserve.
        // 4 crew, 10 days of food+water but only 2 days of O2 -> limiting is O2 at 2 days.
        double food10 = LifeSupport.FoodPerKerbalSec * LifeSupport.SecPerDay * 10.0 * 4.0;
        double water10 = LifeSupport.WaterPerKerbalSec * LifeSupport.SecPerDay * 10.0 * 4.0;
        double o2_2 = LifeSupport.OxygenPerKerbalSec * LifeSupport.SecPerDay * 2.0 * 4.0;
        LsMargins m = LifeSupport.Margins(true, 4, food10, water10, o2_2);
        Check("limiting consumable is the shortest (O2, ~2 days)",
              Math.Abs(m.LimitingDays - 2.0) < 1e-6, m.LimitingDays.ToString("F3"));
        Check("sufficient for a 1-day mission + 0.5 reserve", LifeSupport.SufficientFor(m, 1.0, 0.5), "");
        Check("NOT sufficient for a 2-day mission + 0.5 reserve",
              !LifeSupport.SufficientFor(m, 2.0, 0.5), "");
        Check("O2 hours-to-loss = endurance + the 2 h survival window",
              Math.Abs(m.OxygenHoursToLoss - (2.0 * 24.0 + 2.0)) < 1e-6, m.OxygenHoursToLoss.ToString("F2"));

        // Absent TAC we cannot prove it short, so the commit gate must not block.
        LsMargins absent = LifeSupport.Margins(false, 4, 0.0, 0.0, 0.0);
        Check("TAC absent -> consumables gate does not block", LifeSupport.SufficientFor(absent, 99.0, 9.0), "");
    }

    // ---------------------------------------------------------------- MissionProfile
    static void ProfileChecks()
    {
        MissionProfile c = Missions.Crew2();
        Check("Crew-2 profile is valid", c.Valid(), "");
        Check("Crew-2 is the 51.6 deg ISS mission",
              Math.Abs(c.TargetInclinationDeg - 51.6) < 1e-9 && c.HasRendezvous, "");
        Check("Crew-2 L-approach is outside-in (WP0 > WP1 > WP2)",
              c.Wp0BelowM > c.Wp1AheadM && c.Wp1AheadM > c.Wp2RangeM, "");

        // A free-flyer is valid with no station and no waypoints.
        MissionProfile ff = c;
        ff.Name = "FREE-FLYER"; ff.HasRendezvous = false;
        ff.StationVesselName = null; ff.Wp0BelowM = ff.Wp1AheadM = ff.Wp2RangeM = ff.KeepOutSphereM = 0.0;
        Check("a free-flyer profile is valid without rendezvous fields", ff.Valid(), "");

        // A docking mission with a scrambled L is rejected.
        MissionProfile bad = c; bad.Wp1AheadM = 500.0;   // WP1 now further than WP0
        Check("a scrambled L-approach is rejected", !bad.Valid(), "");
    }

    // ---------------------------------------------------------------- CrewGates catalog
    static void GateCatalogChecks()
    {
        Gate[] outb = CrewGates.Outbound(Missions.Crew2());
        Check("outbound has gates", outb.Length > 0, "");
        Check("outbound ends the countdown with the GO/NO-GO poll", Has(outb, GateId.GoForLaunch), "");
        Check("outbound flies the L-approach holds",
              Has(outb, GateId.HoldWp0) && Has(outb, GateId.HoldWp1) && Has(outb, GateId.HoldWp2), "");
        Check("outbound ends at docking", Has(outb, GateId.DockingComplete), "");
        Check("every gate has at least one item", EveryGateHasItems(outb), "");
        Check("the launch poll carries the crew GO",
              GateWith(outb, GateId.GoForLaunch, "Dragon crew - GO"), "");
        Check("arming the launch escape system is a crew action",
              GateWith(outb, GateId.ArmLaunchEscape, "Arm Launch Escape System"), "");

        Gate[] ret = CrewGates.Return(Missions.Crew2());
        Check("return gates undock then deorbit",
              Has(ret, GateId.GoForUndock) && Has(ret, GateId.GoForDeorbit), "");

        // A free-flyer omits the approach + undock gates.
        MissionProfile ff = Missions.Crew2(); ff.HasRendezvous = false;
        Gate[] ffOut = CrewGates.Outbound(ff);
        Check("free-flyer outbound has the countdown", Has(ffOut, GateId.GoForLaunch), "");
        Check("free-flyer outbound has NO approach gates",
              !Has(ffOut, GateId.HoldWp0) && !Has(ffOut, GateId.DockingComplete), "");
        Gate[] ffRet = CrewGates.Return(ff);
        Check("free-flyer return has NO undock gate, still deorbits",
              !Has(ffRet, GateId.GoForUndock) && Has(ffRet, GateId.GoForDeorbit), "");
    }

    // ---------------------------------------------------------------- CrewProcedure machine
    static void ProcedureMachineChecks()
    {
        Gate[] gates = CrewGates.Outbound(Missions.Crew2());
        ProcState st = CrewProcedureCore.Begin(gates);
        Check("begins at the first gate, holding",
              st.GateIndex == 0 && st.Phase == GatePhase.Holding, "");

        // GO is refused until every item is satisfied.
        Gate g0 = CrewProcedureCore.Current(gates, st);
        Check("GO refused while the checklist is open", !CrewProcedureCore.Go(g0, ref st), "");
        Check("still holding after a refused GO", st.Phase == GatePhase.Holding, "");

        // Satisfy all but one -> still holding; the last one -> GO-ready.
        for (int i = 0; i < st.Satisfied.Length - 1; i++) CrewProcedureCore.SetItem(g0, ref st, i, true);
        Check("partial checklist stays holding", st.Phase == GatePhase.Holding, "");
        CrewProcedureCore.SetItem(g0, ref st, st.Satisfied.Length - 1, true);
        Check("full checklist is GO-ready", st.Phase == GatePhase.GoReady, "");

        // Un-checking an item drops back out of GO-ready.
        CrewProcedureCore.SetItem(g0, ref st, 0, false);
        Check("un-checking drops back to holding", st.Phase == GatePhase.Holding, "");
        CrewProcedureCore.SetItem(g0, ref st, 0, true);

        // GO clears it; Advance moves to the next gate, holding.
        Check("GO clears a ready gate", CrewProcedureCore.Go(g0, ref st) && st.Phase == GatePhase.Go, "");
        CrewProcedureCore.Advance(gates, ref st);
        Check("advanced to the next gate, holding",
              st.GateIndex == 1 && st.Phase == GatePhase.Holding, "");

        // NO-GO holds; ABORT latches abort.
        Gate g1 = CrewProcedureCore.Current(gates, st);
        CrewProcedureCore.NoGo(ref st);
        Check("NO-GO holds the mission", st.Phase == GatePhase.NoGo, "");
        SatisfyAll(g1, ref st);
        Check("clearing the checklist after a NO-GO returns to GO-ready", st.Phase == GatePhase.GoReady, "");
        CrewProcedureCore.Abort(ref st);
        Check("ABORT latches", st.Phase == GatePhase.Abort, "");
        CrewProcedureCore.SetItem(g1, ref st, 0, false);
        Check("a settled ABORT gate ignores further item changes", st.Phase == GatePhase.Abort, "");

        // A clean full drive of the whole outbound procedure reaches Complete.
        ProcState run = CrewProcedureCore.Begin(gates);
        int guard = 0;
        while (!CrewProcedureCore.Complete(run) && guard++ < 1000)
        {
            Gate g = CrewProcedureCore.Current(gates, run);
            SatisfyAll(g, ref run);
            Check("each gate becomes GO-ready when satisfied", run.Phase == GatePhase.GoReady,
                  g.Title);
            CrewProcedureCore.Go(g, ref run);
            CrewProcedureCore.Advance(gates, ref run);
        }
        Check("a fully-cleared outbound procedure completes", CrewProcedureCore.Complete(run), "");
        Check("clearing did not run away", guard < 1000, guard.ToString());
    }

    // ---------------------------------------------------------------- GateCard layout + hit test
    static void GateCardChecks()
    {
        int w = 1280, h = 703, items = 3;
        float x, y, cw, ch;
        GateCard.CardRect(w, h, items, out x, out y, out cw, out ch);
        Check("card is on-screen", x >= 0f && y >= 0f && x + cw <= w && y + ch <= h,
              x.ToString("F0") + "," + y.ToString("F0") + " " + cw.ToString("F0") + "x" + ch.ToString("F0"));

        // Item rows are inside the card, ordered, and do not overlap.
        float pry = -1f;
        for (int i = 0; i < items; i++)
        {
            float rx, ry, rw, rh;
            GateCard.ItemRect(i, x, y, cw, out rx, out ry, out rw, out rh);
            Check("item " + i + " is inside the card",
                  rx >= x && rx + rw <= x + cw && ry >= y && ry + rh <= y + ch, "");
            Check("item rows are ordered", ry > pry, "");
            pry = ry + rh;
        }

        // The three buttons are distinct, inside the card, and hit-test to their kinds.
        float bx, by, bw, bh;
        GateCard.ButtonRect(0, x, y, cw, ch, out bx, out by, out bw, out bh);
        Check("GO hit-tests to GO",
              GateCard.HitTest(bx + bw * 0.5f, by + bh * 0.5f, w, h, items).Kind == GateHitKind.Go, "");
        GateCard.ButtonRect(1, x, y, cw, ch, out bx, out by, out bw, out bh);
        Check("NO-GO hit-tests to NO-GO",
              GateCard.HitTest(bx + bw * 0.5f, by + bh * 0.5f, w, h, items).Kind == GateHitKind.NoGo, "");
        GateCard.ButtonRect(2, x, y, cw, ch, out bx, out by, out bw, out bh);
        Check("ABORT hit-tests to ABORT",
              GateCard.HitTest(bx + bw * 0.5f, by + bh * 0.5f, w, h, items).Kind == GateHitKind.Abort, "");

        // A tap on item 1 resolves to item 1; a tap in open space off the card resolves to nothing.
        float ix, iy, iw, ih;
        GateCard.ItemRect(1, x, y, cw, out ix, out iy, out iw, out ih);
        GateHit hit = GateCard.HitTest(ix + iw * 0.5f, iy + ih * 0.5f, w, h, items);
        Check("item tap resolves to that item", hit.Kind == GateHitKind.Item && hit.Item == 1, "");
        Check("a tap off the card resolves to nothing",
              GateCard.HitTest(2f, 2f, w, h, items).Kind == GateHitKind.None, "");
    }

    // ---- helpers ----
    static void SatisfyAll(Gate g, ref ProcState st)
    {
        for (int i = 0; i < st.Satisfied.Length; i++) CrewProcedureCore.SetItem(g, ref st, i, true);
    }

    static bool Has(Gate[] gates, GateId id)
    {
        for (int i = 0; i < gates.Length; i++) if (gates[i].Id == id) return true;
        return false;
    }

    static bool EveryGateHasItems(Gate[] gates)
    {
        for (int i = 0; i < gates.Length; i++)
            if (gates[i].Items == null || gates[i].Items.Length == 0) return false;
        return true;
    }

    static bool GateWith(Gate[] gates, GateId id, string itemLabel)
    {
        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i].Id != id) continue;
            for (int k = 0; k < gates[i].Items.Length; k++)
                if (gates[i].Items[k].Label == itemLabel) return true;
        }
        return false;
    }
}
