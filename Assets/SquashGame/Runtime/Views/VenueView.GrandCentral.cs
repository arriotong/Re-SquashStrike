// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.GrandCentral.cs
// 2026-11-22 evening (the user: "the tournament of champions new york venue
// needs a redo, the background looks too bare and empty, also research that
// venue in real life and add those cool lights and more details"): from
// public write-ups of the event - the glass court in Vanderbilt Hall under
// its chandeliers, stands on three sides, the front wall left open to the
// passageway where commuters stop and watch, lighting rigs hung over the
// court; the terminal's own marks - the four-faced clock on the information
// booth, the teal ceiling of stars in the Main Concourse beyond the arch:
//
//   the hall       marble pilasters with gold capitals, warm uplights, a
//                  cornice, and a grand chandelier over the court's end
//   the lights     beams from the rig sweeping the hall, banners hung from
//                  the coffers
//   the public     the passage beyond the front wall: a rope line, people
//                  walking through (and some stopped), the clock on its booth
//   the concourse  through the great arch: the teal vault and its stars
//
// Names and marks here are the place's and the event's own words, no
// sponsor's. Presentation only.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static partial class VenueView
    {
        static void GrandCentralMore(Ctx c, float hx, float front, float back, float wallH)
        {
            var marble = Mat(c, new Color(0.93f, 0.86f, 0.74f), 0.6f);
            var gold = Mat(c, Greybox.Hex(0xc8a050), 0.7f); gold.SetFloat("_Metallic", 0.7f);
            var warm = Glow(c, new Color(2.4f, 1.7f, 0.9f));
            // pilasters down both walls, gold at the top, a warm uplight at the foot washing up the stone
            var up = c.Palette.Clear(new Color(1.6f, 1.1f, 0.6f, 0.16f)); up.mainTexture = ProcTex.Beam();
            foreach (int s in new[] { -1, 1 })
                for (float z = front + 5f; z < back - 2f; z += 9f)
                {
                    float x = s * (hx - 0.6f);
                    Greybox.Box("Pilaster", c.Venue, marble, W(x, wallH / 2, z), new Vector3(1.2f, wallH, 1.6f));
                    Greybox.Box("Capital", c.Venue, gold, W(x, wallH - 1.2, z), new Vector3(1.6f, 1.0f, 2.0f), false);
                    Greybox.Box("Uplight", c.Venue, warm, W(x - s * 0.7f, 0.15, z), new Vector3(0.4f, 0.2f, 0.6f), false);
                    var q = Greybox.Primitive(PrimitiveType.Quad, "UplightWash", c.Venue, up, false);
                    q.transform.position = W(x - s * 0.75f, 7, z); q.transform.rotation = Quaternion.Euler(0, s > 0 ? -90f : 90f, 0);
                    q.transform.localScale = new Vector3(3f, 14f, 1f);
                }
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Cornice", c.Venue, gold, W(s * (hx - 0.4f), wallH - 2.2, (front + back) / 2), new Vector3(0.8f, 0.6f, back - front), false);
            // the grand chandelier over the open front of the court, and the rest all lit
            var bulbs = Glow(c, new Color(3f, 2.3f, 1.3f));
            var brass = Mat(c, Greybox.Hex(0xb08a3c), 0.75f); brass.SetFloat("_Metallic", 0.8f);
            Chandelier(c, W(0, 17, -12), 3.2f, bulbs, brass, true);
            Chandelier(c, W(0, 16.2, -12), 2.2f, bulbs, brass, false);

            // the rig's beams: tall soft cones from over the court, sweeping slowly across the hall
            var beamCols = new[] { new Color(0.6f, 0.8f, 1.6f, 0.10f), new Color(1.5f, 1.2f, 0.7f, 0.10f), new Color(1.2f, 0.5f, 1.4f, 0.08f) };
            for (int k = 0; k < 8; k++)
            {
                var m = c.Palette.Clear(beamCols[k % beamCols.Length]); m.mainTexture = ProcTex.Beam();
                var pivot = Pivot(c, "RigBeamPivot", W(k % 2 == 0 ? -HW - 0.8 : HW + 0.8, 9.2, 1 + (k / 2) * 3.0));
                var q = Greybox.Primitive(PrimitiveType.Quad, "RigBeam", c.Venue, m, false);
                q.transform.position = pivot.transform.position + Vector3.up * 7f;
                q.transform.localScale = new Vector3(2.6f, 14f, 1f);
                q.transform.SetParent(pivot.transform, true);
                pivot.transform.rotation = Quaternion.Euler(0, 90f, k % 2 == 0 ? 35f : -35f);
                Move(pivot, VenueMotion.Kind.Sway, Vector3.forward, 22f, 0.22f + 0.03f * k, k);
            }
            // banners hung from the coffers
            var bannerMat = LedMat(c.Palette, "tocbanner", "TOURNAMENT OF CHAMPIONS", Color.white, Greybox.Hex(0x14306a), 0.9f);
            foreach (float z in new[] { -26f, 30f })
                foreach (int s in new[] { -1, 1 })
                {
                    var b = Greybox.Box("Banner", c.Venue, bannerMat, W(s * 8f, wallH - 6, z), new Vector3(9f, 1.8f, 0.05f), false);
                    Move(b, VenueMotion.Kind.Sway, Vector3.up, 3f, 0.3f, s + z);
                }

            // the passage beyond the front wall: a rope line, the clock on its booth, people walking through and some stopped
            var rope = Mat(c, new Color(0.6f, 0.12f, 0.14f), 0.3f);
            for (float x = -14f; x <= 14f; x += 2f)
                Greybox.Box("Stanchion", c.Venue, gold, W(x, 0.5, -6.5), new Vector3(0.1f, 1.0f, 0.1f), false);
            Greybox.Box("Rope", c.Venue, rope, W(0, 0.9, -6.5), new Vector3(28f, 0.05f, 0.05f), false);
            var booth = Mat(c, Greybox.Hex(0x7a5a2a), 0.5f);
            Greybox.Box("InfoBooth", c.Venue, booth, W(-11, 0.7, -18), new Vector3(4.5f, 1.4f, 4.5f));
            Greybox.Box("BoothPost", c.Venue, gold, W(-11, 2.4, -18), new Vector3(0.3f, 2.2f, 0.3f), false);
            var clockHub = Pivot(c, "GrandClock", W(-11, 4.1, -18));
            Kid(c, "ClockBrass", clockHub.transform, gold, W(-11, 4.1, -18), new Vector3(1.6f, 1.6f, 1.6f), false);
            var face = Glow(c, new Color(2.2f, 1.9f, 1.3f));
            foreach (var d in new[] { new Vector3(0.82f, 0, 0), new Vector3(-0.82f, 0, 0), new Vector3(0, 0, 0.82f), new Vector3(0, 0, -0.82f) })
                Kid(c, "ClockFace", clockHub.transform, face, W(-11, 4.1, -18) + d, new Vector3(d.x != 0 ? 0.02f : 1.2f, 1.2f, d.z != 0 ? 0.02f : 1.2f), false);
            Kid(c, "ClockTop", clockHub.transform, gold, W(-11, 5.1, -18), new Vector3(0.3f, 0.6f, 0.3f), false);
            // walkers: dark coats, a few bright, crossing both ways along the passage
            var coats = new[] { Mat(c, new Color(0.12f, 0.12f, 0.14f), 0.2f), Mat(c, new Color(0.25f, 0.22f, 0.2f), 0.2f), Mat(c, new Color(0.5f, 0.12f, 0.12f), 0.2f), Mat(c, new Color(0.18f, 0.24f, 0.4f), 0.2f) };
            var skin = Mat(c, new Color(0.75f, 0.58f, 0.45f), 0.3f);
            var rng = new System.Random(281);
            for (int k = 0; k < 26; k++)
            {
                int dir = k % 2 == 0 ? 1 : -1;
                bool still = k % 5 == 0;
                float z = -9f - (float)rng.NextDouble() * 18f;
                var person = Pivot(c, "Commuter", W(still ? (float)rng.NextDouble() * 24f - 12f : -dir * 26f, 0, z));
                Kid(c, "Coat", person.transform, coats[rng.Next(coats.Length)], person.transform.position + Vector3.up * 1.05f, new Vector3(0.5f, 1.3f, 0.32f), false);
                Kid(c, "Legs", person.transform, c.Dark, person.transform.position + Vector3.up * 0.3f, new Vector3(0.36f, 0.6f, 0.26f), false);
                var hd = Greybox.Primitive(PrimitiveType.Sphere, "Head", c.Venue, skin, false);
                hd.transform.position = person.transform.position + Vector3.up * 1.88f; hd.transform.localScale = Vector3.one * 0.26f;
                hd.transform.SetParent(person.transform, true);
                if (still) person.transform.rotation = Quaternion.Euler(0, 180f, 0);
                else
                {
                    person.transform.rotation = Quaternion.Euler(0, dir > 0 ? 90f : -90f, 0);
                    Move(person, VenueMotion.Kind.Drift, Dir(dir, 0, 0), 1.2f + (float)rng.NextDouble() * 0.6f, 1f, (float)rng.NextDouble() * 40f, 52f);
                }
            }
            // through the great arch at the far end: the Main Concourse's teal vault and its stars
            var teal = Glow(c, new Color(0.18f, 0.42f, 0.44f));
            teal.mainTexture = ProcTex.LegacySky("concourse", new Color(0.10f, 0.30f, 0.32f), new Color(0.14f, 0.40f, 0.42f), new Color(0.12f, 0.36f, 0.38f), new Color(0.08f, 0.24f, 0.26f), 700, 283);
            Greybox.TexturedBox("ConcourseStars", c.Venue, teal, W(0, 13, front - 0.8f), new Vector3(16f, 18f, 0.05f), new Vector2(1f, 1f), false);
        }
    }
}
