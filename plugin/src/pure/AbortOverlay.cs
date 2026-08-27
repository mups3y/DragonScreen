// DragonScreen — AbortOverlay  (pure, previewable: the emergency alert drawn OVER the live page)
// ============================================================================================
// While the vehicle is aborting, every screen paints this on TOP of whatever page is showing — a clear
// (transparent) overlay, so the underlying instruments still read through it.
//   • the centre art STAYS ON (a user-supplied art/dontpanic.png if installed, else the plain wordmark)
//     — as large as possible, aspect-locked, no stretch, never off the edge;
//   • "ABORTING" (centred in the gap just ABOVE the DON'T PANIC wording) and a red frame SQUARE-FLASH
//     on/off together.
// Pure so `build.py preview` renders the exact layout without a game restart. The glue (ScreenPainter)
// gates it on FlightDriver.Aborting and drives the flash + whether the art file actually loaded.
// ============================================================================================
namespace DragonScreen
{
    public static class AbortOverlay
    {
        static readonly Rgba Red = new Rgba(0.95f, 0.14f, 0.11f, 1f);
        static readonly Rgba White = new Rgba(1f, 1f, 1f, 1f);

        // Where the "DON'T PANIC" wording sits WITHIN art/dontpanic.png, as fractions of the image (measured
        // by inspecting the red text: centre-x 0.271, its top 0.493). ABORTING is placed centred on that x,
        // in the gap above the wording. ⚠ If you swap the art for one with the words elsewhere, re-measure these.
        const float DpCx = 0.271f;        // centre-x of DON'T PANIC within the art
        const float DpAbortTop = 0.325f;  // top-y for ABORTING (the gap above the wording), within the art

        // flashOn: the ABORTING banner + red frame are shown this frame (glue square-flashes it; preview = on).
        // hasImage: the glue loaded art/dontpanic.png. imgAspect: its width/height (fit without stretching).
        public static void Build(DisplayList dl, int w, int h, bool flashOn, bool hasImage, float imgAspect)
        {
            // --- the art rect: fit into the area below the banner, centred, as large as possible, aspect-locked ---
            float areaX = w * 0.02f, areaY = h * 0.15f, areaW = w * 0.96f, areaH = h * 0.83f;
            float ix, iy, tw, th;
            if (hasImage && imgAspect > 0.01f)
            {
                tw = areaW; th = areaW / imgAspect;
                if (th > areaH) { th = areaH; tw = areaH * imgAspect; }
                ix = (w - tw) * 0.5f; iy = areaY + (areaH - th) * 0.5f;
                dl.Image(ImageId.DontPanic, ix, iy, tw, th, White);   // ALWAYS on — the art does not flash
            }
            else
            {
                tw = areaW; th = areaH; ix = areaX; iy = areaY;
                dl.Text("DON'T PANIC", w * 0.5f, areaY + areaH * 0.30f, h * 0.21f, TextAlign.Centre, Red);
            }

            // --- ABORTING banner + red frame: square-flash on/off together ---
            if (flashOn)
            {
                float s = h * 0.024f;
                dl.Box(s, s, w - 2f * s, h - 2f * s, s, Red);
                // Centred over the DON'T PANIC wording, in the gap just above it (art case); above the
                // wordmark otherwise.
                float ax = hasImage ? ix + DpCx * tw : w * 0.5f;
                float ay = hasImage ? iy + DpAbortTop * th : areaY + areaH * 0.10f;
                dl.Text("ABORTING", ax, ay, h * 0.105f, TextAlign.Centre, Red);
            }
        }
    }
}
