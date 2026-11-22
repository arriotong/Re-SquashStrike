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
            // 2026-10-29: from the home screen (PLAY / OTHERS / SETTINGS) into the hub with the section; or across to the other one
            var want = MainMenuScreen.HubOf(tab);
            if (fe.Top is MainMenuScreen other && other.Which != want) { fe.Top.Back(); yield return null; yield return null; }
            if (fe.Top is HomeScreen)
            {
                if (want == MainMenuScreen.Hub.Others) yield return tap(kb.downArrowKey);
                yield return tap(kb.enterKey);
                yield return null; yield return null;
            }
            var hub = fe.Top as MainMenuScreen;
            Assert.That(hub, Is.Not.Null, "on the main menu hub");
            for (int i = 0; i < hub.MySections.Length && hub.Tabs.Current != tab; i++) yield return tap(kb.eKey);
            Assert.That(hub.Tabs.Current, Is.EqualTo(tab));
            yield return null;
            for (int i = 0; i < 8 && !(hub.Focused is CardItem c && c.Title == card); i++) yield return tap(kb.rightArrowKey);
            for (int i = 0; i < 8 && !(hub.Focused is CardItem c && c.Title == card); i++) yield return tap(kb.leftArrowKey);
            if (!(hub.Focused is CardItem f && f.Title == card))
            {
                // (the arrows can wander up into the top bar's chips from the end of a row: take the card itself)
                UnityEngine.Debug.Log("[HubNav] " + card + " not reached by the arrows - focused directly");
                var target = hub.Reachable().Find(x => x is CardItem ci && ci.Title == card);
                if (target != null) hub.Focus(target);
            }
            Assert.That(hub.Focused, Is.InstanceOf<CardItem>());
            Assert.That(((CardItem)hub.Focused).Title, Is.EqualTo(card));
            if (pick) yield return tap(kb.enterKey);
        }

        /// <summary>Move the focus (left, then up) until the choice named <paramref name="name"/> has it.</summary>
        public static IEnumerator Seek(FrontEnd fe, string name, ButtonControl left, ButtonControl up, Func<ButtonControl, IEnumerator> tap)
        {
            for (int i = 0; i < 12 && fe.Top.Focused.Name != name; i++) yield return tap(i % 2 == 0 ? left : up);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo(name));
        }

        /// <summary>Move the focus to the screen's primary action (down / right until it has it).</summary>
        public static IEnumerator ToPrimary(FrontEnd fe, ButtonControl down, ButtonControl right, Func<ButtonControl, IEnumerator> tap)
        {
            var seen = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 12 && !(fe.Top.Focused is PrimaryItem); i++) { yield return tap(i % 3 == 2 ? right : down); seen.Add(fe.Top.Focused != null ? fe.Top.Focused.Name : "-"); }
            Assert.That(fe.Top.Focused, Is.InstanceOf<PrimaryItem>(), "the primary action (" + fe.Top.GetType().Name + ": " + string.Join(" > ", seen) + " | items " + string.Join(", ", fe.Top.Reachable().ConvertAll(x => x.GetType().Name + ":" + x.Name)) + ")");
        }
    }
}
