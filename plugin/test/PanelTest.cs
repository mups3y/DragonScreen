/*
 * DragonScreen headless tests - the lower console.
 *
 * ---- WHY THE INTERLOCK IS TESTED HERE AND NOT IN THE CAPSULE ----
 * Arm/execute is a state machine, and a state machine is exactly the thing a game restart is worst at
 * checking: every case needs its own press sequence, most of them are ones you would never think to
 * try by hand, and the interesting ones are the failures. Half a second here beats a restart each.
 *
 * The map is checked for the properties that a transcription can plausibly get wrong - a duplicate
 * transform, a plate that lost a button, a command wired to two places - rather than re-asserting the
 * labels, which would just be the same transcription typed twice.
 */
using System;
using System.Collections.Generic;
using DragonScreen;

public static class PanelTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen panel + sequence tests");

        Map();
        Sequence();
        Lighting();
        Inert();
        Board();
        LampsThatLied();   // S53 / H41 + H42: the STRING 1A/1B/1C lamps and DEPRESS RESPONSE
        Steps();
        Simulated();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // ------------------------------------------------------------------ the map

    static void Map()
    {
        PanelEntry[] all = PanelMap.All;

        // Transcribed from the in-game dump: 8 + 10 + 7 + 5 + 8 across the six populated plates.
        Check("map size", all.Length == 38, "got " + all.Length + " want 38");

        // No transform addressed twice. A duplicate would mean two controls fighting over one button.
        Dictionary<string, string> seen = new Dictionary<string, string>();
        for (int i = 0; i < all.Length; i++)
        {
            string key = all[i].Plate + "/" + all[i].Button;
            Check("unique " + key, !seen.ContainsKey(key),
                  "also used by " + (seen.ContainsKey(key) ? seen[key] : ""));
            seen[key] = all[i].Label;
        }

        // Every entry carries a label and a command. An entry mapped to None would be a dead button
        // that looks wired.
        for (int i = 0; i < all.Length; i++)
        {
            Check("labelled " + all[i].Plate + "/" + all[i].Button,
                  !string.IsNullOrEmpty(all[i].Label), "empty label");
            Check("commanded " + all[i].Label, all[i].Command != PanelCommand.None, "None");
        }

        // The two emergency plates must be identical control sets - either seat can reach one, and a
        // plate that quietly lost a button would leave one seat unable to abort.
        int left = 0, right = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].Plate == PanelMap.PlateLeftEmerg) left++;
            if (all[i].Plate == PanelMap.PlateRightEmerg) right++;
        }
        Check("emergency plates match", left == right && left == 8,
              "left " + left + " right " + right);

        Check("only three need execute",
              PanelMap.NeedsExecute(PanelCommand.DeorbitNow)
              && PanelMap.NeedsExecute(PanelCommand.WaterDeorbit)
              && PanelMap.NeedsExecute(PanelCommand.Breakout)
              && !PanelMap.NeedsExecute(PanelCommand.Execute)
              && !PanelMap.NeedsExecute(PanelCommand.Cancel)
              && !PanelMap.NeedsExecute(PanelCommand.CutMains), "");
    }

    // ------------------------------------------------------------------ arm / execute

    static void Sequence()
    {
        Interlock k = new Interlock();

        // EXECUTE with nothing armed is the dangerous press: the crew believes something is armed.
        // It must refuse loudly, not quietly do nothing.
        Check("bare execute refuses", k.Press(PanelCommand.Execute) == PressResult.Refused, "");
        Check("bare execute fires nothing", k.Fired == PanelCommand.None, k.Fired.ToString());

        // CANCEL with nothing armed is the careful press. It must NOT go red.
        Check("bare cancel ignored", k.Press(PanelCommand.Cancel) == PressResult.Ignored, "");

        // The normal path.
        Check("arm", k.Press(PanelCommand.DeorbitNow) == PressResult.Armed, "");
        Check("armed is held", k.Armed == PanelCommand.DeorbitNow, k.Armed.ToString());
        Check("execute fires", k.Press(PanelCommand.Execute) == PressResult.Fire, "");
        Check("fired the armed one", k.Fired == PanelCommand.DeorbitNow, k.Fired.ToString());
        Check("disarmed after firing", k.Armed == PanelCommand.None, k.Armed.ToString());

        // ...and must not fire twice off one arming.
        Check("second execute refuses", k.Press(PanelCommand.Execute) == PressResult.Refused, "");

        // CANCEL clears.
        k.Press(PanelCommand.Breakout);
        Check("cancel clears", k.Press(PanelCommand.Cancel) == PressResult.Cancelled, "");
        Check("nothing armed after cancel", k.Armed == PanelCommand.None, k.Armed.ToString());

        // Same button twice disarms - it is its own toggle.
        k.Press(PanelCommand.WaterDeorbit);
        Check("re-press disarms", k.Press(PanelCommand.WaterDeorbit) == PressResult.Cancelled, "");
        Check("really disarmed", k.Armed == PanelCommand.None, k.Armed.ToString());

        // Changing your mind re-arms rather than refusing. The one situation this panel exists for is
        // the one with no seconds to spare for finding CANCEL first.
        k.Press(PanelCommand.DeorbitNow);
        Check("switch re-arms", k.Press(PanelCommand.Breakout) == PressResult.Armed, "");
        Check("switched to the new one", k.Armed == PanelCommand.Breakout, k.Armed.ToString());
        Check("switch fires the NEW one",
              k.Press(PanelCommand.Execute) == PressResult.Fire && k.Fired == PanelCommand.Breakout,
              k.Fired.ToString());

        // A non-interlock command must not disturb an arming - pressing CUT MAINS while a deorbit is
        // armed should leave the deorbit armed.
        k.Clear();
        k.Press(PanelCommand.DeorbitNow);
        Check("unrelated press ignored", k.Press(PanelCommand.CutMains) == PressResult.Ignored, "");
        Check("arming survives it", k.Armed == PanelCommand.DeorbitNow, k.Armed.ToString());
    }

    // ------------------------------------------------------------------ lighting (§14.4a)

    static void Lighting()
    {
        // ---- THE WHOLE POINT: THERE IS NO THIRD COLOUR ----
        // §14.4(a) removed the red refused-dash. The way to keep it removed is not a comment saying
        // so - it is a test that walks every outcome the panel can produce and asserts each one is
        // either bright or dark. A reintroduced red state cannot get past this without someone
        // deliberately editing the assertion, which is exactly the friction it is for.
        foreach (PanelPressKind k in Enum.GetValues(typeof(PanelPressKind)))
        {
            PanelLight l = PanelPolicy.LampFor(k);
            Check("no colour beyond bright/dark for " + k,
                  l == PanelLight.Lit || l == PanelLight.Dark, l.ToString());
        }
        Check("the light enum itself has only two states",
              Enum.GetValues(typeof(PanelLight)).Length == 2,
              Enum.GetValues(typeof(PanelLight)).Length.ToString());

        // BRIGHT when active, armed or fired. Dark otherwise - and "otherwise" now includes every
        // press that could not act, which used to be the loud one.
        Check("armed is bright", PanelPolicy.LampFor(PanelPressKind.Armed) == PanelLight.Lit, "");
        Check("fired is bright", PanelPolicy.LampFor(PanelPressKind.Momentary) == PanelLight.Lit, "");
        Check("a mode coming on is bright",
              PanelPolicy.LampFor(PanelPressKind.ModeOn) == PanelLight.Lit, "");
        Check("a mode going off is dark",
              PanelPolicy.LampFor(PanelPressKind.ModeOff) == PanelLight.Dark, "");
        Check("a press that could not act is DARK, not red",
              PanelPolicy.LampFor(PanelPressKind.Nothing) == PanelLight.Dark, "");
        Check("an inert press is dark",
              PanelPolicy.LampFor(PanelPressKind.Inert) == PanelLight.Dark, "");

        // Only the states that mean "still true" hold. A flash that latched would leave the console
        // lit up after an emergency was already dealt with.
        Check("armed holds", PanelPolicy.Latches(PanelPressKind.Armed), "");
        Check("a live mode holds", PanelPolicy.Latches(PanelPressKind.ModeOn), "");
        Check("a fired flash does not hold", !PanelPolicy.Latches(PanelPressKind.Momentary), "");
        Check("a cancel flash does not hold", !PanelPolicy.Latches(PanelPressKind.Disarmed), "");

        // ---- REFUSAL IS STILL A REFUSAL, IT IS JUST NOT A COLOUR ----
        // The interlock must keep refusing a bare EXECUTE; what changed is only the answer the panel
        // gives back. Conflating the two would quietly weaken the interlock.
        Check("bare execute still refuses in the interlock, and shows nothing",
              PanelPolicy.ResolveInterlock(PressResult.Refused, false) == PanelPressKind.Nothing, "");
        Check("a fire nothing acted on shows nothing",
              PanelPolicy.ResolveInterlock(PressResult.Fire, false) == PanelPressKind.Nothing, "");
        Check("a fire that acted is bright",
              PanelPolicy.ResolveInterlock(PressResult.Fire, true) == PanelPressKind.Momentary, "");

        // Both ways of consuming an arming must put the armed lamp out.
        Check("firing clears the armed lamp", PanelPolicy.ClearsArmedLamps(PressResult.Fire), "");
        Check("cancelling clears the armed lamp",
              PanelPolicy.ClearsArmedLamps(PressResult.Cancelled), "");
        Check("arming does not clear it", !PanelPolicy.ClearsArmedLamps(PressResult.Armed), "");

        // CANCEL with nothing armed and nothing running: the careful press, still unpunished.
        Check("bare cancel shows nothing",
              PanelPolicy.ResolveCancel(PressResult.Ignored, false) == PanelPressKind.Nothing, "");
        Check("cancel that stopped a sequence is bright",
              PanelPolicy.ResolveCancel(PressResult.Ignored, true) == PanelPressKind.Momentary, "");

        // ---- EVERY PRESS IS AUDIBLE ----
        // With no red dash, the click is the ONLY feedback an inert or unbacked control gives. A
        // silent one is indistinguishable from a collider that missed.
        PanelEntry[] all = PanelMap.All;
        for (int i = 0; i < all.Length; i++)
            Check("clicks: " + all[i].Label, PanelPolicy.Clicks(all[i].Command), "silent");
        Check("the EJECT handle clicks too", PanelPolicy.Clicks(PanelCommand.Abort), "");
    }

    // ------------------------------------------------------------------ inert controls (§14.4b)

    static void Inert()
    {
        // ---- THE SIX, AND ONLY THE SIX ----
        // SWAP 1/2/3 and the three entry-mode toggles are inferred, not sourced (§4). Naming them
        // one by one rather than counting means a seventh cannot be added by accident, and none of
        // these six can quietly go missing.
        PanelCommand[] inert =
        {
            PanelCommand.SwapString1, PanelCommand.SwapString2, PanelCommand.SwapString3,
            PanelCommand.EnableEntryReboot, PanelCommand.EnableBackupEntry,
            PanelCommand.EnableNormalEntry
        };
        for (int i = 0; i < inert.Length; i++)
            Check("inert: " + inert[i], PanelPolicy.IsInert(inert[i]), "not inert");

        int count = 0;
        foreach (PanelCommand c in Enum.GetValues(typeof(PanelCommand)))
            if (PanelPolicy.IsInert(c)) count++;
        Check("exactly six controls are inert", count == inert.Length, "got " + count);

        // ---- AND THE CONFIRMED NEIGHBOURS ON THE SAME PLATES ARE NOT ----
        // ENABLE BACKUP PYROS sits beside ENABLE ENTRY REBOOT and FIRE PYRD sits on the same plate;
        // both are confirmed-real commands (§4). Being unverified is a property of the control, not
        // of the plate, and a list that swept the plate would take working controls with it.
        Check("ENABLE BACKUP PYROS is not inert",
              !PanelPolicy.IsInert(PanelCommand.EnableBackupPyros), "");
        Check("FIRE PYRD is not inert", !PanelPolicy.IsInert(PanelCommand.FirePyro), "");
        Check("the power buses are not inert",
              !PanelPolicy.IsInert(PanelCommand.Power1) && !PanelPolicy.IsInert(PanelCommand.Power2), "");
        Check("the STRING buttons are not inert",
              !PanelPolicy.IsInert(PanelCommand.String1A) && !PanelPolicy.IsInert(PanelCommand.String2C), "");
        Check("RESET is not inert (owner kept it as display state)",
              !PanelPolicy.IsInert(PanelCommand.Reset1), "");
        Check("the fire and leak responses are not inert",
              !PanelPolicy.IsInert(PanelCommand.SuppressFire)
              && !PanelPolicy.IsInert(PanelCommand.FireResponse)
              && !PanelPolicy.IsInert(PanelCommand.DepressResponse), "");

        // ---- CLICK, NO LIGHT, NO ACTION ----
        // Told with `acted = true`: even if a dispatcher DID carry it out, an inert control lights
        // nothing. The gate is the control, not the outcome - which is what makes it safe when Part
        // B starts filling in the dispatcher one command at a time.
        for (int i = 0; i < inert.Length; i++)
        {
            Check("inert press does nothing: " + inert[i],
                  PanelPolicy.ResolveImmediate(inert[i], true, true) == PanelPressKind.Inert, "");
            Check("inert press stays dark: " + inert[i],
                  PanelPolicy.LampFor(PanelPolicy.ResolveImmediate(inert[i], true, true))
                      == PanelLight.Dark, "");
            Check("inert control is not a mode lamp: " + inert[i],
                  !PanelPolicy.IsMode(inert[i]) && !PanelPolicy.IsLiveMode(inert[i]), "");
        }

        // The confirmed mode next door still latches, or the decision took a working lamp with it.
        Check("ENABLE BACKUP PYROS still latches",
              PanelPolicy.ResolveImmediate(PanelCommand.EnableBackupPyros, true, true)
                  == PanelPressKind.ModeOn, "");
    }

    // ------------------------------------------------------------------ S53: the two lamps that lied

    static void LampsThatLied()
    {
        // ---- (a) H41: STRING 1A/1B/1C COULD NEVER LIGHT ----
        // They are live-mode lamps, so `PanelButton.Update` re-reads `ModeIsOn` every tick and
        // re-darkens on a false. `ModeIsOn` read `AutoPilot.Engaged` / `StationApproach.Engaged` /
        // `DockingOps.Engaged` — three hard-`false` stubs — while the PRESS went to
        // `Systems.ToggleString(ref State, 1, 0..2)`. Sim state changed; the dash never moved.
        // The lamp's two decisions are now pure, so the composition the glue performs is pinned here.
        PanelCommand[] row1 = { PanelCommand.String1A, PanelCommand.String1B, PanelCommand.String1C };

        for (int i = 0; i < row1.Length; i++)
        {
            int bus, index;
            Check("STRING 1" + (char)('A' + i) + " is a string lamp",
                  PanelPolicy.StringLamp(row1[i], out bus, out index), "");
            Check("STRING 1" + (char)('A' + i) + " reports bus 1, index " + i,
                  bus == 1 && index == i, "got bus " + bus + " index " + index);
            Check("STRING 1" + (char)('A' + i) + " is still a live-mode lamp (it tracks state, not its press)",
                  PanelPolicy.IsLiveMode(row1[i]), "");
        }

        // Nothing else claims to be a string lamp — above all not the row-2 siblings, which are
        // deliberately momentary, and not the POWER buses that share the plate.
        PanelCommand[] notStrings = {
            PanelCommand.String2A, PanelCommand.String2B, PanelCommand.String2C,
            PanelCommand.Power1, PanelCommand.Power2, PanelCommand.Reset1, PanelCommand.Reset2,
            PanelCommand.DepressResponse, PanelCommand.EnableBackupPyros, PanelCommand.None };
        for (int i = 0; i < notStrings.Length; i++)
        {
            int bus, index;
            Check("not a string lamp: " + notStrings[i],
                  !PanelPolicy.StringLamp(notStrings[i], out bus, out index), "");
            Check("...and it hands back no bus/index to read by mistake: " + notStrings[i],
                  bus == -1 && index == -1, "got bus " + bus + " index " + index);
        }

        // THE DECISIVE CHECK: press the button through the same model the glue writes to, and the
        // lamp must follow. Online → lit; the crew isolating it → dark; a fault tripping it → dark.
        {
            SystemsState st = SystemsState.Fresh(); st.Bus1On = true;
            for (int i = 0; i < row1.Length; i++)
            {
                int bus, index;
                PanelPolicy.StringLamp(row1[i], out bus, out index);
                Check("a fresh online string lights its lamp: 1" + (char)('A' + i),
                      PanelPolicy.StringLampOn(Systems.Get(st, bus, index)), "");
                Systems.ToggleString(ref st, bus, index);          // the press the crew makes
                Check("isolating it darkens the lamp: 1" + (char)('A' + i),
                      !PanelPolicy.StringLampOn(Systems.Get(st, bus, index)),
                      Systems.Get(st, bus, index).ToString());
                Systems.ToggleString(ref st, bus, index);          // and back
                Check("closing it again re-lights the lamp: 1" + (char)('A' + i),
                      PanelPolicy.StringLampOn(Systems.Get(st, bus, index)),
                      Systems.Get(st, bus, index).ToString());
            }

            // A TRIPPED string is dark too — a two-state dash has no third answer, and the crew reads
            // isolated-vs-tripped off the glass. Trip it the way the model does: undervoltage.
            SystemsState tr = SystemsState.Fresh(); tr.Bus1On = true;
            // 0.12 is under TripCharge (0.15) but ABOVE TripCharge*0.6 (0.09), so ONLY the C string
            // trips — which is what makes the neighbours check below mean something. (0.05, as used
            // elsewhere in this file, would take B down with it.)
            SystemsInputs flat = Quiet(1.0); flat.Charge01 = 0.12;
            Systems.Update(ref tr, flat);
            Check("undervoltage tripped the C string (fixture precondition)",
                  Systems.Get(tr, 1, 2) == StringState.Tripped, Systems.Get(tr, 1, 2).ToString());
            Check("a tripped string is dark, not lit",
                  !PanelPolicy.StringLampOn(Systems.Get(tr, 1, 2)), "");
            Check("...and its neighbours are unaffected",
                  PanelPolicy.StringLampOn(Systems.Get(tr, 1, 0))
                  && PanelPolicy.StringLampOn(Systems.Get(tr, 1, 1)), "");
        }

        // Exactly the three StringState values are covered, and only Online lights.
        Check("Online lights", PanelPolicy.StringLampOn(StringState.Online), "");
        Check("Isolated does not", !PanelPolicy.StringLampOn(StringState.Isolated), "");
        Check("Tripped does not", !PanelPolicy.StringLampOn(StringState.Tripped), "");

        // ---- (b) H42: DEPRESS RESPONSE FLASHED "ACTED" OVER A REFUSAL ----
        // The dispatcher case called `Systems.DepressResponse(ref State)` and then `return true`,
        // discarding the model's answer, so the lamp lit even with no leak to isolate. The model's
        // refusal was always correct; only the glue threw it away. Pinned here as the COMPOSITION the
        // glue performs — model answer → `ResolveImmediate` → lamp — so a re-discarded bool shows up.
        {
            SystemsState calm = SystemsState.Fresh();
            bool actedCalm = Systems.DepressResponse(ref calm);
            Check("no leak: the model refuses", !actedCalm, "");
            Check("no leak: the press resolves to Nothing",
                  PanelPolicy.ResolveImmediate(PanelCommand.DepressResponse, actedCalm, false)
                      == PanelPressKind.Nothing, "");
            Check("no leak: THE LAMP STAYS DARK (§14.4(a): click, no light, no action)",
                  PanelPolicy.LampFor(PanelPolicy.ResolveImmediate(
                      PanelCommand.DepressResponse, actedCalm, false)) == PanelLight.Dark, "");
            Check("no leak: and the state is untouched by the refusal",
                  !calm.Isolating && !calm.Leaking, "");

            SystemsState lk = SystemsState.Fresh();
            SystemsInputs hard = Quiet(1.0); hard.GForce = 14.0;
            Systems.Update(ref lk, hard);
            Check("a real leak exists (fixture precondition)", lk.Leaking, "");
            bool actedLeak = Systems.DepressResponse(ref lk);
            Check("with a leak: the model acts", actedLeak && lk.Isolating, "");
            Check("with a leak: THE LAMP LIGHTS",
                  PanelPolicy.LampFor(PanelPolicy.ResolveImmediate(
                      PanelCommand.DepressResponse, actedLeak, false)) == PanelLight.Lit, "");
            Check("the two answers actually differ — the bool is load-bearing, not decoration",
                  actedLeak != actedCalm, "");

            // Its two plate-siblings always returned theirs; they must still behave the same way, or
            // this fix has quietly changed the honest ones instead of the dishonest one.
            SystemsState nofire = SystemsState.Fresh();
            Check("SUPPRESS FIRE still refuses with no fire, and stays dark",
                  PanelPolicy.LampFor(PanelPolicy.ResolveImmediate(PanelCommand.SuppressFire,
                      Systems.SuppressFire(ref nofire), false)) == PanelLight.Dark, "");
        }
    }

    // ------------------------------------------------------------------ the whole board

    static void Board()
    {
        // ---- ARM ON THE LEFT, EXECUTE ON THE RIGHT ----
        // The two emergency plates are ONE control set so either seat can reach one, and that is a
        // board-level property: no single button can be asked whether it holds.
        PanelBoard b = new PanelBoard();
        int leftArm = PanelBoard.IndexOf(PanelMap.PlateLeftEmerg, PanelCommand.DeorbitNow);
        int rightExec = PanelBoard.IndexOf(PanelMap.PlateRightEmerg, PanelCommand.Execute);
        Check("both plates carry the controls", leftArm >= 0 && rightExec >= 0,
              leftArm + " / " + rightExec);

        Check("arming on the left is bright",
              b.Press(leftArm, false, false, false) == PanelPressKind.Armed, "");
        Check("and it HOLDS", b.Lamp(leftArm) == PanelLight.Lit, b.Lamp(leftArm).ToString());
        b.FlashesOut();
        Check("a held arming survives the flash timer", b.Lamp(leftArm) == PanelLight.Lit, "");

        // EXECUTE from the OTHER seat. With no flight software behind DEORBIT NOW it acts on
        // nothing, so EXECUTE itself shows nothing - but the arming must still be consumed and its
        // lamp must go out, or the panel says something is armed when nothing is.
        Check("executing from the right seat fires",
              b.Press(rightExec, false, false, false) == PanelPressKind.Nothing, "");
        Check("the left plate's armed lamp went out",
              b.Lamp(leftArm) == PanelLight.Dark, b.Lamp(leftArm).ToString());
        Check("nothing anywhere is lit after it", !b.AnyLit(), "");

        // Same again, but the command acts: EXECUTE flashes bright and the arming still clears.
        PanelBoard c = new PanelBoard();
        c.Press(leftArm, false, false, false);
        Check("an EXECUTE that acted is bright",
              c.Press(rightExec, true, false, false) == PanelPressKind.Momentary, "");
        Check("the arming cleared anyway", c.Lamp(leftArm) == PanelLight.Dark, "");

        // ---- AN INERT PRESS CHANGES NOTHING ON THE BOARD ----
        PanelBoard d = new PanelBoard();
        int swap = PanelBoard.IndexOf(PanelMap.PlateEntry, PanelCommand.SwapString2);
        Check("SWAP 2 is on the board", swap >= 0, swap.ToString());
        Check("pressing it is inert", d.Press(swap, true, true, false) == PanelPressKind.Inert, "");
        Check("it lit nothing at all", !d.AnyLit(), "");
        Check("but it CLICKED", d.LastClicked, "silent");

        // ---- A LIVE MODE LAMP FOLLOWS THE STATE, NOT THE PRESS ----
        PanelBoard e = new PanelBoard();
        int pwr = PanelBoard.IndexOf(PanelMap.PlatePower, PanelCommand.Power1);
        Check("POWER 1 lights when its bus comes on",
              e.Press(pwr, true, true, false) == PanelPressKind.ModeOn, "");
        Check("and holds", e.Lamp(pwr) == PanelLight.Lit, "");
        e.FlashesOut();
        Check("still holding after the flash timer", e.Lamp(pwr) == PanelLight.Lit, "");
        Check("pressing it off goes dark",
              e.Press(pwr, true, false, false) == PanelPressKind.ModeOff, "");
        Check("dark", e.Lamp(pwr) == PanelLight.Dark, "");
        // ...and it can be driven from the touchscreen instead, with no press at all.
        e.SetModeLamp(PanelCommand.Power1, true);
        Check("the lamp tracks state set elsewhere", e.Lamp(pwr) == PanelLight.Lit, "");

        // ---- A PRESS THAT COULD NOT ACT LEAVES NO MARK ----
        // Pressing an unpowered STRING used to flash red. It must now be completely quiet, which
        // also means it must not stamp on the live lamp it shares the row with.
        PanelBoard f = new PanelBoard();
        int s1a = PanelBoard.IndexOf(PanelMap.PlatePower, PanelCommand.String1A);
        f.SetModeLamp(PanelCommand.String1A, true);
        Check("a refused press shows nothing",
              f.Press(s1a, false, false, false) == PanelPressKind.Nothing, "");
        Check("and does not disturb the lamp's own state",
              f.Lamp(s1a) == PanelLight.Lit, f.Lamp(s1a).ToString());

        // ---- CANCEL PUTS OUT EVERY ARMED LAMP, ON BOTH PLATES ----
        PanelBoard g = new PanelBoard();
        int lArm = PanelBoard.IndexOf(PanelMap.PlateLeftEmerg, PanelCommand.Breakout);
        int rCancel = PanelBoard.IndexOf(PanelMap.PlateRightEmerg, PanelCommand.Cancel);
        g.Press(lArm, false, false, false);
        Check("armed", g.Lamp(lArm) == PanelLight.Lit, "");
        Check("cancel from the other seat is bright",
              g.Press(rCancel, false, false, false) == PanelPressKind.Disarmed, "");
        Check("the arming lamp is out", g.Lamp(lArm) == PanelLight.Dark, "");
        g.FlashesOut();
        Check("and the board is dark", !g.AnyLit(), "");

        // ---- NOTHING ON THE BOARD CAN LIGHT ANYTHING BUT BRIGHT OR DARK ----
        // The board-level restatement of §14.4(a): press every button, both ways, and look at all
        // 38 lamps each time.
        PanelBoard h = new PanelBoard();
        for (int i = 0; i < h.Count; i++)
        {
            h.Press(i, true, true, true);
            h.Press(i, false, false, false);
            for (int j = 0; j < h.Count; j++)
                Check("lamp " + j + " stays two-state after pressing " + i,
                      h.Lamp(j) == PanelLight.Lit || h.Lamp(j) == PanelLight.Dark,
                      h.Lamp(j).ToString());
        }
    }

    // ------------------------------------------------------------------ FLIGHT's sequence

    const int W = 1280, H = 710;

    static StepInputs Pad()
    {
        StepInputs s = new StepInputs();
        s.Valid = true; s.Phase = MissionPhase.Prelaunch;
        s.Crew = 4; s.OnPad = true; s.Clamped = true; s.Powered = true;
        s.Propellant01 = 0.97; s.EscapeArmed = true;
        s.BoosterAttached = true; s.S2Attached = true;
        return s;
    }

    static StepState StateOf(StepInputs s, StepId id)
    {
        StepRow[] rows = new StepRow[(int)StepId.Count];
        int n = StepList.Build(s, rows);
        for (int i = 0; i < n; i++) if (rows[i].Id == id) return rows[i].State;
        return StepState.Pending;
    }

    static void Steps()
    {
        // ---- THE DESIGN INVARIANT ----
        // Launch is commanded by the Launch Director, not the crew. If a LAUNCH step ever appears
        // here it means someone has given the console an authority the real one does not have, and
        // that is worth failing a build over rather than catching in a screenshot.
        StepRow[] rows = new StepRow[(int)StepId.Count];
        int n = StepList.Build(Pad(), rows);
        Check("sequence is populated", n == (int)StepId.Count, "got " + n);
        for (int i = 0; i < n; i++)
        {
            string l = rows[i].Label;
            Check("no launch command on the list " + l,
                  l != "LAUNCH" && l != "GO FOR LAUNCH" && l != "START LAUNCH" && l != "IGNITION", l);
        }

        // Exactly one running step, always - it is what the highlight means.
        int active = 0;
        for (int i = 0; i < n; i++) if (rows[i].State == StepState.Active) active++;
        Check("exactly one active step on the pad", active == 1, "got " + active);

        // On the pad with nothing ticked, the crew steps are outstanding and the first is running.
        StepInputs pad = Pad();
        Check("crew aboard is observed done", StateOf(pad, StepId.CrewAboard) == StepState.Done, "");
        Check("comm check waits for the crew",
              StateOf(pad, StepId.CommCheck) == StepState.Active, "");
        Check("hatch close still pending",
              StateOf(pad, StepId.HatchClose) == StepState.Pending, "");

        // Ticking advances it, and only it.
        pad.Acknowledged = StepList.Acknowledge(pad.Acknowledged, StepId.CommCheck);
        Check("tick completes comm check", StateOf(pad, StepId.CommCheck) == StepState.Done, "");
        Check("next crew step becomes active",
              StateOf(pad, StepId.SeatRotation) == StepState.Active, "");

        // Clamped on the pad means liftoff has NOT happened, however much else is ticked.
        Check("liftoff not done while clamped",
              StateOf(pad, StepId.Liftoff) != StepState.Done, "");

        // ---- ONCE FLYING, THE COUNTDOWN IS OVER ----
        // Untidy but correct: a crew member who skipped a tap must not be nagged at 40 km by a list
        // that events have overtaken.
        StepInputs up = Pad();
        up.OnPad = false; up.Clamped = false; up.Acknowledged = 0;
        up.RadarAltitude = 40000.0; up.BoosterLit = true;
        Check("hatch close closes out once flying",
              StateOf(up, StepId.HatchClose) == StepState.Done, "");
        Check("liftoff done once flying", StateOf(up, StepId.Liftoff) == StepState.Done, "");

        // Staging is observed, not assumed.
        StepInputs sep = up;
        sep.BoosterAttached = false; sep.BoosterLit = false; sep.S2Lit = true;
        Check("stage sep observed", StateOf(sep, StepId.StageSep) == StepState.Done, "");
        Check("MECO implied by a gone booster", StateOf(sep, StepId.Meco) == StepState.Done, "");
        Check("dragon sep NOT done with S2 attached",
              StateOf(sep, StepId.DragonSep) != StepState.Done, "");

        // ---- ABORT MODES ----
        Check("pad abort on the pad", StepList.AbortMode(Pad()) == "PAD ABORT",
              StepList.AbortMode(Pad()));
        StepInputs dis = Pad(); dis.EscapeArmed = false;
        Check("disarmed reads disarmed", StepList.AbortMode(dis) == "DISARMED",
              StepList.AbortMode(dis));
        StepInputs low = up; low.MaxQPassed = false;
        Check("mode 1 before max q", StepList.AbortMode(low) == "MODE 1 - LOW ALT",
              StepList.AbortMode(low));
        StepInputs hi = up; hi.MaxQPassed = true;
        Check("mode 2 after max q", StepList.AbortMode(hi) == "MODE 2 - HIGH ALT",
              StepList.AbortMode(hi));
        StepInputs free = up;
        free.BoosterAttached = false; free.S2Attached = false;
        Check("no mode once Dragon is free",
              StepList.AbortMode(free) == "NONE - DRAGON FREE", StepList.AbortMode(free));

        // ---- DRAWN WHERE IT IS HIT ----
        // The ChromeBar.LinkRect rule: aim at the centre of the rectangle the page draws and assert
        // the page's own hit test returns that step.
        for (int i = 0; i < (int)StepId.Count; i++)
        {
            float x, y, rw, rh;
            Pages.StepRect(i, W, H, out x, out y, out rw, out rh);
            PageHit got = Pages.HitTest(0, x + rw * 0.5f, y + rh * 0.4f, W, H, 0);
            Check("step " + i + " is hit where it is drawn",
                  got.Act == PageAct.AckStep && got.Arg == i,
                  got.Act + " arg " + got.Arg);
        }

        // And the list must fit above the chrome bar, or the last steps are unreachable.
        float lx, ly, lw, lh;
        Pages.StepRect((int)StepId.Count - 1, W, H, out lx, out ly, out lw, out lh);
        Check("the whole list fits on the page", ly + lh < H - ChromeBar.Height,
              "last step ends at " + (ly + lh) + ", chrome starts at " + (H - ChromeBar.Height));
    }

    // ------------------------------------------------------------------ the simulated systems

    static SystemsInputs Quiet(double dt)
    {
        SystemsInputs i = new SystemsInputs();
        i.Valid = true; i.Dt = dt; i.Crew = 4;
        i.Charge01 = 0.9; i.HottestPart01 = 0.2; i.GForce = 1.0;
        return i;
    }

    static void Simulated()
    {
        // ---- NOTHING FIRES IN NORMAL FLIGHT ----
        // The whole model is worthless if it cries wolf. An hour of quiet cruise must produce no
        // fire, no leak and no trip.
        SystemsState s = SystemsState.Fresh(); s.Bus1On = true;   // powered, or Get() reads Online regardless
        for (int t = 0; t < 360; t++) Systems.Update(ref s, Quiet(10.0));
        Check("quiet cruise starts no fire", !s.Fire, s.FireIntensity.ToString("F3"));
        Check("quiet cruise springs no leak", !s.Leaking, s.LeakRate.ToString("F3"));
        Check("quiet cruise trips no string",
              Systems.Get(s, 1, 2) == StringState.Online, Systems.Get(s, 1, 2).ToString());
        // ...but the consumables MUST have moved, or they are decoration.
        Check("oxygen is being used", s.Oxygen < 1.0 && s.Oxygen > 0.9, s.Oxygen.ToString("F4"));
        Check("canisters are loading", s.CanisterUsed > 0.0, s.CanisterUsed.ToString("F4"));

        // ---- EVERY EVENT HAS A REAL TRIGGER ----
        SystemsState f = SystemsState.Fresh();
        SystemsInputs hot = Quiet(1.0); hot.HottestPart01 = 0.97;
        for (int t = 0; t < 60; t++) Systems.Update(ref f, hot);
        Check("a part near its limit starts a fire", f.Fire, f.FireIntensity.ToString("F3"));

        // The bottle puts it out, and is a one-shot.
        Check("suppressant works", Systems.SuppressFire(ref f), "");
        Check("suppressant is spent by half", f.Suppressant < 0.6, f.Suppressant.ToString("F2"));
        // Refuses on a cabin with no fire - a red dash then means "nothing to fight", not "broken".
        SystemsState calm = SystemsState.Fresh();
        Check("suppress refuses with no fire", !Systems.SuppressFire(ref calm), "");
        Check("depress refuses with no leak", !Systems.DepressResponse(ref calm), "");

        // Overstress opens a leak; isolating closes it, and not instantly.
        SystemsState lk = SystemsState.Fresh();
        SystemsInputs hard = Quiet(1.0); hard.GForce = 14.0;
        Systems.Update(ref lk, hard);
        Check("overstress springs a leak", lk.Leaking, lk.LeakRate.ToString("F3"));
        Check("depress response takes", Systems.DepressResponse(ref lk), "");
        Systems.Update(ref lk, Quiet(1.0));
        Check("leak is still closing after a second", lk.Leaking, lk.LeakRate.ToString("F3"));
        for (int t = 0; t < 70; t++) Systems.Update(ref lk, Quiet(1.0));
        Check("leak closes within a minute", !lk.Leaking, lk.LeakRate.ToString("F3"));

        // ---- STRINGS ----
        // Buses now start OFF (the flight-computer power gate), so the sim's own string behaviour is
        // only exercised with the bus powered - which is the only state in which it means anything.
        SystemsState p = SystemsState.Fresh(); p.Bus1On = true;
        SystemsInputs flat = Quiet(1.0); flat.Charge01 = 0.05;
        Systems.Update(ref p, flat);
        Check("undervoltage trips the C strings",
              Systems.Get(p, 1, 2) == StringState.Tripped, Systems.Get(p, 1, 2).ToString());
        Check("a tripped string will not toggle", !Systems.ToggleString(ref p, 1, 2), "");
        Check("reset refuses on a sick bus", !Systems.ResetBus(ref p, 1, 0.05), "");
        Check("reset takes once charge recovers", Systems.ResetBus(ref p, 1, 0.9), "");
        Check("string is back", Systems.Get(p, 1, 2) == StringState.Online,
              Systems.Get(p, 1, 2).ToString());

        // A string the CREW isolated must never be quietly re-closed by the model.
        SystemsState iso = SystemsState.Fresh(); iso.Bus2On = true;
        Systems.ToggleString(ref iso, 2, 0);
        Check("crew isolation holds", Systems.Get(iso, 2, 0) == StringState.Isolated, "");
        for (int t = 0; t < 100; t++) Systems.Update(ref iso, Quiet(1.0));
        Check("model does not re-close it",
              Systems.Get(iso, 2, 0) == StringState.Isolated, Systems.Get(iso, 2, 0).ToString());
        Check("online count reflects it", Systems.OnlineCount(iso, 2) == 2,
              Systems.OnlineCount(iso, 2).ToString());

        // ---- DETERMINISTIC ----
        // Two vehicles in the same state must give the same answer, or three screens disagree and
        // the whole thing is a random number with extra steps.
        SystemsState a1 = SystemsState.Fresh(), a2 = SystemsState.Fresh();
        for (int t = 0; t < 50; t++) { Systems.Update(ref a1, hot); Systems.Update(ref a2, hot); }
        Check("model is deterministic", a1.FireIntensity == a2.FireIntensity,
              a1.FireIntensity + " vs " + a2.FireIntensity);
    }
}
