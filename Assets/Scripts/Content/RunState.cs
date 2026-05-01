using System;
using System.Collections.Generic;

namespace XTD.Content
{
    [Serializable]
    public sealed class RunState
    {
        public int floor = 1;
        public int row = 1;
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

        public RunState Clone()
        {
            return new RunState
            {
                floor = floor,
                row = row,
                gold = gold,
                playerHp = playerHp,
                heroExperience = heroExperience,
                seed = seed,
                isComplete = isComplete,
                isDefeated = isDefeated,
                lastMessage = lastMessage,
                deckCardIds = new List<string>(deckCardIds),
                artifactIds = new List<string>(artifactIds),
                permanentArtifactIds = new List<string>(permanentArtifactIds)
            };
        }
    }
}
