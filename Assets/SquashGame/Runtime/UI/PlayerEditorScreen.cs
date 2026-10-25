// ===========================================================
// Assets/SquashGame/Runtime/UI/PlayerEditorScreen.cs
// P6 the name editor (from your profile): pick a star, type the names
// you want them to have - real ones too: they stay on this PC
// (players.json). Export to share a names file; import one.
//
//   six choices: the player, first name, last name, reset this player,
//   export, import - and Save
// ===========================================================

using SquashGame.Profile;
using SquashSim.Career;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class PlayerEditorScreen : GameScreen
    {
        protected override string Kicker { get { return "YOUR PROFILE"; } }
        protected override string Title { get { return "Player names"; } }
        protected override bool ShowChip { get { return false; } }
        int _i;
        TextItem _first, _last;
        Label _who, _note;
        /// <summary>The star being edited and the name fields (tests).</summary>
        public Star Current { get { return SquashSim.Career.Stars.All()[_i]; } }
        public TextItem First { get { return _first; } }
        public TextItem Last { get { return _last; } }

        protected override void Compose()
        {
            var all = SquashSim.Career.Stars.All();
            var labels = new string[all.Count];
            for (int k = 0; k < all.Count; k++) labels[k] = "No. " + all[k].Rank + "  " + all[k].First + " " + all[k].Last;
            var p = Kit.Panel(Content, "THE STARS"); p.AddToClassList("pf-panel");
            AddTo(p, new CarouselItem("Player", labels, () => _i, v => { Keep(); _i = v; Show(); }));
            _who = Kit.L(p, "", "k-cr-note");
            _first = AddTo(p, new TextItem("First name", "", PlayerNames.MaxLength));
            _last = AddTo(p, new TextItem("Last name", "", PlayerNames.MaxLength));
            AddTo(p, new ActionItem("Reset this player", () => { PlayerNames.ResetOne(Current.Id); Show(); _note.text = "Back to " + Current.First + " " + Current.Last + "."; }));
            Kit.L(p, "Any names you like, real ones too - they stay on this PC. Every match, ranking and draw uses them.", "k-cr-note");

            var share = Kit.Panel(Content, "SHARE"); share.AddToClassList("pf-panel");
            AddTo(share, new ActionItem("Export names", () => { Keep(); int n = PlayerNames.Export(); _note.text = n + " renamed players written to " + PlayerNames.ExportPath; }));
            AddTo(share, new ActionItem("Import names", () =>
            {
                int n = PlayerNames.Import();
                Show();
                _note.text = n < 0 ? "Put a names file at " + PlayerNames.ImportPath + " and import again." : n + " players renamed from " + PlayerNames.ImportName + ".";
            }));
            _note = Kit.L(share, "", "pf-saved");
            AddTo(Root, new PrimaryItem("Save", () => { Keep(); Fe.Pop(); }));
            Show();
        }

        void Show()
        {
            var s = Current;
            var n = SquashSim.Career.Stars.Names(s);
            _first.Field.SetValueWithoutNotify(n.first);
            _last.Field.SetValueWithoutNotify(n.last);
            _who.text = s.Nat + "  ·  " + Roster.StyleName(s.Style) + "  ·  RATED " + s.Rating + (PlayerNames.Renamed(s.Id) ? "  ·  renamed (was " + s.First + " " + s.Last + ")" : "");
        }

        /// <summary>Keep what is typed for the star on show.</summary>
        public void Keep()
        {
            _first.StopTyping(); _last.StopTyping();
            PlayerNames.Set(Current.Id, _first.Value, _last.Value);
        }

        public override void Back() { Keep(); Fe.Pop(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Save and back")); }
    }
}
