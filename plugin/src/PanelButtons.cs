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
 * So the colour scheme below - unlit grey as modelled, WHITE when pressed or armed, RED when refused -
 * is OUR DECISION (the user's, 2026-08-06), not a reconstruction. Recorded as such so nobody later
 * cites it as fact. What we do NOT do is invent a glow, an outline or a halo: the dash Tundra already
 * drew is the only thing driven.
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

        /// <summary>How long a refusal stays red. Longer, because it is the one you must notice.</summary>
        private const float FailSeconds = 1.5f;

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

            // ---- ⛔ THE "INERT" GATE IS GONE. IT WAS STANDING IN FRONT OF WORKING CODE. ----
            // DEPRESS RESPONSE, SUPPRESS FIRE and FIRE RESPONSE were listed as inert back when stock
            // KSP had no cabin fire and no depressurisation to act on. `pure/VehicleSystems.cs` then
            // MODELLED all three, and `FlightCommands` grew real handlers for them that isolate the
            // cabin, discharge the bottle and run the fire procedure - but this gate was never
            // removed, so every press returned here and logged "no KSP system behind this control"
            // while a complete implementation sat four lines further down, unreachable.
            //
            // On 2026-08-12 the crew pressed all three and got that message from all three. The
            // simulate-a-system rule was applied and then silently undone by a leftover guard.

            if (c == PanelCommand.Cancel || c == PanelCommand.Execute || PanelMap.NeedsExecute(c))
            {
                PressResult r = PanelButtons.Lock.Press(c);
                Debug.Log(Tag + "panel: " + entry.Label + " -> " + r
                          + (PanelButtons.Lock.Armed != PanelCommand.None
                             ? "  (armed: " + PanelButtons.Lock.Armed + ")" : ""));

                switch (r)
                {
                    case PressResult.Armed:
                        Latch(PanelLight.Lit);
                        break;

                    case PressResult.Cancelled:
                        // CANCEL clears every armed lamp on BOTH plates, not just this button's.
                        ClearArmedLamps();
                        Flash(PanelLight.Lit, FlashSeconds);
                        break;

                    case PressResult.Fire:
                        ClearArmedLamps();
                        bool ok = FlightCommands.Run(PanelButtons.Lock.Fired);
                        Flash(ok ? PanelLight.Lit : PanelLight.Failed,
                              ok ? FlashSeconds : FailSeconds);
                        break;

                    case PressResult.Refused:
                        Flash(PanelLight.Failed, FailSeconds);
                        break;

                    case PressResult.Ignored:
                        break;
                }
                return;
            }

            // Everything else acts immediately.
            bool done = FlightCommands.Run(c);
            Debug.Log(Tag + "panel: " + entry.Label + " -> " + (done ? "done" : "REFUSED"));
            if (done && IsMode(c)) Latch(ModeIsOn(c) ? PanelLight.Lit : PanelLight.Dark);
            else Flash(done ? PanelLight.Lit : PanelLight.Failed,
                       done ? FlashSeconds : FailSeconds);
        }

        /// <summary>Modes stay lit while they are on, rather than flashing and going out.</summary>
        private static bool IsMode(PanelCommand c)
        {
            return c == PanelCommand.EnableBackupPyros
                || c == PanelCommand.EnableEntryReboot
                || c == PanelCommand.EnableBackupEntry
                || c == PanelCommand.EnableNormalEntry;
        }

        private static bool ModeIsOn(PanelCommand c)
        {
            switch (c)
            {
                case PanelCommand.EnableBackupPyros:  return FlightCommands.BackupPyros;
                case PanelCommand.EnableEntryReboot:  return FlightCommands.EntryReboot;
                case PanelCommand.EnableBackupEntry:  return FlightCommands.BackupEntry;
                case PanelCommand.EnableNormalEntry:  return !FlightCommands.BackupEntry;
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
            if (until > 0f && Time.realtimeSinceStartup > until)
            {
                until = -1f;
                light = PanelLight.Dark;
                Apply();
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
        private static readonly Color LitColour = new Color(2.2f, 2.2f, 2.2f, 1f);
        private static readonly Color FailColour = new Color(2.0f, 0.22f, 0.24f, 1f);

        private void Apply()
        {
            if (mat == null || colourProp == null || !haveRest) return;
            Color c = (light == PanelLight.Lit) ? LitColour
                    : (light == PanelLight.Failed) ? FailColour
                    : restColour;
            mat.SetColor(colourProp, c);
        }
    }
}
