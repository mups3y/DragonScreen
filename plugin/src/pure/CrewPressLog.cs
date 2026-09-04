/*
 * DragonScreen — CrewPressLog  (register S85; spec: docs/BLACKBOX_RESEARCH.md §2.7, §2.9)
 * =============================================================================================
 * PURE. The publish side of the CVR press channel: the screens APPEND what the crew pressed, and
 * whoever is listening DRAINS it. Nothing here knows a recorder exists.
 *
 * ---- WHY A BUFFER AND NOT A CALL (the constraint this shape exists to respect) ----
 * The BlackBox is EXCISABLE BY DESIGN (owner, 2026-09-03): deleting `src/pure/blackbox/`,
 * `src/BlackBoxRecorder.cs`, `test/BlackBoxTest.cs` and one line of `TestMain.cs` must leave the
 * build green. A `BlackBox.RecordPress(...)` at `ScreenPainter.TouchDown` would make that excision a
 * code EDIT in a screen file instead of a delete — the dependency arrow would point tree → BlackBox,
 * which is the one direction it may never point.
 *
 * A publish-side buffer is not a dependency. `ScreenPainter.cs` and `PanelButtons.cs` reference this
 * file and nothing else; this file references nothing but the screens' own pure types. Delete the
 * whole recorder and both choke points still compile, still run, and still append into a queue no
 * one reads — which is exactly what `ScreenPainter.livePage` already does for the page channel.
 *
 * ---- WHY NOT POLL, LIKE THE PAGE CHANNEL DOES ----
 * The recorder ALREADY polls for crew state: `crew.page_change` is emitted from `PageEdge`, watching
 * `PageState.ScreenPages` and touching no screen file. That works because a page SELECTION is a
 * state with an edge. A PRESS is not:
 *
 *   • a press that is REFUSED (TROUBLESHOOT with no failed suit) changes nothing — no edge;
 *   • a press on an INERT control (§14.4(b)) changes nothing — no edge;
 *   • a press re-selecting the page ALREADY SHOWN changes nothing — no edge;
 *   • two presses between two polls collapse into one edge, or none.
 *
 * And §2.9's `acted` field IS the acted-vs-not distinction, which by definition has no observable
 * edge on the "not" side. Polling cannot see any of it. That is the whole reason this file exists.
 *
 * ---- THE IDIOM IS `livePage`, IN QUEUE FORM ----
 * `ScreenPainter.livePage` (`:199`, published `:287-290`) is a `private static` array the screens
 * write and other code reads. There is NO `static event` and NO `Action<>` anywhere in `plugin/src`
 * — introducing the first one for this would be a new mechanism in a tree that already has the right
 * one. This is that mechanism with a queue instead of a slot, because a press is an EVENT (it must
 * not be overwritten by the next one) where a page is a STATE (the latest is the only truth).
 *
 * ---- NOTHING IS DROPPED SILENTLY ----
 * The buffer is fixed-size and never allocates. Presses are human-rate against a `FixedUpdate`
 * drain, so `Capacity` presses inside one 20 ms tick is not a thing a crew can do — but S76 is what
 * happens when a recorder loses data and says nothing, so overflow is COUNTED and the count goes
 * into the recording as a `crew.press_dropped` event. Not a log line: a log line is not evidence.
 *
 * On overflow the NEWEST press is refused, not the oldest evicted. Two reasons: what survives is
 * then a contiguous prefix rather than a window with an invisible hole in the middle, and the only
 * situation that can actually overflow is "nobody is draining", where the first presses after the
 * drain stopped are the interesting ones.
 *
 * ---- THREADING ----
 * Both choke points and the drain run on Unity's main thread (`OnMouseDown`, the touch pass, and
 * `FixedUpdate`), so no lock is taken and none is needed. Stated because a silent assumption about
 * threading is how a buffer like this eventually corrupts.
 * =============================================================================================
 */
namespace DragonScreen
{
    /// <summary>
    /// One crew interaction, as §2.9's `crew.*` payload — everything the CVR channel needs about a
    /// press, captured AT THE INSTANT OF THE PRESS and carried until someone drains it.
    ///
    /// Plain values only: no KSP types, no strings that have to be built later, no references into
    /// screen state that could change before the drain. What a press meant is decided at the choke
    /// point, where the answer is actually known.
    ///
    /// ⚠ The "absent" convention here is **-1**, not 0. Every `int` below has a real 0 that means
    /// something (`PanelPressKind.Inert`, `PanelLight.Dark`, `UiPage.Cover`, `Severity` nominal,
    /// alarm mask clear), so 0 as "not applicable" would read as a confident, wrong value — §4.6's
    /// blank-never-zero rule, in a struct.
    /// </summary>
    public struct CrewPress
    {
        /// <summary>Universal time at the press, or NaN if the caller could not read a clock. The
        /// drainer substitutes its own tick's UT when this is NaN (§2.9's sub-frame rule: an event
        /// carries the instant it happened, not the instant it was collected).</summary>
        public double Ut;

        /// <summary>Which surface produced this press.</summary>
        public CrewSurface Surface;

        /// <summary>The flat, stable id (`CrewControlIds`). Never null once appended — a touch that
        /// hit nothing carries `CrewControlIds.Miss` and `Surface = None`.</summary>
        public string ControlId;

        /// <summary>The surface-specific enum's ORDINAL (§2.7 carries it alongside the id), or -1
        /// where the surface has no enum. The id is the stable name; this is what the code actually
        /// branched on, so a recording stays decodable against the revision the manifest names.</summary>
        public int EnumValue;

        /// <summary>The painter index this press landed on — 1 LEFT, 2 CENTRE, 3 RIGHT, matching
        /// `ScreenPainter.Configure`. **-1 = the console plate**, which is not a screen.</summary>
        public int Screen;

