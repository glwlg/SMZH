using System;
using System.Collections.Generic;

namespace XTD.Content
{
    [Serializable]
    public sealed class RunState
    {
        public int floor = 1;
        public int row = 1;
        public HeroClassType heroClass = GameContentFactory.DefaultHeroClass;
        public int gold;
        public float playerHp = 100f;
        public int heroExperience;
        public int seed = 12345;
        public bool isComplete;
        public bool isDefeated;
        public string lastMessage = string.Empty;
        public List<string> deckCardIds = new();
        public List<string> artifactIds = new();
        public List<string> permanentArtifactIds = new();
        public List<int> availableNodeIndices = new();
        public List<string> selectedNodeKeys = new();
        public List<string> eventLog = new();
        public List<string> playtestRecords = new();
        public List<FloorAffixType> floorAffixes = new();
        public List<string> pendingCardRewardIds = new();
        public int pendingCardRewardPickCount;
        public int pendingCardRewardSkipGold;
        public List<string> shopOfferCardIds = new();
        public List<string> shopBoughtCardIds = new();
        public int shopOfferRerollCount;
        public bool shopRemoveUsed;
        public int artifactRefreshesRemaining = 2;
        public int artifactOfferRerollCount;

        public RunState Clone()
        {
            return new RunState
            {
                floor = floor,
                row = row,
                heroClass = heroClass,
                gold = gold,
                playerHp = playerHp,
                heroExperience = heroExperience,
                seed = seed,
                isComplete = isComplete,
                isDefeated = isDefeated,
                lastMessage = lastMessage,
                deckCardIds = new List<string>(deckCardIds),
                artifactIds = new List<string>(artifactIds),
                permanentArtifactIds = new List<string>(permanentArtifactIds),
                availableNodeIndices = new List<int>(availableNodeIndices),
                selectedNodeKeys = new List<string>(selectedNodeKeys),
                eventLog = new List<string>(eventLog),
                playtestRecords = new List<string>(playtestRecords),
                floorAffixes = new List<FloorAffixType>(floorAffixes),
                pendingCardRewardIds = new List<string>(pendingCardRewardIds),
                pendingCardRewardPickCount = pendingCardRewardPickCount,
                pendingCardRewardSkipGold = pendingCardRewardSkipGold,
                shopOfferCardIds = new List<string>(shopOfferCardIds),
                shopBoughtCardIds = new List<string>(shopBoughtCardIds),
                shopOfferRerollCount = shopOfferRerollCount,
                shopRemoveUsed = shopRemoveUsed,
                artifactRefreshesRemaining = artifactRefreshesRemaining,
                artifactOfferRerollCount = artifactOfferRerollCount
            };
        }
    }
}
