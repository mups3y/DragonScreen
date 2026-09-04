// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
namespace MuMech.AttitudeControllers
{
    public class TorquePI
    {
        private KosPIDLoop _loop { get; } = new KosPIDLoop();

        public double Update(double input, double setpoint, double momentOfInertia, double maxOutput)
        {
            _loop.Ki = 4 * momentOfInertia;
            _loop.Kp = 4 * momentOfInertia;
            return _loop.Update(input, setpoint, maxOutput);
        }

        public void ResetI() => _loop.ResetI();
    }
}

}
