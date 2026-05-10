using System;
using System.Collections.Generic;
using XTD.Content;

namespace XTD.Cards
{
    public sealed class DeckRuntime
    {
        private readonly Random random;
        private readonly List<CardDefinition> cardPool = new();
        private readonly List<CardDefinition> hand = new();
        private readonly List<CardDefinition> usedPile = new();

        public DeckRuntime(IEnumerable<CardDefinition> startingCards, int seed)
        {
            random = new Random(seed);
            cardPool.AddRange(startingCards);
            Shuffle(cardPool);
        }

        public IReadOnlyList<CardDefinition> CardPool => cardPool;
        public IReadOnlyList<CardDefinition> DrawPile => cardPool;
        public IReadOnlyList<CardDefinition> Hand => hand;
        public IReadOnlyList<CardDefinition> UsedPile => usedPile;
        public IReadOnlyList<CardDefinition> DiscardPile => usedPile;
        public int MaxHandSize { get; set; } = 5;

        public bool ContainsInHand(CardDefinition card) => hand.Contains(card);

        public int Draw(int count)
        {
            var drawn = 0;
            while (drawn < count && hand.Count < MaxHandSize)
            {
                if (cardPool.Count == 0)
                {
                    RecycleUsedIntoCardPool();
                }

                if (cardPool.Count == 0)
                {
                    break;
                }

                var top = cardPool[0];
                cardPool.RemoveAt(0);
                hand.Add(top);
                drawn++;
            }

            return drawn;
        }

        public int DrawFullHand()
        {
            return Draw(MaxHandSize - hand.Count);
        }

        public int RefillHandIfEmpty()
        {
            return hand.Count == 0 ? DrawFullHand() : 0;
        }

        public bool Play(CardDefinition card)
        {
            if (!hand.Remove(card))
            {
                return false;
            }

            usedPile.Add(card);
            return true;
        }

        public void Discard(CardDefinition card)
        {
            if (hand.Remove(card))
            {
                usedPile.Add(card);
            }
        }

        public int DiscardHand()
        {
            var count = hand.Count;
            if (count <= 0)
            {
                return 0;
            }

            usedPile.AddRange(hand);
            hand.Clear();
            return count;
        }

        public int ExhaustCards(Predicate<CardDefinition> match)
        {
            if (match == null)
            {
                return 0;
            }

            var removedFromHand = RemoveAll(hand, match);
            var removed = removedFromHand;
            removed += RemoveAll(cardPool, match);
            removed += RemoveAll(usedPile, match);
            if (removedFromHand > 0)
            {
                Draw(removedFromHand);
            }

            return removed;
        }

        private void RecycleUsedIntoCardPool()
        {
            if (usedPile.Count == 0)
            {
                return;
            }

            cardPool.AddRange(usedPile);
            usedPile.Clear();
            Shuffle(cardPool);
        }

        private void Shuffle(List<CardDefinition> cards)
        {
            for (var i = cards.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
            }
        }

        private static int RemoveAll(List<CardDefinition> cards, Predicate<CardDefinition> match)
        {
            var removed = 0;
            for (var i = cards.Count - 1; i >= 0; i--)
            {
                if (!match(cards[i]))
                {
                    continue;
                }

                cards.RemoveAt(i);
                removed++;
            }

            return removed;
        }
    }
}
