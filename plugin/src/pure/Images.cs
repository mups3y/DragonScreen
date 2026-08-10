/*
 * DragonScreen - Images
 *
 * PURE. Which bitmaps exist, what they are called on disk, and how big they are.
 *
 * ---- WHY THE SIZES LIVE HERE ----
 * A page has to place an image without stretching it, which means knowing its aspect - and the two
 * renderers must agree on that to the pixel or the preview stops being a preview. Reading the size
 * from the file would give each renderer its own answer at its own moment (and the preview would
 * read it from a different copy of the file). So the intrinsic size is a FACT ABOUT THE ASSET,
 * recorded once, here, next to the filename it belongs to.
 *
 * If an asset is replaced, change the numbers here in the same commit. The headless test checks the
 * declared aspect against the file on disk, so a mismatch fails the build rather than quietly
 * squashing the artwork.
 *
 * ---- WHY AN ENUM AND NOT A PATH ----
 * src/pure cannot hold a Texture2D or a System.Drawing.Image, so a page names an image by ID and
 * each renderer maps the ID to its own object. That also means a page can never reference a file
 * that was not declared - a typo is a compile error, not a blank rectangle in flight.
 */
namespace DragonScreen
{
    public enum ImageId : byte
    {
        None = 0,
        /// <summary>The Crew Dragon capsule + trunk render. Centre of the VEHICLE page.</summary>
        Dragon = 1,
        /// <summary>Docking HUD outer ring. From the Vue recreation's own assets, Apache-2.0.</summary>
        HudRing = 2,
        /// <summary>Docking HUD inner ring.</summary>
        HudRingInner = 3,
        /// <summary>
        /// The current body's own surface map, RESOLVED AT RUNTIME - not a file.
        ///
        /// KSP's scaled-space texture for whatever body the vessel is at, which is already stored
        /// equirectangular. That is why NAV needs no bundled world map, works over Kerbin and the
        /// Mun alike, and shows the real Earth under RSS without a line changing. It has no
        /// FileName and no declared Size for the same reason: nothing about it is known until there
        /// is a vessel somewhere.
        /// </summary>
        BodyMap = 4,
        /// <summary>
        /// The navball SKIN - the flat equirectangular texture, as a file.
        ///
        /// Not what a page draws. It is the material on the 3D ball that NavBallRenderer spins; the
        /// pages draw NavBallLive, which is that ball after rendering. Both exist because the skin
        /// has to be loadable like any other art while the result is a RenderTexture.
        /// </summary>
        NavBall = 5,
        /// <summary>
        /// The rendered attitude ball, RESOLVED AT RUNTIME from NavBallRenderer's camera.
        ///
        /// A real 3D sphere, because a navball rotates about all three axes and the flat-strip trick
        /// that draws the NAV globe is only exact viewed from the equator. See NavBallRenderer.
        /// </summary>
        NavBallLive = 6,
        /// <summary>A Crew Dragon seat. One per crew position on the SETTINGS crew card.</summary>
        Seat = 7,
        /// <summary>
        /// The live view out of the docking port, RESOLVED AT RUNTIME from DockingCamRenderer.
        ///
        /// The reference flies a 3D scene behind its HUD and ships stills as a fallback; we render a
        /// real KSP camera instead. A photograph presented as a camera feed is a lie the same way a
        /// dead control is.
        /// </summary>
        DockingCamLive = 8,
        /// <summary>Vignette over the docking view, so the HUD reads against a bright target.</summary>
        HudDarken = 9
    }

    public static class Images
    {
        /// <summary>
        /// True for an image that has no file behind it and is supplied by the engine at runtime.
        ///
        /// Both renderers need to know: the game side asks KSP for it, and the PNG preview - which
        /// has no game - must not report it as MISSING art. A silent "missing file" warning for
        /// something that was never a file is exactly the kind of misleading evidence this project
        /// has already been burned by once.
        /// </summary>
        public static bool IsRuntime(ImageId id)
        {
            return id == ImageId.BodyMap || id == ImageId.NavBallLive
                || id == ImageId.DockingCamLive;
        }

        /// <summary>File name under GameData/DragonScreen/art/. Shared by both renderers.</summary>
        public static string FileName(ImageId id)
        {
            switch (id)
            {
                case ImageId.Dragon: return "dragon.png";
                case ImageId.HudRing: return "hud_ring.png";
                case ImageId.HudRingInner: return "hud_ring_inner.png";
                case ImageId.NavBall: return "navball.png";
                case ImageId.Seat: return "seat.png";
                case ImageId.HudDarken: return "hud_darken.png";
                default: return null;
            }
        }

        /// <summary>Intrinsic pixel size. See the header for why this is declared rather than read.</summary>
        public static void Size(ImageId id, out int w, out int h)
        {
            switch (id)
            {
                case ImageId.Dragon: w = 1800; h = 3010; break;
                case ImageId.HudRing: w = 650; h = 650; break;
                case ImageId.HudRingInner: w = 650; h = 650; break;
                case ImageId.NavBall: w = 2048; h = 2048; break;
                case ImageId.Seat: w = 408; h = 520; break;
                case ImageId.HudDarken: w = 1300; h = 1300; break;
                default: w = 0; h = 0; break;
            }
        }

        /// <summary>
        /// Rect for an image scaled to a target HEIGHT, centred on (cx, cy), aspect preserved.
        ///
        /// Height-driven because that is what the layout constrains here: the Dragon has to fit
        /// between the telemetry strip and the chrome bar, and its width follows. Fitting to a width
        /// instead would let a tall asset run off the bottom of the page.
        /// </summary>
        public static bool FitHeight(ImageId id, float cx, float cy, float targetHeight,
                                     out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            int iw, ih;
            Size(id, out iw, out ih);
            if (iw <= 0 || ih <= 0 || targetHeight <= 0f) return false;

            h = targetHeight;
            w = targetHeight * ((float)iw / ih);
            x = cx - w * 0.5f;
            y = cy - h * 0.5f;
            return true;
        }
    }
}
