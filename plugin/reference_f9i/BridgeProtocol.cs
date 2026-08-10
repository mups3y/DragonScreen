/*
 * F9IScreen - BridgeProtocol
 *
 * PURE. No KSP, no Unity. This is the wire format between kOS and the panel, and it lives here
 * precisely because parsing is where a bridge goes wrong silently - a field shifted by one is not a
 * crash, it is a screen quietly showing the wrong number for the rest of the flight.
 *
 * ---- WHY ONE PACKED FIELD AND NOT THIRTEEN SEPARATE ONES ----
 * kOS reads and writes a PartModule field with GETFIELD/SETFIELD, and it REQUIRES guiActive = true to
 * do so - which means every exposed field also becomes a row in the part's right-click menu. F9_LOP
 * hit exactly this and had to narrow its ModuleManager patch to two pod names to avoid 16 rows on 11
 * parts. Thirteen fields here would be thirteen rows.
 *
 * One packed string is ONE row, and - more importantly - ONE SETFIELD call per update instead of
 * thirteen. kOS cost is the entire reason this port exists, so the cheaper side wins.
 *
 * ---- THE FORMAT ----
 *     <version>~|~<msg1>~|~<msg2>~|~<msg3>~|~<program>~|~<page>~|~<dgPhase>~|~<stPhase>
 *     ~|~<dockingmode>~|~<fdGO>~|~<landProfile>~|~<missionName>~|~<missionTimer>
 *
 * "~|~" as the separator because kOS has NO escape sequences - a delimiter needing escaping would be
 * unwritable on the sending side. Three characters, and no F9I message has ever contained that run.
 *
 * ---- TOLERANCE IS DELIBERATE ----
 * Unpack NEVER throws and never returns a half-filled state. A malformed or truncated payload leaves
 * the previous good state on screen and raises a flag, because a stale reading the pilot can see is
 * far safer than a blank panel or an exception storm mid-docking.
 */
using System;

namespace F9IScreen
{
    /// <summary>Everything kOS pushes to the panel, in one snapshot.</summary>
    public class BridgeState
    {
        public string Message1 = "";
        public string Message2 = "";
        public string Message3 = "";

        public string Program = "None";     // runningprogram
        public string Page = "";            // shownPage
        public string DragonPhase = "";     // dgPhase
        public string StationPhase = "";    // stPhase
        public string DockingMode = "";     // dockingmode

        public bool FlightDirectorGo = true;   // fdGO
        public int LandProfile = 1;            // fdLandProfile: 1 RTLS, 2 ASDS, 3/6 expendable
        public string MissionName = "";
        public double MissionTimer;

        /// <summary>True once a well-formed payload has been received at least once.</summary>
        public bool Valid;
        /// <summary>Count of payloads rejected as malformed. Surfaced on screen; 0 is the healthy value.</summary>
        public int RejectCount;
    }

    public static class BridgeProtocol
    {
        public const string Separator = "~|~";

        /// <summary>Bump ONLY on a breaking field-order change; the panel refuses mismatched versions.</summary>
        public const int Version = 1;

        /// <summary>Field count including the leading version.</summary>
        public const int FieldCount = 13;

        /// <summary>
        /// Build a payload. Used by the tests, and by anything that wants to simulate kOS without the
        /// game running - which is how the parser gets exercised at all.
        /// </summary>
        public static string Pack(BridgeState s)
        {
            if (s == null) return "";
            string[] f = new string[FieldCount];
            f[0]  = Version.ToString();
            f[1]  = Clean(s.Message1);
            f[2]  = Clean(s.Message2);
            f[3]  = Clean(s.Message3);
            f[4]  = Clean(s.Program);
            f[5]  = Clean(s.Page);
            f[6]  = Clean(s.DragonPhase);
            f[7]  = Clean(s.StationPhase);
            f[8]  = Clean(s.DockingMode);
            f[9]  = s.FlightDirectorGo ? "1" : "0";
            f[10] = s.LandProfile.ToString();
            f[11] = Clean(s.MissionName);
            f[12] = s.MissionTimer.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            return string.Join(Separator, f);
        }

        /// <summary>
        /// Strip any separator that sneaks into a value. The sender should never emit one, but a
        /// message line is free text assembled from flight data and this is the cheap guarantee that
        /// one stray run of characters cannot shift every later field by one.
        /// </summary>
        private static string Clean(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return v.Contains(Separator) ? v.Replace(Separator, " ") : v;
        }

        /// <summary>
        /// Parse a payload into <paramref name="into"/>.
        /// Returns true if it was applied. On false, `into` is UNCHANGED apart from RejectCount - the
        /// panel keeps showing the last good values.
        /// </summary>
        public static bool Unpack(string payload, BridgeState into)
        {
            if (into == null) return false;
            if (string.IsNullOrEmpty(payload)) { into.RejectCount++; return false; }

            string[] f = payload.Split(new string[] { Separator }, StringSplitOptions.None);
            if (f.Length != FieldCount) { into.RejectCount++; return false; }

            int ver;
            if (!int.TryParse(f[0], out ver) || ver != Version) { into.RejectCount++; return false; }

            // Past this point the shape is known good, so the assignments cannot half-apply.
            into.Message1     = f[1];
            into.Message2     = f[2];
            into.Message3     = f[3];
            into.Program      = f[4];
            into.Page         = f[5];
            into.DragonPhase  = f[6];
            into.StationPhase = f[7];
            into.DockingMode  = f[8];
            into.FlightDirectorGo = (f[9] == "1");

            int lp;
            into.LandProfile = int.TryParse(f[10], out lp) ? lp : into.LandProfile;

            into.MissionName = f[11];

            // ---- THE SENDER IS kOS, AND kOS DOES NOT FORMAT INVARIANT ----
            // Pack formats the timer with InvariantCulture so C# can never emit "1234,5". That guard
            // protects the wrong side: Pack only runs in the tests, while the real payload is built by
            // string concatenation in falcon9.ks, where kOS renders the scalar with whatever culture
            // the process happens to be running under. On a comma-decimal locale that lands here as
            // "1234,5", TryParse refuses it, and - because a failed parse deliberately leaves the old
            // value alone - the mission clock silently freezes at whatever it last read. No exception,
            // no reject count, just a number that stops.
            // Normalising here rather than in kOS keeps the fix on the side that has tests, and a
            // comma can never be anything BUT a decimal separator in this field: it holds one scalar,
            // never a list, and never a thousands-separated string.
            // NumberStyles.Float already covers the other kOS rendering worth worrying about -
            // exponent notation on a large game time, e.g. "1.2345E+07".
            string rawTimer = f[12];
            if (rawTimer.IndexOf(',') >= 0) rawTimer = rawTimer.Replace(',', '.');

            double mt;
            if (double.TryParse(rawTimer, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out mt))
                into.MissionTimer = mt;

            into.Valid = true;
            return true;
        }

        /// <summary>
        /// Human-readable land profile. Kept here rather than in the drawing code so the mapping has
        /// one home - BOOTER.ks, PARAM.ks and the panel must agree, and 3 being expendable rather
        /// than a landing profile is exactly the sort of thing that drifts (see audit issue #55).
        /// </summary>
        public static string LandProfileName(int lp)
        {
            switch (lp)
            {
                case 1: return "RTLS";
                case 2: return "DRONESHIP";
                case 3: return "EXPENDABLE";
                case 6: return "EXPENDABLE";
                default: return "?" + lp;
            }
        }
    }
}
