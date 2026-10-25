// ===========================================================
// Assets/SquashGame/Runtime/UI/ProfileScreens.cs
// Your profile: the player tag top right on the main menu opens it
// (a click, or the menu's arrows onto the tag and Enter).
//
//   names     your player name - on the tag, the versus screens and
//             online (the leaderboard, rooms, your opponents see it) -
//             and, with a career, your career player's name
//   you       your athlete in your kit; level, title, XP
//   record    matches, rallies, achievements, the arcade's stars, the
//             Gauntlet, the scenarios, your online rating
//   more      your kit (the Locker Room) and the full stats
//
//   ChipItem  the tag as a menu item (focusable, clickable)
// ===========================================================

using System;
using SquashGame.Online;
using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>The player tag as something you can move onto and press (the main menu: it opens your profile).</summary>
    public sealed class ChipItem : MenuItem
    {
        readonly Action _run;
        public readonly PlayerChip Chip;
        public override bool WantsSlant { get { return false; } }
        public ChipItem(PlayerChip chip, Action run) : base("Profile")
        {
            _run = run; Chip = chip;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-chip-item");
            LabelEl.style.display = DisplayStyle.None;
            Root.Add(chip);
        }
        public override void Submit() { if (IsEnabled && _run != null) _run(); }
    }

    public static class PlayerName
    {
        public const int MaxLength = 16;

        /// <summary>The name you go by: the one you picked, else your career player's, else "Player".</summary>
        public static string Shown()
        {
            var on = ProfileStore.Current.Online;
            if (on != null && on.NameChosen && !string.IsNullOrWhiteSpace(on.Name)) return on.Name;
            return CareerStore.Current != null ? CareerStore.Current.S.Name : "Player";
        }

        /// <summary>Your player name (online too: the next time the game connects, it goes by this).</summary>
        public static void Set(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return;
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength);
            var on = ProfileStore.Current.Online;
            if (on.Name == name && on.NameChosen) return;
            on.Name = name; on.NameChosen = true;
            ProfileStore.Save();
            if (OnlineService.Session == null || OnlineService.Session.Finished) OnlineService.Disconnect();
        }
    }

    public sealed class ProfileScreen : GameScreen
    {
        protected override string Kicker { get { return "YOUR PROFILE"; } }
        protected override string Title { get { return PlayerName.Shown(); } }
        protected override bool ShowChip { get { return false; } }
        TextItem _name, _career;
        Label _saved;
        /// <summary>The player name field (tests).</summary>
        public TextItem NameField { get { return _name; } }

        protected override void Compose()
        {
            var p = ProfileStore.Current.Progress;
            var grid = Kit.Box(Content, "pf-grid");

            // ---- names ----
            var names = Kit.Panel(grid, "NAMES"); names.AddToClassList("pf-panel");
            _name = AddTo(names, new TextItem("Player name", PlayerName.Shown(), PlayerName.MaxLength));
            Kit.L(names, "On your tag, the versus screens and online: other players see it in rooms, matches and the leaderboard.", "k-cr-note");
            var c = CareerStore.Current;
            if (c != null)
            {
                _career = AddTo(names, new TextItem("Career name", c.S.Name, PlayerName.MaxLength));
                Kit.L(names, "Your career player, in the draws, the rankings and the news.", "k-cr-note");
            }
            _saved = Kit.L(names, "", "pf-saved");

            // ---- you ----
            var you = Kit.Panel(grid, "YOU"); you.AddToClassList("pf-you");
            Vignettes.Attach(Kit.Box(you, "pf-pic"), "you", 400, 480);
            var t = Cosmetics.Find(p.Loadout.Title);
            Kit.L(you, "LEVEL " + Rewards.LevelOf(p.Xp) + "  ·  " + (t != null ? t.Name.ToUpperInvariant() : "ROOKIE"), "pf-level");
            Kit.L(you, p.Xp.ToString("N0") + " XP", "k-cr-note");

            // ---- the record ----
            var rec = Kit.Panel(grid, "RECORD"); rec.AddToClassList("pf-panel");
            var s = p.Stats;
            int played = s.Quick.Played + s.Versus.Played + s.Career.Played;
            Row(rec, "Matches won", s.MatchesWon + (played > 0 ? " of " + played : ""));
            Row(rec, "Rallies won", s.RalliesWon.ToString("N0") + (s.Rallies > 0 ? "  (" + Mathf.RoundToInt(100f * s.RalliesWon / s.Rallies) + "%)" : ""));
            int ach = 0; foreach (var a in p.Achievements) if (a.Done) ach++;
            Row(rec, "Achievements", ach + " / " + Achievements.All().Count);
            int stars = 0; foreach (var r in ProfileStore.Current.Arcade) stars += r.Stars;
            Row(rec, "Target Arcade", "★ " + stars + " / 54");
            Row(rec, "Scenarios", "★ " + ScenarioFlow.TotalStars() + " / " + SquashSim.Modes.Scenarios.MaxStars);
            Row(rec, "Gauntlet", "best floor " + GauntletFlow.State.BestFloor);
            var on = ProfileStore.Current.Online;
            Row(rec, "Online", on.Games == 0 ? "unrated" : on.Rating + (on.Rank > 0 ? "  ·  #" + on.Rank : "") + "  ·  " + on.Games + " ranked");
            if (c != null) Row(rec, "Career", Career.Rank(c.RankOf(SquashSim.Career.CareerSession.You)) + "  ·  " + c.S.Titles + (c.S.Titles == 1 ? " title" : " titles"));

            var more = Kit.Box(names, "pf-more");
            AddTo(more, new ActionItem("Your kit", () => { Save(); Fe.Push(new LockerRoomScreen()); }, "▶"));
            AddTo(more, new ActionItem("All stats", () => { Save(); Fe.Push(new StatsScreen()); }, "▶"));
            AddTo(Root, new PrimaryItem("Save", () => { Save(); Fe.Pop(); }));
        }

        static void Row(VisualElement panel, string what, string value)
        {
            var r = Kit.Box(panel, "pf-row");
            Kit.L(r, what, "pf-row-k");
            Kit.L(r, value, "pf-row-v");
        }

        /// <summary>Keep the names as typed (empty: unchanged).</summary>
        public void Save()
        {
            _name.StopTyping();
            PlayerName.Set(_name.Value);
            var c = CareerStore.Current;
            if (_career != null && c != null)
            {
                _career.StopTyping();
                var n = _career.Value.Trim();
                if (n.Length > 0 && n != c.S.Name) { c.S.Name = n.Length > PlayerName.MaxLength ? n.Substring(0, PlayerName.MaxLength) : n; CareerStore.Save(); }
            }
            _saved.text = "Saved.";
        }

        public override void Back() { Save(); Fe.Pop(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Save and back")); }
    }
}
