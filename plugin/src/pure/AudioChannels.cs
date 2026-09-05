// DragonScreen — AudioChannels  (PURE: the audio page's channels, and which of them are real)
// ============================================================================================
// ---- THE DECISION THIS FILE EXISTS UNDER, AND WHY IT IS NOT AN OVERRIDE ----
// `SettingsPage.cs:27-29` carries a dated owner decision, quoted here in full because this file would
// otherwise look like it contradicts one (C1.8):
//
//     "---- NO VOLUME SLIDERS ---- (user's call, 2026-08-06). Audio shows per-seat ROLE and occupancy,
//      and the intercom/alert state. KSP has no cabin audio, so a fader would be a control bound to
//      nothing. Simulate a reading, never simulate a control."
//
// ⭐ THAT DECISION IS NOT OVERRULED AND NO `OVERRIDE` WAS GIVEN OR NEEDED. Read its reason: it forbids
// faders ON A STATED PREMISE — "KSP has no cabin audio, so a fader would be a control bound to
// nothing". Binding these channels to the game's own audio layers FALSIFIES that premise rather than
// contradicting the rule: the control is bound to something real, so it is not the thing the rule
// names. **A real control is not a simulated one.** The rule's own closing sentence — "simulate a
// reading, never simulate a control" — is honoured exactly, because nothing here is simulated.
//
// 🟢 OWNER, Q6 (2026-09-05, verbatim — this half he DID type):
//     "make the volume controls control the game sound levels. Music, vehicle sound, ambient sound
//      etc etc. What ever logical sound layer options the game has, tie to those sliders etc"
//
// 🟢 AND THE MAPPING, 2026-09-06 — option selected via the overseer, "MAP THE FOUR THAT FIT".
// ⛔ RECORDED AS A SELECTION, NOT A QUOTE: he chose from presented options and did not write these
// words. Manufacturing a verbatim quote for a chosen option is the `LZ1` failure C1.12's evidentiary
// standard was added after. The option, as presented:
//
//     MAIN -> MASTER_VOLUME, AUX -> AMBIENCE_VOLUME, Vox -> VOICE_VOLUME, ALERTS -> SHIP_VOLUME.
//     MUSIC_VOLUME stays unmapped; INTERCOM stays the crew reading it already is; `dB` stays a unit
//     label. Every wired slider does what its label says.
//
// ⚠ AND HE WAS TOLD, AND ACCEPTED, THAT THESE ARE GLOBAL GAME SETTINGS: a tap in the capsule changes
// his whole-game audio and persists outside the seat. That is recorded here because it is the one
// consequence of this ruling a future reader would otherwise have to rediscover.
//
// ---- WHAT IS A CHANNEL AND WHAT IS NOT ----
// ⚠ QC's Q6 note described a "clean 5-to-5 fit" between the page's channels and KSP's volume sliders.
// It is not five and it is not clean, and the overseer corrected it: `Audio.vue`'s own slot list is
// SIX — "dB, AUX, MAIN, Vox, INTERCOM, ALERTS" (`SettingsPage.cs:17`) — and `GROUND` is a crew ROLE
// from the seat rows ("PASSENGER / PILOT / GROUND", `SettingsPage.cs:311`), not a channel at all.
//
// ---- UNITS: PERCENT, AND dB IS NOT A UNIT WE CAN HONESTLY PRINT ----
// ⛔ The page used to print "12dB", "0dB", "+9dB" as literals. KSP's volumes are LINEAR GAINS in 0..1.
// Turning one into decibels needs a reference level, and no source in this project gives one — so a
// dB figure here would be an invented quantity wearing a real unit, which is worse than no unit.
// Percent is the honest rendering of a 0..1 gain and needs nothing invented. The ruling's "`dB` stays
// a unit label" is consistent with that: it is a legend in Audio.vue's slot list, not a reading.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>A game audio layer this project is allowed to read and move. `None` is a channel with
    /// nothing behind it — a readout, never a control.</summary>
    public enum AudioLayer : byte { None = 0, Master, Ambience, Voice, Ship }

    /// <summary>The game's own volumes, 0..1, as the glue read them this frame. `Valid` false =
    /// settings unreadable, so every mapped channel dashes rather than showing a plausible 0.</summary>
    public struct AudioLevels
    {
        public bool Valid;
        public float Master, Ambience, Voice, Ship;
    }

    public static class AudioChannels
    {
        /// <summary>The ± step, as a fraction of full scale. 5% gives twenty presses end to end, which
        /// is a control a crew can aim with; KSP's own settings slider has no published detent to copy,
        /// so this is OURS and is stated as such rather than presented as the game's.</summary>
        public const float Step = 0.05f;

        /// <summary>
        /// Which game layer a channel label names, or `None`.
        ///
        /// ⛔ THE MAPPING IS THE OWNER'S, NOT A GUESS, and only four labels are in it. Everything else
        /// returns `None` — including `GROUND`, which is a crew ROLE the Figma page put in a channel
        /// slot, and `INTERCOM`, which the ruling explicitly keeps as the crew reading it already is.
        /// A label this method does not know is `None`, so a page that gains a channel gets a readout
        /// rather than a control that moves the wrong slider.
        /// </summary>
        public static AudioLayer LayerFor(string label)
        {
            if (label == "MAIN")     return AudioLayer.Master;
            if (label == "AUX")      return AudioLayer.Ambience;
            if (label == "VOX")      return AudioLayer.Voice;
            if (label == "ALERTS")   return AudioLayer.Ship;
            return AudioLayer.None;
        }

        /// <summary>The layer's current gain, 0..1, or -1 when there is nothing to read.</summary>
        public static float Level(AudioLevels s, AudioLayer l)
        {
            if (!s.Valid || l == AudioLayer.None) return -1f;
            switch (l)
            {
                case AudioLayer.Master:   return s.Master;
                case AudioLayer.Ambience: return s.Ambience;
                case AudioLayer.Voice:    return s.Voice;
                case AudioLayer.Ship:     return s.Ship;
            }
            return -1f;
        }

        /// <summary>What the channel prints. A mapped layer prints its gain as a percent; anything
        /// unmapped or unreadable prints the project dash. ⛔ Never a plausible number: an unreadable
        /// setting showing "0%" would say the game is muted, which is a different claim.</summary>
        public static string Text(AudioLevels s, AudioLayer l)
        {
            float v = Level(s, l);
            if (v < 0f) return Dashes.None;
            int pct = (int)(v * 100f + 0.5f);
            if (pct < 0) pct = 0; else if (pct > 100) pct = 100;
            return pct + "%";
        }

        /// <summary>Where one ± press lands, clamped to the full scale. Pure so the step, the clamp and
        /// the rounding are testable without the game; the glue only stores the result.</summary>
        public static float Nudge(float level, int dir)
        {
            float v = level + Step * (dir < 0 ? -1 : (dir > 0 ? 1 : 0));
            if (v < 0f) v = 0f; else if (v > 1f) v = 1f;
            return v;
        }

        /// <summary>Is this channel's ± pair a real control? True only when the label maps to a layer
        /// AND the settings are readable — so a page cannot paint a live-looking button over a layer it
        /// could not read. This is the ONE predicate the draw tint and the hit test both ask, which is
        /// the S32 discipline: a dimmed control cannot act and a live one cannot look unavailable.</summary>
        public static bool Actionable(AudioLevels s, string label)
        {
            return s.Valid && LayerFor(label) != AudioLayer.None;
        }
    }
}
