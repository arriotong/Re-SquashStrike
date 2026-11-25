// ===========================================================
// Assets/SquashGame/Tests/Editor/ReplayCameraTests.cs
// 2026-11-25 (the user: "some replay scenes like glitches the camera to the
// back of the wall so like the view is blocked"): the replay angles that
// follow the play - over the shoulder, the ball cam, the side glass, the
// front-wall cam - stay inside the court wherever the ball and the players
// are and wherever the ball is heading (the ball cam sat behind the front
// wall as the ball came off it).
// ===========================================================

using NUnit.Framework;
using SquashGame.Replays;
using UnityEngine;

namespace SquashGame.Tests
{
    public class ReplayCameraTests
    {
        static bool InCourt(Vector3 p)
        {
            float hw = (float)SquashSim.CourtDimensions.HALF_W, len = (float)SquashSim.CourtDimensions.LENGTH, h = (float)SquashSim.CourtDimensions.CEILING_H;
            return p.x > -hw && p.x < hw && p.z > 0.05f && p.z < len && p.y > 0f && p.y < h;
        }

        [Test]
        public void FollowingAnglesStayInsideTheCourt()
        {
            var shots = new[] { Shot.Shoulder, Shot.BallCam, Shot.SideGlass, Shot.FrontWall };
            var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, new Vector3(1, 0.3f, 1).normalized, new Vector3(-1, -0.2f, -1).normalized, Vector3.zero };
            int n = 0;
            for (float x = -3.1f; x <= 3.1f; x += 0.775f)
                for (float z = 0.05f; z <= 9.7f; z += 0.6f)
                    for (float y = 0.1f; y <= 4.5f; y += 1.1f)
                        foreach (var d in dirs)
                            foreach (var shot in shots)
                                foreach (float side in new[] { -1f, 1f })
                                {
                                    var v = new RenderSnapshot { BallPos = new Vector3(x, y, z), Count = 2 };
                                    v.A0.Pos = new Vector3(Mathf.Clamp(x * 1.1f, -3.1f, 3.1f), 0f, Mathf.Clamp(z + 1.2f, 0.2f, 9.7f));
                                    v.A1.Pos = new Vector3(-x * 0.5f, 0f, Mathf.Clamp(9.7f - z * 0.3f, 0.2f, 9.7f));
                                    for (int striker = 0; striker < 2; striker++)
                                    {
                                        BroadcastDirector.Frame(shot, v, d, striker, side, out var pos, out var look, out _);
                                        Assert.That(InCourt(pos), Is.True, shot + " camera at " + pos + " (ball " + v.BallPos + " heading " + d + ")");
                                        Assert.That((look - pos).sqrMagnitude, Is.GreaterThan(0.01f), shot + " looks somewhere");
                                        n++;
                                    }
                                }
            Debug.Log("[ReplayCamera] " + n + " frames, every following angle inside the court");
        }
    }
}
