using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FirstTimeTutorial : MonoBehaviour
{
    private const string TutorialCompletedKey = "TutorialCompleted";
    private const bool TutorialSystemEnabled = false;

    [SerializeField] private TMP_FontAsset tutorialFont;
    [SerializeField] private float startDelay = 0.35f;
    [SerializeField] private float hintFadeDuration = 0.25f;
    [SerializeField] private float hintVisibleDuration = 1f;
    [SerializeField] private float dimAlpha = 0.45f;
    [SerializeField] private float ghostStartDelay = 1.35f;
    [SerializeField] private float ghostRepeatDelay = 3.25f;
    [SerializeField] private float ghostMoveDuration = 1.15f;
    [SerializeField] private float ghostTravelPercent = 0.70f;
    [SerializeField] private float ghostAlpha = 0.50f;
    [SerializeField] private float passivePreviewAlpha = 0.40f;
    [SerializeField] private float successVisibleDuration = 0.8f;

    private GridManager grid;
    private Block activeBlock;
    private int targetX;
    private int targetY;
    private bool tutorialRunning;
    private bool tutorialCompleted;
    private bool userStartedDragging;
    private bool successShown;
    private bool boardBuilt;

    private RectTransform tutorialRoot;
    private RectTransform hintRect;
    private CanvasGroup dimGroup;
    private CanvasGroup hintGroup;
    private CanvasGroup successGroup;
    private Coroutine introRoutine;
    private Coroutine ghostRoutine;
    private GameObject ghostObject;
    private Block activePreviewBlock;
    private Vector3 activePreviewStartPosition;
    private Vector3 activePreviewStartScale;
    private readonly List<GameObject> tutorialPreviewVisuals = new List<GameObject>();

    public bool IsRunning => TutorialSystemEnabled && tutorialRunning && !tutorialCompleted;

    private void Awake()
    {
        if (TutorialSystemEnabled)
            return;

        tutorialRunning = false;
        tutorialCompleted = true;
        DestroyGhostBlock();
        DestroyTutorialPreviewVisuals();
        enabled = false;
    }

    private void Start()
    {
        if (!TutorialSystemEnabled)
            return;

        tutorialCompleted = PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
        if (!ShouldRunTutorial())
            return;

        EnsureTutorialUI();
        introRoutine = StartCoroutine(IntroRoutine());
    }

    public bool TryBuildInitialBoard(GridManager gridManager)
    {
        if (!TutorialSystemEnabled)
            return false;

        if (!ShouldRunTutorial() || gridManager == null || boardBuilt)
            return false;

        grid = gridManager;
        tutorialRunning = true;
        boardBuilt = true;

        SpawnTutorialBoard(gridManager);
        ConfigureTutorialPreview(gridManager);

        return true;
    }

    public bool CanSelectBlock(Block block)
    {
        if (!IsRunning)
            return true;

        return block != null && block == activeBlock;
    }

    public bool IsCorrectPlacement(Block block, int snappedX)
    {
        if (!IsRunning)
            return true;

        return block != null &&
            block == activeBlock &&
            snappedX == targetX &&
            block.y == targetY;
    }

    public bool ShouldUsePreviewDrag(Block block)
    {
        return IsRunning &&
            block != null &&
            block == activeBlock &&
            block == activePreviewBlock;
    }

    public bool TryCommitActivePreviewBlock(Vector3 releasePosition)
    {
        if (!ShouldUsePreviewDrag(activeBlock) || grid == null)
            return false;

        int blockWidth = activeBlock.width;
        float releasedLeftEdge = releasePosition.x - (blockWidth - 1) * 0.5f;
        bool releasedOverTarget =
            Mathf.Abs(releasedLeftEdge - targetX) <= 0.85f &&
            Mathf.Abs(releasePosition.y - targetY) <= 0.85f;

        if (!releasedOverTarget || !IsTutorialTargetEmpty(blockWidth))
            return false;

        DestroyGhostBlock();

        Block committedBlock = activePreviewBlock;
        SpriteRenderer committedRenderer = committedBlock.GetComponent<SpriteRenderer>();
        Sprite committedSprite = committedRenderer != null ? committedRenderer.sprite : null;
        Color committedColor = committedBlock.blockColor;
        tutorialPreviewVisuals.Remove(committedBlock.gameObject);
        activePreviewBlock = null;

        committedBlock.enabled = true;
        committedBlock.x = targetX;
        committedBlock.y = targetY;
        committedBlock.width = 2;
        committedBlock.SetVisual(committedSprite, committedColor, committedBlock.width);

        if (committedBlock.TryGetComponent<Collider2D>(out Collider2D collider))
            collider.enabled = true;

        if (!grid.activeBlocks.Contains(committedBlock))
            grid.activeBlocks.Add(committedBlock);

        grid.UpdateBlockInGrid(committedBlock, targetX, targetY);
        committedBlock.transform.position = releasePosition;
        committedBlock.MoveTo(targetX, targetY);

        return true;
    }

    public void ResetActivePreviewBlock()
    {
        if (!ShouldUsePreviewDrag(activeBlock))
            return;

        activeBlock.transform.position = activePreviewStartPosition;
        activeBlock.transform.localScale = activePreviewStartScale;
        activeBlock.x = targetX;
        activeBlock.y = targetY;
        activeBlock.width = 2;
        SetPreviewObjectAlpha(activeBlock.gameObject, 1f);
    }

    public void NotifyDragStarted()
    {
        if (!IsRunning)
            return;

        userStartedDragging = true;
        DestroyGhostBlock();
    }

    public IEnumerator PlaySuccessBeforePushUp()
    {
        if (!IsRunning || successShown)
            yield break;

        successShown = true;
        DestroyGhostBlock();

        EnsureTutorialUI();
        if (tutorialRoot != null)
            tutorialRoot.gameObject.SetActive(true);

        if (hintGroup != null)
            hintGroup.alpha = 0f;

        yield return FadeCanvasGroup(successGroup, 0f, 1f, 0.15f, false);
        yield return new WaitForSeconds(successVisibleDuration);
        yield return FadeCanvasGroup(successGroup, 1f, 0f, 0.20f, false);
    }

    public IEnumerator CompleteAfterPushUp()
    {
        if (!IsRunning)
            yield break;

        if (grid != null)
            grid.SetAllPreviewVisualsAlpha(1f);

        DestroyTutorialPreviewVisuals();

        yield return FadeCanvasGroup(dimGroup, dimGroup != null ? dimGroup.alpha : 0f, 0f, 0.25f, false);

        tutorialCompleted = true;
        tutorialRunning = false;
        PlayerPrefs.SetInt(TutorialCompletedKey, 1);
        PlayerPrefs.Save();

        HideTutorialUI();
    }

    private bool ShouldRunTutorial()
    {
        if (!TutorialSystemEnabled)
            return false;

        if (tutorialCompleted || PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1)
            return false;

        return ProgressManager.Instance == null ||
            ProgressManager.Instance.currentSelectedLevel == null;
    }

    private void SpawnTutorialBoard(GridManager gridManager)
    {
        // R1
        SpawnBlock(gridManager, 0, 0, 4, 2);
        SpawnBlock(gridManager, 5, 0, 2, 0);

        // R2
        SpawnBlock(gridManager, 0, 1, 3, 1);
        SpawnBlock(gridManager, 5, 1, 3, 4);

        // R3
        SpawnBlock(gridManager, 0, 2, 3, 0);
        SpawnBlock(gridManager, 5, 2, 2, 3);

        targetX = 3;
        targetY = 1;

        gridManager.RebuildGridMemory();
    }

    private Block SpawnBlock(GridManager gridManager, int x, int y, int width, int normalGemIndex)
    {
        GridManager.BlockData data = gridManager.CreateSingleCellBlockData(
            x,
            BlockType.Normal,
            normalGemIndex
        );
        data.width = width;

        return gridManager.SpawnConfiguredBlock(data, y);
    }

    private void ConfigureTutorialPreview(GridManager gridManager)
    {
        List<GridManager.BlockData> previewRow = new List<GridManager.BlockData>
        {
            CreatePreviewBlock(gridManager, 0, 2, 0),
            CreatePreviewBlock(gridManager, 2, 3, 1),
            CreatePreviewBlock(gridManager, 5, 3, 4)
        };

        gridManager.SetNextRowData(previewRow);
        gridManager.SetAllPreviewVisualsAlpha(0f);
        CreateTutorialPreviewVisuals(gridManager);
    }

    private GridManager.BlockData CreatePreviewBlock(GridManager gridManager, int x, int width, int normalGemIndex)
    {
        GridManager.BlockData data = gridManager.CreateSingleCellBlockData(
            x,
            BlockType.Normal,
            normalGemIndex
        );
        data.width = width;
        return data;
    }

    private void CreateTutorialPreviewVisuals(GridManager gridManager)
    {
        DestroyTutorialPreviewVisuals();

        int[] widths = { 2, 3, 4, 3 };
        int[] gemIndices = { 0, 1, 2, 4 };
        const float visualScaleX = 0.55f;
        const float visualScaleY = 0.50f;
        const float gap = 0.30f;

        float totalWidth = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            totalWidth += widths[i] * visualScaleX;
        }
        totalWidth += gap * (widths.Length - 1);

        float cursor = (gridManager.width - totalWidth) * 0.5f;

        for (int i = 0; i < widths.Length; i++)
        {
            GridManager.BlockData data = gridManager.CreateSingleCellBlockData(0, BlockType.Normal, gemIndices[i]);
            data.width = widths[i];

            float visualWidth = widths[i] * visualScaleX;
            Vector3 position = new Vector3(cursor + visualWidth * 0.5f, gridManager.previewYPosition, 0f);
            Block previewBlock = Instantiate(gridManager.blockPrefab, position, Quaternion.identity);
            previewBlock.gameObject.name = $"TutorialPreviewBlock_{i + 1}";
            previewBlock.enabled = false;
            previewBlock.SetVisual(data.visualSprite, data.color, data.width);
            previewBlock.transform.localScale = new Vector3(visualScaleX, visualScaleY, 1f);

            if (previewBlock.TryGetComponent<Collider2D>(out Collider2D collider))
                collider.enabled = i == 0;

            SetPreviewObjectAlpha(previewBlock.gameObject, i == 0 ? 1f : passivePreviewAlpha);
            tutorialPreviewVisuals.Add(previewBlock.gameObject);
            if (i == 0)
            {
                previewBlock.x = targetX;
                previewBlock.y = targetY;
                previewBlock.width = data.width;
                activePreviewBlock = previewBlock;
                activeBlock = previewBlock;
                activePreviewStartPosition = previewBlock.transform.position;
                activePreviewStartScale = previewBlock.transform.localScale;
            }

            cursor += visualWidth + gap;
        }
    }

    private void SetPreviewObjectAlpha(GameObject previewObject, float alpha)
    {
        if (previewObject == null)
            return;

        SpriteRenderer[] renderers = previewObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private void DestroyTutorialPreviewVisuals()
    {
        for (int i = 0; i < tutorialPreviewVisuals.Count; i++)
        {
            if (tutorialPreviewVisuals[i] != null)
                Destroy(tutorialPreviewVisuals[i]);
        }

        tutorialPreviewVisuals.Clear();
        activePreviewBlock = null;
    }

    private IEnumerator IntroRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        if (!IsRunning)
            yield break;

        EnsureTutorialUI();
        if (tutorialRoot == null)
            yield break;

        tutorialRoot.gameObject.SetActive(true);

        yield return FadeCanvasGroup(dimGroup, 0f, dimAlpha, hintFadeDuration, true);
        yield return FadeCanvasGroup(hintGroup, 0f, 1f, hintFadeDuration, true);
        yield return new WaitForSeconds(hintVisibleDuration);
        yield return MoveHintUp();

        if (!userStartedDragging)
            ghostRoutine = StartCoroutine(GhostHintRoutine());
    }

    private IEnumerator MoveHintUp()
    {
        if (hintRect == null)
            yield break;

        Vector2 startPosition = hintRect.anchoredPosition;
        Vector2 targetPosition = new Vector2(0f, 430f);
        float elapsed = 0f;
        const float duration = 0.25f;

        while (elapsed < duration)
        {
            if (!IsRunning)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            hintRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            hintGroup.alpha = Mathf.Lerp(1f, 0.65f, t);
            yield return null;
        }
    }

    private IEnumerator GhostHintRoutine()
    {
        yield return new WaitForSeconds(ghostStartDelay);

        if (!userStartedDragging)
            yield return PlayGhostHint();

        yield return new WaitForSeconds(ghostRepeatDelay);

        if (!userStartedDragging)
            yield return PlayGhostHint();
    }

    private IEnumerator PlayGhostHint()
    {
        if (activeBlock == null || grid == null)
            yield break;

        DestroyGhostBlock();

        Block sourceBlock = activePreviewBlock != null ? activePreviewBlock : activeBlock;
        SpriteRenderer sourceRenderer = sourceBlock.GetComponent<SpriteRenderer>();
        if (sourceRenderer == null)
            yield break;

        ghostObject = new GameObject("TutorialGhostHint");
        SpriteRenderer ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = sourceRenderer.sprite;
        ghostRenderer.drawMode = sourceRenderer.drawMode;
        ghostRenderer.size = sourceRenderer.size;
        ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = sourceRenderer.sortingOrder + 20;
        ghostObject.transform.localScale = sourceBlock.transform.localScale;

        Vector3 startPosition = GetActivePreviewWorldPosition();
        Vector3 finalTarget = GetTutorialTargetWorldPosition();
        Vector3 ghostTarget = Vector3.Lerp(startPosition, finalTarget, ghostTravelPercent);
        ghostObject.transform.position = startPosition;

        SetGhostRenderersAlpha(ghostAlpha);

        float elapsed = 0f;
        while (elapsed < ghostMoveDuration)
        {
            if (!IsRunning || userStartedDragging || ghostObject == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / ghostMoveDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            ghostObject.transform.position = Vector3.Lerp(startPosition, ghostTarget, eased);
            SetGhostRenderersAlpha(Mathf.Lerp(ghostAlpha, 0f, eased));

            yield return null;
        }

        DestroyGhostBlock();
    }

    private Vector3 GetActivePreviewWorldPosition()
    {
        if (tutorialPreviewVisuals.Count > 0 && tutorialPreviewVisuals[0] != null)
            return tutorialPreviewVisuals[0].transform.position;

        return activeBlock != null ? activeBlock.transform.position : Vector3.zero;
    }

    private Vector3 GetTutorialTargetWorldPosition()
    {
        int blockWidth = activeBlock != null ? activeBlock.width : 2;
        return new Vector3(targetX + (blockWidth - 1) * 0.5f, targetY, 0f);
    }

    private bool IsTutorialTargetEmpty(int blockWidth)
    {
        if (grid == null || grid.gridArray == null)
            return false;

        if (targetX < 0 || targetY < 0 || targetY >= grid.height || targetX + blockWidth > grid.width)
            return false;

        for (int i = 0; i < blockWidth; i++)
        {
            if (grid.gridArray[targetX + i, targetY] != null)
                return false;
        }

        return true;
    }

    private void SetGhostRenderersAlpha(float alpha)
    {
        if (ghostObject == null)
            return;

        SpriteRenderer[] renderers = ghostObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
            renderer.sortingOrder = 25;
        }
    }

    private void StopGhostHint()
    {
        if (ghostRoutine != null)
        {
            StopCoroutine(ghostRoutine);
            ghostRoutine = null;
        }

        DestroyGhostBlock();
    }

    private void DestroyGhostBlock()
    {
        if (ghostObject == null)
            return;

        Destroy(ghostObject);
        ghostObject = null;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration, bool cancelWhenTutorialEnds)
    {
        if (group == null)
            yield break;

        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            if (cancelWhenTutorialEnds && !IsRunning)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }

    private void EnsureTutorialUI()
    {
        if (tutorialRoot != null)
            return;

        Canvas hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
            return;

        tutorialRoot = CreateRoot(hudCanvas.transform);
        dimGroup = CreateDim(tutorialRoot);
        hintGroup = CreateText(tutorialRoot, "TutorialHintText", "Blokları sağa ve sola sürükle.", new Vector2(0f, 120f), 50f, out hintRect);
        successGroup = CreateText(tutorialRoot, "TutorialSuccessText", "Harika!", new Vector2(0f, 180f), 72f, out _);

        dimGroup.alpha = 0f;
        hintGroup.alpha = 0f;
        successGroup.alpha = 0f;
        tutorialRoot.gameObject.SetActive(false);
    }

    private RectTransform CreateRoot(Transform parent)
    {
        GameObject root = new GameObject("TutorialRoot", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        return rect;
    }

    private CanvasGroup CreateDim(RectTransform parent)
    {
        GameObject dimObject = new GameObject("TutorialDim", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform rect = dimObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = dimObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        CanvasGroup group = dimObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        return group;
    }

    private CanvasGroup CreateText(
        RectTransform parent,
        string objectName,
        string text,
        Vector2 anchoredPosition,
        float fontSize,
        out RectTransform rect
    )
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 150f);
        rect.anchoredPosition = anchoredPosition;

        CanvasGroup group = textObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;

        if (tutorialFont != null)
            label.font = tutorialFont;

        return group;
    }

    private Canvas FindHudCanvas()
    {
        GameObject hudCanvasObject = GameObject.Find("HUDCanvas");
        if (hudCanvasObject != null)
            return hudCanvasObject.GetComponent<Canvas>();

        return FindFirstObjectByType<Canvas>();
    }

    private void HideTutorialUI()
    {
        StopGhostHint();
        DestroyTutorialPreviewVisuals();

        if (hintGroup != null)
            hintGroup.alpha = 0f;

        if (successGroup != null)
            successGroup.alpha = 0f;

        if (dimGroup != null)
            dimGroup.alpha = 0f;

        if (tutorialRoot != null)
            tutorialRoot.gameObject.SetActive(false);
    }
}
