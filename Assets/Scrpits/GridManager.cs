using UnityEngine;
using System.Collections; // Coroutine için şart
using UnityEngine.SceneManagement;

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

    [Header("Efektler")]
    public FloatingText floatingTextPrefab;

    public GameObject explosionPrefab;

    [SerializeField] private bool enableFreezeFrame = true;
    [SerializeField] private float freezeDuration = 0.04f;
    [SerializeField] private float freezeTimeScale = 0.08f;

    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeStrength = 0.08f;

    // Sahnedeki tüm kanlı canlı blokları burada tutacağız
    public System.Collections.Generic.List<Block> activeBlocks = new System.Collections.Generic.List<Block>();
    public static GridManager Instance { get; private set; }

    public Block[,] gridArray;
    public Block blockPrefab;
    public GameState currentState = GameState.IDLE;
    public GameObject cellPrefab; // Az önce oluşturduğumuz Square'i buraya sürükleyeceğiz
    public Color gridColor = new Color(1f, 1f, 1f, 0.1f); // Hafif transparan beyaz/gri

    public bool isGameOver = false;
    public GameObject losePanel; // Unity'den atayacağımız panel

    [Header("Önizleme (Preview) Ayarları")]
    public float previewYPosition = -1.2f; // Gridin hemen altında duracağı Y koordinatı
    public float previewAlpha = 0.5f;      // Yarı şeffaflık oranı
    
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

    public System.Collections.Generic.List<BlockData> nextRowData = new System.Collections.Generic.List<BlockData>();
    private System.Collections.Generic.List<GameObject> previewVisuals = new System.Collections.Generic.List<GameObject>();
    //ÖN İZLEME HEADER BİTİMİ
  
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
        gridArray = new Block[width, height];
    }


void Start()
    {
        GenerateBackgroundGrid();
        GenerateFog(); // <--- YENİ EKLENDİ (Oyuna başlarken sisi basar)

        // 1. Grid'i tertemiz hazırla
        gridArray = new Block[width, height];
        activeBlocks.Clear();

        // 2. Başlangıç tahtasını kur (Oyuncuya oynayabileceği 4 satır veriyoruz)
        SetupInitialBoard(4);

        // 3. Durumu IDLE yapalım ki oyuncu dokunabilsin
        currentState = GameState.IDLE;

        // 4. Sistemi coroutine ile dürt
        StartCoroutine(InitialGravityCheck());
        
        // Skor tabelasını da hemen dürtelim
        if(ScoreManager.Instance != null) ScoreManager.Instance.UpdateScoreUI();
    }


