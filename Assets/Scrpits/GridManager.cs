using UnityEngine;
using System.Collections; // Coroutine için şart
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum GameState { IDLE, MOVING, FALLING, CHECKING, SPAWNING }


public class GridManager : MonoBehaviour
{
    private static readonly bool TutorialBoardOverrideEnabled = false;
    private const int PreviewSortingOrder = -5;
    private const float SpecialRowClearGravityStartDelay = 0.2f;
    private const float NormalRowClearGravityStartDelay = 0f;
    private const float DefaultRowClearCrunchDuration = 0.15f;
    private const float NormalRowClearCrunchDuration = 0.09f;
    private const float PushGravityOverlapPushProgressThreshold = 0.75f;
    private static readonly AnimationCurve GravityMotionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 1.8f, 1.8f),
        new Keyframe(0.12f, 0.23f, 1.55f, 1.25f),
        new Keyframe(0.55f, 0.72f, 0.95f, 0.75f),
        new Keyframe(0.85f, 0.94f, 0.55f, 0.35f),
        new Keyframe(1f, 1f, 0.15f, 0.15f));
    private const int DangerSafeEmptyRows = 2;
    private const float BoardDangerAlarmMinAlpha = 0.42f;
    private const float BoardDangerAlarmMaxAlpha = 1.00f;
    private const float BoardDangerPulseDuration = 1.15f;
    private static readonly Color ForgeGameOverEnergyTint = new Color(0.34f, 0.38f, 0.38f, 0.34f);

    [Header("Gravity")]
    [SerializeField] private float gravityBaseDuration = 0.1336f;
    [InspectorName("Gravity Additional Duration")]
    [SerializeField] private float gravityAdditionalCellDuration = 0.0371f;
    [SerializeField] private float gravityMaxDuration = 0.2544f;
    [Header("Preview")]
    [SerializeField] private float previewVisualYOffset = -0.25f;
    [SerializeField] private float previewVisualScale = 0.93f;
    [SerializeField] private float previewHorizontalCompression = 0.93f;
    [Header("References")]
    [SerializeField] private ForgeTeleportController forgeTeleportController;

    [System.Serializable]
    public struct LengthSpriteSet
    {
        public Sprite small;
        public Sprite medium;
        public Sprite longSprite;

        public Sprite GetSpriteForLength(int logicalLength, Sprite fallback)
        {
            switch (GetSizeGroupForLength(logicalLength))
            {
                case BlockVisualSizeGroup.Small:
                    return small != null ? small : fallback;
                case BlockVisualSizeGroup.Medium:
                    return medium != null ? medium : fallback;
                case BlockVisualSizeGroup.Long:
                    return longSprite != null ? longSprite : fallback;
                default:
                    return fallback;
            }
        }
    }

    private enum BlockVisualSizeGroup
    {
        Small,
        Medium,
        Long
    }

    private static BlockVisualSizeGroup GetSizeGroupForLength(int logicalLength)
    {
        if (logicalLength <= 1)
            return BlockVisualSizeGroup.Small;

        if (logicalLength == 2)
            return BlockVisualSizeGroup.Medium;

        return BlockVisualSizeGroup.Long;
    }

    [System.Serializable]
    public struct GemVisual
    {
        public Sprite sprite;
        public LengthSpriteSet lengthSprites;
        public Color particleColor; // O mücevher patladığında çıkacak renk
    }

    [Header("Mücevher Koleksiyonu")]
    public GemVisual[] normalGems; // 1, 2, 3, 7, 8. sıradaki renkli taşlar
    public Sprite rockSprite;      // 2. sıradaki gri taş
    [Header("Rock Length Sprites")]
    public LengthSpriteSet rockLengthSprites;
    public Sprite iceSprite;       // 4. sıradaki buz
    [Header("Ice Length Sprites")]
    public LengthSpriteSet iceLengthSprites;
    [Header("Chain Length Sprites")]
    [SerializeField] private LengthSpriteSet chainIntactLengthSprites;
    [SerializeField] private LengthSpriteSet chainDamagedLengthSprites;
    public Sprite lavaSprite;      // 6. sıradaki lavlı taş (Yeni mekanik!)
    [Header("Fire Block Sprites")]
    [SerializeField] private Sprite fireSprite_S;
    [SerializeField] private Sprite fireSprite_M;
    [SerializeField] private Sprite fireSprite_L;
    [Header("Slice Block Sprites")]
    [SerializeField] private Sprite sliceSprite_S;
    [SerializeField] private Sprite sliceSprite_M;
    [SerializeField] private Sprite sliceSprite_L;

    [Header("Collectibles")]
    [SerializeField] private CollectibleDatabase collectibleDatabase;

    [Range(0f, 1f)] public float classicFireChance = 0.03f;
    [Range(0f, 1f)] public float classicSliceChance = 0.02f;
    
    [Header("Renk Ayarları")]
    public Color[] blockColors; // Unity'den 5-6 renk ekle
    [Header("Grid Ayarları")]
    public int width = 8;
    public int height = 10;
    public float cellSize = 1f; // HATAYI DÜZELTEN SATIR
    public GameObject[] activeFogRows; // Sis objelerini tutacak dizi
    [SerializeField] private GameObject fogOverlayPrefab;
    [SerializeField] private Sprite fogLightSprite;
    [SerializeField] private Sprite fogDenseSprite;
    [SerializeField] [Range(0, 255)] private int fogLightAlpha = 200;
    [SerializeField] [Range(0, 255)] private int fogDenseAlpha = 200;
    [SerializeField] [Range(0.15f, 0.25f)] private float fogTransitionDuration = 0.20f;
    [SerializeField] private float fogRevealPerRowClear = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float fogRevealProgress;
    [SerializeField] [Range(0.01f, 0.5f)] private float fogRevealSoftness = 0.15f;
    [SerializeField] [Range(0f, 0.10f)] private float fogDistortionStrength = 0.035f;
    [SerializeField] [Range(0f, 1.50f)] private float fogDistortionSpeed = 0.55f;

    [Header("Efektler")]
    public FloatingText floatingTextPrefab;

    public GameObject explosionPrefab;
    [Header("Slice FX")]
    [Tooltip("Assign the separate Slice FX prefab from Assets/Prefabs/VFX/SliceCutFX.prefab")]
    [FormerlySerializedAs("sliceCutFxPrefab")]
    [SerializeField] private GameObject sliceFXPrefab;

    [SerializeField] private bool enableFreezeFrame = true;
    [SerializeField] private float freezeDuration = 0.04f;
    [SerializeField] private float freezeTimeScale = 0.08f;

    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeStrength = 0.08f;

    [Header("Fire Wave Clear Timing")]
    // Neutral cadence retained after each target batch; this replaces the removed FireArc wait.
    [SerializeField] private float fireWaveDelayBetweenBlocks = 0.045f;
    [SerializeField] private bool enableFireWaveClear = true;
    private Block heldFireSourceBlock;
    private bool isFireResolving = false;

    [Header("Fire Arc FX")]
    [SerializeField] private bool     enableFireArcFX     = true;
    [SerializeField] private Material fireArcMaterial;             // null → FireArcFX runtime fallback
    [SerializeField] private float    fireArcDuration     = 0.20f;
    [SerializeField] private int      fireArcSegments     = 12;
    [SerializeField] private float    fireArcDisplacement = 0.20f;
    [SerializeField] private float    fireArcStartWidth   = 0.18f;
    [SerializeField] private float    fireArcEndWidth     = 0.10f;

    [Header("Slice Trigger Feedback")]
    [SerializeField] private string sliceTriggerText = "SLICE!";
    [SerializeField] private Color sliceTriggerTextColor = new Color(0.5f, 0.9f, 1f);
    [SerializeField] private float sliceTriggerTextSize = 8f;

    [SerializeField] private float sliceShakeDuration = 0.10f;
    [SerializeField] private float sliceShakeStrength = 0.08f;
    [SerializeField] private float sliceResolveDelay = 0.18f;
    [SerializeField] private float slicePreSplitDelay = 0.08f;
    [SerializeField] private float sliceSplitOffset = 0.12f;
    [SerializeField] private float sliceSplitMoveDuration = 0.12f;
    [SerializeField] private Vector3 sliceCutFxOffset = new Vector3(0f, 0f, -0.5f);
    [SerializeField] private Vector3 sliceCutFxScale = Vector3.one;
    [SerializeField] private float sliceCutFxLifetime = 0.35f;
    private bool warnedMissingSliceFX = false;

    [Header("Chain Break Feedback")]
    [SerializeField] private float chainBreakImpactPause = 0.16f;

    [SerializeField] private bool enableClassicDoubleRowSpawn = true;
    private BlockTestSpawner blockTestSpawner;

    // Sahnedeki tüm kanlı canlı blokları burada tutacağız
    public System.Collections.Generic.List<Block> activeBlocks = new System.Collections.Generic.List<Block>();
    public static GridManager Instance { get; private set; }

    public Block[,] gridArray;
    public Block blockPrefab;
    public CollectibleDatabase CollectibleDatabase => collectibleDatabase;
    public GameState currentState = GameState.IDLE;
    public bool IsBoardBusy =>
        isGameOver ||
        currentState != GameState.IDLE ||
        isResolvingNoMove ||
        isRunningDifficultyPush ||
        isSliceResolving ||
        isFireResolving ||
        activeSliceOperations > 0 ||
        AreBlocksMoving();
    private bool isResolvingNoMove = false;
    private bool isRunningDifficultyPush = false;
    private bool isSliceResolving = false;
    private bool isTrackingClassicPlayerResolution = false;
    private int pendingClassicClearedRowsForPush = 0;
    private bool chainBreakImpactPausePending = false;
    private bool isPushGravityOverlapAnimating = false;
    private Coroutine pushGravityOverlapAnimationRoutine;
    private readonly HashSet<Block> pushGravityOverlapAnimatedBlocks = new HashSet<Block>();
    private int activeSliceOperations = 0;
    private FogController fogController;
    private FirstTimeTutorial firstTimeTutorial;
    public GameObject cellPrefab; // Az önce oluşturduğumuz Square'i buraya sürükleyeceğiz
    public Color gridColor = new Color(1f, 1f, 1f, 0.1f); // Hafif transparan beyaz/gri

    private struct GravityPlanEntry
    {
        public Block block;
        public int sourceX;
        public int sourceY;
        public int targetY;
        public bool willMove;
        public bool isFixed;
    }

    private struct GravityAnimationEntry
    {
        public Block block;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public int fallenCells;
        public float duration;
    }

    private struct PushAnimationProgressEntry
    {
        public Block block;
        public Vector3 startPosition;
        public Vector3 targetPosition;
    }

    public bool isGameOver = false;
    public GameObject losePanel; // Unity'den atayacağımız panel
    [SerializeField] private GameObject noSpaceWarningPanel;
    [SerializeField] private float gameOverGreyWaveRowDelay = 0.045f;
    [SerializeField] private float noSpaceWarningDuration = 2f;
    [SerializeField] private UnityEngine.UI.Image boardDangerAlarmImage;
    private bool isBoardDangerActive = false;
    private float boardDangerPulseTimer = 0f;
    private readonly List<SpriteRendererGameOverState> gameOverPreviewRendererStates = new List<SpriteRendererGameOverState>();
    private readonly List<SpriteRendererGameOverState> gameOverForgeRendererStates = new List<SpriteRendererGameOverState>();
    private readonly List<BehaviourGameOverState> gameOverForgeBehaviourStates = new List<BehaviourGameOverState>();
    private readonly List<ParticleSystemGameOverState> gameOverForgeParticleStates = new List<ParticleSystemGameOverState>();
    private static Material sharedRuntimeGridGameOverGrayscaleMaterial;
    private Material gridGameOverGrayscaleMaterial;
    private bool isForgeGameOverVisualShutdownActive = false;

    [Header("Önizleme (Preview) Ayarları")]
    public float previewYPosition = -1.2f; // Gridin hemen altında duracağı Y koordinatı
    public float previewAlpha = 0.7f;      // Yarı şeffaflık oranı
    
    // Gelecek satırın "taslağını" tutacak veri yapısı
    [System.Serializable]
public struct BlockData
{
    public int x;
    public int width;
    public Color color;
    public Sprite visualSprite;
    public GemColor fireTargetColor;

    public BlockType blockType;

    public bool isFrozen;
    public bool isRock;
    public bool isChained;
}

private struct ClassicDifficultyProfile
{
    public float rockChance;
    public float frozenChance;
    public float chainedChance;
    public float fireChance;
    public float sliceChance;
    public int minBlockWidth;
    public int maxBlockWidth;
    public bool allowDoubleRowSpawn;
    public float doubleRowChance;
    public bool allowTripleRowSpawn;
    public float tripleRowChance;
}

    public System.Collections.Generic.List<BlockData> nextRowData = new System.Collections.Generic.List<BlockData>();
    private System.Collections.Generic.List<GameObject> previewVisuals = new System.Collections.Generic.List<GameObject>();
    //ÖN İZLEME HEADER BİTİMİ

