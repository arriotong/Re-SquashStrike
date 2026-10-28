// ===========================================================
// Assets/SquashSim/Player/RunMods.Chaos.cs
// The Gauntlet chaos remake (2026-10-28): "the power ups now are very
// boring ... I want more chaotic and arcade, so time stop or whatever
// that we can't do in real squash gets in there too ... if you get the
// right build you can basically score 7 points in one shot" (the user;
// plan: RE_SQUASHSTRIKE_GAUNTLET_CHAOS_PLAN.md).
//
// Five builds, a POWER meter and an ultimate, points that stack - all
// in the sim, all worked out from the levels and the match's own state,
// so replays stay exact. Like the rest of RunMods: null outside the
// Gauntlet, so every other match never runs a line of this.
//
//   meter      a run with an ultimate card fills POWER with every shot
//              and every rally won; full + a CHARGED shot = the
//              ultimate (all of the ultimate cards at once)
//   TIME       freeze     ULT: the opponent frozen solid
//              slowfield  after your shot they move slower
//              haste      after your shot you move faster
//              rewind     a lost rally replayed (a few a game)
//              overclock  the meter fills faster
//              world      (evolved) the freeze twice as long, the meter
//                         starts every game full
//   POWER      inferno    ULT: the next 3 shots are fireballs (faster;
//                         a fireball winner scores +2)
//              meteor     charged drives and kills faster
//              quake      your kill's bounce makes them stumble
//              heavyball  after your hard shot their reach shrinks
//              supernova  (evolved) every charged shot is a fireball
//   TRICK      curve      drives and kills bend after the front wall
//              deaddrop   drops and boasts barely bounce
//              ricochet   every side wall speeds the ball up
//              knuckle    their timing window shrinks on your shots
//              wizard     (evolved) a winner that curved scores +1
//   JACKPOT    jackpot    ULT: a winner with it scores +2..+4
//              combo      each rally won in a row is worth one more
//              wallride   +1 for each wall your winner touched
//              bounty     long rallies won are worth more
//              firstblood the first point of each game is worth more
//              allin      (evolved) the jackpot times your combo
//   GUARD      magnet     much more reach
//              shield     your own errors become lets (a few a game)
//              fortress   (evolved) the shield never runs out
// ===========================================================

namespace SquashSim
{
    /// <summary>What a chaos card just did (MatchSimulation.Chaos): the game shows it - "ult", "freeze" (Player: the one
    /// frozen), "fire" (Value: fireballs left), "curve", "ricochet" (Value: walls so far), "deaddrop", "quake", "shield",
    /// "rewind", "bonus" (Value: the extra points).</summary>
    public sealed class ChaosNote
    {
        public int Player, Value;
        public string Kind;
        public double Time;
    }

    public sealed partial class RunMods
    {
        // ---------------- the state a match keeps ----------------

        /// <summary>The POWER meter, 0..1 (a run with an ultimate card).</summary>
        public double Power;
        /// <summary>The ultimate fired on the shot being struck now (MatchSimulation reads it at the contact).</summary>
        public bool UltNow;
        /// <summary>Fireballs left (Inferno), and whether the shot being struck is one.</summary>
        public int FireShots;
        public bool ShotFire;
        /// <summary>Frozen (the opponent's Time Stop or Quake): no move, no stroke.</summary>
        public double FreezeTimer;
        /// <summary>How slow the opponent's Matador / Slow Field makes this athlete (with SlowTimer).</summary>
        public double SlowScale = MatadorSpeed;
        /// <summary>Haste: faster for a moment after your own shot.</summary>
        public double HasteTimer;
        /// <summary>Until this athlete's next stroke: reach and timing window cut (Heavy Ball, Knuckleball against it).</summary>
        public double ReachCut, GraceCut;
        /// <summary>Shields and rewinds spent this game.</summary>
        public int ShieldsUsed, RewindsUsed;

        // ---------------- tuning (constants: never config, so the sim's fingerprint is untouched) ----------------

        public const double UltCharge = 0.6;             // a charged shot: the hold at least this full
        public const double FirePace = 1.35, FireWinBonus = 2;
        public const int FireShotsPerUlt = 3;
        public const double MeterPerShot = 0.11, MeterPerWin = 0.25;

        // ---------------- the meter and the ultimate ----------------

        /// <summary>Has an ultimate card (Time Stop, Inferno, Jackpot - or an evolution of one).</summary>
        public bool HasUltimate { get { return Level("freeze") > 0 || Level("inferno") > 0 || Level("jackpot") > 0 || Level("world") > 0 || Level("allin") > 0; } }
        double MeterRate { get { return 1 + 0.2 * Level("overclock"); } }
        public void MeterShot() { if (HasUltimate) Power = System.Math.Min(1, Power + MeterPerShot * MeterRate); }
        public void MeterWin() { if (HasUltimate) Power = System.Math.Min(1, Power + MeterPerWin * MeterRate); }
        /// <summary>A new game: The World starts it with a full meter.</summary>
        public void NewGame() { ShieldsUsed = 0; RewindsUsed = 0; if (Level("world") > 0) Power = 1; }

