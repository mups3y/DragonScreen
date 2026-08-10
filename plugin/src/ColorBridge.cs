/*
 * DragonScreen - ColorBridge
 *
 * THE ONLY PLACE THE TWO TYPE SYSTEMS MEET.
 *
 * src/pure is deliberately free of UnityEngine, so the palette is stored as Rgba (four plain floats)
 * and can be built and tested with the game closed. Every draw call needs a UnityEngine.Color. One
 * function, one job; if a second conversion appears anywhere else, delete it and call this instead.
 */
using UnityEngine;

namespace DragonScreen
{
    internal static class ColorBridge
    {
        public static Color To(Rgba c)
        {
            return new Color(c.R, c.G, c.B, c.A);
        }
    }
}
