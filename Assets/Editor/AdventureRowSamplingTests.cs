using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AdventureRowSamplingTests
{
    [Test]
    public void SameInputsProduceEquivalentReports()
    {
        AdventureRowGenerationContext context = CreateContext(rockChance: 0.1f, fireChance: 0.15f, sliceChance: 0.1f, chainedChance: 0.2f, frozenChance: 0.1f, useCustomRules: true);

        AdventureRowSamplingReport first = AdventureRowSampling.Sample(context, 100, 80, 12);
        AdventureRowSamplingReport second = AdventureRowSampling.Sample(context, 100, 80, 12);

        Assert.That(first.IsEquivalentTo(second), Is.True);
    }

    [Test]
    public void ZeroSpecialAndObstacleChancesProduceNoOccurrences()
    {
        AdventureRowSamplingReport report = AdventureRowSampling.Sample(CreateContext(), 1, 40, 10);

        Assert.That(report.TypeCounts[BlockType.Rock], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Fire], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Slice], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Chained], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Ice], Is.EqualTo(0));
    }

    [Test]
    public void FullRockSuppressesLaterPriorityTypes()
    {
        AdventureRowSamplingReport report = AdventureRowSampling.Sample(
            CreateContext(rockChance: 1f, fireChance: 1f, sliceChance: 1f, chainedChance: 1f, frozenChance: 1f, useCustomRules: true),
            3,
            30,
            8);

        Assert.That(report.TypeCounts[BlockType.Rock], Is.GreaterThan(0));
        Assert.That(report.TypeCounts[BlockType.Normal], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Fire], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Slice], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Chained], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Ice], Is.EqualTo(0));
    }

    [Test]
    public void FireSliceCompetitionUsesExistingPriorityOrder()
    {
        AdventureRowSamplingReport report = AdventureRowSampling.Sample(
            CreateContext(fireChance: 0.4f, sliceChance: 0.8f, chainedChance: 1f, frozenChance: 1f, useCustomRules: true),
            5,
            200,
            10);

        Assert.That(report.TypeCounts[BlockType.Fire], Is.GreaterThan(0));
        Assert.That(report.TypeCounts[BlockType.Slice], Is.GreaterThan(0));
        Assert.That(report.TypeCounts[BlockType.Chained], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Ice], Is.EqualTo(0));
        Assert.That(report.TypeCounts[BlockType.Normal], Is.EqualTo(0));
    }

    [Test]
    public void InitialCarrierAggregationMatchesGeneratedRows()
    {
        AdventureRowGenerationContext context = CreateContext(chainedChance: 0.2f, frozenChance: 0.2f, initialRows: 3);
        const int seed = 42;
        AdventureRowSamplingReport report = AdventureRowSampling.Sample(context, seed, 1, 4);

        int expectedCarrierCount = CountInitialCarrierCandidates(context, seed);
        Assert.That(report.InitialCarrierCountDistribution[expectedCarrierCount], Is.EqualTo(1));
        Assert.That(report.InitialBoardCarrierCandidates.Min, Is.EqualTo(expectedCarrierCount));
        Assert.That(report.InitialBoardCarrierCandidates.Max, Is.EqualTo(expectedCarrierCount));
    }

    private static AdventureRowGenerationContext CreateContext(
        float rockChance = 0f,
        float fireChance = 0f,
        float sliceChance = 0f,
        float chainedChance = 0f,
        float frozenChance = 0f,
        bool useCustomRules = false,
        int initialRows = 5)
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        level.baseGapChance = 0.3f;
        level.largeBlockChance = 0.12f;
        level.rockBlockChance = rockChance;
        level.fireBlockChance = fireChance;
        level.sliceBlockChance = sliceChance;
        level.chainedBlockChance = chainedChance;
        level.frozenBlockChance = frozenChance;
        level.useCustomSpawnRules = useCustomRules;
        return new AdventureRowGenerationContext(8, level, new[] { Color.red, Color.blue }, initialRows);
    }

    private static int CountInitialCarrierCandidates(AdventureRowGenerationContext context, int seed)
    {
        AdventureGameplayRng rng = new AdventureGameplayRng(seed);
        int total = 0;
        for (int rowIndex = 0; rowIndex < context.InitialRowCount; rowIndex++)
        {
            List<GridManager.BlockData> row = AdventureRowGenerator.GenerateRowData(context, rng);
            foreach (GridManager.BlockData block in row)
                if (block.blockType == BlockType.Normal && !block.isRock && !block.isChained && !block.isFrozen)
                    total++;
        }

        return total;
    }
}
