// DragonScreen - Readouts
// ---- WHY THIS EXISTS ----
// ---- THE UNIT GETS ITS OWN COLUMN ----
namespace DragonScreen
{
    public static class Readouts
    {
        public const float UnitColumn = 54f;

        public const int Commands = 3;

        public static void Row(DisplayList dl, float x, float y, float w,
                               string caption, string value, string unit, float valueSize)
        { Row(dl, x, y, w, caption, value, unit, valueSize, 1f); }

        /// <summary>
        /// As Row, on a panel whose type scale is <paramref name="sc"/> = Typography.ScaleFor(panelW).
        ///
        /// ---- WHY THIS OVERLOAD EXISTS (S117 / QC R-02, 2026-09-06) ----
        /// The caption and the unit are drawn at Typography.Caption / Typography.Dense, which are
        /// sizes AT Typography.RefPanelW (1280) - see Typography's header. On a 2560-wide panel they
        /// are half the physical size they were measured at. `valueSize` is the caller's own number
        /// and arrives already scaled; these two were not the caller's to scale, so they are scaled
        /// here, along with the unit column and the unit's baseline nudge that go with them.
        ///
        /// The 8-argument overload above delegates with sc = 1, so every page that has not had a
        /// scale pass yet renders byte-identically. That is deliberate: this fixes the pages that
        /// KNOW their scale and leaves the rest visibly un-passed rather than silently half-done.
        /// </summary>
        public static void Row(DisplayList dl, float x, float y, float w,
                               string caption, string value, string unit, float valueSize, float sc)
        {
            dl.Text(caption, x, y, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text6);

            bool hasUnit = !string.IsNullOrEmpty(unit);
            float valueRight = x + w - (hasUnit ? UnitColumn * sc : 0f);
            dl.Text(value ?? "-", valueRight, y, valueSize, TextAlign.Right, DragonPalette.Text0);

            if (hasUnit)
                dl.Text(unit, x + w, y + 2f * sc, Typography.Dense * sc, TextAlign.Right, DragonPalette.Text7);
        }
    }
}
