// ===========================================================
// Assets/SquashSim.Testing/ReplayRunner.cs
// Drives SquashSim.Core through a Replay exactly the way the lab's
// recorder drove the JS simulation, producing one frame per step.
// Shared by SimParity (.NET 8) and the Unity EditMode tests (Mono).
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim.Lab;

namespace SquashSim.Testing
{
    public sealed class ReplayResult
    {
        public string Name;
        public int Steps;
        public int FirstMismatch = -1;   // -1 = every step identical
        public int Impacts;
        public int RacketContacts;
        public bool Identical { get { return FirstMismatch < 0; } }
    }

    public sealed class ReplayStepper
    {
        readonly Replay _r;
        readonly double _dt;
        readonly List<Surface> _surfaces;
        readonly BallState _ball;
        readonly List<SimEvent> _events;
        readonly LabSimulation _sim;
        readonly InputIntent _intent;

        public int Impacts, RacketContacts;

        /// <summary>Sets PhysicsConfig.Current to the replay's config.</summary>
        public ReplayStepper(Replay r)
        {
            _r = r;
            PhysicsConfig.Current = r.Config;
            _dt = r.Config.Sim.FixedDt;

            if (r.Kind == "ball")
            {
                _surfaces = CourtSurfaces.Create();
                _ball = new BallState();
                _events = new List<SimEvent>();
                var launcher = new BallLauncher(_ball);
                launcher.Apply(TestPresets.Find(r.Preset).Launch);
                launcher.Apply(r.Launch);
                launcher.Fire(fresh: true);
            }
            else if (r.Kind == "lab")
            {
                _sim = new LabSimulation();
                _sim.SelectPreset(r.Preset);
                _sim.Launcher.SetRepeat(r.Repeat);
                _intent = new InputIntent();
            }
            else throw new ArgumentException("unknown replay kind " + r.Kind);
        }

        public double Dt { get { return _dt; } }

        /// <summary>Advance one fixed step and return its frame.</summary>
        public List<object> Step(int step)
        {
            if (_sim == null)
            {
                _events.Clear();
                BallPhysics.Step(_ball, _surfaces, _dt, _events);
                Impacts += _events.Count;
                return Snapshot.Ball(_ball, _events);
            }

            int i = step * 5;
            _intent.Move.X = _r.Inputs[i]; _intent.Move.Z = _r.Inputs[i + 1];
            _intent.MoveMagnitude = _r.Inputs[i + 2];
            _intent.AimRawX = _r.Inputs[i + 3]; _intent.AimRawY = _r.Inputs[i + 4];
            byte hf = _r.Flags[step * 2], tf = _r.Flags[step * 2 + 1];
            _intent.Hit.Pressed = (hf & 1) != 0; _intent.Hit.Released = (hf & 2) != 0; _intent.Hit.Held = (hf & 4) != 0;
            _intent.Touch.Pressed = (tf & 1) != 0; _intent.Touch.Released = (tf & 2) != 0; _intent.Touch.Held = (tf & 4) != 0;

            _sim.Step(_intent, _dt);
            foreach (var e in _sim.StepEvents)
            {
                if (e.Type == SimEventType.RacketContact) RacketContacts++; else Impacts++;
            }
            return Snapshot.Lab(_sim.Player, _sim.Ball, _sim.StepEvents);
        }
    }

    public static class ReplayRunner
    {
        /// <summary>Replay every step and compare fingerprints. Restores PhysicsConfig afterwards.</summary>
        public static ReplayResult Run(Replay r)
        {
            SimConfig saved = PhysicsConfig.Current;
            try
            {
                var stepper = new ReplayStepper(r);
                var result = new ReplayResult { Name = r.Name, Steps = r.Steps };
                for (int s = 0; s < r.Steps; s++)
                {
                    ulong h = Replay.ParseHash(Snapshot.Hash(stepper.Step(s)));
                    if (h != r.Hashes[s]) { result.FirstMismatch = s; break; }
                }
                result.Impacts = stepper.Impacts;
                result.RacketContacts = stepper.RacketContacts;
                return result;
            }
            finally
            {
                PhysicsConfig.Current = saved;
            }
        }
    }
}
