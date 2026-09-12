using System;
using System.Collections.Generic;
using System.Text;

public sealed class AdventureSamplingMetric
{
    private double sum;
    private double sumSquares;

    public int Count { get; private set; }
    public int Min { get; private set; } = int.MaxValue;
    public int Max { get; private set; } = int.MinValue;
    public double Mean => Count == 0 ? 0d : sum / Count;
    public double Variance => Count == 0 ? 0d : Math.Max(0d, (sumSquares / Count) - (Mean * Mean));
    public double StandardDeviation => Math.Sqrt(Variance);

    internal void Add(int value)
    {
        Count++;
        sum += value;
        sumSquares += (double)value * value;
        Min = Math.Min(Min, value);
        Max = Math.Max(Max, value);
    }

    internal bool IsEquivalentTo(AdventureSamplingMetric other)
    {
        return other != null && Count == other.Count && Min == other.Min && Max == other.Max &&
               Math.Abs(Mean - other.Mean) < 0.0000001d && Math.Abs(Variance - other.Variance) < 0.0000001d;
    }
}

public sealed class AdventureRowSamplingReport
{
    public int FirstSeed { get; internal set; }
    public int SeedCount { get; internal set; }
    public int RowsPerSeed { get; internal set; }
    public int TotalRows => SeedCount * RowsPerSeed;

    public AdventureSamplingMetric BlocksPerRow { get; } = new AdventureSamplingMetric();
    public AdventureSamplingMetric OccupiedCellsPerRow { get; } = new AdventureSamplingMetric();
    public AdventureSamplingMetric GapCellsPerRow { get; } = new AdventureSamplingMetric();
    public AdventureSamplingMetric EligibleCarriersPerRow { get; } = new AdventureSamplingMetric();
    public AdventureSamplingMetric InitialBoardCarrierCandidates { get; } = new AdventureSamplingMetric();

    public Dictionary<int, long> WidthCounts { get; } = new Dictionary<int, long>();
    public Dictionary<BlockType, long> TypeCounts { get; } = new Dictionary<BlockType, long>();
    public Dictionary<BlockType, AdventureSamplingMetric> TypeCountsPerSeed { get; } = new Dictionary<BlockType, AdventureSamplingMetric>();
    public Dictionary<BlockType, int> ZeroOccurrenceSeedCounts { get; } = new Dictionary<BlockType, int>();
    public Dictionary<int, int> InitialCarrierCountDistribution { get; } = new Dictionary<int, int>();
    public Dictionary<int, int> InitialCarrierBelowRequirementCounts { get; } = new Dictionary<int, int>();

    public float RawGapChance { get; internal set; }
    public float RawLargeBlockChance { get; internal set; }
    public float RawRockChance { get; internal set; }
    public float RawFireChance { get; internal set; }
    public float RawSliceChance { get; internal set; }
    public float RawChainedChance { get; internal set; }
    public float RawFrozenChance { get; internal set; }

    public double GetTypeFrequency(BlockType type)
    {
        long totalBlocks = 0;
        foreach (long count in TypeCounts.Values)
            totalBlocks += count;

        return totalBlocks == 0 || !TypeCounts.TryGetValue(type, out long typeCount)
            ? 0d
            : (double)typeCount / totalBlocks;
    }

    public double GetZeroOccurrenceSeedFrequency(BlockType type)
    {
        return SeedCount == 0 || !ZeroOccurrenceSeedCounts.TryGetValue(type, out int count)
            ? 0d
            : (double)count / SeedCount;
    }

    public double GetInitialCarrierBelowRequirementFrequency(int requirement)
    {
        return SeedCount == 0 || !InitialCarrierBelowRequirementCounts.TryGetValue(requirement, out int count)
            ? 0d
            : (double)count / SeedCount;
    }