private ClassicDifficultyProfile GetClassicDifficultyProfile(int level)
{
    ClassicDifficultyProfile profile = new ClassicDifficultyProfile();

    profile.rockChance = 0f;
    profile.frozenChance = 0f;
    profile.chainedChance = 0f;
    profile.minBlockWidth = 1;
    profile.maxBlockWidth = 4;

    profile.allowDoubleRowSpawn = false;
    profile.doubleRowChance = 0f;
    profile.allowTripleRowSpawn = false;
    profile.tripleRowChance = 0f;

    if (level >= 15)
    {
        profile.fireChance = 0.06f;
        profile.sliceChance = 0.04f;
        profile.frozenChance = 0.07f;
        profile.chainedChance = 0.02f;
        profile.rockChance = 0.08f;
        profile.allowDoubleRowSpawn = true;
        profile.doubleRowChance = 0.22f;
        profile.allowTripleRowSpawn = true;
        profile.tripleRowChance = 0.04f;
    }
    else if (level >= 10)
    {
        profile.fireChance = 0.05f;
        profile.sliceChance = 0.03f;
        profile.frozenChance = 0.06f;
        profile.chainedChance = 0.02f;
        profile.rockChance = 0.05f;
        profile.allowDoubleRowSpawn = true;
        profile.doubleRowChance = 0.15f;
    }
    else if (level >= 6)
    {
        profile.fireChance = 0.04f;
        profile.sliceChance = 0.03f;
        profile.frozenChance = 0.04f;
        profile.chainedChance = 0.02f;
        profile.rockChance = 0.03f;
        profile.allowDoubleRowSpawn = true;
        profile.doubleRowChance = 0.10f;
    }
    else if (level >= 5)
    {
        profile.fireChance = 0.03f;
        profile.sliceChance = 0.02f;
        profile.rockChance = 0.03f;
    }
    else if (level >= 3) 
    {
        profile.fireChance = 0.03f;
        profile.sliceChance = 0.02f;
    }

    return profile;
}

private int GetClassicScoreMultiplier(int level)
{
    if (level >= 15)
        return 8;

    if (level >= 10)
        return 4;

    if (level >= 5)
        return 2;

    return 1;
}

private int GetPerfectClearBonus(int level)
{
    if (level >= 13)
        return 800;

    if (level >= 10)
        return 400;

    if (level >= 7)
        return 200;

    if (level >= 4)
        return 150;

    return 100;
}
  
    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        blockTestSpawner = GetComponent<BlockTestSpawner>();
        ResolveForgeTeleportController();
        firstTimeTutorial = TutorialBoardOverrideEnabled
            ? FindAnyObjectByType<FirstTimeTutorial>()
            : null;
        gridArray = new Block[width, height];
    }


void Start()
    {
        ResetClassicPushResolutionState();

        GenerateBackgroundGrid();
        GenerateFog(); // <--- YENİ EKLENDİ (Oyuna başlarken sisi basar)

        // 1. Grid'i tertemiz hazırla
        gridArray = new Block[width, height];
        activeBlocks.Clear();

        // 2. Başlangıç tahtasını kur (debug spawner varsa onu kullan, yoksa normal akış)
        bool usedTutorialBoard =
            TutorialBoardOverrideEnabled &&
            firstTimeTutorial != null &&
            firstTimeTutorial.TryBuildInitialBoard(this);

        bool usedDebugBoard =
            !usedTutorialBoard &&
            blockTestSpawner != null &&
            blockTestSpawner.enabled &&
            blockTestSpawner.TryBuildInitialBoard(this);

        if (!usedTutorialBoard && !usedDebugBoard)
        {
            SetupInitialBoard(4);
        }

        SpawnInitialCollectibles();

        // 3. Durumu IDLE yapalım ki oyuncu dokunabilsin
        currentState = GameState.IDLE;

        // 4. Sistemi coroutine ile dürt
        StartCoroutine(InitialGravityCheck());
        
        // Skor tabelasını da hemen dürtelim
        if(ScoreManager.Instance != null) ScoreManager.Instance.UpdateScoreUI();
    }

private void Update()
{
    UpdateBoardDangerAlarmPulse();

    if (!CanResolveNoMoveSoftlock())
        return;

    if (!HasAnyLegalPlayerMove())
    {
        StartCoroutine(ResolveNoMoveRoutine());
    }
}

private bool CanResolveNoMoveSoftlock()
{
    if (isGameOver)
        return false;

    if (isResolvingNoMove)
        return false;

    if (currentState != GameState.IDLE)
        return false;

    if (IsBoardBusy)
        return false;

    return true;
}

private bool HasAnyLegalPlayerMove()
{
    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (block.isRock || block.isChained)
            continue;

        int y = block.y;

        // Can move left?
        int leftX = block.x - 1;
        if (leftX >= 0 && gridArray[leftX, y] == null)
            return true;

        // Can move right?
        int rightX = block.x + block.width;
        if (rightX < width && gridArray[rightX, y] == null)
            return true;
    }

    return false;
}

private IEnumerator ResolveNoMoveRoutine()
{
    isResolvingNoMove = true;

    yield return new WaitForSeconds(0.25f);

    bool canStillResolve =
        !isGameOver &&
        currentState == GameState.IDLE &&
        !isRunningDifficultyPush &&
        !isSliceResolving &&
        !isFireResolving &&
        activeSliceOperations <= 0 &&
        !AreBlocksMoving() &&
        !HasAnyLegalPlayerMove();

    if (canStillResolve)
    {
        ChangeState(GameState.SPAWNING);
        yield return StartCoroutine(PushBoardUpRoutine());

        if (!isGameOver && currentState == GameState.SPAWNING)
            ChangeState(GameState.IDLE);
    }

    isResolvingNoMove = false;
}


// Oyun başlarken tahtayı dolduran yeni ve temiz fonksiyon
void SetupInitialBoard(int startingRowCount)
    {
        for (int y = 0; y < startingRowCount; y++)
        {
            GenerateNextRowData(); 
            
            foreach (BlockData data in nextRowData)
            {
                SpawnConfiguredBlock(data, y);
            }
        }
        RebuildGridMemory();
        GenerateNextRowData();
    }

private void SpawnInitialCollectibles()
{
    if (ProgressManager.Instance == null ||
        ProgressManager.Instance.currentSelectedAdventureConfig == null ||
        collectibleDatabase == null)
    {
        return;
    }

    AdventureLevelConfig config = ProgressManager.Instance.currentSelectedAdventureConfig;
    if (config.objectives == null || config.objectives.Count == 0)
    {
        return;
    }

    List<Block> availableBlocks = GetCollectibleSpawnCandidates();

    for (int i = 0; i < config.objectives.Count; i++)
    {
        AdventureObjectiveDefinition objective = config.objectives[i];
        if (objective == null ||
            objective.action != AdventureObjectiveAction.CollectItem ||
            string.IsNullOrWhiteSpace(objective.collectibleId))
        {
            continue;
        }

        CollectibleDefinition collectible = collectibleDatabase.GetById(objective.collectibleId);
        if (collectible == null)
        {
            Debug.LogWarning($"Collectible spawn skipped. ID not found: {objective.collectibleId}");
            continue;
        }

        int spawnCount = Mathf.Min(Mathf.Max(0, objective.requiredAmount), availableBlocks.Count);
        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            int randomIndex = Random.Range(0, availableBlocks.Count);
            Block targetBlock = availableBlocks[randomIndex];
            availableBlocks.RemoveAt(randomIndex);

            if (targetBlock != null)
            {
                targetBlock.AssignCollectible(objective.collectibleId, collectible.icon, false);
            }
        }

        Debug.Log($"Spawned collectibles:\n{objective.collectibleId}\nCount:\n{spawnCount}");
    }
}

private List<Block> GetCollectibleSpawnCandidates()
{
    List<Block> candidates = new List<Block>();

    for (int i = 0; i < activeBlocks.Count; i++)
    {
        Block block = activeBlocks[i];
        if (CanSpawnCollectibleOnBlock(block))
        {
            candidates.Add(block);
        }
    }

    return candidates;
}

private bool CanSpawnCollectibleOnBlock(Block block)
{
    if (block == null || block.HasCollectible())
    {
        return false;
    }

    if (block.blockType != BlockType.Normal)
    {
        return false;
    }

    return !block.isRock &&
           !block.isFrozen &&
           !block.isChained;
}

public int NormalGemCount
{
    get
    {
        return normalGems != null ? normalGems.Length : 0;
    }
}

public BlockData CreateSingleCellBlockData(int x, BlockType blockType, int normalGemIndex, bool useRandomNormalGem = false)
{
    BlockData data = new BlockData();
    data.x = x;
    data.width = 1;
    data.blockType = blockType;

    ApplyNormalGemVisual(ref data, normalGemIndex, useRandomNormalGem);

    switch (blockType)
    {
        case BlockType.Rock:
            data.isRock = true;
            data.visualSprite = GetRockSpriteForLength(data.width);
            data.color = Color.gray;
            break;
        case BlockType.Ice:
            data.isFrozen = true;
            break;
        case BlockType.Chained:
            data.isChained = true;
            break;
        case BlockType.Fire:
            break;
        case BlockType.Slice:
            break;
    }

    return data;
}

public Block SpawnConfiguredBlock(BlockData data, int y, bool animateIntoPlace = false, float spawnYOffset = 0f)
{
    Vector3 spawnPos = new Vector3(
        data.x + (data.width - 1) * 0.5f,
        y + spawnYOffset,
        0f
    );

    Block newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
    newBlock.width = data.width;
    newBlock.x = data.x;
    newBlock.y = y;
    newBlock.blockType = data.blockType;
    newBlock.fireTargetColor = data.fireTargetColor;

    newBlock.SetVisual(GetVisualSpriteForBlockData(data), data.color, data.width);

    if (data.isRock) newBlock.SetRock(true);
    if (data.isFrozen) newBlock.SetFrozen(true, GetIceSpriteForLength(data.width));
    if (data.isChained) newBlock.SetChained(newBlock.width, GetChainIntactSpriteForLength(data.width), GetChainDamagedSpriteForLength(data.width));

    activeBlocks.Add(newBlock);

    if (animateIntoPlace)
        newBlock.MoveTo(newBlock.x, newBlock.y);

    return newBlock;
}

public void SetNextRowData(List<BlockData> rowData)
{
    nextRowData.Clear();

    if (rowData != null)
    {
        nextRowData.AddRange(rowData);
    }

    UpdatePreviewVisuals();
}

public void SetPreviewVisualAlpha(int previewIndex, float alpha)
{
    if (previewIndex < 0 || previewIndex >= previewVisuals.Count)
        return;

    SetPreviewVisualAlpha(previewVisuals[previewIndex], alpha);
}

public void SetAllPreviewVisualsAlpha(float alpha)
{
    for (int i = 0; i < previewVisuals.Count; i++)
    {
        SetPreviewVisualAlpha(previewVisuals[i], alpha);
    }
}

private void SetPreviewVisualAlpha(GameObject previewObject, float alpha)
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

public bool CanFitBlockAt(int x, int y, int blockWidth, Block ignoredBlock = null)
{
    if (gridArray == null)
        return false;

    if (y < 0 || y >= height)
        return false;

    if (x < 0 || x + blockWidth > width)
        return false;

    for (int i = 0; i < blockWidth; i++)
    {
        Block occupyingBlock = gridArray[x + i, y];
        if (occupyingBlock != null && occupyingBlock != ignoredBlock)
            return false;
    }

    return true;
}

private void ApplyNormalGemVisual(ref BlockData data, int normalGemIndex, bool useRandomNormalGem)
{
    if (normalGems == null || normalGems.Length == 0)
    {
        Debug.LogWarning("GridManager: Normal Gems listesi boş! Lütfen Inspector'dan doldur.");
        data.color = Color.white;
        return;
    }

    int resolvedIndex = useRandomNormalGem
        ? Random.Range(0, normalGems.Length)
        : Mathf.Clamp(normalGemIndex, 0, normalGems.Length - 1);

    data.visualSprite = normalGems[resolvedIndex].sprite;
    data.color = normalGems[resolvedIndex].particleColor;
    data.fireTargetColor = ResolveGemColor(data.color);
}

public Sprite GetVisualSpriteForBlockData(BlockData data)
{
    if (data.isRock || data.blockType == BlockType.Rock)
        return GetRockSpriteForLength(data.width);

    if (data.isFrozen || data.blockType == BlockType.Ice)
        return GetNormalGemSpriteForColorAndLength(data.color, data.width, data.visualSprite);

    if (data.isChained || data.blockType == BlockType.Chained)
        return GetNormalGemSpriteForColorAndLength(data.color, data.width, data.visualSprite);

    if (data.blockType == BlockType.Fire)
        return GetSpecialBlockSprite(BlockType.Fire, data.width) ??
               FireV2SpriteLibrary.GetFireBaseSprite() ??
               data.visualSprite;

    if (data.blockType == BlockType.Slice)
        return GetSpecialBlockSprite(BlockType.Slice, data.width) ??
               data.visualSprite;

    if (data.blockType != BlockType.Normal)
        return data.visualSprite;

    return GetNormalGemSpriteForColorAndLength(data.color, data.width, data.visualSprite);
}

public Sprite GetSpecialBlockSprite(BlockType type, int blockWidth)
{
    switch (type)
    {
        case BlockType.Fire:
            return GetFireSprite(blockWidth);

        case BlockType.Slice:
            return GetSliceSprite(blockWidth);

        default:
            return null;
    }
}

private Sprite GetFireSprite(int blockWidth)
{
    if (blockWidth <= 1)
        return fireSprite_S;

    if (blockWidth == 2)
        return fireSprite_M;

    return fireSprite_L;
}

private Sprite GetSliceSprite(int blockWidth)
{
    if (blockWidth <= 1)
        return sliceSprite_S;

    if (blockWidth == 2)
        return sliceSprite_M;

    return sliceSprite_L;
}

public Sprite GetRockSpriteForLength(int logicalLength)
{
    return rockLengthSprites.GetSpriteForLength(logicalLength, rockSprite);
}

public Sprite GetIceSpriteForLength(int logicalLength)
{
    return iceLengthSprites.GetSpriteForLength(logicalLength, iceSprite);
}

public Sprite GetChainIntactSpriteForLength(int logicalLength)
{
    return chainIntactLengthSprites.GetSpriteForLength(logicalLength, null);
}

