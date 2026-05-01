using UnityEngine;

namespace XTD.Content
{
    [CreateAssetMenu(menuName = "X-TD/Content/Map Node Definition", fileName = "MapNodeDefinition")]
    public sealed class MapNodeDefinition : ScriptableObject
    {
        public MapNodeType nodeType = MapNodeType.NormalMonster;
        public int floor = 1;
        public int row = 1;
        public string encounterId;
        public string rewardTableId;
    }
}
