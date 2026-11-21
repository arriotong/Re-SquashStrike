// ===========================================================
// Assets/SquashGame/Runtime/UI/OnlineScreens.cs
// P2-M8 online: the screens around an online match (the main menu's
// ONLINE tab leads here; the match itself is MatchRunner's).
//
//   OnlineSearchScreen   connecting, then the quick or ranked queue: a
//                        timer, your rating and the band; Cancel
//   PrivateRoomScreen    host (length, scoring, court; the code to give
//                        a friend) or join (JoinRoomScreen: type their code)
//   LeaderboardScreen    the server's top players, and you
// 2026-11-21 (the user: "all the online UI needs a redo - like the career
// UI, big and small cards"): each screen a big card and small ones, on the
// Online world's linked islands (Dioramas).
//   OnlineText           the lines the cards and the HUD share
//
// When the server starts a match, the front end begins it from any of
// these (FrontEnd.StartOnline): nothing here waits for it.
// ===========================================================

using SquashGame.Online;
using SquashGame.Profile;
using SquashSim.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public static class OnlineText
    {
        public static string Rating()
        {
            var me = OnlineService.Me;
            return me.Games == 0 ? "UNRATED  ·  1000" : "RATING " + me.Rating + (me.Rank > 0 ? "  ·  #" + me.Rank : "");
        }

        public static string Status()
        {
            switch (OnlineService.State)
            {
                case NetState.Online: return "ONLINE" + (OnlineService.Ping >= 0 ? "  ·  " + Ms(OnlineService.Ping) : "");
                case NetState.Connecting: return "CONNECTING";
                case NetState.Failed: return "OFFLINE";
                default: return "READY";                          // 2026-10-25 playtest: "NOT CONNECTED" read as "the servers are down"
            }
        }

        public static string Ms(double seconds) { return Mathf.RoundToInt((float)(seconds * 1000)) + " ms"; }
        public static string Clock(float s) { int t = Mathf.FloorToInt(s); return (t / 60) + ":" + (t % 60).ToString("00"); }

        /// <summary>How the match ended, in words, for the full-time screen.</summary>
        public static string Ending(MatchRunner m)
        {
            var s = m.OnlineMatch;
            if (s == null) return "";
            switch (s.EndReason)
            {
                case "desync": return "The two games stopped agreeing - a connection problem. The match does not count.";
                case "left": return "Your opponent left the match.";
                case "forfeit": return s.Winner == s.Me ? "Your opponent dropped out - you win by forfeit." : "You lost the connection - a forfeit.";
                case "abandoned": return "The match was abandoned.";
                default: return "";
            }
        }
    }

    // ---------------- your name (asked once) ----------------

    /// <summary>The first visit to ONLINE: the name other players will see, then on to where you were going.</summary>
    public sealed class OnlineNameScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Online; } }
        public override bool Spatial { get { return true; } }
        readonly System.Func<MenuScreen> _then;
        TextItem _name;
        Label _note, _big;
        public OnlineNameScreen(System.Func<MenuScreen> then) { _then = then; }
        protected override string Kicker { get { return "ONLINE"; } }
        protected override string Title { get { return "Your name online"; } }
        protected override bool ShowChip { get { return false; } }

        /// <summary>Open <paramref name="screen"/>, asking for a name first if it hasn't been picked.</summary>
        public static void Go(FrontEnd fe, System.Func<MenuScreen> screen)
        {
            if (OnlineService.Me.NameChosen) fe.Push(screen());
            else fe.Push(new OnlineNameScreen(screen));
        }

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "on-stage");
            var play = AddTo(stage, new CardItem("PLAY AS THIS NAME", "WHO ARE YOU?", Done, CardSize.Hero)
                .Drawn("split", new Color32(16, 26, 46, 255), new Color32(60, 170, 255, 255))
                .Line("Other players see it in rooms, matches and the leaderboard."));
            play.Root.AddToClassList("on-hero");
            _big = Kit.L(play.Art, "", "on-hero-big");
            var col = Kit.Box(stage, "on-col");
            var box = Kit.Box(col, "on-namebox");
            Kit.L(box, "YOUR NAME", "k-card-kicker");
            _name = AddTo(box, new TextItem("Name", OnlineService.Me.Name, Hub_MaxName));
            _name.Root.AddToClassList("on-namefield");
            _note = Kit.L(box, "Press Enter on it to type. You can change it any time in your profile.", "on-small");
        }

        public override void Tick(float dt) { if (_big != null) _big.text = _name.Value.Trim().Length > 0 ? _name.Value.Trim() : "?"; }

        const int Hub_MaxName = 16;

        void Done()
        {
            _name.StopTyping();
            var n = _name.Value.Trim();
            if (n.Length == 0) { _note.text = "Type a name first."; return; }
            var me = OnlineService.Me;
            me.Name = n; me.NameChosen = true;
            ProfileStore.Save();
            OnlineService.Disconnect();                           // the next hello carries the new name
            Fe.Replace(_then());
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the queue ----------------

    public sealed class OnlineSearchScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Online; } }
        public override bool Spatial { get { return true; } }
        readonly QueueKind _kind;
        Label _time;
        string _state = "", _line = "";
        CardItem _cancel;
        bool _queued;
        public OnlineSearchScreen(QueueKind kind) { _kind = kind; }
        protected override string Kicker { get { return "ONLINE"; } }
        protected override string Title { get { return _kind == QueueKind.Ranked ? "Ranked" : "Quick play"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "on-stage");
            bool ranked = _kind == QueueKind.Ranked;
            var card = AddTo(stage, new CardItem(ranked ? "FINDING A MATCH" : "FINDING A PLAYER", ranked ? "RANKED  ·  BEST OF 3" : "QUICK PLAY  ·  BEST OF 3", null, CardSize.Hero)
                .Drawn("rings", new Color32(14, 22, 40, 255), new Color32(60, 170, 255, 255))
                .Line(() => _state + (string.IsNullOrEmpty(_line) ? "" : "\n" + _line))
                .Badge(() => ranked ? OnlineText.Rating() : OnlineText.Status(), true));
            card.Root.AddToClassList("on-hero");
            _time = Kit.L(card.Art, "", "on-hero-big");
            var col = Kit.Box(stage, "on-col");
            _cancel = AddTo(col, new CardItem("CANCEL", "STOP LOOKING", Back)
                .Drawn("split", new Color32(40, 14, 16, 255), new Color32(255, 90, 74, 255)));
            _cancel.Root.AddToClassList("on-mini");
            var info = Kit.Box(col, "on-info");
            Kit.L(info, ranked ? "Someone near your rating. Waiting longer widens the search. Wins and losses move your rating."
                               : "The next player looking. Your rating doesn't change.", "on-small");
            OnlineService.Connect();
        }

        public override void OnShow() { base.OnShow(); if (_cancel != null) Focus(_cancel); }

        public override void Tick(float dt)
        {
            var st = OnlineService.State;
            if (st == NetState.Online && !_queued) { OnlineService.Queue(_kind); _queued = true; }
            if (st == NetState.Failed || (OnlineService.Error != null && _queued && !OnlineService.Queued))
            {
                _state = "Can't play online right now";
                _time.text = "--:--";
                _line = (OnlineService.Error ?? "") + "  ·  Settings > Gameplay > Online server";
                return;
            }
            _state = st == NetState.Online ? "Looking for an opponent" : "Connecting to the server";
            _time.text = st == NetState.Online ? OnlineText.Clock(OnlineService.QueuedSeconds) : "...";
            _line = _kind == QueueKind.Ranked ? "Searching within " + (100 + 50 * Mathf.FloorToInt(OnlineService.QueuedSeconds / 10f)) + " of your rating" : "";
        }

        public override void Back() { OnlineService.Cancel(); Fe.Pop(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Cancel"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- a private room ----------------

    public sealed class PrivateRoomScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Online; } }
        public override bool Spatial { get { return true; } }
        static readonly string[] Lengths = { "1 game", "Best of 3", "Best of 5" };
        static readonly int[] Games = { 1, 2, 3 };
        int _length = 1, _scoring, _court;
        Label _codeBig;
        string _host = "Pick the match, make the room, give the code to a friend.";
        bool _wantRoom;
        protected override string Kicker { get { return "ONLINE"; } }
        protected override string Title { get { return "Private room"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            _court = Mathf.Max(0, Courts.IndexOf("show"));
            var stage = Kit.Box(Content, "on-stage");
            var make = AddTo(stage, new CardItem("MAKE A ROOM", "HOST", MakeRoom, CardSize.Hero)
                .Drawn("bracket", new Color32(14, 26, 22, 255), new Color32(200, 255, 61, 255))
                .Line(() => Lengths[_length] + "  ·  " + MatchChoices.ScoringShort[_scoring] + "  ·  " + Courts.Pickable[Mathf.Clamp(_court, 0, Courts.Pickable.Length - 1)].Name + "\n" + _host)
                .Badge(() => OnlineService.RoomCode != null ? "ROOM OPEN" : "", true));
            make.Root.AddToClassList("on-hero");
            _codeBig = Kit.L(make.Art, "", "on-hero-big");
            var col = Kit.Box(stage, "on-sides");
            Mini(col, new CardItem("MATCH", "HOW LONG", () => _length = (_length + 1) % Lengths.Length).Drawn("stripes", new Color32(16, 22, 34, 255), new Color32(111, 184, 255, 255))
                .Badge(() => Lengths[_length].ToUpperInvariant(), true));
            Mini(col, new CardItem("SCORING", "POINTS", () => _scoring = (_scoring + 1) % MatchChoices.ScoringShort.Length).Drawn("corner", new Color32(26, 22, 14, 255), new Color32(255, 184, 59, 255))
                .Badge(() => MatchChoices.ScoringShort[_scoring].ToUpperInvariant(), true));
            Mini(col, new CardItem("COURT", "WHERE", () => Fe.Push(new PickListScreen("Court", Courts.Names, _court, v => { _court = v; Courts.CurrentId = Courts.Pickable[v].Id; }, Courts.SectionOf)))
                .Drawn("wall", new Color32(30, 18, 52, 255), new Color32(190, 110, 255, 255))
                .Line(() => Courts.Pickable[Mathf.Clamp(_court, 0, Courts.Pickable.Length - 1)].Name));
            Mini(col, new CardItem("JOIN A FRIEND", "THEIR CODE", () => Fe.Push(new JoinRoomScreen())).Drawn("split", new Color32(16, 26, 46, 255), new Color32(60, 170, 255, 255))
                .Line("Type the 5 letters they see"));
            OnlineService.Connect();
        }

        void Mini(VisualElement row, CardItem card) { AddTo(row, card); card.Root.AddToClassList("on-mini"); }

        void MakeRoom()
        {
            _wantRoom = true;
            if (OnlineService.State == NetState.Online) Send();
            else OnlineService.Connect();
        }

        void Send()
        {
            _wantRoom = false;
            OnlineService.CreateRoom(Games[_length], _scoring == 1 ? "english" : "par", Courts.Pickable[Mathf.Clamp(_court, 0, Courts.Pickable.Length - 1)].Id);
        }

        public override void Tick(float dt)
        {
            if (_wantRoom && OnlineService.State == NetState.Online) Send();
            string code = OnlineService.RoomCode;
            _codeBig.text = code ?? "";
            if (OnlineService.State == NetState.Failed) _host = OnlineService.Error ?? "Can't reach the server.";
            else if (OnlineService.OpponentLeftRoom) _host = "Your friend left the room.";
            else if (code != null) _host = "Give your friend this code  ·  waiting  ·  " + OnlineText.Status();
        }

        public override void Back() { if (OnlineService.RoomCode != null) OnlineService.Cancel(); Fe.Pop(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    /// <summary>2026-11-21 joining a friend's room: their code, big.</summary>
    public sealed class JoinRoomScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Online; } }
        public override bool Spatial { get { return true; } }
        TextItem _code;
        string _note = "Your friend sees a 5-letter code when they make a room.";
        Label _big;
        protected override string Kicker { get { return "ONLINE  ·  PRIVATE ROOM"; } }
        protected override string Title { get { return "Join a friend"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "on-stage");
            var join = AddTo(stage, new CardItem("JOIN", "THEIR ROOM", Join, CardSize.Hero)
                .Drawn("split", new Color32(16, 26, 46, 255), new Color32(60, 170, 255, 255))
                .Line(() => _note));
            join.Root.AddToClassList("on-hero");
            _big = Kit.L(join.Art, "", "on-hero-big");
            var col = Kit.Box(stage, "on-col");
            var box = Kit.Box(col, "on-namebox");
            Kit.L(box, "THE CODE", "k-card-kicker");
            _code = AddTo(box, new TextItem("Code", "", 5));
            _code.Root.AddToClassList("on-namefield");
            Kit.L(box, "Press Enter on it to type, then pick JOIN.", "on-small");
            OnlineService.Connect();
        }

        public override void Tick(float dt)
        {
            var v = _code.Value.Trim().ToUpperInvariant();
            _big.text = v.Length > 0 ? v : "_ _ _ _ _";
            if (OnlineService.Error != null && OnlineService.State == NetState.Online && OnlineService.RoomCode == null) _note = OnlineService.Error;
        }

        void Join()
        {
            _code.StopTyping();
            var code = _code.Value.Trim().ToUpperInvariant();
            if (code.Length != 5) { _note = "A room code is 5 letters."; return; }
            if (OnlineService.State != NetState.Online) { OnlineService.Connect(); _note = "Connecting - try again in a moment."; return; }
            OnlineService.JoinRoom(code);
            _note = "Joining " + code + "...";
        }

        public override void Back() { if (OnlineService.RoomCode != null) OnlineService.Cancel(); Fe.Pop(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the leaderboard ----------------

    public sealed class LeaderboardScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Online; } }
        public override bool Spatial { get { return true; } }
        public const int Shown = 10;
        VisualElement _list;
        Label _note, _rank;
        string _you = "";
        Board _drawn;
        protected override string Kicker { get { return "ONLINE"; } }
        protected override string Title { get { return "Leaderboard"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "on-stage");
            var you = AddTo(stage, new CardItem(OnlineService.Me.Name.ToUpperInvariant(), "YOU", () => OnlineService.FetchBoard(), CardSize.Hero)
                .Drawn("rings", new Color32(16, 26, 46, 255), new Color32(60, 170, 255, 255))
                .Line(() => _you + "\nWin: your score goes up. Lose: it goes down. Everyone starts at 1000.")
                .Badge(() => "PRESS ENTER TO REFRESH", false));
            you.Root.AddToClassList("on-hero");
            _rank = Kit.L(you.Art, "", "on-hero-big");
            var board = Kit.Box(stage, "on-board2");
            Kit.L(board, "THE TOP 10", "k-card-kicker");
            _list = Kit.Box(board, "on-rows2");
            _note = Kit.L(board, "", "on-small");
            OnlineService.FetchBoard();
        }

        public override void Tick(float dt)
        {
            OnlineService.PollBoard();
            var b = OnlineService.LastBoard;
            _note.text = OnlineService.BoardLoading ? "Loading..." : OnlineService.BoardError ?? (b != null && b.players.Length == 0 ? "Nobody has played a ranked match yet." : "");
            bool ranked = b != null && b.you != null && b.you.rank > 0;
            _rank.text = ranked ? "#" + b.you.rank : "-";
            _you = ranked ? b.you.rating + " points  ·  " + b.you.wins + " W  " + (b.you.games - b.you.wins) + " L" : OnlineText.Rating();
            if (b == _drawn) return;
            _drawn = b;
            _list.Clear();
            if (b == null) return;
            for (int i = 0; i < b.players.Length && i < Shown; i++)
            {
                var r = b.players[i];
                var row = Kit.Box(_list, "on-row2");
                if (r.you) row.AddToClassList("on-row2--you");
                if (r.rank <= 3) row.AddToClassList("on-row2--top" + r.rank);
                Kit.L(row, r.rank.ToString(), "on-row2-rank");
                Kit.L(row, r.name, "on-row2-name");
                Kit.L(row, r.wins + "-" + (r.games - r.wins), "on-row2-wl");
                Kit.L(row, r.rating.ToString(), "on-row2-rating");
            }
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Refresh"), (PromptBar.Btn.B, "Back")); }
    }
}
