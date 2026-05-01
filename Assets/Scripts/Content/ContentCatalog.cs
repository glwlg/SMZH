using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XTD.Content
{
    [CreateAssetMenu(menuName = "X-TD/Content/Content Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        public List<CardDefinition> cards = new();
        public List<UnitDefinition> units = new();
        public List<ArtifactDefinition> artifacts = new();
        public List<EncounterDefinition> encounters = new();
        public List<MapNodeDefinition> nodes = new();

        public CardDefinition FindCard(string id) => cards.FirstOrDefault(card => card != null && card.id == id);

        public UnitDefinition FindUnit(string id) => units.FirstOrDefault(unit => unit != null && unit.id == id);

        public ArtifactDefinition FindArtifact(string id) =>
            artifacts.FirstOrDefault(artifact => artifact != null && artifact.id == id);

        public EncounterDefinition FindEncounter(string id) =>
            encounters.FirstOrDefault(encounter => encounter != null && encounter.id == id);

        public EncounterDefinition FirstEncounter(MapNodeType nodeType) =>
            encounters.FirstOrDefault(encounter => encounter != null && encounter.nodeType == nodeType);
    }
}
