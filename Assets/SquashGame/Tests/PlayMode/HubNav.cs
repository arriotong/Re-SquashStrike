// ===========================================================
// Assets/SquashGame/Tests/PlayMode/HubNav.cs
// UI redesign U2: getting around the new screens in tests, the way a
// player does - tabs with E, cards with the arrows, Enter.
// ===========================================================

using System;
using System.Collections;
using NUnit.Framework;
using SquashGame.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SquashGame.Tests
{
    public static class HubNav
    {
        /// <summary>On the main menu hub: go to <paramref name="tab"/>, focus the card <paramref name="card"/>, and pick it.</summary>
        public static IEnumerator Open(FrontEnd fe, Keyboard kb, Func<ButtonControl, IEnumerator> tap, string tab, string card, bool pick = true)
        {
            var hub = fe.Top as MainMenuScreen;
            Assert.That(hub, Is.Not.Null, "on the main menu hub");
            for (int i = 0; i < 6 && hub.Tabs.Current != tab; i++) yield return tap(kb.eKey);
            Assert.That(hub.Tabs.Current, Is.EqualTo(tab));
            yield return null;
            for (int i = 0; i < 8 && !(hub.Focused is CardItem c && c.Title == card); i++) yield return tap(kb.rightArrowKey);
            for (int i = 0; i < 8 && !(hub.Focused is CardItem c && c.Title == card); i++) yield return tap(kb.leftArrowKey);
            Assert.That(hub.Focused, Is.InstanceOf<CardItem>());
            Assert.That(((CardItem)hub.Focused).Title, Is.EqualTo(card));
            if (pick) yield return tap(kb.enterKey);
        }

        /// <summary>Move the focus to the screen's primary action (down / right until it has it).</summary>
        public static IEnumerator ToPrimary(FrontEnd fe, ButtonControl down, ButtonControl right, Func<ButtonControl, IEnumerator> tap)
        {
            for (int i = 0; i < 12 && !(fe.Top.Focused is PrimaryItem); i++) yield return tap(i % 3 == 2 ? right : down);
            Assert.That(fe.Top.Focused, Is.InstanceOf<PrimaryItem>(), "the primary action");
        }
    }
}
