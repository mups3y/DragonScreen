// DragonScreen — LaunchTuner  (autopilot rebuild B9: the GravityTurn LaunchDB ascent-shape auto-tuner)
// ============================================================================================
// GravityTurn's whole value: a persistent optimizer that improves the launch ACROSS flights with zero human
// tuning (linuxgurugamer/GravityTurn `LaunchDB`: BestSettings → GuessSettings → RecordLaunch → Save). It
// stores each launch's (shape params → resulting loss) and refines toward the minimum. We adopt it to self-
// tune OUR gravity-turn shape (Ascent.TurnStartVMps / FinalPitchDeg / TurnShape …) against the AscentLoss
// objective, so the ascent tunes itself over flights and the hand-set pitch constants retire into L6 self-cal.
//
// The optimizer is a deterministic COORDINATE hill-climb (a line-search per parameter): try one parameter at a
// time, nudged ± the current step; keep any improvement and keep going that way (momentum); when neither
// direction improves, shrink that parameter's step and move to the next; converge when every step has shrunk
// below its floor. Deterministic + objective-agnostic (it takes a scalar loss) so it is trivially headless-
// tested — replayed against a synthetic loss it must walk to the minimum — and can be replayed against the
// recorded ascent corpus. The GLUE owns persistence (best params ↔ PluginData/learned.cfg, our LearnedParams).
// ============================================================================================
using System;

namespace DragonScreen
{
    public class LaunchTuner
    {
        [Tunable] public static double StepShrink = 0.5;   // shrink a parameter's search step by this each time
                                                           // both its directions fail to improve

        public readonly int N;
        private readonly double[] _min, _max, _floor;
        public readonly double[] Best;      // best params found so far (the ones to fly when settled)
        public readonly double[] Step;      // per-parameter current search step
        public double BestLoss = double.PositiveInfinity;
        public int Launches;
        public bool Converged;

        private int _cursor;                // which parameter is being tuned
        private int _dir = +1;              // current search direction (+1 tried first, then −1)

        public LaunchTuner(double[] initial, double[] min, double[] max, double[] step0, double[] floor)
        {
            if (initial == null || min == null || max == null || step0 == null || floor == null)
                throw new ArgumentNullException("LaunchTuner: all arrays are required");
            N = initial.Length;
            if (min.Length != N || max.Length != N || step0.Length != N || floor.Length != N)
                throw new ArgumentException("LaunchTuner: array lengths must all equal " + N);
            Best  = (double[])initial.Clone();
            _min  = (double[])min.Clone();
            _max  = (double[])max.Clone();
            Step  = (double[])step0.Clone();
            _floor = (double[])floor.Clone();
        }

        // The parameters to fly on the next launch. Before any launch has been scored (or once converged) it
        // returns the current best; otherwise the best with the active parameter nudged in the active direction.
        public double[] NextTrial()
        {
            double[] t = (double[])Best.Clone();
            if (double.IsPositiveInfinity(BestLoss) || Converged) return t;
            t[_cursor] = Clamp(Best[_cursor] + _dir * Step[_cursor], _min[_cursor], _max[_cursor]);
            return t;
        }

        // Fold in a flown result (the params that were flown and the loss they produced).
        public void Record(double[] triedParams, double loss)
        {
            Launches++;

            if (loss < BestLoss)
            {
                BestLoss = loss;
                for (int i = 0; i < N; i++) Best[i] = triedParams[i];
                return;                     // improvement → keep tuning this parameter in this direction
            }

            if (_dir == +1) { _dir = -1; return; }   // no improvement → try the other direction next

            // both directions of this parameter failed to beat the best → shrink its step, move to the next
            _dir = +1;
            Step[_cursor] *= StepShrink;
            AdvanceCursor();
        }

        private void AdvanceCursor()
        {
            for (int scanned = 0; scanned < N; scanned++)
            {
                _cursor = (_cursor + 1) % N;
                if (Step[_cursor] >= _floor[_cursor]) return;   // this parameter can still move
            }
            Converged = true;              // every parameter's step is below its floor → settled
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
