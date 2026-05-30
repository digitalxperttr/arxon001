using UnityEngine;
using System.Collections; // Coroutine için şart
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum GameState { IDLE, MOVING, FALLING, CHECKING, SPAWNING }


public class GridManager : MonoBehaviour
{
    [System.Serializable]
    public struct GemVisual
    {
        public Sprite sprite;
        public Color particleColor; // O mücevher patladığında çıkacak renk
    }

    [Header("Mücevher Koleksiyonu")]
    public GemVisual[] normalGems; // 1, 2, 3, 7, 8. sıradaki renkli taşlar
    public Sprite rockSprite;      // 2. sıradaki gri taş
    public Sprite iceSprite;       // 4. sıradaki buz
    public Sprite lavaSprite;      // 6. sıradaki lavlı taş (Yeni mekanik!)
    [Header("Özel Blok Ayarları")]
    public Sprite fireSprite;
    public Sprite sliceSprite;

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
    [SerializeField] [Range(0.005f, 0.012f)] private float fogDistortionStrength = 0.008f;
    [SerializeField] [Range(0.25f, 0.45f)] private float fogDistortionSpeed = 0.35f;

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

    [Header("Fire Trigger Feedback")]
    [SerializeField] private string fireTriggerText = "FIRE!";
    [SerializeField] private Color fireTriggerTextColor = new Color(1f, 0.35f, 0.05f);
    [SerializeField] private float fireTriggerTextSize = 9f;

    [SerializeField] private float fireTriggerShakeDuration = 0.18f;
    [SerializeField] private float fireTriggerShakeStrength = 0.16f;

    [Header("Fire Arc Visual")]
    [SerializeField] private bool enableFireArcVisual = true;
    [SerializeField] private float fireArcDuration = 0.08f;
    [SerializeField] private float fireArcWidth = 0.06f;
    [SerializeField] private Color fireArcColor = new Color(1f, 0.35f, 0.05f, 1f);

    [Header("Fire Wave Clear")]
    [SerializeField] private float fireWaveDelayBetweenBlocks = 0.045f;
    [SerializeField] private bool enableFireWaveClear = true;
    private bool hasFireSourcePosition = false;
    private Vector3 currentFireSourcePosition;
    private Block heldFireSourceBlock;
    private bool isFireResolving = false;

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
    [SerializeField] private float chainBreakGravityStepDelay = 0.12f;

    [Header("Fire Color Pulse")]
    [SerializeField] private float firePulseScale = 1.12f;
    [SerializeField] private float firePulseDuration = 0.12f;

    [SerializeField] private bool enableClassicDoubleRowSpawn = true;
    private BlockTestSpawner blockTestSpawner;

    // Sahnedeki tüm kanlı canlı blokları burada tutacağız
    public System.Collections.Generic.List<Block> activeBlocks = new System.Collections.Generic.List<Block>();
    public static GridManager Instance { get; private set; }

    public Block[,] gridArray;
    public Block blockPrefab;
    public GameState currentState = GameState.IDLE;
    private bool isResolvingNoMove = false;
    private bool isRunningDifficultyPush = false;
    private bool isSliceResolving = false;
    private bool chainBreakImpactPausePending = false;
    private bool slowGravityAfterChainBreakPending = false;
    private bool useSlowGravityThisPass = false;
    private int activeSliceOperations = 0;
    private FogController fogController;
    public GameObject cellPrefab; // Az önce oluşturduğumuz Square'i buraya sürükleyeceğiz
    public Color gridColor = new Color(1f, 1f, 1f, 0.1f); // Hafif transparan beyaz/gri

    public bool isGameOver = false;
    public GameObject losePanel; // Unity'den atayacağımız panel

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
        profile.rockChance = 0.05f;
        profile.allowDoubleRowSpawn = true;
        profile.doubleRowChance = 0.15f;
    }
    else if (level >= 6)
    {
        profile.fireChance = 0.04f;
        profile.sliceChance = 0.03f;
        profile.frozenChance = 0.04f;
        profile.allowDoubleRowSpawn = true;
        profile.doubleRowChance = 0.10f;
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
        gridArray = new Block[width, height];
    }


