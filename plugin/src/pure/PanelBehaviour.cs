/*
 * DragonScreen - PanelBehaviour
 *
 * PURE. What a console button press MEANS: whether the control is inert, whether it clicks, and
 * which way its indicator dash goes. The map (`PanelMap`) says which transform is which control;
 * this says what happens when a crew member pushes it.
 *
 * ---- WHY THIS IS PURE AND NOT IN PanelButtons ----
 * The policy used to live inside `PanelButton.OnMouseDown`, which is a MonoBehaviour: it needed a
 * running game and a mouse to exercise, so the one part of the panel that is a decision rather than
 * a wire was the one part no test could reach. It is a small state machine and this project has
 * already learned what an untested small state machine costs. Now the glue holds the collider, the
 * material and the clock, and every DECISION is taken here - where the headless test and the PNG
 * preview run the same code the capsule runs.
 *
 * ---- THE TWO OWNER DECISIONS THIS FILE ENFORCES (BUILD_PLAN §14.4 a + b, 2026-09-02) ----
 *
 * (a) LIGHTING - BRIGHT, AND NO RED.
 *     A button lights BRIGHT when it is active, armed or fired, and is otherwise unlit. There is
 *     NO red state. The old "refused -> red dash" was our own invention from 2026-08-06, and no
 *     source shows a red button on this console, so it is gone. What a press that cannot act now
 *     does is: CLICK, no light, no action. That is deliberately quiet - an honest no-op that does
 *     not dress itself up as a fault.
 *
 * (b) INERT UNTIL VERIFIED.
 *     SWAP 1/2/3 and the three entry-mode toggles (ENABLE ENTRY REBOOT / ENABLE BACKUP ENTRY /
 *     ENABLE NORMAL ENTRY) are the controls whose FUNCTION is inferred rather than sourced (§4).
 *     They are modelled, they are pressable, and they do nothing at all until a real console
 *     procedure verifies what they are for. Inventing a behaviour and lighting a lamp for it would
 *     make the panel claim knowledge the project does not have.
 *
 *     The confirmed neighbours on the same plates - ENABLE BACKUP PYROS, FIRE PYRD - are NOT inert.
 *     Being unverified is a property of the control, not of the plate it sits on.
 *
 * ---- ⛔ AND WHY `IsInert` IS BACK, AFTER BEING DELETED ----
 * `PanelMap.IsInert` was deleted on 2026-08-12 for a good reason: it was a STALE list. It named
 * DEPRESS RESPONSE, SUPPRESS FIRE and FIRE RESPONSE as having nothing behind them, and kept saying
 * so for weeks after `VehicleSystems` had modelled all three - three working controls held
 * unreachable by a list that had outlived its facts.
 *
 * This list is the opposite case and must not be confused with it. Those three were inert because
 * the SIMULATION was missing, so building the simulation made the list wrong. These six are inert
 * because the SOURCE is missing - nobody outside SpaceX knows what SWAP 2 does - and no amount of
 * code here can change that. It ends when a real procedure turns up, and the fix then is to delete
 * the entry, not to guess.
 */
namespace DragonScreen
{
    /// <summary>
    /// What a press did, in the terms the dash cares about. The glue turns this into a colour and a
    /// duration; the preview turns it into a PNG; the test asserts on it directly.
    /// </summary>
    public enum PanelPressKind : byte
    {
        /// <summary>§14.4(b): modelled but unverified. Clicks, does nothing, stays dark.</summary>
        Inert = 0,
        /// <summary>Nothing to do, or nothing behind it yet. Clicks, does nothing, stays dark.</summary>
        Nothing,
        /// <summary>It acted. Bright, briefly.</summary>
        Momentary,
        /// <summary>Armed and waiting on EXECUTE. Bright, and stays bright.</summary>
        Armed,
        /// <summary>An arming was cleared. Bright briefly, and every armed lamp goes out.</summary>
        Disarmed,
        /// <summary>A mode came on. Bright, and stays bright while it is on.</summary>
        ModeOn,
        /// <summary>A mode went off. Dark.</summary>
        ModeOff
    }