    public bool IsEquivalentTo(AdventureRowSamplingReport other)
    {
        if (other == null || FirstSeed != other.FirstSeed || SeedCount != other.SeedCount || RowsPerSeed != other.RowsPerSeed ||
            !BlocksPerRow.IsEquivalentTo(other.BlocksPerRow) ||
            !OccupiedCellsPerRow.IsEquivalentTo(other.OccupiedCellsPerRow) ||
            !GapCellsPerRow.IsEquivalentTo(other.GapCellsPerRow) ||
            !EligibleCarriersPerRow.IsEquivalentTo(other.EligibleCarriersPerRow) ||
            !InitialBoardCarrierCandidates.IsEquivalentTo(other.InitialBoardCarrierCandidates))
        {
            return false;
        }

        return DictionariesEqual(WidthCounts, other.WidthCounts) &&
               DictionariesEqual(TypeCounts, other.TypeCounts) &&
               MetricDictionariesEqual(TypeCountsPerSeed, other.TypeCountsPerSeed) &&
               DictionariesEqual(ZeroOccurrenceSeedCounts, other.ZeroOccurrenceSeedCounts) &&
               DictionariesEqual(InitialCarrierCountDistribution, other.InitialCarrierCountDistribution) &&
               DictionariesEqual(InitialCarrierBelowRequirementCounts, other.InitialCarrierBelowRequirementCounts);
    }

    public string ToSummaryString()
    {
        StringBuilder summary = new StringBuilder();
        summary.Append($"Seeds={SeedCount}, RowsPerSeed={RowsPerSeed}, TotalRows={TotalRows}\n");
        summary.Append($"Raw: Gap={RawGapChance:F3}, Large={RawLargeBlockChance:F3}, Rock={RawRockChance:F3}, Fire={RawFireChance:F3}, Slice={RawSliceChance:F3}, Chain={RawChainedChance:F3}, Ice={RawFrozenChance:F3}\n");
        summary.Append($"Rows: blocks mean={BlocksPerRow.Mean:F3}, occupied mean={OccupiedCellsPerRow.Mean:F3}, gaps mean={GapCellsPerRow.Mean:F3}\n");
        summary.Append($"Initial carriers: mean={InitialBoardCarrierCandidates.Mean:F3}, min={InitialBoardCarrierCandidates.Min}, max={InitialBoardCarrierCandidates.Max}");
        return summary.ToString();
    }

    private static bool DictionariesEqual<TKey, TValue>(Dictionary<TKey, TValue> left, Dictionary<TKey, TValue> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (KeyValuePair<TKey, TValue> pair in left)
            if (!right.TryGetValue(pair.Key, out TValue value) || !EqualityComparer<TValue>.Default.Equals(pair.Value, value))
                return false;

        return true;
    }

    private static bool MetricDictionariesEqual<TKey>(Dictionary<TKey, AdventureSamplingMetric> left, Dictionary<TKey, AdventureSamplingMetric> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (KeyValuePair<TKey, AdventureSamplingMetric> pair in left)
            if (!right.TryGetValue(pair.Key, out AdventureSamplingMetric value) || !pair.Value.IsEquivalentTo(value))
                return false;

        return true;
    }
}

public static class AdventureRowSampling
{
    private static readonly BlockType[] TrackedTypes =
    {
        BlockType.Normal, BlockType.Rock, BlockType.Fire, BlockType.Slice, BlockType.Chained, BlockType.Ice
    };

    private static readonly int[] CarrierRequirements = { 1, 2, 3, 4, 5, 6 };

    public static AdventureRowSamplingReport Sample(
        AdventureRowGenerationContext context,
        int firstSeed,
        int seedCount,
        int rowsPerSeed)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (seedCount <= 0) throw new ArgumentOutOfRangeException(nameof(seedCount));
        if (rowsPerSeed <= 0) throw new ArgumentOutOfRangeException(nameof(rowsPerSeed));

        AdventureRowSamplingReport report = CreateReport(context, firstSeed, seedCount, rowsPerSeed);
        for (int seedOffset = 0; seedOffset < seedCount; seedOffset++)
        {
            int seed = unchecked(firstSeed + seedOffset);
            SampleRowsForSeed(context, new AdventureGameplayRng(seed), rowsPerSeed, report);
            SampleInitialCarrierSupply(context, seed, report);
        }