public Sprite GetChainDamagedSpriteForLength(int logicalLength)
{
    return chainDamagedLengthSprites.GetSpriteForLength(logicalLength, null);
}

public Sprite GetNormalGemSpriteForLength(int normalGemIndex, int logicalLength, Sprite fallback = null)
{
    if (normalGems == null || normalGems.Length == 0)
        return fallback;

    int resolvedIndex = Mathf.Clamp(normalGemIndex, 0, normalGems.Length - 1);
    Sprite resolvedFallback = fallback != null ? fallback : normalGems[resolvedIndex].sprite;

    return normalGems[resolvedIndex].lengthSprites.GetSpriteForLength(logicalLength, resolvedFallback);
}

private Sprite GetNormalGemSpriteForColorAndLength(Color color, int logicalLength, Sprite fallback)
{
    if (normalGems == null || normalGems.Length == 0)
        return fallback;

    for (int i = 0; i < normalGems.Length; i++)
    {
        if (ApproximatelySameColor(normalGems[i].particleColor, color))
            return GetNormalGemSpriteForLength(i, logicalLength, fallback);
    }

    return fallback;
}

private static bool ApproximatelySameColor(Color a, Color b)
{
    return Mathf.Approximately(a.r, b.r) &&
           Mathf.Approximately(a.g, b.g) &&
           Mathf.Approximately(a.b, b.b) &&
           Mathf.Approximately(a.a, b.a);
}

public Color GetColorForGemColor(GemColor targetColor)
{
    if (normalGems != null)
    {
        for (int i = 0; i < normalGems.Length; i++)
        {
            if (ResolveGemColor(normalGems[i].particleColor) == targetColor)
                return normalGems[i].particleColor;
        }
    }

    Debug.LogWarning($"GridManager: No normal gem color is configured for Fire target {targetColor}.");
    return Color.clear;
}

private static GemColor ResolveGemColor(Color color)
{
    Color.RGBToHSV(color, out float hue, out _, out _);

    if (hue < 0.05f || hue >= 0.95f)
        return GemColor.Red;

    if (hue < 0.20f)
        return GemColor.Yellow;

    if (hue < 0.48f)
        return GemColor.Green;

    if (hue < 0.68f)
        return GemColor.Blue;

    return GemColor.Purple;
}


// -----------Kontrol fonksiyonu
void CheckGameOver()
{
    // En üst satırı (height - 1) kontrol et
    int topRowY = height - 1;

    for (int x = 0; x < width; x++)
    {
        if (gridArray[x, topRowY] != null)
        {
            TriggerGameOver();
            break;
        }
    }
}

public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        SetBoardDangerActive(false);
        ApplyGameOverForgeVisualShutdown();
        ResetClassicPushResolutionState();

        StartCoroutine(GameOverNoSpaceRoutine());
    }

private IEnumerator GameOverNoSpaceRoutine()
{
    yield return StartCoroutine(PlayGameOverGreyWaveRoutine());

    if (noSpaceWarningPanel != null)
    {
        noSpaceWarningPanel.SetActive(true);
        yield return new WaitForSeconds(noSpaceWarningDuration);
        noSpaceWarningPanel.SetActive(false);
    }

    // Kaybetme panelini aç
    if (losePanel != null) losePanel.SetActive(true);

    // Oyunu durdur
    Time.timeScale = 0;
}

private IEnumerator PlayGameOverGreyWaveRoutine()
{
    List<Block> blocks = new List<Block>();
    foreach (Block block in activeBlocks)
    {
        if (block != null)
        {
            block.SetHighlight(false);
            blocks.Add(block);
        }
    }

    blocks.Sort((a, b) =>
    {
        if (a.y != b.y)
            return b.y.CompareTo(a.y);

        return a.x.CompareTo(b.x);
    });

    int lastY = int.MinValue;
    foreach (Block block in blocks)
    {
        if (block == null)
            continue;

        if (lastY != int.MinValue && block.y != lastY)
        {
            yield return new WaitForSeconds(gameOverGreyWaveRowDelay);
        }

        block.SetGameOverGreyed(true);
        lastY = block.y;
    }
}

void GenerateBackgroundGrid()
{
    // Bir "Background" objesi oluşturalım ki Hierarchy kalabalıklaşmasın
    GameObject gridParent = new GameObject("BackgroundGrid");

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            Vector3 pos = GetCellWorldPosition(x, y);
            GameObject cell = Instantiate(cellPrefab, pos, Quaternion.identity);
            cell.name = $"Cell_{x}_{y}";
            cell.transform.SetParent(gridParent.transform);

            // Görünümü ayarla
            SpriteRenderer sr = cell.GetComponent<SpriteRenderer>();
            sr.color = gridColor;
            sr.sortingOrder = -1; // Blokların arkasında kalması için
            
            // Hücreleri hafif küçülterek (0.9 gibi) o aradaki boşluk hissini verebilirsin
            cell.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
        }
    }

    HideBoardDangerAlarm();
}
// Başlangıç için özel bir kontrol süreci
IEnumerator InitialGravityCheck()
{
    yield return new WaitForSeconds(0.1f); // Kısa bir süre bekleyelim
    
    ChangeState(GameState.FALLING);
    yield return StartCoroutine(ApplyGravityRoutine());
    
    ChangeState(GameState.CHECKING);
    yield return StartCoroutine(CheckAndClearRowsRoutine());
}

public void RebuildGridMemory()
{
    // 1. TÜM HAFIZAYI SIFIRLA (Tertemiz yap)
    for (int x = 0; x < width; x++) {
        for (int y = 0; y < height; y++) {
            gridArray[x, y] = null;
        }
    }

    // 2. SADECE LİSTEDEKİ GERÇEK BLOKLARI YERLEŞTİR
    foreach (Block b in activeBlocks) {
        for (int i = 0; i < b.width; i++) {
            int targetX = b.x + i;
            if (targetX >= 0 && targetX < width && b.y >= 0 && b.y < height) {
                gridArray[targetX, b.y] = b;
            }
        }
    }
}

private IEnumerator RebuildAndApplyGravityRoutine()
{
    while (isSliceResolving)
    {
        yield return null;
    }

    RebuildGridMemory();
    yield return StartCoroutine(ApplyGravityRoutine());
    RebuildGridMemory();
}



public IEnumerator PushBoardUpRoutine()
{
    if (ShouldUseClassicDifficultyPush())
    {
        yield return StartCoroutine(PushBoardUpByDifficultyRoutine());
        yield break;
    }

    // EMNİYET KİLİDİ: Oyun bittiyse asla yeni satır ekleme ve yukarı itme
    if (isGameOver) yield break;

    if (forgeTeleportController != null)
    {
        yield return StartCoroutine(forgeTeleportController.PlayDepartureRoutine(previewVisuals));
    }

    // 1. ÖNCE HER ŞEYİ AYNI ANDA YAP (Zıplama olmasın)
    // Mevcutları yukarı it
    List<PushAnimationProgressEntry> pushProgressEntries = new List<PushAnimationProgressEntry>(activeBlocks.Count);
    foreach (Block b in activeBlocks) 
    {
        Vector3 startPosition = b.transform.position;
        b.y += 1;
        b.MoveTo(b.x, b.y);
        pushProgressEntries.Add(new PushAnimationProgressEntry
        {
            block = b,
            startPosition = startPosition,
            targetPosition = new Vector3(
                b.x + (b.width - 1) * 0.5f,
                b.y,
                0f)
        });
    }
    
    // Hafızayı güncelle
    RebuildGridMemory();

    // HİÇ BEKLEMEDEN: Rastgele satır doğurma! Onun yerine önizlemedeki satırı oyuna al.
    List<Block> spawnedRowBlocks = SpawnRowFromData(0);

    if (forgeTeleportController != null)
    {
        forgeTeleportController.PrepareArrival(spawnedRowBlocks);
    }

    StartPushGravityOverlapExperiment(pushProgressEntries);

    // HEMEN ARDINDAN: Bir sonraki hamle için yeni bir önizleme (taslak) oluştur.
    GenerateNextRowData(); 

    if (forgeTeleportController != null)
    {
        forgeTeleportController.ClearCompletedDepartureState();
        yield return StartCoroutine(forgeTeleportController.PlayArrivalRoutine(spawnedRowBlocks));
    }

    // 2. ŞİMDİ OYUNCUYA SÜRE TANI (Hızı buradan kontrol et)
    // "Hızlı gibi" dediğin yer burası, bu süreyi artırabilirsin.
    yield return new WaitForSeconds(0.25f); // 0.1f yerine 0.4f veya 0.5f yaparak sakinleştiriyoruz.

    // --- YENİ: GAME OVER KONTROLÜ ---
    CheckGameOver();
    if (isGameOver)
    {
        ClearPushGravityOverlapExperiment();
        yield break;
    }
    // -------------------------------

    // 3. SONRA KONTROLLERİ YAP
    ChangeState(GameState.FALLING); 
    yield return StartCoroutine(WaitForPushGravityOverlapExperimentRoutine());
    yield return StartCoroutine(RebuildAndApplyGravityRoutine());
    
    ChangeState(GameState.CHECKING);
    yield return StartCoroutine(CheckAndClearRowsRoutine());
}

private void ResolveForgeTeleportController()
{
    if (forgeTeleportController != null)
        return;

    GameObject forgeRoot = GameObject.Find("MysticForgeRoot");
    if (forgeRoot != null)
        forgeTeleportController = forgeRoot.GetComponent<ForgeTeleportController>();
}

private bool ShouldUseClassicDifficultyPush()
{
    if (isRunningDifficultyPush)
        return false;

    if (!enableClassicDoubleRowSpawn)
        return false;

    if (isResolvingNoMove)
        return false;

    if (currentState != GameState.IDLE)
        return false;

    if (LevelManager.Instance != null && LevelManager.Instance.enabled)
        return false;

    return true;
}

private IEnumerator PushBoardUpByDifficultyRoutine()
{
    isRunningDifficultyPush = true;

    int level = ScoreManager.Instance != null ? ScoreManager.Instance.currentLevel : 1;
    int clearedRows = pendingClassicClearedRowsForPush;
    ResetClassicPushResolutionState();

    int pushCount = 1;
    int randomPushCount = 1;
    int minimumPushCount = GetClassicClearedRowsMinimumPushCount(level, clearedRows);

    if (enableClassicDoubleRowSpawn)
    {
        ClassicDifficultyProfile profile = GetClassicDifficultyProfile(level);
        float rowSpawnRoll = Random.value;
        float tripleRowChance = profile.allowTripleRowSpawn ? profile.tripleRowChance : 0f;
        float doubleRowChance = profile.allowDoubleRowSpawn ? profile.doubleRowChance : 0f;

        if (rowSpawnRoll < tripleRowChance)
        {
            randomPushCount = 3;
        }
        else if (rowSpawnRoll < tripleRowChance + doubleRowChance)
        {
            randomPushCount = 2;
            Debug.Log("DOUBLE ROW SPAWN!");
        }

        pushCount = Mathf.Max(randomPushCount, minimumPushCount);

    }

    for (int i = 0; i < pushCount; i++)
    {
        yield return StartCoroutine(PushBoardUpRoutine());

        if (isGameOver)
            break;

        yield return new WaitForSeconds(0.08f);
    }

    if (!isGameOver)
    {
        yield return StartCoroutine(RebuildAndApplyGravityRoutine());
    }

    isRunningDifficultyPush = false;
}

private static int GetClassicClearedRowsMinimumPushCount(int level, int clearedRows)
{
    if (level < 6 || clearedRows < 2)
        return 1;

    if (level >= 15 && clearedRows >= 3)
        return 3;

    return 2;
}

private void ResetClassicPushResolutionState()
{
    isTrackingClassicPlayerResolution = false;
    pendingClassicClearedRowsForPush = 0;
}

public void RestartGame()
{
    RestoreGameOverForgeVisualShutdown();
    Time.timeScale = 1f;
    isGameOver = false;
    SetBoardDangerActive(false);

    if (IsClassicRun())
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScoreAndLevel();

        ResetClassicRunState();
    }
    
    // Çalışan tüm Coroutine'leri durdur ki eski referanslara gitmesinler
    StopAllCoroutines(); 
    
    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}

public void ResetClassicRunState()
{
    isResolvingNoMove = false;
    isRunningDifficultyPush = false;
    ResetClassicPushResolutionState();
}

public bool IsClassicRun()
{
    return LevelManager.Instance == null ||
        !LevelManager.Instance.enabled ||
        LevelManager.Instance.currentLevel == null;
}

private List<GravityPlanEntry> BuildGravityPlan(out List<string> validationFailures)
{
    validationFailures = new List<string>();

    List<Block> fixedBlocks = new List<Block>();
    List<Block> movableBlocks = new List<Block>();
    HashSet<Block> seenBlocks = new HashSet<Block>();

    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (!seenBlocks.Add(block))
        {
            validationFailures.Add($"Duplicate active block reference: {block.name}");
            continue;
        }

        if (IsFixedGravityOccupant(block))
        {
            fixedBlocks.Add(block);
            continue;
        }

        if (IsBlockEligibleForGravityPlan(block))
        {
            movableBlocks.Add(block);
            continue;
        }

        fixedBlocks.Add(block);
    }

    Block[,] temporaryOccupancy = new Block[width, height];
    List<GravityPlanEntry> plan = new List<GravityPlanEntry>(fixedBlocks.Count + movableBlocks.Count);

    foreach (Block block in fixedBlocks)
    {
        GravityPlanEntry entry = CreateGravityPlanEntry(block, block.y, false, true);
        plan.Add(entry);

        if (!PlaceFootprint(temporaryOccupancy, block, block.x, block.y, validationFailures, "fixed"))
        {
            validationFailures.Add(
                $"Fixed occupant placement failed: block={GetGravityPlanBlockLabel(block)}, width={block.width}, source=({block.x},{block.y})");
        }
    }

    movableBlocks.Sort(CompareBlocksForGravityPlan);

    foreach (Block block in movableBlocks)
    {
        int targetY = block.y;

        while (targetY > 0 && CanOccupyFootprint(temporaryOccupancy, block, block.x, targetY - 1))
        {
            targetY--;
        }

        GravityPlanEntry entry = CreateGravityPlanEntry(block, targetY, targetY != block.y, false);
        plan.Add(entry);

        if (!PlaceFootprint(temporaryOccupancy, block, block.x, targetY, validationFailures, "movable"))
        {
            validationFailures.Add(
                $"Movable placement failed: block={GetGravityPlanBlockLabel(block)}, width={block.width}, source=({block.x},{block.y}), plannedTargetY={targetY}");
        }
    }

    return plan;
}

