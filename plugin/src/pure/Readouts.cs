/*
 * DragonScreen - Readouts
 *
 * PURE. One caption / value / unit row, drawn the same way everywhere.
 *
 * ---- WHY THIS EXISTS ----
 * MECH, SETTINGS and NAV each grew their own Row() and each placed the unit differently: one put it
 * 46 px left of the value at Dense, one 64 px left at Caption, one had no unit column at all. Read
 * down a page and the numbers did not line up; read across two pages and they disagreed about what a
 * unit even looks like. That is CLAUDE.md trigger #2 - a rule three places needed, written three
 * times - in its most ordinary form.
 *
 * ---- THE UNIT GETS ITS OWN COLUMN ----
 * Value right-aligned at a FIXED offset from the row's right edge, unit right-aligned at the edge.
 * So the digits form one column the eye can run down, and the units form another beside it. Putting
 * the unit immediately after a variable-width number is what makes a table look ragged.
 */
namespace DragonScreen
{
    public static class Readouts
    {
        /// <summary>Width reserved for the unit column, at the row's right edge.</summary>
        public const float UnitColumn = 54f;

        /// <summary>Commands one row emits, for display-list sizing.</summary>
        public const int Commands = 3;

        public static void Row(DisplayList dl, float x, float y, float w,
                               string caption, string value, string unit, float valueSize)
        {
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);

            bool hasUnit = !string.IsNullOrEmpty(unit);
            // The value stops short of the unit column whether or not this row HAS a unit, so a
            // table of mixed rows still lines up. A row without a unit is not a row with more room.
            float valueRight = x + w - (hasUnit ? UnitColumn : 0f);
            dl.Text(value ?? "-", valueRight, y, valueSize, TextAlign.Right, DragonPalette.Text0);

            if (hasUnit)
                dl.Text(unit, x + w, y + 2f, Typography.Dense, TextAlign.Right, DragonPalette.Text7);
        }
    }
}
