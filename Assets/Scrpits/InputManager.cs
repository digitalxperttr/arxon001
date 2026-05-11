using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public GridManager grid;
    private Camera mainCam;
    private Block selectedBlock;
    private Vector2 startTouchPos;
    private Vector2 touchStartPos;
    private Vector2 blockStartGridPos; // Bloğun başladığı grid koordinatı
    private GameObject SelectedBlock; // Seçili olan bloğu hafızada tutmak için
    private bool isDragging = false;
    public float dragThreshold = 0.5f;
    private int originalGridX;

    void Awake() => mainCam = Camera.main;

void Update()
{
    // 1. ADIM: Mouse/Parmak Basıldığı An
    
    if (Input.GetMouseButtonDown(0))
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            // Tıklanan objede Block scriptini bul
            selectedBlock = hit.collider.GetComponent<Block>();

            if (selectedBlock != null)
            {
                // Başlangıç verilerini kaydet
                touchStartPos = mousePos;
                blockStartGridPos = selectedBlock.transform.position;
                isDragging = false;
                originalGridX = selectedBlock.x;
            }
        }
    }

        // 2. ADIM: Mouse/Parmak Basılı Tutulurken
        if (Input.GetMouseButton(0) && selectedBlock != null)
        {
            Vector2 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            // Mevcut fare pozisyonu ile son işlem yapılan pozisyon arasındaki fark
            float diff = currentMousePos.x - touchStartPos.x;

            // Hassasiyeti burada ayarlıyoruz (0.5f idealdir, istersen 0.3f yapabilirsin)
            float threshold = 0.5f; 

            if (Mathf.Abs(diff) >= threshold)
            {
                int direction = diff > 0 ? 1 : -1;
                
                // Bloğu 1 birim kaydırmayı dene
                TryMoveBlock(selectedBlock, direction); 
                
                // --- KRİTİK DÜZELTME ---
                // Başlangıç pozisyonunu 'direction' kadar değil, fareye göre güncelle.
                // Böylece fare hareketine devam ettikçe blok da bir sonraki kareye geçer.
                touchStartPos.x += direction; 
                
                isDragging = true;
            }
        }


    // 3. ADIM: Mouse/Parmak Bırakıldığı An
    if (Input.GetMouseButtonUp(0))
    {
        if (selectedBlock != null && isDragging)
        {
            // YENİ KONTROL: Eğer blok başladığı kareye geri döndüyse hamle sayma!
            if (selectedBlock.x != originalGridX)
            {
                // === YENİ EKLENEN: Hamle sayacından 1 düş! ===
                if (LevelManager.Instance != null && LevelManager.Instance.enabled) 
                {
                    LevelManager.Instance.PlayerDidMove();
                }
                // ==============================================
                
                // Sadece blok farklı bir karedeyse yeni satır ekle ve kontrol yap
                StartCoroutine(FinishMovementRoutine());
            }
            else
            {
                //Debug.Log("Blok eski yerine döndü, hamle sayılmadı.");
            }
        }

        selectedBlock = null;
        isDragging = false;
    }

}

void HandleDragging()
{
    Vector2 currentMousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
    float deltaX = currentMousePos.x - startTouchPos.x;

    // Kaç hücre kaymak istediğini hesapla (örneğin 1.2 hücre çektiyse 1 hücre)
    int stepDelta = Mathf.RoundToInt(deltaX / grid.cellSize);
    int targetX = selectedBlock.x + stepDelta;

    // Hedef X'in sınır dışına çıkmasını engelle
    targetX = Mathf.Clamp(targetX, 0, grid.width - selectedBlock.width);

    // YOL KONTROLÜ: Başlangıç noktasından hedef noktasına kadar yol boş mu?
    int finalTargetX = selectedBlock.x;
    int direction = (targetX > selectedBlock.x) ? 1 : -1;

    for (int i = 1; i <= Mathf.Abs(targetX - selectedBlock.x); i++)
    {
        int checkX = selectedBlock.x + (i * direction);
        if (IsPathClear(selectedBlock, checkX))
        {
            finalTargetX = checkX;
        }
        else
        {
            break; // Engel varsa daha ileri gidemezsin
        }
    }

    // GÖRSEL TAKİP: Blok parmağı takip etsin ama sadece izin verilen hücreye kadar
    // Hafif bir yumuşatma (Lerp) eklersen daha şık durur
    float visualTargetX = finalTargetX + (selectedBlock.width - 1) * 0.5f;
    selectedBlock.transform.position = Vector3.Lerp(selectedBlock.transform.position, 
        new Vector3(visualTargetX, selectedBlock.y, 0), Time.deltaTime * 20f);
}

// Yolun boş olup olmadığını kontrol eden yardımcı fonksiyon
bool IsPathClear(Block b, int targetX)
{
    for (int i = 0; i < b.width; i++)
    {
        Block other = grid.gridArray[targetX + i, b.y];
        if (other != null && other != b) return false;
    }
    return true;
}

    void HandleTouchStart()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null && hit.collider.TryGetComponent(out Block block))
        {
            selectedBlock = block;
            startTouchPos = mousePos;
        }
    }

