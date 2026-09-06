// DragonScreen — CoverActs  (PURE: what the Cover's four action rows actually do)
// ============================================================================================
// [[S128]], 2026-09-06. `ActOnSpaceX`, `ActDeorbitBrief`, `ActReview` and `ActAcknowledge` have had
// named, measured hit rects since the Cover was built and **no dispatcher case at all** — not even the
// honest-refusal log the Manual Chute page emits. A crew member pressing any of the four got silence,
// which is the one outcome §14.4(a) is careful to avoid: it is indistinguishable from a dead panel.
//
// ---- ⭐ WHY THIS IS NOT §14.4(a)-BLOCKED, WHICH IS THE WHOLE REASON IT CAN LAND ----
// §14.4(a) holds FLIGHT ACTUATION to an honest no-op until Part B. **None of these four commands the
// vehicle.** Read one at a time, off their own baked labels:
//
//   `review_reference_content`              -> select rail slot 5. The destination already exists and
//                                              the label names it. A view change, nothing more.
//   `deorbit_burn_brief`                    -> open the Deorbit Burn Prep page (UiPage 29). Navigation.
//   `acknowledge`                           -> a CREW acknowledgement. A latch on this screen.
//   `on_spacex_on_begin_procedure_4_700`    -> "On SpaceX, on, begin procedure 4.700" is the GROUND
//                                              authorising the crew to proceed. Recording that the call
//                                              was received is a latch; it flies nothing.
//
// ---- ⛔ AND THE TYPE MAKES A FLIGHT COMMAND UNREPRESENTABLE ----
// `CoverAct` can say: select a phase, go to a page, set a latch, or do nothing. There is no field that
// can carry a command id, an actuator, or a `FlightCommands` call — so "none of these reaches
// FlightCommands" is not a property a test has to chase through the glue, it is a property of the
// shape. The test pins it anyway, because the shape can be changed and the reason it must not be is
// here rather than in whoever changes it.
//
// PURE: no KSP/Unity, no state of its own. The caller owns the latches; this only says what a press
// MEANS. That split is what lets the meaning be tested headlessly while the glue stays thin.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>What a Cover action row does. ⛔ Deliberately NOT extensible to a vehicle command —
    /// see the header. A new kind that actuates belongs in Part B's dispatch, not here.</summary>
    public enum CoverActKind : byte
    {
        /// <summary>The press means nothing here — every button that is not one of the four.</summary>
        None = 0,
        /// <summary>Select a rail phase in-page. `Phase` carries which.</summary>
        SelectPhase = 1,
        /// <summary>Navigate to another screen. `Page` carries which.</summary>
        GoPage = 2,
        /// <summary>Set a screen-local latch. `Latch` carries which.</summary>
        Latch = 3
    }

    /// <summary>The two latches the Cover's action rows can set. Both are RECORDS OF A HUMAN ACT —
    /// one by the crew, one relayed from the ground — and neither is a vehicle state.</summary>
    public enum CoverLatch : byte
    {
        None = 0,
        /// <summary>The crew pressed ACKNOWLEDGE on this screen.</summary>
        CrewAcknowledge = 1,
        /// <summary>"On SpaceX, on, begin procedure 4.700" — the ground's go, marked as received.
        /// ⚠ It records that the call came, not that anything was done about it.</summary>
        GroundAuthorised = 2
    }

    /// <summary>One resolved press.</summary>
    public struct CoverAct
    {
        public CoverActKind Kind;
        /// <summary>Meaningful when Kind == SelectPhase.</summary>
        public int Phase;
        /// <summary>Meaningful when Kind == GoPage.</summary>
        public UiPage Page;
        /// <summary>Meaningful when Kind == Latch.</summary>
        public CoverLatch Latch;

        public static CoverAct None
        { get { CoverAct a; a.Kind = CoverActKind.None; a.Phase = -1; a.Page = UiPage.Cover; a.Latch = CoverLatch.None; return a; } }

        public static CoverAct Phase_(int phase)
        { CoverAct a = None; a.Kind = CoverActKind.SelectPhase; a.Phase = phase; return a; }

        public static CoverAct Go(UiPage page)
        { CoverAct a = None; a.Kind = CoverActKind.GoPage; a.Page = page; return a; }

        public static CoverAct Set(CoverLatch latch)
        { CoverAct a = None; a.Kind = CoverActKind.Latch; a.Latch = latch; return a; }
    }

    public static class CoverActs
    {
        /// <summary>
        /// What pressing <paramref name="b"/> means. `None` for every button this module does not own —
        /// the rail, the camera cluster, the chrome and the entry rows all belong to their own handlers,
        /// and returning None here leaves them exactly as they were.
        ///
        /// ⚠ `ActReview` targets `CoverPage.ReferencePhase` rather than a literal 5, so the two cannot
        /// drift; that constant is what `Build` tests against to swap the panel body.
        /// </summary>
        public static CoverAct Of(CoverPage.CoverButton b)
        {
            switch (b)
            {
                case CoverPage.CoverButton.ActReview:
                    return CoverAct.Phase_(CoverPage.ReferencePhase);
                case CoverPage.CoverButton.ActDeorbitBrief:
                    return CoverAct.Go(UiPage.DeorbitBurnPrep);
                case CoverPage.CoverButton.ActAcknowledge:
                    return CoverAct.Set(CoverLatch.CrewAcknowledge);
                case CoverPage.CoverButton.ActOnSpaceX:
                    return CoverAct.Set(CoverLatch.GroundAuthorised);
                default:
                    return CoverAct.None;
            }
        }

        /// <summary>True for the four rows this module owns. ⭐ Written as a call to `Of` rather than a
        /// second list of four names, so the two can never disagree — the mistake QC `H-04` is about,
        /// one level up from geometry.</summary>
        public static bool IsAction(CoverPage.CoverButton b)
        { return Of(b).Kind != CoverActKind.None; }
    }
}
