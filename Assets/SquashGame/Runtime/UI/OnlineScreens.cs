// ===========================================================
// Assets/SquashGame/Runtime/UI/OnlineScreens.cs
// P2-M8 online: the screens around an online match (the main menu's
// ONLINE tab leads here; the match itself is MatchRunner's).
//
//   OnlineSearchScreen   connecting, then the quick or ranked queue: a
//                        timer, your rating and the band; Cancel
//   PrivateRoomScreen    host (length, scoring, court; the code to give
//                        a friend) or join (type their code)
//   LeaderboardScreen    the server's top players, and you
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
        readonly System.Func<MenuScreen> _then;
        TextItem _name;
        Label _note;
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
            var p = Kit.Panel(Content, "WHO ARE YOU?"); p.AddToClassList("on-panel");
            _name = AddTo(p, new TextItem("Name", OnlineService.Me.Name, Hub_MaxName));
            _note = Kit.L(p, "Other players see this in rooms, matches and the leaderboard. Change it any time in Settings > Gameplay.", "k-cr-note");
            AddTo(Root, new PrimaryItem("Play as this name", Done));
        }

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

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the queue ----------------

    public sealed class OnlineSearchScreen : GameScreen
    {
        readonly QueueKind _kind;
        Label _state, _time, _line;
        bool _queued;
        public OnlineSearchScreen(QueueKind kind) { _kind = kind; }
        protected override string Kicker { get { return "ONLINE"; } }
        protected override string Title { get { return _kind == QueueKind.Ranked ? "Ranked" : "Quick match"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var p = Kit.Panel(Content, _kind == QueueKind.Ranked ? "THE LADDER" : "ANYONE, NOW"); p.AddToClassList("on-panel");
            _state = Kit.L(p, "", "on-state");
            _time = Kit.L(p, "", "on-time");
            _line = Kit.L(p, "", "k-cr-note");
            Kit.L(p, _kind == QueueKind.Ranked
                ? "Best of 3, PAR-11, on the show court. Paired with someone near your rating; the longer you wait, the wider the search. Wins and losses move your rating."
                : "Best of 3, PAR-11, on the show court, against the next player looking. It does not change your rating.", "k-cr-note");
            AddTo(Root, new PrimaryItem("Cancel", Back));
            OnlineService.Connect();
        }

        public override void Tick(float dt)
        {
            var st = OnlineService.State;
            if (st == NetState.Online && !_queued) { OnlineService.Queue(_kind); _queued = true; }
            if (st == NetState.Failed || (OnlineService.Error != null && _queued && !OnlineService.Queued))
            {
                _state.text = "Can't play online right now";
                _time.text = "";
                _line.text = (OnlineService.Error ?? "") + "  ·  check Settings > Gameplay > Online server";
                return;
            }
            _state.text = st == NetState.Online ? "Looking for an opponent" : "Connecting to the server";
            _time.text = st == NetState.Online ? OnlineText.Clock(OnlineService.QueuedSeconds) : "";
            _line.text = _kind == QueueKind.Ranked
                ? OnlineText.Rating() + "  ·  within " + (100 + 50 * Mathf.FloorToInt(OnlineService.QueuedSeconds / 10f))
                : OnlineText.Status();
        }

        public override void Back() { OnlineService.Cancel(); Fe.Pop(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Cancel"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- a private room ----------------

    public sealed class PrivateRoomScreen : GameScreen
    {
        static readonly string[] Lengths = { "1 game", "Best of 3", "Best of 5" };
        static readonly int[] Games = { 1, 2, 3 };
        int _length = 1, _scoring, _court;
        TextItem _code;
        Label _host, _join, _codeBig;
        bool _wantRoom;
        protected override string Kicker { get { return "ONLINE"; } }
        protected override string Title { get { return "Private room"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            _court = Courts.IndexOf("show");
            var grid = Kit.Box(Content, "on-grid");
            var host = Kit.Panel(grid, "HOST A ROOM"); host.AddToClassList("on-panel");
            AddTo(host, new SelectorItem("Match", Lengths, () => _length, v => _length = v));
            AddTo(host, new SelectorItem("Scoring", new[] { "PAR-11", "English" }, () => _scoring, v => _scoring = v));
            AddTo(host, new SelectorItem("Court", Courts.Names, () => _court, v => { _court = v; Courts.CurrentId = Courts.All[v].Id; }));
            AddTo(host, new ActionItem("Make the room", MakeRoom, "▶"));
            _codeBig = Kit.L(host, "", "on-code");
            _host = Kit.L(host, "You pick the match; give the code to a friend.", "k-cr-note");

            var join = Kit.Panel(grid, "JOIN A FRIEND"); join.AddToClassList("on-panel");
            _code = AddTo(join, new TextItem("Code", "", 5));
            AddTo(join, new ActionItem("Join", Join, "▶"));
            _join = Kit.L(join, "Type the 5 letters your friend sees.", "k-cr-note");
            OnlineService.Connect();
        }

        void MakeRoom()
        {
            _wantRoom = true;
            if (OnlineService.State == NetState.Online) Send();
            else OnlineService.Connect();
        }

        void Send()
        {
            _wantRoom = false;
            OnlineService.CreateRoom(Games[_length], _scoring == 1 ? "english" : "par", Courts.All[Mathf.Clamp(_court, 0, Courts.All.Length - 1)].Id);
        }

        void Join()
        {
            _code.StopTyping();
            var code = _code.Value.Trim().ToUpperInvariant();
            if (code.Length != 5) { _join.text = "A room code is 5 letters."; return; }
            if (OnlineService.State != NetState.Online) { OnlineService.Connect(); _join.text = "Connecting - try again in a moment."; return; }
            OnlineService.JoinRoom(code);
            _join.text = "Joining " + code + "...";
        }

        public override void Tick(float dt)
        {
            if (_wantRoom && OnlineService.State == NetState.Online) Send();
            string code = OnlineService.RoomCode;
            _codeBig.text = code ?? "";
            if (OnlineService.State == NetState.Failed) _host.text = OnlineService.Error ?? "Can't reach the server.";
            else if (OnlineService.OpponentLeftRoom) _host.text = "Your friend left the room.";
            else if (code != null) _host.text = "Waiting for your friend to join  ·  " + OnlineText.Status();
            if (OnlineService.Error != null && OnlineService.State == NetState.Online && code == null) _join.text = OnlineService.Error;
        }

        public override void Back() { if (OnlineService.RoomCode != null) OnlineService.Cancel(); Fe.Pop(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the leaderboard ----------------

    public sealed class LeaderboardScreen : GameScreen
    {
        public const int Shown = 15;
        VisualElement _list;
        Label _note, _you;
        Board _drawn;
        protected override string Kicker { get { return "ONLINE"; } }
        protected override string Title { get { return "Leaderboard"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var grid = Kit.Box(Content, "on-grid");
            var p = Kit.Panel(grid, "THE LADDER"); p.AddToClassList("on-board");
            _list = Kit.Box(p, "on-rows");
            _note = Kit.L(p, "", "k-cr-note");
            var me = Kit.Panel(grid, "YOU"); me.AddToClassList("on-panel");
            _you = Kit.L(me, "", "on-state");
            Kit.L(me, "Ranked wins and losses move your rating (Elo, from 1000). One ticket for Gear Drops with every ranked win.", "k-cr-note");
            AddTo(Root, new PrimaryItem("Refresh", () => OnlineService.FetchBoard()));
            OnlineService.FetchBoard();
        }

        public override void Tick(float dt)
        {
            OnlineService.PollBoard();
            var b = OnlineService.LastBoard;
            _note.text = OnlineService.BoardLoading ? "Loading..." : OnlineService.BoardError ?? (b != null && b.players.Length == 0 ? "Nobody has played a ranked match yet." : "");
            var me = OnlineService.Me;
            _you.text = me.Name + "\n" + (b != null && b.you != null && b.you.rank > 0 ? "#" + b.you.rank + "  ·  " + b.you.rating + "  ·  " + b.you.wins + " W  " + (b.you.games - b.you.wins) + " L" : OnlineText.Rating());
            if (b == _drawn) return;
            _drawn = b;
            _list.Clear();
            if (b == null) return;
            for (int i = 0; i < b.players.Length && i < Shown; i++)
            {
                var r = b.players[i];
                var row = Kit.Box(_list, "on-row");
                if (r.you) row.AddToClassList("on-row--you");
                Kit.L(row, r.rank.ToString(), "on-row-rank");
                Kit.L(row, r.name, "on-row-name");
                Kit.L(row, r.wins + "-" + (r.games - r.wins), "on-row-wl");
                Kit.L(row, r.rating.ToString(), "on-row-rating");
            }
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Refresh"), (PromptBar.Btn.B, "Back")); }
    }
}
