// ===========================================================
// Assets/SquashSim/Lab/SimulationClock.cs
// Port of lab/core/SimulationTimeController.js. The ONLY thing that
// decides when the simulation advances: fixed dt, time scale, pause,
// single-step, and a cap on steps per frame.
//
// Slow motion feeds the accumulator less real time - it never shrinks
// the timestep - so a slowed run and a full-speed run take exactly the
// same steps. Alpha (accumulator / dt) is exposed for render
// interpolation; it never feeds back into the simulation.
// ===========================================================

using System;

namespace SquashSim.Lab
{
    public sealed class SimulationClock
    {
        readonly Action<double> _onStep;
        double _accumulator;
        int _pendingSingleSteps;

        public double TimeScale = 1.0;
        public bool Paused { get; private set; }
        public int StepsLastFrame { get; private set; }
        public long TotalSteps { get; private set; }
        public double SimulatedTime { get; private set; }
        public bool Clamped { get; private set; }

        public SimulationClock(Action<double> onStep) { _onStep = onStep; }

        /// <summary>0..1: how far real time has run past the last fixed step (render interpolation).</summary>
        public double Alpha
        {
            get
            {
                double dt = PhysicsConfig.Current.Sim.FixedDt;
                return Paused || dt <= 0 ? 1.0 : DMath.Min(1.0, _accumulator / dt);
            }
        }

        /// <summary>Consume one frame of REAL elapsed time (unscaled seconds). Returns steps taken.</summary>
        public int Advance(double rawDt)
        {
            double fixedDt = PhysicsConfig.Current.Sim.FixedDt;
            int maxSteps = PhysicsConfig.Current.Sim.MaxStepsPerFrame;
            Clamped = false;
            int steps = 0;

            if (Paused)
            {
                _accumulator = 0;
                while (_pendingSingleSteps > 0 && steps < maxSteps)
                {
                    _pendingSingleSteps--;
                    DoStep(fixedDt);
                    steps++;
                }
            }
            else
            {
                double cappedRaw = DMath.Min(rawDt, 0.25);
                _accumulator += cappedRaw * TimeScale;
                while (_accumulator >= fixedDt)
                {
                    if (steps >= maxSteps)
                    {
                        _accumulator = 0;
                        Clamped = true;
                        break;
                    }
                    _accumulator -= fixedDt;
                    DoStep(fixedDt);
                    steps++;
                    if (Paused) { _accumulator = 0; break; }          // paused by a step (a shot puzzle's freeze): no more this frame
                }
            }
            StepsLastFrame = steps;
            return steps;
        }

        void DoStep(double dt)
        {
            _onStep(dt);
            TotalSteps++;
            SimulatedTime += dt;
        }

        public void Pause() { Paused = true; }
        public void Resume() { Paused = false; _accumulator = 0; }
        public bool TogglePause() { if (Paused) Resume(); else Pause(); return Paused; }

        /// <summary>Advance exactly one fixed step. Implies pause.</summary>
        public void StepOnce() { Paused = true; _pendingSingleSteps++; }

        /// <summary>Drop accumulated real time (after a reset).</summary>
        public void Flush() { _accumulator = 0; _pendingSingleSteps = 0; }
    }
}
