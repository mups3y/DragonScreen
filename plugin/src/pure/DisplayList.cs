/*
 * DragonScreen - DisplayList
 *
 * PURE. No KSP, no Unity, no System.Drawing. A page describes itself by filling one of these; a
 * RENDERER then walks it. Two renderers exist and must never disagree:
 *
 *      ScreenPainter        GL immediate mode -> RenderTexture -> the IVA screen
 *      preview/PreviewMain  System.Drawing    -> PNG           -> looked at with the game CLOSED
 *
 * ---- WHY THIS EXISTS: RESTARTS ARE THE SCARCE RESOURCE ----
 * A DLL change costs a full KSP restart, and so does a cfg change. Page design is where the
 * iteration count explodes - proportions, palette, whether a gauge reads at a glance - and almost
 * none of that needs the game. With one description and two renderers, the look is iterated in
 * seconds against a PNG, and a restart is spent only on what genuinely needs the capsule: material
 * behaviour, anti-aliasing, legibility at IVA distance, and input.
 *
 * ---- THIS IS NOT A PAGE ENGINE, AND THE DIFFERENCE IS NOT SUBTLE ----
 * Pages are C#. This is an in-memory structure that C# page code EMITS. It has no parser, no file
 * format, and nothing here is authored by hand. The day it wants to be serialised into a .cfg that
 * someone edits, it has turned into the thing this project explicitly rejected - see CLAUDE.md,
 * "Where the cfg line is".
 *
 * ---- NO ALLOCATION IN THE DRAW PATH ----
 * Fixed capacity, filled by Clear() then refill. The command buffer is built once per screen and
 * reused every frame, because this runs three times a frame forever.
 */
using System;

namespace DragonScreen
{
    public enum DrawKind : byte
    {
        /// <summary>Filled rectangle. A=x B=y C=width D=height.</summary>
        Rect = 0,
        /// <summary>Filled annulus segment. A=cx B=cy C=innerRadius D=outerRadius, plus the angles.</summary>
        ArcBand = 1,
        /// <summary>Text. A=x B=y(TOP of the line) C=pixel size, Str=the string, Align=horizontal.</summary>
        Text = 2,
        /// <summary>Bitmap. A=x B=y C=width D=height, Image=which one. Colour TINTS it.</summary>
        Image = 3,
        /// <summary>Straight line. A=x0 B=y0 C=x1 D=y1, StartDeg=stroke width. A real primitive (a
        /// rotated quad in the GL painter, a pen in the preview) so a track can be a solid line rather
        /// than a row of dots - the one shape a Rect cannot make because it is not axis-aligned.</summary>
        Line = 4
    }

    /// <summary>
    /// Horizontal placement of a text command relative to its x.
    ///
    /// There is no vertical variant on purpose: y is always the TOP of the line, matching Rect and
    /// the top-left origin both renderers use. A baseline option would be a second convention for
    /// the same number, and this project has already paid for one of those.
    /// </summary>
    public enum TextAlign : byte { Left = 0, Centre = 1, Right = 2 }

    /// <summary>
    /// One drawing command. A struct in a flat array on purpose: a class per command would allocate
    /// per page rebuild, which is the trap this project has already caught three times in OnGUI.
    ///
    /// The fields are deliberately named A..D rather than x/y/w/h, because they mean different
    /// things per kind and naming them for one kind makes the other read as a bug.
    /// </summary>
    public struct DrawCmd
    {
        public DrawKind Kind;
        public float A, B, C, D;
        public float StartDeg, EndDeg;
        public Rgba Colour;

        /// <summary>Text only. Null for every other kind.</summary>
        public string Str;
        public TextAlign Align;
        /// <summary>Image only. ImageId.None for every other kind.</summary>
        public ImageId Image;

        /// <summary>Image only: a NAMED external asset (a Figma-exported PNG in art/cover/&lt;key&gt;.png),
        /// used by the pixel-exact rebuilt pages. Null for a built-in ImageId or any non-image kind.</summary>
        public string AssetKey;

        /// <summary>Image only: clip the bitmap to the CIRCLE inscribed in its rect. The navball reads as
        /// a circle for free (its render has transparent corners), but an OPAQUE feed - the docking
        /// camera - would show a square where a ball goes, so the caller asks for a mask. The renderers
        /// paint the square-minus-circle corners with <see cref="ClipBg"/> (the colour that sits behind
        /// the disc), which is how both stay in step without a scissor rect neither one has.</summary>
        public bool CircleClip;
        /// <summary>Image only, CircleClip: the colour the disc sits on, used to mask the corners.</summary>
        public Rgba ClipBg;

