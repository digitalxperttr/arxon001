using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AdventureGameplayRng
{
    private uint state;

    public int Seed { get; }

    public AdventureGameplayRng(int seed)
    {
        Seed = seed;
        state = unchecked((uint)seed);
        if (state == 0)
            state = 0x6D2B79F5u;
    }

    public float NextFloat01()
    {
        return (NextUInt() >> 8) * (1f / 16777216f);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));

        uint range = (uint)(maxExclusive - minInclusive);
        uint threshold = unchecked((uint)-(int)range) % range;
        uint value;
        do
        {
            value = NextUInt();
        }
        while (value < threshold);

        return minInclusive + (int)(value % range);
    }

    private uint NextUInt()
    {
        uint value = state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        return value;
    }
}

public sealed class AdventureRowGenerationContext
{
    public int GridWidth { get; }
    public float GapChance { get; }
    public float LargeBlockChance { get; }
    public float FrozenChance { get; }
    public float RockChance { get; }
    public float ChainedChance { get; }
    public float FireChance { get; }
    public float SliceChance { get; }
    public bool UseCustomSpawnRules { get; }
    public int MinBlockSize { get; }
    public int MaxBlockSize { get; }
    public int InitialRowCount { get; }
    public IReadOnlyList<Color> NormalGemColors { get; }

    public AdventureRowGenerationContext(int gridWidth, LevelData level, IReadOnlyList<Color> normalGemColors, int initialRowCount = 5)
    {
        GridWidth = gridWidth;
        GapChance = level.baseGapChance;
        LargeBlockChance = level.largeBlockChance;
        FrozenChance = level.frozenBlockChance;
        RockChance = level.rockBlockChance;
        ChainedChance = level.chainedBlockChance;
        UseCustomSpawnRules = level.useCustomSpawnRules;
        FireChance = UseCustomSpawnRules ? level.fireBlockChance : 0f;
        SliceChance = UseCustomSpawnRules ? level.sliceBlockChance : 0f;
        MinBlockSize = Mathf.Max(1, level.minBlockSize);
        MaxBlockSize = Mathf.Max(MinBlockSize, level.maxBlockSize);
        InitialRowCount = Mathf.Max(0, initialRowCount);
        NormalGemColors = normalGemColors ?? Array.Empty<Color>();
    }
}

public static class AdventureRowGenerator
{
    public static List<GridManager.BlockData> GenerateRowData(AdventureRowGenerationContext context, AdventureGameplayRng rng)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        List<GridManager.BlockData> rowData = new List<GridManager.BlockData>();
        int currentX = 0;
        int blockCountInRow = 0;
        float currentT4 = 1f - context.LargeBlockChance;
        float currentT3 = currentT4 - 0.15f;
        float currentT2 = currentT3 - 0.25f;

        while (currentX < context.GridWidth)
        {
            float gapChance = blockCountInRow > 2 ? context.GapChance * 0.75f : context.GapChance;
            if (rng.NextFloat01() < gapChance)
            {
                currentX++;
                continue;
            }

            int blockWidth;
            if (context.UseCustomSpawnRules)
            {
                int availableCells = context.GridWidth - currentX;
                if (availableCells < context.MinBlockSize)
                    break;

                blockWidth = rng.NextInt(context.MinBlockSize, context.MaxBlockSize + 1);
                if (blockWidth > availableCells)
                    blockWidth = availableCells;
            }
            else
            {
                float widthRoll = rng.NextFloat01();
                if (widthRoll > currentT4) blockWidth = 4;
                else if (widthRoll > currentT3) blockWidth = 3;
                else if (widthRoll > currentT2) blockWidth = 2;
                else blockWidth = 1;

                if (currentX + blockWidth > context.GridWidth)
                    blockWidth = context.GridWidth - currentX;
            }

            GridManager.BlockData data = CreateNormalBlockData(currentX, rng, context.NormalGemColors);
            data.width = blockWidth;
            ApplySpecialRules(ref data, rng, context);
            rowData.Add(data);
            currentX += blockWidth;
            blockCountInRow++;
        }

        if (blockCountInRow == 0)
        {
            int fallbackWidth = context.UseCustomSpawnRules
                ? RollCustomBlockWidth(context.MinBlockSize, context.MaxBlockSize, context.GridWidth, rng)
                : 1;

            if (fallbackWidth <= 0)
                fallbackWidth = context.UseCustomSpawnRules ? Mathf.Clamp(context.MinBlockSize, 1, context.GridWidth) : 1;

            int maxX = Mathf.Max(1, context.GridWidth - fallbackWidth + 1);
            int fallbackX = rng.NextInt(0, maxX);
            GridManager.BlockData data = CreateNormalBlockData(fallbackX, rng, context.NormalGemColors);
            data.width = fallbackWidth;
            ApplySpecialRules(ref data, rng, context);
            rowData.Add(data);
        }

