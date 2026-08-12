/*
 * DragonScreen - PanelMap
 *
 * PURE. The console's ~39 physical buttons: which transform is which control, and the arm/execute
 * interlock that governs the two emergency plates.
 *
 * ---- THE MAP IS TRANSCRIBED, NOT GUESSED ----
 * Every row below comes from docs/REAL_DRAGON_SCREENS.md, which was built from a transform dump taken
 * in game on 2026-08-05 plus labels read off the rendered model. Two earlier hypotheses about the
 * naming were both wrong; this is the one that matched every count, including the two blank filler
 * plates and FIRE PYRD sitting apart from its row. DO NOT "tidy" a name here without re-dumping.
 *
 *      TE_CD2_PROP_BUTTON_1  LEFT emergencies      TE_CD2_PROP_BUTTON_6  RIGHT emergencies (identical)
 *      TE_CD2_PROP_BUTTON_2  power + strings       TE_CD2_PROP_BUTTON_8  ABORT handle
 *      TE_CD2_PROP_BUTTON_3  chutes + pyros        TE_CD2_PROP_BUTTON_4  entry mode
 *
 * Within a plate, BUT1..BUT5 are the TOP row left to right and BUT6..BUT10 the BOTTOM row.
 *
 * `SURPRESS FIRE` is spelled that way IN THE MODEL. Not corrected here - matching the installed art
 * matters more, and it is Tundra's typo to fix.
 *
 * ---- WHY AN INTERLOCK, AND WHY ONLY ON THOSE TWO PLATES ----
 * CANCEL and EXECUTE bracket the emergency row on the real capsule. That is the whole reason they are
 * there: an emergency action is ARMED by its own button, then committed by EXECUTE, and CANCEL clears
 * it. Copying the buttons without the sequence would be copying the picture and not the machine.
 *
 * The chute/pyro and entry plates carry no CANCEL and no EXECUTE, so they act immediately. That is not
 * an inconsistency in the design, it is the design: those are already guarded controls.
 *
 * ---- LEFT AND RIGHT EMERGENCY PLATES ARE ONE CONTROL SET ----
 * Both copies exist so either seat can reach one. Arming on the left and executing on the right is
 * therefore CORRECT and must work - they share a single interlock, not one each.
 */
namespace DragonScreen
{
    /// <summary>Every control on the lower console. `None` means modelled but deliberately inert.</summary>
    public enum PanelCommand
    {
        None = 0,

        // Emergency plates - these go through the interlock.
        Cancel, WaterDeorbit, DeorbitNow, Breakout, Execute,
        DepressResponse, SuppressFire, FireResponse,

        // Power and strings.
        Power1, String1A, String1B, String1C, Reset1,
        Power2, String2A, String2B, String2C, Reset2,

        // Chutes and pyros - immediate.
        EnableBackupPyros, JettisonNoseCone, MainsOnly, DroguesAndMains,
        EnableEntryReboot, CutMains, FirePyro,

        // Entry mode - immediate.
        EnableBackupEntry, SwapString1, SwapString2, SwapString3, EnableNormalEntry,

        // The handle.
        Abort
    }

    /// <summary>What a button's indicator dash is showing.</summary>
    public enum PanelLight
    {
        /// <summary>As modelled - the unlit grey dash.</summary>
        Dark = 0,
        /// <summary>Pressed, armed, or the mode currently selected.</summary>
        Lit,
        /// <summary>The press was refused, or the action could not be carried out.</summary>
        Failed
    }

    public struct PanelEntry
    {
        public string Plate;
        public string Button;
        public string Label;
        public PanelCommand Command;

        public PanelEntry(string plate, string button, string label, PanelCommand cmd)
        {
            Plate = plate; Button = button; Label = label; Command = cmd;
        }
    }

    public static class PanelMap
    {
        public const string PlateLeftEmerg = "TE_CD2_PROP_BUTTON_1";
        public const string PlatePower = "TE_CD2_PROP_BUTTON_2";
        public const string PlateChutes = "TE_CD2_PROP_BUTTON_3";
        public const string PlateEntry = "TE_CD2_PROP_BUTTON_4";
        public const string PlateAbort = "TE_CD2_PROP_BUTTON_8";
        public const string PlateRightEmerg = "TE_CD2_PROP_BUTTON_6";

