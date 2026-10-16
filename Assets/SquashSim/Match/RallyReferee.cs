// ===========================================================
// Assets/SquashSim/Match/RallyReferee.cs
// The rules of a rally. An OBSERVER, like nick detection: it reads the
// step's events (racket contacts, impacts, body hits) and decides when
// the rally ends and why. It never changes the ball. Delete it and the
// court still plays; nobody wins.
//
// After every racket contact the ball must reach the front wall (side
// and back walls first are fine) above the tin and below the out lines,
// before touching the floor. After the front wall it may bounce once
// before the next player strikes it.
//
// The serve additionally: server has a foot in the box at contact
// (foot fault), the ball hits the FRONT WALL FIRST, above the service
// line, and its first bounce lands in the opposite back quarter -
// unless the receiver volleys it.
//
// Lines are OUT (walls) and FAULT (serve floor lines): a ball whose
// contact point touches the painted line is judged as if it hit beyond.
//
// Interference v2: appeals are decided by Interference.Decide (reach,
// effort, clearing). Ball hitting a player:
//   before the front wall, the striker      -> striker loses
//   before the front wall, the opponent     -> would it have reached the
//        front wall directly? Stroke to the striker. Via a side wall? Let.
//        Not good anyway (tin, floor, out)? The striker loses.
//   after the front wall, the next striker  -> they lose (touched the ball)
//   after the front wall, the non-striker   -> Stroke to the next striker
// ===========================================================

