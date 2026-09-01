// DragonScreen — MechPage  (PURE: the OLD vehicle-tab model's "Mech" sub-view)
// ============================================================================================
// This belongs to the ORIGINAL page model (Pages.VehicleTabs, subview 1), which is now dead code under
// ScreenPainter.FigmaMode = true — but it must still compile, and it stays intact so the old model is
// reversible. Its own note called Mech "the half we never had": it was always a stub. The new Figma
// UI's Mech schematic is a separate class, VehicleMechPage. Do not confuse the two.
// ============================================================================================
namespace DragonScreen
{
    public static class MechPage
    {
        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            dl.Text("MECH", w * 0.5f, h * 0.42f, h * 0.06f, TextAlign.Centre, DragonPalette.Text6);
            dl.Text("not yet implemented", w * 0.5f, h * 0.42f + h * 0.08f, h * 0.03f,
                    TextAlign.Centre, DragonPalette.Text7);
        }
    }
}
