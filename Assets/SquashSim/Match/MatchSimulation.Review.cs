// ===========================================================
// Assets/SquashSim/Match/MatchSimulation.Review.cs
// 2026-11-22 evening, the video review (the user: "add referee decision
// review so player and ai opponent can actually challenge the ref on
// calls"; answers: 1 a game each, kept if right; a replay + a verdict; the
// AI challenges by persona):
//
//   the call   an appeal is decided as the referee sees it: a close call
//              (InterferenceDecision.Doubt) can go the other way
//              (RefErrorRate at the very line - Interference.AsSeen); the
//              exact call is kept for the review
//   challenge  the player the call went against (a let: either) has
//              ChallengeSeconds after it, with a review left this game:
//              Challenge(i) - a command, not an input bit (replays carry it
//              as ReplayCommand.Challenge0/1; online has no reviews)
//   review     ReviewSeconds while the game shows the replay; then the
//              exact call: overturned - the rally's result and the score
//              are put right, the review kept; upheld - the review spent
//   the AI     challenges a call against it now and then: more when it was
//              close, more for a hot head (its AppealTendency)
//
// Deterministic (the dice are a hash of the moment), UnityEngine-free.
// ===========================================================

namespace SquashSim
{
    public sealed partial class MatchSimulation
    {
        /// <summary>Reviews on (off online: both machines would have to agree on the press).</summary>
        public bool ReviewsEnabled = true;
        /// <summary>The referee can see a close call wrong (off: the rules exactly - scripted tests).</summary>
        public bool RefereeErrors = true;
        public const int ReviewsPerGame = 1;
        /// <summary>The referee's chance of getting a call at the very line wrong (it falls to 0 as the call gets clear-cut).</summary>
        public const double RefErrorRate = 0.9;
        /// <summary>Seconds after the call to challenge it; the review's length after the replay (the game shows the replay first, the match waiting).</summary>
        public const double ChallengeSeconds = 3.0, ReviewSeconds = 1.2;

        /// <summary>Reviews left this game, each player.</summary>
        public readonly int[] ReviewsLeft = { ReviewsPerGame, ReviewsPerGame };
        /// <summary>The last appeal's call as the referee made it, and as the review sees it (exact).</summary>
        public InterferenceDecision CallMade { get; private set; }
        public InterferenceDecision CallTrue { get; private set; }
        /// <summary>The last call is open to a challenge (a rally just ended on an appeal), and who appealed.</summary>
        public bool ReviewOpen { get; private set; }
        public int AppealBy { get; private set; } = -1;
        /// <summary>Who is reviewing now (-1: nobody), for how long; the last review's result (0 none, 1 overturned, -1 upheld).</summary>
        public int ReviewBy { get; private set; } = -1;
        public double ReviewTime { get; private set; }
        public int ReviewResult { get; private set; }
        /// <summary>Calls the referee got wrong, reviews held and overturned (tests, the bench).</summary>
        public int RefErrors { get; private set; }
        public int Reviews { get; private set; }
        public int Overturned { get; private set; }

        bool _appealThisRally;
        MatchScore.State _scoreBefore;

        /// <summary>The appeal's decision, as the referee sees it (the exact one kept for a review).</summary>
        InterferenceDecision RefereeSees(int appellant, InterferenceDecision exact)
        {
            var seen = RefereeErrors ? Interference.AsSeen(exact, Dice(appellant), RefErrorRate) : exact;
            CallTrue = exact; CallMade = seen; AppealBy = appellant; _appealThisRally = true;
            if (seen.Call != exact.Call) RefErrors++;
            return seen;
        }

