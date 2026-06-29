using System.Collections.Generic;
using UnityEngine;

public class PlacementGuide : MonoBehaviour
{
    private const float DefaultColumnAlpha = 0.10f;
    private const int DefaultSortingOrder = 5;

    [SerializeField] private Sprite guideSprite;
    [SerializeField] private Color columnGuideColor = new Color(1f, 1f, 1f, DefaultColumnAlpha);
    [SerializeField] private int sortingOrder = DefaultSortingOrder;
    [SerializeField] private float cellScale = 0.96f;

    private readonly List<SpriteRenderer> columnCells = new List<SpriteRenderer>();

    public int SortingOrder => sortingOrder;
    public float ColumnAlpha => columnGuideColor.a;

    private void Awake()
    {
        Hide();
    }

    public void ConfigureFromGrid(GridManager grid)
    {
        if (guideSprite != null || grid == null || grid.cellPrefab == null)
            return;

        SpriteRenderer gridCellRenderer = grid.cellPrefab.GetComponent<SpriteRenderer>();
        if (gridCellRenderer != null)
        {
            guideSprite = gridCellRenderer.sprite;
        }
    }

    public void Show(GridManager grid, Block block, int snappedX, int targetY, IReadOnlyList<Vector2Int> heldBlockOriginCells)
    {
        if (grid == null || block == null)
        {
            Hide();
            return;
        }

        Vector2Int[] targetOffsets = GetTargetCellOffsets(block);
        if (targetOffsets.Length == 0)
        {
            Hide();
            return;
        }

        List<int> columns = GetUniqueTargetColumns(snappedX, targetOffsets);
        List<Vector2Int> pathCells = GetAvailablePathCells(grid, columns, heldBlockOriginCells);

        EnsureCellCount(columnCells, pathCells.Count, "ColumnGuideCell");
        gameObject.SetActive(true);

        for (int i = 0; i < pathCells.Count; i++)
        {
            SpriteRenderer cell = columnCells[i];
            cell.gameObject.SetActive(true);
            cell.transform.position = grid.GetCellWorldPosition(pathCells[i].x, pathCells[i].y);
            cell.transform.localScale = new Vector3(cellScale, cellScale, 1f);
            cell.color = columnGuideColor;
            cell.sortingOrder = sortingOrder;
        }

        DisableUnusedCells(columnCells, pathCells.Count);
    }

    public void Hide()
    {
        DisableUnusedCells(columnCells, 0);

        gameObject.SetActive(false);
    }

    private void EnsureCellCount(List<SpriteRenderer> cells, int count, string cellName)
    {
        while (cells.Count < count)
        {
            GameObject cellObject = new GameObject($"{cellName}_{cells.Count}");
            cellObject.transform.SetParent(transform);

            SpriteRenderer renderer = cellObject.AddComponent<SpriteRenderer>();
            renderer.sprite = guideSprite;
            renderer.sortingOrder = sortingOrder;
            renderer.drawMode = SpriteDrawMode.Simple;

            cells.Add(renderer);
        }
    }

    private void DisableUnusedCells(List<SpriteRenderer> cells, int startIndex)
    {
        for (int i = startIndex; i < cells.Count; i++)
        {
            if (cells[i] != null)
            {
                cells[i].gameObject.SetActive(false);
            }
        }
    }

    private Vector2Int[] GetTargetCellOffsets(Block block)
    {
        int width = Mathf.Max(1, block.width);
        Vector2Int[] offsets = new Vector2Int[width];

        for (int i = 0; i < width; i++)
        {
            offsets[i] = new Vector2Int(i, 0);
        }

        return offsets;
    }

    private List<int> GetUniqueTargetColumns(int snappedX, Vector2Int[] targetOffsets)
    {
        List<int> columns = new List<int>();

        for (int i = 0; i < targetOffsets.Length; i++)
        {
            int column = snappedX + targetOffsets[i].x;
            if (!columns.Contains(column))
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    private List<Vector2Int> GetAvailablePathCells(GridManager grid, List<int> columns, IReadOnlyList<Vector2Int> heldBlockOriginCells)
    {
        List<Vector2Int> pathCells = new List<Vector2Int>();

        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            int x = columns[columnIndex];
            if (x < 0 || x >= grid.width)
                continue;

            for (int y = grid.height - 1; y >= 0; y--)
            {
                if (IsPathCellAvailable(grid, x, y, heldBlockOriginCells))
                {
                    pathCells.Add(new Vector2Int(x, y));
                }
            }
        }

        return pathCells;
    }

    private bool IsPathCellAvailable(GridManager grid, int x, int y, IReadOnlyList<Vector2Int> heldBlockOriginCells)
    {
        if (x < 0 || x >= grid.width || y < 0 || y >= grid.height)
            return false;

        if (ContainsCell(heldBlockOriginCells, x, y))
            return false;

        Block occupyingBlock = grid.gridArray[x, y];
        return occupyingBlock == null;
    }

    private bool ContainsCell(IReadOnlyList<Vector2Int> cells, int x, int y)
    {
        if (cells == null)
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].x == x && cells[i].y == y)
                return true;
        }

        return false;
    }
}
