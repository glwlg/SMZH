using NUnit.Framework;
using UnityEngine;
using XTD.Content;

namespace XTD.Tests
{
    public sealed class CardDefinitionTests
    {
        [Test]
        public void CommandCost_CountsStructuresButIgnoresSoldiers()
        {
            var soldier = ScriptableObject.CreateInstance<UnitDefinition>();
            soldier.role = UnitRole.Soldier;
            soldier.commandCost = 99;

            var structure = ScriptableObject.CreateInstance<UnitDefinition>();
            structure.role = UnitRole.Structure;
            structure.commandCost = 99;

            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.unitSpawns.Add(new CardUnitSpawn { unit = soldier, count = 8 });
            card.unitSpawns.Add(new CardUnitSpawn { unit = structure, count = 2 });

            Assert.That(card.CommandCost(), Is.EqualTo(2));

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(structure);
            Object.DestroyImmediate(soldier);
        }
    }
}