void HandleTouchEnd()
{
    if (selectedBlock != null)
    {
        // 1. HATA ÇÖZÜMÜ: Bloğun dünyadaki X pozisyonundan griddeki X koordinatını hesapla
        // Matematiksel olarak: (Pozisyon - (Genişlik offset)) = Grid X
        float visualX = selectedBlock.transform.position.x;
        float offset = (selectedBlock.width - 1) * 0.5f;
        int finalX = Mathf.RoundToInt(visualX - offset);

        // Sınırları koru (Dışarı taşmasın)
        finalX = Mathf.Clamp(finalX, 0, grid.width - selectedBlock.width);

        // 2. HAFIZAYI GÜNCELLE: Nükleer modelimizi çağırıyoruz
        grid.UpdateBlockInGrid(selectedBlock, finalX, selectedBlock.y);
        
        // 3. SNAP (MIKNATIS): Bloğu tam koordinatına pürüzsüzce oturt
        selectedBlock.MoveTo(selectedBlock.x, selectedBlock.y);

        // 4. SÜRECİ BİTİR
        StartCoroutine(FinishMovementRoutine());
    }

    selectedBlock = null;
    // isDragging varsa onu da false yapabilirsin
}

void TryMoveBlockWithSteps(Block block, int direction, int maxSteps)
{
    int finalTargetX = block.x;
    int stepsTaken = 0;

    // Oyuncunun istediği adım sayısı kadar VEYA engele çarpana kadar ilerle
    while (stepsTaken < maxSteps)
    {
        int nextX = finalTargetX + direction;
        
        // Sınır ve Boşluk Kontrolü
        if (nextX >= 0 && nextX + (block.width - 1) < grid.width && 
            grid.gridArray[nextX + (direction == 1 ? block.width - 1 : 0), block.y] == null)
        {
            finalTargetX = nextX;
            stepsTaken++;
        }
        else break; // Engele çarptık
    }

    if (finalTargetX != block.x)
    {
        grid.ChangeState(GameState.MOVING);
        grid.UpdateBlockInGrid(block, finalTargetX, block.y);
        block.MoveTo(finalTargetX, block.y);
        StartCoroutine(FinishMovementRoutine());
    }
}

void TryMoveBlock(Block block, int direction)
{
    // Hedefimiz: Mevcut konumdan sadece 1 birim ötesi
    int nextX = block.x + direction;

    // 1. ADIM: Sınır Kontrolü (Grid dışına çıkıyor mu?)
    // 2. ADIM: Boşluk Kontrolü (Gideceği yerdeki hücreler null mı?)
    // Bloğun genişliğini (width) hesaba katarak kontrol ediyoruz.
    if (nextX >= 0 && nextX + (block.width - 1) < grid.width && 
        grid.gridArray[nextX + (direction == 1 ? block.width - 1 : 0), block.y] == null)
    {
        // Hareket onaylandı: Durumu MOVING yap
        grid.ChangeState(GameState.MOVING); 

        // Hafızayı (gridArray) güncelle
        grid.UpdateBlockInGrid(block, nextX, block.y); 

        // Bloğu görsel olarak yeni koordinatına kaydır[cite: 4]
        block.MoveTo(nextX, block.y); 

        // ÖNEMLİ: FinishMovementRoutine burada kapalı kalmalı![cite: 4]
        // Sadece parmağını bıraktığında (3. Adım) tetiklenecek.
    }
}


System.Collections.IEnumerator FinishMovementRoutine()
{
    // Eğer seçili blok yoksa (hızlı tıklama vs.) fonksiyonu burada bitir
    if (selectedBlock == null) 
    {
        yield break; 
    }
    // 1. Görsel hareketlerin bitmesini bekle
    while (grid.AreBlocksMoving()) yield return null;
    
    // 2. ÖNCE YERÇEKİMİ: Boşluklara düşenler bir yerleşsin
    grid.ChangeState(GameState.FALLING);
    yield return StartCoroutine(grid.ApplyGravityRoutine());

    // 3. PATLAMA VE KOMBO: 
    // Satır patladıkça yerçekimi tekrar çalışmalı (Özyinelemeli kontrol)
    grid.ChangeState(GameState.CHECKING);
    yield return StartCoroutine(grid.CheckAndClearRowsRoutine(true));
    
    // 4. BOARD'U YUKARI İT VE SÜRECİ BİTİR
    yield return StartCoroutine(grid.PushBoardUpRoutine());
    grid.ChangeState(GameState.IDLE);
    
    // === YENİ EKLENEN KISIM ===
    // Tahta tamamen duruldu, patlamalar bitti. Hamle hakkı bitmiş mi ŞİMDİ kontrol et!
    if (LevelManager.Instance != null && LevelManager.Instance.enabled)
    {
        LevelManager.Instance.EvaluateEndOfTurn();
    }
    // ===========================

}


}