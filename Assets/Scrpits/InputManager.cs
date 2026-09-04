using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

public class InputManager : MonoBehaviour
{
    private static readonly bool TutorialInputHooksEnabled = true;

    public static event Action UserInputStarted;
    public static event Action SuccessfulPlacement;

    private const float DragOriginGhostAlpha = 0.45f;
    private const int DragOriginGhostBaseSortingOrder = 9;

    public GridManager grid;
    [SerializeField] private PlacementGuide placementGuide;
    private FirstTimeTutorial firstTimeTutorial;
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
    private bool isTutorialPreviewDrag = false;
    private Vector3 tutorialPreviewDragOffset;
    private readonly List<Vector2Int> heldBlockOriginCells = new List<Vector2Int>();
    private GameObject dragOriginGhost;

    void Awake()
    {
        mainCam = Camera.main;
        if (grid == null)
        {
            grid = GridManager.Instance;
        }

        if (placementGuide == null)
        {
            placementGuide = FindAnyObjectByType<PlacementGuide>();
        }

        if (placementGuide == null)
        {
            GameObject guideRoot = new GameObject("PlacementGuide");
            placementGuide = guideRoot.AddComponent<PlacementGuide>();
        }

        firstTimeTutorial = TutorialInputHooksEnabled
            ? FindAnyObjectByType<FirstTimeTutorial>()
            : null;
        placementGuide.ConfigureFromGrid(grid);
        placementGuide.Hide();
    }

void Update()
{
    if (Input.GetMouseButtonDown(0))
    {
        UserInputStarted?.Invoke();
    }

    if (IsBoardBusy())
    {
        CancelActiveDrag();
        return;
    }

    /// 1. ADIM: Mouse/Parmak Basıldığı An
    if (Input.GetMouseButtonDown(0))
    {
        if (IsBoardBusy())
        {
            return;
        }

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            selectedBlock = hit.collider.GetComponent<Block>();

            // === YENİ KONTROL: Eğer seçilen blok varsa ve KAYA DEĞİLSE hareketine izin ver! ===
            if (selectedBlock != null &&
                !selectedBlock.isRock &&
                !selectedBlock.isChained &&
                CanSelectBlock(selectedBlock))
            {
                touchStartPos = mousePos;
                blockStartGridPos = selectedBlock.transform.position;
                isDragging = false;
                originalGridX = selectedBlock.x;
                blockStartWorldPos = selectedBlock.transform.position;
                CreateDragOriginGhost(selectedBlock);

                if (TutorialInputHooksEnabled &&
                    firstTimeTutorial != null &&
                    firstTimeTutorial.ShouldUsePreviewDrag(selectedBlock))
                {
                    tutorialPreviewDragOffset = selectedBlock.transform.position -
                        new Vector3(mousePos.x, mousePos.y, selectedBlock.transform.position.z);
                    isTutorialPreviewDrag = true;
                    HidePlacementGuide();
                    firstTimeTutorial.NotifyDragStarted();
                    return;
                }

                isTutorialPreviewDrag = false;
                CacheHeldBlockOriginCells(selectedBlock);
                CalculateDragLimits(selectedBlock);
                selectedBlock.SetHighlight(true);
                UpdatePlacementGuide();
                firstTimeTutorial?.NotifyDragStarted();
                
            }
            else
            {
                // Eğer kaya veya kafesli (zincirli) blok ise taşınamadığı geribildirimini ver
                if (selectedBlock != null && (selectedBlock.isRock || selectedBlock.isChained))
                {
                    selectedBlock.PlayImmovableShake();
                }

                // Seçimi iptal et, oyuncu onu tutamasın.
                selectedBlock = null; 
            }
        }
    }

        // 2. ADIM: Mouse/Parmak Basılı Tutulurken
        if (Input.GetMouseButton(0) && selectedBlock != null)
        {
            Vector2 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            if (isTutorialPreviewDrag)
            {
                selectedBlock.transform.position = new Vector3(
                    currentMousePos.x + tutorialPreviewDragOffset.x,
                    currentMousePos.y + tutorialPreviewDragOffset.y,
                    blockStartWorldPos.z
                );

                isDragging = Vector2.Distance(currentMousePos, touchStartPos) > 0.05f;
            }
            else
            {
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
                UpdatePlacementGuide();
            }
            
        }


