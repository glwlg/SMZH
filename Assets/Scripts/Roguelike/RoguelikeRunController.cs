using System.Collections.Generic;
using UnityEngine;
using XTD.Content;

namespace XTD.Roguelike
{
    public sealed class RoguelikeRunController : MonoBehaviour
    {
        [SerializeField] private ContentCatalog catalog;

        private readonly MapGenerationService mapGeneration = new();
        private List<List<MapNodeRuntime>> mapRows;

        public RunState State { get; private set; }
        public IReadOnlyList<List<MapNodeRuntime>> MapRows => mapRows;

        private void Awake()
        {
            catalog ??= DemoContentFactory.CreateCatalog();
            State = DemoContentFactory.CreateStartingRun(catalog);
            mapRows = mapGeneration.Generate(State.seed);
        }

        public IReadOnlyList<MapNodeRuntime> CurrentChoices()
        {
            var index = ((State.floor - 1) * 10) + (State.row - 1);
            return index >= 0 && index < mapRows.Count ? mapRows[index] : new List<MapNodeRuntime>();
        }

        public void Advance(MapNodeRuntime selectedNode)
        {
            State.floor = selectedNode.Floor;
            State.row = selectedNode.Row + 1;
            if (State.row > 10)
            {
                State.floor++;
                State.row = 1;
            }
        }
    }
}