// Oyun başlarken tahtayı dolduran yeni ve temiz fonksiyon
void SetupInitialBoard(int startingRowCount)
    {
        for (int y = 0; y < startingRowCount; y++)
        {
            GenerateNextRowData(); 
            
            foreach (BlockData data in nextRowData)
            {
                Vector3 spawnPos = new Vector3(data.x + (data.width - 1) * 0.5f, y, 0); 
                Block newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
                newBlock.width = data.width;
                newBlock.x = data.x;
                newBlock.y = y;
                newBlock.blockType = data.blockType;
                
                // --- DÜZELTME BURADA: Artık başlangıçta da mücevher görsellerini giydiriyoruz ---
                newBlock.SetVisual(data.visualSprite, data.color, data.width);

                // YENİ: Kimlikleri aktarıyoruz
                if (data.isRock) newBlock.SetRock(true);
                if (data.isFrozen) newBlock.SetFrozen(true, iceSprite); // iceSprite'ı parametre olarak gönderdik
                if (data.isChained) newBlock.SetChained(true);
                
                activeBlocks.Add(newBlock);
            }
        }
        RebuildGridMemory();
        GenerateNextRowData();
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



public IEnumerator PushBoardUpRoutine()
{
    // EMNİYET KİLİDİ: Oyun bittiyse asla yeni satır ekleme ve yukarı itme
    if (isGameOver) yield break;

    // 1. ÖNCE HER ŞEYİ AYNI ANDA YAP (Zıplama olmasın)
    // Mevcutları yukarı it
    foreach (Block b in activeBlocks) 
    {
        b.y += 1;
        b.MoveTo(b.x, b.y);
        ClearFogNearRow(b.y); // <--- YENİ EKLENDİ
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
    yield return StartCoroutine(ApplyGravityRoutine());
    
    ChangeState(GameState.CHECKING);
    yield return StartCoroutine(CheckAndClearRowsRoutine());
}

public void RestartGame()
{
    Time.timeScale = 1f;
    isGameOver = false;
    
    // Çalışan tüm Coroutine'leri durdur ki eski referanslara gitmesinler
    StopAllCoroutines(); 
    
    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}

    // YERÇEKİMİ MOTORU
public IEnumerator ApplyGravityRoutine()
{
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
        if (movedAny) yield return new WaitForSeconds(0.1f);
    } while (movedAny);
}

// Bloğun altındaki tüm genişliği kontrol eden yardımcı:
bool CanFall(Block b) {
    for (int i = 0; i < b.width; i++) {
        if (b.y - 1 < 0 || gridArray[b.x + i, b.y - 1] != null) return false;
    }
    return true;
}



public IEnumerator CheckAndClearRowsRoutine(bool isPlayerMove = false, int chainDepth = 1)
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

            int comboMultiplier = ScoreManager.Instance != null ? ScoreManager.Instance.comboMultiplier : 1;
            int chainMultiplier = Mathf.Max(1, chainDepth);
            int multiplier = comboMultiplier * chainMultiplier;
            int pointsToGive = clearedRowCount * 100 * clearedRowCount * multiplier; 
            
            if (ScoreManager.Instance != null) {
                ScoreManager.Instance.AddScore(pointsToGive);
                ScoreManager.Instance.AddClearedLines(clearedRowCount);
            }
            // === YENİ EKLENEN SATIRLAR ===
            if (LevelManager.Instance != null && LevelManager.Instance.enabled) {
                LevelManager.Instance.LinesCleared(clearedRowCount);
            }
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
            if (comboMultiplier > 1)
            {
                Vector3 comboPos = spawnPos + new Vector3(0, 1.2f, 0);
                FloatingText comboText = Instantiate(floatingTextPrefab, comboPos, Quaternion.identity);
                comboText.SetText($"{comboMultiplier}x COMBO!", new Color(1f, 0.4f, 0f), 8f); // Turuncu renk
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

            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(ApplyGravityRoutine());
            
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
        System.Collections.Generic.List<Block> blocksToUnchain = new System.Collections.Generic.List<Block>(); // YENİ

        // 1. Satırdaki blokları ayır
        for (int x = 0; x < width; x++) {
            Block b = gridArray[x, y];
            if (b != null) {
                if (b.isChained && !blocksToUnchain.Contains(b)) {
                    blocksToUnchain.Add(b); // Zincirliyse zinciri kırılacak
                }
                else if (b.isFrozen && !b.isChained && !blocksToUnfreeze.Contains(b)) {
                    blocksToUnfreeze.Add(b); // Sadece buzluysa buzu kırılacak
                }
                else if (!b.isFrozen && !b.isChained && !blocksToDestroy.Contains(b) && !blocksToUnfreeze.Contains(b) && !blocksToUnchain.Contains(b)) {
                    b.TriggerSpecial();
                    blocksToDestroy.Add(b); // Hiçbir şeyi yoksa patlayacak!
                }
            }
        }

        // 2. Etkileri Kır (Oyunda kalmaya devam ederler)
        foreach (Block b in blocksToUnchain) b.SetChained(false);
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

    ClearFogNearRow(newY); // <--- YENİ EKLENDİ
}

public void GenerateFog()
    {
        if (activeFogRows != null) {
            foreach(var fog in activeFogRows) if(fog != null) Destroy(fog);
        }
        activeFogRows = new GameObject[height];

        int fogStart = -1;
        if (ProgressManager.Instance != null && ProgressManager.Instance.currentSelectedLevel != null) {
            fogStart = ProgressManager.Instance.currentSelectedLevel.fogStartingRow;
        }

        if (fogStart == -1) return; // Sis yoksa direkt çık

        // Belirtilen satırdan en tepeye kadar siyah sis örtüleri oluştur
        for (int y = fogStart; y < height; y++)
        {
            GameObject fogObj = new GameObject($"FogRow_{y}");
            fogObj.transform.SetParent(this.transform);

            fogObj.transform.position = new Vector3((width - 1) / 2f, y, 0); 
            fogObj.transform.localScale = new Vector3(width, 1.05f, 1);
            
            SpriteRenderer sr = fogObj.AddComponent<SpriteRenderer>();
            sr.sprite = cellPrefab.GetComponent<SpriteRenderer>().sprite; 
            
            // YENİ: Hücremizin grafik ayarlarını ezip "Dümdüz" çiz diyoruz ki saydam kalmasın.
            sr.drawMode = SpriteDrawMode.Simple; 
            
            // YENİ: Tamamen zifiri karanlık, gece mavisi/siyah bir renk (Alpha 1.0 = Opak)
            sr.color = new Color(0.02f, 0.02f, 0.05f, 1f); 
            sr.sortingOrder = 15; // Blokların önünü kapatsın

            activeFogRows[y] = fogObj;
        }
    }

    public void ClearFogNearRow(int y)
    {
        if (activeFogRows == null) return;
        
        // YENİ: Sadece bloğun BULUNDUĞU satırı açıyoruz. (y+1'i sildik!)
        // Böylece kar küreme aracı gibi daha oraya varmadan sisi yok etmeyecekler.
        if (y >= 0 && y < height && activeFogRows[y] != null)
        {
            Destroy(activeFogRows[y]);
            activeFogRows[y] = null;
        }
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
            
            currentFreezeChance = level >= 3 ? Mathf.Clamp01((level - 2) / 12f) * 0.6f : 0f;
            currentRockChance = level >= 5 ? Mathf.Clamp01((level - 4) / 15f) * 0.2f : 0f;
            
            // ŞİMDİLİK 0 YAPTIK: Zincirli bloklar kafa karıştırmasın
            currentChainedChance = 0f; 
            currentFireChance = level >= 2 ? classicFireChance : 0f;
            currentSliceChance = level >= 3 ? classicSliceChance : 0f;
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
            float widthRoll = Random.value;
            
            if (widthRoll > currentT4) bWidth = 4;
            else if (widthRoll > currentT3) bWidth = 3;
            else if (widthRoll > currentT2) bWidth = 2;
            else bWidth = 1;                              

            if (currentX + bWidth > width) bWidth = width - currentX;

            // --- GÖRSEL SEÇİM MANTIĞI ---
            BlockData newData = new BlockData();
            newData.x = currentX;
            newData.width = bWidth;
            newData.blockType = BlockType.Normal;
            
            // Emniyet Kemeri: Eğer mücevher listesi doluysa içinden seç
                if (normalGems != null && normalGems.Length > 0)
                {
                    int randomIndex = Random.Range(0, normalGems.Length);
                    newData.visualSprite = normalGems[randomIndex].sprite;
                    newData.color = normalGems[randomIndex].particleColor;
                }
                else
                {
                    // Liste boşsa hata verme, geçici olarak beyaz bir şey ata (Hata logu bas)
                    Debug.LogWarning("GridManager: Normal Gems listesi boş! Lütfen Inspector'dan doldur.");
                    newData.color = Color.white;
                }

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

                if (fireSprite != null)
                    newData.visualSprite = fireSprite;
                else if (lavaSprite != null)
                    newData.visualSprite = lavaSprite;
            }
            else if (specialRoll < currentFireChance + currentSliceChance)
            {
                newData.blockType = BlockType.Slice;

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
           BlockData newData = new BlockData();
           newData.x = Random.Range(0, width);
           newData.width = 1;
           
           // Görsel ataması
           int randomIndex = Random.Range(0, normalGems.Length);
           newData.visualSprite = normalGems[randomIndex].sprite;
           newData.color = normalGems[randomIndex].particleColor;

           // GÜVENLİK: Bu blok asla kilitli veya buzlu doğmasın ki oyuncu hamle yapabilsin
           newData.isFrozen = false;
           newData.isRock = false;
           newData.isChained = false; 
           newData.blockType = BlockType.Normal;

           nextRowData.Add(newData);
       }

        EnsureNextRowHasAtLeastOneGap();
        // Veri hesaplandı, şimdi görselleri çiz!
        UpdatePreviewVisuals();
}

