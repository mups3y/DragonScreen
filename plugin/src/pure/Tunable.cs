/*
 * DragonScreen - Tunable
 *
 * PURE. The marker only. A `public static` field tagged `[Tunable]` is discovered by reflection at
 * flight start and can be overridden live from `PluginData/tuning.cfg` - see `src/Tuning.cs` for the
 * loader. This attribute lives in `src/pure` on purpose: the pure files carry the flight constants
 * and the headless tests compile `src/pure` ONLY, so the marker has to be visible there. It is a bare
 * `System.Attribute` with no Unity or KSP dependency, so it does not compromise that.
 */
using System;

namespace DragonScreen
{
    /// <summary>Marks a `public static` field as live-tunable via PluginData/tuning.cfg.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TunableAttribute : Attribute { }
}
