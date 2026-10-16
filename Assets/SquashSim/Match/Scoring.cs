// ===========================================================
// Assets/SquashSim/Match/Scoring.cs
// PAR and English scoring on one state machine (Phase 1 decision 5).
//
//   PAR-11   Every rally scores for its winner. To 11, win by 2.
//   English  Only the server scores; a receiver winning the rally takes
//            the serve ("hand-out") without a point. To 9; when the
//            score first reaches 8-8 the receiver chooses to play to 9
//            or to 10 ("set two"). No win-by-2 beyond that.
//
// Serve: the rally winner serves next. A server who keeps the serve
// alternates boxes; a new server starts in the right box (the choice is
// theirs in real squash; right is the default). A let replays the rally
// from the same box.
// ===========================================================

namespace SquashSim
{
    public enum ScoringSystem { Par, English }

    public sealed class MatchScore
    {
        public readonly ScoringSystem System;
        public readonly int[] Points = new int[2];
        public readonly int[] Games = new int[2];
        public int Server { get; private set; }
        public int ServeSide { get; private set; } = 1;     // +1 right box, -1 left box
        public int GameTarget { get; private set; }
        public bool GameOver { get; private set; }
        public bool MatchOver { get; private set; }
        public int GameWinner { get; private set; } = -1;
        public int MatchWinner { get; private set; } = -1;
        public int GamesToWin { get; private set; }
        public int Rallies { get; private set; }
        readonly bool _englishSetTwo;
        bool _setDecided;

        public MatchScore(ScoringSystem system, int firstServer, int gamesToWin, bool englishSetTwo)
        {
            System = system;
            Server = firstServer;
            GamesToWin = gamesToWin < 1 ? 1 : gamesToWin;
            _englishSetTwo = englishSetTwo;
            ResetGame(firstServer);
        }

        public static MatchScore FromConfig(int firstServer)
        {
            var m = PhysicsConfig.Current.Match;
            var sys = m.Scoring == "english" ? ScoringSystem.English : ScoringSystem.Par;
            return new MatchScore(sys, firstServer, m.GamesToWin, m.EnglishSetTwo);
        }

        int BaseTarget
        {
            get
            {
                var m = PhysicsConfig.Current.Match;
                return System == ScoringSystem.Par ? m.ParPoints : m.EnglishPoints;
            }
        }

        void ResetGame(int server)
        {
            Points[0] = 0; Points[1] = 0;
            Server = server;
            ServeSide = 1;
            GameTarget = BaseTarget;
            GameOver = false;
            GameWinner = -1;
            _setDecided = false;
        }

        /// <summary>Apply a finished rally. Returns true if it changed the score or serve.</summary>
        public bool Apply(RallyOutcome o)
        {
            if (MatchOver || GameOver || o == null) return false;
            Rallies++;
            if (o.Kind == RallyEnd.Let || o.Winner < 0) return false;   // replay, same box

            int w = o.Winner;
            bool serverWon = w == Server;

            if (System == ScoringSystem.Par)
            {
                Points[w]++;
                if (serverWon) ServeSide = -ServeSide;
                else { Server = w; ServeSide = 1; }
            }
            else
            {
                if (serverWon) { Points[w]++; ServeSide = -ServeSide; }
                else { Server = w; ServeSide = 1; }   // hand-out: no point

                int target = BaseTarget;
                if (!_setDecided && Points[0] == target - 1 && Points[1] == target - 1)
                {
                    _setDecided = true;
                    GameTarget = _englishSetTwo ? target + 1 : target;
                }
            }

            CheckGameOver();
            return true;
        }

        void CheckGameOver()
        {
            for (int p = 0; p < 2; p++)
            {
                int me = Points[p], them = Points[1 - p];
                bool won = System == ScoringSystem.Par
                    ? me >= GameTarget && me - them >= 2
                    : me >= GameTarget;
                if (!won) continue;
                GameOver = true;
                GameWinner = p;
                Games[p]++;
                if (Games[p] >= GamesToWin) { MatchOver = true; MatchWinner = p; }
                return;
            }
        }

        /// <summary>Start the next game: the previous game's winner serves first.</summary>
        public void NextGame()
        {
            if (!GameOver || MatchOver) return;
            ResetGame(GameWinner);
        }

        /// <summary>Score line from the server's point of view, e.g. "7-5".</summary>
        public string ServerCall()
        {
            return Points[Server] + "-" + Points[1 - Server];
        }
    }
}
