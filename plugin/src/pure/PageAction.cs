/*
 * DragonScreen - PageAction
 *
 * PURE. What a touch on a PAGE means. The chrome bar already had this - ChromeBar.HitTest returns a
 * page index - and this is the same idea for everything inside a page.
 *
 * ---- WHY AN ACTION AND NOT A CALLBACK ----
 * A delegate per control would put behaviour in src/pure, which cannot reference KSP, so every
 * control would end up calling back out through an interface the glue implements - more machinery
 * than the whole feature. Returning a VALUE keeps the hit-testing pure and testable, and leaves the
 * glue with one switch that does the KSP-side work. It also means a headless test can press every
 * button on every page and assert what came back, which is exactly the coverage the buttons need.
 *
 * ---- ONE RECT FUNCTION PER CONTROL, SHARED BY DRAWING AND HITTING ----
 * The rule that ChromeBar.LinkRect exists to enforce applies to every control here: the rectangle
 * that is drawn and the rectangle that is hit must come from the same call. NavPage and SettingsPage
 * both do their layout through a *Rect function that Build and HitTest each call.
 */
namespace DragonScreen
{
    public enum PageAct : byte
    {
        None = 0,

        // ---- NAV ----
        NavPanLeft, NavPanRight, NavPanUp, NavPanDown,
        NavZoomIn, NavZoomOut,
        /// <summary>Re-centre on the vessel and resume following.</summary>
        NavCentre,
        /// <summary>Cycle map / orbit, as the demo's NEXT VIEW does.</summary>
        NavNextView,

        // ---- SETTINGS ----
        /// <summary>Cabin and exterior lights - the vessel's Light action group.</summary>
        ToggleLights,
        /// <summary>Screen brightness, one step. Applies to all three displays.</summary>
        BrightUp, BrightDown,
        /// <summary>
        /// Put a page on a screen. Arg encodes BOTH: screen index * 8 + page index. Packed rather
        /// than carried in two fields because every other action needs none, and a second field that
        /// is meaningless nine times out of ten invites being read when it is not set.
        /// </summary>
        SetScreenPage,
        /// <summary>Write the render target to a PNG. Developer tooling, deliberately reachable.</summary>
        Capture,
        /// <summary>
        /// Toggle AUTO BOOSTER RECOVERY (MissionConductor.AutoRecoverBooster). OFF = the full crew mission flies
        /// the Dragon to orbit (the booster is not recovered). ON = after MECO the conductor hands focus to the
        /// separated booster and lands it — a focused booster-recovery test that sacrifices the Dragon's orbit
        /// that run (stock KSP flies one active vessel). Lets us collect booster-phase data on demand.
        /// </summary>
        ToggleBoosterRecovery,

        // ---- FLIGHT ----
        /// <summary>
        /// Tick a crew step on the sequence list. Arg is the StepId.
        ///
        /// The ONLY control on FLIGHT, and there is deliberately no launch action beside it: the
        /// Launch Director commands the countdown and the ground calls the abort-mode switches. The
        /// crew perform checks and report them, which is exactly what this is.
        /// </summary>
        AckStep,

        /// <summary>
        /// Engage or disengage the ascent autopilot.
        ///
        /// NOT a launch button - it does not light the rocket, and the countdown is still not the
        /// crew's. It is the AUTO/MANUAL authority the real vehicle genuinely gives them: Dragon
        /// flies itself and the crew can take it or hand it back.
        /// </summary>
        ToggleAuto,

        /// <summary>
        /// UNDOCK: release the docking hooks when the crew is ready (user 2026-08-28 — RENDEZVOUS + AUTO-DOCK
        /// buttons removed; AUTO SEQUENCE flies rendezvous + docking). It does NOT deorbit or back away — the
        /// return leg (careful KOS-safe backaway → deorbit → entry → splashdown) is flown by pressing AUTO
        /// SEQUENCE again, which detects we have already docked this mission and RESUMES at departure, so it
        /// never tries to dock again and it eases out of the ISS keep-out zone without hitting anything.
        /// </summary>
        Undock,
        /// <summary>
        /// Look through the eyes of the crew member in this seat. Arg is the IVA seat index.
        ///
        /// A REAL action, which is why the crew card has one at all. KSP has no cabin audio to bind
        /// per-seat volume to, and this project does not build controls that move nothing - but
        /// moving the IVA camera to a specific seat is something the game genuinely does, it is
        /// useful in a four-seat capsule where two displays are a stretch away, and it is exactly
        /// what a crew-position control on a real panel would be for.
        /// </summary>
        ViewFromSeat,
        /// <summary>Switch a page's subview tab. Arg is the tab index. See Card.</summary>
        SetSubview,
        /// <summary>Point the live camera. Arg is 0 Front, 1 Rear, 2 Left, 3 Right.</summary>
        SetCamera
    }

