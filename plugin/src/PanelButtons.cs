/*
 * DragonScreen - PanelButtons
 *
 * GLUE. Makes the ~39 modelled console buttons into working controls: a collider each, a click
 * handler each, and the indicator dash driven from the result.
 *
 * ---- THE COLLIDER MECHANISM IS PROVEN, NOT ASSUMED ----
 * Settled in game 2026-08-05 (docs/REAL_DRAGON_SCREENS.md, "the probe experiment"):
 *
 *      Tundra ships ONE collider for the whole console and none per button.
 *      AddComponent<BoxCollider> auto-fits the mesh - CD2_PROP_BUT1 came out 0.0177 x 0.0024 x 0.0148.
 *      The collider inherits layer 16 from the button's own GameObject, so put it THERE, not on a
 *          new child, and there is no layer decision to make.
 *      A nested collider WINS the hit: probe B fired, probe A never did, because the console
 *          collider is a MeshCollider following the surface rather than a solid box.
 *      FreeIva is unaffected - every collider we add is inside a volume the player cannot enter.
 *
 * The buttons have real depth, so unlike the screens they need no minimum-thickness fudge.
 *
 * ---- LIGHTING: WHAT IS KNOWN, AND WHAT IS OURS ----
 * Researched 2026-08-06. **How the real capsule's buttons indicate state is not publicly documented.**
 * Press coverage describes three screens, ~38 manual buttons, most under clear guards as a third-line
 * backup behind the touchscreens and the ground, and a pull-then-twist EJECT handle. Nothing
 * describes illumination, and neither detailed public reconstruction covers the physical panel.
 *
 * What IS established is from the model itself: every button carries a small horizontal dash above
 * its label, and that dash is the panel's entire visual language for state.
 *
 * The scheme is therefore OURS, and since 2026-09-02 it is the owner's §14.4(a) decision rather than
 * the 2026-08-06 one this file used to record: unlit as modelled, BRIGHT when active, armed or fired,
 * and NO RED. The red "refused" dash was invented here and no source shows a red button on this
 * console, so it is gone - a press that cannot act CLICKS and leaves the dash dark. What we still do
 * NOT do is invent a glow, an outline or a halo: the dash Tundra already drew is the only thing driven.
 *
 * ---- AND THE POLICY ITSELF NOW LIVES IN pure/PanelBehaviour.cs ----
 * Which lamp a press produces, which controls are inert, and whether a press is audible are
 * DECISIONS, and decisions in a MonoBehaviour need a running game and a mouse to exercise. They are
 * in `PanelPolicy` / `PanelBoard` now, where the headless test and the PNG preview run the same code
 * this does. What is left here is what genuinely needs Unity: the collider, the material, the clock.
 *
 * ---- WHICH MATERIAL PROPERTY, DECIDED BY LOOKING ----
 * Deferred and TexturesUnlimited are both installed and both rewrite shaders on load, so the property
 * that carries colour on these buttons cannot be known from here. Rather than guess one and get a
 * silently dead indicator, the first button to be set up LOGS its shader and every colour property it
 * actually has, and the tint uses the first supported name. One load says whether this works and, if
 * not, exactly what to use instead.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    /// <summary>Marker so the panel is built once per prop, not once per screen module on it.</summary>
    public class PanelMarker : MonoBehaviour { }

    public static class PanelButtons
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Shared by both emergency plates - see PanelMap, they are one control set.</summary>
        public static readonly Interlock Lock = new Interlock();

        private static bool loggedMaterial;

        public static void Attach(InternalProp prop)
        {
            if (prop == null) return;
            if (prop.transform.GetComponent<PanelMarker>() != null) return;   // already built
            prop.transform.gameObject.AddComponent<PanelMarker>();

            PanelEntry[] map = PanelMap.All;
            int made = 0, missing = 0;
            List<string> notFound = new List<string>();

            for (int i = 0; i < map.Length; i++)
            {
                Transform t = FindButton(prop, map[i].Plate, map[i].Button);
                if (t == null)
                {
                    missing++;
                    if (notFound.Count < 8) notFound.Add(map[i].Plate + "/" + map[i].Button);
                    continue;
                }

                if (t.GetComponent<Collider>() == null) t.gameObject.AddComponent<BoxCollider>();

                PanelButton pb = t.gameObject.GetComponent<PanelButton>();
                if (pb == null) pb = t.gameObject.AddComponent<PanelButton>();
                pb.Configure(map[i]);
                made++;
            }

            // The abort handle is its own transform on its own plate, not a BUT-numbered button.
            Transform h = prop.FindModelTransform(PanelMap.AbortHandle);
            if (h != null)
            {
                if (h.GetComponent<Collider>() == null) h.gameObject.AddComponent<BoxCollider>();
                PanelButton pb = h.gameObject.GetComponent<PanelButton>();
                if (pb == null) pb = h.gameObject.AddComponent<PanelButton>();
                pb.Configure(new PanelEntry(PanelMap.PlateAbort, PanelMap.AbortHandle,
                                            "EJECT", PanelCommand.Abort));
                made++;
            }
            else missing++;

            Debug.Log(Tag + "panel armed: " + made + " controls live, " + missing + " not found"
                      + (notFound.Count > 0 ? "  missing e.g. " + string.Join(", ", notFound.ToArray()) : ""));
        }

        /// <summary>
        /// Buttons are children of a PLATE, and the same button mesh reused in another plate gets
        /// Unity's `_2 .. _5` copy suffix - so `CD2_PROP_BUT1` is not unique across the prop and
        /// FindModelTransform on the bare name would return whichever it hit first. Search inside the
        /// plate instead, and accept the suffix.
        /// </summary>
        private static Transform FindButton(InternalProp prop, string plate, string button)
        {
            Transform p = prop.FindModelTransform(plate);
            if (p == null) return null;

            string want = "CD2_PROP_" + button;
            Transform[] all = p.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n == want) return all[i];
                // "CD2_PROP_BUT1_3" is BUT1's copy; "CD2_PROP_BUT10" is a different button. Only
                // accept a suffix that starts with '_'.
                if (n.Length > want.Length && n.StartsWith(want, StringComparison.Ordinal)
                    && n[want.Length] == '_') return all[i];
            }
            return null;
        }

        /// <summary>
        /// Colour properties worth trying, commonest first. Logged once so a shader that supports
        /// none of them is a one-line diagnosis instead of an evening.
        /// </summary>
        private static readonly string[] ColourProps =
            { "_EmissiveColor", "_EmissionColor", "_Color", "_TintColor" };

        internal static string PickColourProperty(Material m)
        {
            if (m == null) return null;

            if (!loggedMaterial)
            {
                loggedMaterial = true;
                List<string> has = new List<string>();
                for (int i = 0; i < ColourProps.Length; i++)
                    if (m.HasProperty(ColourProps[i])) has.Add(ColourProps[i]);

                string pick = has.Count > 0 ? has[0] : null;
                Debug.Log(Tag + "panel button material '" + m.name + "' shader '"
                          + (m.shader != null ? m.shader.name : "none")
                          + "' colour properties present: "
                          + (has.Count > 0 ? string.Join(", ", has.ToArray()) : "NONE"
                             + " - the indicator cannot be tinted through this shader, dump needed")
                          // The RESTING VALUE decides how much headroom a brighter state has. First
                          // pass assumed white and would have had none; it is a mid grey, which is
                          // why lighting to white was visible but weak.
                          + (pick != null ? "  resting " + pick + " = " + m.GetColor(pick).ToString("F3")
                                          : ""));
            }

            for (int i = 0; i < ColourProps.Length; i++)
                if (m.HasProperty(ColourProps[i])) return ColourProps[i];
            return null;
        }
    }

    /// <summary>One console button: takes the click, runs the command, drives its own dash.</summary>
    public class PanelButton : MonoBehaviour
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>How long a momentary press stays lit, seconds.</summary>
        private const float FlashSeconds = 0.6f;

        private PanelEntry entry;
        private Renderer rend;
        private Material mat;
        private string colourProp;
        private Color restColour;
        private bool haveRest;

        private PanelLight light = PanelLight.Dark;
        private float until = -1f;
        private bool latched;

        public void Configure(PanelEntry e)
        {
            entry = e;
            rend = GetComponent<Renderer>();
            if (rend != null)
            {
                // .material makes a per-renderer instance, which is what we want: tinting the shared
                // material would light every button on the console at once.
                mat = rend.material;
                colourProp = PanelButtons.PickColourProperty(mat);
                if (colourProp != null)
                {
                    restColour = mat.GetColor(colourProp);
                    haveRest = true;
                }
            }
        }

        public void OnMouseDown()
        {
            if (InternalCamera.Instance == null) return;   // same guard as ScreenTouch

            PanelCommand c = entry.Command;

            // ---- THE CLICK IS FIRST AND IT IS UNCONDITIONAL (§14.4(a)) ----
            // The switch made a noise because the switch MOVED, not because the command worked. It
            // therefore happens before anything is decided, and it happens for the inert controls
            // and the unbacked ones too - since the red dash went away it is the only feedback they
            // have, and a control that answers with nothing at all reads as a missed collider.
            if (PanelPolicy.Clicks(c)) PanelAudio.Click();

            PanelPressKind kind;

            // ---- CANCEL: clear any armed command AND stop any running sequence (user 2026-08-21) ----
            // Two jobs. The interlock clears an armed emergency command; CancelAllSequences stops a
            // running ascent / rendezvous / dock / de-orbit / undock, whether or not anything was
            // armed. It still never punishes the careful press: with nothing armed and nothing
            // running it clicks and stays dark.
            if (c == PanelCommand.Cancel)
            {
                PressResult r = PanelButtons.Lock.Press(c);
                if (PanelPolicy.ClearsArmedLamps(r)) ClearArmedLamps();
                bool stopped = FlightCommands.CancelAllSequences();
                kind = PanelPolicy.ResolveCancel(r, stopped);
                Debug.Log(Tag + "panel: CANCEL -> " + kind
                          + (r == PressResult.Cancelled ? "  (armed cleared)" : "")
                          + (stopped ? "  (sequence stopped)" : ""));
            }
            else if (c == PanelCommand.Execute || PanelMap.NeedsExecute(c))
            {
                PressResult r = PanelButtons.Lock.Press(c);
                if (PanelPolicy.ClearsArmedLamps(r)) ClearArmedLamps();

                // Only a FIRE dispatches. Everything else is a state change inside the interlock.
                bool acted = (r == PressResult.Fire) && FlightCommands.Run(PanelButtons.Lock.Fired);

                kind = PanelPolicy.ResolveInterlock(r, acted);
                Debug.Log(Tag + "panel: " + entry.Label + " -> " + r + " / " + kind
                          + (PanelButtons.Lock.Armed != PanelCommand.None
                             ? "  (armed: " + PanelButtons.Lock.Armed + ")" : ""));
            }
            else if (PanelPolicy.IsInert(c))
            {
                // ---- §14.4(b): MODELLED, PRESSABLE, AND DELIBERATELY WITHOUT FUNCTION ----
                // SWAP 1/2/3 and the three entry-mode toggles are inferred, not sourced. The
                // dispatcher is not called AT ALL - not called and ignoring the result would leave
                // one edit between here and a control acting on a guess.
                kind = PanelPressKind.Inert;
                Debug.Log(Tag + "panel: " + entry.Label
                          + " -> INERT (function unverified, BUILD_PLAN §14.4(b))");
            }
            else
            {
                // Everything else acts immediately.
                bool acted = FlightCommands.Run(c);
                kind = PanelPolicy.ResolveImmediate(c, acted, ModeIsOn(c));
                Debug.Log(Tag + "panel: " + entry.Label + " -> " + kind);
            }

            Show(kind);
        }

        /// <summary>Turn the pure outcome into this button's dash: bright or dark, held or momentary.</summary>
        private void Show(PanelPressKind kind)
        {
            PanelLight want = PanelPolicy.LampFor(kind);
            if (PanelPolicy.Latches(kind)) Latch(want);
            else if (want == PanelLight.Lit) Flash(want, FlashSeconds);
            else if (kind == PanelPressKind.ModeOff) Latch(PanelLight.Dark);
            // Inert and Nothing: the click already happened and nothing lights. Not even a flash of
            // dark - leaving the lamp exactly as it was is what "this press did nothing" looks like.
        }

        /// <summary>
        /// Which lamps hold their state, and which are driven from somewhere other than their own
        /// press, are `PanelPolicy`'s calls now - see pure/PanelBehaviour.cs. The three entry-mode
        /// toggles are no longer among them: §14.4(b) made them inert, so they latch nothing.
        /// </summary>
        private static bool IsLiveMode(PanelCommand c) { return PanelPolicy.IsLiveMode(c); }

        /// <summary>
        /// The state BEHIND a mode lamp. This one stays in the glue because it reads the dispatcher,
        /// which is where the game is.
        /// </summary>
        private static bool ModeIsOn(PanelCommand c)
        {
            switch (c)
            {
                case PanelCommand.EnableBackupPyros:  return FlightCommands.BackupPyros;
                // POWER lamps show which bus is live, so the crew can see the row is armed.
                case PanelCommand.Power1: return FlightCommands.State.Bus1On;
                case PanelCommand.Power2: return FlightCommands.State.Bus2On;
                // The flight-computer engage lamps track the live phase state (grid column A/B/C),
                // however the phase was started - the physical STRING button OR the touchscreen.
                case PanelCommand.String1A: return AutoPilot.Engaged;                        // ASCENT
                case PanelCommand.String1B: return StationApproach.Engaged
                                                || DockingOps.Engaged;                       // RNDZ/DOCK
                case PanelCommand.String1C: return DeorbitOps.Engaged;                       // DEORBIT
            }
            return false;
        }

        private void ClearArmedLamps()
        {
            PanelButton[] all = UnityEngine.Object.FindObjectsOfType<PanelButton>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].latched && PanelMap.NeedsExecute(all[i].entry.Command))
                    all[i].Latch(PanelLight.Dark);
        }

        private void Latch(PanelLight l)
        {
            latched = l != PanelLight.Dark;
            light = l;
            until = -1f;
            Apply();
        }

        private void Flash(PanelLight l, float seconds)
        {
            latched = false;
            light = l;
            until = Time.realtimeSinceStartup + seconds;
            Apply();
        }

        public void Update()
        {
            // A momentary flash - a press confirmation, the only kind left - plays out first, even on
            // a live-mode button, so an acted-on press is seen before the lamp goes back to tracking
            // its state. A press that could NOT act sets no flash at all now (§14.4(a)), so a
            // live-mode lamp is never interrupted by one.
            if (until > 0f)
            {
                if (Time.realtimeSinceStartup <= until) return;    // flash still showing
                until = -1f;
                if (!IsLiveMode(entry.Command))
                {
                    light = PanelLight.Dark;                       // momentary button: flash done
                    Apply();
                    return;
                }
                // live-mode: fall through and re-establish the state-driven lamp
            }

            // The POWER and flight-computer engage lamps mirror live state, so re-read it every tick
            // rather than latching once at the press: the ASCENT lamp must go dark by itself at
            // insertion, and light when the phase re-engages - or is started from the touchscreen -
            // without another press.
            if (IsLiveMode(entry.Command))
            {
                PanelLight want = ModeIsOn(entry.Command) ? PanelLight.Lit : PanelLight.Dark;
                if (want != light)
                {
                    light = want;
                    latched = (want != PanelLight.Dark);
                    Apply();
                }
            }
        }

        // ---- OVERDRIVEN, BECAUSE PLAIN WHITE WAS ONLY JUST VISIBLE ----
        // Measured in flight 2026-08-06: the shader is `KSP/Unlit` and the only colour property is
        // `_Color`, which MULTIPLIES the button texture. The resting value is a mid grey, so setting
        // it to white did light the dash - just barely, which is no use for the one control set you
        // read under pressure.
        //
        // These go ABOVE 1. SetColor passes values through unclamped at runtime (only the inspector's
        // colour picker clamps), so on a shader that honours it the dash goes properly bright, and on
        // one that clamps we land on full white - which is exactly what we already had. **This can
        // only improve the contrast, never reduce it**, which is why it is preferred over dimming the
        // resting state: that would have altered art Tundra drew, on every button, all the time.
        //
        // ⛔ `FailColour` - the red one - WAS DELETED HERE 2026-09-02 (§14.4(a)). It was ours, no
        // source shows a red button on this console, and the state that drove it is gone from
        // `PanelLight` too. Two colours is now the whole language: the dash Tundra drew, or a bright
        // one.
        private static readonly Color LitColour = new Color(2.2f, 2.2f, 2.2f, 1f);

        private void Apply()
        {
            if (mat == null || colourProp == null || !haveRest) return;
            mat.SetColor(colourProp, (light == PanelLight.Lit) ? LitColour : restColour);
        }
    }
}
