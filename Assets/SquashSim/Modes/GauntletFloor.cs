// ===========================================================
// Assets/SquashSim/Modes/GauntletFloor.cs
// 2026-11-21 the Gauntlet's arcade floors (the user, after RACCOIN: "no
// life system - if you lose, the run ends; the floor's objective changes";
// plan: SQUASH FRONTEND MODIFIED/RE_SQUASHSTRIKE_GAUNTLET_ARCADE_PLAN.md):
//
//   a floor     a target score in a set number of rallies; reach it and the
//               floor is cleared at once (every rally left over pays a
//               bonus); run out short of it and the floor is failed
//   a rally     won: points by how - their error 50, a stroke 75, a winner
//               100, a kill winner 120, an ace 150, a nick 200, +10 a shot
//               past 6 - times the game points it gave (the Jackpot cards:
//               a nick worth 2), times the multiplier. Lost: nothing, and
//               the multiplier back to 1x. A let doesn't count.
//   multiplier  1x; +1 a rally won, +0.5 more for a winner / kill / nick /
//               ace; 5x at most (a twist may cap it lower)
//   the wheel   at the cap, each rally won adds power (up to 3); when the
//               streak ends (or the floor does) the wheel spins: points,
//               rallies, "the next rally scores double", a card level, coins
//               - or the Golden Wheel, likelier the more power
//
// Pure and seeded (the run's seed, the floor, the spin count): the same
// floor plays the same wheel. UnityEngine-free; the game feeds it rallies.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Modes
{
    public enum WheelPrize { Points, Rally, Double, CardLevel, Coins, Golden, BigPoints, TwoRallies, Triple, ExtraPick, BigCoins }

    /// <summary>A floor being played: its goal, how far along, the multiplier and the wheel.</summary>
    [Serializable]
    public sealed class FloorPlay
    {
        public int Floor, Target, Rallies, Played, Score;
        public double Mult = 1.0, MaxMult = GauntletFloor.MaxMult, MultStep = 1.0;
        public int Power, Spins, DoubleNext, TripleNext, BestRally, Coins, CardLevels, ExtraPicks;
        public bool NoWheel, WinnersOnly, Done;
        public uint Seed;
        /// <summary>The wheel is waiting to spin (the streak ended charged, or the floor ended charged).</summary>
        public bool SpinDue;
        public bool Charged { get { return Power > 0; } }
        public bool Cleared { get { return Score >= Target; } }
        public int Left { get { return Math.Max(0, Rallies - Played); } }
        /// <summary>A fresh copy (a floor played again from its start).</summary>
        public FloorPlay Clone() { return (FloorPlay)MemberwiseClone(); }
    }

    /// <summary>A rally, as the floor sees it.</summary>
    public struct FloorRally
    {
        public bool Won, Let;
        public RallyEnd Kind;
        public int Shots, GamePoints;
        public bool Ace, Nick, Kill;
    }

    /// <summary>What a rally scored (the HUD's pop).</summary>
    public struct FloorScore
    {
        public int Points, Base;
        public double Mult;
        public string What;
    }

    public struct WheelSpin
    {
        public WheelPrize Prize;
        public bool Golden;
        public int Amount;
        public string Words;
        /// <summary>The slice it landed on (the wheel's picture), and the slices there were.</summary>
        public int Slice;
        public WheelPrize[] Slices;
    }

    public static class GauntletFloor
    {
        public const double MaxMult = 5.0, PerWin = 1.0, PerGreat = 0.5;
        public const int MaxPower = 3, LongFrom = 6, LongPerShot = 10;
        public const int ErrorPts = 50, StrokePts = 75, WinnerPts = 100, KillPts = 120, AcePts = 150, NickPts = 200;
        /// <summary>A rally left over when the target is reached: this many points and Gauntlet coins.</summary>
        public const int LeftoverPts = 25, LeftoverCoins = 3;

        public static FloorPlay Begin(int floor, int target, int rallies, uint seed, int startScore = 0)
        {
            return new FloorPlay { Floor = floor, Target = target, Rallies = rallies, Seed = seed == 0 ? 1u : seed, Score = startScore };
        }

        /// <summary>The base points for a won rally, and its name.</summary>
        public static int BasePoints(FloorRally r, out string what)
        {
            if (r.Nick) { what = "NICK"; return NickPts; }
            if (r.Ace) { what = "ACE"; return AcePts; }
            if (r.Kind == RallyEnd.NotUp) { if (r.Kill) { what = "KILL"; return KillPts; } what = "WINNER"; return WinnerPts; }
            if (r.Kind == RallyEnd.Stroke) { what = "STROKE"; return StrokePts; }
            what = "THEIR MISTAKE"; return ErrorPts;
        }

        static bool Great(FloorRally r) { return r.Nick || r.Ace || r.Kind == RallyEnd.NotUp; }

        /// <summary>A rally ended: points, the multiplier, the wheel's charge; the floor done or not.</summary>
        public static FloorScore Rally(FloorPlay f, FloorRally r)
        {
            var s = new FloorScore { Mult = f.Mult, What = "" };
            if (f.Done || r.Let) return s;
            f.Played++;
            if (r.Won)
            {
                int b = BasePoints(r, out s.What);
                if (f.WinnersOnly && !Great(r)) { b = 0; s.What = "NO POINTS: WINNERS ONLY"; }
                int extra = Math.Max(0, r.Shots - LongFrom) * LongPerShot;
                int gp = Math.Max(1, r.GamePoints);
                double boost = f.TripleNext > 0 ? 3 : f.DoubleNext > 0 ? 2 : 1;
                if (f.TripleNext > 0) f.TripleNext--; else if (f.DoubleNext > 0) f.DoubleNext--;
                s.Base = (b + (b > 0 ? extra : 0)) * gp;
                s.Points = (int)Math.Round(s.Base * f.Mult * boost);
                if (boost > 1) s.What += boost >= 3 ? "  ·  TRIPLE" : "  ·  DOUBLE";
                if (gp > 1) s.What += "  ·  x" + gp + " POINTS";
                f.Score += s.Points;
                if (s.Points > f.BestRally) f.BestRally = s.Points;
                // the streak: up to the cap, then the wheel's power
                bool atCap = f.Mult >= f.MaxMult - 1e-9;
                if (atCap) { if (!f.NoWheel && f.Power < MaxPower) f.Power++; }
                f.Mult = Math.Min(f.MaxMult, f.Mult + (PerWin + (Great(r) ? PerGreat : 0)) * f.MultStep);
                if (!atCap && f.Mult >= f.MaxMult - 1e-9 && !f.NoWheel && f.Power == 0) f.Power = 1;   // reaching the cap charges it
            }
            else
            {
                s.What = "LOST";
                f.Mult = 1.0;
                if (f.Charged) f.SpinDue = true;                              // the streak ended charged: spin
            }
            if (f.Cleared || f.Played >= f.Rallies)
            {
                f.Done = true;
                if (f.Charged) f.SpinDue = true;
                if (f.Cleared)
                {
                    int left = f.Left;
                    f.Score += left * LeftoverPts;
                    f.Coins += left * LeftoverCoins;
                }
            }
            return s;
        }

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

        public static readonly WheelPrize[] Wheel1 = { WheelPrize.Points, WheelPrize.Rally, WheelPrize.Double, WheelPrize.Coins, WheelPrize.Points, WheelPrize.CardLevel, WheelPrize.Rally, WheelPrize.Golden };
        public static readonly WheelPrize[] Wheel2 = { WheelPrize.BigPoints, WheelPrize.TwoRallies, WheelPrize.Triple, WheelPrize.BigCoins, WheelPrize.ExtraPick, WheelPrize.BigPoints };

        /// <summary>Spin (the power spent): where it lands, and the prize given to the floor. Golden: the Golden Wheel spun at once.</summary>
        public static List<WheelSpin> Spin(FloorPlay f)
        {
            var o = new List<WheelSpin>();
            if (!f.SpinDue || f.Power <= 0) { f.SpinDue = false; return o; }
            int power = f.Power;
            f.Power = 0; f.SpinDue = false;
            uint h = Hash(f.Seed * 2654435761u + (uint)f.Floor * 7919u + (uint)f.Spins * 104729u + 11u);
            f.Spins++;
            // more power, likelier gold: the golden slice weighs 1, 2, 4
            int goldW = power >= 3 ? 4 : power;
            int total = Wheel1.Length - 1 + goldW;
            int r = (int)(h % (uint)total);
            int slice = r < Wheel1.Length - 1 ? r : Wheel1.Length - 1;
            var first = Give(f, Wheel1, slice, power, false);
            o.Add(first);
            if (first.Golden)
            {
                h = Hash(h + 0x9E3779B9u);
                int s2 = (int)(h % (uint)Wheel2.Length);
                o.Add(Give(f, Wheel2, s2, power, true));
            }
            return o;
        }

        static WheelSpin Give(FloorPlay f, WheelPrize[] wheel, int slice, int power, bool golden)
        {
            var p = wheel[slice];
            var s = new WheelSpin { Prize = p, Slice = slice, Slices = wheel, Golden = p == WheelPrize.Golden };
            switch (p)
            {
                case WheelPrize.Points: s.Amount = 150 * power; f.Score += (int)Math.Round(s.Amount * 1.0); s.Words = "+" + s.Amount + " POINTS"; break;
                case WheelPrize.Rally: s.Amount = 1; if (!f.Done) f.Rallies++; s.Words = f.Done ? "+20 COINS" : "+1 RALLY"; if (f.Done) f.Coins += 20; break;
                case WheelPrize.Double: s.Amount = 1; f.DoubleNext += 1; s.Words = "NEXT RALLY SCORES DOUBLE"; break;
                case WheelPrize.Coins: s.Amount = 15 * power; f.Coins += s.Amount; s.Words = "+" + s.Amount + " COINS"; break;
                case WheelPrize.CardLevel: s.Amount = 1; f.CardLevels++; s.Words = "A CARD LEVELS UP"; break;
                case WheelPrize.Golden: s.Words = "THE GOLDEN WHEEL!"; break;
                case WheelPrize.BigPoints: s.Amount = 600 * power; f.Score += s.Amount; s.Words = "+" + s.Amount + " POINTS"; break;
                case WheelPrize.TwoRallies: s.Amount = 2; if (!f.Done) f.Rallies += 2; else f.Coins += 50; s.Words = f.Done ? "+50 COINS" : "+2 RALLIES"; break;
                case WheelPrize.Triple: s.Amount = 3; f.TripleNext += 3; s.Words = "NEXT 3 RALLIES SCORE TRIPLE"; break;
                case WheelPrize.BigCoins: s.Amount = 60 * power; f.Coins += s.Amount; s.Words = "+" + s.Amount + " COINS"; break;
                case WheelPrize.ExtraPick: s.Amount = 1; f.ExtraPicks++; s.Words = "AN EXTRA CARD TO PICK FROM"; break;
            }
            // points from the wheel can still clear a floor that's still on
            if (!f.Done && f.Cleared) { f.Done = true; f.Coins += f.Left * LeftoverCoins; f.Score += f.Left * LeftoverPts; }
            return s;
        }

        /// <summary>A prize's short name (the wheel's slices).</summary>
        public static string SliceName(WheelPrize p)
        {
            switch (p)
            {
                case WheelPrize.Points: return "POINTS";
                case WheelPrize.Rally: return "+1 RALLY";
                case WheelPrize.Double: return "x2 NEXT";
                case WheelPrize.Coins: return "COINS";
                case WheelPrize.CardLevel: return "CARD UP";
                case WheelPrize.Golden: return "GOLDEN";
                case WheelPrize.BigPoints: return "BIG POINTS";
                case WheelPrize.TwoRallies: return "+2 RALLIES";
                case WheelPrize.Triple: return "x3 NEXT 3";
                case WheelPrize.BigCoins: return "BIG COINS";
                case WheelPrize.ExtraPick: return "EXTRA PICK";
                default: return p.ToString();
            }
        }
    }
}
