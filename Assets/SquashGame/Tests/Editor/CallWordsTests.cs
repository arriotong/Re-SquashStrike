// ===========================================================
// Assets/SquashGame/Tests/Editor/CallWordsTests.cs
// P2-M7: the referee's calls in plain words on the lower third.
// ===========================================================

using NUnit.Framework;
using SquashGame.UI;
using SquashSim;

namespace SquashGame.Tests
{
    public class CallWordsTests
    {
        static readonly string[] Names = { "YOU", "SHOT-MAKER (HARD)" };

        static string Say(RallyEnd kind, string reason, int striker, int you = 0)
        {
            return CallWords.Reason(new RallyOutcome { Kind = kind, Reason = reason }, Names, striker, you);
        }

        [Test] public void CallsReadPlainly()
        {
            Assert.That(Say(RallyEnd.Let, "Let - opponent was clearing", 0), Is.EqualTo("Shot-maker was clearing"));
            Assert.That(Say(RallyEnd.Let, "Let - opponent was clearing", 1), Is.EqualTo("You were clearing"));
            Assert.That(Say(RallyEnd.Stroke, "Stroke - opponent in the line to the front wall", 0), Is.EqualTo("Shot-maker was in the line to the front wall"));
            Assert.That(Say(RallyEnd.Stroke, MatchSimulation.SwingStopReason, 1), Is.EqualTo("You were in the swing - play stopped for safety"));
            Assert.That(Say(RallyEnd.Stroke, "Stroke - opponent did not clear", 0), Is.EqualTo("Shot-maker didn't clear"));
            Assert.That(Say(RallyEnd.NoLet, "No let - not making every effort", 0), Is.EqualTo("You weren't going for it"));
            Assert.That(Say(RallyEnd.NoLet, "No let - could not have reached the ball", 1), Is.EqualTo("Shot-maker couldn't have reached it"));
            Assert.That(Say(RallyEnd.Stroke, "Stroke - ball hit P2 going straight to the front wall", 0), Is.EqualTo("The ball hit Shot-maker on its way to the front wall"));
            Assert.That(Say(RallyEnd.Let, "Let - ball hit P1 going via the side wall", 1), Is.EqualTo("The ball hit you, but was going via the side wall"));
            Assert.That(Say(RallyEnd.Stroke, "Stroke - opponent did not clear", 0, you: -1), Is.EqualTo("Shot-maker didn't clear"), "two players: by name");
            Assert.That(CallWords.Decision(new RallyOutcome { Kind = RallyEnd.NoLet }), Is.EqualTo("NO LET"));
        }
    }
}