using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public enum RallyEnd
    {
        Down,        // tin, or the floor before the front wall
        Out,         // above an out line, ceiling, over the glass
        NotUp,       // second bounce before the next strike
        Fault,       // serve: below the service line, side wall first, wrong quarter
        FootFault,   // serve: server not in the box at contact
        HitSelf,     // own shot struck own body before the front wall
        Let,         // replay the rally (interference the opponent tried to clear; ball deflected off the opponent)
        NoLet,       // appeal rejected: the appellant loses the rally
        Stroke       // interference awarded to the striker: they win the rally
    }

    public sealed class RallyOutcome
    {
        public RallyEnd Kind;
        public int Winner = -1;      // -1 for a let
        public int Loser = -1;
        public string Reason;        // shown to the player: "Tin", "Out - side wall", ...
        public double Time;
        public V3 Point;

        public override string ToString()
        {
            return Kind + (Winner >= 0 ? " (P" + (Winner + 1) + " wins)" : "") + ": " + Reason;
        }
    }

    public sealed class RallyReferee
    {
        public int Server { get; private set; }
        public int ServeSide { get; private set; }          // +1 right box, -1 left box (as the server faces the front wall)

        /// <summary>Who may legally strike the ball next; -1 = nobody (ball travelling to the front wall).</summary>
        public int ExpectedStriker { get; private set; } = -1;

        /// <summary>Who will strike next once the ball reaches the front wall (the player going for it).</summary>
        public int PendingStriker
        {
            get
            {
                if (Outcome != null) return -1;
                if (ExpectedStriker >= 0) return ExpectedStriker;
                return LastHitter >= 0 ? Other(LastHitter) : -1;
            }
        }
        public int LastHitter { get; private set; } = -1;
        public bool InServe { get; private set; }
        public bool ServeStruck { get; private set; }
        public bool ServeVolleyed { get; private set; }
        public int Shots { get; private set; }                  // racket contacts this rally
        public RallyOutcome Outcome { get; private set; }
        public bool TossDropped { get; private set; }           // tossed ball fell untouched: re-toss, no fault

        /// <summary>The court, for projecting where a deflected ball would have gone.</summary>
        public System.Collections.Generic.List<Surface> Surfaces;
        readonly BallState _proj = new BallState();
        readonly System.Collections.Generic.List<SimEvent> _projEvents = new System.Collections.Generic.List<SimEvent>();

        bool _toFrontWall;           // struck, has not yet reached the front wall
        int _floorAfterFront;        // floor bounces since the front wall
        bool _serveLandingChecked;

        static int Other(int p) { return 1 - p; }

        public void BeginRally(int server, int serveSide)
        {
            Server = server;
            ServeSide = serveSide;
            ExpectedStriker = server;
            LastHitter = -1;
            InServe = true;
            ServeStruck = false;
            ServeVolleyed = false;
            Shots = 0;
            Outcome = null;
            TossDropped = false;
            _toFrontWall = false;
            _floorAfterFront = 0;
            _serveLandingChecked = false;
        }

        /// <summary>The server re-tosses after dropping the ball: the rally starts over, no fault.</summary>
        public void ResetToss()
        {
            TossDropped = false;
            ExpectedStriker = Server;
        }

        // ---------------- geometry helpers ----------------

        static double LineW { get { return PhysicsConfig.Current.Match.LineWidth; } }

        /// <summary>Box the server must stand in: side +1 = right (sim +x), -1 = left.</summary>
        public static void ServiceBox(int side, out double minX, out double maxX, out double minZ, out double maxZ)
        {
            if (side > 0) { minX = HALF_W - SERVICE_BOX; maxX = HALF_W; }
            else { minX = -HALF_W; maxX = -HALF_W + SERVICE_BOX; }
            minZ = SHORT_LINE_Z; maxZ = SHORT_LINE_Z + SERVICE_BOX;
        }

        public static bool InServiceBox(int side, V3 pos, double tolerance)
        {
            ServiceBox(side, out double minX, out double maxX, out double minZ, out double maxZ);
            return pos.X >= minX - tolerance && pos.X <= maxX + tolerance &&
                   pos.Z >= minZ - tolerance && pos.Z <= maxZ + tolerance;
        }

        /// <summary>Is this impact point out of court? Null if in.</summary>
        public static string OutReason(SimEvent ev)
        {
            double y = ev.Point.Y;
            switch (ev.SurfaceClass)
            {
                case "frontWall": return y >= FRONT_WALL_OUT_H - LineW ? "Out - front wall" : null;
                case "sideWall": return y >= SideWallOutHeight(ev.Point.Z) - LineW ? "Out - side wall" : null;
                case "backWall": return y >= BACK_WALL_OUT_H - LineW ? "Out - back wall" : null;
                case "ceiling": return "Out - ceiling";
                case "outBackstop": return "Out - over the glass";
                case "floor": return ev.Point.Z > LENGTH ? "Out - over the glass" : null;
                default: return null;
            }
        }

        void End(RallyEnd kind, int loser, string reason, SimEvent ev)
        {
            if (Outcome != null) return;
            Outcome = new RallyOutcome
            {
                Kind = kind,
                Loser = loser,
                Winner = kind == RallyEnd.Let || loser < 0 ? -1 : Other(loser),
                Reason = reason,
                Time = ev != null ? ev.Time : 0,
                Point = ev != null && ev.Point != null ? ev.Point.Clone() : null
            };
            ExpectedStriker = -1;
        }

        // ---------------- event handlers ----------------

        /// <param name="strikerPos">the striker's position at contact (foot-fault check)</param>
        public void OnRacketContact(SimEvent ev, V3 strikerPos)
        {
            if (Outcome != null) return;
            int p = ev.PlayerIndex;

            if (Shots == 0)
            {
                // The serve.
                if (p != Server) return;    // gated; cannot happen
                ServeStruck = true;
                if (!InServiceBox(ServeSide, strikerPos, PhysicsConfig.Current.Match.FootFaultTolerance))
                {
                    End(RallyEnd.FootFault, Server, "Foot fault", ev);
                    return;
                }
            }
            else if (Shots == 1 && !_serveLandingChecked)
            {
                ServeVolleyed = true;       // receiver took the serve before it bounced: landing rule waived
            }

            Shots++;
            if (Shots >= 2) InServe = false;
            LastHitter = p;
            _toFrontWall = true;
            _floorAfterFront = 0;
            ExpectedStriker = -1;
        }

        public void OnImpact(SimEvent ev)
        {
            if (Outcome != null) return;

            // Before the serve is struck the only thing that matters is a dropped toss.
            if (Shots == 0)
            {
                if (ev.SurfaceClass == "floor") { TossDropped = true; ExpectedStriker = -1; }
                return;
            }

            // The serve is in flight until anyone strikes the ball again.
            bool serving = Shots == 1;
            string outReason = OutReason(ev);

            if (_toFrontWall)
            {
                switch (ev.SurfaceClass)
                {
                    case "tin":
                        End(serving ? RallyEnd.Fault : RallyEnd.Down, LastHitter, serving ? "Fault - tin" : "Tin", ev);
                        return;
                    case "floor":
                        End(serving ? RallyEnd.Fault : RallyEnd.Down, LastHitter,
                            outReason ?? (serving ? "Fault - did not reach the front wall" : "Down - floor before the front wall"), ev);
                        return;
                    case "frontWall":
                        if (outReason != null) { End(serving ? RallyEnd.Fault : RallyEnd.Out, LastHitter, outReason, ev); return; }
                        if (serving && ev.Point.Y <= SERVICE_LINE_H)
                        {
                            End(RallyEnd.Fault, LastHitter, "Fault - below the service line", ev);
                            return;
                        }
                        _toFrontWall = false;
                        _floorAfterFront = 0;
                        ExpectedStriker = Other(LastHitter);
                        return;
                    default:
                        if (outReason != null) { End(serving ? RallyEnd.Fault : RallyEnd.Out, LastHitter, outReason, ev); return; }
                        if (serving) { End(RallyEnd.Fault, LastHitter, "Fault - side wall before the front wall", ev); return; }
                        return;     // side or back wall first: legal in a rally
                }
            }

            // ---- after the front wall ----
            // A serve that has already landed well is an ordinary ball from here on.
            if (outReason != null)
            {
                bool serveFault = serving && !_serveLandingChecked;
                End(serveFault ? RallyEnd.Fault : RallyEnd.Out, LastHitter, outReason, ev);
                return;
            }

            if (ev.SurfaceClass == "floor")
            {
                _floorAfterFront++;
                if (serving && !_serveLandingChecked)
                {
                    _serveLandingChecked = true;
                    // First bounce of a serve: opposite back quarter, lines are faults.
                    double half = LineW * 0.5;
                    bool deepEnough = ev.Point.Z > SHORT_LINE_Z + half;
                    bool rightHalf = -ServeSide * ev.Point.X > half;
                    if (!deepEnough || !rightHalf)
                    {
                        End(RallyEnd.Fault, LastHitter, !deepEnough ? "Fault - short of the short line" : "Fault - wrong side of the half-court line", ev);
                        return;
                    }
                }
                if (_floorAfterFront >= 2)
                {
                    End(RallyEnd.NotUp, Other(LastHitter), "Not up - second bounce", ev);
                }
            }
        }

        public void OnBodyHit(SimEvent ev)
        {
            if (Outcome != null) return;
            int body = ev.PlayerIndex;
            if (Shots == 0)
            {
                // The tossed ball touched the server's own body before being struck.
                if (body == Server) { End(RallyEnd.Fault, Server, "Fault - toss hit the server", ev); }
                return;
            }
            if (LastHitter < 0) return;

            if (_toFrontWall)
            {
                if (body == LastHitter) { End(RallyEnd.HitSelf, LastHitter, "Ball hit the striker", ev); return; }
                // Hit the opponent on its way to the front wall: where would it have gone?
                switch (WouldHaveBeen(ev))
                {
                    case 1: End(RallyEnd.Stroke, Other(LastHitter), "Stroke - ball hit P" + (body + 1) + " going straight to the front wall", ev); return;
                    case 2: End(RallyEnd.Let, -1, "Let - ball hit P" + (body + 1) + " going via the side wall", ev); return;
                    default: End(RallyEnd.Down, LastHitter, "Ball hit P" + (body + 1) + " but would not have been good", ev); return;
                }
            }

            // After the front wall.
            int striker = PendingStriker;
            if (body == striker) End(RallyEnd.HitSelf, striker, "Ball touched P" + (striker + 1) + " before they played it", ev);
            else End(RallyEnd.Stroke, body, "Stroke - ball hit the non-striker after the front wall", ev);
        }

        /// <summary>
        /// Project the deflected ball from where it met the body with the velocity it had:
        /// 1 = front wall directly (good), 2 = side/back wall then front wall (good), 0 = not good.
        /// </summary>
        int WouldHaveBeen(SimEvent ev)
        {
            if (Surfaces == null || ev.InPos == null || ev.InVel == null) return 2;
            _proj.Reset(ev.InPos, ev.InVel, null, ev.Temperature);
            bool otherWallFirst = false;
            double dt = 1.0 / 240;
            for (int i = 0; i < 480; i++)
            {
                _projEvents.Clear();
                BallPhysics.Step(_proj, Surfaces, dt, _projEvents);
                foreach (var e in _projEvents)
                {
                    if (OutReason(e) != null) return 0;
                    if (e.SurfaceClass == "tin" || e.SurfaceClass == "floor") return 0;
                    if (e.SurfaceClass == "frontWall") return otherWallFirst ? 2 : 1;
                    otherWallFirst = true;
                }
            }
            return 0;
        }

        /// <summary>
        /// "Let, please." Only the player going for the ball may appeal; anyone else's appeal is ignored.
        /// The decision (No let / Let / Stroke) comes from Interference.Decide.
        /// </summary>
        public bool OnAppeal(int appellant, InterferenceDecision decision, double time)
        {
            if (Outcome != null || Shots == 0 || appellant != PendingStriker) return false;
            var ev = new SimEvent { Time = time };
            switch (decision.Call)
            {
                case InterferenceCall.Let: End(RallyEnd.Let, -1, decision.Reason, ev); break;
                case InterferenceCall.Stroke: End(RallyEnd.Stroke, Other(appellant), decision.Reason, ev); break;
                default: End(RallyEnd.NoLet, appellant, decision.Reason, ev); break;
            }
            return true;
        }
    }
}