    public static class PanelPolicy
    {
        /// <summary>
        /// §14.4(b) - the controls whose function is INFERRED, not sourced. They click and do
        /// nothing. Adding to this list needs no evidence; REMOVING from it needs a real console
        /// procedure, which is the whole point of it.
        /// </summary>
        public static bool IsInert(PanelCommand c)
        {
            return c == PanelCommand.SwapString1
                || c == PanelCommand.SwapString2
                || c == PanelCommand.SwapString3
                || c == PanelCommand.EnableEntryReboot
                || c == PanelCommand.EnableBackupEntry
                || c == PanelCommand.EnableNormalEntry;
        }

        /// <summary>
        /// §14.4(a) - every mechanical press on this panel is audible, including the ones that do
        /// nothing. A refused or inert press with no click and no light is indistinguishable from a
        /// collider that missed, and the crew would press it again.
        /// </summary>
        public static bool Clicks(PanelCommand c)
        {
            return c != PanelCommand.None;
        }

        /// <summary>
        /// Modes stay lit while they are on, rather than flashing and going out. Note that the three
        /// inert entry-mode toggles are NOT modes any more - they light nothing at all.
        /// </summary>
        public static bool IsMode(PanelCommand c)
        {
            if (IsInert(c)) return false;
            return c == PanelCommand.EnableBackupPyros || IsLiveMode(c);
        }

        /// <summary>
        /// Modes whose "on" state is set by something OTHER than this button - the bus and
        /// flight-computer lamps, which must go dark on their own when the state behind them
        /// changes, and must light when it is changed from the touchscreen instead. Re-read every
        /// tick by the glue rather than latched at the press.
        /// </summary>
        public static bool IsLiveMode(PanelCommand c)
        {
            return c == PanelCommand.Power1        // lit while its bus is powered
                || c == PanelCommand.Power2
                || c == PanelCommand.String1A      // lit while its phase is engaged (row 1)
                || c == PanelCommand.String1B
                || c == PanelCommand.String1C;
        }

        /// <summary>
        /// The dash colour for an outcome. THE ONLY TWO ANSWERS ARE BRIGHT AND DARK - there is no
        /// third one, which is §14.4(a) expressed as a function rather than a comment.
        /// </summary>
        public static PanelLight LampFor(PanelPressKind k)
        {
            switch (k)
            {
                case PanelPressKind.Momentary:
                case PanelPressKind.Armed:
                case PanelPressKind.Disarmed:
                case PanelPressKind.ModeOn:
                    return PanelLight.Lit;
                default:
                    return PanelLight.Dark;       // Inert, Nothing, ModeOff
            }
        }

        /// <summary>True while the lamp holds rather than flashing out.</summary>
        public static bool Latches(PanelPressKind k)
        {
            return k == PanelPressKind.Armed || k == PanelPressKind.ModeOn;
        }

        /// <summary>
        /// True when this interlock result must put out the ARMED lamp on both emergency plates.
        ///
        /// Firing counts as well as cancelling, and that is the case worth stating: EXECUTE consumes
        /// the arming, so a DEORBIT NOW lamp left burning after its own EXECUTE would tell the crew
        /// something is still armed when nothing is. It is the same lamp-clearing job either way,
        /// which is why one predicate owns both and the glue does not re-derive it.
        /// </summary>
        public static bool ClearsArmedLamps(PressResult r)
        {
            return r == PressResult.Fire || r == PressResult.Cancelled;
        }

        /// <summary>
        /// CANCEL. Two jobs: the interlock's own result, plus whether a running sequence was
        /// stopped. With nothing armed and nothing running it stays dark - the careful press is
        /// never punished, which was true before the red state went away and is still true.
        /// </summary>
        public static PanelPressKind ResolveCancel(PressResult r, bool sequenceStopped)
        {
            if (r == PressResult.Cancelled) return PanelPressKind.Disarmed;
            return sequenceStopped ? PanelPressKind.Momentary : PanelPressKind.Nothing;
        }