        /// <summary>The match's own dice for the referee and the AI's challenges: a hash of this moment (deterministic).</summary>
        double Dice(int salt)
        {
            ulong h = (ulong)StepCount * 0x9E3779B97F4A7C15UL ^ (ulong)(long)(Ball.Pos.X * 1e5) * 0xBF58476D1CE4E5B9UL ^
                      (ulong)(long)(Ball.Pos.Z * 1e5) * 0x94D049BB133111EBUL ^ (ulong)(salt + 7) * 0x2545F4914F6CDD1DUL;
            h ^= h >> 31; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 29; h *= 0x94D049BB133111EBUL; h ^= h >> 32;
            return (h >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>FinishRally, before the score: the call is open to a challenge (an appeal decided it).</summary>
        void ReviewAtRallyEnd()
        {
            ReviewOpen = ReviewsEnabled && _appealThisRally && Phase != MatchPhase.WarmUp;
            _appealThisRally = false;
            ReviewBy = -1; ReviewResult = 0;
            if (ReviewOpen) _scoreBefore = Score.Save();
        }

        /// <summary>Did the last call go against player i (they may challenge it)? A let went against both.</summary>
        public bool CallAgainst(int i)
        {
            if (AppealBy < 0) return false;
            switch (CallMade.Call)
            {
                case InterferenceCall.Stroke: return i == 1 - AppealBy;
                case InterferenceCall.NoLet: return i == AppealBy;
                default: return true;
            }
        }

        /// <summary>Player i may challenge the last call now.</summary>
        public bool CanChallenge(int i)
        {
            return i >= 0 && i < 2 && ReviewOpen && ReviewBy < 0 && Phase == MatchPhase.RallyOver && PhaseTime < ChallengeSeconds &&
                   ReviewsLeft[i] > 0 && CallAgainst(i);
        }

        /// <summary>Player i challenges the last call (a person: the game's key; recorded as a command). False: not now.</summary>
        public bool Challenge(int i)
        {
            if (!CanChallenge(i)) return false;
            ReviewBy = i; ReviewTime = 0; Reviews++;
            Note(i, "review", 0);
            return true;
        }

        /// <summary>Each step while the rally is over: the AI's challenges, the review running. True: hold the break (reviewing).</summary>
        bool ReviewStep(double dt)
        {
            if (!ReviewOpen) return false;
            if (ReviewBy < 0)
            {
                // the computer: once, a moment after the call - more likely when it was close and for a hot head
                for (int i = 0; i < 2 && ReviewBy < 0; i++)
                {
                    if (!(Bots[i] is OpponentBrain ob) || !CanChallenge(i) || PhaseTime < AiChallengeAt || PhaseTime - dt >= AiChallengeAt) continue;
                    double want = (0.15 + 0.85 * CallMade.Doubt) * (0.4 + ob.Profile.AppealTendency);
                    if (Dice(10 + i) < want) Challenge(i);
                }
                if (ReviewBy < 0) { if (PhaseTime >= ChallengeSeconds) ReviewOpen = false; return false; }
            }
            ReviewTime += dt;
            if (ReviewTime < ReviewSeconds) return true;
            // the verdict
            int by = ReviewBy;
            if (CallTrue.Call != CallMade.Call)
            {
                ReviewResult = 1; Overturned++;
                Score.Load(_scoreBefore);
                var old = LastOutcome;
                var fixedUp = new RallyOutcome
                {
                    Kind = CallTrue.Call == InterferenceCall.Stroke ? RallyEnd.Stroke : CallTrue.Call == InterferenceCall.Let ? RallyEnd.Let : RallyEnd.NoLet,
                    Winner = CallTrue.Call == InterferenceCall.Stroke ? AppealBy : CallTrue.Call == InterferenceCall.Let ? -1 : 1 - AppealBy,
                    Loser = CallTrue.Call == InterferenceCall.Stroke ? 1 - AppealBy : CallTrue.Call == InterferenceCall.Let ? -1 : AppealBy,
                    Reason = "Overturned - " + CallTrue.Reason,
                    Point = old != null ? old.Point : null, Time = old != null ? old.Time : Time,
                };
                LastOutcome = fixedUp;
                if (History.Count > 0) History[History.Count - 1] = fixedUp;
                if (Stats.Count > 0) Stats[Stats.Count - 1].Outcome = fixedUp;
                Score.Apply(fixedUp);
                CallMade = CallTrue;
                Note(by, "overturned", 0);
            }
            else
            {
                ReviewResult = -1;
                ReviewsLeft[by] = System.Math.Max(0, ReviewsLeft[by] - 1);
                Note(by, "upheld", 0);
            }
            ReviewOpen = false; ReviewBy = -1;
            PhaseTime = System.Math.Max(0, PhysicsConfig.Current.Match.BetweenRallySeconds - AfterReviewSeconds);   // a moment to read it
            return false;
        }
        /// <summary>When the computer decides (s after the call); the pause after a verdict before play goes on.</summary>
        public const double AiChallengeAt = 1.1, AfterReviewSeconds = 1.5;

        void ReviewNewGame() { ReviewsLeft[0] = ReviewsLeft[1] = ReviewsPerGame; ReviewOpen = false; ReviewBy = -1; }
    }
}
