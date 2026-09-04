// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using System;
using MechJebLib.Primitives;

namespace MechJebLib.ODE
{
    public abstract class DenseNode : IDisposable
    {
        public double T; // left endpoint
        public int N;
        public double H; // signed step (Habs * Direction)

        // ReSharper disable once NullableWarningSuppressionIsUsed
        public Vec Y = null!; // y at T

        public abstract void Evaluate(double t, Vec yout);

        public virtual void Dispose() => Y.Dispose();
    }

    /// <summary>
    ///     This is a fake "interpolant" for zero-length t0 == tf "integration".
    /// </summary>
    public class ConstantNode : DenseNode
    {
        public ConstantNode(double t, Vec y)
        {
            T = t;
            Y = y.Dup();
            N = y.Length;
            H = 0;
        }

        public override void Evaluate(double t, Vec yout) => yout.CopyFrom(Y);
    }
}

}
