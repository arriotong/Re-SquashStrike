// ===========================================================
// Assets/SquashGame/Runtime/UI/DevScreens.cs
// 2026-11-25 (the user: "in the profile can you add a password button if i
// enter 0908 it gives me infinite money and dev tools so i can test the
// game easier"):
//
//   CodeScreen      Profile > Enter a code: the code typed (hidden); the
//                   right one turns developer mode on (DevMode.TryCode) and
//                   opens the tools; a wrong one says so
//   DevToolsScreen  coins unlimited (always, while on); unlock everything;
//                   your Special meter full every rally; the debug keys
//                   (F1 panel, F2 debug view, 1-4 slow motion, P pause,
//                   . one step); developer mode off
// ===========================================================

using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class CodeScreen : DialogScreen
    {
        TextItem _code;
        Label _says;
        /// <summary>The code field (tests).</summary>
        public TextItem Field { get { return _code; } }
        protected override string Title { get { return "Enter a code"; } }
        protected override string YesLabel { get { return "OK"; } }
        protected override string NoLabel { get { return "Back"; } }

        protected override void Build()
        {
            _code = AddTo(Content, new TextItem("Code", "", 12));
            _code.Field.isPasswordField = true;
            _code.Field.RegisterCallback<KeyDownEvent>(e =>                            // Enter in the field tries it
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { OnYes(); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            _says = Kit.L(Content, "", "k-cr-note");
            base.Build();
        }

        public override void OnShow() { base.OnShow(); Focus(_code); }

        protected override void OnYes()
        {
            _code.StopTyping();
            if (DevMode.TryCode(_code.Value))
            {
                Sounds.Play("star");
                Fe.Toast("DEVELOPER MODE", "Unlimited coins and the dev tools are on.");
                Fe.Replace(new DevToolsScreen());
                return;
            }
            Sounds.Play("back");
            _says.text = "That's not it.";
            _code.Field.value = "";
        }
    }

    public sealed class DevToolsScreen : GameScreen
    {
        protected override string Kicker { get { return "DEVELOPER MODE"; } }
        protected override string Title { get { return "Dev tools"; } }
        protected override bool ShowChip { get { return false; } }
        protected override bool ShowCoins { get { return true; } }
        static DevState S { get { return ProfileStore.Current.Dev; } }
        Label _said;

        protected override void Compose()
        {
            var col = Kit.Box(Content, "k-col");
            var p = Kit.Panel(col, "WHILE IT'S ON");
            Kit.L(p, "Coins never run out (" + DevMode.Coins.ToString("N0") + "), career points never run out (" + DevMode.CareerPoints + ").", "k-cr-note");
            AddTo(p, new ActionItem("Unlock everything", () =>
            {
                int n = DevMode.UnlockEverything();
                Sounds.Play("stamp");
                _said.text = n + " new items - every kit piece, every pro card at Holo, every perk at level 3.";
            }, "▶"));
            AddTo(p, new SelectorItem("Full Special meter", new[] { "Off", "On" }, () => S.FullMeter ? 1 : 0, v => { S.FullMeter = v == 1; ProfileStore.Save(); }));
            Kit.L(p, "On: your Special meter is full at the start of every rally (Quick Match, career, the Gauntlet - not online).", "k-cr-note");
            AddTo(p, new SelectorItem("Debug keys", new[] { "Off", "On" }, () => S.DebugKeys ? 1 : 0, v =>
            {
                S.DebugKeys = v == 1; DebugOverlay.DevMode = S.DebugKeys || Application.isEditor; ProfileStore.Save();
            }));
            Kit.L(p, "In a match: F1 the debug panel · F2 the debug view · 1 2 3 4 slow motion · P pause · . one step.", "k-cr-note");
            _said = Kit.L(p, "", "pf-saved");
            AddTo(Root, new PrimaryItem("Turn developer mode off", () => { DevMode.TurnOff(); Fe.Toast("DEVELOPER MODE", "Off. What you got stays yours."); Fe.Pop(); }));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }
}
