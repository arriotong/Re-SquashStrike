// ===========================================================
// Assets/SquashGame/Runtime/UI/ChallengeScreens.cs
// P3-R2: the daily and weekly challenges - today's three with their
// bars and rewards, the week's one, the streak and what it unlocks.
// (The main menu's CHALLENGES tab shows them at a glance: HubScreens.)
// ===========================================================

using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class ChallengeText
    {
        /// <summary>"✓ Win 2 matches" / "Win 2 matches  1/2", one per line (the hub card).</summary>
        public static string Today()
        {
            var s = Challenges.Current();
            var lines = new System.Collections.Generic.List<string>();
            foreach (var c in s.Daily)
            {
                var d = Challenges.Find(c.Id);
                if (d == null) continue;
                lines.Add(c.Done ? "✓  " + d.Describe() : d.Single ? d.Describe() : d.Describe() + "   " + c.Progress + "/" + d.Target);
            }
            return string.Join("\n", lines);
        }

        public static int DoneToday() { int n = 0; foreach (var c in Challenges.Current().Daily) if (c.Done) n++; return n; }

        public static string Week()
        {
            var w = Challenges.Current().Weekly;
            var d = w != null ? Challenges.Find(w.Id) : null;
            if (d == null) return "";
            return (w.Done ? "✓  " : "") + d.Describe() + (w.Done || d.Single ? "" : "   " + w.Progress + "/" + d.Target) + "\n" + Challenges.WeekLeft();
        }

        public static string Streak()
        {
            int s = Challenges.StreakNow();
            return s == 0 ? "NO STREAK" : s + (s == 1 ? " DAY" : " DAYS");
        }
    }

    public sealed class ChallengesScreen : GameScreen
    {
        protected override string Kicker { get { return "CHALLENGES"; } }
        protected override string Title { get { return "Today and this week"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var s = Challenges.Current();
            int streak = Challenges.StreakNow();
            var grid = Kit.Box(Content, "ch-grid");

            // ---- today ----
            var today = Kit.Panel(grid, "TODAY  ·  " + Challenges.LeftToday() + " LEFT"); today.AddToClassList("ch-today");
            int xp = (int)System.Math.Round(Challenges.DailyXp * (1 + Challenges.StreakBonus(System.Math.Max(1, streak + (ChallengeText.DoneToday() > 0 ? 0 : 1)))));
            foreach (var c in s.Daily)
            {
                var d = Challenges.Find(c.Id);
                if (d != null) Row(today, d, c, "+" + xp + " XP");
            }
            Kit.L(today, "New challenges every day at midnight. The first one you finish each day keeps your streak going.", "k-cr-note");

            // ---- the week ----
            var week = Kit.Panel(grid, "THIS WEEK  ·  " + Challenges.WeekLeft().ToUpperInvariant()); week.AddToClassList("ch-week");
            var wd = s.Weekly != null ? Challenges.Find(s.Weekly.Id) : null;
            var prize = Cosmetics.Find(Challenges.WeeklyItems[s.WeekliesDone % Challenges.WeeklyItems.Length]);
            if (wd != null) Row(week, wd, s.Weekly, "+" + Challenges.WeeklyXp + " XP");
            if (prize != null) Item(week, prize, "AND THE NEXT WEEKLY PRIZE");

            // ---- the streak ----
            var st = Kit.Panel(grid, "STREAK"); st.AddToClassList("ch-streak");
            var big = Kit.Box(st, "ch-streak-row");
            Kit.L(big, streak.ToString(), "ch-streak-n");
            Kit.L(big, streak == 1 ? "DAY" : "DAYS", "ch-streak-k");
            Kit.L(st, "Best " + s.BestStreak + "  ·  daily XP +" + (int)(100 * Challenges.StreakBonus(System.Math.Max(1, streak))) + "%", "k-cr-note");
            for (int i = 0; i < Challenges.StreakRewards.Length; i++)
            {
                if (s.BestStreak >= Challenges.StreakRewards[i]) continue;
                var it = Cosmetics.Find(Challenges.StreakItems[i]);
                if (it != null) Item(st, it, "AT " + Challenges.StreakRewards[i] + " DAYS");
                break;
            }

            AddTo(Root, new PrimaryItem("Play", () => Fe.Pop()));
        }

        static void Row(VisualElement panel, ChallengeDef d, ChallengeProgress c, string reward)
        {
            var row = Kit.Box(panel, "ch-row");
            if (c.Done) row.AddToClassList("ch-row--done");
            var top = Kit.Box(row, "ch-row-top");
            Kit.L(top, (c.Done ? "✓  " : "") + d.Describe(), "ch-row-text");
            Kit.L(top, c.Done ? "DONE" : reward, "ch-row-reward");
            if (d.Single) { if (!c.Done) Kit.L(row, "in one match", "ch-row-count"); return; }
            var bar = Kit.Box(row, "k-ta-bar");
            Kit.Box(bar, "k-ta-bar-fill").style.width = Length.Percent(100f * Mathf.Clamp01((float)c.Progress / Mathf.Max(1, d.Target)));
            Kit.L(row, Mathf.Min(c.Progress, d.Target) + " / " + d.Target, "ch-row-count");
        }

        static void Item(VisualElement panel, Item it, string kicker)
        {
            var box = Kit.Box(panel, "ch-item");
            var chip = Kit.Box(box, "k-sw"); chip.style.backgroundColor = it.A;
            var t = Kit.Box(box, "ch-item-text");
            Kit.L(t, kicker, "ch-item-k");
            Kit.L(t, it.Name + "  ·  " + Cosmetics.SlotNames[(int)it.Slot], "ch-item-name");
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Play"), (PromptBar.Btn.B, "Back")); }
    }
}
