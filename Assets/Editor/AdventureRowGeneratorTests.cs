using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AdventureRowGeneratorTests
{
    private static readonly Color[] Palette =
    {
        Color.red, Color.yellow, Color.green, Color.blue, new Color(0.6f, 0.1f, 0.8f)
    };

    [Test]
    public void SameSeedAndContextProduceTheSameRowSequence()
    {
        AdventureRowGenerationContext context = CreateContext();
        AdventureGameplayRng first = new AdventureGameplayRng(12345);
        AdventureGameplayRng second = new AdventureGameplayRng(12345);

        AssertRowsEqual(AdventureRowGenerator.GenerateRowData(context, first), AdventureRowGenerator.GenerateRowData(context, second));
        AssertRowsEqual(AdventureRowGenerator.GenerateRowData(context, first), AdventureRowGenerator.GenerateRowData(context, second));
    }

    [Test]
    public void UnityRandomConsumptionDoesNotChangeAdventureRows()
    {
        AdventureRowGenerationContext context = CreateContext();
        AdventureGameplayRng isolated = new AdventureGameplayRng(77);
        AdventureGameplayRng control = new AdventureGameplayRng(77);

        AssertRowsEqual(AdventureRowGenerator.GenerateRowData(context, isolated), AdventureRowGenerator.GenerateRowData(context, control));

        UnityEngine.Random.State originalState = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(999);
            for (int i = 0; i < 50; i++)
                UnityEngine.Random.value.ToString();
        }
        finally
        {
            UnityEngine.Random.state = originalState;
        }

        AssertRowsEqual(AdventureRowGenerator.GenerateRowData(context, isolated), AdventureRowGenerator.GenerateRowData(context, control));
    }

    [Test]
    public void EmptyRowFallbackIsDeterministic()
    {
        AdventureRowGenerationContext context = CreateContext(gapChance: 1f);
        List<GridManager.BlockData> first = AdventureRowGenerator.GenerateRowData(context, new AdventureGameplayRng(9));
        List<GridManager.BlockData> second = AdventureRowGenerator.GenerateRowData(context, new AdventureGameplayRng(9));

        Assert.That(first.Count, Is.GreaterThan(0));
        AssertRowsEqual(first, second);
    }

    [Test]
    public void FullRowsStillReceiveOneDeterministicGap()
    {
        AdventureRowGenerationContext context = CreateContext(gapChance: 0f, useCustomRules: true, minSize: 1, maxSize: 1);
        List<GridManager.BlockData> row = AdventureRowGenerator.GenerateRowData(context, new AdventureGameplayRng(14));

        Assert.That(CountOccupiedCells(row), Is.LessThan(context.GridWidth));
        AssertRowsEqual(row, AdventureRowGenerator.GenerateRowData(context, new AdventureGameplayRng(14)));
    }

    [Test]
    public void RockThenSpecialThenObstaclePriorityIsPreserved()
    {
        AdventureRowGenerationContext rockContext = CreateContext(gapChance: 0f, rockChance: 1f, fireChance: 1f, sliceChance: 1f, chainedChance: 1f, frozenChance: 1f, useCustomRules: true);
        foreach (GridManager.BlockData block in AdventureRowGenerator.GenerateRowData(rockContext, new AdventureGameplayRng(5)))
            Assert.That(block.blockType, Is.EqualTo(BlockType.Rock));

        AdventureRowGenerationContext fireContext = CreateContext(gapChance: 0f, rockChance: 0f, fireChance: 1f, sliceChance: 1f, chainedChance: 1f, frozenChance: 1f, useCustomRules: true);
        foreach (GridManager.BlockData block in AdventureRowGenerator.GenerateRowData(fireContext, new AdventureGameplayRng(5)))
            Assert.That(block.blockType, Is.EqualTo(BlockType.Fire));
    }

    [Test]
    public void TargetObstacleLimiterCapsSingleTargetRowsWithoutChangingBlockWidths()
    {
        List<GridManager.BlockData> row = new List<GridManager.BlockData>
        {
            Obstacle(AdventureObjectiveTarget.Ice, 0, 4),
            Obstacle(AdventureObjectiveTarget.Ice, 4, 2),
            Obstacle(AdventureObjectiveTarget.Ice, 6, 1)
        };

        ApplyLimit(row, new[] { AdventureObjectiveTarget.Ice }, 2, 2, new Dictionary<AdventureObjectiveTarget, int>());

        Assert.That(CountTargets(row), Is.EqualTo(2));
        Assert.That(row[0].width, Is.EqualTo(4));
        Assert.That(row[1].width, Is.EqualTo(2));
        Assert.That(row[2].width, Is.EqualTo(1));
        Assert.That(row[2].blockType, Is.EqualTo(BlockType.Normal));
    }

    [Test]
    public void TargetObstacleLimiterCapsMixedObjectiveTargetsAcrossTheWholeRow()
    {
        List<GridManager.BlockData> row = new List<GridManager.BlockData>
        {
            Obstacle(AdventureObjectiveTarget.Rock, 0, 1),
            Obstacle(AdventureObjectiveTarget.Ice, 2, 1),
            Obstacle(AdventureObjectiveTarget.Chain, 4, 1)
        };

        ApplyLimit(row, new[] { AdventureObjectiveTarget.Rock, AdventureObjectiveTarget.Ice, AdventureObjectiveTarget.Chain }, 2, 2,
            new Dictionary<AdventureObjectiveTarget, int>());

        Assert.That(CountTargets(row), Is.EqualTo(2));
        Assert.That(row[2].blockType, Is.EqualTo(BlockType.Normal));
    }

    [Test]
    public void TargetObstacleLimiterPausesOnlyTheTargetTypeAlreadyAtTheBoardLimit()
    {
        List<GridManager.BlockData> row = new List<GridManager.BlockData>
        {
            Obstacle(AdventureObjectiveTarget.Ice, 0, 1),
            Obstacle(AdventureObjectiveTarget.Rock, 2, 1)
        };
        Dictionary<AdventureObjectiveTarget, int> active = new Dictionary<AdventureObjectiveTarget, int>
        {
            { AdventureObjectiveTarget.Ice, 2 },
            { AdventureObjectiveTarget.Rock, 1 }
        };

        ApplyLimit(row, new[] { AdventureObjectiveTarget.Ice, AdventureObjectiveTarget.Rock }, 2, 2, active);

        Assert.That(row[0].blockType, Is.EqualTo(BlockType.Normal));
        Assert.That(row[1].blockType, Is.EqualTo(BlockType.Rock));

        active[AdventureObjectiveTarget.Ice] = 1;
        row[0] = Obstacle(AdventureObjectiveTarget.Ice, 0, 1);
        ApplyLimit(row, new[] { AdventureObjectiveTarget.Ice, AdventureObjectiveTarget.Rock }, 2, 2, active);
        Assert.That(row[0].blockType, Is.EqualTo(BlockType.Ice));
    }

    [Test]
    public void TargetObstacleLimiterDoesNotConsumeGameplayRng()
    {
        AdventureRowGenerationContext context = CreateContext(gapChance: 0f, frozenChance: 1f, useCustomRules: true, minSize: 1, maxSize: 1);
        AdventureGameplayRng limited = new AdventureGameplayRng(97);
        AdventureGameplayRng control = new AdventureGameplayRng(97);
        List<GridManager.BlockData> limitedRow = AdventureRowGenerator.GenerateRowData(context, limited);
        AdventureRowGenerator.GenerateRowData(context, control);

        ApplyLimit(limitedRow, new[] { AdventureObjectiveTarget.Ice }, 2, 2, new Dictionary<AdventureObjectiveTarget, int>());

        AssertRowsEqual(
            AdventureRowGenerator.GenerateRowData(context, limited),
            AdventureRowGenerator.GenerateRowData(context, control));
    }

    [Test]
    public void ZeroLimitsPreserveHistoricalRows()
    {
        List<GridManager.BlockData> row = new List<GridManager.BlockData>
        {
            Obstacle(AdventureObjectiveTarget.Ice, 0, 1),
            Obstacle(AdventureObjectiveTarget.Ice, 2, 1),
            Obstacle(AdventureObjectiveTarget.Ice, 4, 1)
        };

        ApplyLimit(row, new[] { AdventureObjectiveTarget.Ice }, 0, 0, new Dictionary<AdventureObjectiveTarget, int> { { AdventureObjectiveTarget.Ice, 99 } });

        Assert.That(CountTargets(row), Is.EqualTo(3));
    }

    private static AdventureRowGenerationContext CreateContext(
        float gapChance = 0.35f,
        float rockChance = 0.1f,
        float fireChance = 0.08f,
        float sliceChance = 0.06f,
        float chainedChance = 0.05f,
        float frozenChance = 0.04f,
        bool useCustomRules = false,
        int minSize = 1,
        int maxSize = 4)
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        level.baseGapChance = gapChance;
        level.largeBlockChance = 0.12f;
        level.rockBlockChance = rockChance;
        level.fireBlockChance = fireChance;
        level.sliceBlockChance = sliceChance;
        level.chainedBlockChance = chainedChance;
        level.frozenBlockChance = frozenChance;
        level.useCustomSpawnRules = useCustomRules;
        level.minBlockSize = minSize;
        level.maxBlockSize = maxSize;
        return new AdventureRowGenerationContext(8, level, Palette);
    }

    private static int CountOccupiedCells(List<GridManager.BlockData> row)
    {
        int occupied = 0;
        foreach (GridManager.BlockData block in row)
            occupied += block.width;
        return occupied;
    }

    private static void ApplyLimit(List<GridManager.BlockData> row, IEnumerable<AdventureObjectiveTarget> targets, int perRow, int activeBoard,
        IReadOnlyDictionary<AdventureObjectiveTarget, int> activeCounts)
    {
        AdventureTargetObstacleSpawnLimiter.Apply(row, new HashSet<AdventureObjectiveTarget>(targets), activeCounts, perRow, activeBoard, Color.red);
    }

    private static int CountTargets(List<GridManager.BlockData> row)
    {
        int count = 0;
        foreach (GridManager.BlockData data in row)
            if (AdventureTargetObstacleSpawnLimiter.GetTarget(data) != AdventureObjectiveTarget.None)
                count++;
        return count;
    }

    private static GridManager.BlockData Obstacle(AdventureObjectiveTarget target, int x, int width)
    {
        return new GridManager.BlockData
        {
            x = x,
            width = width,
            blockType = target == AdventureObjectiveTarget.Rock ? BlockType.Rock :
                target == AdventureObjectiveTarget.Ice ? BlockType.Ice : BlockType.Chained,
            isRock = target == AdventureObjectiveTarget.Rock,
            isFrozen = target == AdventureObjectiveTarget.Ice,
            isChained = target == AdventureObjectiveTarget.Chain,
            color = target == AdventureObjectiveTarget.Rock ? Color.gray : Color.blue
        };
    }

    private static void AssertRowsEqual(List<GridManager.BlockData> expected, List<GridManager.BlockData> actual)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count));
        for (int i = 0; i < expected.Count; i++)
        {
            GridManager.BlockData left = expected[i];
            GridManager.BlockData right = actual[i];
            Assert.That(right.x, Is.EqualTo(left.x));
            Assert.That(right.width, Is.EqualTo(left.width));
            Assert.That(right.color, Is.EqualTo(left.color));
            Assert.That(right.fireTargetColor, Is.EqualTo(left.fireTargetColor));
            Assert.That(right.blockType, Is.EqualTo(left.blockType));
            Assert.That(right.isRock, Is.EqualTo(left.isRock));
            Assert.That(right.isChained, Is.EqualTo(left.isChained));
            Assert.That(right.isFrozen, Is.EqualTo(left.isFrozen));
        }
    }
}
