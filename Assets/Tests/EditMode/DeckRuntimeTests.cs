using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XTD.Cards;
using XTD.Content;

namespace XTD.Tests
{
    public sealed class DeckRuntimeTests
    {
        [Test]
        public void RefillHandIfEmpty_DoesNotDrawWhileHandStillHasCards()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var cards = catalog.cards.Take(6).ToList();
            var deck = new DeckRuntime(cards, 1)
            {
                MaxHandSize = 5
            };

            deck.DrawFullHand();
            deck.Play(deck.Hand[0]);

            var drawn = deck.RefillHandIfEmpty();

            Assert.That(drawn, Is.EqualTo(0));
            Assert.That(deck.Hand.Count, Is.EqualTo(4));
            Assert.That(deck.UsedPile.Count, Is.EqualTo(1));
        }

        [Test]
        public void RefillHandIfEmpty_RecyclesUsedPileIntoCardPool()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var cards = catalog.cards.Take(3).ToList();
            var deck = new DeckRuntime(cards, 1)
            {
                MaxHandSize = 3
            };

            deck.DrawFullHand();
            deck.Play(deck.Hand[0]);
            deck.Play(deck.Hand[0]);
            deck.Play(deck.Hand[0]);

            Assert.That(deck.Hand.Count, Is.EqualTo(0));
            Assert.That(deck.UsedPile.Count, Is.EqualTo(3));

            var drawn = deck.RefillHandIfEmpty();

            Assert.That(drawn, Is.EqualTo(3));
            Assert.That(deck.Hand.Count, Is.EqualTo(3));
            Assert.That(deck.CardPool.Count, Is.EqualTo(0));
            Assert.That(deck.UsedPile.Count, Is.EqualTo(0));
        }

        [Test]
        public void DiscardHand_MovesCurrentHandToUsedPile()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var cards = catalog.cards.Take(6).ToList();
            var deck = new DeckRuntime(cards, 1)
            {
                MaxHandSize = 5
            };

            deck.DrawFullHand();

            var discarded = deck.DiscardHand();

            Assert.That(discarded, Is.EqualTo(5));
            Assert.That(deck.Hand.Count, Is.EqualTo(0));
            Assert.That(deck.UsedPile.Count, Is.EqualTo(5));
            Assert.That(deck.CardPool.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExhaustCards_RemovesHeroAndDrawsReplacement()
        {
            var hero = ScriptableObject.CreateInstance<CardDefinition>();
            hero.id = "hero_card";
            hero.displayName = "测试英雄";
            hero.type = CardType.Hero;

            var supportA = ScriptableObject.CreateInstance<CardDefinition>();
            supportA.id = "support_a";
            supportA.type = CardType.Tactic;

            var supportB = ScriptableObject.CreateInstance<CardDefinition>();
            supportB.id = "support_b";
            supportB.type = CardType.Spell;

            var supportC = ScriptableObject.CreateInstance<CardDefinition>();
            supportC.id = "support_c";
            supportC.type = CardType.Structure;

            var deck = new DeckRuntime(new[] { hero, supportA, supportB, supportC }, 1)
            {
                MaxHandSize = 4
            };

            deck.DrawFullHand();
            var cardToRecycle = deck.Hand.First(card => card != hero);
            deck.Play(cardToRecycle);

            var removed = deck.ExhaustCards(card => card == hero);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(deck.Hand.Count, Is.EqualTo(3));
            Assert.That(deck.Hand.Any(card => card == hero), Is.False);
            Assert.That(deck.CardPool.Any(card => card == hero), Is.False);
            Assert.That(deck.UsedPile.Any(card => card == hero), Is.False);
        }
    }
}
