/*
 * DragonScreen - DragonScreenState
 *
 * THE ONLY THING THAT SURVIVES. A PartModule on the pod holding which page each screen shows.
 *
 * ---- WHY A PartModule WHEN EVERYTHING VISIBLE IS AN InternalModule ----
 * CLAUDE.md, "ONE SCREEN, FOUR SURFACES", flagged as the consequence that must not be designed
 * around later: internal models are torn down and rebuilt whenever the camera mode changes, so a
 * page selection stored in an InternalModule is lost on the next rebuild, and lost again on every
 * save/load. A PartModule lives with the PART, and `[KSPField(isPersistant = true)]` goes into the
 * save file.
 *
 * MAS reached the same arrangement independently, which is the evidence it is right: every MASMonitor
 * reads its current page from a persistent value keyed by monitorID (MASMonitor.cs:269-278), stored
 * on MASFlightComputer - a PartModule (MASFlightComputer.cs:44).
 *
 * ---- IT DOES NOTHING ELSE, ON PURPOSE ----
 * No drawing, no input, no vessel reading. It is a persisted string with two accessors. The moment
 * it starts doing more, it becomes a second place where screen behaviour lives, and there is already
 * one.
 *
 * The encoding and every malformed-input case are in src/pure/PageSelection.cs, headless tested -
 * this file must not parse anything itself.
 */
using UnityEngine;

namespace DragonScreen
{
    public class DragonScreenState : PartModule
    {
        /// <summary>
        /// Page per screen, e.g. "1,0,2". Written by PageSelection, never by hand.
        ///
        /// EMPTY MEANS "NEVER TOUCHED", which is a real state and not a missing value: a vessel that
        /// has never been flown shows each screen's cfg default. Only once the crew selects something
        /// does this take over. That is why it is not seeded at load.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string screenPages = "";

        /// <summary>
        /// Screen brightness in tenths. 0 means "never set" and resolves to full, which keeps the
        /// same never-touched-is-a-real-state rule as screenPages rather than baking a default into
        /// the save file the first time a vessel loads.
        /// </summary>
        [KSPField(isPersistant = true)]
        public int screenBrightness = 0;

        /// <summary>
        /// Bumped on every change. NOT persisted - it is a change signal, not a value.
        ///
        /// Three painters cache what they read from here, and the SETTINGS grid lets one display
        /// change ANOTHER display's page. Without this, the screen that was changed would keep
        /// drawing its old page until something else made it re-read. A counter is cheaper than
        /// wiring the three painters to each other, and it cannot go stale in the direction that
        /// matters: a missed bump shows the wrong page, and there are no missed bumps because every
        /// setter is here.
        /// </summary>
        public int Version { get; private set; }

        public int GetPage(int screenIndex, int pageCount, int fallback)
        {
            return PageSelection.Get(screenPages, screenIndex, pageCount, fallback);
        }

        public void SetPage(int screenIndex, int page, int pageCount)
        {
            string next = PageSelection.Set(screenPages, screenIndex, page, pageCount);
            if (next == screenPages) return;
            screenPages = next;
            Version++;
            Debug.Log("[DragonScreen] state -> '" + screenPages + "' on " + part.partInfo.name);
        }

        public int GetBrightness()
        {
            if (screenBrightness < SettingsPage.MinBright) return SettingsPage.MaxBright;
            if (screenBrightness > SettingsPage.MaxBright) return SettingsPage.MaxBright;
            return screenBrightness;
        }

        public void SetBrightness(int tenths)
        {
            if (tenths == screenBrightness) return;
            screenBrightness = tenths;
            Version++;
        }

        /// <summary>
        /// Find the state module on the part this prop belongs to.
        ///
        /// Returns null rather than creating one: if the ModuleManager patch did not land, the right
        /// outcome is screens that work but forget - not a module conjured at runtime that never
        /// makes it into the save and quietly does nothing. The caller logs it once.
        /// </summary>
        public static DragonScreenState FindOn(Part p)
        {
            if (p == null) return null;
            for (int i = 0; i < p.Modules.Count; i++)
            {
                DragonScreenState s = p.Modules[i] as DragonScreenState;
                if (s != null) return s;
            }
            return null;
        }
    }
}