        return report;
    }

    private static AdventureRowSamplingReport CreateReport(AdventureRowGenerationContext context, int firstSeed, int seedCount, int rowsPerSeed)
    {
        AdventureRowSamplingReport report = new AdventureRowSamplingReport
        {
            FirstSeed = firstSeed,
            SeedCount = seedCount,
            RowsPerSeed = rowsPerSeed,
            RawGapChance = context.GapChance,
            RawLargeBlockChance = context.LargeBlockChance,
            RawRockChance = context.RockChance,
            RawFireChance = context.FireChance,
            RawSliceChance = context.SliceChance,
            RawChainedChance = context.ChainedChance,
            RawFrozenChance = context.FrozenChance
        };

        for (int width = 1; width <= 4; width++) report.WidthCounts[width] = 0;
        foreach (BlockType type in TrackedTypes)
        {
            report.TypeCounts[type] = 0;
            report.TypeCountsPerSeed[type] = new AdventureSamplingMetric();
            report.ZeroOccurrenceSeedCounts[type] = 0;
        }
        foreach (int requirement in CarrierRequirements) report.InitialCarrierBelowRequirementCounts[requirement] = 0;
        return report;
    }

    private static void SampleRowsForSeed(
        AdventureRowGenerationContext context,
        AdventureGameplayRng rng,
        int rowsPerSeed,
        AdventureRowSamplingReport report)
    {
        Dictionary<BlockType, int> typeCountsForSeed = new Dictionary<BlockType, int>();
        foreach (BlockType type in TrackedTypes) typeCountsForSeed[type] = 0;

        for (int rowIndex = 0; rowIndex < rowsPerSeed; rowIndex++)
        {
            List<GridManager.BlockData> row = AdventureRowGenerator.GenerateRowData(context, rng);
            int occupiedCells = 0;
            int eligibleCarrierCandidates = 0;
            foreach (GridManager.BlockData block in row)
            {
                occupiedCells += block.width;
                if (report.WidthCounts.ContainsKey(block.width)) report.WidthCounts[block.width]++;
                if (report.TypeCounts.ContainsKey(block.blockType))
                {
                    report.TypeCounts[block.blockType]++;
                    typeCountsForSeed[block.blockType]++;
                }
                if (IsEligibleCarrierCandidate(block)) eligibleCarrierCandidates++;
            }

            report.BlocksPerRow.Add(row.Count);
            report.OccupiedCellsPerRow.Add(occupiedCells);
            report.GapCellsPerRow.Add(Math.Max(0, context.GridWidth - occupiedCells));
            report.EligibleCarriersPerRow.Add(eligibleCarrierCandidates);
        }

        foreach (BlockType type in TrackedTypes)
        {
            report.TypeCountsPerSeed[type].Add(typeCountsForSeed[type]);
            if (typeCountsForSeed[type] == 0) report.ZeroOccurrenceSeedCounts[type]++;
        }
    }

    private static void SampleInitialCarrierSupply(AdventureRowGenerationContext context, int seed, AdventureRowSamplingReport report)
    {
        AdventureGameplayRng rng = new AdventureGameplayRng(seed);
        int carrierCount = 0;
        for (int rowIndex = 0; rowIndex < context.InitialRowCount; rowIndex++)
        {
            List<GridManager.BlockData> row = AdventureRowGenerator.GenerateRowData(context, rng);
            foreach (GridManager.BlockData block in row)
                if (IsEligibleCarrierCandidate(block)) carrierCount++;
        }

        report.InitialBoardCarrierCandidates.Add(carrierCount);
        report.InitialCarrierCountDistribution.TryGetValue(carrierCount, out int distributionCount);
        report.InitialCarrierCountDistribution[carrierCount] = distributionCount + 1;
        foreach (int requirement in CarrierRequirements)
            if (carrierCount < requirement) report.InitialCarrierBelowRequirementCounts[requirement]++;
    }

    private static bool IsEligibleCarrierCandidate(GridManager.BlockData block)
    {
        return block.blockType == BlockType.Normal && !block.isRock && !block.isChained && !block.isFrozen;
    }
}
