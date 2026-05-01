using System.Linq;
using NUnit.Framework;
using XTD.Cards;
using XTD.Content;

namespace XTD.Tests
{
    public sealed class DeckRuntimeTests
    {
        [Test]
        public void RefillHandIfEmpty_DoesNotDrawWhileHandStillHasCards()
        {
            var catalog = DemoContentFactory.CreateCatalog();
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
            var catalog = DemoContentFactory.CreateCatalog();
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
    }
}
