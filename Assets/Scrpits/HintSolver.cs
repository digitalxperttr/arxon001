using System.Collections.Generic;
using UnityEngine;

public struct HintMove
{
    public Block block;
    public int fromX;
    public int fromY;
    public int toX;
    public int landingY;
    public int clearedRowsCount;
    public int lowestClearedY;
    public bool movesRight => toX > fromX;
}

public static class HintSolver
{
    public static bool TryFindBestHint(GridManager grid, out HintMove bestHint)
    {
        bestHint = default;
        if (grid == null || grid.gridArray == null || grid.activeBlocks == null || grid.activeBlocks.Count == 0)
            return false;

        int width = grid.width;
        int height = grid.height;
        Block[,] originalGrid = grid.gridArray;

        List<HintMove> candidateMoves = new List<HintMove>();

        // Reusable simulation buffer
        Block[,] simGrid = new Block[width, height];

        for (int bIndex = 0; bIndex < grid.activeBlocks.Count; bIndex++)
        {
            Block b = grid.activeBlocks[bIndex];
            if (b == null || !b.gameObject.activeInHierarchy || b.isBeingDestroyed || b.isMoving || b.isRock || b.isChained)
                continue;

            int originY = b.y;
            int originX = b.x;
            int blockWidth = b.width;

            // 1. Min and Max allowed X for this block along its row
            int minX = originX;
            for (int cx = originX - 1; cx >= 0; cx--)
            {
                if (originalGrid[cx, originY] == null)
                    minX = cx;
                else
                    break;
            }

            int maxX = originX;
            for (int cx = originX + blockWidth; cx < width; cx++)
            {
                if (originalGrid[cx, originY] == null)
                    maxX = cx - blockWidth + 1;
                else
                    break;
            }

            if (minX == originX && maxX == originX)
                continue; // Cannot move horizontally

            // 2. Test each possible targetX
            for (int targetX = minX; targetX <= maxX; targetX++)
            {
                if (targetX == originX)
                    continue;

                // Copy original grid to simGrid
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        simGrid[x, y] = originalGrid[x, y];
                    }
                }

                // Remove block b from its current cells in simGrid
                for (int cx = originX; cx < originX + blockWidth; cx++)
                {
                    if (cx >= 0 && cx < width && originY >= 0 && originY < height)
                    {
                        if (simGrid[cx, originY] == b)
                            simGrid[cx, originY] = null;
                    }
                }

                // Calculate where block b lands at targetX
                int landingY = originY;
                while (landingY > 0)
                {
                    bool canFall = true;
                    int checkY = landingY - 1;
                    for (int cx = targetX; cx < targetX + blockWidth; cx++)
                    {
                        if (simGrid[cx, checkY] != null)
                        {
                            canFall = false;
                            break;
                        }
                    }

                    if (canFall)
                        landingY--;
                    else
                        break;
                }

                // Place block b at (targetX, landingY)
                for (int cx = targetX; cx < targetX + blockWidth; cx++)
                {
                    simGrid[cx, landingY] = b;
                }

                // Simulate gravity for other blocks in the grid from bottom to top
                SimulateGridGravity(simGrid, width, height, b);

                // Check for full rows
                int clearedCount = 0;
                int lowestClearedY = -1;

                for (int y = 0; y < height; y++)
                {
                    bool rowFull = true;
                    for (int x = 0; x < width; x++)
                    {
                        if (simGrid[x, y] == null)
                        {
                            rowFull = false;
                            break;
                        }
                    }

                    if (rowFull)
                    {
                        clearedCount++;
                        if (lowestClearedY == -1)
                            lowestClearedY = y;
                    }
                }

                if (clearedCount > 0)
                {
                    HintMove move = new HintMove
                    {
                        block = b,
                        fromX = originX,
                        fromY = originY,
                        toX = targetX,
                        landingY = landingY,
                        clearedRowsCount = clearedCount,
                        lowestClearedY = lowestClearedY
                    };
                    candidateMoves.Add(move);
                }
            }
        }

        if (candidateMoves.Count == 0)
            return false;

        // Rank candidates:
        // 1. Highest clearedRowsCount (multi-line clears first)
        // 2. Lowest lowestClearedY (clearing bottom rows is better)
        // 3. Shortest travel distance
        candidateMoves.Sort((a, b) =>
        {
            int clearDiff = b.clearedRowsCount.CompareTo(a.clearedRowsCount);
            if (clearDiff != 0) return clearDiff;

            int yDiff = a.lowestClearedY.CompareTo(b.lowestClearedY);
            if (yDiff != 0) return yDiff;

            int distA = Mathf.Abs(a.toX - a.fromX);
            int distB = Mathf.Abs(b.toX - b.fromX);
            return distA.CompareTo(distB);
        });

        bestHint = candidateMoves[0];
        return true;
    }

    private static void SimulateGridGravity(Block[,] simGrid, int width, int height, Block movedBlock)
    {
        HashSet<Block> processedBlocks = new HashSet<Block>();

        for (int y = 1; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Block b = simGrid[x, y];
                if (b == null || b == movedBlock || processedBlocks.Contains(b))
                    continue;

                // Process block once from its leftmost cell
                if (x > b.x)
                    continue;

                processedBlocks.Add(b);

                int blockWidth = b.width;
                int currentY = y;
                int fallTargetY = currentY;

                while (fallTargetY > 0)
                {
                    bool canFall = true;
                    int checkY = fallTargetY - 1;
                    for (int cx = b.x; cx < b.x + blockWidth; cx++)
                    {
                        if (simGrid[cx, checkY] != null)
                        {
                            canFall = false;
                            break;
                        }
                    }

                    if (canFall)
                        fallTargetY--;
                    else
                        break;
                }

                if (fallTargetY != currentY)
                {
                    for (int cx = b.x; cx < b.x + blockWidth; cx++)
                    {
                        simGrid[cx, currentY] = null;
                        simGrid[cx, fallTargetY] = b;
                    }
                }
            }
        }
    }
}
