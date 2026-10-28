// ===========================================================
// Assets/SquashGame/Runtime/Progression/ShotPuzzleBank.cs
// Playable challenges (2026-10-28): the shot puzzles the game plays, made
// ahead of time (MatchBench --puzzle-bank, merged by merge_puzzles.py:
// every answer about as often) - a puzzle takes seconds of sim to make,
// so the day's is ready the moment you open it. The sim is the same in
// the bench and the game (the server relies on it), so a puzzle plays
// here exactly as it was measured; ChallengeTests check the bank still
// matches the sim.
// ===========================================================

using System.Collections.Generic;
using SquashSim.Modes;
using UnityEngine;

namespace SquashGame.Progression
{
    public static class ShotPuzzleBank
    {
        static List<ShotPuzzle> _all;

        public static List<ShotPuzzle> All()
        {
            if (_all != null) return _all;
            _all = new List<ShotPuzzle>();
            var text = Resources.Load<TextAsset>("Challenges/puzzles");
            if (text == null) return _all;
            foreach (var line in text.text.Split('\n'))
            {
                var l = line.Trim();
                if (l.Length == 0) continue;
                var p = JsonUtility.FromJson<ShotPuzzle>(l);
                if (p != null && p.Options.Count == p.Rates.Count && p.Options.Count > 1) _all.Add(p);
            }
            return _all;
        }

        public static int Count { get { return All().Count; } }

        /// <summary>The puzzle at this index (wrapping round the bank); null with no bank.</summary>
        public static ShotPuzzle At(int index)
        {
            var a = All();
            return a.Count == 0 ? null : a[((index % a.Count) + a.Count) % a.Count];
        }
    }
}