        /// <summary>
        /// EXECUTE and the three commands that arm for it.
        ///
        /// <paramref name="acted"/> is only consulted on a FIRE - it is what the dispatcher said
        /// when the released command was carried out. A refusal there is `Nothing`, not red: with no
        /// flight software behind DEORBIT NOW, EXECUTE releasing it is an honest click into silence.
        /// </summary>
        public static PanelPressKind ResolveInterlock(PressResult r, bool acted)
        {
            switch (r)
            {
                case PressResult.Armed:     return PanelPressKind.Armed;
                case PressResult.Cancelled: return PanelPressKind.Disarmed;
                case PressResult.Fire:      return acted ? PanelPressKind.Momentary
                                                         : PanelPressKind.Nothing;
                // EXECUTE with nothing armed. It is still the press that means "I believe something
                // is armed" when nothing is, and the interlock still refuses it - but the panel no
                // longer answers in a colour it was never shown to have.
                default:                    return PanelPressKind.Nothing;   // Refused, Ignored
            }
        }

        /// <summary>
        /// Everything that acts immediately.
        ///
        /// The inert check comes FIRST and short-circuits: an inert control's dispatcher is never
        /// called at all, so there is no path by which one could act by accident.
        /// </summary>
        public static PanelPressKind ResolveImmediate(PanelCommand c, bool acted, bool modeOn)
        {
            if (IsInert(c)) return PanelPressKind.Inert;
            if (!acted) return PanelPressKind.Nothing;
            if (IsMode(c)) return modeOn ? PanelPressKind.ModeOn : PanelPressKind.ModeOff;
            return PanelPressKind.Momentary;
        }
    }

    /// <summary>
    /// The whole console's worth of dashes, headless.
    ///
    /// The capsule has 38 buttons on six plates and one shared interlock, and the interesting
    /// question - "after this sequence of presses, what is lit?" - spans all of them: arming on the
    /// left plate lights a lamp that EXECUTE on the RIGHT plate must put out. That is a board-level
    /// property, so it gets a board-level model, which the test asserts on and the preview draws.
    ///
    /// It does NOT decide whether a command acted. The host supplies that, because "did the bus
    /// come on" belongs to the dispatcher and inventing an answer here would make the preview a
    /// picture of a machine we do not have.
    /// </summary>
    public sealed class PanelBoard
    {
        private readonly PanelLight[] lamps;
        private readonly bool[] latched;

        /// <summary>Shared by both emergency plates - they are one control set, see PanelMap.</summary>
        public readonly Interlock Lock = new Interlock();

        /// <summary>True if the last press was audible. Every press is - see PanelPolicy.Clicks.</summary>
        public bool LastClicked { get; private set; }

        /// <summary>What the last press came to.</summary>
        public PanelPressKind LastKind { get; private set; }

        public PanelBoard()
        {
            int n = PanelMap.All.Length;
            lamps = new PanelLight[n];
            latched = new bool[n];
            LastKind = PanelPressKind.Nothing;
        }

        public int Count { get { return lamps.Length; } }
        public PanelLight Lamp(int i) { return (i >= 0 && i < lamps.Length) ? lamps[i] : PanelLight.Dark; }

        /// <summary>The first lamp carrying this command. Both emergency plates carry each of theirs.</summary>
        public PanelLight LampOf(PanelCommand c)
        {
            PanelEntry[] all = PanelMap.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Command == c) return lamps[i];
            return PanelLight.Dark;
        }

        /// <summary>True if ANY dash on the board is lit.</summary>
        public bool AnyLit()
        {
            for (int i = 0; i < lamps.Length; i++) if (lamps[i] != PanelLight.Dark) return true;
            return false;
        }

