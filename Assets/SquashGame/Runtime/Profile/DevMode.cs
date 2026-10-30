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

        /// <summary>Your name is the developer's (on a PC that allows it).</summary>
        public static bool On
        {
            get
            {
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
