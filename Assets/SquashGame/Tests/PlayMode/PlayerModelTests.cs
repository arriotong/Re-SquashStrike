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
            // the user's Mixamo clips at their moments: the dive (launch, landing, on the floor, rising), the swings at contact
            foreach (var (want, sec) in new[] { ("Dive_Left", 1.2f), ("Dive_Left", 1.6f), ("Dive_Left", 2.6f), ("Swing_Forehand", Athlete.AthleteBody.FhContact),
                                                ("Swing_Backhand", Athlete.AthleteBody.BhContact), ("Swing_Overhead", Athlete.AthleteBody.OhContact) })
                if (clips.ContainsKey(want)) shots.Add((want, sec / clips[want].length));
            Assert.That(clips.ContainsKey("Dive_Left") && clips.ContainsKey("Swing_Forehand") && clips.ContainsKey("Strafe_Right_Loop"), Is.True, "the Mixamo clips are in");

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
