// ===========================================================
// Assets/SquashGame/Runtime/UI/CallWords.cs
// P2-M7: the referee's interference calls in plain words, for the
// lower third and the review caption:
//
//   LET      Retriever was clearing
//   STROKE   Retriever was in the line to the front wall
//   NO LET   you weren't going for it
//
// The sim's reasons ("Stroke - opponent did not clear") stay as they
// are (tests, logs); this only rewords them for the screen.
// ===========================================================

using SquashSim;

namespace SquashGame.UI
{
    public static class CallWords
    {
        /// <summary>A call on interference (an appeal, the referee's own stop) or on the ball hitting a player.</summary>
        public static bool IsCall(RallyOutcome o)
        {
            return o != null && (o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet || o.Kind == RallyEnd.Stroke);
        }

        public static string Decision(RallyOutcome o)
        {
            switch (o.Kind)
            {
                case RallyEnd.Let: return "LET";
                case RallyEnd.NoLet: return "NO LET";
                case RallyEnd.Stroke: return "STROKE";
                default: return o.Reason;
            }
        }

        /// <summary>A short name for the screen: "RETRIEVER (MEDIUM)" -> "Retriever".</summary>
        public static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            int k = name.IndexOf(" (");
            if (k > 0) name = name.Substring(0, k);
            if (name.ToUpperInvariant() == name && name.Length > 1) name = name.Substring(0, 1) + name.Substring(1).ToLowerInvariant();
            return name;
        }

        /// <summary>Why, in plain words. <paramref name="striker"/>: who was going for the ball (-1: unknown);
        /// <paramref name="you"/>: the local player in a match against the AI (-1 in two-player: everyone by name).</summary>
        public static string Reason(RallyOutcome o, string[] names, int striker, int you)
        {
            string r = o.Reason ?? "";
            int body = BodyIn(r);
            string s;
            if (body >= 0 && r.Contains("going straight")) s = "the ball hit " + Who(body, names, you) + " on its way to the front wall";
            else if (body >= 0 && r.Contains("via the side wall")) s = "the ball hit " + Who(body, names, you) + ", but was going via the side wall";
            else if (r.Contains("non-striker")) s = "the ball hit " + (striker >= 0 ? Who(1 - striker, names, you) : "the non-striker") + " after the front wall";
            else if (striker < 0) s = Strip(r);
            else
            {
                int ob = 1 - striker;
                string O = Who(ob, names, you), S = Who(striker, names, you);
                if (r.Contains("clearing")) s = O + " " + Was(ob, you) + " clearing";
                else if (r.Contains("play stopped")) s = O + " " + Was(ob, you) + " in the swing - play stopped for safety";
                else if (r.Contains("in the swing")) s = O + " " + Was(ob, you) + " in the swing";
                else if (r.Contains("line to the front wall")) s = O + " " + Was(ob, you) + " in the line to the front wall";
                else if (r.Contains("did not clear")) s = O + " didn't clear";
                else if (r.Contains("every effort")) s = S + " " + Was(striker, you) + "n't going for it";
                else if (r.Contains("could not have reached")) s = S + " couldn't have reached it";
                else if (r.Contains("was clear")) s = O + " " + Was(ob, you) + " out of the way";
                else s = Strip(r);
            }
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
        }

        static string Who(int i, string[] names, int you) { return i == you ? "you" : Short(names != null && i >= 0 && i < names.Length ? names[i] : "P" + (i + 1)); }
        static string Was(int i, int you) { return i == you ? "were" : "was"; }

        /// <summary>"Ball hit P2 ..." -> 1.</summary>
        static int BodyIn(string r)
        {
            int k = r.IndexOf("hit P");
            if (k < 0 || k + 5 >= r.Length) return -1;
            char c = r[k + 5];
            return c == '1' ? 0 : c == '2' ? 1 : -1;
        }

        static string Strip(string r)
        {
            int k = r.IndexOf(" - ");
            return k >= 0 ? r.Substring(k + 3) : r;
        }
    }
}