        EnsureRowHasAtLeastOneGap(rowData, context.GridWidth, context.UseCustomSpawnRules ? context.MinBlockSize : 1, rng);
        return rowData;
    }

    private static GridManager.BlockData CreateNormalBlockData(int x, AdventureGameplayRng rng, IReadOnlyList<Color> normalGemColors)
    {
        GridManager.BlockData data = new GridManager.BlockData
        {
            x = x,
            width = 1,
            blockType = BlockType.Normal
        };

        if (normalGemColors == null || normalGemColors.Count == 0)
        {
            data.color = Color.white;
            return data;
        }

        int gemIndex = rng.NextInt(0, normalGemColors.Count);
        data.color = normalGemColors[gemIndex];
        data.fireTargetColor = ResolveGemColor(data.color);
        return data;
    }

    private static void ApplySpecialRules(ref GridManager.BlockData data, AdventureGameplayRng rng, AdventureRowGenerationContext context)
    {
        data.isRock = rng.NextFloat01() < context.RockChance;
        if (data.isRock)
        {
            data.blockType = BlockType.Rock;
            data.color = Color.gray;
        }

        if (!data.isRock)
        {
            float specialRoll = rng.NextFloat01();
            if (specialRoll < context.FireChance)
                data.blockType = BlockType.Fire;
            else if (specialRoll < context.FireChance + context.SliceChance)
                data.blockType = BlockType.Slice;
        }

        bool canApplyObstacle = data.blockType == BlockType.Normal && !data.isRock;
        data.isChained = canApplyObstacle && rng.NextFloat01() < context.ChainedChance;
        if (data.isChained)
            data.blockType = BlockType.Chained;

        canApplyObstacle = data.blockType == BlockType.Normal && !data.isRock && !data.isChained;
        data.isFrozen = canApplyObstacle && rng.NextFloat01() < context.FrozenChance;
        if (data.isFrozen)
            data.blockType = BlockType.Ice;
    }

    private static int RollCustomBlockWidth(int minBlockSize, int maxBlockSize, int availableCells, AdventureGameplayRng rng)
    {
        int minSize = Mathf.Max(1, minBlockSize);
        int maxSize = Mathf.Max(minSize, maxBlockSize);
        if (availableCells < minSize)
            return 0;

        return Mathf.Min(rng.NextInt(minSize, maxSize + 1), availableCells);
    }

    private static void EnsureRowHasAtLeastOneGap(List<GridManager.BlockData> rowData, int gridWidth, int minimumAllowedWidth, AdventureGameplayRng rng)
    {
        if (rowData == null || rowData.Count == 0)
            return;

        bool[] occupied = new bool[gridWidth];
        foreach (GridManager.BlockData data in rowData)
        {
            for (int i = 0; i < data.width; i++)
            {
                int cellX = data.x + i;
                if (cellX >= 0 && cellX < gridWidth)
                    occupied[cellX] = true;
            }
        }

        for (int x = 0; x < gridWidth; x++)
            if (!occupied[x])
                return;

        int randomIndex = rng.NextInt(0, rowData.Count);
        GridManager.BlockData selectedData = rowData[randomIndex];
        if (selectedData.width > minimumAllowedWidth)
        {
            selectedData.width--;
            rowData[randomIndex] = selectedData;
        }
        else
        {
            rowData.RemoveAt(randomIndex);
        }
    }

    private static GemColor ResolveGemColor(Color color)
    {
        Color.RGBToHSV(color, out float hue, out _, out _);
        if (hue < 0.05f || hue >= 0.95f) return GemColor.Red;
        if (hue < 0.20f) return GemColor.Yellow;
        if (hue < 0.48f) return GemColor.Green;
        if (hue < 0.68f) return GemColor.Blue;
        return GemColor.Purple;
    }
}

// This only changes already-generated row data. It deliberately makes no random rolls so a preview
// remains the exact row that will later be pushed onto the board.
public static class AdventureTargetObstacleSpawnLimiter
{
    public static void Apply(
        List<GridManager.BlockData> rowData,
        ISet<AdventureObjectiveTarget> incompleteTargets,
        IReadOnlyDictionary<AdventureObjectiveTarget, int> activeBoardCounts,
        int perRowLimit,
        int activeBoardLimit,
        Color normalFallbackColor)
    {
        if (rowData == null || incompleteTargets == null || incompleteTargets.Count == 0 ||
            (perRowLimit <= 0 && activeBoardLimit <= 0))
            return;

        int allowedInRow = 0;
        for (int i = 0; i < rowData.Count; i++)
        {
            GridManager.BlockData data = rowData[i];
            AdventureObjectiveTarget target = GetTarget(data);
            if (!incompleteTargets.Contains(target))
                continue;

            int activeCount = 0;
            if (activeBoardCounts != null)
                activeBoardCounts.TryGetValue(target, out activeCount);

            if ((activeBoardLimit > 0 && activeCount >= activeBoardLimit) ||
                (perRowLimit > 0 && allowedInRow >= perRowLimit))
            {
                ConvertToNormal(ref data, normalFallbackColor);
                rowData[i] = data;
                continue;
            }

            allowedInRow++;
        }
    }

    public static AdventureObjectiveTarget GetTarget(GridManager.BlockData data)
    {
        if (data.isRock || data.blockType == BlockType.Rock) return AdventureObjectiveTarget.Rock;
        if (data.isFrozen || data.blockType == BlockType.Ice) return AdventureObjectiveTarget.Ice;
        if (data.isChained || data.blockType == BlockType.Chained) return AdventureObjectiveTarget.Chain;
        return AdventureObjectiveTarget.None;
    }

    private static void ConvertToNormal(ref GridManager.BlockData data, Color normalFallbackColor)
    {
        data.blockType = BlockType.Normal;
        data.isRock = false;
        data.isFrozen = false;
        data.isChained = false;
        data.color = normalFallbackColor;
    }
}