private GravityPlanEntry CreateGravityPlanEntry(Block block, int targetY, bool willMove, bool isFixed)
{
    return new GravityPlanEntry
    {
        block = block,
        sourceX = block.x,
        sourceY = block.y,
        targetY = targetY,
        willMove = willMove,
        isFixed = isFixed
    };
}

private bool IsFixedGravityOccupant(Block block)
{
    if (block == null)
        return false;

    if (block.isBeingDestroyed)
        return true;

    return block == heldFireSourceBlock && isFireResolving;
}

private bool IsBlockEligibleForGravityPlan(Block block)
{
    return block != null && !IsFixedGravityOccupant(block);
}

private int CompareBlocksForGravityPlan(Block a, Block b)
{
    if (a == b)
        return 0;

    if (a == null)
        return 1;

    if (b == null)
        return -1;

    int yComparison = a.y.CompareTo(b.y);
    if (yComparison != 0)
        return yComparison;

    int xComparison = a.x.CompareTo(b.x);
    if (xComparison != 0)
        return xComparison;

    return a.GetEntityId().CompareTo(b.GetEntityId());
}

private bool CanOccupyFootprint(Block[,] occupancy, Block block, int targetX, int targetY)
{
    if (occupancy == null || block == null)
        return false;

    if (targetY < 0 || targetY >= height)
        return false;

    if (targetX < 0 || targetX + block.width > width)
        return false;

    for (int i = 0; i < block.width; i++)
    {
        if (occupancy[targetX + i, targetY] != null)
            return false;
    }

    return true;
}

private bool PlaceFootprint(
    Block[,] occupancy,
    Block block,
    int targetX,
    int targetY,
    List<string> validationFailures,
    string placementPhase)
{
    if (!CanOccupyFootprint(occupancy, block, targetX, targetY))
    {
        if (validationFailures != null)
        {
            validationFailures.Add(
                $"Cannot occupy footprint during {placementPhase}: block={GetGravityPlanBlockLabel(block)}, width={(block != null ? block.width : -1)}, target=({targetX},{targetY})");
        }

        return false;
    }

    for (int i = 0; i < block.width; i++)
    {
        occupancy[targetX + i, targetY] = block;
    }

    return true;
}

private bool CommitGravityPlan(List<GravityPlanEntry> plan, List<string> validationFailures)
{
    if (!ValidateGravityPlanForCommit(plan, validationFailures))
        return false;

    for (int i = 0; i < plan.Count; i++)
    {
        GravityPlanEntry entry = plan[i];
        Block block = entry.block;

        if (block == null || entry.isFixed)
            continue;

        if (!activeBlocks.Contains(block) || block.isBeingDestroyed)
            continue;

        block.x = entry.sourceX;
        block.y = entry.targetY;
    }

    RebuildGridMemory();
    return true;
}

private bool ValidateGravityPlanForCommit(List<GravityPlanEntry> plan, List<string> validationFailures)
{
    if (validationFailures == null)
        validationFailures = new List<string>();

    if (plan == null)
    {
        validationFailures.Add("Gravity plan is null.");
        ReportGravityPlanValidationFailure("commit", validationFailures);
        return false;
    }

    for (int i = 0; i < plan.Count; i++)
    {
        GravityPlanEntry entry = plan[i];
        Block block = entry.block;

        if (block == null)
        {
            validationFailures.Add($"Gravity plan contains null block at entry {i}.");
            continue;
        }

        if (!activeBlocks.Contains(block))
        {
            validationFailures.Add($"Gravity plan stale block is no longer active: {GetGravityPlanBlockLabel(block)}.");
            continue;
        }

        if (block.isBeingDestroyed && !entry.isFixed)
            validationFailures.Add($"Moving gravity plan block is being destroyed: {GetGravityPlanBlockLabel(block)}.");

        if (entry.sourceX < 0 || entry.sourceX + block.width > width || entry.targetY < 0 || entry.targetY >= height)
        {
            validationFailures.Add(
                $"Gravity plan target out of bounds: block={GetGravityPlanBlockLabel(block)}, width={block.width}, target=({entry.sourceX},{entry.targetY})");
        }
    }

    if (validationFailures.Count > 0)
    {
        ReportGravityPlanValidationFailure("commit", validationFailures);
        return false;
    }

    return true;
}

private List<GravityAnimationEntry> BuildGravityAnimationEntries(
    List<GravityPlanEntry> plan,
    bool skipCompletedPushOverlapEntries = false)
{
    List<GravityAnimationEntry> animationEntries = new List<GravityAnimationEntry>();

    if (plan == null)
        return animationEntries;

    for (int i = 0; i < plan.Count; i++)
    {
        GravityPlanEntry entry = plan[i];
        Block block = entry.block;

        if (!entry.willMove || entry.isFixed)
            continue;

        if (block == null || !activeBlocks.Contains(block) || block.isBeingDestroyed)
            continue;

        Vector3 targetPosition = GetGravityTargetWorldPosition(block, entry.targetY);
        if (skipCompletedPushOverlapEntries &&
            pushGravityOverlapAnimatedBlocks.Contains(block) &&
            Vector3.Distance(block.transform.position, targetPosition) <= 0.01f)
        {
            continue;
        }

        int fallenCells = entry.sourceY - entry.targetY;
        animationEntries.Add(new GravityAnimationEntry
        {
            block = block,
            startPosition = block.transform.position,
            targetPosition = targetPosition,
            fallenCells = fallenCells,
            duration = GetGravityAnimationDuration(fallenCells)
        });
    }

    return animationEntries;
}

private IEnumerator AnimateGravityPlanRoutine(List<GravityAnimationEntry> animationEntries)
{
    if (animationEntries == null || animationEntries.Count == 0)
    {
        yield break;
    }

    float elapsed = 0f;
    float maxDuration = 0f;

    for (int i = 0; i < animationEntries.Count; i++)
    {
        maxDuration = Mathf.Max(maxDuration, animationEntries[i].duration);
    }

    while (elapsed < maxDuration)
    {
        elapsed += Time.deltaTime;

        for (int i = 0; i < animationEntries.Count; i++)
        {
            GravityAnimationEntry entry = animationEntries[i];
            Block block = entry.block;

            if (block == null || !activeBlocks.Contains(block) || block.isBeingDestroyed)
                continue;

            float t = entry.duration > 0f
                ? Mathf.Clamp01(elapsed / entry.duration)
                : 1f;
            float interpolation = EvaluateGravityEasing(t);
            block.transform.position = Vector3.LerpUnclamped(
                entry.startPosition,
                entry.targetPosition,
                interpolation);
        }

        yield return null;
    }

    SnapGravityAnimationEntriesToTarget(animationEntries);
}

private void SnapGravityAnimationEntriesToTarget(List<GravityAnimationEntry> animationEntries)
{
    if (animationEntries == null)
        return;

    for (int i = 0; i < animationEntries.Count; i++)
    {
        GravityAnimationEntry entry = animationEntries[i];
        Block block = entry.block;

        if (block == null || !activeBlocks.Contains(block) || block.isBeingDestroyed)
            continue;

        block.transform.position = entry.targetPosition;
        block.isMoving = false;
    }
}

private float GetGravityAnimationDuration(int fallenCells)
{
    float duration =
        gravityBaseDuration +
        Mathf.Max(0, fallenCells - 1) * gravityAdditionalCellDuration;

    return Mathf.Max(0.01f, Mathf.Min(duration, gravityMaxDuration));
}

private float EvaluateGravityEasing(float t)
{
    t = Mathf.Clamp01(t);
    if (GravityMotionCurve == null || GravityMotionCurve.length == 0)
        return t;

    return Mathf.Clamp01(GravityMotionCurve.Evaluate(t));
}

private IEnumerator AnimatePushGravityOverlapRoutine(List<GravityAnimationEntry> animationEntries)
{
    if (animationEntries == null || animationEntries.Count == 0)
    {
        yield break;
    }

    float elapsed = 0f;
    float maxDuration = 0f;

    for (int i = 0; i < animationEntries.Count; i++)
    {
        maxDuration = Mathf.Max(maxDuration, animationEntries[i].duration);
    }

    while (elapsed < maxDuration)
    {
        elapsed += Time.deltaTime;

        for (int i = 0; i < animationEntries.Count; i++)
        {
            GravityAnimationEntry entry = animationEntries[i];
            Block block = entry.block;

            if (block == null || !activeBlocks.Contains(block) || block.isBeingDestroyed)
                continue;

            float t = entry.duration > 0f
                ? Mathf.Clamp01(elapsed / entry.duration)
                : 1f;
            float interpolation = EvaluatePushGravityOverlapEasing(t);
            block.transform.position = Vector3.LerpUnclamped(
                entry.startPosition,
                entry.targetPosition,
                interpolation);
        }

        yield return null;
    }

    SnapGravityAnimationEntriesToTarget(animationEntries);
}

private float EvaluatePushGravityOverlapEasing(float t)
{
    t = Mathf.Clamp01(t);
    return t * t;
}

private Vector3 GetGravityTargetWorldPosition(Block block)
{
    return GetGravityTargetWorldPosition(block, block.y);
}

private Vector3 GetGravityTargetWorldPosition(Block block, int targetY)
{
    return new Vector3(
        block.x + (block.width - 1) * 0.5f,
        targetY,
        block.transform.position.z);
}

private void StartPushGravityOverlapExperiment(List<PushAnimationProgressEntry> pushProgressEntries)
{
    if (isGameOver)
        return;

    ClearPushGravityOverlapExperiment();

    List<string> validationFailures;
    List<GravityPlanEntry> gravityPlan = BuildGravityPlan(out validationFailures);

    if (validationFailures.Count > 0)
    {
        ReportGravityPlanValidationFailure("push-overlap-build", validationFailures);
        return;
    }

    List<GravityAnimationEntry> animationEntries = BuildGravityAnimationEntries(gravityPlan);
    if (animationEntries.Count == 0)
        return;

    for (int i = 0; i < animationEntries.Count; i++)
    {
        Block block = animationEntries[i].block;
        if (block != null)
            pushGravityOverlapAnimatedBlocks.Add(block);
    }

    pushGravityOverlapAnimationRoutine = StartCoroutine(
        PushGravityOverlapExperimentRoutine(animationEntries, pushProgressEntries));
}

private IEnumerator PushGravityOverlapExperimentRoutine(
    List<GravityAnimationEntry> animationEntries,
    List<PushAnimationProgressEntry> pushProgressEntries)
{
    isPushGravityOverlapAnimating = true;

    while (GetPushAnimationProgress(pushProgressEntries) < PushGravityOverlapPushProgressThreshold)
    {
        yield return null;
    }

    animationEntries = RebuildPushGravityOverlapStartPositions(animationEntries);

    for (int i = 0; i < animationEntries.Count; i++)
    {
        Block block = animationEntries[i].block;
        if (block != null)
            block.isMoving = false;
    }

    yield return StartCoroutine(AnimatePushGravityOverlapRoutine(animationEntries));

    isPushGravityOverlapAnimating = false;
    pushGravityOverlapAnimationRoutine = null;
}

private List<GravityAnimationEntry> RebuildPushGravityOverlapStartPositions(
    List<GravityAnimationEntry> animationEntries)
{
    if (animationEntries == null)
        return animationEntries;

    List<GravityAnimationEntry> updatedEntries =
        new List<GravityAnimationEntry>(animationEntries.Count);

    for (int i = 0; i < animationEntries.Count; i++)
    {
        GravityAnimationEntry entry = animationEntries[i];
        Block block = entry.block;

        if (block != null && activeBlocks.Contains(block) && !block.isBeingDestroyed)
            entry.startPosition = block.transform.position;

        updatedEntries.Add(entry);
    }

    return updatedEntries;
}

private float GetPushAnimationProgress(List<PushAnimationProgressEntry> pushProgressEntries)
{
    if (pushProgressEntries == null || pushProgressEntries.Count == 0)
        return 1f;

    float boardProgress = 1f;

    for (int i = 0; i < pushProgressEntries.Count; i++)
    {
        PushAnimationProgressEntry entry = pushProgressEntries[i];
        Block block = entry.block;

        if (block == null || !activeBlocks.Contains(block) || block.isBeingDestroyed)
            continue;

        Vector3 pushVector = entry.targetPosition - entry.startPosition;
        float pushDistanceSquared = pushVector.sqrMagnitude;
        if (pushDistanceSquared <= 0.0001f)
            continue;

        float blockProgress = Vector3.Dot(
            block.transform.position - entry.startPosition,
            pushVector) / pushDistanceSquared;
        boardProgress = Mathf.Min(boardProgress, Mathf.Clamp01(blockProgress));
    }

    return boardProgress;
}