        /// <summary>Index of a command's button on a given plate, or -1.</summary>
        public static int IndexOf(string plate, PanelCommand c)
        {
            PanelEntry[] all = PanelMap.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Command == c && (plate == null || all[i].Plate == plate)) return i;
            return -1;
        }

        /// <summary>
        /// Press the button at <paramref name="index"/>.
        ///
        /// <paramref name="acted"/> - did the dispatcher carry it out. Ignored for an inert control,
        /// which is never dispatched.
        /// <paramref name="modeOn"/> - for a mode button, its state AFTER the press.
        /// <paramref name="sequenceStopped"/> - CANCEL only: was something running that it stopped.
        /// </summary>
        public PanelPressKind Press(int index, bool acted, bool modeOn, bool sequenceStopped)
        {
            if (index < 0 || index >= lamps.Length) return PanelPressKind.Nothing;

            PanelCommand c = PanelMap.All[index].Command;
            LastClicked = PanelPolicy.Clicks(c);

            PanelPressKind k;
            if (c == PanelCommand.Cancel)
            {
                PressResult r = Lock.Press(c);
                if (PanelPolicy.ClearsArmedLamps(r)) ClearArmedLamps();
                k = PanelPolicy.ResolveCancel(r, sequenceStopped);
            }
            else if (c == PanelCommand.Execute || PanelMap.NeedsExecute(c))
            {
                PressResult r = Lock.Press(c);
                // Cleared BEFORE this button's own lamp is set, so EXECUTE lighting itself cannot be
                // undone by the sweep that puts the arming out.
                if (PanelPolicy.ClearsArmedLamps(r)) ClearArmedLamps();
                k = PanelPolicy.ResolveInterlock(r, acted);
            }
            else
            {
                k = PanelPolicy.ResolveImmediate(c, acted, modeOn);
            }

            // ---- A PRESS THAT DID NOTHING CHANGES NOTHING, INCLUDING THE LAMP ----
            // Not even to dark. `Inert` and `Nothing` leave the dash exactly as they found it, which
            // matters on the buttons whose lamp is driven from somewhere else: pressing an unpowered
            // STRING must not black out the row lamp that is tracking a live phase. It is also what
            // the glue does - `PanelButton.Show` simply falls through on those two - and a model
            // that disagreed with the shipped code would make the preview a picture of a machine we
            // do not have.
            if (k != PanelPressKind.Inert && k != PanelPressKind.Nothing)
                Set(index, PanelPolicy.LampFor(k), PanelPolicy.Latches(k));

            LastKind = k;
            return k;
        }

        /// <summary>Convenience for the test and the preview: press this command on this plate.</summary>
        public PanelPressKind Press(string plate, PanelCommand c, bool acted, bool modeOn)
        {
            return Press(IndexOf(plate, c), acted, modeOn, false);
        }

        /// <summary>
        /// Drop a momentary flash, the way the glue does when its timer runs out. Latched lamps -
        /// an arming, a mode that is on - are left alone.
        /// </summary>
        public void FlashesOut()
        {
            for (int i = 0; i < lamps.Length; i++)
                if (!latched[i]) lamps[i] = PanelLight.Dark;
        }

        /// <summary>Set a live-mode lamp from the state behind it, as the glue's Update does.</summary>
        public void SetModeLamp(PanelCommand c, bool on)
        {
            PanelEntry[] all = PanelMap.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Command == c) Set(i, on ? PanelLight.Lit : PanelLight.Dark, on);
        }

        private void ClearArmedLamps()
        {
            PanelEntry[] all = PanelMap.All;
            for (int i = 0; i < all.Length; i++)
                if (latched[i] && PanelMap.NeedsExecute(all[i].Command)) Set(i, PanelLight.Dark, false);
        }

        private void Set(int i, PanelLight l, bool hold)
        {
            lamps[i] = l;
            latched[i] = hold && l != PanelLight.Dark;
        }
    }
}
