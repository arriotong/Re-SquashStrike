// ===========================================================
// Assets/SquashGame/Tests/PlayMode/PlayerModelTests.cs
// 2026-11-01 the real players: a MakeHuman body loads as a Humanoid,
// dresses in a kit, and plays the motion capture - a contact sheet of
// it on the court (Captures/players/<id>_sheet.png): T-pose, idle,
// walk, jog, sprint, roll, from the side and from the front.
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class PlayerModelTests
    {
        /// <summary>All twelve bodies (male and female, the six skin tones) in a row, each in another hair style and kit, on
        /// the court in the idle: Captures/players/lineup.png (front) and lineup_back.png.</summary>
        [UnityTest]
        public IEnumerator TheLineUp()
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<MatchCourt>();
            var a0 = GameObject.Find("Athlete0").GetComponent<Athlete.AthleteFigure>();
            Vector3 spot = a0.transform.position, fwd = a0.transform.forward; fwd.y = 0; fwd.Normalize();      // toward the front wall
            Vector3 across = Vector3.Cross(Vector3.up, fwd);
            foreach (var f in Object.FindObjectsByType<Athlete.AthleteFigure>(FindObjectsSortMode.None)) f.gameObject.SetActive(false);
            var clips = Athlete.PlayerModels.Clips();
            Color[] shirts = { new Color(0.85f, 0.15f, 0.15f), new Color(0.1f, 0.35f, 0.85f), new Color(0.78f, 1f, 0.24f), Color.white, new Color(0.45f, 0.2f, 0.7f), new Color(0.1f, 0.5f, 0.3f) };
            Color[] hairs = { new Color(0.09f, 0.07f, 0.06f), new Color(0.21f, 0.14f, 0.09f), new Color(0.77f, 0.6f, 0.36f), new Color(0.48f, 0.2f, 0.09f) };
            var graphs = new List<PlayableGraph>();
            int n = 0;
            var ids = new List<string> { Athlete.PlayerModels.You, Athlete.PlayerModels.Opponent, Athlete.PlayerModels.You, Athlete.PlayerModels.Opponent };
            for (int t = 0; t < 6; t++) ids.Add(Athlete.PlayerModels.IdFor(true, t));
            foreach (var id in ids)
                {
                    int t = n < 4 ? (n % 2 == 0 ? 0 : 2) : n - 4;
                    var parts = new List<Athlete.PlayerModels.Part>();
                    var go = Athlete.PlayerModels.Spawn(id, null, court.Palette.Lit, parts);
                    Assert.That(go, Is.Not.Null, id);
                    Assert.That(go.GetComponent<Animator>().avatar.isHuman, Is.True, id);
                    Athlete.PlayerModels.Dress(parts, shirts[n % 6], n % 2 == 0 ? Color.black : Color.white, Color.white, Color.white, hairs[n % 4], 1 + n % 6, Athlete.PlayerModels.Tones[t]);
                    go.transform.position = new Vector3(spot.x, 0, spot.z) + across * ((n - (ids.Count - 1) * 0.5f) * 0.5f);
                    go.transform.rotation = Quaternion.LookRotation(fwd);
                    var gr = PlayableGraph.Create("lineup" + n);
                    var cp = AnimationClipPlayable.Create(gr, clips["Idle_Loop"]);
                    AnimationPlayableOutput.Create(gr, "out", go.GetComponent<Animator>()).SetSourcePlayable(cp);
                    cp.SetTime(0.2f * n); gr.Evaluate(0);
                    graphs.Add(gr);
                    n++;
                }
            yield return null;
            var cam = Camera.main;
            var rigCam = cam.GetComponent<GameplayCameraRig>(); if (rigCam != null) rigCam.enabled = false;
            Directory.CreateDirectory(Path.Combine("Captures", "players"));
            foreach (var (file, back) in new[] { ("lineup.png", false), ("lineup_back.png", true) })
            {
                cam.fieldOfView = 52f;
                Vector3 c = new Vector3(spot.x, 1.0f, spot.z);
                cam.transform.position = c + (back ? -fwd : fwd) * 4.4f + Vector3.up * 0.15f;
                cam.transform.LookAt(c);
                var rt = new RenderTexture(2400, 900, 24);
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(2400, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 2400, 900), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine("Captures", "players", file), tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null;
            }
            foreach (var gr in graphs) gr.Destroy();
        }

        /// <summary>2026-11-02 our squash motion capture (Rokoko Vision from real rally footage) played straight through on the
        /// player, from behind and the side: Captures/players/mocap_&lt;clip&gt;.png.</summary>
        [UnityTest]
        public IEnumerator OurSquashMocapOnThePlayer()
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<MatchCourt>();
            var a0 = GameObject.Find("Athlete0").GetComponent<Athlete.AthleteFigure>();
            Vector3 spot = a0.transform.position, fwd = a0.transform.forward; fwd.y = 0; fwd.Normalize();
            foreach (var f in Object.FindObjectsByType<Athlete.AthleteFigure>(FindObjectsSortMode.None)) f.gameObject.SetActive(false);
            var parts = new List<Athlete.PlayerModels.Part>();
            var go = Athlete.PlayerModels.Spawn(Athlete.PlayerModels.You, null, court.Palette.Lit, parts);
            Athlete.PlayerModels.Dress(parts, new Color(0.78f, 1f, 0.24f), Color.white, Color.white, Color.white, new Color(0.2f, 0.14f, 0.1f), 2, Athlete.PlayerModels.Tones[1]);
            go.transform.position = new Vector3(spot.x, 0, spot.z); go.transform.rotation = Quaternion.LookRotation(fwd);
            var anim = go.GetComponent<Animator>(); anim.applyRootMotion = false;
            var clips = Athlete.PlayerModels.Clips();
            var cam = Camera.main; var rigCam = cam.GetComponent<GameplayCameraRig>(); if (rigCam != null) rigCam.enabled = false;
            Directory.CreateDirectory(Path.Combine("Captures", "players"));
            foreach (var name in new[] { "Mocap_EliteRally", "Mocap_LowStance", "Mocap_Swings" })
            {
                Assert.That(clips.ContainsKey(name), Is.True, name);
                var c = clips[name];
                const int CELL = 300, N = 12;
                var sheet = new Texture2D(N * CELL, 2 * CELL, TextureFormat.RGB24, false);
                var rt = new RenderTexture(CELL, CELL, 24);
                for (int i = 0; i < N; i++)
                {
                    var g = PlayableGraph.Create("mocap");
                    var cp = AnimationClipPlayable.Create(g, c);
                    AnimationPlayableOutput.Create(g, "out", anim).SetSourcePlayable(cp);
                    cp.SetTime(c.length * i / N); g.Evaluate(0);
                    yield return null;
                    for (int row = 0; row < 2; row++)
                    {
                        Vector3 p = go.transform.position;
                        cam.fieldOfView = 42f;
                        cam.transform.position = row == 0 ? p - fwd * 3.6f + Vector3.up * 1.5f : p + Vector3.Cross(Vector3.up, fwd) * 3.6f + Vector3.up * 1.2f;
                        cam.transform.LookAt(p + Vector3.up * 0.8f);
                        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var cell = new Texture2D(CELL, CELL, TextureFormat.RGB24, false);
                        cell.ReadPixels(new Rect(0, 0, CELL, CELL), 0, 0); cell.Apply();
                        sheet.SetPixels(i * CELL, (1 - row) * CELL, CELL, CELL, cell.GetPixels());
                        RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(cell);
                    }
                    g.Destroy();
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine("Captures", "players", "mocap_" + name + ".png"), sheet.EncodeToPNG());
            }
        }

        /// <summary>2026-11-02 a real rally (both athletes on autopilot) with our squash capture as the players' animation: each
        /// stroke photographed at the ball and in its follow-through from beside the striker (Captures/players/rally.png), the
        /// racket hand close up (grip.png); the racket is drawn in the body's hand near where the sim has it, both wings played.</summary>
        [UnityTest]
        public IEnumerator ARallyWithOurSquashMoves()
        {
            LocalMatchTests.FreshProfile();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            Object.FindFirstObjectByType<UI.FrontEnd>().StartMatch(MatchSettings.QuickGame());
            var r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            r.SetAutopilot(new SquashSim.OpponentBrain(SquashSim.AiProfile.ByName("retriever").WithDifficulty(0.9), 11));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 4f) yield return null;          // past the walk-on
            var bodies = Object.FindObjectsByType<Athlete.AthleteBody>(FindObjectsSortMode.None);
            Assert.That(bodies.Length, Is.EqualTo(2));
            foreach (var b in bodies) Assert.That(b.HasMoves, Is.True, "our squash capture is in");

            var camGo = new GameObject("RallyCam"); var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 40f;
            const int CELL = 320, COLS = 8, ROWS = 3;
            var sheet = new Texture2D(COLS * CELL, ROWS * CELL, TextureFormat.RGB24, false);
            var grip = new Texture2D(COLS * CELL, 2 * CELL, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CELL, CELL, 24);
            System.Action<Texture2D, int, int> shoot = (into, x, y) =>
            {
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var cell = new Texture2D(CELL, CELL, TextureFormat.RGB24, false);
                cell.ReadPixels(new Rect(0, 0, CELL, CELL), 0, 0); cell.Apply();
                into.SetPixels(x * CELL, y * CELL, CELL, CELL, cell.GetPixels());
                RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(cell);
            };
            var seen = new List<string>(); var counts = new Dictionary<string, int>();
            int shots = 0, grips = 0; var gaps = new List<float>(); var elbows = new List<float>();
            var locked = new Dictionary<Athlete.AthleteBody, string>(); int changed = 0;
            var last = new Dictionary<Athlete.AthleteBody, string>();
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 50f && shots < COLS * ROWS)
            {
                yield return null;                                  // (the transforms hold last frame's drawn pose: the grip, the racket in the hand)
                foreach (var b in bodies)
                {
                    if (!b.Showing) continue;
                    gaps.Add(b.GripGap);
                    if (b.FollowShown > 0.99f)
                    {
                        Vector3 up = b.Anim.GetBoneTransform(HumanBodyBones.RightLowerArm).position - b.Anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                        Vector3 low = b.Anim.GetBoneTransform(HumanBodyBones.RightHand).position - b.Anim.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
                        elbows.Add(Vector3.Angle(up, low));
                    }
                    locked.TryGetValue(b, out var held);
                    if (!b.MoveLocked) locked[b] = "";
                    else if (b.MoveShown != "") { if (held != "" && held != null && held != b.MoveShown) changed++; locked[b] = b.MoveShown; }
                    var v = b.Figure.Posed;
                    string phase = b.MoveShown == "" ? "" : v.Swing == SwingPhase.Swinging && v.SwingT >= 0.45f && v.SwingT < 0.75f ? "ball" :
                                   v.Swing == SwingPhase.Recovering || (v.Swing == SwingPhase.Swinging && v.SwingT >= 0.9f) ? "follow" : "";
                    string key = phase == "" ? "" : b.MoveShown + "/" + phase;
                    last.TryGetValue(b, out var was);
                    if (key == "" || key == was) { last[b] = key; continue; }
                    last[b] = key;
                    counts.TryGetValue(b.MoveShown, out int k); counts[b.MoveShown] = k + (phase == "ball" ? 1 : 0);
                    if (phase == "ball" && !seen.Contains(b.MoveShown)) seen.Add(b.MoveShown);
                    // from beside the striker (their right), a little behind, at chest height
                    Vector3 p = b.Figure.transform.position, f = b.Figure.DrawnForward; f.y = 0; f.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, f);
                    cam.transform.position = p + side * 3.0f - f * 1.6f + Vector3.up * 1.35f;
                    cam.transform.LookAt(p + Vector3.up * 0.85f);
                    shoot(sheet, shots % COLS, ROWS - 1 - shots / COLS);
                    shots++;
                    if (phase == "ball" && grips < COLS)
                    {
                        // the palm side and the back of the hand
                        var hand = b.Anim.GetBoneTransform(HumanBodyBones.RightHand);
                        Vector3 mid = b.Anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position;
                        Vector3 knuckles = (b.Anim.GetBoneTransform(HumanBodyBones.RightIndexProximal).position - b.Anim.GetBoneTransform(HumanBodyBones.RightLittleProximal).position).normalized;
                        Vector3 palm = Vector3.Cross((mid - hand.position).normalized, knuckles).normalized;
                        Vector3 c = Vector3.Lerp(hand.position, mid, 0.5f);
                        for (int view = 0; view < 2; view++)
                        {
                            cam.transform.position = c + (view == 0 ? palm : -palm) * 0.45f + knuckles * 0.12f;
                            cam.transform.LookAt(c);
                            shoot(grip, grips, 1 - view);
                        }
                        grips++;
                    }
                }
            }
            sheet.Apply(); grip.Apply();
            Directory.CreateDirectory(Path.Combine("Captures", "players"));
            File.WriteAllBytes(Path.Combine("Captures", "players", "rally.png"), sheet.EncodeToPNG());
            File.WriteAllBytes(Path.Combine("Captures", "players", "grip.png"), grip.EncodeToPNG());
            var tally = new List<string>(); foreach (var kv in counts) tally.Add(kv.Key + " x" + kv.Value);
            gaps.Sort();
            float g50 = gaps[gaps.Count / 2], g90 = gaps[(int)(gaps.Count * 0.9f)], gMax = gaps[gaps.Count - 1];
            Debug.Log("[Rally] strokes at the ball: " + string.Join(", ", tally) + "; the racket drawn off the sim's by " + g50.ToString("0.000") +
                      " m (median), " + g90.ToString("0.000") + " (90%), " + gMax.ToString("0.000") + " (worst)");
            Assert.That(elbows.Count, Is.GreaterThan(0), "follow-throughs were seen");
            elbows.Sort();
            Debug.Log("[Rally] the elbow's bend in the finish: " + elbows[elbows.Count / 2].ToString("0") + " deg (median), " + elbows[elbows.Count - 1].ToString("0") + " (worst)");
            Assert.That(elbows[elbows.Count / 2], Is.LessThan(20f), "the arm finishes straight out");
            Assert.That(seen.Exists(m => m.StartsWith("fh_")), Is.True, "a forehand");
            Assert.That(seen.Exists(m => m.StartsWith("bh_")), Is.True, "a backhand");
            Assert.That(changed, Is.EqualTo(0), "a stroke's move never changes once it has committed");
            Assert.That(g90, Is.LessThan(0.01f), "the body's arm takes the racket where the sim has it");
            Assert.That(gMax, Is.LessThan(0.05f), "never far off it");
            Object.Destroy(camGo);
        }

        /// <summary>2026-11-07 one situation, one move: the same facts always give the same row, and each situation has its own.</summary>
        [Test]
        public void EachSituationHasItsOneMove()
        {
            int Pick(bool fore, int reach, float h, bool touch = false, bool serve = false) { return Athlete.MoveBook.Pick(fore, reach, h, touch, serve); }
            Assert.That(Pick(true, 0, 1.0f), Is.EqualTo(Athlete.MoveBook.FhDrive));
            Assert.That(Pick(false, 0, 1.0f), Is.EqualTo(Athlete.MoveBook.BhDrive));
            Assert.That(Pick(true, 1, 1.0f), Is.EqualTo(Athlete.MoveBook.FhLunge), "a reach");
            Assert.That(Pick(false, 3, 1.0f), Is.EqualTo(Athlete.MoveBook.BhLunge), "a stretch");
            Assert.That(Pick(true, 0, 0.3f), Is.EqualTo(Athlete.MoveBook.FhLunge), "off the floor");
            Assert.That(Pick(true, 0, 1.0f, touch: true), Is.EqualTo(Athlete.MoveBook.FhTouch));
            Assert.That(Pick(false, 0, 1.0f, touch: true), Is.EqualTo(Athlete.MoveBook.BhTouch));
            Assert.That(Pick(true, 0, 1.8f), Is.EqualTo(Athlete.MoveBook.FhHigh));
            Assert.That(Pick(false, 0, 1.8f), Is.EqualTo(Athlete.MoveBook.BhHigh));
            Assert.That(Pick(false, 2, 0.2f, true, true), Is.EqualTo(Athlete.MoveBook.Serve), "the serve first");
            for (int i = 0; i < Athlete.MoveBook.All.Length; i++) Assert.That(Pick(true, 0, 1.0f), Is.EqualTo(Athlete.MoveBook.FhDrive), "nothing random");
        }

        [UnityTest]
        public IEnumerator ABodyLoadsDressesAndPlaysTheMotionCapture()
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<MatchCourt>();
            var spot = GameObject.Find("Athlete0").transform.position;
            foreach (var f in Object.FindObjectsByType<Athlete.AthleteFigure>(FindObjectsSortMode.None)) f.gameObject.SetActive(false);

            var parts = new List<Athlete.PlayerModels.Part>();
            var go = Athlete.PlayerModels.Spawn(Athlete.PlayerModels.You, null, court.Palette.Lit, parts);
            Assert.That(go, Is.Not.Null, "Resources/Players/" + Athlete.PlayerModels.You);
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.applyRootMotion = false;                                  // as in the game: the sim moves the athlete
            Assert.That(anim.avatar, Is.Not.Null, "an avatar");
            Assert.That(anim.avatar.isHuman && anim.avatar.isValid, Is.True, "a valid Humanoid avatar");
            Assert.That(parts.Exists(p => p.Kind == "skin") && parts.Exists(p => p.Kind == "shirt") && parts.Exists(p => p.Kind == "shoes") && parts.Exists(p => p.Kind == "hair"), Is.True);
            Athlete.PlayerModels.Dress(parts, new Color(0.1f, 0.35f, 0.85f), new Color(0.08f, 0.08f, 0.1f), Color.white, Color.white, new Color(0.25f, 0.17f, 0.1f), 2, Athlete.PlayerModels.Tones[0]);
            Assert.That(parts.FindAll(p => p.Kind == "hair" && p.R.enabled).Count, Is.EqualTo(1), "one hair shown");

            var clips = Athlete.PlayerModels.Clips();
            Debug.Log("[Players] clips: " + string.Join(", ", clips.Keys));
            Assert.That(clips.Count, Is.GreaterThan(5));

            go.transform.position = new Vector3(spot.x, 0, spot.z);
            var cam = Camera.main;
            var rigCam = cam.GetComponent<GameplayCameraRig>(); if (rigCam != null) rigCam.enabled = false;
            var shots = new List<(string clip, float t)> { (null, 0) };
            foreach (var want in new[] { "Jog_Fwd_Loop", "Sprint_Loop", "Strafe_Left_Loop", "Jog_Back_Loop" })
                if (clips.ContainsKey(want)) { shots.Add((want, 0.25f)); shots.Add((want, 0.75f)); }
            // the user's Mixamo dive (launch, landing, on the floor, rising); our squash capture's moves at the ball
            foreach (var (want, sec) in new[] { ("Dive_Left", 1.2f), ("Dive_Left", 1.6f), ("Dive_Left", 2.6f) })
                if (clips.ContainsKey(want)) shots.Add((want, sec / clips[want].length));
            foreach (var m in Athlete.MoveBook.All)
            {
                Assert.That(clips.ContainsKey(m.Clip), Is.True, m.Clip);
                Assert.That(m.Start < m.Contact && m.Contact < m.End && m.End <= m.Recover && m.Recover <= clips[m.Clip].length + 0.02f, Is.True, m.Clip + " moments in order, inside the clip");
                shots.Add((m.Clip, m.Contact / clips[m.Clip].length));
            }
            Assert.That(clips.ContainsKey("Dive_Left") && clips.ContainsKey("Strafe_Right_Loop"), Is.True, "the Mixamo clips are in");

            const int CELL = 360;
            int cols = shots.Count;
            var sheet = new Texture2D(cols * CELL, 2 * CELL, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CELL, CELL, 24);
            for (int i = 0; i < shots.Count; i++)
            {
                PlayableGraph g = default;
                if (shots[i].clip != null)
                {
                    g = PlayableGraph.Create("sheet");
                    var c = clips[shots[i].clip];
                    var cp = AnimationClipPlayable.Create(g, c);
                    var output = AnimationPlayableOutput.Create(g, "out", anim);
                    output.SetSourcePlayable(cp);
                    cp.SetTime(shots[i].t * c.length);
                    g.Evaluate(0);
                }
                yield return null;
                for (int row = 0; row < 2; row++)
                {
                    Vector3 p = go.transform.position;
                    cam.fieldOfView = 34f;
                    cam.transform.position = row == 0 ? p + new Vector3(3.6f, 1.1f, 0.2f) : p + new Vector3(0.4f, 1.2f, -3.6f);
                    cam.transform.LookAt(p + Vector3.up * 0.95f);
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt;
                    var cell = new Texture2D(CELL, CELL, TextureFormat.RGB24, false);
                    cell.ReadPixels(new Rect(0, 0, CELL, CELL), 0, 0); cell.Apply();
                    sheet.SetPixels(i * CELL, (1 - row) * CELL, CELL, CELL, cell.GetPixels());
                    RenderTexture.active = null; cam.targetTexture = null;
                    Object.Destroy(cell);
                }
                if (g.IsValid()) g.Destroy();
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.Combine("Captures", "players"));
            File.WriteAllBytes(Path.Combine("Captures", "players", "you_sheet.png"), sheet.EncodeToPNG());
        }
    }
}