        /// <summary>The abort handle is pulled and twisted, not pressed - see Abort() in FlightCommands.</summary>
        public const string AbortHandle = "CD2_ABORT_HANDLE";

        private static PanelEntry[] table;

        public static PanelEntry[] All
        {
            get
            {
                if (table == null) table = Build();
                return table;
            }
        }

        private static PanelEntry[] Build()
        {
            System.Collections.Generic.List<PanelEntry> e =
                new System.Collections.Generic.List<PanelEntry>();

            // ---- the two emergency plates, identical ----
            string[] emerg = { PlateLeftEmerg, PlateRightEmerg };
            for (int i = 0; i < emerg.Length; i++)
            {
                string p = emerg[i];
                e.Add(new PanelEntry(p, "BUT1", "CANCEL", PanelCommand.Cancel));
                e.Add(new PanelEntry(p, "BUT2", "WATER DEORBIT", PanelCommand.WaterDeorbit));
                e.Add(new PanelEntry(p, "BUT3", "DEORBIT NOW", PanelCommand.DeorbitNow));
                e.Add(new PanelEntry(p, "BUT4", "BREAKOUT", PanelCommand.Breakout));
                e.Add(new PanelEntry(p, "BUT5", "EXECUTE", PanelCommand.Execute));
                // Cabin emergencies. Stock KSP has no depressurisation and no fire, so these are
                // mapped, lit on press, and do nothing. Inert beats invented - the alternative is a
                // control that claims to fight a fire that cannot happen.
                e.Add(new PanelEntry(p, "BUT7", "DEPRESS RESPONSE", PanelCommand.DepressResponse));
                e.Add(new PanelEntry(p, "BUT8", "SURPRESS FIRE", PanelCommand.SuppressFire));
                e.Add(new PanelEntry(p, "BUT9", "FIRE RESPONSE", PanelCommand.FireResponse));
            }

            // ---- power and strings ----
            e.Add(new PanelEntry(PlatePower, "BUT1", "POWER 1", PanelCommand.Power1));
            e.Add(new PanelEntry(PlatePower, "BUT2", "STRING 1A", PanelCommand.String1A));
            e.Add(new PanelEntry(PlatePower, "BUT3", "STRING 1B", PanelCommand.String1B));
            e.Add(new PanelEntry(PlatePower, "BUT4", "STRING 1C", PanelCommand.String1C));
            e.Add(new PanelEntry(PlatePower, "BUT5", "RESET 1", PanelCommand.Reset1));
            e.Add(new PanelEntry(PlatePower, "BUT6", "POWER 2", PanelCommand.Power2));
            e.Add(new PanelEntry(PlatePower, "BUT7", "STRING 2A", PanelCommand.String2A));
            e.Add(new PanelEntry(PlatePower, "BUT8", "STRING 2B", PanelCommand.String2B));
            e.Add(new PanelEntry(PlatePower, "BUT9", "STRING 2C", PanelCommand.String2C));
            e.Add(new PanelEntry(PlatePower, "BUT10", "RESET 2", PanelCommand.Reset2));

            // ---- chutes and pyros ----
            e.Add(new PanelEntry(PlateChutes, "BUT1", "ENABLE BACKUP PYROS", PanelCommand.EnableBackupPyros));
            e.Add(new PanelEntry(PlateChutes, "BUT2", "JETTISON NOSE CONE", PanelCommand.JettisonNoseCone));
            e.Add(new PanelEntry(PlateChutes, "BUT3", "MAINS ONLY", PanelCommand.MainsOnly));
            e.Add(new PanelEntry(PlateChutes, "BUT4", "DROGUES & MAINS", PanelCommand.DroguesAndMains));
            e.Add(new PanelEntry(PlateChutes, "BUT6", "ENABLE ENTRY REBOOT", PanelCommand.EnableEntryReboot));
            e.Add(new PanelEntry(PlateChutes, "BUT7", "CUT MAINS", PanelCommand.CutMains));
            e.Add(new PanelEntry(PlateChutes, "BUT10", "FIRE PYRD", PanelCommand.FirePyro));

            // ---- entry mode: a single row sitting at the BOTTOM row's z ----
            e.Add(new PanelEntry(PlateEntry, "BUT6", "ENABLE BACKUP ENTRY", PanelCommand.EnableBackupEntry));
            e.Add(new PanelEntry(PlateEntry, "BUT7", "SWAP 1", PanelCommand.SwapString1));
            e.Add(new PanelEntry(PlateEntry, "BUT8", "SWAP 2", PanelCommand.SwapString2));
            e.Add(new PanelEntry(PlateEntry, "BUT9", "SWAP 3", PanelCommand.SwapString3));
            e.Add(new PanelEntry(PlateEntry, "BUT10", "ENABLE NORMAL ENTRY", PanelCommand.EnableNormalEntry));

            return e.ToArray();
        }

