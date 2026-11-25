// ===========================================================
// Assets/SquashGame/Runtime/UI/MatchDrills.cs
// 2026-11-25 (the user: "in the learning how to play thing consider adding
// all those new machenics we have added like special shots diving focusing
// everything"): the Academy's MATCH SKILLS chapter - each lesson a drill
// played as a real match against an easy opponent, with the Special card
// and a meter that fills every rally ("practice"), the coach's line on the
// HUD, and the lesson passed the moment its goal is met.
//
//   Focus          Focus taken (held as their shot comes), 3 times
//   The dive       a dive (Focus let go with the ball out of reach), twice
//   Special Shot   your Special Shot fired, twice
//   The duel       their Special Shot defended (they fire theirs), once
//   Hold the T     on the T as they strike, 4 times
//
// Tracker: counts from what the match shows each frame (Observation), so
// the counting is tested without a match (MachineTests' neighbour,
// DrillTests).
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim;
using SquashSim.Academy;
using UnityEngine;

namespace SquashGame.UI
{
    public static class MatchDrills
    {
        /// <summary>The drill being played (null: none) - the HUD's coach line, the front end's check.</summary>
        public static Tracker Current;
        public const string Opponent = "club:retriever:62";
        public const int Points = 99;                                   // (a long game: the drill ends when it's passed)

        public static MatchSettings SettingsFor(Lesson l)
        {
            var practice = new List<RunMod> { new RunMod { Id = "special", Level = 1 }, new RunMod { Id = "practice", Level = 1 } };
            bool duel = l.Steps.Count > 0 && l.Steps[0].Goal == GoalKind.DuelWins;
            var s = new MatchSettings
            {
                Opponent = Opponent, Difficulty = 0.35f, GamesToWin = 1, Scoring = "par", ParPoints = Points, WarmUp = false,
                Mods = practice, MeterFocus = true, Drill = l.Id, Class = "",
                OppMods = duel ? new List<RunMod> { new RunMod { Id = "special", Level = 1 }, new RunMod { Id = "practice", Level = 1 } } : null,
            }.Dressed();
            s.Difficulty = 0.35f;                                           // (Dressed sets it from the rating: kept easy)
            return s;
        }

        /// <summary>A drill asked for from the lesson scene (no match there): started when the Academy opens in the match scene.</summary>
        public static string Pending;

        public static void Launch(FrontEnd fe, Lesson l)
        {
            if (fe.Match == null) { Pending = l.Id; fe.BackToAcademyMenu(); return; }
            Current = new Tracker(l);
            fe.StartMatch(SettingsFor(l));
        }

        /// <summary>What a frame of the match shows the tracker.</summary>
        public struct Observation
        {
            public bool Focusing;
            public int Dives;              // your dives so far
            public int SpecialBy;          // who's Special Shot is in the air (-1 none)
            public int SpecialsDefended;   // the match's count
            public int Shots;              // strokes this rally (the referee's count)
            public int LastStriker;
            public double X, Z;            // you (sim metres)
            public int Me;
        }

        public static Observation Observe(MatchRunner r)
        {
            var m = r.Sim; int me = r.HumanIndex >= 0 ? r.HumanIndex : 0;
            var p = m.Players[me].Player.Pos;
            return new Observation
            {
                Focusing = m.Focusing(me), Dives = m.Players[me].Arcade != null ? m.Players[me].Arcade.DiveCount : 0,
                SpecialBy = m.SpecialBy, SpecialsDefended = m.SpecialsDefended, Shots = m.Referee.Shots, LastStriker = m.LastStriker,
                X = p.X, Z = p.Z, Me = me,
            };
        }

        public const double TRadius = 1.0;

        public sealed class Tracker
        {
            public readonly Lesson Lesson;
            public LessonStep Step { get { return Lesson.Steps[0]; } }
            public int Progress { get; private set; }
            public bool Passed { get { return Progress >= Step.Count; } }
            /// <summary>The note to flash (null: none) and when it was set.</summary>
            public string Note { get; private set; }
            public float NoteAt { get; private set; }
            bool _have;
            Observation _last;
            int _lastSpecialBy = -1;

            public Tracker(Lesson l) { Lesson = l; }

            void Count(string note) { if (Passed) return; Progress++; Note = note; NoteAt = Time.unscaledTime; }

            public void See(Observation o)
            {
                if (!_have) { _last = o; _have = true; _lastSpecialBy = o.SpecialBy; return; }
                int opp = 1 - o.Me;
                switch (Step.Goal)
                {
                    case GoalKind.FocusUses: if (o.Focusing && !_last.Focusing) Count("FOCUS!"); break;
                    case GoalKind.SmartDives: if (o.Dives > _last.Dives) Count("DIVE!"); break;
                    case GoalKind.SpecialShots: if (o.SpecialBy == o.Me && _last.SpecialBy != o.Me) Count("SPECIAL SHOT!"); break;
                    case GoalKind.DuelWins: if (o.SpecialsDefended > _last.SpecialsDefended && _lastSpecialBy == opp) Count("DUEL WON!"); break;
                    case GoalKind.OnTheT:
                        // they've just struck: where are you?
                        if (o.Shots > _last.Shots && o.LastStriker == opp)
                        {
                            double dz = o.Z - SquashBook.TZ;
                            if (o.X * o.X + dz * dz <= TRadius * TRadius) Count("ON THE T!");
                            else { Note = "GET BACK TO THE T"; NoteAt = Time.unscaledTime; }
                        }
                        break;
                }
                if (o.SpecialBy >= 0) _lastSpecialBy = o.SpecialBy;
                else if (o.Shots < _last.Shots) _lastSpecialBy = -1;                           // a new rally
                _last = o;
            }
        }

        /// <summary>The HUD's line: "MATCH SKILLS  ·  FOCUS   2 / 3".</summary>
        public static string Head(Tracker t) { return "MATCH SKILLS  ·  " + t.Lesson.Title.ToUpperInvariant() + "   " + Mathf.Min(t.Progress, t.Step.Count) + " / " + t.Step.Count; }
    }
}