    // 3. ADIM: Mouse/Parmak Bırakıldığı An
    if (Input.GetMouseButtonUp(0))
    {
        if (IsBoardBusy())
        {
            CancelActiveDrag();
            return;
        }

        //if (selectedBlock != null && isDragging)
        if (selectedBlock != null)
        {
            if (isTutorialPreviewDrag)
            {
                DestroyDragOriginGhost();
                HidePlacementGuide();
                bool committed = firstTimeTutorial != null &&
                    firstTimeTutorial.TryCommitActivePreviewBlock(selectedBlock.transform.position);

                if (committed)
                {
                    SuccessfulPlacement?.Invoke();
                    StartCoroutine(FinishMovementRoutine());
                }
                else
                {
                    firstTimeTutorial?.ResetActivePreviewBlock();
                }

                selectedBlock = null;
                isDragging = false;
                isTutorialPreviewDrag = false;
                ClearHeldBlockOriginCells();
                HidePlacementGuide();
                return;
            }

            selectedBlock.SetHighlight(false); // <--- YENİ: Parmağı çekince parlaklık normale dönsün
            DestroyDragOriginGhost();
            HidePlacementGuide();
            
            int snappedX = GetSnappedX(selectedBlock);

            if (!IsAllowedPlacement(selectedBlock, snappedX))
            {
                selectedBlock.transform.position = blockStartWorldPos;
                selectedBlock.MoveTo(originalGridX, selectedBlock.y);
                selectedBlock = null;
                isDragging = false;
                ClearHeldBlockOriginCells();
                HidePlacementGuide();
                return;
            }

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
                SuccessfulPlacement?.Invoke();
                StartCoroutine(FinishMovementRoutine());
            }
            else
            {
                //Debug.Log("Blok eski yerine döndü, hamle sayılmadı.");
            }
        }

        selectedBlock = null;
        isDragging = false;
        isTutorialPreviewDrag = false;
        DestroyDragOriginGhost();
        ClearHeldBlockOriginCells();
        HidePlacementGuide();
    }

}

private bool IsBoardBusy()
{
    if (grid == null)
    {
        grid = GridManager.Instance;
    }

    return grid != null && grid.IsBoardBusy;
}

private bool CanSelectBlock(Block block)
{
    if (firstTimeTutorial == null)
    {
        if (!TutorialInputHooksEnabled)
            return true;

        firstTimeTutorial = FirstTimeTutorial.Instance != null
            ? FirstTimeTutorial.Instance
            : FindAnyObjectByType<FirstTimeTutorial>();
    }

    return firstTimeTutorial == null || firstTimeTutorial.CanSelectBlock(block);
}

private bool IsAllowedPlacement(Block block, int snappedX)
{
    if (firstTimeTutorial == null)
    {
        if (!TutorialInputHooksEnabled)
            return true;

        firstTimeTutorial = FirstTimeTutorial.Instance != null
            ? FirstTimeTutorial.Instance
            : FindAnyObjectByType<FirstTimeTutorial>();
    }

    return firstTimeTutorial == null || firstTimeTutorial.IsCorrectPlacement(block, snappedX);
}

private void CancelActiveDrag()
{
    if (selectedBlock != null)
    {
        selectedBlock.SetHighlight(false);

        if (TutorialInputHooksEnabled && isTutorialPreviewDrag)
            firstTimeTutorial?.ResetActivePreviewBlock();
        else
            selectedBlock.transform.position = blockStartWorldPos;
    }

    selectedBlock = null;
    isDragging = false;
    isTutorialPreviewDrag = false;
    DestroyDragOriginGhost();
    ClearHeldBlockOriginCells();
    HidePlacementGuide();
}



// Yolun boş olup olmadığını kontrol eden yardımcı fonksiyon
bool IsPathClear(Block b, int targetX)
{
    if (b == null || grid == null)
        return false;

    return grid.CanFitBlockAt(targetX, b.y, b.width, b);
}

private void UpdatePlacementGuide()
{
    if (placementGuide == null || selectedBlock == null || grid == null)
        return;

    int snappedX = GetSnappedX(selectedBlock);
    bool canPlaceAtSnappedPosition =
        snappedX >= minAllowedX &&
        snappedX <= maxAllowedX &&
        IsPathClear(selectedBlock, snappedX);

    if (canPlaceAtSnappedPosition)
    {
        placementGuide.Show(grid, selectedBlock, snappedX, selectedBlock.y, heldBlockOriginCells);
    }
    else
    {
        placementGuide.Hide();
    }
}

private void HidePlacementGuide()
{
    if (placementGuide != null)
    {
        placementGuide.Hide();
    }
}

private void CreateDragOriginGhost(Block sourceBlock)
{
    DestroyDragOriginGhost();

    if (sourceBlock == null)
        return;

    dragOriginGhost = Instantiate(sourceBlock.gameObject, blockStartWorldPos, sourceBlock.transform.rotation);
    dragOriginGhost.name = $"{sourceBlock.gameObject.name}_DragOriginGhost";

    RemoveGhostInteractionAndLogic(dragOriginGhost);
    ConfigureGhostRenderers(sourceBlock, dragOriginGhost);
}

private void DestroyDragOriginGhost()
{
    if (dragOriginGhost == null)
        return;

    Destroy(dragOriginGhost);
    dragOriginGhost = null;
}