        /// <summary>
        /// Image only: which PART of the texture to use. UMin/UMax left to right; VMin is the
        /// texture's BOTTOM edge and VMax its top, because that is texture space's own convention and
        /// both renderers already map a rect's top edge to the larger v.
        ///
        /// Full-image draws set 0,1,0,1. NAV is what needs anything else: the body map is panned and
        /// zoomed by choosing a window of the texture rather than by clipping the quad, because
        /// neither renderer has a scissor rect and giving each one its own would be two things to
        /// keep in step. See MapProjection.BodyQuads.
        /// </summary>
        public float UMin, UMax, VMin, VMax;
    }

    public sealed class DisplayList
    {
        private readonly DrawCmd[] cmds;
        private int count;

        /// <summary>
        /// True if a command was dropped for want of capacity.
        ///
        /// DROPPING BEATS THROWING AND BEATS GROWING. Throwing takes the screen out mid-flight over
        /// a cosmetic overflow; growing allocates in the draw path, silently, exactly where it costs
        /// most. Dropping degrades visibly - part of the page is missing - and this flag says so, so
        /// a renderer or a test can report it once rather than the pilot wondering.
        /// </summary>
        public bool Overflowed { get; private set; }

        public DisplayList(int capacity)
        {
            if (capacity < 1) capacity = 1;
            cmds = new DrawCmd[capacity];
        }

        public int Capacity { get { return cmds.Length; } }
        public int Count { get { return count; } }
        public DrawCmd At(int i) { return cmds[i]; }

        public void Clear()
        {
            count = 0;
            Overflowed = false;
        }

        private void Add(DrawCmd c)
        {
            if (count >= cmds.Length) { Overflowed = true; return; }
            cmds[count++] = c;
        }

        /// <summary>Filled rectangle, top-left origin, y increasing downward.</summary>
        public void Rect(float x, float y, float w, float h, Rgba colour)
        {
            DrawCmd c = new DrawCmd();
            c.Kind = DrawKind.Rect;
            c.A = x; c.B = y; c.C = w; c.D = h;
            c.Colour = colour;
            Add(c);
        }

        /// <summary>
        /// Unfilled rectangle as four rects of the given stroke width.
        ///
        /// Not a primitive: GL has no line width worth trusting and neither renderer should be
        /// guessing one. Four explicit quads are exactly as wide as they say they are.
        /// </summary>
        public void Box(float x, float y, float w, float h, float stroke, Rgba colour)
        {
            if (stroke <= 0f) return;
            Rect(x, y, w, stroke, colour);
            Rect(x, y + h - stroke, w, stroke, colour);
            Rect(x, y, stroke, h, colour);
            Rect(x + w - stroke, y, stroke, h, colour);
        }

        /// <summary>
        /// A line of text. <paramref name="y"/> is the TOP of the line; <paramref name="pixelSize"/>
        /// is the font size in render-target pixels.
        ///
        /// ---- THE STRING MUST ALREADY EXIST ----
        /// Pass a literal or a CACHED string. Formatting a number here - "ALT " + alt.ToString("F1") -
        /// allocates a string every frame, on three screens, forever. A page that shows changing
        /// values must keep its formatted text and rebuild it only when the value actually changes.
        /// This is the same rule as the vertex buffers, and text is where it will be broken first.
        ///
        /// ---- WIDTH IS NOT KNOWN HERE, AND THAT IS A REAL LIMIT ----
        /// Measuring a string needs a font, and a font is exactly the kind of engine dependency
        /// src/pure refuses. So a page can ANCHOR text (left/centre/right of a point) but cannot yet
        /// lay out relative to how wide another string turned out. When something needs that, the
        /// answer is a measurement interface the two renderers implement - NOT a font in here.
        /// </summary>
        public void Text(string text, float x, float y, float pixelSize,
                         TextAlign align, Rgba colour)
        {
            if (string.IsNullOrEmpty(text) || pixelSize <= 0f) return;
            DrawCmd c = new DrawCmd();
            c.Kind = DrawKind.Text;
            c.A = x; c.B = y; c.C = pixelSize;
            c.Str = text;
            c.Align = align;
            c.Colour = colour;
            Add(c);
        }

