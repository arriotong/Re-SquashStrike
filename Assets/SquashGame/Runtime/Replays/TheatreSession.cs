// ===========================================================
// Assets/SquashGame/Runtime/Replays/TheatreSession.cs
// P2-M6 Replay Theatre: watching a replay on the court.
//
//   Open      load the file, start the background pass (ReplayBuilder),
//             dress both athletes as they were, freeze the menus' demo
//   Play      a rally's frames through the runner's frame player
//             (speed 1 / 0.5 / 0.25, pause, scrub); the reel plays its
//             rallies one after another
//   Film      the cameras: broadcast, back wall, side, overhead, and a
//             free orbit you steer
//   Close     the court goes back to the menus' demo
// ===========================================================

using System.Collections.Generic;
using SquashGame.Progression;
using SquashSim;
using SquashSim.Replays;
using UnityEngine;

namespace SquashGame.Replays
{
    public enum TheatreCam { Broadcast, BackWall, Side, Overhead, Free, Director, FrontWall, Shoulder, Corner, BallCam }

    public sealed class TheatreSession
    {
        public static TheatreSession Active { get; private set; }

        public readonly ReplayEntry Entry;
        public readonly ReplayFile File;
        public readonly ReplayBuilder Builder;
        readonly MatchRunner _runner;

        /// <summary>The rally showing (-1: none yet).</summary>
        public int Rally { get; private set; } = -1;
        public RallyClip Clip { get { return Rally >= 0 ? Builder.Clips[Rally] : null; } }
        public TheatreCam Camera = TheatreCam.Director;          // camera work (2026-10-28): the director's cuts by default
        public static readonly double[] Speeds = { 1, 0.5, 0.25 };
        public int SpeedIndex { get; private set; }
        /// <summary>Playing the highlight reel (its rally list), or one rally.</summary>
        public List<int> Reel { get; private set; }
        int _reelAt;
        /// <summary>The reel plays the last moments of the winning shot again, slow - the director from two angles (the
        /// front-wall cam, then over the shoulder or the corner), a camera you chose from the side.</summary>
        bool _slowEnding;
        int _endingPass;
        TheatreCam _userCam = TheatreCam.Director;
        readonly ShotCamera _shots = new ShotCamera();

        /// <summary>The free camera, steered (yaw, pitch in degrees; distance in metres).</summary>
        public float FreeYaw = 25f, FreePitch = 22f, FreeDistance = 7.5f;

        TheatreSession(ReplayEntry entry, ReplayFile file, MatchRunner runner)
        {
            Entry = entry; File = file; _runner = runner;
            Builder = new ReplayBuilder(file);
        }

        /// <summary>Open a replay on the court (closes any open one).</summary>
        public static TheatreSession Open(ReplayEntry entry, ReplayFile file, MatchRunner runner)
        {
            if (Active != null) Active.Close();
            var t = new TheatreSession(entry, file, runner);
            Active = t;
            KitDirector.Preview = ReplayMeta.Kit(file, 0);
            KitDirector.OpponentPreview = ReplayMeta.Kit(file, 1);
            t.Builder.Run(12);
            return t;
        }

        public bool Playing { get { return _runner.FramesActive; } }
        public bool Paused { get { return _runner.FramesPaused; } set { _runner.FramesPaused = value; } }
        public double Speed { get { return Speeds[SpeedIndex]; } }
        /// <summary>0..1 through the rally showing.</summary>
        public float Position { get { return _runner.FrameCount <= 1 ? 0f : (float)(_runner.FrameCursor / (_runner.FrameCount - 1)); } set { _runner.FrameCursor = value * (_runner.FrameCount - 1); } }
        public double Cursor { get { return _runner.FrameCursor; } set { _runner.FrameCursor = value; } }
        /// <summary>The call has been made in the rally showing (the cursor is past its end).</summary>
        public bool PastTheCall { get { var c = Clip; return c != null && _runner.FrameCursor >= c.EndFrame; } }

        /// <summary>Each frame: carry the background pass on; move the reel along.</summary>
        public void Tick(float dt)
        {
            if (!Builder.Done) Builder.Run(6);
            var c = Clip;
            if (c == null || !Playing) return;
            bool atEnd = _runner.FrameCursor >= _runner.FrameCount - 1.001;
            if (Reel != null && atEnd && !Paused)
            {
                if (c.Tags != RallyTag.None && _endingPass < (_userCam == TheatreCam.Director ? 2 : 1))
                {
                    // the winning shot again: the last 2 seconds before the call, at half speed
                    _slowEnding = true; _endingPass++;
                    SetSpeed(1);
                    if (_userCam != TheatreCam.Director) Camera = TheatreCam.Side;
                    _shots.Reset();
                    _runner.FrameCursor = System.Math.Max(0, c.EndFrame - 2 * SimRunnerBase.FrameRate);
                    return;
                }
                _slowEnding = false; _endingPass = 0;
                _reelAt++;
                if (_reelAt < Reel.Count && Builder.Clips[Reel[_reelAt]].Ready) { SetSpeed(0); Camera = _userCam; Show(Reel[_reelAt]); }
                else if (_reelAt >= Reel.Count) Paused = true;
                else _reelAt--;                                         // not built yet: wait for it
            }
        }

        /// <summary>Play one rally (from its serve).</summary>
        public bool Play(int rally)
        {
            Reel = null;
            return Show(rally);
        }

