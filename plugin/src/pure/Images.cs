// DragonScreen - Images
// ---- WHY THE SIZES LIVE HERE ----
// ---- WHY AN ENUM AND NOT A PATH ----
namespace DragonScreen
{
    public enum ImageId : byte
    {
        None = 0,
        Dragon = 1,
        HudRing = 2,
        HudRingInner = 3,
        BodyMap = 4,
        NavBall = 5,
        NavBallLive = 6,
        Seat = 7,
        DockingCamLive = 8,
        HudDarken = 9,
        DontPanic = 10,  // user-supplied art/dontpanic.png, shown on the abort overlay (optional)
        /// <summary>The LIVE 3D scaled-planet render (docs/MAP_MFD_RESEARCH.md §2) - a dedicated
        /// camera copied from ScaledCamera.Instance.cam into a RenderTexture, aimed by PlanetGeom.
        /// ⚠ NOTHING SERVES IT YET: the camera is S10b (it cannot exist with the game closed), so
        /// ImageStore resolves it to null and NavPage draws PlanetGeom.NoSignalLabel instead. The id
        /// exists now so the page, the glue and the preview all agree on the seam before the renderer
        /// lands - the same idle-seam pattern src/_AutopilotStub.cs uses.</summary>
        ScaledPlanetLive = 11
    }

    public static class Images
    {
        public static bool IsRuntime(ImageId id)
        {
            return id == ImageId.BodyMap || id == ImageId.NavBallLive
                || id == ImageId.DockingCamLive || id == ImageId.ScaledPlanetLive;
        }

        // An OPTIONAL image is user-supplied — dropped into art/ by the player, NOT shipped with the mod,
        // and its declared size is only nominal. The build must not require it to exist or match a size,
        // and a missing one is fine (the overlay falls back to a plain wordmark).
        public static bool IsOptional(ImageId id) { return id == ImageId.DontPanic; }

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
                case ImageId.DontPanic: return "dontpanic.png";
                default: return null;
            }
        }

        public static void Size(ImageId id, out int w, out int h)
        {
            switch (id)
            {
                case ImageId.Dragon: w = 1800; h = 3010; break;
                case ImageId.HudRing: w = 650; h = 650; break;
                case ImageId.HudRingInner: w = 650; h = 650; break;
                case ImageId.NavBall: w = 512; h = 256; break;
                case ImageId.Seat: w = 408; h = 520; break;
                case ImageId.HudDarken: w = 1300; h = 1300; break;
                case ImageId.DontPanic: w = 1024; h = 683; break;   // nominal; the overlay scales to fit
                default: w = 0; h = 0; break;
            }
        }

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
