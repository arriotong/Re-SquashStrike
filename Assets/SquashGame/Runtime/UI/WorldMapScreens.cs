// ===========================================================
// Assets/SquashGame/Runtime/UI/WorldMapScreens.cs
// P7 phase 4 (2026-10-26): the World Tour on a map.
//
//   THIS WEEK   the map: a pin for every event this week (its tier's
//               colour) and your home club's open; where you are and
//               your home; the coming weeks' events as faint dots. The
//               pin in focus draws your flight there, and the panel
//               shows the event: prize money, the points, the field's
//               top seeds, how you'd get in (your ranking against the
//               cut-off, a wildcard, qualifying), the trip (flight from
//               where you are + hotel nights) and your cash after it.
//               A on an open pin: confirm the trip, fly, enter.
//   PLANNER     the next six weeks from here: every event with its trip
//               cost from where you are now and your chances, so a run
//               can be planned (staying in a region keeps flights cheap).
//
// The pins are a map you browse, not choices (ChoiceBudgetTests).
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>An event on the map: a dot in its tier's colour and the city's name.</summary>
    public sealed class MapPinItem : MenuItem
    {
        public readonly CareerSession.Option Option;
        public readonly float Lon, Lat;
        /// <summary>The map it sits on (the world, or the Europe inset).</summary>
        public WorldMapView View;
        readonly System.Action<MapPinItem> _pick;
        public readonly Label Tag;
        public override bool WantsSlant { get { return false; } }

        public MapPinItem(CareerSession.Option o, float lon, float lat, Color colour, System.Action<MapPinItem> pick) : base("pin_" + o.Index)
        {
            Option = o; Lon = lon; Lat = lat; _pick = pick;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-pin");
            if (!o.Open) Root.AddToClassList("k-pin--closed");
            if (o.Home) Root.AddToClassList("k-pin--home");
            LabelEl.style.display = DisplayStyle.None;
            var dot = Kit.Box(Root, "k-pin-dot");
            dot.style.backgroundColor = colour;
            Tag = Kit.L(Root, o.Home ? "CLUB OPEN" : o.City.ToUpperInvariant(), "k-pin-tag");
        }

        public override void Submit() { _pick?.Invoke(this); }
    }

    public sealed class WorldMapScreen : GameScreen
    {
        const int You = CareerSession.You;
        protected override string Kicker { get { return "WORLD TOUR  ·  " + Career.When(Career.S); } }
        protected override string Title { get { return "The world map"; } }

        public WorldMapView Map { get; private set; }
        /// <summary>The Europe inset: a zoom of Europe when a week has 3+ events there (null otherwise).</summary>
        public WorldMapView Europe { get; private set; }
        public static readonly Vector4 EuropeBox = new Vector4(-11, 36, 30, 61);    // lon0, lat0, lon1, lat1
        public const int EuropeAt = 3;
        public readonly List<MapPinItem> Pins = new List<MapPinItem>();
        VisualElement _detail;
        VisualElement _you, _home, _youE, _homeE;
        MenuItem _shown;
        bool _placed;

        protected override void Compose()
        {
            var c = Career.C;
            var s = c.S;

            // ---- THIS WEEK ----
            var page = Kit.Box(Page("THIS WEEK"), "k-wm");
            var left = Kit.Box(page, "k-wm-left");
            var frame = Kit.Box(left, "k-wm-frame");
            Map = new WorldMapView();
            frame.Add(Map);
            var options = c.Options();
            int european = 0;
            foreach (var o in options) { var city = World.CityOf(o.City); if (city != null && InEurope(city)) european++; }
            if (european >= EuropeAt)
            {
                // a busy week in Europe: its pins on a zoomed inset, its box outlined on the world
                var box = Kit.Box(frame, "k-wm-inset");
                Europe = new WorldMapView(false, EuropeBox.x, EuropeBox.z, EuropeBox.w, EuropeBox.y);
                box.Add(Europe);
                Kit.L(box, "EUROPE", "k-wm-inset-name");
                Map.Outline = EuropeBox;
                _youE = Kit.Box(Europe, "k-wm-you"); _homeE = Kit.Box(Europe, "k-wm-home");
                Europe.RegisterCallback<GeometryChangedEvent>(_ => Place());
            }
            foreach (var o in options)
            {
                var city = World.CityOf(o.City);
                if (city == null) continue;
                var view = ViewOf(city);
                var pin = AddTo(view, new MapPinItem(o, city.Lon, city.Lat, o.Home ? (Color)new Color32(200, 255, 61, 255) : Career.TierColor((int)o.Tier), Pick));
                pin.View = view;
                Pins.Add(pin);
            }
            for (int w = 1; w <= 3; w++)
                foreach (var e in Tour.Week(s.Slot + w))
                {
                    var city = World.CityOf(e.City);
                    if (city != null) Map.Dots.Add((city.Lon, city.Lat, Career.TierColor((int)e.Tier)));
                }
            _you = Kit.Box(Map, "k-wm-you");                                     // a ring where you are, a square at home
            _home = Kit.Box(Map, "k-wm-home");
            Map.RegisterCallback<GeometryChangedEvent>(_ => Place());
            var legend = Kit.Box(left, "k-wm-legend");
            Kit.L(legend, Tour.WeekLabel(s.Week).ToUpperInvariant() + "  ·  " + (Tour.Week(s.Slot).Count == 0 ? (s.Week >= Tour.OffSeasonFrom ? "THE OFF-SEASON" : "A QUIET WEEK") : Tour.Week(s.Slot).Count + (Tour.Week(s.Slot).Count == 1 ? " EVENT" : " EVENTS")) +
                          "  ·  YOU'RE IN " + s.City.ToUpperInvariant() + "  ·  " + Career.Money(s.Cash), "k-wm-legend-t");
            Kit.L(legend, "◯ you   ▢ home   · faint: the next three weeks", "k-wm-legend-n");
            _detail = Kit.Panel(page, "EVENT");
            _detail.AddToClassList("k-wm-detail");

            // ---- PLANNER ----
            var plan = Kit.Box(Page("PLANNER"), "k-wm-plan");
            for (int w = 0; w < 6; w++)
            {
                int slot = s.Slot + w;
                if (Tour.WeekOf(slot) < Tour.WeekOf(s.Slot)) break;                  // this season only
                var col = Kit.Panel(plan, Tour.WeekLabel(slot).ToUpperInvariant() + (w == 0 ? "  ·  NOW" : ""));
                col.AddToClassList("k-wm-week");
                var week = Tour.Week(slot);
                if (week.Count == 0) Kit.L(col, (Tour.WeekOf(slot) >= Tour.OffSeasonFrom ? "Off-season" : "A quiet week") + ": only your club's open", "k-wm-row-n");
                foreach (var e in week)
                {
                    var row = Kit.Box(col, "k-wm-row");
                    row.Add(Career.Badge((int)e.Tier, 18));
                    var txt = Kit.Box(row, "k-wm-row-text");
                    Kit.L(txt, e.Name, "k-wm-row-name");
                    var info = Tour.Info(e.Tier);
                    Kit.L(txt, info.Short + "  ·  " + e.City + "  ·  trip ~" + Career.Money(c.TripCost(e.City, e.Tier)) + "  ·  " + Chance(c, e, w == 0), "k-wm-row-n");
                }
            }
        }

        /// <summary>Your chance at an event: this week's real entry, else from the tier's usual cut-off and your ranking now.</summary>
        static string Chance(CareerSession c, TourEvent e, bool now)
        {
            if (now)
            {
                for (int i = 0; i < Tour.Week(c.S.Slot).Count; i++)
                    if (Tour.EventAt(c.S.Slot, i) == e) return Route(c.OptionAt(i));
            }
            if (e.Tier == Tier.WorldTourFinals) return "the Road's top 8";
            int rank = c.RankOf(You), lastIn = Tour.TypicalLastIn(e.Tier);
            var city = World.CityOf(e.City);
            if (rank <= lastIn) return "likely in (usually ~#" + lastIn + ")";
            if (city != null && city.Country == c.S.Nat && c.WildcardsLeft > 0) return "home: a wildcard chance";
            if (Tour.QualifyingField(e.Tier) > 0 && rank <= lastIn + Tour.QualifyingField(e.Tier)) return "qualifying";
            return "usually needs ~#" + lastIn;
        }

        static string Route(CareerSession.Option o)
        {
            if (o.Home) return "your club - always open";
            switch (o.Via)
            {
                case CareerSession.Via.Direct: return o.LastIn == int.MaxValue ? "in: the field isn't full" : "in (last in #" + o.LastIn + ")";
                case CareerSession.Via.Wildcard: return "in on a wildcard";
                case CareerSession.Via.Qualifying: return "qualifying (last in #" + o.LastIn + ")";
                default: return o.Why ?? "closed";
            }
        }

        /// <summary>A city inside the Europe inset's box.</summary>
        static bool InEurope(City c) { return c.Lon >= EuropeBox.x && c.Lon <= EuropeBox.z && c.Lat >= EuropeBox.y && c.Lat <= EuropeBox.w; }

        /// <summary>The view a city shows on (the inset for Europe's, when there is one).</summary>
        WorldMapView ViewOf(City c) { return Europe != null && c != null && InEurope(c) ? Europe : Map; }

        void Place()
        {
            if (Map.contentRect.width < 8) return;
            var c = Career.C;
            var here = World.CityOf(c.S.City); var home = World.CityOf(World.HomeOf(c.S.Nat));
            if (Europe != null) PlaceInset(here);
            Marker(_you, _youE, here);
            Marker(_home, _homeE, here == home ? null : home);
            PlaceOn(Map, here);
            if (Europe != null && Europe.contentRect.width >= 8) PlaceOn(Europe, here);
        }

        /// <summary>The Europe inset goes in a corner of the world with none of the week's other pins (nor you) under it.</summary>
        void PlaceInset(City here)
        {
            var r = Map.contentRect;
            float w = 460, h = 300, m = 14;
            var under = new List<Vector2>();
            foreach (var pin in Pins) if (pin.View == Map) under.Add(Map.Project(pin.Lon, pin.Lat));
            if (here != null && !InEurope(here)) under.Add(Map.Project(here.Lon, here.Lat));
            Vector2[] corners = { new Vector2(m, r.height - h - m), new Vector2((r.width - w) / 2 + 80, r.height - h - m), new Vector2(r.width - w - m, r.height - h - m), new Vector2(m, m) };
            var pick = corners[0];
            foreach (var k in corners)
            {
                var rect = new Rect(k.x - 16, k.y - 16, w + 32, h + 32);
                if (!under.Exists(u => rect.Contains(u))) { pick = k; break; }
            }
            var box = Europe.parent;
            box.style.left = pick.x; box.style.top = pick.y; box.style.width = w; box.style.height = h;
        }

        void Marker(VisualElement onMap, VisualElement onInset, City city)
        {
            bool inset = city != null && onInset != null && Europe != null && InEurope(city);
            onMap.style.display = city != null && !inset ? DisplayStyle.Flex : DisplayStyle.None;
            if (onInset != null) onInset.style.display = inset ? DisplayStyle.Flex : DisplayStyle.None;
            if (city == null) return;
            var view = inset ? Europe : Map;
            var el = inset ? onInset : onMap;
            var a = view.Project(city.Lon, city.Lat);
            el.style.left = a.x; el.style.top = a.y;
        }

        /// <summary>A view's pins and their names: each name where it overlaps nothing placed before (right, left, then above / below).</summary>
        void PlaceOn(WorldMapView view, City here)
        {
            var r = view.contentRect;
            var mine = Pins.FindAll(p => p.View == view);
            var taken = new List<Rect>();
            foreach (var pin in mine) { var at = view.Project(pin.Lon, pin.Lat); taken.Add(new Rect(at.x - 12, at.y - 12, 24, 24)); }
            if (here != null && ViewOf(here) == view) { var a = view.Project(here.Lon, here.Lat); taken.Add(new Rect(a.x - 20, a.y - 20, 40, 40)); }
            foreach (var pin in mine)
            {
                var at = view.Project(pin.Lon, pin.Lat);
                pin.Root.style.left = at.x; pin.Root.style.top = at.y;
                float w = pin.Tag.text.Length * 9f + 18f, h = 22f;
                Vector2[] tries = { new Vector2(16, -h / 2), new Vector2(-16 - w, -h / 2), new Vector2(16, -h - 10), new Vector2(-16 - w, -h - 10),
                                    new Vector2(16, 10), new Vector2(-16 - w, 10), new Vector2(-w / 2, -h - 22), new Vector2(-w / 2, 22),
                                    new Vector2(16, -2 * h - 14), new Vector2(16, h + 14) };
                Vector2 best = tries[0];
                foreach (var t in tries)
                {
                    var rect = new Rect(at.x + t.x, at.y + t.y, w, h);
                    if (rect.xMin < 0 || rect.xMax > r.width || rect.yMin < 0 || rect.yMax > r.height) continue;
                    if (!taken.Exists(q => q.Overlaps(rect))) { best = t; break; }
                }
                taken.Add(new Rect(at.x + best.x, at.y + best.y, w, h));
                pin.Tag.style.left = best.x; pin.Tag.style.top = best.y;
            }
        }

        void Pick(MapPinItem pin)
        {
            var c = Career.C;
            var o = c.OptionAt(pin.Option.Index);
            if (o == null || !o.Open) { Sounds.Play("back"); return; }
            string trip = o.Cost == 0 ? "No flight needed." : "The trip costs " + Career.Money(o.Cost) + ": you'd have " + Career.Money(c.S.Cash - o.Cost) + " left.";
            if (o.Home && o.Cost == 0 && c.S.City != o.City) trip = "Your federation flies you home.";
            Fe.Push(new ConfirmScreen((o.Home ? "Play the " : "Go to the ") + o.Name + "? " + trip, () => Enter(o.Index)));
        }

        void Enter(int index)
        {
            Career.C.Enter(index);
            CareerStore.Save();
            Fe.Pop();                                                   // the map
            Fe.Replace(new CareerHubScreen());
        }

        protected override void SetPrompts()
        {
            Prompts.Set((PromptBar.Btn.Move, "Browse"), (PromptBar.Btn.A, "Travel & enter"), (PromptBar.Btn.B, "Back"), (PromptBar.Btn.Tabs, "Tabs"));
        }

        public override void OnShow()
        {
            base.OnShow();
            if (_placed) return;
            _placed = true;
            Root.schedule.Execute(() =>
            {
                // start on the biggest event open to you, else your club
                MapPinItem pick = null;
                foreach (var p in Pins) if (p.Option.Open && !p.Option.Home) { pick = p; break; }
                if (pick == null) pick = Pins.Find(p => p.Option.Home);
                if (pick != null) Focus(pick);
            });
        }

        public override void Tick(float dt)
        {
            if (Focused == _shown || !(Focused is MapPinItem pin)) return;
            _shown = Focused;
            var here = World.CityOf(Career.S.City);
            var from = here != null ? new Vector2(here.Lon, here.Lat) : (Vector2?)null;
            bool local = Europe != null && pin.View == Europe && here != null && InEurope(here);     // both in Europe: the inset draws it
            Map.Arc(local ? null : from, local ? (Vector2?)null : new Vector2(pin.Lon, pin.Lat));
            if (Europe != null) Europe.Arc(local ? from : null, local ? new Vector2(pin.Lon, pin.Lat) : (Vector2?)null);
            ShowDetail(pin);
        }

        void ShowDetail(MapPinItem pin)
        {
            var c = Career.C;
            var o = c.OptionAt(pin.Option.Index);
            var info = o.Info;
            var city = World.CityOf(o.City);
            var country = city != null ? World.CountryOf(city.Country) : null;
            _detail.Clear();
            Kit.L(_detail, (o.Home ? "CLUB OPEN" : info.Name.ToUpperInvariant()) + "  ·  " + Tour.WeekLabel(c.S.Week).ToUpperInvariant(), "k-panel-title");
            var head = Kit.Box(_detail, "k-wm-dt-head");
            head.Add(Career.Badge((int)o.Tier, 34));
            var ht = Kit.Box(head, "k-wm-dt-headt");
            Kit.L(ht, o.Name, "k-wm-dt-name");
            Kit.L(ht, o.City + (country != null ? ", " + country.Name + "  ·  " + World.RegionName(country.Region) : ""), "k-wm-dt-meta");

            Row("PRIZE", "Winner " + Career.Money(info.Prize[0]) + "  ·  +" + info.Points[0].ToString("N0") + " pts  ·  purse " + Career.Money(info.Purse));
            Row("DRAW", info.Field + " players" + (info.Draw > info.Field ? " in a " + info.Draw + "-draw (top " + (info.Draw - info.Field) + " seeds: byes)" : ""));
            if (!o.Home)
            {
                var field = c.FieldOf(o.Index);
                if (field.Count > 0)
                {
                    var top = new List<string>();
                    for (int i = 0; i < field.Count && i < 3; i++) top.Add(c.NameOf(field[i]) + " (" + Career.Rank(c.RankOf(field[i])) + ")");
                    Row("TOP SEEDS", string.Join(", ", top));
                }
            }
            string entry = Route(o);
            if (o.Via == CareerSession.Via.Direct && o.LastIn != int.MaxValue) entry = "In: you're " + Career.Rank(c.RankOf(You)) + ", the last in is #" + o.LastIn;
            if (o.Via == CareerSession.Via.Wildcard) entry = "A wildcard: a home event (" + c.WildcardsLeft + " of 2 left this year)";
            if (o.Via == CareerSession.Via.Qualifying) entry = "Qualifying: win " + (Tour.QualifyingDraw(o.Tier) == 16 ? 4 : 3) + " matches for a main-draw place";
            if (o.Via == CareerSession.Via.None) entry = "Closed: " + o.Why;
            var er = Row("ENTRY", entry);
            er.EnableInClassList("k-wm-dt-row--bad", !o.Open);

            int flight = World.Flight(c.S.City, o.City);
            int stay = o.Cost - flight;
            string trip;
            if (o.Home) trip = o.Cost == 0 ? (c.S.City == o.City ? "Home: no flight, no hotel" : "Your federation flies you home") : "Flight home " + Career.Money(flight);
            else
            {
                int nights = Tour.Nights(o.Tier) + (o.Via == CareerSession.Via.Qualifying ? Tour.QualifyingNights : 0);
                trip = (flight == 0 ? "No flight" : "Flight from " + c.S.City + " " + Career.Money(flight)) +
                       (stay > 0 ? "  +  hotel " + nights + " nights × " + Career.Money(city != null ? city.Hotel : 0) : "  ·  home: no hotel");
            }
            Row("TRIP", trip);
            var cash = Row("CASH", Career.Money(c.S.Cash) + "  →  " + Career.Money(c.S.Cash - o.Cost) + " after the trip");
            cash.EnableInClassList("k-wm-dt-row--bad", c.S.Cash - o.Cost < 0);
            Kit.L(_detail, o.Open ? "A: travel and enter" : "", "k-wm-dt-go");
        }

        VisualElement Row(string k, string v)
        {
            var row = Kit.Box(_detail, "k-wm-dt-row");
            Kit.L(row, k, "k-wm-dt-k");
            Kit.L(row, v, "k-wm-dt-v");
            return row;
        }
    }
}
