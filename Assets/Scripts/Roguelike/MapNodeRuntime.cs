using System.Collections.Generic;
using XTD.Content;

namespace XTD.Roguelike
{
    public sealed class MapNodeRuntime
    {
        public MapNodeRuntime(int floor, int row, int nodeIndex, MapNodeType nodeType, string encounterId, IEnumerable<int> nextNodeIndices = null)
        {
            Floor = floor;
            Row = row;
            NodeIndex = nodeIndex;
            NodeType = nodeType;
            EncounterId = encounterId;
            NextNodeIndices = nextNodeIndices != null ? new List<int>(nextNodeIndices) : new List<int>();
        }

        public int Floor { get; }
        public int Row { get; }
        public int NodeIndex { get; }
        public MapNodeType NodeType { get; }
        public string EncounterId { get; }
        public IReadOnlyList<int> NextNodeIndices { get; }
        public string Key => $"{Floor}-{Row}-{NodeIndex}";
    }
}
