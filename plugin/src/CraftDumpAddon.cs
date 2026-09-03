/*
 * DragonScreen - CraftDumpAddon
 *
 * WHAT THIS IS: a read-only diagnostic. It exists only to give CraftDump.Auto() one caller per frame
 * in the flight scene. Our tree has no [KSPAddon] anywhere else - the only live glue, DragonScreenMonitor,
 * is an InternalModule that ticks solely while the player is in IVA, which is too fragile for a dump that
 * must fire on the pad regardless of camera. This file is excisable: delete it and CraftDump.Auto() simply
 * never runs again. It does not read or write any screen state, and it does not touch the render path.
 */
using UnityEngine;

namespace DragonScreen
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CraftDumpAddon : MonoBehaviour
    {
        private void Update()
        {
            CraftDump.Auto();
        }
    }
}
