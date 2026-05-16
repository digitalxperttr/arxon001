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
            selectedBlock.SetHighlight(false); // <--- YENİ: Parmağı çekince parlaklık normale dönsün
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