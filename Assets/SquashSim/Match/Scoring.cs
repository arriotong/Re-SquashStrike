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
            var s = new MatchScore(sys, m.StartServer >= 0 ? m.StartServer : firstServer, m.GamesToWin, m.EnglishSetTwo);
            s.EachGame[0] = m.EachGamePoints0; s.EachGame[1] = m.EachGamePoints1;
            if (s.EachGame[0] != 0 || s.EachGame[1] != 0) { s.Points[0] = s.EachGame[0]; s.Points[1] = s.EachGame[1]; }
            if (m.StartPoints0 != 0 || m.StartPoints1 != 0 || m.StartGames0 != 0 || m.StartGames1 != 0)
                s.StartAt(m.StartPoints0, m.StartPoints1, m.StartGames0, m.StartGames1);
            return s;
        }

        /// <summary>P3-C1: every game starts at this score (the Gauntlet's head start). 0-0 normally.</summary>
        public readonly int[] EachGame = new int[2];

        /// <summary>P3-C1: begin part-way through (a Scenario): this game's points and the games already won. Before the first rally only.</summary>
        public void StartAt(int p0, int p1, int g0, int g1)
        {
            if (Rallies > 0) return;
            Points[0] = p0; Points[1] = p1;
            Games[0] = g0 < 0 ? 0 : (g0 >= GamesToWin ? GamesToWin - 1 : g0);
            Games[1] = g1 < 0 ? 0 : (g1 >= GamesToWin ? GamesToWin - 1 : g1);
            // English at 8-all is already the set decision's moment: let it be decided on the next rally as usual
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
            Points[0] = EachGame != null ? EachGame[0] : 0; Points[1] = EachGame != null ? EachGame[1] : 0;
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