void Start()
    {
        GenerateBackgroundGrid();
        GenerateFog(); // <--- YENİ EKLENDİ (Oyuna başlarken sisi basar)

        // 1. Grid'i tertemiz hazırla
        gridArray = new Block[width, height];
        activeBlocks.Clear();

        // 2. Başlangıç tahtasını kur (debug spawner varsa onu kullan, yoksa normal akış)
        bool usedDebugBoard =
            blockTestSpawner != null &&
            blockTestSpawner.enabled &&
            blockTestSpawner.TryBuildInitialBoard(this);

        if (!usedDebugBoard)
        {
            SetupInitialBoard(4);
        }

        // 3. Durumu IDLE yapalım ki oyuncu dokunabilsin
        currentState = GameState.IDLE;

        // 4. Sistemi coroutine ile dürt
        StartCoroutine(InitialGravityCheck());
        
        // Skor tabelasını da hemen dürtelim
        if(ScoreManager.Instance != null) ScoreManager.Instance.UpdateScoreUI();
    }

private void Update()
{
    if (isGameOver)
        return;

    if (currentState != GameState.IDLE)
        return;

    if (isResolvingNoMove)
        return;

    if (LevelManager.Instance != null && LevelManager.Instance.enabled)
        return;

    if (!HasAnyLegalPlayerMove())
    {
        StartCoroutine(ResolveNoMoveRoutine());
    }
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

    if (!isGameOver && currentState == GameState.IDLE && !HasAnyLegalPlayerMove())
    {
        ChangeState(GameState.SPAWNING);
        yield return StartCoroutine(PushBoardUpRoutine());
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
            data.visualSprite = rockSprite;
            data.color = Color.gray;
            break;
        case BlockType.Ice:
            data.isFrozen = true;
            break;
        case BlockType.Chained:
            data.isChained = true;
            break;
        case BlockType.Fire:
            if (fireSprite != null)
                data.visualSprite = fireSprite;
            break;
        case BlockType.Slice:
            if (sliceSprite != null)
                data.visualSprite = sliceSprite;
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

    newBlock.SetVisual(data.visualSprite, data.color, data.width);

    if (data.isRock) newBlock.SetRock(true);
    if (data.isFrozen) newBlock.SetFrozen(true, iceSprite);
    if (data.isChained) newBlock.SetChained(newBlock.width);

    activeBlocks.Add(newBlock);

    if (animateIntoPlace)
        newBlock.MoveTo(newBlock.x, newBlock.y);

    return newBlock;
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
        
        // Kaybetme panelini aç
        if (losePanel != null) losePanel.SetActive(true);

        // Oyunu durdur
        Time.timeScale = 0; 
    }

void GenerateBackgroundGrid()
{
    // Bir "Background" objesi oluşturalım ki Hierarchy kalabalıklaşmasın
    GameObject gridParent = new GameObject("BackgroundGrid");

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            Vector2 pos = new Vector2(x, y);
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

    // 1. ÖNCE HER ŞEYİ AYNI ANDA YAP (Zıplama olmasın)
    // Mevcutları yukarı it
    foreach (Block b in activeBlocks) 
    {
        b.y += 1;
        b.MoveTo(b.x, b.y);
    }
    
    // Hafızayı güncelle
    RebuildGridMemory();

    // HİÇ BEKLEMEDEN: Rastgele satır doğurma! Onun yerine önizlemedeki satırı oyuna al.
    SpawnRowFromData(0); 

    // HEMEN ARDINDAN: Bir sonraki hamle için yeni bir önizleme (taslak) oluştur.
    GenerateNextRowData(); 

    // 2. ŞİMDİ OYUNCUYA SÜRE TANI (Hızı buradan kontrol et)
    // "Hızlı gibi" dediğin yer burası, bu süreyi artırabilirsin.
    yield return new WaitForSeconds(0.4f); // 0.1f yerine 0.4f veya 0.5f yaparak sakinleştiriyoruz.

    // --- YENİ: GAME OVER KONTROLÜ ---
    CheckGameOver();
    if (isGameOver) yield break; 
    // -------------------------------

    // 3. SONRA KONTROLLERİ YAP
    ChangeState(GameState.FALLING); 
    yield return StartCoroutine(RebuildAndApplyGravityRoutine());
    
    ChangeState(GameState.CHECKING);
    yield return StartCoroutine(CheckAndClearRowsRoutine());
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
    int pushCount = 1;

    if (enableClassicDoubleRowSpawn)
    {
        ClassicDifficultyProfile profile = GetClassicDifficultyProfile(level);

        if (profile.allowDoubleRowSpawn && Random.value < profile.doubleRowChance)
        {
            pushCount = 2;
            Debug.Log("DOUBLE ROW SPAWN!");
        }
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

public void RestartGame()
{
    Time.timeScale = 1f;
    isGameOver = false;

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
}

public bool IsClassicRun()
{
    return LevelManager.Instance == null ||
        !LevelManager.Instance.enabled ||
        LevelManager.Instance.currentLevel == null;
}

    // YERÇEKİMİ MOTORU
public IEnumerator ApplyGravityRoutine()
{
    while (isSliceResolving)
    {
        yield return null;
    }

    bool shouldUseSlowGravity = useSlowGravityThisPass;
    useSlowGravityThisPass = false;
    float gravityStepDelay = shouldUseSlowGravity ? chainBreakGravityStepDelay : 0.1f;

    bool movedAny;
    do {
        movedAny = false;
        // height-1'den başla (Yukarıdan Aşağı tarama)
        for (int y = 1; y < height; y++) 
        {
            for (int x = 0; x < width; x++)
            {
                Block b = gridArray[x, y];
                // Sadece bloğun 'ana' (sol) hücresini bulduğumuzda işlem yap
                if (b != null && b.x == x && b.y == y)
                {
                    if (CanFall(b))
                    {
                        UpdateBlockInGrid(b, b.x, b.y - 1);
                        b.MoveTo(b.x, b.y);
                        movedAny = true;
                    }
                }
            }
        }
        if (movedAny) yield return new WaitForSeconds(gravityStepDelay);
    } while (movedAny);
}

// Bloğun altındaki tüm genişliği kontrol eden yardımcı:
bool CanFall(Block b) {
    if (b == null) return false;
    if (b == heldFireSourceBlock && isFireResolving) return false;

    for (int i = 0; i < b.width; i++) {
        if (b.y - 1 < 0 || gridArray[b.x + i, b.y - 1] != null) return false;
    }
    return true;
}



public IEnumerator CheckAndClearRowsRoutine(bool isPlayerMove = false, int chainDepth = 0)
    {
        // 1. EMNİYET KONTROLÜ VE PERFECT CLEAR (BONUS BURAYA TAŞINDI)
        if (activeBlocks == null || activeBlocks.Count == 0)
        {
            if (!isGameOver) 
            {
                Debug.Log("<color=yellow>PERFECT CLEAR! Tahta tertemiz oldu!</color>");
                
                // Oyuncuya 1000 Puan Mükemmel Temizlik Bonusu
                if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(1000); 
                
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
        int lowestClearedY = -1; // YENİ: Yazının çıkacağı pozisyonu bulmak için

        for (int y = 0; y < height; y++)
        {
            if (IsRowFull(y))
            {
                if (lowestClearedY == -1) lowestClearedY = y; // Patlayan ilk satırın yerini kaydet
                ClearRow(y);
                clearedRowCount++; 
                y--; 
            }
        }

        if (clearedRowCount > 0)
        {
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

            if (chainBreakImpactPausePending)
            {
                chainBreakImpactPausePending = false;
                yield return new WaitForSeconds(chainBreakImpactPause);
            }

            if (slowGravityAfterChainBreakPending)
            {
                useSlowGravityThisPass = true;
                slowGravityAfterChainBreakPending = false;
            }

            RebuildGridMemory();
            yield return new WaitForSeconds(0.2f);
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

public void ShowFireTriggerFeedback(Block fireBlock)
{
    if (fireBlock == null)
        return;

    if (floatingTextPrefab != null)
    {
        Vector3 textPos = fireBlock.transform.position + new Vector3(0f, 1.2f, 0f);
        FloatingText text = Instantiate(floatingTextPrefab, textPos, Quaternion.identity);
        text.SetText(fireTriggerText, fireTriggerTextColor, fireTriggerTextSize);
    }

    StartCoroutine(CameraShakeRoutine(
        fireTriggerShakeDuration,
        fireTriggerShakeStrength
    ));
}

public void SetCurrentFireSource(Block fireBlock)
{
    if (fireBlock == null)
    {
        hasFireSourcePosition = false;
        heldFireSourceBlock = null;
        return;
    }

    currentFireSourcePosition = fireBlock.transform.position;
    hasFireSourcePosition = true;
    heldFireSourceBlock = fireBlock;
}

private IEnumerator FireArcRoutine(Vector3 startPos, Vector3 endPos)
{
    if (!enableFireArcVisual)
        yield break;

    GameObject arcObj = new GameObject("FireArc");

    LineRenderer line = arcObj.AddComponent<LineRenderer>();

    line.positionCount = 2;
    line.SetPosition(0, startPos);
    line.SetPosition(1, endPos);

    line.startWidth = fireArcWidth;
    line.endWidth = fireArcWidth * 0.4f;

    line.startColor = fireArcColor;
    line.endColor = new Color(fireArcColor.r, fireArcColor.g, fireArcColor.b, 0f);

    line.sortingOrder = 50;

    Shader spriteShader = Shader.Find("Sprites/Default");
    if (spriteShader != null)
    {
        Material mat = new Material(spriteShader);
        line.material = mat;
    }

    yield return new WaitForSeconds(fireArcDuration);

    Destroy(arcObj);
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

private IEnumerator FireColorPulseRoutine(Color targetColor)
{
    List<Block> affectedBlocks = new List<Block>();

    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (block.isBeingDestroyed)
            continue;

        if (block.blockColor == targetColor)
        {
            affectedBlocks.Add(block);
        }
    }

    Dictionary<Block, Vector3> originalScales = new Dictionary<Block, Vector3>();

    foreach (Block block in affectedBlocks)
    {
        if (block == null)
            continue;

        originalScales[block] = block.transform.localScale;

        block.transform.localScale *= firePulseScale;
    }

    yield return new WaitForSeconds(firePulseDuration);

    foreach (Block block in affectedBlocks)
    {
        if (block == null)
            continue;

        if (originalScales.ContainsKey(block))
        {
            block.transform.localScale = originalScales[block];
        }
    }
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

void ClearRow(int y)
    {
        System.Collections.Generic.List<Block> blocksToDestroy = new System.Collections.Generic.List<Block>();
        System.Collections.Generic.List<Block> blocksToUnfreeze = new System.Collections.Generic.List<Block>();
        System.Collections.Generic.List<Block> blocksToHoldSlice = new System.Collections.Generic.List<Block>();
        System.Collections.Generic.HashSet<Block> processedChainedBlocks = new System.Collections.Generic.HashSet<Block>();
        System.Collections.Generic.HashSet<Block> protectedFromRemovalThisClear = new System.Collections.Generic.HashSet<Block>();

        // 1. Satırdaki blokları ayır
        for (int x = 0; x < width; x++) {
            Block b = gridArray[x, y];
            if (b != null) {
                bool wasChainedAtClearStart = b.IsChained();

                if (wasChainedAtClearStart) {
                    if (!processedChainedBlocks.Contains(b))
                    {
                        if (b.BreakOneChain())
                        {
                            chainBreakImpactPausePending = true;
                            slowGravityAfterChainBreakPending = true;
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
                    blocksToUnfreeze.Add(b); // Sadece buzluysa buzu kırılacak
                }
                else if (!b.isFrozen && !b.IsChained() && !blocksToDestroy.Contains(b) && !blocksToUnfreeze.Contains(b)) {
                    if (b.blockType == BlockType.Slice)
                    {
                        if (!blocksToHoldSlice.Contains(b))
                        {
                            blocksToHoldSlice.Add(b);
                            b.TriggerSpecial();
                            StartCoroutine(SliceSourceHoldAndDestroyRoutine(b));
                        }
                    }
                    else
                    {
                        b.TriggerSpecial();
                        blocksToDestroy.Add(b); // Hiçbir şeyi yoksa patlayacak!
                    }
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
                SafeDestroyBlock(b);
            }

        RebuildGridMemory(); 
    }

public bool AreBlocksMoving()
    {
        foreach (var b in gridArray) { if (b != null && b.isMoving) return true; }
        return false;
    }

public void ChangeState(GameState newState)
    {
        currentState = newState;
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
//---------------------ÖN İZLEME FONKSİYONLARI-----------------------

public void GenerateNextRowData()
{
        nextRowData.Clear();
        int currentX = 0;
        int blockCountInRow = 0;

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
            int level = ScoreManager.Instance != null ? ScoreManager.Instance.currentLevel : 1;
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

            // Eğer özel bir durum varsa (Kaya, Buz vb.) resmi değiştirelim
            newData.isRock = (Random.value < currentRockChance);
            if (newData.isRock)
            {
                newData.blockType = BlockType.Rock;
                newData.visualSprite = rockSprite;
                newData.color = Color.gray;
            }
            if (!newData.isRock && !newData.isFrozen && !newData.isChained)
{
            float specialRoll = Random.value;

            if (specialRoll < currentFireChance)
            {
                newData.blockType = BlockType.Fire;
                newData.width = 1;

                if (fireSprite != null)
                    newData.visualSprite = fireSprite;
            }
            else if (specialRoll < currentFireChance + currentSliceChance)
            {
                newData.blockType = BlockType.Slice;
                newData.width = 1;

                if (sliceSprite != null)
                    newData.visualSprite = sliceSprite;
            }
}

            newData.isChained = !newData.isRock && (Random.value < currentChainedChance);
            if (newData.isChained)
            {
                newData.blockType = BlockType.Chained;
            }

            newData.isFrozen = !newData.isRock && !newData.isChained && (Random.value < currentFreezeChance);
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

           newData.isRock = (Random.value < currentRockChance);
           if (newData.isRock)
           {
               newData.blockType = BlockType.Rock;
               newData.visualSprite = rockSprite;
               newData.color = Color.gray;
           }

           if (!newData.isRock && !newData.isFrozen && !newData.isChained)
           {
               float specialRoll = Random.value;

               if (specialRoll < currentFireChance)
               {
                   newData.blockType = BlockType.Fire;
                   newData.width = 1;

                   if (fireSprite != null)
                       newData.visualSprite = fireSprite;
               }
               else if (specialRoll < currentFireChance + currentSliceChance)
               {
                   newData.blockType = BlockType.Slice;
                   newData.width = 1;

                   if (sliceSprite != null)
                       newData.visualSprite = sliceSprite;
               }
           }

           newData.isChained = !newData.isRock && (Random.value < currentChainedChance);
           if (newData.isChained)
           {
               newData.blockType = BlockType.Chained;
           }

           newData.isFrozen = !newData.isRock && !newData.isChained && (Random.value < currentFreezeChance);
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
        Vector3 spawnPos = new Vector3(data.x + (data.width - 1) * 0.5f, -1.0f, 0);
        Block previewBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
        previewBlock.gameObject.name = "PreviewBlock";
        previewBlock.enabled = false;
        if (previewBlock.TryGetComponent<Collider2D>(out Collider2D col)) col.enabled = false;

        // Görseli ayarla (Yeni width parametresiyle)
        previewBlock.SetVisual(data.visualSprite, data.color, data.width);
        
        // 1. Önce rengi ve boyutu ayarla (Parent)
        SpriteRenderer sr = previewBlock.GetComponent<SpriteRenderer>();
        sr.size = new Vector2(data.width - 0.1f, 0.5f); 
        sr.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Gölge rengi
        sr.sortingOrder = -5;
        if (data.isFrozen) previewBlock.SetFrozen(true, iceSprite);


        previewVisuals.Add(previewBlock.gameObject);
    }
}

public void SpawnRowFromData(int y)
{
    foreach (BlockData data in nextRowData)
    {
        SpawnConfiguredBlock(data, y, true, -1f);
    }
    RebuildGridMemory();
}

//---------------------//ÖN İZLEME FONKSİYONLARI-----------------------

public void SafeDestroyBlock(Block block, GameObject destroyFxPrefab = null, bool useDefaultFxIfNull = true)
{
    if (block == null)
        return;

    if (block.isBeingDestroyed)
        return;

    if (block == heldFireSourceBlock && isFireResolving)
        return;

    block.isBeingDestroyed = true;

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

    block.StartCoroutine(block.CrunchAndDestroy(explosionPrefab, destroyFxPrefab, useDefaultFxIfNull));
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

    StartCoroutine(FireColorPulseRoutine(targetColor));

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
            blocksToDestroy.Add(block);
        }
    }

    blocksToDestroy.Sort((a, b) =>
    {
        if (a.y != b.y)
            return b.y.CompareTo(a.y);

        return a.x.CompareTo(b.x);
    });

    foreach (Block block in blocksToDestroy)
    {
        if (block == null)
            continue;

        if (block.isBeingDestroyed)
            continue;

        if (hasFireSourcePosition)
        {
            yield return StartCoroutine(FireArcRoutine(
                currentFireSourcePosition,
                block.transform.position
            ));
        }

        SafeDestroyBlock(block);

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

    hasFireSourcePosition = false;
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
    activeSliceOperations = targets.Count;

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

    newBlock.SetVisual(sprite, color, widthValue);

    newBlock.width = widthValue;
    newBlock.x = x;
    newBlock.y = y;
    newBlock.blockColor = color;
    newBlock.blockType = BlockType.Normal;

    SpriteRenderer sr = newBlock.GetComponent<SpriteRenderer>();

    if (sr != null)
    {
        sr.color = color;

        if (sprite != null)
            sr.sprite = sprite;
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

    newBlock.SetVisual(sprite, color, widthValue);

    newBlock.width = widthValue;
    newBlock.x = x;
    newBlock.y = y;
    newBlock.blockColor = color;
    newBlock.blockType = BlockType.Normal;

    SpriteRenderer sr = newBlock.GetComponent<SpriteRenderer>();


    sr.color = color;

    if (sprite != null)
        sr.sprite = sprite;

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
