/*
 * F9IScreen - F9IBridgeModule
 *
 * GLUE. The single point where kOS hands state to the panel. It contains no logic beyond storing a
 * string and handing it to the parser - deliberately, because this is the file that cannot be tested
 * headless, and the F9_LOP lesson is that the untestable half is where the bugs live.
 *
 * kOS talks to it exactly the way F9I already talks to engines and decouplers:
 *
 *     SET b TO SHIP:MODULESNAMED("F9IBridgeModule")[0].
 *     b:SETFIELD("f9i data", "1~|~" + message1:text + "~|~" + ... ).
 *
 * ---- ONE FIELD, ON PURPOSE ----
 * kOS REQUIRES guiActive = true to read or write a field, so every field is also a right-click row.
 * Thirteen values as thirteen fields would be thirteen rows AND thirteen SETFIELD calls per update.
 * One packed string is one row and one call. kOS cost is the reason this port exists, so the sending
 * side is the one to keep cheap. Format and parsing live in src/pure/BridgeProtocol.cs, where they
 * are covered by 34 headless checks.
 *
 * ---- NOT isPersistant ----
 * This is live flight state, not configuration. Persisting it would restore a stale snapshot on load
 * and show the pilot last session's numbers until kOS got round to the first push.
 */
using System;
using UnityEngine;

namespace F9IScreen
{
    public class F9IBridgeModule : PartModule
    {
        /// <summary>
        /// The packed payload, written by kOS. See BridgeProtocol for the format.
        /// guiActive is mandatory - kOS cannot see a field without it - so this row is visible in the
        /// part's right-click menu whether we like it or not. Keeping it to ONE field is the mitigation.
        /// </summary>
        [KSPField(isPersistant = false, guiActive = true, guiName = "f9i data")]
        public string f9iData = "";

        /// <summary>
        /// Parsed state. Read by the panel. Lives on the module rather than in a static so that two
        /// loaded Dragons cannot overwrite each other's readings - the panel picks the ACTIVE vessel's
        /// module explicitly rather than trusting a global "most recent writer".
        /// </summary>
        public readonly BridgeState State = new BridgeState();

        // Only re-parse when the string actually changes. kOS pushes on change, but KSP calls Update
        // every frame regardless, and re-splitting an unchanged string 60 times a second is exactly
        // the kind of quiet waste this project keeps finding.
        private string lastParsed;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            lastParsed = null;
        }

        public void Update()
        {
            if (f9iData == lastParsed) return;
            lastParsed = f9iData;
            BridgeProtocol.Unpack(f9iData, State);
        }

        /// <summary>
        /// Find the bridge on the active vessel, or null.
        /// Deliberately a plain search rather than a cached static: vessel switching, docking and
        /// undocking all reshuffle part ownership, and a stale cached reference surviving an undock is
        /// precisely the class of bug that produced audit issue #68 (a handle to a jettisoned stage).
        /// The CALLER caches this per vessel change - see F9IScreenAddon.
        /// </summary>
        public static F9IBridgeModule FindOnActiveVessel()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.parts == null) return null;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null || p.Modules == null) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    F9IBridgeModule b = p.Modules[m] as F9IBridgeModule;
                    if (b != null) return b;
                }
            }
            return null;
        }
    }
}