private IEnumerator WaitForPushGravityOverlapExperimentRoutine()
{
    while (isPushGravityOverlapAnimating)
    {
        yield return null;
    }
}

private void ClearPushGravityOverlapExperiment()
{
    if (pushGravityOverlapAnimationRoutine != null)
    {
        StopCoroutine(pushGravityOverlapAnimationRoutine);
        pushGravityOverlapAnimationRoutine = null;
    }

    isPushGravityOverlapAnimating = false;
    pushGravityOverlapAnimatedBlocks.Clear();
}

private void ReportGravityPlanValidationFailure(string phase, List<string> validationFailures)
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    if (validationFailures == null || validationFailures.Count <= 0)
        return;

    Debug.LogWarning(
        $"Gravity planner {phase} validation found {validationFailures.Count} issue(s).\n" +
        string.Join("\n", validationFailures));
#endif
}

private void ValidateCurrentGravityOccupancy(List<string> validationFailures)
{
    if (validationFailures == null)
        return;

    Block[,] occupancy = new Block[width, height];

    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (block.width <= 0)
        {
            validationFailures.Add($"Invalid block width after gravity: block={GetGravityPlanBlockLabel(block)}, width={block.width}");
            continue;
        }

        if (block.y < 0 || block.y >= height || block.x < 0 || block.x + block.width > width)
        {
            validationFailures.Add(
                $"Out-of-bounds block after gravity: block={GetGravityPlanBlockLabel(block)}, width={block.width}, actual=({block.x},{block.y})");
            continue;
        }

        for (int i = 0; i < block.width; i++)
        {
            int cellX = block.x + i;
            Block occupant = occupancy[cellX, block.y];

            if (occupant != null && occupant != block)
            {
                validationFailures.Add(
                    $"Occupancy overlap after gravity: cell=({cellX},{block.y}), first={GetGravityPlanBlockLabel(occupant)}, second={GetGravityPlanBlockLabel(block)}");
                continue;
            }

            occupancy[cellX, block.y] = block;
        }
    }
}

private string GetGravityPlanBlockLabel(Block block)
{
    if (block == null)
        return "null";

    return $"{block.name}#{block.GetEntityId()}";
}

    // YERÇEKİMİ MOTORU
public IEnumerator ApplyGravityRoutine()
{
    while (isSliceResolving)
    {
        yield return null;
    }

    List<string> gravityPlanValidationFailures;
    List<GravityPlanEntry> gravityPlan = BuildGravityPlan(out gravityPlanValidationFailures);

    if (gravityPlanValidationFailures.Count > 0)
    {
        ReportGravityPlanValidationFailure("build", gravityPlanValidationFailures);
        ClearPushGravityOverlapExperiment();
        yield break;
    }

    if (!CommitGravityPlan(gravityPlan, gravityPlanValidationFailures))
    {
        ClearPushGravityOverlapExperiment();
        yield break;
    }

    bool skipCompletedPushOverlapEntries = pushGravityOverlapAnimatedBlocks.Count > 0;
    List<GravityAnimationEntry> animationEntries = BuildGravityAnimationEntries(
        gravityPlan,
        skipCompletedPushOverlapEntries);
    yield return StartCoroutine(AnimateGravityPlanRoutine(animationEntries));
    ClearPushGravityOverlapExperiment();

    List<string> occupancyValidationFailures = new List<string>();
    ValidateCurrentGravityOccupancy(occupancyValidationFailures);
    ReportGravityPlanValidationFailure("occupancy", occupancyValidationFailures);
}



public IEnumerator CheckAndClearRowsRoutine(bool isPlayerMove = false, int chainDepth = 0)
    {
        if (isPlayerMove && chainDepth == 0 && IsClassicRun())
        {
            isTrackingClassicPlayerResolution = true;
            pendingClassicClearedRowsForPush = 0;
        }

        // 1. EMNİYET KONTROLÜ VE PERFECT CLEAR (BONUS BURAYA TAŞINDI)
        if (activeBlocks == null || activeBlocks.Count == 0)
        {
            if (!isGameOver) 
            {
                int level = ScoreManager.Instance != null ? ScoreManager.Instance.currentLevel : 1;
                int perfectClearBonus = GetPerfectClearBonus(level);

                Debug.Log($"<color=yellow>PERFECT CLEAR! +{perfectClearBonus}</color>");

                if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(perfectClearBonus); 
                
                // Oyunu kilitten kurtarmak için otomatik olarak alttan yeni satır ver!
                yield return StartCoroutine(PushBoardUpRoutine());
            }
            else 
            {
                ChangeState(GameState.IDLE);
            }
            
            yield break; // Fonksiyonu burada keser
        }

        int clearedRowCount = 0; 
        bool rowClearUsedSpecialResolution = false;
        int lowestClearedY = -1; // YENİ: Yazının çıkacağı pozisyonu bulmak için

        for (int y = 0; y < height; y++)
        {
            if (IsRowFull(y))
            {
                if (lowestClearedY == -1)
                {
                    lowestClearedY = y; // Patlayan ilk satırın yerini kaydet
                }
                bool rowUsedSpecialResolution = ClearRow(y, out string specialResolutionTypes);
                rowClearUsedSpecialResolution |= rowUsedSpecialResolution;
                clearedRowCount++; 
                y--; 
            }
        }

        if (clearedRowCount > 0)
        {
            if (isTrackingClassicPlayerResolution && IsClassicRun())
            {
                pendingClassicClearedRowsForPush += clearedRowCount;
            }

            if (clearedRowCount >= 2)
            {
                StartCoroutine(FreezeFrameRoutine());
            }

            if (chainDepth >= 2)
            {
                StartCoroutine(CameraShakeRoutine(shakeDuration, shakeStrength));
            }

            if (isPlayerMove && ScoreManager.Instance != null) {
                ScoreManager.Instance.IncrementCombo();
                isPlayerMove = false; 
            }

            int level = ScoreManager.Instance != null ? ScoreManager.Instance.currentLevel : 1;
            int baseScore = 8 * clearedRowCount * clearedRowCount;
            int moveMultiplier = ScoreManager.Instance != null ? ScoreManager.Instance.comboMultiplier : 1;
            int chainMultiplier = chainDepth + 1;
            int levelMultiplier = IsClassicRun() ? GetClassicScoreMultiplier(level) : 1;
            int pointsToGive =
                baseScore
                * moveMultiplier
                * chainMultiplier
                * levelMultiplier; 
            
            if (ScoreManager.Instance != null) {
                ScoreManager.Instance.AddScore(pointsToGive);
                ScoreManager.Instance.AddClearedLines(clearedRowCount);
            }
            // === YENİ EKLENEN SATIRLAR ===
            if (LevelManager.Instance != null && LevelManager.Instance.enabled) {
                LevelManager.Instance.LinesCleared(clearedRowCount);
            }

            if (fogController != null)
                fogController.RevealRows(clearedRowCount, fogRevealPerRowClear);
            // =============================

            // === YENİ: EKRANDA UÇAN YAZILAR (FLOATING TEXT) ===
        if (floatingTextPrefab != null)
        {
            // Yazıyı board'un tam ortasında, patlayan satır hizasında çıkar
            float spawnX = (width - 1) / 2f; 
            Vector3 spawnPos = new Vector3(spawnX, lowestClearedY + (clearedRowCount / 2f), 0);
            
            // Puan Yazısı
            FloatingText pointText = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);
            pointText.SetText($"+{pointsToGive}", Color.yellow, 6f);

            // Eğer kombo varsa, kombo yazısını puanın biraz üstünde çıkar
            if (moveMultiplier > 1)
            {
                Vector3 comboPos = spawnPos + new Vector3(0, 1.2f, 0);
                FloatingText comboText = Instantiate(floatingTextPrefab, comboPos, Quaternion.identity);
                comboText.SetText($"{moveMultiplier}x COMBO!", new Color(1f, 0.4f, 0f), 8f); // Turuncu renk
            }

            if (chainMultiplier > 1)
            {
                Vector3 chainPos = spawnPos + new Vector3(0, 2.1f, 0);
                FloatingText chainText = Instantiate(floatingTextPrefab, chainPos, Quaternion.identity);
                chainText.SetText($"CHAIN x{chainMultiplier}!", Color.cyan, 8f);

                if (chainDepth >= 3)
                {
                    StartCoroutine(CameraShakeRoutine(
                        shakeDuration * 1.5f,
                        shakeStrength * 1.8f
                    ));
                }
            }
        }
        // ================================================

            while (isFireResolving)
            {
                yield return null;
            }

            while (isSliceResolving)
            {
                yield return null;
            }

            bool useSpecialRowClearDelay =
                rowClearUsedSpecialResolution ||
                chainBreakImpactPausePending;

            if (chainBreakImpactPausePending)
            {
                chainBreakImpactPausePending = false;
                yield return new WaitForSeconds(chainBreakImpactPause);
            }

            RebuildGridMemory();
            float gravityStartDelay = useSpecialRowClearDelay
                ? SpecialRowClearGravityStartDelay
                : NormalRowClearGravityStartDelay;
            if (gravityStartDelay > 0f)
            {
                yield return new WaitForSeconds(gravityStartDelay);
            }

            yield return StartCoroutine(RebuildAndApplyGravityRoutine());
            
            // Zincirleme reaksiyonları kontrol et
            yield return StartCoroutine(CheckAndClearRowsRoutine(false, chainDepth + 1));
        }
        else
        {
            // 3. KOMBO SIFIRLAMA
            if (isPlayerMove && ScoreManager.Instance != null) {
                ScoreManager.Instance.ResetCombo();
            }
            
            // Sadece IDLE yapıyoruz, Perfect Clear artık en üstte kontrol ediliyor.
            ChangeState(GameState.IDLE); 
        }
    }

private IEnumerator FreezeFrameRoutine()
{
    if (!enableFreezeFrame)
        yield break;

    float originalScale = Time.timeScale;

    Time.timeScale = freezeTimeScale;

    yield return new WaitForSecondsRealtime(freezeDuration);

    Time.timeScale = originalScale;
}

private IEnumerator CameraShakeRoutine(float duration, float strength)
{
    if (!enableCameraShake)
        yield break;

    Camera cam = Camera.main;

    if (cam == null)
        yield break;

    Vector3 originalPos = cam.transform.position;

    float timer = 0f;

    while (timer < duration)
    {
        timer += Time.unscaledDeltaTime;

        Vector2 offset = Random.insideUnitCircle * strength;

        cam.transform.position = originalPos + new Vector3(offset.x, offset.y, 0f);

        yield return null;
    }

    cam.transform.position = originalPos;
}

public void SetCurrentFireSource(Block fireBlock)
{
    if (fireBlock == null)
    {
        heldFireSourceBlock = null;
        return;
    }

    heldFireSourceBlock = fireBlock;
}

public void ShowSliceTriggerFeedback(Block target)
{
    if (target == null)
        return;

    if (floatingTextPrefab != null)
    {
        Vector3 textPos = target.transform.position + new Vector3(0f, 1.0f, 0f);
        FloatingText text = Instantiate(floatingTextPrefab, textPos, Quaternion.identity);
        text.SetText(sliceTriggerText, sliceTriggerTextColor, sliceTriggerTextSize);
    }

    StartCoroutine(CameraShakeRoutine(sliceShakeDuration, sliceShakeStrength));
}

bool IsRowFull(int y)
{
    for (int x = 0; x < width; x++)
    {
        // Eğer tek bir hücre bile null ise o satır dolmamıştır
        if (gridArray[x, y] == null) return false;
    }
    return true;
}

bool ClearRow(int y, out string specialResolutionTypes)
    {
        bool usedSpecialResolution = false;
        List<string> specialResolutionReasons = new List<string>();
        System.Collections.Generic.List<Block> blocksToDestroy = new System.Collections.Generic.List<Block>();
        System.Collections.Generic.List<Block> blocksToUnfreeze = new System.Collections.Generic.List<Block>();
        System.Collections.Generic.HashSet<Block> processedChainedBlocks = new System.Collections.Generic.HashSet<Block>();
        System.Collections.Generic.HashSet<Block> protectedFromRemovalThisClear = new System.Collections.Generic.HashSet<Block>();

        // 1. Satırdaki blokları ayır
        for (int x = 0; x < width; x++) {
            Block b = gridArray[x, y];
            if (b != null) {
                bool wasChainedAtClearStart = b.IsChained();

                if (wasChainedAtClearStart) {
                    usedSpecialResolution = true;
                    AddSpecialResolutionReason(specialResolutionReasons, "Chain");

                    if (!processedChainedBlocks.Contains(b))
                    {
                        if (b.BreakOneChain())
                        {
                            chainBreakImpactPausePending = true;
                        }

                        processedChainedBlocks.Add(b);
                        protectedFromRemovalThisClear.Add(b);
                    }

                    continue;
                }
                else if (protectedFromRemovalThisClear.Contains(b)) {
                    continue;
                }
                else if (b.isFrozen && !b.IsChained() && !blocksToUnfreeze.Contains(b)) {
                    usedSpecialResolution = true;
                    AddSpecialResolutionReason(specialResolutionReasons, "Ice");
                    blocksToUnfreeze.Add(b); // Sadece buzluysa buzu kırılacak
                }
                else if (!b.isFrozen && !b.IsChained() && !blocksToDestroy.Contains(b) && !blocksToUnfreeze.Contains(b)) {
                    if (b.blockType == BlockType.Fire)
                    {
                        usedSpecialResolution = true;
                        AddSpecialResolutionReason(specialResolutionReasons, "Fire");
                    }
                    else if (b.blockType == BlockType.Slice)
                    {
                        usedSpecialResolution = true;
                        AddSpecialResolutionReason(specialResolutionReasons, "Slice");
                    }

                    b.TriggerSpecial();
                    blocksToDestroy.Add(b); // Hiçbir şeyi yoksa patlayacak!
                }
            }
        }

            // 2. Etkileri Kır (Oyunda kalmaya devam ederler)
            foreach (Block b in blocksToUnfreeze) {
        b.SetFrozen(false);
        b.SetHighlight(false); // <--- BU SATIRI EKLE (Rengi ve boyutu normale döndürür)
    }

        // 3. Normal blokları patlat ve yok et
        foreach (Block b in blocksToDestroy)
            {
                float crunchDuration = usedSpecialResolution
                    ? DefaultRowClearCrunchDuration
                    : NormalRowClearCrunchDuration;
                SafeDestroyBlock(b, null, true, crunchDuration);
            }

        RebuildGridMemory(); 
        specialResolutionTypes = specialResolutionReasons.Count > 0
            ? string.Join("|", specialResolutionReasons)
            : "None";
        return usedSpecialResolution;
    }