        /// <summary>At the stroke: a full meter and a charged shot fire the ultimate. Then: is this shot a fireball?</summary>
        public void AtStroke(double charge, bool touch)
        {
            UltNow = false;
            if (HasUltimate && Power >= 1 && charge >= UltCharge)
            {
                Power = 0;
                UltNow = true;
                if (Level("inferno") > 0) FireShots = FireShotsPerUlt - 1 + Level("inferno");   // 3, 4, 5
            }
            ShotFire = !touch && (FireShots > 0 || (Level("supernova") > 0 && charge >= UltCharge));
        }

        // ---------------- TIME ----------------

        /// <summary>Seconds the ultimate freezes the opponent (Time Stop 0.7 .. 1.2 s; The World twice that).</summary>
        public double FreezeSeconds
        {
            get
            {
                int l = Level("freeze");
                double s = l > 0 ? 0.7 + 0.25 * (l - 1) : 0;
                if (Level("world") > 0) s = System.Math.Max(s, 0.95) * 2;
                return s;
            }
        }
        /// <summary>After your shot: the opponent slowed this long, to this share of their speed (Slow Field).</summary>
        public double SlowFieldSeconds { get { return Level("slowfield") > 0 ? 0.6 : 0; } }
        public double SlowFieldScale { get { return 1 - 0.06 * Level("slowfield"); } }
        /// <summary>After your shot: you faster this long, by this much (Haste).</summary>
        public double HasteSeconds { get { return Level("haste") > 0 ? 1.0 : 0; } }
        public double HasteScale { get { return 1 + 0.05 * Level("haste"); } }
        /// <summary>The move speed share now (frozen: 0; slowed; hasted). Exactly 1 with nothing on.</summary>
        public double MoveScale
        {
            get
            {
                if (FreezeTimer > 0) return 0;
                double k = 1;
                if (SlowTimer > 0) k *= SlowScale;
                if (HasteTimer > 0) k *= HasteScale;
                return k;
            }
        }
        public bool Frozen { get { return FreezeTimer > 0; } }
        /// <summary>Lost rallies replayed a game (Rewind).</summary>
        public int Rewinds { get { return Level("rewind"); } }

        // ---------------- POWER ----------------

        /// <summary>Charged drives and kills: the Meteor's extra pace.</summary>
        public double MeteorPace(string shot, double charge) { return (shot == "drive" || shot == "kill") && charge >= 0.5 ? 1 + 0.08 * Level("meteor") : 1; }
        /// <summary>Seconds your kill's bounce freezes the opponent (Quake).</summary>
        public double QuakeSeconds { get { return 0.12 * Level("quake"); } }
        /// <summary>The reach cut your hard shot puts on the opponent (Heavy Ball).</summary>
        public double HeavyCut { get { return 0.04 * Level("heavyball"); } }

        // ---------------- TRICK ----------------

        /// <summary>The sideways bend after the front wall, m/s² (Curveball), and how long it lasts.</summary>
        public double CurveAccel { get { int l = Level("curve"); return l > 0 ? 1.2 + 0.6 * l : 0; } }
        public const double CurveSeconds = 0.6;
        /// <summary>The share of its bounce a drop or boast keeps (Dead Drop).</summary>
        public double DeadDropKeep { get { return 1 - 0.15 * Level("deaddrop"); } }
        /// <summary>Pace a side wall adds (Ricochet).</summary>
        public double RicochetPace { get { return 1 + 0.05 * Level("ricochet"); } }
        /// <summary>The timing-window cut your shot puts on the opponent (Knuckleball).</summary>
        public double KnuckleCut { get { return 0.08 * Level("knuckle"); } }

        // ---------------- GUARD ----------------

        /// <summary>Your own errors (tin, out) a game that are lets instead (Shield; Fortress: all).</summary>
        public bool ShieldLeft { get { return Level("fortress") > 0 || ShieldsUsed < Level("shield"); } }

        // ---------------- JACKPOT: the points a won rally adds ----------------

        /// <summary>The extra points for a rally this athlete won. struckLast: their shot won it (a winner, not the opponent's
        /// error). streak: rallies won in a row before this one. walls: walls the winner touched after the front wall.</summary>
        public int ChaosBonus(bool struckLast, bool ult, bool fire, bool curved, int walls, int shots, bool firstOfGame, int streak)
        {
            int b = 0;
            int combo = System.Math.Min(Level("combo"), streak);
            b += combo;
            if (struckLast)
            {
                if (ult && (Level("jackpot") > 0 || Level("allin") > 0))
                {
                    int jack = 1 + System.Math.Max(Level("jackpot"), Level("allin") > 0 ? 3 : 0);
                    if (Level("allin") > 0) jack *= 1 + System.Math.Min(3, streak);
                    b += jack;
                }
                if (fire) b += (int)FireWinBonus;
                if (Level("wallride") > 0) b += System.Math.Min(walls, Level("wallride"));
                if (curved && Level("wizard") > 0) b += 1;
            }
            int bounty = Level("bounty");
            if (bounty > 0 && shots >= (bounty >= 3 ? 6 : 8)) b += bounty >= 5 ? 2 : 1;
            if (firstOfGame) b += Level("firstblood");
            return b;
        }
    }
}