private void EnsureNextRowHasAtLeastOneGap()
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

    if (selectedData.width > 1)
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
        Vector3 spawnPos = new Vector3(data.x + (data.width - 1) * 0.5f, y - 1f, 0);
        Block newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
        
        newBlock.width = data.width;
        newBlock.x = data.x;
        newBlock.y = y;
        newBlock.blockType = data.blockType;

        // Görseli ayarla
        newBlock.SetVisual(data.visualSprite, data.color, data.width);

        // YENİ: Kimlikleri aktarıyoruz
        if (data.isRock) newBlock.SetRock(true);
        if (data.isFrozen) newBlock.SetFrozen(true, iceSprite); // iceSprite'ı parametre olarak gönderdik
        if (data.isChained) newBlock.SetChained(true);

        activeBlocks.Add(newBlock);
        newBlock.MoveTo(newBlock.x, newBlock.y);
    }
    RebuildGridMemory();
}

//---------------------//ÖN İZLEME FONKSİYONLARI-----------------------

public void SafeDestroyBlock(Block block)
{
    if (block == null)
        return;

    if (block.isBeingDestroyed)
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

    block.StartCoroutine(block.CrunchAndDestroy(explosionPrefab));
}

public void DestroyBlocksByColor(Color targetColor)
{
    System.Collections.Generic.List<Block> blocksToDestroy = new System.Collections.Generic.List<Block>();

    foreach (Block block in activeBlocks)
    {
        if (block == null)
            continue;

        if (block.isBeingDestroyed)
            continue;

        if (block.blockColor == targetColor && !block.isRock)
        {
            blocksToDestroy.Add(block);
        }
    }

    foreach (Block block in blocksToDestroy)
    {
        SafeDestroyBlock(block);
    }

    RebuildGridMemory();
}

