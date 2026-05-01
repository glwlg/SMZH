using XTD.Content;

namespace XTD.Roguelike
{
    public readonly struct MapNodeRuntime
    {
        public MapNodeRuntime(int floor, int row, MapNodeType nodeType, string encounterId)
        {
            Floor = floor;
            Row = row;
            NodeType = nodeType;
            EncounterId = encounterId;
        }

        public int Floor { get; }
        public int Row { get; }
        public MapNodeType NodeType { get; }
        public string EncounterId { get; }
    }
}
