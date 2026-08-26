// DragonScreen — DockApproach  (autopilot rebuild L3 docking: the R-bar→V-bar L-approach FSM)
// ============================================================================================
// The real Crew Dragon proximity-ops approach (PHASE_4_DOCKING_RESEARCH + telemetry DB): up the R-bar
// to WP0 (~400 m directly BELOW), hold + crew GO to enter the keep-out sphere, swing onto the V-bar to
// WP1 (~200 m in FRONT on the docking axis), hold + GO, close to WP2 (~20 m), hold + GO for docking,
// then close to CONTACT at ~8 cm/s. Each waypoint is a station-keeping HOLD released only by a crew GO,
// which is what makes the approach abortable at every step. OFFSET-targeted; ANY UNPLANNED KOS breach
// commands an automatic ABORT (retreat). DockControl.cs flies the 6-DOF glideslope between the points.
//
// ⛔ FULL CONTROL: Guide() ALWAYS returns a unit AimLvlh — the capsule points its docking ring at the
// port the whole time; L2 holds it (attitude first), then DockControl translates.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum DockPhase : byte { Idle, WP0Hold, ToWP1, WP1Hold, ToWP2, WP2Hold, Contact, Captured, Abort }

    public struct DockInputs
    {
        public bool Valid;
        public LvlhState Rel;          // capsule relative to the station, LVLH
        public double KosRadiusM;      // 200
        public bool GoWP0, GoWP1, GoWP2;   // crew GO gates released at the holds
        public double WP0BelowM;       // 400  (−radial)
        public double WP1FrontM;       // 200  (+along, on the V-bar)
        public double WP2FrontM;       // 20
        public double ArriveTolM;      // hold-capture tolerance to consider a waypoint reached
        public bool CorridorOk;        // on the planned corridor (false → unplanned breach → abort)
    }

    public struct DockCommand
    {
        public DockPhase Phase;
        public Vec3 TargetLvlh;        // the point to fly to (DockControl closes to it)
        public Vec3 AimLvlh;           // ALWAYS unit — point the docking ring at the port (toward station)
        public bool Hold;              // station-keeping, waiting for GO
        public bool Docked;
    }

    public static class DockApproach
    {
        public const double ContactRangeM = 0.3;   // soft-capture contact

        static Vec3 Toward(LvlhState r)
        {
            // point the docking ring at the station (−relative position). Never undefined.
            Vec3 v = new Vec3(-r.Rx, -r.Ry, -r.Rz);
            return v.Magnitude > 1e-6 ? v.Normalized : new Vec3(0, 1, 0);
        }

        static Vec3 WP0(DockInputs s) { return new Vec3(-s.WP0BelowM, 0, 0); }
        static Vec3 WP1(DockInputs s) { return new Vec3(0, s.WP1FrontM, 0); }
        static Vec3 WP2(DockInputs s) { return new Vec3(0, s.WP2FrontM, 0); }
        static Vec3 Port() { return new Vec3(0, 0, 0); }

        static double DistTo(LvlhState r, Vec3 wp)
        {
            double dx = r.Rx - wp.X, dy = r.Ry - wp.Y, dz = r.Rz - wp.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static DockCommand Guide(DockInputs s, DockPhase phase)
        {
            DockCommand c = new DockCommand();
            c.Phase = phase;
            c.AimLvlh = s.Valid ? Toward(s.Rel) : new Vec3(0, 1, 0);   // ALWAYS pointed at the port
            c.TargetLvlh = WP0(s);

            if (!s.Valid) { c.Phase = DockPhase.Idle; return c; }

            // ---- ANY unplanned KOS breach aborts (retreat) — the real hard rule ----
            if (phase != DockPhase.Abort && phase != DockPhase.Captured
                && s.Rel.RangeM < s.KosRadiusM && !s.CorridorOk)
            { c.Phase = DockPhase.Abort; c.TargetLvlh = WP0(s); return c; }

            if (phase == DockPhase.Idle) phase = DockPhase.WP0Hold;

            switch (phase)
            {
                case DockPhase.WP0Hold:
                    c.Phase = DockPhase.WP0Hold; c.TargetLvlh = WP0(s); c.Hold = true;
                    if (s.GoWP0) { c.Phase = DockPhase.ToWP1; c.Hold = false; }
                    break;

                case DockPhase.ToWP1:
                    c.Phase = DockPhase.ToWP1; c.TargetLvlh = WP1(s);
                    if (DistTo(s.Rel, WP1(s)) <= s.ArriveTolM) c.Phase = DockPhase.WP1Hold;
                    break;

                case DockPhase.WP1Hold:
                    c.Phase = DockPhase.WP1Hold; c.TargetLvlh = WP1(s); c.Hold = true;
                    if (s.GoWP1) { c.Phase = DockPhase.ToWP2; c.Hold = false; }
                    break;

                case DockPhase.ToWP2:
                    c.Phase = DockPhase.ToWP2; c.TargetLvlh = WP2(s);
                    if (DistTo(s.Rel, WP2(s)) <= s.ArriveTolM) c.Phase = DockPhase.WP2Hold;
                    break;

                case DockPhase.WP2Hold:
                    c.Phase = DockPhase.WP2Hold; c.TargetLvlh = WP2(s); c.Hold = true;
                    if (s.GoWP2) { c.Phase = DockPhase.Contact; c.Hold = false; }
                    break;

                case DockPhase.Contact:
                    c.Phase = DockPhase.Contact; c.TargetLvlh = Port();   // close at contact speed (DockControl caps it)
                    if (s.Rel.RangeM <= ContactRangeM) { c.Phase = DockPhase.Captured; c.Docked = true; }
                    break;

                case DockPhase.Captured:
                    c.Phase = DockPhase.Captured; c.Docked = true; c.TargetLvlh = Port();
                    break;

                case DockPhase.Abort:
                    c.Phase = DockPhase.Abort; c.TargetLvlh = WP0(s);    // retreat back out along the approach
                    break;
            }
            return c;
        }
    }
}