public void TriggerSlice(Block sliceBlock)
{
    if (sliceBlock == null)
        return;

    int targetY = sliceBlock.y;

    for (int x = sliceBlock.x + sliceBlock.width; x < width; x++)
    {
        Block target = gridArray[x, targetY];

        if (target == null)
            continue;

        if (target == sliceBlock)
            continue;

        if (target.isRock)
            return;

        SliceBlock(target);

        return;
    }
}

public void SliceBlock(Block target)
{
    Debug.Log("SLICE TARGET: " + target.name + " width: " + target.width);
    if (target == null)
        return;

    if (target.isBeingDestroyed)
        return;

    // Küçük blok direkt yok olur
    if (target.width <= 2)
    {
        StartCoroutine(SliceDestroyAfterFeedback(target));
        return;
    }

    int originalWidth = target.width;
    int leftWidth = originalWidth / 2;
    int rightWidth = originalWidth - leftWidth;

    int originalX = target.x;
    int y = target.y;

    Color color = target.blockColor;
    Sprite sprite = target.GetComponent<SpriteRenderer>().sprite;

    target.StartCoroutine(target.SliceFeedback());
    SafeDestroyBlock(target);

    CreateSplitBlock(originalX, y, leftWidth, color, sprite);
    CreateSplitBlock(originalX + leftWidth, y, rightWidth, color, sprite);

    RebuildGridMemory();
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

    SafeDestroyBlock(target);
}

}
