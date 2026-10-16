// ===========================================================
// Assets/SquashSim.Testing/Editor/ClockTests.cs
// Frame-rate independence (lab invariant 1, M1 exit test): a golden
// rally driven through SimulationClock at 30, 60 and 144 fps, at a
// jittery frame rate, and in slow motion, is bit-identical at every
// step to the HTML lab. Render rate decides how many steps run per
// frame, never what a step computes.
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashSim.Lab;
using SquashSim.Testing;
using UnityEngine;

namespace SquashSim.Tests
{
    public class ClockTests
    {
        static Replay Load(string name)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "SimParity", "Replays", name + ".ssreplay"));
            using (var fs = File.OpenRead(path)) return Replay.Read(fs);
        }

        static int RunThroughClock(Replay r, System.Func<int, double> frameDt, double timeScale)
        {
            SimConfig saved = PhysicsConfig.Current;
            try
            {
                var stepper = new ReplayStepper(r);
                int step = 0, firstMismatch = -1;
                var clock = new SimulationClock(dt =>
                {
                    if (step >= r.Steps) return;
                    ulong h = Replay.ParseHash(Snapshot.Hash(stepper.Step(step)));
                    if (firstMismatch < 0 && h != r.Hashes[step]) firstMismatch = step;
                    step++;
                });
                clock.TimeScale = timeScale;
                int frame = 0;
                while (step < r.Steps && frame < 2000000) clock.Advance(frameDt(frame++));
                Assert.That(step, Is.EqualTo(r.Steps), "clock never finished the replay");
                return firstMismatch;
            }
            finally { PhysicsConfig.Current = saved; }
        }

        [TestCase(30.0)]
        [TestCase(60.0)]
        [TestCase(144.0)]
        [TestCase(240.0)]
        public void IdenticalAtAnyFrameRate(double fps)
        {
            Replay r = Load("lab_free_tap");
            int mismatch = RunThroughClock(r, f => 1.0 / fps, 1.0);
            Assert.That(mismatch, Is.EqualTo(-1), "diverged at step " + mismatch + " when rendered at " + fps + " fps");
        }

        [Test]
        public void IdenticalWithJitteryFrames()
        {
            Replay r = Load("lab_free_hold");
            var rng = new System.Random(7);
            int mismatch = RunThroughClock(r, f => 0.002 + rng.NextDouble() * 0.045, 1.0);
            Assert.That(mismatch, Is.EqualTo(-1), "diverged at step " + mismatch);
        }

        [Test]
        public void IdenticalInSlowMotion()
        {
            Replay r = Load("lab_backWall_mixed");
            int mismatch = RunThroughClock(r, f => 1.0 / 60, 0.1);
            Assert.That(mismatch, Is.EqualTo(-1), "diverged at step " + mismatch);
        }

        [Test]
        public void StepCapDropsBacklogInsteadOfSpiralling()
        {
            int steps = 0;
            var clock = new SimulationClock(dt => steps++);
            clock.Advance(0.25);    // a stalled frame
            Assert.That(clock.StepsLastFrame, Is.EqualTo(PhysicsConfig.Current.Sim.MaxStepsPerFrame));
            Assert.That(clock.Clamped, Is.True);
            clock.Advance(1.0 / 240);
            Assert.That(clock.StepsLastFrame, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void PauseAndSingleStep()
        {
            int steps = 0;
            var clock = new SimulationClock(dt => steps++);
            clock.StepOnce();
            Assert.That(clock.Paused, Is.True);
            clock.Advance(1.0);
            Assert.That(steps, Is.EqualTo(1));
            clock.Advance(1.0);
            Assert.That(steps, Is.EqualTo(1));
            clock.Resume();
            clock.Advance(1.0 / 60 + 1e-9);   // four 1/240 steps
            Assert.That(steps, Is.EqualTo(5));
        }
    }
}
