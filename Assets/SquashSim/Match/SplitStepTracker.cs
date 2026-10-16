// ===========================================================
// Assets/SquashSim/Match/SplitStepTracker.cs
// Momentum Focus = split-step (Phase 1 decision 3).
//
// A split-step is a small hop timed to the opponent's racket contact.
// Pressed within +-Window of that contact: a quicker first step
// (BoostTimer). Pressed with no opponent contact inside the window:
// briefly planted (PlantTimer). No meter, no resource - pure timing,
// identical online because it is just input plus sim time.
// ===========================================================

namespace SquashSim
{
    public sealed class SplitStepTracker
    {
        readonly double[] _pendingPress = { -1, -1 };          // sim time of an unanswered press
        readonly double[] _lastContact = { -1e9, -1e9 };       // sim time of each player's last racket contact

        public void Reset()
        {
            _pendingPress[0] = _pendingPress[1] = -1;
            _lastContact[0] = _lastContact[1] = -1e9;
        }

        /// <summary>Player <paramref name="i"/> pressed split-step at <paramref name="time"/>.</summary>
        public void Press(int i, double time, TestPlayer player)
        {
            var ss = PhysicsConfig.Current.SplitStep;
            if (time - _lastContact[1 - i] <= ss.Window)
            {
                Boost(player);                 // just after the opponent's contact: still in time
                _pendingPress[i] = -1;
            }
            else
            {
                _pendingPress[i] = time;       // wait for the opponent's contact
            }
        }

        /// <summary>Player <paramref name="striker"/> struck the ball; the opponent's pending press may land.</summary>
        public void Contact(int striker, double time, TestPlayer opponent)
        {
            _lastContact[striker] = time;
            int o = 1 - striker;
            if (_pendingPress[o] >= 0 && time - _pendingPress[o] <= PhysicsConfig.Current.SplitStep.Window)
            {
                _pendingPress[o] = -1;
                Boost(opponent);
            }
        }

        /// <summary>Call once per step: presses whose window passed unanswered plant the player.</summary>
        public void Update(double time, TestPlayer p0, TestPlayer p1)
        {
            var ss = PhysicsConfig.Current.SplitStep;
            for (int i = 0; i < 2; i++)
            {
                if (_pendingPress[i] >= 0 && time - _pendingPress[i] > ss.Window)
                {
                    _pendingPress[i] = -1;
                    TestPlayer p = i == 0 ? p0 : p1;
                    p.PlantTimer = ss.PlantSeconds;
                    p.BoostTimer = 0;
                }
            }
        }

        static void Boost(TestPlayer p)
        {
            p.BoostTimer = PhysicsConfig.Current.SplitStep.BoostSeconds;
            p.PlantTimer = 0;
        }
    }
}
