using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AdventureObjectiveRuntimeTests
{
    [SetUp]
    public void SetUp()
    {
        DestroyExistingObjectiveManager();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyExistingObjectiveManager();
    }

    [Test]
    public void ObstacleProgressIsClampedAndChainProgressUsesCompletionEvents()
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        SetRuntimeObjectives(level, new List<AdventureObjectiveDefinition>
        {
            new AdventureObjectiveDefinition
            {
                action = AdventureObjectiveAction.DestroyObstacle,
                target = AdventureObjectiveTarget.Ice,
                requiredAmount = 1
            },
            new AdventureObjectiveDefinition
            {
                action = AdventureObjectiveAction.BreakChain,
                target = AdventureObjectiveTarget.Chain,
                requiredAmount = 1
            }
        });

        GameObject managerObject = new GameObject("ObjectiveManager_Test");
        ObjectiveManager manager = managerObject.AddComponent<ObjectiveManager>();
        manager.Initialize(level);

        manager.ReportObstacleDestroyed(AdventureObjectiveTarget.Ice);
        manager.ReportObstacleDestroyed(AdventureObjectiveTarget.Ice);
        manager.ReportObstacleDestroyed(AdventureObjectiveTarget.Chain);

        IReadOnlyList<ObjectiveRuntimeState> states = manager.GetObjectives();
        Assert.That(states[0].currentAmount, Is.EqualTo(1));
        Assert.That(states[1].currentAmount, Is.EqualTo(1));

        Object.DestroyImmediate(level);
    }

    [Test]
    public void DetachingCollectiblePreservesItsIdWithoutCountingCollection()
    {
        GameObject blockObject = new GameObject("CollectibleCarrier_Test");
        Block block = blockObject.AddComponent<Block>();
        block.AssignCollectible("CR_BLUE", null, false);

        Assert.That(block.TryDetachCollectible(out string id, out Sprite sprite), Is.True);
        Assert.That(id, Is.EqualTo("CR_BLUE"));
        Assert.That(sprite, Is.Null);
        Assert.That(block.HasCollectible(), Is.False);
        Assert.That(block.TryCollectCollectible(), Is.False);

        Object.DestroyImmediate(blockObject);
    }

    [Test]
    public void AreBlocksMovingReturnsFalseWhenGridMemoryIsUnavailable()
    {
        GameObject gridObject = new GameObject("GridManager_NullMemory_Test");
        GridManager grid = gridObject.AddComponent<GridManager>();
        grid.gridArray = null;

        Assert.That(grid.AreBlocksMoving(), Is.False);

        Object.DestroyImmediate(gridObject);
        FieldInfo backingField = typeof(GridManager).GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        backingField?.SetValue(null, null);
    }

    private static void SetRuntimeObjectives(LevelData level, List<AdventureObjectiveDefinition> objectives)
    {
        FieldInfo field = typeof(LevelData).GetField("runtimeObjectives", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(level, objectives);
        FieldInfo backingField = typeof(LevelData).GetField("<IsRuntimeLevel>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        backingField.SetValue(level, true);
    }

    private static void DestroyExistingObjectiveManager()
    {
        ObjectiveManager existing = ObjectiveManager.Instance;
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        FieldInfo backingField = typeof(ObjectiveManager).GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        backingField?.SetValue(null, null);
    }

}