private static void AddSpecialResolutionReason(List<string> reasons, string reason)
{
    if (!reasons.Contains(reason))
        reasons.Add(reason);
}

public bool AreBlocksMoving()
    {
        foreach (var b in gridArray) { if (b != null && b.isMoving) return true; }
        return false;
    }

public void ChangeState(GameState newState)
    {
        currentState = newState;

        if (newState == GameState.IDLE)
        {
            EvaluateBoardDangerState();
        }
    }

    // private void OnDrawGizmos()
    // {
    //     Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
    //     for (int x = 0; x <= width; x++)
    //         Gizmos.DrawLine(new Vector3(x - 0.5f, -0.5f, 0), new Vector3(x - 0.5f, height - 0.5f, 0));
    //     for (int y = 0; y <= height; y++)
    //         Gizmos.DrawLine(new Vector3(-0.5f, y - 0.5f, 0), new Vector3(width - 0.5f, y - 0.5f, 0));
    // }



public void UpdateBlockInGrid(Block b, int newX, int newY)
{
    b.x = newX;
    b.y = newY;
    RebuildGridMemory(); // Hafızayı baştan kur!
}

public Vector3 GetCellWorldPosition(int gridX, int gridY)
{
    return new Vector3(gridX * cellSize, gridY * cellSize, 0f);
}

private void EvaluateBoardDangerState()
{
    if (isGameOver)
    {
        SetBoardDangerActive(false);
        return;
    }

    int highestOccupiedRow = GetHighestOccupiedRow();
    bool shouldShowDanger =
        highestOccupiedRow >= 0 &&
        GetEmptyRowsAbove(highestOccupiedRow) <= DangerSafeEmptyRows;

    SetBoardDangerActive(shouldShowDanger);
}

private int GetHighestOccupiedRow()
{
    if (gridArray == null)
        return -1;

    for (int y = height - 1; y >= 0; y--)
    {
        for (int x = 0; x < width; x++)
        {
            if (gridArray[x, y] != null)
                return y;
        }
    }

    return -1;
}

private int GetEmptyRowsAbove(int highestOccupiedRow)
{
    return (height - 1) - highestOccupiedRow;
}

private void SetBoardDangerActive(bool active)
{
    if (isBoardDangerActive == active)
        return;

    isBoardDangerActive = active;
    boardDangerPulseTimer = 0f;

    if (active)
    {
        ShowBoardDangerAlarm();
    }
    else
    {
        HideBoardDangerAlarm();
    }
}

private void UpdateBoardDangerAlarmPulse()
{
    if (!isBoardDangerActive || boardDangerAlarmImage == null)
        return;

    boardDangerPulseTimer += Time.deltaTime;
    float pulsePhase = BoardDangerPulseDuration > 0f
        ? Mathf.PingPong(boardDangerPulseTimer, BoardDangerPulseDuration) / BoardDangerPulseDuration
        : 1f;
    float easedPulsePhase = Mathf.Sin(pulsePhase * Mathf.PI * 0.5f);
    float alpha = Mathf.Lerp(BoardDangerAlarmMinAlpha, BoardDangerAlarmMaxAlpha, easedPulsePhase);

    SetBoardDangerAlarmAlpha(alpha);
}

private void SetBoardDangerAlarmAlpha(float alpha)
{
    if (boardDangerAlarmImage == null)
        return;

    Color color = boardDangerAlarmImage.color;
    color.a = alpha;
    boardDangerAlarmImage.color = color;
}

private void ShowBoardDangerAlarm()
{
    if (boardDangerAlarmImage == null)
        return;

    boardDangerAlarmImage.gameObject.SetActive(true);
    SetBoardDangerAlarmAlpha(BoardDangerAlarmMaxAlpha);
}

private void HideBoardDangerAlarm()
{
    SetBoardDangerAlarmAlpha(0f);

    if (boardDangerAlarmImage != null)
        boardDangerAlarmImage.gameObject.SetActive(false);
}

private void ApplyGameOverForgeVisualShutdown()
{
    if (isForgeGameOverVisualShutdownActive)
        return;

    isForgeGameOverVisualShutdownActive = true;
    ApplyGameOverPreviewVisuals();
    ApplyGameOverForgeVisuals();
}

private void ApplyGameOverPreviewVisuals()
{
    Material grayscaleMaterial = GetGridGameOverGrayscaleMaterial();

    for (int i = 0; i < previewVisuals.Count; i++)
    {
        GameObject previewObject = previewVisuals[i];
        if (previewObject == null)
            continue;

        SpriteRenderer[] renderers = previewObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            SpriteRenderer renderer = renderers[rendererIndex];
            if (renderer == null)
                continue;

            CacheRendererState(renderer, gameOverPreviewRendererStates);
            ApplyPreviewGameOverRendererState(renderer, grayscaleMaterial);
        }
    }
}

private void ApplyPreviewGameOverRendererState(SpriteRenderer renderer, Material grayscaleMaterial)
{
    if (renderer == null)
        return;

    if (grayscaleMaterial != null)
        renderer.sharedMaterial = grayscaleMaterial;

    renderer.SetPropertyBlock(null);

    Color color = renderer.color;
    float alpha = color.a;
    color = Color.Lerp(color, Color.gray, 0.65f);
    color *= 0.72f;
    color.a = alpha;
    renderer.color = color;
}

private void ApplyGameOverForgeVisuals()
{
    Transform forgeRoot = GetForgeVisualRoot();
    if (forgeRoot == null)
        return;

    StopForgeParticles(forgeRoot);
    DisableForgeEnergyBehaviours(forgeRoot);
    MuteForgeEnergyRenderers(forgeRoot);
}

private Transform GetForgeVisualRoot()
{
    ResolveForgeTeleportController();

    if (forgeTeleportController != null)
        return forgeTeleportController.transform;

    return null;
}

