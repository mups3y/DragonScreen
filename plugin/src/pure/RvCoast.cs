// DragonScreen — RvCoast  (PURE: the far-field rendezvous attitude gate — Campaign 1 / C2a)
// ============================================================================================
// The one-line decision the rendezvous far field uses to STOP wasting RCS holding prograde. On a no-reaction-
// wheel vehicle, holding prograde to the ~0.1° loop deadband while prograde rotates ~0.06°/s re-fires the Dracos
// roughly every 1.7 s — so 69% of the realtime far-field RCS firing was attitude-only (CSV 155116), draining the
// MMH/NTO the mission needs to come home. Instead: hold prograde tight ONLY when actually burning; on a coast,
// re-acquire prograde only after drifting past a band, then release the channel and drift. The band keeps the
// nose within ~ReacquireDeg of prograde (kept < the 5° burn gate so a re-acquire leaves us burn-ready), at a
// fraction of the RCS. Pure + headless-tested so the intent can't silently regress.
// ⛔ FAR-FIELD ONLY. Near-field CW points the burn axis, which is off-prograde by design — never gate that here.
namespace DragonScreen
{
    public static class RvCoast
    {
        // true → hold prograde (call Steering.Point); false → release the attitude channel (drift, no RCS).
        public static bool HoldPrograde(bool burning, double pointErrDeg, double reacquireDeg)
        {
            return burning || pointErrDeg > reacquireDeg;
        }
    }
}
