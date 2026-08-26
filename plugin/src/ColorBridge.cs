// DragonScreen - ColorBridge
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