        /// <summary>True for the three commands that must be armed and then executed.</summary>
        public static bool NeedsExecute(PanelCommand c)
        {
            return c == PanelCommand.WaterDeorbit
                || c == PanelCommand.DeorbitNow
                || c == PanelCommand.Breakout;
        }

        // ⛔ `IsInert` DELETED 2026-08-12. It listed DepressResponse, SuppressFire and
        // FireResponse as having nothing behind them, which stopped being true the day
        // `VehicleSystems` modelled the cabin - but the list outlived the fact and kept three
        // working controls unreachable. If a control ever genuinely has nothing behind it, the
        // honest answer is not to add it back here: it is that `FlightCommands` returns false and
        // the dash goes red with the reason, which is the refusal path everything else uses.

    }

    /// <summary>What a press did, so the caller knows which way to light the dash.</summary>
    public enum PressResult
    {
        /// <summary>Nothing happened and nothing should light.</summary>
        Ignored,
        /// <summary>Command armed and waiting for EXECUTE.</summary>
        Armed,
        /// <summary>Armed command cleared.</summary>
        Cancelled,
        /// <summary>Carry this command out now.</summary>
        Fire,
        /// <summary>Refused - light the dash red.</summary>
        Refused
    }

    /// <summary>
    /// The arm/execute sequence for the emergency plates. PURE so the sequence itself is testable
    /// without a vessel: the rules are the part that has to be right, and they are the part a game
    /// restart is worst at checking.
    /// </summary>
    public class Interlock
    {
        /// <summary>The command waiting on EXECUTE, or None.</summary>
        public PanelCommand Armed { get; private set; }

        /// <summary>The command EXECUTE released, for the caller to carry out. Cleared on read.</summary>
        public PanelCommand Fired { get; private set; }

        public PressResult Press(PanelCommand c)
        {
            Fired = PanelCommand.None;

            if (c == PanelCommand.Cancel)
            {
                // CANCEL with nothing armed is not a failure - it is the safe no-op a crew member
                // makes when they are not sure. Refusing it would light a red dash for doing the
                // careful thing.
                if (Armed == PanelCommand.None) return PressResult.Ignored;
                Armed = PanelCommand.None;
                return PressResult.Cancelled;
            }

            if (c == PanelCommand.Execute)
            {
                // EXECUTE with nothing armed IS a failure, and a loud one. It is the press that means
                // "I believe something is armed" when nothing is.
                if (Armed == PanelCommand.None) return PressResult.Refused;
                Fired = Armed;
                Armed = PanelCommand.None;
                return PressResult.Fire;
            }

            if (!PanelMap.NeedsExecute(c)) return PressResult.Ignored;

            // Pressing the armed command again disarms it - the same button is the toggle. Pressing a
            // DIFFERENT one re-arms to that instead of refusing: the crew changed their mind, and
            // making them find CANCEL first would cost seconds in the one situation that has none.
            if (Armed == c)
            {
                Armed = PanelCommand.None;
                return PressResult.Cancelled;
            }

            Armed = c;
            return PressResult.Armed;
        }

        public void Clear()
        {
            Armed = PanelCommand.None;
            Fired = PanelCommand.None;
        }
    }
}
