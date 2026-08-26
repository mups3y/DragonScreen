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
        {
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);

            bool hasUnit = !string.IsNullOrEmpty(unit);
            float valueRight = x + w - (hasUnit ? UnitColumn : 0f);
            dl.Text(value ?? "-", valueRight, y, valueSize, TextAlign.Right, DragonPalette.Text0);

            if (hasUnit)
                dl.Text(unit, x + w, y + 2f, Typography.Dense, TextAlign.Right, DragonPalette.Text7);
        }
    }
}