        /// <summary>
        /// Draw a bitmap into the given rect.
        ///
        /// The colour TINTS - white leaves the artwork alone, and anything else multiplies it. That
        /// is how a shared image serves an active and an inactive state without shipping two files,
        /// and it is why the parameter is not optional: a page should have to say it means "as
        /// drawn".
        ///
        /// Aspect is the CALLER's problem, via Images.FitHeight - a renderer silently letterboxing
        /// would hide a layout mistake instead of showing it.
        /// </summary>
        public void Image(ImageId id, float x, float y, float w, float h, Rgba tint)
        {
            ImageUV(id, x, y, w, h, 0f, 1f, 0f, 1f, tint);
        }

        /// <summary>
        /// Draw a NAMED external asset (a Figma-exported PNG in art/cover/&lt;key&gt;.png) into the rect.
        /// Used by the pixel-exact rebuilt pages, which place the design's own PNG assets at their
        /// measured positions rather than reproducing them with primitives. Same tint rule as Image.
        /// </summary>
        public void Asset(string key, float x, float y, float w, float h, Rgba tint)
        {
            if (string.IsNullOrEmpty(key) || w <= 0f || h <= 0f) return;
            DrawCmd c = new DrawCmd();
            c.Kind = DrawKind.Image;
            c.A = x; c.B = y; c.C = w; c.D = h;
            c.AssetKey = key;
            c.Colour = tint;
            c.UMin = 0f; c.UMax = 1f; c.VMin = 0f; c.VMax = 1f;
            Add(c);
        }

        /// <summary>
        /// Draw a bitmap clipped to the CIRCLE inscribed in its rect. Same tint rule as Image; the
        /// square-minus-circle corners are masked with <paramref name="clipBg"/> (the colour behind the
        /// disc) so an OPAQUE feed - the docking camera - reads as a ball, not a square. See
        /// DrawCmd.CircleClip.
        /// </summary>
        public void ImageCircle(ImageId id, float x, float y, float w, float h, Rgba tint, Rgba clipBg)
        {
            if (id == ImageId.None || w <= 0f || h <= 0f) return;
            DrawCmd c = new DrawCmd();
            c.Kind = DrawKind.Image;
            c.A = x; c.B = y; c.C = w; c.D = h;
            c.Image = id;
            c.Colour = tint;
            c.UMin = 0f; c.UMax = 1f; c.VMin = 0f; c.VMax = 1f;
            c.CircleClip = true; c.ClipBg = clipBg;
            Add(c);
        }

        /// <summary>
        /// Draw part of a bitmap. See DrawCmd's UV fields for the convention and for why this exists
        /// rather than a clip rectangle.
        /// </summary>
        public void ImageUV(ImageId id, float x, float y, float w, float h,
                            float uMin, float uMax, float vMin, float vMax, Rgba tint)
        {
            if (id == ImageId.None || w <= 0f || h <= 0f) return;
            DrawCmd c = new DrawCmd();
            c.Kind = DrawKind.Image;
            c.A = x; c.B = y; c.C = w; c.D = h;
            c.Image = id;
            c.Colour = tint;
            c.UMin = uMin; c.UMax = uMax; c.VMin = vMin; c.VMax = vMax;
            Add(c);
        }

        /// <summary>
        /// A straight line from (x0,y0) to (x1,y1) of the given stroke width. Unlike Box's four rects,
        /// this is ONE primitive that can run at any angle - drawn as a rotated quad by the GL painter
        /// and a round-capped pen by the preview, so both render the same solid stroke. Used for the
        /// ground track and the 3D orbit, which are polylines at arbitrary angles.
        /// </summary>
        public void Line(float x0, float y0, float x1, float y1, float width, Rgba colour)
        {
            if (width <= 0f) return;
            DrawCmd c = new DrawCmd();
            c.Kind = DrawKind.Line;
            c.A = x0; c.B = y0; c.C = x1; c.D = y1;
            c.StartDeg = width;
            c.Colour = colour;
            Add(c);
        }

        /// <summary>
        /// Filled band between two radii, swept from startDeg to endDeg.
        /// Angles are instrument convention - 0 at twelve o'clock, increasing clockwise - because
        /// that is how ArcGeometry defines them and there is exactly one convention in this codebase.
        /// </summary>
        public void ArcBand(float cx, float cy, float rInner, float rOuter,
                            double startDeg, double endDeg, Rgba colour)
        {
            DrawCmd c = new DrawCmd();
            c.Kind = DrawKind.ArcBand;
            c.A = cx; c.B = cy; c.C = rInner; c.D = rOuter;
            c.StartDeg = (float)startDeg; c.EndDeg = (float)endDeg;
            c.Colour = colour;
            Add(c);
        }
    }
}
