/*
 * DragonScreen - PanelAudio
 *
 * GLUE. The console's mechanical click: one 60 ms sample, played on every button press.
 *
 * ---- WHY EVERY PRESS, INCLUDING THE ONES THAT DO NOTHING ----
 * BUILD_PLAN §14.4(a) removed the red "refused" dash, which was the only feedback an inert or
 * unbacked control used to give. Without a replacement, pressing SWAP 2 would produce absolutely
 * nothing - and a control that gives nothing back is indistinguishable from a collider you missed,
 * so the crew presses it again, harder, and then reports the panel as broken. The click is what
 * makes "it did nothing" READ as "it did nothing" rather than as "it did not register".
 *
 * So the rule is the physical one and not the logical one: the sound belongs to the SWITCH moving,
 * not to the command succeeding. `PanelPolicy.Clicks` owns that decision; this file only plays it.
 *
 * ---- THE SAMPLE IS OURS ----
 * build/make_click.py synthesises art-free, licence-free, deterministic PCM into
 * GameData/DragonScreen/sounds/panel_click.wav. Nothing was downloaded (C7 puts external URLs
 * off-limits) and there is no attribution to keep straight.
 *
 * ---- 2D ON PURPOSE, AND IT IS A FAIL-SAFE CHOICE ----
 * A positional source would be more correct: the click would come from the button you pushed. But
 * 3D audio in an IVA depends on where KSP has put the AudioListener, on the internal model's scale
 * and on the rolloff curve, and NONE of that can be judged with the game closed - the failure mode
 * is a sound that is simply never heard, which looks identical to a sound that never played.
 *
 * spatialBlend = 0 cannot fail that way. It is the version that is audible or obviously broken,
 * never silently wrong, which is the right trade for something whose first hearing is a whole
 * capsule session away (REGISTER.md S17). If it reads as flat on the glass, that session moves it
 * to 3D with real numbers instead of guessed ones.
 *
 * ---- A MISSING FILE IS A STATE, NOT A CRASH ----
 * Same rule as ImageStore: log once, remember the failure, and let the panel go on working
 * silently. A button that throws because its sound is missing would be a worse bug than no sound.
 */
using UnityEngine;

namespace DragonScreen
{
    internal static class PanelAudio
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>GameData-relative, no extension - that is how GameDatabase keys audio.</summary>
        private const string ClipPath = "DragonScreen/sounds/panel_click";

        /// <summary>Under 1 because a switch under your hands should not be an event.</summary>
        private const float Volume = 0.55f;

        private static AudioClip clip;
        private static AudioSource source;
        private static bool looked;      // the load was attempted, successfully or not

        /// <summary>
        /// Play the switch click. Safe to call from anywhere at any time: if the clip is missing, or
        /// the database is not up yet, this does nothing at all rather than complaining per press.
        /// </summary>
        internal static void Click()
        {
            AudioClip c = Clip();
            if (c == null) return;

            AudioSource s = Source();
            if (s == null) return;

            // SHIP_VOLUME, not UI_VOLUME: this is a thing in the cabin making a noise, not a beep
            // the interface is making at you. A crew member who has turned the ship down should get
            // a quieter panel.
            s.PlayOneShot(c, Volume * GameSettings.SHIP_VOLUME);
        }

        private static AudioClip Clip()
        {
            if (looked) return clip;

            // GameDatabase is populated at load; before that there is nothing to find and asking
            // again next press costs nothing, so do NOT latch `looked` on a null database.
            if (GameDatabase.Instance == null) return null;
            looked = true;

            clip = GameDatabase.Instance.GetAudioClip(ClipPath);
            if (clip == null)
                Debug.LogWarning(Tag + "panel click sound missing: GameData/" + ClipPath
                                 + ".wav - the buttons will work silently."
                                 + "  Run `python build/make_click.py` and reinstall.");
            else
                Debug.Log(Tag + "panel click loaded (" + clip.length.ToString("F3") + "s)");
            return clip;
        }

        private static AudioSource Source()
        {
            // Re-made if a scene change took it: one shared source rather than 39, because the
            // sound is 2D and a per-button source would differ only in the memory it wasted.
            if (source != null) return source;

            GameObject go = new GameObject("DragonScreenPanelAudio");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;                       // see the header - deliberately 2D
            source.dopplerLevel = 0f;
            source.bypassEffects = true;
            source.bypassReverbZones = true;
            return source;
        }
    }
}
