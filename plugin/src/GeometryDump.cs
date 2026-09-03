// DragonScreen — GeometryDump  (READ-ONLY diagnostic; touches nothing in the control path)
// ============================================================================================
// Captures the RCS actuator GEOMETRY the authority estimator needs, so the "why is achievable pitch/yaw
// authority ~0 while the geometric estimate is ~62000" question can be PROVEN from real thruster data
// rather than inferred (S2 ascent tumble investigation, docs/FLIGHT_VERIFICATION.md).
//
// Isolated on purpose: its own [KSPAddon], never called by FlightDriver/AttitudeController/ControlTorque —
// it only READS the live vessel and writes a CSV. It CANNOT change what the vehicle does. Frozen-implementation
// safe (no ControlTorque / AttitudeLoop / tuning / roll-trim touched).
//
// Runs once automatically on the pad (full stack), and re-dumps on Alt+G at any time (so the S2+Dragon and
// the Dragon-alone/deorbit configs can each be captured in flight). Output:
//   <KSP>/DragonScreen_capture/geometry_dump_<why>.csv
// with, per row:
//   COM        — vessel centre of mass (world) + the ReferenceTransform (control-frame) basis in world
//   PART       — idx, name, stage, mass(t), world position   (→ reconstruct the CoM of any sub-config)
//   RCSMOD     — per ModuleRCS: stock GetPotentialTorque(pos/neg) + thrusterPower + count + enable flags
//   THRUSTER   — per thruster: world position + world thrust direction + power(kN)
// Everything needed to compute nominal (Σ r×F), achievable (control-allocation), and compare to stock +
// the flight-measured effective authority.
// ============================================================================================
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DragonScreen
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GeometryDumpProbe : MonoBehaviour
    {
        const string Tag = "[DragonScreen] ";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        bool padDumped;

        public void Update()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.parts == null || v.parts.Count == 0) return;

            // Auto once on the pad (full stack), then let the user re-trigger in any flight config with Alt+G.
            if (!padDumped && v.situation == Vessel.Situations.PRELAUNCH)
            {
                padDumped = true;
                Dump(v, "pad");
            }
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.G))
                Dump(v, "manual_" + Mathf.RoundToInt((float)v.missionTime) + "s");
        }

        void Dump(Vessel v, string why)
        {
            try
            {
                string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "DragonScreen_capture");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "geometry_dump_" + why + ".csv");

                StringBuilder sb = new StringBuilder(1 << 16);
                sb.Append("row,part_idx,part_name,stage,mass_t,ax,ay,az,bx,by,bz,power_kn,eP,eY,eR,useZ\n");

                Transform ctf = v.ReferenceTransform;
                Vector3 com = v.CoM;
                // COM row: a=world CoM, b=(control-frame basis packed across the next 3 fields is not enough,
                // so emit the ReferenceTransform right/up/forward as their own THREE rows for a full basis).
                Row(sb, "COM", -1, v.vesselName, 0, 0.0, com.x, com.y, com.z, 0, 0, 0, 0, 0, 0, 0, 0);
                if (ctf != null)
                {
                    Row(sb, "REF_RIGHT",   -1, "ctf.right",   0, 0, ctf.right.x,   ctf.right.y,   ctf.right.z,   0,0,0, 0,0,0,0,0);
                    Row(sb, "REF_UP",      -1, "ctf.up",      0, 0, ctf.up.x,      ctf.up.y,      ctf.up.z,      0,0,0, 0,0,0,0,0);
                    Row(sb, "REF_FORWARD", -1, "ctf.forward", 0, 0, ctf.forward.x, ctf.forward.y, ctf.forward.z, 0,0,0, 0,0,0,0,0);
                }

                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    if (p == null || p.transform == null) continue;
                    string pn = p.partInfo != null ? p.partInfo.name : p.name;
                    double massT = p.mass + p.GetResourceMass();
                    Vector3 pp = p.transform.position;
                    Row(sb, "PART", i, pn, p.inverseStage, massT, pp.x, pp.y, pp.z, 0,0,0, 0,0,0,0,0);

                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        ModuleRCS rcs = p.Modules[m] as ModuleRCS;
                        if (rcs == null) continue;
                        int eP = BoolF(rcs, "enablePitch"), eY = BoolF(rcs, "enableYaw"), eR = BoolF(rcs, "enableRoll");
                        float powerKn = rcs.thrusterPower * Math.Max(rcs.thrustPercentage * 0.01f, 0f);
                        int count = rcs.thrusterTransforms != null ? rcs.thrusterTransforms.Count : 0;

                        // stock GetPotentialTorque — KSP's own (achievability-aware) authority estimate.
                        Vector3 gpos = Vector3.zero, gneg = Vector3.zero;
                        try { rcs.GetPotentialTorque(out gpos, out gneg); } catch { }
                        // pack stock pos in a-fields, stock neg in b-fields.
                        Row(sb, "RCSMOD", i, pn, p.inverseStage, count,
                            gpos.x, gpos.y, gpos.z, gneg.x, gneg.y, gneg.z,
                            powerKn, eP, eY, eR, rcs.useZaxis ? 1 : 0);

                        if (rcs.thrusterTransforms == null) continue;
                        for (int t = 0; t < rcs.thrusterTransforms.Count; t++)
                        {
                            Transform tt = rcs.thrusterTransforms[t];
                            if (tt == null) continue;
                            Vector3 tp = tt.position;
                            Vector3 tdir = rcs.useZaxis ? -tt.forward : -tt.up;   // matches ControlTorque
                            Row(sb, "THRUSTER", i, pn, p.inverseStage, 0,
                                tp.x, tp.y, tp.z, tdir.x, tdir.y, tdir.z,
                                powerKn, eP, eY, eR, rcs.useZaxis ? 1 : 0);
                        }
                    }
                }

                File.WriteAllText(path, sb.ToString());
                Debug.Log(Tag + "GEOMETRY DUMP (" + why + ") -> " + path + "  (" + v.parts.Count
                          + " parts, " + v.vesselName + ", mass " + (v.totalMass).ToString("F1") + " t)");
                ScreenMessages.PostScreenMessage("DragonScreen geometry dump: " + why, 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception e) { Debug.LogWarning(Tag + "geometry dump failed: " + e.Message); }
        }

        static int BoolF(PartModule pm, string name)
        {
            try { object o = pm.Fields[name].GetValue(pm); return (o is bool && (bool)o) ? 1 : 0; }
            catch { return -1; }   // -1 = field absent
        }

        void Row(StringBuilder sb, string row, int idx, string name, int stage, double mass,
                 double ax, double ay, double az, double bx, double by, double bz,
                 double powerKn, int eP, int eY, int eR, int useZ)
        {
            sb.Append(row).Append(',').Append(idx).Append(',');
            sb.Append((name ?? "-").Replace(',', ';')).Append(',').Append(stage).Append(',');
            sb.Append(mass.ToString("G6", Inv)).Append(',');
            sb.Append(ax.ToString("G7", Inv)).Append(',').Append(ay.ToString("G7", Inv)).Append(',').Append(az.ToString("G7", Inv)).Append(',');
            sb.Append(bx.ToString("G7", Inv)).Append(',').Append(by.ToString("G7", Inv)).Append(',').Append(bz.ToString("G7", Inv)).Append(',');
            sb.Append(powerKn.ToString("G6", Inv)).Append(',');
            sb.Append(eP).Append(',').Append(eY).Append(',').Append(eR).Append(',').Append(useZ).Append('\n');
        }
    }
}
