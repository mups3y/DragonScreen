// DragonScreen — Dashes  (PURE: the ONE glyph a screen prints when there is no value)
// ============================================================================================
// Register S148 / S49 H45's latent half: "the codebase has TWO dash glyphs — `—` in the vehicle
// family, ASCII `-` in the … widgets". Measured before fixing: **48 sites used `—` and 25 used `-`**,
// and the split is not the one H45 guessed. It is not two stray widgets; it is the whole LEGACY page
// family (`Pages`, `ChromeBar`, `Readouts`, `Gauge`, `StatusIndicator`, `DockingPageCentral`,
// `SettingsPage`) against the whole FIGMA-era family.
//
// ---- ⛔ WHY THE EM DASH WINS, AND WHY THAT IS NOT A TASTE CALL ----
// Not because it is prettier. Because the Figma-era pages are THE SHIPPED UI — `ScreenPainter`'s
// `FigmaMode` is a `const true`, so those 48 sites are what a crew actually reads — and the 25 are in
// the branch that never runs. Making the unreachable half match the shipped half is the only direction
// that does not change what anybody sees.
//
// ---- ⚠ AND THAT IS ALSO WHY THIS SWEEP IS COSMETICALLY INERT, STATED SO NOBODY EXPECTS A DIFF ----
// Every one of the 25 sites is in code `FigmaMode` makes unreachable (`ScreenPainter.cs:1201-1206`
// draws `Pages`/`ChromeBar` in the `else` branch only). No preview PNG of a SHIPPED page changes, and
// no pixel on the glass changes. This is hygiene against the day that branch is revived — which
// [[S121]] and [[S134]] are both about — not a visible fix, and the register entry says so.
//
// ⛔ DO NOT "SIMPLIFY" THIS TO A BARE STRING LITERAL AT EACH SITE. A named constant is how the next
// page gets it right without reading this file: `?? Dashes.None` says what it means, and a literal
// does not. The whole finding is that two literals drifted apart in the first place.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>The glyphs a screen prints in place of a value it does not have.</summary>
    public static class Dashes
    {
        /// <summary>NO VALUE — the source is absent, not zero. The em dash, matching the 48 shipped
        /// Figma-era sites and every `const string Dash = "—"` in the vehicle family.
        ///
        /// ⚠ It means "there is nothing to show", never "the reading is zero". §14.4(f) narrowed when
        /// it is allowed at all — a dash now survives only for a genuinely-absent state — but where it
        /// IS right, this is the glyph.</summary>
        public const string None = "—";
    }
}
