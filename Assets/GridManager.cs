using UnityEngine;
using System.Collections; // Coroutine için şart
using UnityEngine.SceneManagement;

public enum GameState { IDLE, MOVING, FALLING, CHECKING, SPAWNING }


public class GridManager : MonoBehaviour
{
    [Header("Renk Ayarları")]
    public Color[] blockColors; // Unity'den 5-6 renk ekle
    [Header("Grid Ayarları")]
    public int width = 8;
    public int height = 10;
    public float cellSize = 1f; // HATAYI DÜZELTEN SATIR

    public GameObject explosionPrefab;

    // Sahnedeki tüm kanlı canlı blokları burada tutacağız
    public System.Collections.Generic.List<Block> activeBlocks = new System.Collections.Generic.List<Block>();
    public static GridManager Instance { get; private set; }

    public Block[,] gridArray;
    public Block blockPrefab;
    public GameState currentState = GameState.IDLE;
    public GameObject cellPrefab; // Az önce oluşturduğumuz Square'i buraya sürükleyeceğiz
    public Color gridColor = new Color(1f, 1f, 1f, 0.1f); // Hafif transparan beyaz/gri

    public bool isGameOver = false;
    public GameObject gameOverPanel; // Unity'den atayacağımız panel

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
    }

    public System.Collections.Generic.List<BlockData> nextRowData = new System.Collections.Generic.List<BlockData>();
    private System.Collections.Generic.List<GameObject> previewVisuals = new System.Collections.Generic.List<GameObject>();
    //ÖN İZLEME HEADER BİTİMİ
  
    void Awake()
    {
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
    // 1. Grid'i tertemiz hazırla
    gridArray = new Block[width, height];
    activeBlocks.Clear();

    // 2. İlk satırı oluştur
    //SpawnRandomRow(0);

    // TEST İÇİN: 0'dan 6. satıra kadar doldur
    for (int i = 0; i <= 8; i++)
    {
        SpawnRandomRow(i);
    }
    
    // 3. KİLİDİ AÇ: Oyunun başında durumu IDLE yapalım ki oyuncu dokunabilsin
    currentState = GameState.IDLE;

    // 4. Sistemi coroutine ile dürt
    StartCoroutine(InitialGravityCheck());
    
    GenerateNextRowData();
    
    // Skor tabelasını da hemen dürtelim
    if(ScoreManager.Instance != null) ScoreManager.Instance.UpdateScoreUI();
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

void TriggerGameOver()
{
    if (isGameOver) return;

    isGameOver = true;

    
    // UI Panelini aç
    if (gameOverPanel != null)
        gameOverPanel.SetActive(true);

    // Oyunu durdur (opsiyonel)
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
IEnumerator InitialCheckRoutine()
{
    yield return new WaitForEndOfFrame();
    yield return StartCoroutine(CheckAndClearRowsRoutine());
    ChangeState(GameState.IDLE);
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

public void SpawnRandomRow(int y)
{
    int currentX = 0;
    int blockCountInRow = 0;

    while (currentX < width)
    {
        // Boşluk bırakma ihtimali
        float gapChance = (blockCountInRow > 2) ? 0.7f : 0.4f; 
        if (Random.value < gapChance) 
        {
            currentX++;
            continue;
        }

        int bWidth = 1;
        float widthRoll = Random.value;

        // YENİ: 4 ve 5 birimlik blok ihtimalleri eklendi
        if (widthRoll > 0.98f) bWidth = 5;      // %2 ihtimal
        else if (widthRoll > 0.94f) bWidth = 4; // %4 ihtimal
        else if (widthRoll > 0.85f) bWidth = 3; // %9 ihtimal
        else if (widthRoll > 0.60f) bWidth = 2; // %25 ihtimal
        else bWidth = 1;                       // %60 ihtimal

        // Senin kullandığın sınır kontrolü (Aynı kalabilir)
        if (currentX + bWidth > width) bWidth = width - currentX;

        SpawnBlockWithColor(currentX, y, bWidth);
        currentX += bWidth;
        blockCountInRow++;
    }
    
    if (blockCountInRow == 0)
    {
        SpawnBlockWithColor(Random.Range(0, width), y, 1);
    }
}

void SpawnBlockWithColor(int x, int y, int bWidth)
{
    Vector3 spawnPos = new Vector3(x + (bWidth - 1) * 0.5f, y, 0);
    Block newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
    newBlock.width = bWidth;
    newBlock.x = x;
    newBlock.y = y;
    newBlock.transform.localScale = new Vector3(bWidth - 0.1f, 0.9f, 1);
    newBlock.SetBlockColor(blockColors[Random.Range(0, blockColors.Length)]);

    activeBlocks.Add(newBlock); // YENİ: Listeye ekle
    RebuildGridMemory(); // Hafızayı baştan kur!
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


// Yardımcı kontrol fonksiyonu
bool CanMoveTo(Block b, int targetX, int targetY)
{
    if (targetY < 0 || targetY >= height) return false;
    
    for (int i = 0; i < b.width; i++)
    {
        // Eğer hedef hücrede başka bir blok varsa (ve o blok kendisi değilse)
        if (gridArray[targetX + i, targetY] != null && gridArray[targetX + i, targetY] != b)
            return false;
    }
    return true;
}



public IEnumerator CheckAndClearRowsRoutine()
{
    // EMNİYET: Eğer bir şekilde liste boşsa direkt IDLE'a dön ve çık
    if (activeBlocks == null || activeBlocks.Count == 0)
    {
        ChangeState(GameState.IDLE);
        yield break;
    }

    //bool rowCleared = false;
    
    // Satırları tara
    int clearedRowCount = 0; // Bu kontrolde kaç satır silindi?

    for (int y = 0; y < height; y++)
    {
        if (IsRowFull(y))
        {
            ClearRow(y);
            clearedRowCount++; // Satır sayısını artır
            y--; 
        }
    }

    if (clearedRowCount > 0)
    {
        // PUAN HESAPLAMA: 
        // 1 satır: 100
        // 2 satır: 300 (Bonuslu)
        // 3 satır: 600 (Daha çok bonus!)
        int pointsToGive = clearedRowCount * 100 * clearedRowCount; 
        // EMNİYET: ScoreManager'ın hayatta olup olmadığını kontrol et
        if (ScoreManager.Instance != null) 
        {
            ScoreManager.Instance.AddScore(pointsToGive);
        }

        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(ApplyGravityRoutine());
        yield return StartCoroutine(CheckAndClearRowsRoutine());
    }
    else
    {
        // PATLAYACAK SATIR KALMADIĞINDA BURAYA DÜŞER
        // Kilidi açan en kritik satır:
        ChangeState(GameState.IDLE); 
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
    // Önce bu satırdaki benzersiz blokları bul
    System.Collections.Generic.List<Block> blocksToDestroy = new System.Collections.Generic.List<Block>();
    for (int x = 0; x < width; x++) {
        Block b = gridArray[x, y];
        if (b != null && !blocksToDestroy.Contains(b)) {
            blocksToDestroy.Add(b);
        }
    }

    // Sonra onları yok et ve listeden çıkar
    foreach (Block b in blocksToDestroy) {
        if (explosionPrefab != null) {
            GameObject effect = Instantiate(explosionPrefab, b.transform.position, Quaternion.identity);
            var main = effect.GetComponent<ParticleSystem>().main;

            // sr.color artık beyaz değil, SetBlockColor'dan gelen gerçek renk!
            Color finalColor = b.GetComponent<SpriteRenderer>().color;
            finalColor.a = 1.0f; 
            
            // MinMaxGradient hatayı engeller ve rengi sisteme paketler
            main.startColor = new ParticleSystem.MinMaxGradient(finalColor);

            Destroy(effect, 1f);
        }
        activeBlocks.Remove(b);
        Destroy(b.gameObject);
    }
    RebuildGridMemory(); // Hafızayı baştan kur!
}

    public void SpawnBlock(int x, int y, int bWidth)
    {
        Vector3 spawnPos = new Vector3(x + (bWidth - 1) * 0.5f, y, 0);
        Block newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
        newBlock.width = bWidth;
        newBlock.MoveTo(x, y);
        for (int i = 0; i < bWidth; i++) gridArray[x + i, y] = newBlock;
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

//---------------------ÖN İZLEME FONKSİYONLARI-----------------------

public void GenerateNextRowData()
    {
        nextRowData.Clear();
        int currentX = 0;
        int blockCountInRow = 0;

        while (currentX < width)
        {
            // Senin güncellediğin 4-5 hücreli zar atma mantığı:
            float gapChance = (blockCountInRow > 2) ? 0.7f : 0.4f; 
            if (Random.value < gapChance) 
            {
                currentX++;
                continue;
            }

            int bWidth = 1;
            float widthRoll = Random.value;
            if (widthRoll > 0.98f) bWidth = 5;      
            else if (widthRoll > 0.94f) bWidth = 4; 
            else if (widthRoll > 0.85f) bWidth = 3; 
            else if (widthRoll > 0.60f) bWidth = 2; 
            else bWidth = 1;                       

            if (currentX + bWidth > width) bWidth = width - currentX;

            // Bloğu OLUŞTURMA, sadece VERİSİNİ kaydet
            BlockData newData = new BlockData();
            newData.x = currentX;
            newData.width = bWidth;
            newData.color = blockColors[Random.Range(0, blockColors.Length)];
            nextRowData.Add(newData);

            currentX += bWidth;
            blockCountInRow++;
        }
        
        // Güvenlik: Satır boş kalamaz
        if (blockCountInRow == 0)
        {
            BlockData newData = new BlockData();
            newData.x = Random.Range(0, width);
            newData.width = 1;
            newData.color = blockColors[Random.Range(0, blockColors.Length)];
            nextRowData.Add(newData);
        }

        // Veri hesaplandı, şimdi görselleri çiz!
        UpdatePreviewVisuals();
    }

private void UpdatePreviewVisuals()
    {
        // 1. Eski önizleme görsellerini yok et
        foreach (GameObject obj in previewVisuals) { Destroy(obj); }
        previewVisuals.Clear();

        // 2. Yeni önizlemeleri oluştur
        foreach (BlockData data in nextRowData)
        {
            // Pozisyon: -1.0f yaparak tam gridin (0. satırın) altına "yapıştırıyoruz"
            Vector3 spawnPos = new Vector3(data.x + (data.width - 1) * 0.5f, -1.0f, 0);
            
            Block previewBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
            previewBlock.gameObject.name = "PreviewBlock";
            
            // Fizik ve hareketleri kapat
            previewBlock.enabled = false; 
            if (previewBlock.TryGetComponent<Collider2D>(out Collider2D col)) 
                col.enabled = false;

            // GÖRSEL DÜZENLEME 1: İlham oyunundaki gibi altta "basık / yarım" görünsün
            // Normalde Y scale 0.9f idi, bunu 0.5f yaparak yarısını gizlenmiş gibi gösteriyoruz.
            // Ayrıca pozisyonunu hafifçe aşağı kaydırarak tam hizalıyoruz
            previewBlock.transform.localScale = new Vector3(data.width - 0.1f, 0.5f, 1);
            previewBlock.transform.position -= new Vector3(0, 0.25f, 0); 

            // GÖRSEL DÜZENLEME 2: Alpha yerine URP Emission'ı "boğmak" için rengi koyulaştır (Gölgeli)
            // RGB değerlerini %30'una (0.3f) düşürüyoruz. Bu neon parlamayı söndürür.
            Color shadowColor = new Color(
                data.color.r * 0.3f, 
                data.color.g * 0.3f, 
                data.color.b * 0.3f, 
                1f
            );
            previewBlock.SetBlockColor(shadowColor);

            // GÖRSEL DÜZENLEME 3: Gridin/Efektlerin Arkasında Dursun
            SpriteRenderer sr = previewBlock.GetComponent<SpriteRenderer>();
            sr.sortingOrder = -5; // Alttan/derinden geliyormuş hissi için

            previewVisuals.Add(previewBlock.gameObject);
        }
    }
public void SpawnRowFromData(int y)
    {
        foreach (BlockData data in nextRowData)
        {
            // 1. DEĞİŞİKLİK: Görsel olarak doğrudan Y=0'da değil, Y-1'de (yerin altında) doğuruyoruz.
            Vector3 spawnPos = new Vector3(data.x + (data.width - 1) * 0.5f, y - 1f, 0); 
            
            Block newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
            
            newBlock.width = data.width;
            newBlock.x = data.x;
            newBlock.y = y; // Mantıksal olarak hedefi hala 0. satır
            
            newBlock.transform.localScale = new Vector3(data.width - 0.1f, 0.9f, 1);
            newBlock.SetBlockColor(data.color);

            activeBlocks.Add(newBlock);

            // 2. DEĞİŞİKLİK: Yeni doğan bloğa da "hedefine doğru hareket et" emri veriyoruz.
            // Böylece üstteki bloklar 0'dan 1'e giderken, bu da -1'den 0'a onlarla beraber gidecek.
            newBlock.MoveTo(newBlock.x, newBlock.y);
        }
        RebuildGridMemory();
    }
    
//---------------------ÖN İZLEME FONKSİYONLARI-----------------------




}


