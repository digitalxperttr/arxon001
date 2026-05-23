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
    private bool isDragging = false;
    public float dragThreshold = 0.5f;
    private int originalGridX;
    private Vector3 blockStartWorldPos;
    private int minAllowedX;
    private int maxAllowedX;

    void Awake() => mainCam = Camera.main;

void Update()
{
    /// 1. ADIM: Mouse/Parmak Basıldığı An
    if (Input.GetMouseButtonDown(0))
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            selectedBlock = hit.collider.GetComponent<Block>();

            // === YENİ KONTROL: Eğer seçilen blok varsa ve KAYA DEĞİLSE hareketine izin ver! ===
            if (selectedBlock != null && !selectedBlock.isRock && !selectedBlock.isChained)
            {
                touchStartPos = mousePos;
                blockStartGridPos = selectedBlock.transform.position;
                isDragging = false;
                originalGridX = selectedBlock.x;
                blockStartWorldPos = selectedBlock.transform.position;
                CalculateDragLimits(selectedBlock);
                selectedBlock.SetHighlight(true);
                
            }
            else
            {
                // Eğer kayaysa (veya blok yoksa) seçimi iptal et, oyuncu onu tutamasın.
                selectedBlock = null; 
            }
        }
    }

        // 2. ADIM: Mouse/Parmak Basılı Tutulurken
        if (Input.GetMouseButton(0) && selectedBlock != null)
        {
            Vector2 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            float deltaX = currentMousePos.x - touchStartPos.x;

            float minWorldX = minAllowedX + (selectedBlock.width - 1) * 0.5f;
            float maxWorldX = maxAllowedX + (selectedBlock.width - 1) * 0.5f;

            float targetWorldX = Mathf.Clamp(blockStartWorldPos.x + deltaX, minWorldX, maxWorldX);

            selectedBlock.transform.position = new Vector3(
                targetWorldX,
                blockStartWorldPos.y,
                blockStartWorldPos.z
            );

            isDragging = Mathf.Abs(deltaX) > 0.05f;
            
        }


    // 3. ADIM: Mouse/Parmak Bırakıldığı An
    if (Input.GetMouseButtonUp(0))
    {
        //if (selectedBlock != null && isDragging)
        if (selectedBlock != null)
        {
            selectedBlock.SetHighlight(false); // <--- YENİ: Parmağı çekince parlaklık normale dönsün
            
            int snappedX = GetSnappedX(selectedBlock);
            grid.UpdateBlockInGrid(selectedBlock, snappedX, selectedBlock.y);
            selectedBlock.MoveTo(snappedX, selectedBlock.y);
            
            // YENİ KONTROL: Eğer blok başladığı kareye geri döndüyse hamle sayma!
            if (snappedX != originalGridX)
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


void CalculateDragLimits(Block block)
{
    minAllowedX = block.x;
    maxAllowedX = block.x;

    // Sola doğru kaç hücre gidebilir?
    while (minAllowedX > 0)
    {
        int checkX = minAllowedX - 1;

        if (grid.gridArray[checkX, block.y] != null)
            break;

        minAllowedX--;
    }

    // Sağa doğru kaç hücre gidebilir?
    while (maxAllowedX + block.width < grid.width)
    {
        int checkX = maxAllowedX + block.width;

        if (grid.gridArray[checkX, block.y] != null)
            break;

        maxAllowedX++;
    }
}

int GetSnappedX(Block block)
{
    float leftEdgeWorldX = block.transform.position.x - (block.width - 1) * 0.5f;
    int snappedX = Mathf.RoundToInt(leftEdgeWorldX);

    snappedX = Mathf.Clamp(snappedX, minAllowedX, maxAllowedX);

    return snappedX;
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