private void RemoveGhostInteractionAndLogic(GameObject ghost)
{
    if (ghost == null)
        return;

    Collider2D[] colliders = ghost.GetComponentsInChildren<Collider2D>(true);
    foreach (Collider2D collider in colliders)
    {
        collider.enabled = false;
        Destroy(collider);
    }

    Rigidbody2D[] rigidbodies = ghost.GetComponentsInChildren<Rigidbody2D>(true);
    foreach (Rigidbody2D rigidbody in rigidbodies)
    {
        Destroy(rigidbody);
    }

    TrailRenderer[] trails = ghost.GetComponentsInChildren<TrailRenderer>(true);
    foreach (TrailRenderer trail in trails)
    {
        trail.Clear();
        trail.enabled = false;
        Destroy(trail);
    }

    ParticleSystem[] particleSystems = ghost.GetComponentsInChildren<ParticleSystem>(true);
    foreach (ParticleSystem particleSystem in particleSystems)
    {
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(particleSystem);
    }

    MonoBehaviour[] behaviours = ghost.GetComponentsInChildren<MonoBehaviour>(true);
    foreach (MonoBehaviour behaviour in behaviours)
    {
        behaviour.enabled = false;
        Destroy(behaviour);
    }
}

private void ConfigureGhostRenderers(Block sourceBlock, GameObject ghost)
{
    SpriteRenderer sourceRenderer = sourceBlock.GetComponent<SpriteRenderer>();
    int sourceBaseSortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder : 0;

    SpriteRenderer[] renderers = ghost.GetComponentsInChildren<SpriteRenderer>(true);
    foreach (SpriteRenderer renderer in renderers)
    {
        renderer.sortingOrder = DragOriginGhostBaseSortingOrder + (renderer.sortingOrder - sourceBaseSortingOrder);
        renderer.color = GetGhostColor(renderer.color);
        renderer.maskInteraction = SpriteMaskInteraction.None;
    }

    if (sourceBlock != null && sourceBlock.isFrozen)
    {
        Transform srcIce = sourceBlock.transform.Find("IceVisual");
        Transform ghostIce = ghost.transform.Find("IceVisual");
        if (srcIce != null && ghostIce != null)
        {
            SpriteRenderer srcIceSr = srcIce.GetComponent<SpriteRenderer>();
            SpriteRenderer ghostIceSr = ghostIce.GetComponent<SpriteRenderer>();
            if (srcIceSr != null && ghostIceSr != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                srcIceSr.GetPropertyBlock(mpb);
                ghostIceSr.SetPropertyBlock(mpb);
            }
        }
    }
}

private Color GetGhostColor(Color sourceColor)
{
    float luminance = sourceColor.r * 0.299f + sourceColor.g * 0.587f + sourceColor.b * 0.114f;
    Color desaturated = Color.Lerp(sourceColor, new Color(luminance, luminance, luminance, sourceColor.a), 0.35f);
    desaturated *= 0.8f;
    desaturated.a = DragOriginGhostAlpha;
    return desaturated;
}

private void CacheHeldBlockOriginCells(Block block)
{
    heldBlockOriginCells.Clear();

    if (block == null)
        return;

    for (int i = 0; i < block.width; i++)
    {
        heldBlockOriginCells.Add(new Vector2Int(block.x + i, block.y));
    }
}

private void ClearHeldBlockOriginCells()
{
    heldBlockOriginCells.Clear();
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

    FirstTimeTutorial activeTutorial = firstTimeTutorial != null ? firstTimeTutorial : FirstTimeTutorial.Instance;
    if (TutorialInputHooksEnabled && activeTutorial != null && activeTutorial.IsRunning)
    {
        yield return StartCoroutine(activeTutorial.PlaySuccessBeforePushUp());
    }
    
    // 4. BOARD'U YUKARI İT VE SÜRECİ BİTİR
    yield return StartCoroutine(grid.PushBoardUpRoutine());

    if (TutorialInputHooksEnabled && activeTutorial != null && activeTutorial.IsRunning)
    {
        yield return StartCoroutine(activeTutorial.CompleteAfterPushUp());
    }

    grid.ChangeState(GameState.IDLE);
    
    // Yeni bir özel blok geldiyse ve ilk kez görünüyorsa tanıtımını tetikle
    SpecialBlockIntroManager.Instance?.CheckActiveBoardForSpecialIntros(grid);
    
    // === YENİ EKLENEN KISIM ===
    // Tahta tamamen duruldu, patlamalar bitti. Hamle hakkı bitmiş mi ŞİMDİ kontrol et!
    if (LevelManager.Instance != null && LevelManager.Instance.enabled)
    {
        LevelManager.Instance.EvaluateEndOfTurn();
    }
    // ===========================

}


}