private void StopForgeParticles(Transform forgeRoot)
{
    ParticleSystem[] particleSystems = forgeRoot.GetComponentsInChildren<ParticleSystem>(true);
    for (int i = 0; i < particleSystems.Length; i++)
    {
        ParticleSystem particleSystem = particleSystems[i];
        if (particleSystem == null)
            continue;

        gameOverForgeParticleStates.Add(new ParticleSystemGameOverState(particleSystem));
        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}

private void DisableForgeEnergyBehaviours(Transform forgeRoot)
{
    MonoBehaviour[] behaviours = forgeRoot.GetComponentsInChildren<MonoBehaviour>(true);
    for (int i = 0; i < behaviours.Length; i++)
    {
        MonoBehaviour behaviour = behaviours[i];
        if (!IsForgeEnergyBehaviour(behaviour))
            continue;

        gameOverForgeBehaviourStates.Add(new BehaviourGameOverState(behaviour));
        behaviour.enabled = false;
    }
}

private bool IsForgeEnergyBehaviour(MonoBehaviour behaviour)
{
    return behaviour is ForgeEnergyBreathing ||
        behaviour is ForgeEnergyWaveController;
}

private void MuteForgeEnergyRenderers(Transform forgeRoot)
{
    Material grayscaleMaterial = GetGridGameOverGrayscaleMaterial();
    SpriteRenderer[] renderers = forgeRoot.GetComponentsInChildren<SpriteRenderer>(true);

    for (int i = 0; i < renderers.Length; i++)
    {
        SpriteRenderer renderer = renderers[i];
        if (!ShouldMuteForgeRenderer(renderer))
            continue;

        CacheRendererState(renderer, gameOverForgeRendererStates);

        if (grayscaleMaterial != null)
            renderer.sharedMaterial = grayscaleMaterial;

        renderer.SetPropertyBlock(null);
        renderer.color = ForgeGameOverEnergyTint;
    }
}

private bool ShouldMuteForgeRenderer(SpriteRenderer renderer)
{
    if (renderer == null)
        return false;

    Transform rendererTransform = renderer.transform;
    if (HasForgeEnergyComponent(rendererTransform))
        return true;

    while (rendererTransform != null)
    {
        if (rendererTransform.name.Contains("Energy") ||
            rendererTransform.name.Contains("Flow"))
        {
            return true;
        }

        if (rendererTransform == GetForgeVisualRoot())
            break;

        rendererTransform = rendererTransform.parent;
    }

    return false;
}

private bool HasForgeEnergyComponent(Transform target)
{
    if (target == null)
        return false;

    return target.GetComponent<ForgeEnergyBreathing>() != null ||
        target.GetComponent<ForgeEnergyWaveController>() != null;
}

private void CacheRendererState(SpriteRenderer renderer, List<SpriteRendererGameOverState> states)
{
    if (renderer == null || states == null)
        return;

    for (int i = 0; i < states.Count; i++)
    {
        if (states[i].Renderer == renderer)
            return;
    }

    states.Add(new SpriteRendererGameOverState(renderer));
}

private Material GetGridGameOverGrayscaleMaterial()
{
    if (gridGameOverGrayscaleMaterial == null)
        gridGameOverGrayscaleMaterial = Resources.Load<Material>("M_GameOverGrayscale");

    if (gridGameOverGrayscaleMaterial == null)
    {
        if (sharedRuntimeGridGameOverGrayscaleMaterial == null)
        {
            Shader shader = Shader.Find("ARXON/Sprite Grayscale");
            if (shader != null)
            {
                sharedRuntimeGridGameOverGrayscaleMaterial = new Material(shader)
                {
                    name = "Runtime_GridGameOverGrayscale"
                };
            }
        }

        gridGameOverGrayscaleMaterial = sharedRuntimeGridGameOverGrayscaleMaterial;
    }

    return gridGameOverGrayscaleMaterial;
}

private void RestoreGameOverForgeVisualShutdown()
{
    if (!isForgeGameOverVisualShutdownActive)
        return;

    RestoreRendererStates(gameOverPreviewRendererStates);
    RestoreRendererStates(gameOverForgeRendererStates);
    RestoreParticleStates(gameOverForgeParticleStates);
    RestoreBehaviourStates(gameOverForgeBehaviourStates);

    gameOverPreviewRendererStates.Clear();
    gameOverForgeRendererStates.Clear();
    gameOverForgeParticleStates.Clear();
    gameOverForgeBehaviourStates.Clear();
    isForgeGameOverVisualShutdownActive = false;
}

private void RestoreRendererStates(List<SpriteRendererGameOverState> states)
{
    if (states == null)
        return;

    for (int i = 0; i < states.Count; i++)
    {
        states[i].Restore();
    }
}

private void RestoreParticleStates(List<ParticleSystemGameOverState> states)
{
    if (states == null)
        return;

    for (int i = 0; i < states.Count; i++)
    {
        states[i].Restore();
    }
}

private void RestoreBehaviourStates(List<BehaviourGameOverState> states)
{
    if (states == null)
        return;

    for (int i = 0; i < states.Count; i++)
    {
        states[i].Restore();
    }
}

private void OnDisable()
{
    RestoreGameOverForgeVisualShutdown();
    SetBoardDangerActive(false);
}

private void OnDestroy()
{
    RestoreGameOverForgeVisualShutdown();
}

private sealed class SpriteRendererGameOverState
{
    public SpriteRenderer Renderer { get; }

    private readonly Color color;
    private readonly Material sharedMaterial;
    private readonly bool enabled;
    private readonly MaterialPropertyBlock propertyBlock;

    public SpriteRendererGameOverState(SpriteRenderer renderer)
    {
        Renderer = renderer;

        if (renderer == null)
            return;

        color = renderer.color;
        sharedMaterial = renderer.sharedMaterial;
        enabled = renderer.enabled;
        propertyBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
    }

    public void Restore()
    {
        if (Renderer == null)
            return;

        Renderer.enabled = enabled;
        Renderer.sharedMaterial = sharedMaterial;
        Renderer.color = color;
        Renderer.SetPropertyBlock(propertyBlock);
    }
}

private sealed class BehaviourGameOverState
{
    private readonly MonoBehaviour behaviour;
    private readonly bool enabled;
    private readonly Transform transform;
    private readonly Vector3 localPosition;
    private readonly Vector3 localScale;

    public BehaviourGameOverState(MonoBehaviour behaviour)
    {
        this.behaviour = behaviour;

        if (behaviour == null)
            return;

        enabled = behaviour.enabled;
        transform = behaviour.transform;
        localPosition = transform.localPosition;
        localScale = transform.localScale;
    }

    public void Restore()
    {
        if (behaviour == null)
            return;

        if (transform != null)
        {
            transform.localPosition = localPosition;
            transform.localScale = localScale;
        }

        behaviour.enabled = enabled;
    }
}

private sealed class ParticleSystemGameOverState
{
    private readonly ParticleSystem particleSystem;
    private readonly bool wasPlaying;
    private readonly bool emissionEnabled;

    public ParticleSystemGameOverState(ParticleSystem particleSystem)
    {
        this.particleSystem = particleSystem;

        if (particleSystem == null)
            return;

        wasPlaying = particleSystem.isPlaying;
        emissionEnabled = particleSystem.emission.enabled;
    }

    public void Restore()
    {
        if (particleSystem == null)
            return;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = emissionEnabled;

        if (wasPlaying)
            particleSystem.Play(true);
        else
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}

public void GenerateFog()
{
    if (activeFogRows != null)
    {
        foreach (var fog in activeFogRows)
            if (fog != null)
                Destroy(fog);
    }

    activeFogRows = new GameObject[0];

    if (fogController == null)
        fogController = GetComponent<FogController>() ?? gameObject.AddComponent<FogController>();

    LevelData currentLevel =
        ProgressManager.Instance != null
        ? ProgressManager.Instance.currentSelectedLevel
        : null;

    FogDensity density = FogDensity.None;
    float coveragePercent = 0f;
    fogRevealProgress = 0f;

    if (currentLevel != null)
    {
        density = currentLevel.fogDensity;
        coveragePercent = Mathf.Clamp01(currentLevel.fogCoveragePercent);

        if (density == FogDensity.None && currentLevel.fogStartingRow >= 0)
        {
            density = FogDensity.Dense;
            coveragePercent = Mathf.Clamp01((height - currentLevel.fogStartingRow) / (float)height);
        }
    }

    fogController.Configure(
        transform,
        fogOverlayPrefab,
        width,
        height,
        density,
        coveragePercent,
        fogLightSprite,
        fogDenseSprite,
        fogLightAlpha,
        fogDenseAlpha,
        fogTransitionDuration,
        fogRevealProgress,
        fogRevealSoftness,
        fogDistortionStrength,
        fogDistortionSpeed);
}

public void ClearFogNearRow(int y)
{
}

private int CountBlocksByType(BlockType type)
{
    int count = 0;

    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (block.blockType == type)
            count++;
    }

    return count;
}

//---------------------ÖN İZLEME FONKSİYONLARI-----------------------

public void GenerateNextRowData()
{
        nextRowData.Clear();
        int currentX = 0;
        int blockCountInRow = 0;
        int level = ScoreManager.Instance != null ? ScoreManager.Instance.currentLevel : 1;
        bool isClassicMode =
            LevelManager.Instance == null ||
            !LevelManager.Instance.enabled ||
            LevelManager.Instance.currentLevel == null;
        int currentFireCount = CountBlocksByType(BlockType.Fire);
        int currentSliceCount = CountBlocksByType(BlockType.Slice);

        // --- ZORLUK (DIFFICULTY) DEĞERLERİNİ BELİRLE ---
        float currentGapChance = 0.4f;
        float currentT4 = 0.96f; // 4'lü blok şansı eşiği (1 - 0.04)
        float currentT3 = 0.85f;
        float currentT2 = 0.60f;
        float currentFreezeChance = 0f;
        float currentRockChance = 0f;
        float currentChainedChance = 0f;
        float currentFireChance = 0f;
        float currentSliceChance = 0f;
        bool useCustomSpawnRules = false;
        int customMinBlockSize = 1;
        int customMaxBlockSize = 4;

        // Hangi modda olduğumuzu soruyoruz:
        if (LevelManager.Instance != null && LevelManager.Instance.enabled && LevelManager.Instance.currentLevel != null)
        {
            // MACERA MODU: Verileri özel LevelData dosyasından çek
            LevelData data = LevelManager.Instance.currentLevel;
            currentGapChance = data.baseGapChance;
            
            currentT4 = 1f - data.largeBlockChance; // Eğer %10 dev blok dediysek eşik 0.90 olur.
            currentT3 = currentT4 - 0.15f; 
            currentT2 = currentT3 - 0.25f; 
            currentFreezeChance = data.frozenBlockChance;
            currentRockChance = data.rockBlockChance;
            currentChainedChance = data.chainedBlockChance;
            useCustomSpawnRules = data.useCustomSpawnRules;

            if (useCustomSpawnRules)
            {
                customMinBlockSize = Mathf.Max(1, data.minBlockSize);
                customMaxBlockSize = Mathf.Max(customMinBlockSize, data.maxBlockSize);
                currentFireChance = data.fireBlockChance;
                currentSliceChance = data.sliceBlockChance;
            }
        }
        else
        {
            // --- KLASİK MOD (Sonsuz) ---
            float diffFactor = Mathf.Clamp01((level - 1) / 20f);

            currentGapChance = Mathf.Lerp(0.4f, 0.1f, diffFactor);
            currentT4 = Mathf.Lerp(0.96f, 0.70f, diffFactor); 
            currentT3 = Mathf.Lerp(0.85f, 0.40f, diffFactor); 
            currentT2 = Mathf.Lerp(0.60f, 0.15f, diffFactor);

            ClassicDifficultyProfile profile = GetClassicDifficultyProfile(level);

            currentRockChance = profile.rockChance;
            currentFreezeChance = profile.frozenChance;
            currentChainedChance = profile.chainedChance;
            currentFireChance = profile.fireChance;
            currentSliceChance = profile.sliceChance;
        }

        // --- ŞİMDİ BLOKLARI ÜRET ---
        while (currentX < width)
        {
            // Satırda çok blok varsa boşluk bırakma ihtimalini bir tık düşür
            float gapChance = (blockCountInRow > 2) ? currentGapChance * 0.75f : currentGapChance;
            
            if (Random.value < gapChance)
            {
                currentX++;
                continue;
            }

            int bWidth = 1;

            if (useCustomSpawnRules)
            {
                int availableCells = width - currentX;

                if (availableCells < customMinBlockSize)
                    break;

                bWidth = Random.Range(customMinBlockSize, customMaxBlockSize + 1);

                if (bWidth > availableCells)
                    bWidth = availableCells;
            }
            else
            {
                float widthRoll = Random.value;
                
                if (widthRoll > currentT4) bWidth = 4;
                else if (widthRoll > currentT3) bWidth = 3;
                else if (widthRoll > currentT2) bWidth = 2;
                else bWidth = 1;                              

                if (currentX + bWidth > width) bWidth = width - currentX;
            }

            // --- GÖRSEL SEÇİM MANTIĞI ---
            BlockData newData = CreateSingleCellBlockData(currentX, BlockType.Normal, 0, true);
            newData.width = bWidth;
            bool canSpawnFire = !isClassicMode || currentFireCount < 1;
            bool canSpawnSlice = !isClassicMode || currentSliceCount < 1;

            // Eğer özel bir durum varsa (Kaya, Buz vb.) resmi değiştirelim
            newData.isRock = (Random.value < currentRockChance);
            if (newData.isRock)
            {
                if (isClassicMode)
                    Debug.Log("ROCK GENERATED at level " + level);

                newData.blockType = BlockType.Rock;
                newData.visualSprite = GetRockSpriteForLength(newData.width);
                newData.color = Color.gray;
            }
            if (!newData.isRock && !newData.isFrozen && !newData.isChained)
{
            float specialRoll = Random.value;

            if (canSpawnFire && specialRoll < currentFireChance)
            {
                newData.blockType = BlockType.Fire;
                currentFireCount++;
            }
            else if (canSpawnSlice && specialRoll < currentFireChance + currentSliceChance)
            {
                newData.blockType = BlockType.Slice;
                currentSliceCount++;
            }
}

            bool canApplyObstacle = newData.blockType == BlockType.Normal && !newData.isRock;

            newData.isChained = canApplyObstacle && (Random.value < currentChainedChance);
            if (newData.isChained)
            {
                newData.blockType = BlockType.Chained;
            }

            canApplyObstacle = newData.blockType == BlockType.Normal && !newData.isRock && !newData.isChained;

            newData.isFrozen = canApplyObstacle && (Random.value < currentFreezeChance);
            if (newData.isFrozen)
            {
                newData.blockType = BlockType.Ice;
            }


            
            nextRowData.Add(newData);

            currentX += bWidth;
            blockCountInRow++;
        }       
        
       if (blockCountInRow == 0)
       {
           int fallbackWidth = useCustomSpawnRules
               ? RollCustomBlockWidth(customMinBlockSize, customMaxBlockSize, width)
               : 1;

           if (fallbackWidth <= 0)
               fallbackWidth = useCustomSpawnRules ? Mathf.Clamp(customMinBlockSize, 1, width) : 1;

           int maxX = Mathf.Max(1, width - fallbackWidth + 1);
           int fallbackX = Random.Range(0, maxX);

           BlockData newData = CreateSingleCellBlockData(fallbackX, BlockType.Normal, 0, true);
           newData.width = fallbackWidth;
           bool canSpawnFire = !isClassicMode || currentFireCount < 1;
           bool canSpawnSlice = !isClassicMode || currentSliceCount < 1;

           newData.isRock = (Random.value < currentRockChance);
           if (newData.isRock)
           {
               if (isClassicMode)
                   Debug.Log("ROCK GENERATED at level " + level);

               newData.blockType = BlockType.Rock;
               newData.visualSprite = GetRockSpriteForLength(newData.width);
               newData.color = Color.gray;
           }

           if (!newData.isRock && !newData.isFrozen && !newData.isChained)
           {
               float specialRoll = Random.value;

               if (canSpawnFire && specialRoll < currentFireChance)
               {
                   newData.blockType = BlockType.Fire;
                   currentFireCount++;
               }
               else if (canSpawnSlice && specialRoll < currentFireChance + currentSliceChance)
               {
                   newData.blockType = BlockType.Slice;
                   currentSliceCount++;
               }
           }

           bool canApplyObstacle = newData.blockType == BlockType.Normal && !newData.isRock;

           newData.isChained = canApplyObstacle && (Random.value < currentChainedChance);
           if (newData.isChained)
           {
               newData.blockType = BlockType.Chained;
           }

           canApplyObstacle = newData.blockType == BlockType.Normal && !newData.isRock && !newData.isChained;

           newData.isFrozen = canApplyObstacle && (Random.value < currentFreezeChance);
           if (newData.isFrozen)
           {
               newData.blockType = BlockType.Ice;
           }

           nextRowData.Add(newData);
       }

        EnsureNextRowHasAtLeastOneGap(useCustomSpawnRules ? customMinBlockSize : 1);
        // Veri hesaplandı, şimdi görselleri çiz!
        UpdatePreviewVisuals();
}

private int RollCustomBlockWidth(int minBlockSize, int maxBlockSize, int availableCells)
{
    int minSize = Mathf.Max(1, minBlockSize);
    int maxSize = Mathf.Max(minSize, maxBlockSize);

    if (availableCells < minSize)
        return 0;

    int rolledSize = Random.Range(minSize, maxSize + 1);

    if (rolledSize > availableCells)
        rolledSize = availableCells;

    return rolledSize;
}

private void EnsureNextRowHasAtLeastOneGap(int minimumAllowedWidth = 1)
{
    if (nextRowData == null || nextRowData.Count == 0)
        return;

    bool[] occupied = new bool[width];

    foreach (BlockData data in nextRowData)
    {
        for (int i = 0; i < data.width; i++)
        {
            int cellX = data.x + i;

            if (cellX >= 0 && cellX < width)
                occupied[cellX] = true;
        }
    }

    bool isFull = true;

    for (int x = 0; x < width; x++)
    {
        if (!occupied[x])
        {
            isFull = false;
            break;
        }
    }

    if (!isFull)
        return;

    int randomIndex = Random.Range(0, nextRowData.Count);
    BlockData selectedData = nextRowData[randomIndex];

    if (selectedData.width > minimumAllowedWidth)
    {
        selectedData.width -= 1;
        nextRowData[randomIndex] = selectedData;
    }
    else
    {
        nextRowData.RemoveAt(randomIndex);
    }
}
private void UpdatePreviewVisuals()
{
    foreach (GameObject obj in previewVisuals) { Destroy(obj); }
    previewVisuals.Clear();

    foreach (BlockData data in nextRowData)
    {
        float previewCenterX = (width - 1) * 0.5f;
        float logicalBlockCenterX = data.x + (data.width - 1) * 0.5f;
        float visualX = previewCenterX + (logicalBlockCenterX - previewCenterX) * previewHorizontalCompression;
        Vector3 spawnPos = new Vector3(visualX, previewYPosition + previewVisualYOffset, 0);
        previewVisuals.Add(BuildDetailedBlockPreview(data, spawnPos));
    }
}

private GameObject BuildDetailedBlockPreview(BlockData data, Vector3 spawnPos)
{
    Block previewBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
    previewBlock.gameObject.name = "PreviewBlock";
    previewBlock.enabled = false;
    if (previewBlock.TryGetComponent<Collider2D>(out Collider2D col)) col.enabled = false;

    previewBlock.width = data.width;
    previewBlock.blockType = data.blockType;
    previewBlock.fireTargetColor = data.fireTargetColor;

    previewBlock.SetVisual(GetVisualSpriteForBlockData(data), data.color, data.width);

    if (data.isFrozen) previewBlock.SetFrozen(true, GetIceSpriteForLength(data.width));
    if (data.isChained) previewBlock.SetChained(previewBlock.width, GetChainIntactSpriteForLength(data.width), GetChainDamagedSpriteForLength(data.width));
    previewBlock.ApplyPreviewRendererSorting(PreviewSortingOrder);
    previewBlock.transform.localScale = new Vector3(previewVisualScale, previewVisualScale, 1f);

    return previewBlock.gameObject;
}

public List<Block> SpawnRowFromData(int y)
{
    List<Block> spawnedBlocks = new List<Block>(nextRowData.Count);

    foreach (BlockData data in nextRowData)
    {
        Block spawnedBlock = SpawnConfiguredBlock(data, y, true, -1f);
        if (spawnedBlock != null)
            spawnedBlocks.Add(spawnedBlock);
    }

    RebuildGridMemory();
    return spawnedBlocks;
}

//---------------------//ÖN İZLEME FONKSİYONLARI-----------------------

public void SafeDestroyBlock(Block block, GameObject destroyFxPrefab = null, bool useDefaultFxIfNull = true, float crunchDuration = DefaultRowClearCrunchDuration)
{
    if (block == null)
        return;

    if (block.isBeingDestroyed)
        return;

    if (block == heldFireSourceBlock && isFireResolving)
        return;

    block.isBeingDestroyed = true;
    block.TryCollectCollectible();

    activeBlocks.Remove(block);

    for (int i = 0; i < block.width; i++)
    {
        int cellX = block.x + i;

        if (cellX >= 0 && cellX < width && block.y >= 0 && block.y < height)
        {
            if (gridArray[cellX, block.y] == block)
                gridArray[cellX, block.y] = null;
        }
    }

    block.StartCoroutine(block.CrunchAndDestroy(explosionPrefab, destroyFxPrefab, useDefaultFxIfNull, crunchDuration));
}

public void DestroyBlocksByColor(Color targetColor)
{
    if (enableFireWaveClear)
    {
        if (isFireResolving)
            return;

        StartCoroutine(DestroyBlocksByColorWaveRoutine(targetColor));
        return;
    }

    System.Collections.Generic.List<Block> blocksToDestroy = new System.Collections.Generic.List<Block>();

    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (block.isBeingDestroyed)
            continue;

        if (block == heldFireSourceBlock)
            continue;

        if (block.blockColor == targetColor && !block.isRock && block.blockType != BlockType.Fire && block.blockType != BlockType.Slice)
        {
            if (block.isFrozen)
            {
                block.SetFrozen(false);
                continue;
            }

            blocksToDestroy.Add(block);
        }
    }

    foreach (Block block in blocksToDestroy)
    {
        SafeDestroyBlock(block);
    }

    RebuildGridMemory();
    StartCoroutine(RebuildAndApplyGravityRoutine());
}