        bool Show(int rally)
        {
            if (rally < 0 || rally >= Builder.Clips.Length || !Builder.Clips[rally].Ready) return false;
            Rally = rally;
            var c = Builder.Clips[rally];
            _runner.PlayFrames(c.Frames, c.Events);
            _runner.FrameSpeed = Speed;
            return true;
        }

        /// <summary>Play the highlight reel (rallies in order; each winning shot again, slow).</summary>
        public void PlayReel(List<int> reel = null)
        {
            Reel = reel ?? Highlights.Reel(File);
            _reelAt = 0; _slowEnding = false; _endingPass = 0;
            if (Camera == TheatreCam.Side && _userCam != TheatreCam.Side) Camera = _userCam;
            _userCam = Camera;
            SetSpeed(0);
            if (Reel.Count > 0) Show(Reel[0]);
        }

        public void SetSpeed(int index) { SpeedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1); _runner.FrameSpeed = Speed; }
        public void NextSpeed() { SetSpeed((SpeedIndex + 1) % Speeds.Length); }
        public void NextCamera() { Camera = (TheatreCam)(((int)Camera + 1) % System.Enum.GetValues(typeof(TheatreCam)).Length); _userCam = Camera; }

        /// <summary>The previous / next rally that is ready.</summary>
        public bool Step(int d)
        {
            int i = Rally + d;
            while (i >= 0 && i < Builder.Clips.Length) { if (Builder.Clips[i].Ready) return Play(i); i += d; }
            return false;
        }

        /// <summary>Stop watching: the court goes back to the menus' demo.</summary>
        public void Close()
        {
            if (Active == this) Active = null;
            KitDirector.Preview = null;
            KitDirector.OpponentPreview = null;
            _runner.StopFrames();
            _runner.BeginDemo();
        }

        // ---------------- the cameras ----------------

        Vector3 _pos, _look;
        bool _haveCam;
        TheatreCam _lastCam = (TheatreCam)(-1);
        int _lastRally = -2;

        /// <summary>Point the camera (called by the camera rig while frames show).</summary>
        public void Film(Camera cam, SimRunnerBase runner, float dt)
        {
            var v = runner.View;
            Vector3 ball = v.BallPos;
            Vector3 mid = v.Count > 1 ? (v.A0.Pos + v.A1.Pos) * 0.5f : v.A0.Pos;
            Vector3 action = Vector3.Lerp(mid, ball, 0.45f); action.y = Mathf.Clamp(ball.y, 0.6f, 2.2f) * 0.5f + 0.5f;
            Vector3 pos, look; float fov;
            if (Camera != _lastCam || Rally != _lastRally) { _haveCam = false; _lastCam = Camera; _lastRally = Rally; _shots.Reset(); }
            // camera work (2026-10-28): the director's cuts and the broadcast angles
            if (Camera >= TheatreCam.Director)
            {
                var c = Clip;
                Shot s;
                switch (Camera)
                {
                    case TheatreCam.FrontWall: s = Shot.FrontWall; break;
                    case TheatreCam.Shoulder: s = Shot.Shoulder; break;
                    case TheatreCam.Corner: s = Shot.Corner; break;
                    case TheatreCam.BallCam: s = Shot.BallCam; break;
                    default:
                        s = _slowEnding ? BroadcastDirector.EndingAngle(_endingPass - 1, Rally)
                                        : BroadcastDirector.Cut(Position, c != null && c.Tags != RallyTag.None, Rally);
                        break;
                }
                _shots.Film(cam, s, v, dt);
                return;
            }
            switch (Camera)
            {
                case TheatreCam.BackWall:                            // low at the back wall, looking down the court
                {
                    var bw = SimToWorld.Pos(0f, 1.25f, 10.4f);
                    pos = new Vector3(bw.x + action.x * 0.25f, bw.y, bw.z); look = Vector3.Lerp(action, ball, 0.3f); fov = 58; break;
                }
                case TheatreCam.Side:                                // in the side wall, level with the play
                {
                    var wall = SimToWorld.Pos(3.05f, 2.1f, 0f);
                    pos = new Vector3(wall.x, 2.1f, action.z); look = action; fov = 62; break;
                }
                case TheatreCam.Overhead:
                    pos = SimToWorld.Pos(0f, 9.5f, 5.6f); look = SimToWorld.Pos(0f, 0f, 4.9f); fov = 64; break;
                case TheatreCam.Free:
                {
                    float yaw = FreeYaw * Mathf.Deg2Rad, pitch = Mathf.Clamp(FreePitch, 3f, 80f) * Mathf.Deg2Rad;
                    var back = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
                    look = action; pos = action + back * Mathf.Clamp(FreeDistance, 2f, 16f); fov = 55;
                    break;
                }
                default:                                             // broadcast: high behind the glass, leaning with the play
                {
                    var b = SimToWorld.Pos(0f, 3.0f, 12.6f);
                    pos = new Vector3(b.x + action.x * 0.35f, b.y, b.z); look = Vector3.Lerp(SimToWorld.Pos(0f, 1.1f, 4.2f), action, 0.4f); fov = 44; break;
                }
            }
            float k = _haveCam ? 1f - Mathf.Exp(-dt * (Camera == TheatreCam.Free ? 14f : 6f)) : 1f;
            _pos = Vector3.Lerp(_pos, pos, k); _look = Vector3.Lerp(_look, look, k);
            _haveCam = true;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, k);
            cam.transform.position = _pos;
            cam.transform.LookAt(_look, Vector3.up);
        }

        /// <summary>A cut (a new camera or rally): no easing across it.</summary>
        public void Cut() { _haveCam = false; }
    }
}