    /// <summary>
    /// The bits of PAGE state a touch sets, that the PAINTER owns (T14).
    ///
    /// ---- WHY THESE LIVE WITH THE SCREEN AND NOT WITH THE VESSEL ----
    /// PageState is what the VEHICLE is doing; this is what the crew member at THIS screen has chosen to
    /// look at. Which of FUNCTIONS / ALERTS a subsystem page is showing, and how big a docking nudge the
    /// cluster is set to, are the same kind of thing as the Cover's camera view and the suit countdown -
    /// per screen, three screens, and none of it is the vessel's business. Keeping them out of PageState
    /// also keeps them out of VesselData, which would otherwise have to invent a value for each of them
    /// every tick for state it does not own.
    ///
    /// Not persisted, for the reason the Cover's camera is not: it is where you happen to be looking, not
    /// a decision worth restoring across a load.
    /// </summary>
    public struct PageControls
    {
        /// <summary>VehicleSubsystemPage: showing ALERTS (true) rather than FUNCTIONS. T5 drew the
        /// toggle and left it inert; the tap is T14's.</summary>
        public bool Alerts;

        /// <summary>DockingSimPage: the ROTATION cluster's magnitude toggle - LARGE (true) or
        /// PRECISE.</summary>
        public bool DockRotLarge;

        /// <summary>DockingSimPage: the TRANSLATION cluster's magnitude toggle.</summary>
        public bool DockTransLarge;

        /// <summary>What a screen opens on: FUNCTIONS, and both clusters on LARGE - the states every
        /// page already drew before the toggles could be tapped, so nothing moved under anyone.</summary>
        public static PageControls Default
        {
            get
            {
                PageControls c;
                c.Alerts = false;
                c.DockRotLarge = true;
                c.DockTransLarge = true;
                return c;
            }
        }
    }

    /// <summary>What a touch resolved to. Act = None means the touch hit nothing.</summary>
    public struct PageHit
    {
        public PageAct Act;
        public int Arg;

        public static PageHit Of(PageAct a) { PageHit h; h.Act = a; h.Arg = 0; return h; }
        public static PageHit Of(PageAct a, int arg) { PageHit h; h.Act = a; h.Arg = arg; return h; }
        public static PageHit None { get { return Of(PageAct.None); } }

        /// <summary>Pack a screen/page pair into Arg. 8 because there will never be 8 pages.</summary>
        public static int PackScreenPage(int screen, int page) { return screen * 8 + page; }
        public static void UnpackScreenPage(int arg, out int screen, out int page)
        {
            screen = arg / 8;
            page = arg % 8;
        }
    }

    /// <summary>
    /// A touchable control drawn as a rounded plate with a label. Shared by NAV and SETTINGS so the
    /// two pages cannot drift into two different-looking buttons.
    ///
    /// "Rounded" is aspirational: there is no rounded-rect primitive and adding one would mean arc
    /// geometry per corner on both renderers for a cosmetic gain. A plate with a hairline reads
    /// correctly at IVA distance, which is the bar that matters.
    /// </summary>
    public static class Control
    {
        /// <summary>Commands one button emits, for display-list sizing.</summary>
        public const int Commands = 3;

        public static bool Hit(float px, float py, float x, float y, float w, float h)
        {
            return px >= x && px < x + w && py >= y && py < y + h;
        }

        /// <summary>
        /// A button. <paramref name="on"/> lights it as an ACTIVE state, not as a press - nothing
        /// here knows about presses, and a control that looked pressed for one frame at 5 Hz would
        /// mostly be missed anyway.
        /// </summary>
        public static void Button(DisplayList dl, float x, float y, float w, float h,
                                  string label, bool on, bool enabled)
        {
            Rgba face = on ? DragonPalette.Accent : DragonPalette.Panel;
            Rgba edge = enabled ? DragonPalette.Hairline : DragonPalette.Inset2;
            Rgba text = on ? DragonPalette.Background
                      : enabled ? DragonPalette.Text2 : DragonPalette.Text7;

            dl.Rect(x, y, w, h, face);
            dl.Box(x, y, w, h, 2f, edge);
            // Vertically centred by eye on the cap height rather than the line box: Typography.Caption
            // is 16 px and the glyphs occupy roughly the top 12 of it in both renderers.
            dl.Text(label, x + w * 0.5f, y + (h - Typography.Caption) * 0.5f - 1f,
                    Typography.Caption, TextAlign.Centre, text);
        }
    }
}