private IEnumerator DestroyBlocksByColorWaveRoutine(Color targetColor)
{
    isFireResolving = true;

    List<Block> blocksToDestroy = new List<Block>();

    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (block.isBeingDestroyed)
            continue;

        if (block == heldFireSourceBlock)
            continue;

        if (block.blockColor == targetColor && !block.isRock && block.blockType != BlockType.Fire && block.blockType != BlockType.Slice)
        {
            if (block.isFrozen)
            {
                block.SetFrozen(false);
                continue;
            }

            blocksToDestroy.Add(block);
        }
    }

    RebuildGridMemory();

    blocksToDestroy.Sort((a, b) =>
    {
        if (a.y != b.y)
            return b.y.CompareTo(a.y);

        return a.x.CompareTo(b.x);
    });

    // [Fire Arc FX] Tüm hedeflere aynı anda çok kollu yıldırım arkları bağlanır ve bloklar elektrikle titrer.
    if (enableFireArcFX && heldFireSourceBlock != null && blocksToDestroy.Count > 0)
    {
        Vector3 arcOrigin = heldFireSourceBlock.GetArcAnchorPosition();
        foreach (Block arcTarget in blocksToDestroy)
        {
            if (arcTarget == null || arcTarget.isBeingDestroyed) continue;

            FireArcFX.Spawn(
                arcOrigin,
                arcTarget.GetArcAnchorPosition(),
                arcTarget.GetArcTargetSize(),
                fireArcDuration,
                fireArcSegments,
                fireArcDisplacement,
                fireArcStartWidth,
                fireArcEndWidth,
                fireArcMaterial);

            arcTarget.PlayFireShockFeedback(fireArcDuration);
        }

        // Elektrik çarpması ve şok hissinin gözlemlenmesi için kısa bir yüklenme beklemesi
        yield return new WaitForSeconds(0.12f);
    }

    foreach (Block block in blocksToDestroy)
    {
        if (block == null)
            continue;

        if (block.isBeingDestroyed)
            continue;

        SafeDestroyBlock(block);

        // Preserve a non-zero wave cadence after removing the old per-target arc wait.
        yield return new WaitForSeconds(fireWaveDelayBetweenBlocks);
    }

    yield return StartCoroutine(RebuildAndApplyGravityRoutine());

    if (heldFireSourceBlock != null)
    {
        Block blockToDestroy = heldFireSourceBlock;
        heldFireSourceBlock = null;
        SafeDestroyBlock(blockToDestroy);

        yield return StartCoroutine(RebuildAndApplyGravityRoutine());
    }

    isFireResolving = false;

            yield return StartCoroutine(CheckAndClearRowsRoutine(false, 0));
}

public void TriggerSlice(Block sliceBlock)
{
    if (sliceBlock == null)
        return;

    List<Block> targets = new List<Block>();

    TryAddSliceTarget(targets, sliceBlock, sliceBlock.y + 1);
    TryAddSliceTarget(targets, sliceBlock, sliceBlock.y - 1);

    if (targets.Count == 0)
        return;

    isSliceResolving = true;
    activeSliceOperations += targets.Count;

    foreach (Block target in targets)
    {
        if (target == null)
            continue;

        SliceBlock(target);
    }

    StartCoroutine(SliceResolveDelayRoutine());
}

private IEnumerator SliceBlockRoutine(Block target)
{
    if (target == null)
    {
        FinishSliceOperation();
        yield break;
    }

    if (target.isBeingDestroyed)
    {
        FinishSliceOperation();
        yield break;
    }

    yield return StartCoroutine(SliceBlockRoutineInternal(target));

    FinishSliceOperation();
}

private IEnumerator SliceBlockRoutineInternal(Block target)
{
    if (target == null)
        yield break;

    if (target.isBeingDestroyed)
        yield break;

    ShowSliceTriggerFeedback(target);

    yield return target.StartCoroutine(target.SliceFeedback());

    yield return new WaitForSeconds(slicePreSplitDelay);

    if (target == null)
        yield break;

    if (target.isBeingDestroyed)
        yield break;

    if (target.width <= 2)
    {
        SpawnSliceCutFx(target.transform.position);
        yield return new WaitForSeconds(sliceResolveDelay);
        SpriteRenderer targetSrSmall = target.GetComponent<SpriteRenderer>();
        if (targetSrSmall != null)
            targetSrSmall.enabled = false;
        SafeDestroyBlock(target, null, false);
        yield break;
    }

    int originalWidth = target.width;
    int leftWidth = originalWidth / 2;
    int rightWidth = originalWidth - leftWidth;

    int originalX = target.x;
    int y = target.y;

    Color color = target.blockColor;
    Sprite sprite = target.GetComponent<SpriteRenderer>().sprite;

    Block leftBlock = CreateSplitBlockAndReturn(originalX, y, leftWidth, color, sprite);
    Block rightBlock = CreateSplitBlockAndReturn(originalX + leftWidth, y, rightWidth, color, sprite);

    if (leftBlock != null)
    {
        Vector3 finalPos = leftBlock.transform.position;
        leftBlock.transform.position = finalPos + new Vector3(-sliceSplitOffset, 0f, 0f);
        StartCoroutine(MoveBlockToPosition(leftBlock.transform, finalPos, sliceSplitMoveDuration));
    }

    if (rightBlock != null)
    {
        Vector3 finalPos = rightBlock.transform.position;
        rightBlock.transform.position = finalPos + new Vector3(sliceSplitOffset, 0f, 0f);
        StartCoroutine(MoveBlockToPosition(rightBlock.transform, finalPos, sliceSplitMoveDuration));
    }

    SpriteRenderer targetSr = target.GetComponent<SpriteRenderer>();
    if (targetSr != null)
        targetSr.enabled = false;

    SpawnSliceCutFx(target.transform.position);
    yield return new WaitForSeconds(sliceResolveDelay);
    SafeDestroyBlock(target, null, false);

    RebuildGridMemory();
}

private IEnumerator SliceResolveDelayRoutine()
{
    yield return new WaitForSeconds(sliceResolveDelay);

    while (activeSliceOperations > 0)
    {
        yield return null;
    }

    isSliceResolving = false;
}

private void FinishSliceOperation()
{
    if (activeSliceOperations > 0)
        activeSliceOperations--;
}

private IEnumerator SliceSourceHoldAndDestroyRoutine(Block sliceBlock)
{
    if (sliceBlock == null)
        yield break;

    if (isSliceResolving)
        yield return new WaitUntil(() => !isSliceResolving);
    else
        yield return new WaitForSeconds(sliceResolveDelay);

    if (sliceBlock == null)
        yield break;

    if (sliceBlock.isBeingDestroyed)
        yield break;

    SafeDestroyBlock(sliceBlock, null, false);
}

private void SpawnSliceCutFx(Vector3 position)
{
    if (sliceFXPrefab == null)
    {
        if (!warnedMissingSliceFX)
        {
            Debug.LogWarning("GridManager: sliceFXPrefab is not assigned. Slice FX will be skipped.");
            warnedMissingSliceFX = true;
        }
        return;
    }

    GameObject fx = Instantiate(sliceFXPrefab, position + sliceCutFxOffset, Quaternion.identity);
    fx.transform.localScale = sliceCutFxScale;

    if (fx.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
    {
        sr.sortingOrder = 60;
    }

    Destroy(fx, sliceCutFxLifetime);
}

private void TryAddSliceTarget(List<Block> targets, Block sliceBlock, int targetY)
{
    if (targetY < 0 || targetY >= height)
        return;

    for (int x = sliceBlock.x; x < sliceBlock.x + sliceBlock.width; x++)
    {
        if (x < 0 || x >= width)
            continue;

        Block target = gridArray[x, targetY];

        if (target == null)
            continue;

        if (target == sliceBlock)
            continue;

        if (targets.Contains(target))
            continue;

        if (target.width < 2)
            continue;

        if (target.isRock)
            continue;

        if (target.isFrozen)
            continue;

        if (target.isChained)
            continue;

        if (target.blockType == BlockType.Fire)
            continue;

        if (target.blockType == BlockType.Slice)
            continue;

        targets.Add(target);
    }
}

public void SliceBlock(Block target)
{
    StartCoroutine(SliceBlockRoutine(target));
}

private IEnumerator SlicePostSplitSettleRoutine()
{
    isSliceResolving = true;

    yield return null;
    yield return StartCoroutine(RebuildAndApplyGravityRoutine());

    isSliceResolving = false;
}

private Block CreateSplitBlockAndReturn(int x, int y, int widthValue, Color color, Sprite sprite)
{
    if (widthValue <= 0)
        return null;

    Block newBlock = Instantiate(blockPrefab);
    GameObject obj = newBlock.gameObject;

    Sprite resolvedSprite = GetNormalGemSpriteForColorAndLength(color, widthValue, sprite);

    newBlock.SetVisual(resolvedSprite, color, widthValue);

    newBlock.width = widthValue;
    newBlock.x = x;
    newBlock.y = y;
    newBlock.blockColor = color;
    newBlock.blockType = BlockType.Normal;

    SpriteRenderer sr = newBlock.GetComponent<SpriteRenderer>();

    if (sr != null)
    {
        if (resolvedSprite != null)
            sr.sprite = resolvedSprite;
    }

    float worldX = x + (widthValue - 1) * 0.5f;
    obj.transform.position = new Vector3(worldX, y, 0f);

    activeBlocks.Add(newBlock);

    for (int i = 0; i < widthValue; i++)
    {
        int cellX = x + i;

        if (cellX >= 0 && cellX < width)
            gridArray[cellX, y] = newBlock;
    }

    return newBlock;
}

private IEnumerator MoveBlockToPosition(Transform target, Vector3 finalPosition, float duration)
{
    if (target == null)
        yield break;

    Vector3 startPosition = target.position;
    float timer = 0f;

    while (timer < duration)
    {
        if (target == null)
            yield break;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        t = t * t * (3f - 2f * t);

        target.position = Vector3.Lerp(startPosition, finalPosition, t);

        yield return null;
    }

    if (target != null)
        target.position = finalPosition;
}

void CreateSplitBlock(int x, int y, int widthValue, Color color, Sprite sprite)
{
    if (widthValue <= 0)
        return;

    Block newBlock = Instantiate(blockPrefab);
    GameObject obj = newBlock.gameObject;

    Sprite resolvedSprite = GetNormalGemSpriteForColorAndLength(color, widthValue, sprite);

    newBlock.SetVisual(resolvedSprite, color, widthValue);

    newBlock.width = widthValue;
    newBlock.x = x;
    newBlock.y = y;
    newBlock.blockColor = color;
    newBlock.blockType = BlockType.Normal;

    SpriteRenderer sr = newBlock.GetComponent<SpriteRenderer>();

    if (sr != null && resolvedSprite != null)
        sr.sprite = resolvedSprite;

        float worldX = x + (widthValue - 1) * 0.5f;

        obj.transform.position = new Vector3(worldX, y, 0f);

        
    activeBlocks.Add(newBlock);

    for (int i = 0; i < widthValue; i++)
    {
        int cellX = x + i;

        if (cellX >= 0 && cellX < width)
        {
            gridArray[cellX, y] = newBlock;
        }
    }
}
private System.Collections.IEnumerator SliceDestroyAfterFeedback(Block target)
{
    if (target == null)
        yield break;

    yield return target.StartCoroutine(target.SliceFeedback());

    SpawnSliceCutFx(target.transform.position);
    SafeDestroyBlock(target, null, false);

    yield return StartCoroutine(RebuildAndApplyGravityRoutine());
}

}
