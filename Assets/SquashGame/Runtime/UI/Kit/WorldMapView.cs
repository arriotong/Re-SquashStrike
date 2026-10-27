// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/WorldMapView.cs
// P7 phase 4 (2026-10-26): the career's world map, drawn (no image
// assets): stylised continents from coarse lon/lat outlines on an
// equirectangular grid (Alaska and the far south left off), the cities'
// dots, faint dots for the coming weeks' events, and a flight arc from
// where you are to the event in focus. Pins (MapPinItem) sit on top at
// Project(lon, lat).
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class WorldMapView : VisualElement
    {
        public const float World0 = -135, World1 = 180, WorldN = 70, WorldS = -50;
        public readonly float West, East, North, South;                 // this view's bounds (the world, or a zoom: the Europe inset)
        /// <summary>A zoom's bounds outlined on this map (lon0, lat0, lon1, lat1), or null.</summary>
        public Vector4? Outline;

        static readonly Color Ocean = new Color32(11, 16, 24, 255), Grid = new Color(1, 1, 1, 0.04f);
        static readonly Color Land = new Color32(34, 42, 54, 255), Coast = new Color(1, 1, 1, 0.10f);
        static readonly Color Lime = new Color32(200, 255, 61, 255);

        /// <summary>Faint dots (the coming weeks' events): lon, lat, colour.</summary>
        public readonly List<(float lon, float lat, Color c)> Dots = new List<(float, float, Color)>();
        Vector2? _from, _to;
        readonly bool _small;

        public WorldMapView(bool small = false, float west = World0, float east = World1, float north = WorldN, float south = WorldS)
        {
            _small = small; West = west; East = east; North = north; South = south;
            AddToClassList("k-map");
            pickingMode = PickingMode.Ignore;
            style.overflow = Overflow.Hidden;
            generateVisualContent += Paint;
        }

        /// <summary>A place on the map, in this element's local pixels.</summary>
        public Vector2 Project(float lon, float lat)
        {
            var r = contentRect;
            return new Vector2((lon - West) / (East - West) * r.width, (North - lat) / (North - South) * r.height);
        }

        /// <summary>The flight arc (null: none).</summary>
        public void Arc(Vector2? fromLonLat, Vector2? toLonLat)
        {
            if (_from == fromLonLat && _to == toLonLat) return;
            _from = fromLonLat; _to = toLonLat;
            MarkDirtyRepaint();
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 8) return;
            var p = mgc.painter2D;
            p.fillColor = Ocean;
            p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(r.width, 0)); p.LineTo(new Vector2(r.width, r.height)); p.LineTo(new Vector2(0, r.height)); p.ClosePath(); p.Fill();
            // the grid: every 30 degrees
            p.strokeColor = Grid; p.lineWidth = 1;
            for (int lon = -120; lon <= 180; lon += 30) { var a = Project(lon, North); var b = Project(lon, South); p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
            for (int lat = -30; lat <= 60; lat += 30) { var a = Project(West, lat); var b = Project(East, lat); p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
            // the land, then the inland seas
            foreach (var poly in MapShapes.Land) Shape(p, poly, Land, _small ? (Color?)null : Coast);
            foreach (var poly in MapShapes.Seas) Shape(p, poly, Ocean, null);
            // a zoom's box
            if (Outline.HasValue)
            {
                var o = Outline.Value;
                var a = Project(o.x, o.w); var b = Project(o.z, o.y);
                p.strokeColor = new Color(Lime.r, Lime.g, Lime.b, 0.5f); p.lineWidth = 1.5f;
                p.BeginPath(); p.MoveTo(a); p.LineTo(new Vector2(b.x, a.y)); p.LineTo(b); p.LineTo(new Vector2(a.x, b.y)); p.ClosePath(); p.Stroke();
            }
            // the coming weeks' events
            foreach (var (lon, lat, c) in Dots)
            {
                p.fillColor = new Color(c.r, c.g, c.b, 0.35f);
                p.BeginPath(); p.Arc(Project(lon, lat), _small ? 2.5f : 4f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            }
            // the flight
            if (_from.HasValue && _to.HasValue && _from.Value != _to.Value)
            {
                var a = Project(_from.Value.x, _from.Value.y); var b = Project(_to.Value.x, _to.Value.y);
                var mid = (a + b) * 0.5f + new Vector2(0, -Mathf.Min(140f, Vector2.Distance(a, b) * 0.3f));
                p.strokeColor = new Color(Lime.r, Lime.g, Lime.b, 0.85f); p.lineWidth = 2.5f;
                p.BeginPath(); p.MoveTo(a); p.QuadraticCurveTo(mid, b); p.Stroke();
            }
        }

        void Shape(Painter2D p, double[] pts, Color fill, Color? edge)
        {
            p.BeginPath();
            p.MoveTo(Project((float)pts[0], (float)pts[1]));
            for (int i = 2; i + 1 < pts.Length; i += 2) p.LineTo(Project((float)pts[i], (float)pts[i + 1]));
            p.ClosePath();
            p.fillColor = fill; p.Fill();
            if (edge.HasValue) { p.strokeColor = edge.Value; p.lineWidth = 1.2f; p.Stroke(); }
        }
    }

    /// <summary>The continents, coarse: lon, lat pairs (stylised - a squash tour's map, not an atlas).</summary>
    static class MapShapes
    {
        public static readonly double[][] Land =
        {
            // North America
            new double[] { -135,68, -135,58, -130,55, -124,48, -124,40, -117,32, -110,23, -105,20, -97,16, -92,15, -87,13, -83,9, -78,8, -77,9,
                          -83,10, -84,15, -88,16, -87,21, -90,21, -97,22, -97,26, -94,29.5, -89,30, -84,30, -82,26, -80,25, -81,31, -76,35, -74,40,
                          -70,42, -66,44, -60,46, -56,50, -60,55, -64,60, -70,61, -78,58, -82,53, -90,57, -95,61, -90,66, -100,68 },
            new double[] { -55,68, -55,66, -50,60, -43,60, -35,66, -30,68 },                                   // Greenland
            new double[] { -24,65.5, -22,66.3, -15,66.2, -13.5,65, -18,63.4, -22.5,63.8 },                     // Iceland
            new double[] { -85,21.9, -82,23, -77,22, -74.2,20.2, -77.5,19.9, -80,21.7 },                       // Cuba
            new double[] { -74.4,18.5, -72,19.9, -68.4,18.6, -71.5,17.6 },                                     // Hispaniola
            new double[] { -59.3,47.6, -56,51.6, -53,49.5, -52.7,47, -55.5,46.8 },                             // Newfoundland
            // South America
            new double[] { -80,10, -75,11, -72,12, -63,10.5, -60,8, -52,5, -50,0, -44,-2, -35,-5, -35,-9, -39,-14, -40,-21, -45,-23, -48,-26,
                          -53,-34, -58,-38, -62,-39, -65,-42, -67,-46, -68,-50, -74,-50, -74,-44, -73,-37, -71,-30, -70,-18, -76,-14, -80,-6,
                          -81,-3, -80,1, -78,4, -77,8 },
            // Eurasia: Iberia, up the Atlantic, the Baltic, Finland, across the north, down the Pacific, round Asia's south, India,
            // Arabia, the Levant, Turkey, Greece, the Adriatic, Italy, the Riviera, Spain
            new double[] { -9,37, -9,43.5, -1.5,43.5, -1,46.5, -4.5,48.5, 1.5,50.5, 4,51.5, 8,54, 10,54, 14,54, 20,54.5, 21,57, 24,59.5,
                          22,60.3, 21.5,61.5, 22.5,63.5, 25.5,65, 24.5,68, 70,68, 100,68, 140,68, 160,62, 163,58, 156,51, 143,59, 138,55,
                          141,48, 135,43, 129,41, 129,35, 126,35, 126,38, 121,40, 118,38, 122,37, 119,32, 122,30, 120,26, 116,23, 110,21,
                          108,22, 106,19, 109,12, 105,9, 101,13, 100,8, 103,1.5, 100,4, 98,8, 98,16, 94,17, 92,22, 87,21.5, 80,16, 80,10,
                          77,8, 73,16, 72.5,21, 68,23.5, 61,25, 57,25.5, 56,27, 50,30, 48,29, 50,26, 51.5,24, 56.3,26.3, 59,22.5, 57,19,
                          52,16, 43,12.5, 39,21, 35,28, 34,31.5, 35.5,36, 32,36.2, 30,36.3, 28,36.7, 26.5,38.5, 26,40, 23,40.5, 22,37,
                          21.5,38.5, 19.5,42, 16,43.5, 13.5,45.6, 12.4,44.5, 14,42, 16,41.5, 18.5,40, 16,38, 15.6,40, 12.5,41.8, 10.5,43,
                          8.5,44.3, 7,43.7, 3,43.3, 3,42, 0,39.5, -0.5,38, -2,36.7, -5.5,36, -6.5,37 },
            new double[] { 5,58.5, 5,62, 12,68, 25,68, 25,65.5, 21.5,63.5, 21,60.5, 18.5,60, 16.5,57, 14,55.5, 12.5,56, 11,58.5, 8,58 },   // Scandinavia
            new double[] { -5.5,50, 1.5,51, 1.7,52.8, 0,53.5, -1.5,55.5, -2,57, -1.8,57.6, -4,58.6, -5,58.6, -6,56.5, -5,55, -3,54.5,
                          -3,53.3, -4.6,52.8, -5,51.7, -3,51.5 },                                                                   // Great Britain
            new double[] { -10,51.6, -6,52.2, -6,54, -7.5,55.3, -10,54.3, -10,53 },                                                  // Ireland
            // Africa
            new double[] { -17,21, -17,15, -16.5,12, -13,8, -8,4.5, -3,5, 2,6.3, 5,4.3, 9,4, 9.5,1, 12,-5, 12,-9, 13.5,-12.5, 12,-17, 15,-27,
                          18,-32, 18.5,-34.3, 20,-34.8, 25,-34, 28,-32.5, 32.5,-28.5, 32.8,-25.8, 35.5,-24, 35,-20, 40.5,-15, 40.5,-10.5,
                          39,-6, 41,-1.5, 43.5,2, 48,5, 51,10.5, 51,12, 44,10.5, 43,12.5, 38.5,18, 37,22, 35,24.5, 33.8,27.5, 32.5,30,
                          32,31.2, 29,30.9, 25,31.7, 20,30.5, 19.8,32, 15,32.3, 11,33.3, 10,37, 8,37, 3,36.8, -1,35.8, -5,35.8, -6,35.6,
                          -9.5,32, -10,29.5, -13,27.5, -16,24 },
            new double[] { 49.2,-12, 50.5,-15.5, 47,-25, 44,-25, 43.3,-22, 44.5,-16, 47,-14 },                                     // Madagascar
            new double[] { 79.8,6.5, 80,9.8, 81.5,8, 81.8,6.9, 80.5,5.9 },                                                           // Sri Lanka
            new double[] { 130,31, 131.5,31.5, 132,33.8, 135,33.5, 137,34.6, 140,35, 141,38, 142,40, 141.5,41.5, 140,41.5, 139.8,40,
                          139.5,38, 137,37, 135.5,35.7, 132.5,35.4, 130.8,34, 129.8,33 },                                           // Japan
            new double[] { 140,42, 141.5,42.5, 144,43, 145.5,43.3, 142,45.5, 141.5,45 },                                             // Hokkaido
            new double[] { 120.2,22.5, 121,22, 122,25, 121.5,25.3, 120.1,23.5 },                                                     // Taiwan
            new double[] { 108.6,19.2, 110.5,20.1, 111,19.6, 109.6,18.2 },                                                           // Hainan
            new double[] { 95.5,5.5, 98,4, 104,-1, 106,-5.9, 103,-5, 101,-2.5, 98.5,0.5, 95.2,2.8 },                                // Sumatra
            new double[] { 109,1.5, 110,-2.8, 114.5,-4, 116.5,-2, 118,1, 119,5, 117,7, 115.5,5, 111,2.5 },                         // Borneo
            new double[] { 105.5,-6.8, 108,-6.3, 111,-6.5, 114.5,-7.8, 110,-8.3, 106,-7.5 },                                        // Java
            new double[] { 120,18.5, 122.3,18.3, 122,15, 124,13, 121.5,13.8, 120,15 },                                               // Luzon
            new double[] { 122,8, 125.5,9.5, 126.3,7, 125,5.8, 123.5,7.5 },                                                          // Mindanao
            new double[] { 131,-1.3, 137,-1.5, 141,-2.6, 147.5,-6, 150.5,-10.5, 144,-7.8, 141,-9.1, 138,-8.3, 135,-4.3, 132,-3 },   // New Guinea
            // Australia
            new double[] { 113.5,-22, 114,-26.5, 115,-34, 118,-35, 123,-34, 126,-32.3, 131,-31.5, 134,-32.6, 136,-35, 138,-34.5, 138.5,-35.6,
                          140.5,-38, 144,-38.5, 147,-38, 150,-37.5, 151.5,-33, 153.5,-28, 153,-25, 150.5,-22.5, 147,-19, 145.5,-15, 143.5,-14,
                          142.5,-10.7, 141.5,-13, 141.5,-17, 140,-17.5, 136.5,-15.5, 136.5,-12, 132.5,-11.5, 129.5,-15, 127,-14, 125,-15.5,
                          122,-17.5, 121,-19.5, 117,-20.7 },
            new double[] { 144.7,-40.7, 148.2,-41, 148,-43.2, 146.5,-43.5, 145.2,-42 },                                             // Tasmania
            new double[] { 172.7,-34.4, 174.5,-36.5, 175.5,-37, 178.5,-37.7, 177,-39.3, 176.8,-40, 175.2,-41.6, 174.6,-41.2, 174.6,-39.8,
                          173.7,-39.2, 174.6,-37.5 },                                                                               // New Zealand
            new double[] { 172.7,-40.5, 174.3,-41.7, 173.4,-42.6, 172.7,-43.8, 171.2,-44.5, 170.6,-45.9, 169,-46.6, 166.5,-46, 166.5,-45.3,
                          168.3,-44, 171.5,-41.7 },
        };

        public static readonly double[][] Seas =
        {
            new double[] { 28,41.5, 28.5,43.5, 30,45.5, 33,44.5, 36,45, 39,47, 38,44.5, 41.5,41.5, 36,41.7, 32,41.8 },              // the Black Sea
            new double[] { 47,45, 51,47, 53,45, 51,41, 53,40, 54,37, 51,36.6, 49,38, 49.5,40.5, 47,43 },                           // the Caspian
        };
    }
}
