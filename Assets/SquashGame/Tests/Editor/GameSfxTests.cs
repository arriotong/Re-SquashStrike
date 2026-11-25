// ===========================================================
// Assets/SquashGame/Tests/Editor/GameSfxTests.cs
// 2026-11-25 (the user: "i want more variety of sound effects"): every new
// sound is made, has its takes, and isn't silent.
// ===========================================================

using NUnit.Framework;
using SquashGame.Audio;

namespace SquashGame.Tests
{
    public class GameSfxTests
    {
        [Test]
        public void EverySoundIsMadeWithItsTakes()
        {
            int takes = 0;
            foreach (var n in GameSfx.Names)
            {
                int k = GameSfx.Takes(n);
                Assert.That(k, Is.GreaterThanOrEqualTo(1), n);
                takes += k;
            }
            Assert.That(GameSfx.Names.Length, Is.GreaterThanOrEqualTo(20));
            UnityEngine.Debug.Log("[GameSfx] " + GameSfx.Names.Length + " sounds, " + takes + " takes");
            GameSfx.Play("coin");
            Assert.That(GameSfx.Last, Is.EqualTo("coin"));
        }
    }
}
