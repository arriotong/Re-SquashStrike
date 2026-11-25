// ===========================================================
// Assets/SquashGame/Runtime/Profile/DevMode.cs
// The developer's test mode (2026-10-29, the user: "when I type arrio as
// my name I get unlimited resources so I can test the things out
// better"). On while your player name - the online name you chose, or
// your career player's - is "arrio" (any capitals):
//
//   coins          never below Coins (GearDrops.Wallet tops them up):
//                  every gacha, evolving cards, the kit store
//   career points  never below CareerPoints to spend on your stats
//
// Rename and it is off again; what was bought stays bought.
//
// 2026-10-30: only on the developer's own PC - in the Unity editor, or
// where the save folder holds a file called "devmode" (MarkerFile). A
// shared build has no such file, so the name does nothing there.
//
// 2026-11-25 (the user: "in the profile can you add a password button if i
// enter 0908 it gives me infinite money and dev tools"): Profile > Enter a
// code - the right code turns it on for this profile on any PC (only its
// hash is kept here), with the tools (DevToolsScreen): everything
// unlocked, a full Special meter every rally, the debug keys.
// ===========================================================

using System;
using System.IO;
using UnityEngine;

namespace SquashGame.Profile
{
    public static class DevMode
    {
        public const string Name = "arrio";
        public const int Coins = 9999999, CareerPoints = 99;

        static bool Is(string name) { return name != null && string.Equals(name.Trim(), Name, StringComparison.OrdinalIgnoreCase); }

        public const string MarkerFile = "devmode";
        static bool? _allowed;
        /// <summary>This PC may use it: the editor, or the marker file in the save folder.</summary>
        public static bool Allowed
        {
            get
            {
                if (_allowed == null) _allowed = Application.isEditor || File.Exists(Path.Combine(Application.persistentDataPath, MarkerFile));
                return _allowed.Value;
            }
        }

        /// <summary>The code's hash (SHA-256 of "re:squash/" + the code).</summary>
        const string CodeHash = "0a558d2c5c4e5d63e971be9f0056c9ec6d05e9f8793239440586e375294fb5ee";

        public static bool IsCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var h = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("re:squash/" + code.Trim()));
                var sb = new System.Text.StringBuilder();
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString() == CodeHash;
            }
        }

        static DevState S { get { var p = ProfileStore.Current; if (p.Dev == null) p.Dev = new DevState(); return p.Dev; } }

        /// <summary>The code entered: developer mode on for this profile (coins topped up at once). False: the wrong code.</summary>
        public static bool TryCode(string code)
        {
            if (!IsCode(code)) return false;
            S.Code = true;
            Progression.GearDrops.Wallet.Coins = Math.Max(Progression.GearDrops.Wallet.Coins, Coins);
            ProfileStore.Save();
            return true;
        }

        /// <summary>Developer mode off (the code forgotten; the tools' switches off).</summary>
        public static void TurnOff() { S.Code = false; S.FullMeter = false; S.DebugKeys = false; DebugOverlay.DevMode = Application.isEditor; ProfileStore.Save(); }

        /// <summary>The code was entered on this profile.</summary>
        public static bool ByCode { get { return ProfileStore.Current != null && ProfileStore.Current.Dev != null && ProfileStore.Current.Dev.Code; } }
        /// <summary>The debug keys tool is on (the built game's debug panel).</summary>
        public static bool DebugKeys { get { return On && S.DebugKeys; } }

        /// <summary>The tool: every item in the game owned - kit, the machines' items at their top tier / level.</summary>
        public static int UnlockEverything()
        {
            var p = ProfileStore.Current.Progress;
            int n = 0;
            foreach (var it in Progression.Cosmetics.All())
            {
                if (!p.Owned.Contains(it.Id)) { p.Owned.Add(it.Id); n++; }
                if (Progression.Machines.Counts(it))
                {
                    int top = it.Slot == Progression.Slot.ProCard ? Progression.Machines.TierAt[Progression.Machines.TierAt.Length - 1]
                                                                  : Progression.Machines.PerkLevelAt[Progression.Machines.PerkLevelAt.Length - 1];
                    p.Copies.RemoveAll(c => c.Id == it.Id);
                    p.Copies.Add(new Progression.ItemCount { Id = it.Id, N = top });
                }
            }
            Progression.Machines.CheckSets(); Progression.Machines.JustCompleted.Clear();
            ProfileStore.Save();
            return n;
        }

        /// <summary>The full-meter tool is on.</summary>
        public static bool FullMeter { get { return On && S.FullMeter; } }

        /// <summary>Your name is the developer's (on a PC that allows it), or the code was entered.</summary>
        public static bool On
        {
            get
            {
                if (ByCode) return true;
                if (!Allowed) return false;
                var on = ProfileStore.Current.Online;
                if (on != null && on.NameChosen && Is(on.Name)) return true;
                var c = CareerStore.Current;
                return c != null && Is(c.S.Name);
            }
        }

        /// <summary>The career's stat points topped up (call before showing or spending them).</summary>
        public static void TopUpCareer()
        {
            var c = CareerStore.Current;
            if (c == null || !On) return;
            if (c.S.TrainingPoints < CareerPoints) c.S.TrainingPoints = CareerPoints;
        }
    }
}