        /// <summary>The `UiPage` int being shown when the press landed, or -1 off-glass.</summary>
        public int Page;

        /// <summary>Where the finger landed, in PAGE pixels, or NaN off-glass.</summary>
        public float Px, Py;

        /// <summary>The `PanelCommand` ordinal this press dispatched, or -1 if it dispatched none.
        /// The join key between the same command pressed on the plate and on the glass.</summary>
        public int Cmd;

        /// <summary>
        /// §2.9's honest verdict: **did this press do anything?**
        ///
        /// Where the press reaches `FlightCommands.Run`, this IS the dispatcher's bool return and
        /// nothing else is consulted. Where it does not reach the dispatcher, the same question is
        /// answered by the only thing that can — whether the state the surface owns changed. So a
        /// re-selection of the page already shown, a refused TROUBLESHOOT, an inert plate and a
        /// §14.4(a) no-op are all `false`, and all four are invisible to any poll.
        /// </summary>
        public bool Acted;

        /// <summary>`PanelPressKind` ordinal where the press was resolved by `PanelPolicy`, else -1.
        /// The finer verdict `Acted` deliberately does not carry: an ARM is `Acted` true with
        /// `PressKind` Armed, a refused EXECUTE is `Acted` false with `PressKind` Nothing.</summary>
        public int PressKind;

        /// <summary>`PanelLight` ordinal — the dash AS IT STANDS after the press, read back off the
        /// button. -1 on the glass, which has no lamp of its own (a page reads its lit state from
        /// `PageState` every frame, so there is nothing latched here to photograph).</summary>
        public int Lamp;

        /// <summary>§2.8's alarm channel at the instant of the press — the CVR's area-microphone
        /// context, so a press can be read against what was lit when it was made. -1 when the screen
        /// state was not valid.</summary>
        public int AlarmMask;

        /// <summary>`Alarms.Severity` ordinal for the system channel at that instant, or -1.</summary>
        public int SevSystem;
    }

    public static class CrewPressLog
    {
        /// <summary>
        /// Sized for the pathological case, not the expected one. At a `FixedUpdate` drain the
        /// expected depth is 0 or 1; 64 is what a crew would have to produce inside a single physics
        /// tick to lose anything, which is not physically possible. It is a fixed array so the touch
        /// path never allocates.
        /// </summary>
        public const int Capacity = 64;

        static readonly CrewPress[] buf = new CrewPress[Capacity];
        static int count;
        static int dropped;

        /// <summary>How deep the buffer is right now. Diagnostic; the drain is the real reader.</summary>
        public static int Count { get { return count; } }

        /// <summary>
        /// Presses this session that were never handed to anyone — CUMULATIVE and never reset by a
        /// drain, so a reader that misses one report still sees the total. Non-zero means the
        /// recording is incomplete, and it says so in the recording rather than in a log.
        /// </summary>
        public static int Dropped { get { return dropped; } }

        /// <summary>
        /// Record one interaction. Called from the two choke points and nowhere else.
        ///
        /// A press with a null `ControlId` is normalised to `CrewControlIds.Miss` rather than
        /// rejected: "the crew touched the glass and hit nothing" is a fact worth keeping (§2.9's
        /// `crew.touch`), and dropping it here would make a mis-aimed press indistinguishable from
        /// no press at all.
        /// </summary>
        public static void Append(CrewPress p)
        {
            if (p.ControlId == null) { p.ControlId = CrewControlIds.Miss; p.Surface = CrewSurface.None; }
            if (count >= Capacity) { dropped++; return; }
            buf[count++] = p;
        }

        /// <summary>
        /// Hand over everything buffered and empty the buffer. Returns how many entries were written
        /// into <paramref name="into"/>.
        ///
        /// ⚠ A destination shorter than `Capacity` cannot take a full buffer, and the remainder is
        /// COUNTED as dropped rather than left behind to be re-drained later — leaving it would
        /// reorder the log the next time round, and an out-of-order CVR is worse than a short one.
        /// The recorder sizes its destination at `Capacity`, so this never fires there; it is here
        /// because a silent truncation would be exactly the S76 failure one level down.
        /// </summary>
        public static int Drain(CrewPress[] into)
        {
            int n = count;
            if (into == null) { dropped += n; count = 0; return 0; }
            if (n > into.Length) { dropped += n - into.Length; n = into.Length; }
            for (int i = 0; i < n; i++) into[i] = buf[i];
            // Clear the slots we are giving up so a stale string cannot be held alive by the buffer
            // after the caller owns the copy.
            for (int i = 0; i < count; i++) buf[i] = default(CrewPress);
            count = 0;
            return n;
        }

        /// <summary>Empty the buffer AND the drop counter. For the headless suite only — a flight has
        /// no reason to forget how many presses it lost.</summary>
        public static void Reset()
        {
            for (int i = 0; i < count; i++) buf[i] = default(CrewPress);
            count = 0;
            dropped = 0;
        }

        /// <summary>
        /// A blank record with every "not applicable" field already at -1/NaN. Every caller starts
        /// here, so a field nobody filled in reads as absent rather than as a confident zero.
        /// </summary>
        public static CrewPress Blank()
        {
            CrewPress p = default(CrewPress);
            p.Ut = double.NaN;
            p.Surface = CrewSurface.None;
            p.ControlId = CrewControlIds.Miss;
            p.EnumValue = -1;
            p.Screen = -1;
            p.Page = -1;
            p.Px = float.NaN; p.Py = float.NaN;
            p.Cmd = -1;
            p.Acted = false;
            p.PressKind = -1;
            p.Lamp = -1;
            p.AlarmMask = -1;
            p.SevSystem = -1;
            return p;
        }
    }
}
