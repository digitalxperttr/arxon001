using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BlockTestSpawner : MonoBehaviour
{
    public enum DebugSpecialType
    {
        None,
        Fire,
        Slice
    }

    [System.Serializable]
    public class TestBlockPlacement
    {
        public int x;
        public int y;
        [Min(1)] public int width = 1;
        public BlockType blockType = BlockType.Normal;
        public DebugSpecialType specialType = DebugSpecialType.None;
        public int normalGemIndex;
    }

    [Header("Debug Board")]
    [SerializeField] private bool useTestLayout = true;
    [SerializeField] private bool fillUnspecifiedCellsWithRandomBlocks = false;
    [SerializeField] private List<TestBlockPlacement> placements = new List<TestBlockPlacement>();

    public bool TryBuildInitialBoard(GridManager gridManager)
    {
        if (!useTestLayout || gridManager == null)
            return false;

        Dictionary<Vector2Int, GridManager.BlockData> plannedBlocks = new Dictionary<Vector2Int, GridManager.BlockData>();

        foreach (TestBlockPlacement placement in placements)
        {
            if (placement == null)
                continue;

            if (!IsWithinBounds(gridManager, placement.x, placement.y))
            {
                Debug.LogWarning($"BlockTestSpawner: ({placement.x}, {placement.y}) grid disinda, placement atlandi.");
                continue;
            }

            BlockType resolvedType = ResolveBlockType(placement);
            int resolvedWidth = ResolveWidth(placement, resolvedType);

            if (placement.x + resolvedWidth > gridManager.width)
            {
                Debug.LogWarning(
                    $"BlockTestSpawner: ({placement.x}, {placement.y}) icin width {resolvedWidth} gridi asiyor, placement atlandi."
                );
                continue;
            }

            GridManager.BlockData blockData = gridManager.CreateSingleCellBlockData(
                placement.x,
                resolvedType,
                placement.normalGemIndex
            );
            blockData.width = resolvedWidth;

            if (HasOverlap(plannedBlocks, placement.x, placement.y, resolvedWidth))
            {
                Debug.LogWarning(
                    $"BlockTestSpawner: ({placement.x}, {placement.y}) icin width {resolvedWidth} baska bir test bloguyla cakisiyor, placement atlandi."
                );
                continue;
            }

            plannedBlocks[new Vector2Int(placement.x, placement.y)] = blockData;
        }

        if (fillUnspecifiedCellsWithRandomBlocks)
        {
            FillEmptyCells(gridManager, plannedBlocks);
        }

        List<Vector2Int> orderedPositions = new List<Vector2Int>(plannedBlocks.Keys);
        orderedPositions.Sort((a, b) =>
        {
            if (a.y != b.y)
                return a.y.CompareTo(b.y);

            return a.x.CompareTo(b.x);
        });

        foreach (Vector2Int position in orderedPositions)
        {
            gridManager.SpawnConfiguredBlock(plannedBlocks[position], position.y);
        }

        gridManager.RebuildGridMemory();
        gridManager.GenerateNextRowData();

        return true;
    }

    private static bool IsWithinBounds(GridManager gridManager, int x, int y)
    {
        return x >= 0 && x < gridManager.width && y >= 0 && y < gridManager.height;
    }

    private static BlockType ResolveBlockType(TestBlockPlacement placement)
    {
        switch (placement.specialType)
        {
            case DebugSpecialType.Fire:
                return BlockType.Fire;
            case DebugSpecialType.Slice:
                return BlockType.Slice;
            default:
                return placement.blockType;
        }
    }

    private static int ResolveWidth(TestBlockPlacement placement, BlockType resolvedType)
    {
        if (resolvedType == BlockType.Slice)
            return 1;

        return Mathf.Max(1, placement.width);
    }

    private static bool HasOverlap(
        Dictionary<Vector2Int, GridManager.BlockData> plannedBlocks,
        int x,
        int y,
        int width
    )
    {
        foreach (KeyValuePair<Vector2Int, GridManager.BlockData> pair in plannedBlocks)
        {
            if (pair.Key.y != y)
                continue;

            int otherMinX = pair.Key.x;
            int otherMaxX = pair.Key.x + pair.Value.width - 1;
            int thisMaxX = x + width - 1;

            bool overlaps = x <= otherMaxX && thisMaxX >= otherMinX;

            if (overlaps)
                return true;
        }

        return false;
    }

    private static void FillEmptyCells(
        GridManager gridManager,
        Dictionary<Vector2Int, GridManager.BlockData> plannedBlocks
    )
    {
        for (int y = 0; y < gridManager.height; y++)
        {
            for (int x = 0; x < gridManager.width; x++)
            {
                Vector2Int key = new Vector2Int(x, y);

                if (plannedBlocks.ContainsKey(key))
                    continue;

                plannedBlocks[key] = gridManager.CreateSingleCellBlockData(
                    x,
                    BlockType.Normal,
                    0,
                    true
                );
            }
        }
    }
}
