using UnityEngine;
using System.Collections; // Coroutine için şart
using System.Collections.Generic;


public class Block : MonoBehaviour
{
    private const float ChainOverlayCellWidth = 1f;
    private const float ChainOverlayCellHeight = 0.99f;
    private const float ChainOverlayPaddingMultiplier = 1.05f;

    public int x, y, width;
    public bool isMoving = false;
    public bool isBeingDestroyed = false;
    private Vector2 originalSize; // Bloğun normal boyutunu aklında tutması için

    public Color blockColor;
    private Coroutine popCoroutine; // Büyüme animasyonunu takip etmek için
    
    [Header("Hareket Ayarları")]
    public float moveSpeed = 15f; // Kayma hızı, ihtiyaca göre artırabilirsin
    private Vector3 targetPosition;

    [Header("Block Type")]
    public BlockType blockType = BlockType.Normal;
    private IBlockEffect blockEffect;
    public bool isFrozen = false;
    public bool isRock = false;
    public bool isChained = false;

    [Header("Collectible Carrier")]
    public bool hasCollectible = false;
    public string collectibleId;
    [SerializeField] private SpriteRenderer collectibleVisualRenderer;
    private bool collectibleCollected = false;

    [Header("Chain System")]
    [SerializeField] private Sprite chainStage1Sprite;
    [SerializeField] private Sprite chainStage2Sprite;
    [SerializeField] private Transform chainOverlayRoot;
    [SerializeField] private GameObject chainOverlayPrefab;
    [SerializeField] private GameObject chainBreakFXPrefab;
    [SerializeField] private SpriteRenderer flashOverlayRenderer;
    [SerializeField] private Transform vfxAnchor;
    [SerializeField] private float chainBreakFlashDuration = 0.10f;
    [SerializeField] private float chainBreakShakeDuration = 0.15f;
    [SerializeField] private float chainBreakShakeStrength = 0.04f;
    [SerializeField] private float chainBreakPunchScale = 1.06f;
    private const int MaxChainHealth = 2;
    private int chainHealth = 0;
    private readonly List<GameObject> spawnedChainOverlays = new List<GameObject>();
    private static bool hasLoggedMissingChainSpriteWarning = false;
    private GameObject iceVisual; // Üzerine eklenecek buz katmanı
    private Coroutine chainBreakFeedbackRoutine;
    private bool isChainFlashPlaying = false;
    private MaterialPropertyBlock mpb;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Shader'daki renk değişkeninin adı, URP'de genelde "_BaseColor" veya "_EmissionColor" olabilir.
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static Material sharedRuntimeGrayscaleMaterial;
    private readonly Dictionary<SpriteRenderer, Color> gameOverOriginalRendererColors = new Dictionary<SpriteRenderer, Color>();
    private readonly Dictionary<SpriteRenderer, Material> gameOverOriginalRendererMaterials = new Dictionary<SpriteRenderer, Material>();
    private readonly Dictionary<SpriteRenderer, bool> gameOverOriginalRendererEnabledStates = new Dictionary<SpriteRenderer, bool>();
    private bool isGameOverGreyed = false;
    
    [Header("Görsel Efektler")]
    [SerializeField] private Material gameOverGrayscaleMaterial;
    public float glowIntensity = 1.3f; // Seçilince parlaklık çarpanı
    private TrailRenderer trail; // Hız izi (Motion Trail) için
    private SpriteRenderer sr;

    [Header("Fire Idle FX")]
    [SerializeField] private GameObject fireIdleParticlePrefab;

    [SerializeField] private float fireParticleMinDelay = 3f;
    [SerializeField] private float fireParticleMaxDelay = 5f;
    [SerializeField] private int fireParticleMinCount = 3;
    [SerializeField] private int fireParticleMaxCount = 7;
    [SerializeField] private float fireParticleSpawnRadiusX = 0.35f;
    [SerializeField] private float fireParticleSpawnRadiusY = 0.25f;

    private bool isSpecialVisualActive = false;
    private Coroutine fireIdleRoutine;


void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        trail = GetComponent<TrailRenderer>();
        ResolveChainOverlayReferences();
        ResolveCollectibleVisualReferences();
    }

private void Start()
{
    SetupEffect();
    UpdateSpecialVisualState();
}

private void UpdateSpecialVisualState()
{
    isSpecialVisualActive =
        blockType == BlockType.Fire;

    if (isSpecialVisualActive)
    {
        if (fireIdleRoutine == null)
            fireIdleRoutine = StartCoroutine(FireIdleParticleRoutine());
    }
    else
    {
        if (fireIdleRoutine != null)
        {
            StopCoroutine(fireIdleRoutine);
            fireIdleRoutine = null;
        }
    }
}

public void SetHighlight(bool isHighlighted)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        // 1. PARLAMA (Glow): Rengi doğrudan şiddetlendiriyoruz
        float intensity = isHighlighted ? 1.45f : 1.0f;
        sr.color = isHighlighted ? new Color(intensity, intensity, intensity, 1f) : Color.white;

        // 2. KATMAN: Tutulan blok öne çıksın
        sr.sortingOrder = isHighlighted ? 30 : 10;
        if (iceVisual != null && iceVisual.activeSelf)
        {
            iceVisual.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1;
            iceVisual.GetComponent<SpriteRenderer>().color = sr.color;
        }

        RefreshChainOverlays();
        RefreshCollectibleVisual();

        // 3. OVAL IŞIK KESİN ÇÖZÜM: Şalteri tamamen kapat ve izi sil
        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.Clear();
            trail.enabled = false; // emitting yerine direkt bileşeni kapatıyoruz!
        }

        // 4. BÜYÜME (SMOOTH POP): Anında değil, tatlı bir animasyonla büyüsün
        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(AnimatePop(isHighlighted));
    }

    // YENİ EKLENEN ANİMASYON FONKSİYONU
  private IEnumerator AnimatePop(bool isHighlighted)
{
    if (sr == null) sr = GetComponent<SpriteRenderer>();

    float elapsed = 0f;
    float duration = 0.08f;

    // Normal boyut
    Vector2 startSize = sr.size;

    // Highlight olunca biraz büyüsün
    Vector2 targetSize = isHighlighted
        ? new Vector2(originalSize.x * 1.08f, originalSize.y * 1.08f)
        : originalSize;

    while (elapsed < duration)
    {
        sr.size = Vector2.Lerp(startSize, targetSize, elapsed / duration);

        elapsed += Time.deltaTime;
        yield return null;
    }

    sr.size = targetSize;
}
    
public void SetRock(bool rockStatus)
{
    isRock = rockStatus;

    if (isRock)
        blockType = BlockType.Rock;
    else if (!isFrozen && !isChained)
        blockType = BlockType.Normal;
}

public void SetBlockColor(Color c)
    {
        ClearGameOverGreyCache();
        blockColor = c; 
        
        if (mpb == null) mpb = new MaterialPropertyBlock();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorProperty, c);
        mpb.SetColor(BaseColorProperty, c);
        sr.SetPropertyBlock(mpb);

        // === İZ (TRAIL) RENGİNİ DİNAMİK AYARLAMA ===
        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            Gradient gradient = new Gradient();
            
            // Sistemi çökerten kısmı adım adım yazarak aşıyoruz:
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(c, 0.0f);
            colorKeys[1] = new GradientColorKey(c, 1.0f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.6f, 0.0f); // Başı %60 opak
            alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f); // Sonu tam şeffaf
            
            gradient.SetKeys(colorKeys, alphaKeys);
            trail.colorGradient = gradient;
        }
    }
// Trail (iz) rengini güncelleyen yardımcı fonksiyon
    private void UpdateTrailColor(Color c)
    {
        if (trail != null)
        {
            Gradient gradient = new Gradient();
            
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(c, 0.0f);
            colorKeys[1] = new GradientColorKey(c, 1.0f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.6f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f);
            
            gradient.SetKeys(colorKeys, alphaKeys);
            trail.colorGradient = gradient;
        }
    }
    
public void SetVisual(Sprite newSprite, Color colorData, int blockWidth)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        ClearGameOverGreyCache();
        
        sr.sprite = newSprite;
        sr.color = Color.white; 
        blockColor = colorData;
        sr.drawMode = SpriteDrawMode.Sliced;
        
        // Boyutu ayarla ve hafızaya al
        originalSize = new Vector2(blockWidth - 0.01f, 0.99f);
        sr.size = originalSize;
        sr.maskInteraction = SpriteMaskInteraction.None;
        
        // Collider güncelleme
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.size = originalSize;

        SyncFlashOverlay();
        transform.localScale = Vector3.one;
        UpdateSpecialVisualState();
        UpdateTrailColor(colorData);
        RefreshChainOverlays();
        RefreshCollectibleVisual();
    }

public void AssignCollectible(string newCollectibleId, Sprite sprite, bool logAssignment = true)
{
    ResolveCollectibleVisualReferences();

    collectibleId = newCollectibleId;
    hasCollectible = true;
    collectibleCollected = false;

    if (collectibleVisualRenderer != null)
    {
        collectibleVisualRenderer.sprite = sprite;
        collectibleVisualRenderer.enabled = true;
        RefreshCollectibleVisual();
    }

    if (logAssignment)
    {
        Debug.Log($"Assigned collectible: [{collectibleId}]");
    }
}

public void ClearCollectible(bool logClear = true)
{
    string clearedId = collectibleId;

    collectibleId = string.Empty;
    hasCollectible = false;

    if (collectibleVisualRenderer != null)
    {
        collectibleVisualRenderer.sprite = null;
        collectibleVisualRenderer.enabled = false;
    }

    if (logClear && !string.IsNullOrWhiteSpace(clearedId))
    {
        Debug.Log($"Cleared collectible: [{clearedId}]");
    }
}

public bool TryCollectCollectible()
{
    if (collectibleCollected ||
        !hasCollectible ||
        string.IsNullOrWhiteSpace(collectibleId))
    {
        return false;
    }

    string collectedId = collectibleId;
    collectibleCollected = true;

    if (ObjectiveManager.Instance != null)
    {
        ObjectiveManager.Instance.ReportCollectibleCollected(collectedId, 1);
    }

    ClearCollectible(false);
    Debug.Log($"Collected collectible: {collectedId}");
    return true;
}

public bool HasCollectible()
{
    return hasCollectible;
}

public string GetCollectibleId()
{
    return collectibleId;
}

public SpriteRenderer GetCollectibleRenderer()
{
    ResolveCollectibleVisualReferences();
    return collectibleVisualRenderer;
}

public void SetGameOverGreyed(bool greyed)
{
    if (sr == null) sr = GetComponent<SpriteRenderer>();
    if (sr == null) return;

    if (mpb == null) mpb = new MaterialPropertyBlock();

    if (!greyed)
    {
        RestoreGameOverRendererColors();
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorProperty, blockColor);
        mpb.SetColor(BaseColorProperty, blockColor);
        sr.SetPropertyBlock(mpb);
        isGameOverGreyed = false;
        return;
    }

    if (!isGameOverGreyed)
    {
        CacheGameOverRendererColors();
    }

    bool canUseGrayscaleShader = blockType == BlockType.Normal;
    if (canUseGrayscaleShader)
    {
        Material grayscaleMaterial = GetGameOverGrayscaleMaterial();
        sr.color = Color.white;
        if (grayscaleMaterial != null)
        {
            sr.sharedMaterial = grayscaleMaterial;
        }
    }
    else
    {
        sr.color = GetSpecialBlockGameOverTint();
    }

    HideGameOverChildRenderers();

    isGameOverGreyed = true;
}

private void CacheGameOverRendererColors()
{
    gameOverOriginalRendererColors.Clear();
    gameOverOriginalRendererMaterials.Clear();
    gameOverOriginalRendererEnabledStates.Clear();

    SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
    foreach (SpriteRenderer renderer in renderers)
    {
        if (renderer == null)
            continue;

        if (!gameOverOriginalRendererColors.ContainsKey(renderer))
        {
            gameOverOriginalRendererColors.Add(renderer, renderer.color);
        }

        if (!gameOverOriginalRendererMaterials.ContainsKey(renderer))
        {
            gameOverOriginalRendererMaterials.Add(renderer, renderer.sharedMaterial);
        }

        if (!gameOverOriginalRendererEnabledStates.ContainsKey(renderer))
        {
            gameOverOriginalRendererEnabledStates.Add(renderer, renderer.enabled);
        }
    }
}

private void HideGameOverChildRenderers()
{
    SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
    foreach (SpriteRenderer renderer in renderers)
    {
        if (renderer != null && renderer != sr)
        {
            renderer.enabled = false;
        }
    }
}

private void RestoreGameOverRendererColors()
{
    foreach (KeyValuePair<SpriteRenderer, Color> entry in gameOverOriginalRendererColors)
    {
        if (entry.Key != null)
        {
            entry.Key.color = entry.Value;
        }
    }

    foreach (KeyValuePair<SpriteRenderer, Material> entry in gameOverOriginalRendererMaterials)
    {
        if (entry.Key != null)
        {
            entry.Key.sharedMaterial = entry.Value;
        }
    }

    foreach (KeyValuePair<SpriteRenderer, bool> entry in gameOverOriginalRendererEnabledStates)
    {
        if (entry.Key != null)
        {
            entry.Key.enabled = entry.Value;
        }
    }

    gameOverOriginalRendererColors.Clear();
    gameOverOriginalRendererMaterials.Clear();
    gameOverOriginalRendererEnabledStates.Clear();
}

private void ClearGameOverGreyCache()
{
    gameOverOriginalRendererColors.Clear();
    gameOverOriginalRendererMaterials.Clear();
    gameOverOriginalRendererEnabledStates.Clear();
    isGameOverGreyed = false;
}

private Color GetSpecialBlockGameOverTint()
{
    return new Color(0.48f, 0.48f, 0.48f, 1f);
}

private Material GetGameOverGrayscaleMaterial()
{
    if (gameOverGrayscaleMaterial == null)
    {
        gameOverGrayscaleMaterial = Resources.Load<Material>("M_GameOverGrayscale");
    }

    if (gameOverGrayscaleMaterial == null)
    {
        if (sharedRuntimeGrayscaleMaterial == null)
        {
            Shader shader = Shader.Find("ARXON/Sprite Grayscale");
            if (shader != null)
            {
                sharedRuntimeGrayscaleMaterial = new Material(shader)
                {
                    name = "Runtime_GameOverGrayscale"
                };
            }
        }

        gameOverGrayscaleMaterial = sharedRuntimeGrayscaleMaterial;
    }

    return gameOverGrayscaleMaterial;
}

public void SetFrozen(bool frozen, Sprite iceSprite = null)
{
    isFrozen = frozen;
    if (isFrozen)
        blockType = BlockType.Ice;
    else if (!isRock && !isChained)
        blockType = BlockType.Normal;

    if (isFrozen)
    {
        if (iceVisual == null)
        {
            iceVisual = new GameObject("IceVisual");
            iceVisual.transform.SetParent(this.transform);
            // Z değerini -0.1f yaparak "perde" gibi titremesini engelliyoruz
            iceVisual.transform.localPosition = new Vector3(0, 0, -0.1f); 
            iceVisual.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer iceSr = iceVisual.GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        iceSr.sprite = iceSprite;
        iceSr.drawMode = SpriteDrawMode.Sliced;
        
        // Boyutları ana blokla birebir eşitle
        iceSr.size = sr.size; 
        iceSr.color = sr.color;
        iceSr.sortingOrder = sr.sortingOrder + 1;
        
        iceVisual.SetActive(true);
    }
    else
    {
        if (iceVisual != null) iceVisual.SetActive(false);
    }
}

public void SetChained(bool chained)
{
    SetChained(chained ? MaxChainHealth : 0);
}

public void SetChained(int count)
{
    chainHealth = count > 0 ? MaxChainHealth : 0;
    isChained = chainHealth > 0;

    if (isChained)
    {
        blockType = BlockType.Chained;
    }
    else if (!isRock && !isFrozen)
    {
        blockType = BlockType.Normal;
    }

    RefreshChainOverlays();
    UpdateChainVisual();
}

public bool IsChained()
{
    return isChained;
}

public bool BreakOneChain()
{
    if (!isChained)
        return false;

    Vector3 fxPosition = GetChainFXPosition();
    chainHealth--;
    PlayChainBreakFX(fxPosition);

    if (chainBreakFeedbackRoutine != null)
        StopCoroutine(chainBreakFeedbackRoutine);

    chainBreakFeedbackRoutine = StartCoroutine(PlayChainBreakFeedback());

    if (chainHealth <= 0)
    {
        chainHealth = 0;
        isChained = false;

        if (!isRock && !isFrozen)
            blockType = BlockType.Normal;
    }

    RefreshChainOverlays();
    UpdateChainVisual();
    return true;
}

public int GetChainCount()
{
    return chainHealth;
}

public bool DamageChain()
{
    return BreakOneChain();
}

public IEnumerator PlayChainBreakFeedback()
{
    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    Vector3 originalLocalPosition = transform.localPosition;
    Vector3 originalLocalScale = transform.localScale;
    Vector3 punchScale = originalLocalScale * chainBreakPunchScale;
    float elapsed = 0f;

    ShowFlashOverlay();

    while (elapsed < chainBreakShakeDuration)
    {
        float t = elapsed / chainBreakShakeDuration;
        float shakeStrength = chainBreakShakeStrength * (1f - t);
        Vector2 shakeOffset = Random.insideUnitCircle * shakeStrength;

        transform.localPosition = originalLocalPosition + new Vector3(shakeOffset.x, shakeOffset.y, 0f);
        transform.localScale = Vector3.Lerp(punchScale, originalLocalScale, t);

        elapsed += Time.deltaTime;
        yield return null;
    }

    transform.localPosition = originalLocalPosition;
    transform.localScale = originalLocalScale;

    chainBreakFeedbackRoutine = null;
}

private void PlayChainBreakFX()
{
    if (chainBreakFXPrefab == null)
        return;

    Vector3 pos = vfxAnchor != null ? vfxAnchor.position : transform.position;
    Instantiate(chainBreakFXPrefab, pos, Quaternion.identity);
}

private void PlayChainBreakFX(Vector3 pos)
{
    if (chainBreakFXPrefab == null)
        return;

    Instantiate(chainBreakFXPrefab, pos, Quaternion.identity);
}

private void UpdateChainVisual()
{
    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    if (sr == null)
        return;

    Color c = sr.color;
    c.a = 1f;
    sr.color = c;
}

private void ShowFlashOverlay()
{
    SyncFlashOverlay();

    if (flashOverlayRenderer == null)
        return;

    if (!flashOverlayRenderer.gameObject.activeSelf)
        flashOverlayRenderer.gameObject.SetActive(true);

    StopCoroutine(nameof(FlashOverlayRoutine));
    StartCoroutine(nameof(FlashOverlayRoutine));
}

private IEnumerator FlashOverlayRoutine()
{
    Debug.Log("FLASH OVERLAY START: " + flashOverlayRenderer);

    if (flashOverlayRenderer == null)
        yield break;

    SyncFlashOverlay();

    isChainFlashPlaying = true;
    flashOverlayRenderer.color = new Color(1f, 1f, 1f, 1f);
    yield return new WaitForSeconds(0.5f);
    flashOverlayRenderer.color = new Color(1f, 1f, 1f, 0f);
    isChainFlashPlaying = false;
    flashOverlayRenderer.gameObject.SetActive(false);
}

private void SyncFlashOverlay()
{
    if (flashOverlayRenderer == null || sr == null)
        return;

    flashOverlayRenderer.sprite = sr.sprite;
    flashOverlayRenderer.drawMode = sr.drawMode;
    flashOverlayRenderer.size = sr.size;
    flashOverlayRenderer.material = sr.material;
    flashOverlayRenderer.sortingLayerID = sr.sortingLayerID;
    flashOverlayRenderer.sortingOrder = sr.sortingOrder + 20;

    if (flashOverlayRenderer.transform.parent != transform)
        flashOverlayRenderer.transform.SetParent(transform);

    flashOverlayRenderer.transform.localPosition = Vector3.zero;
    flashOverlayRenderer.transform.localRotation = Quaternion.identity;
    flashOverlayRenderer.transform.localScale = Vector3.one;

    if (isChainFlashPlaying)
        return;

    flashOverlayRenderer.color = new Color(1f, 1f, 1f, 0f);

    if (flashOverlayRenderer.gameObject.activeSelf)
        flashOverlayRenderer.gameObject.SetActive(false);
}

private void ResolveChainOverlayReferences()
{
    if (chainOverlayRoot == null)
    {
        Transform existingRoot = transform.Find("ChainOverlayRoot");
        if (existingRoot != null)
        {
            chainOverlayRoot = existingRoot;
        }
        else
        {
            GameObject createdRoot = new GameObject("ChainOverlayRoot");
            Transform parent = sr != null ? sr.transform : transform;
            createdRoot.transform.SetParent(parent, false);
            chainOverlayRoot = createdRoot.transform;
        }
    }

    if (chainOverlayRoot != null)
    {
        chainOverlayRoot.localPosition = Vector3.zero;
        chainOverlayRoot.localRotation = Quaternion.identity;
        chainOverlayRoot.localScale = Vector3.one;

        Transform parent = sr != null ? sr.transform : transform;
        if (chainOverlayRoot.parent != parent)
            chainOverlayRoot.SetParent(parent, false);
    }

    if (chainOverlayPrefab == null)
    {
        Transform existingOverlay = chainOverlayRoot.Find("ChainOverlay");
        if (existingOverlay == null)
            existingOverlay = transform.Find("ChainOverlay");

        if (existingOverlay != null)
        {
            chainOverlayPrefab = existingOverlay.gameObject;
            chainOverlayPrefab.SetActive(false);
        }
    }
}

private void ResolveCollectibleVisualReferences()
{
    if (collectibleVisualRenderer == null)
    {
        Transform existingVisual = transform.Find("CollectibleVisual");
        if (existingVisual == null && sr != null)
            existingVisual = sr.transform.Find("CollectibleVisual");

        if (existingVisual != null)
        {
            collectibleVisualRenderer = existingVisual.GetComponent<SpriteRenderer>();
            if (collectibleVisualRenderer == null)
                collectibleVisualRenderer = existingVisual.gameObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            GameObject visualObject = new GameObject("CollectibleVisual");
            Transform parent = sr != null ? sr.transform : transform;
            visualObject.transform.SetParent(parent, false);
            collectibleVisualRenderer = visualObject.AddComponent<SpriteRenderer>();
        }
    }

    RefreshCollectibleVisual();
}

private void RefreshCollectibleVisual()
{
    if (collectibleVisualRenderer == null)
        return;

    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    Transform visualTransform = collectibleVisualRenderer.transform;
    Transform parent = sr != null ? sr.transform : transform;
    if (visualTransform.parent != parent)
        visualTransform.SetParent(parent, false);

    visualTransform.localPosition = Vector3.zero;
    visualTransform.localRotation = Quaternion.identity;

    if (sr != null)
    {
        collectibleVisualRenderer.sortingLayerID = sr.sortingLayerID;
        collectibleVisualRenderer.sortingOrder = sr.sortingOrder + 3;
    }

    collectibleVisualRenderer.drawMode = SpriteDrawMode.Simple;
    collectibleVisualRenderer.color = Color.white;

    Sprite sprite = collectibleVisualRenderer.sprite;
    if (sprite != null)
    {
        collectibleVisualRenderer.enabled = hasCollectible;

        Vector2 spriteSize = sprite.bounds.size;
        if (spriteSize.x > 0f && spriteSize.y > 0f)
        {
            Vector2 blockSize = originalSize;
            if ((blockSize.x <= 0f || blockSize.y <= 0f) && sr != null)
                blockSize = sr.size;

            float targetSize = Mathf.Min(blockSize.x, blockSize.y) * 0.5f;
            float largestSpriteSide = Mathf.Max(spriteSize.x, spriteSize.y);
            float scale = largestSpriteSide > 0f ? targetSize / largestSpriteSide : 1f;
            visualTransform.localScale = new Vector3(scale, scale, 1f);
        }
        else
        {
            visualTransform.localScale = Vector3.one;
        }
    }
    else
    {
        visualTransform.localScale = Vector3.one;
        collectibleVisualRenderer.enabled = false;
    }
}

private void RefreshChainOverlays()
{
    ResolveChainOverlayReferences();

    for (int i = spawnedChainOverlays.Count - 1; i >= 0; i--)
    {
        if (spawnedChainOverlays[i] == null)
            spawnedChainOverlays.RemoveAt(i);
    }

    int overlayCellCount = Mathf.Clamp(width, 0, 4);

    while (spawnedChainOverlays.Count < overlayCellCount)
    {
        GameObject overlay = CreateChainOverlayInstance(spawnedChainOverlays.Count);
        if (overlay == null)
            break;

        spawnedChainOverlays.Add(overlay);
    }

    for (int i = 0; i < spawnedChainOverlays.Count; i++)
    {
        GameObject overlay = spawnedChainOverlays[i];
        if (overlay == null)
            continue;

        overlay.transform.localPosition = GetChainOverlayLocalPosition(i);
        overlay.SetActive(ShouldShowChainOverlay());
        SyncChainOverlayRenderer(overlay);
    }
}

private GameObject CreateChainOverlayInstance(int overlayIndex)
{
    GameObject overlay;
    if (chainOverlayPrefab != null)
    {
        overlay = Instantiate(chainOverlayPrefab, chainOverlayRoot);
    }
    else
    {
        overlay = new GameObject("ChainOverlay");
        overlay.transform.SetParent(chainOverlayRoot, false);
        overlay.AddComponent<SpriteRenderer>();
    }

    overlay.name = $"ChainOverlay_{overlayIndex}";
    overlay.transform.localScale = Vector3.one;
    overlay.SetActive(false);
    return overlay;
}

private Vector3 GetChainOverlayLocalPosition(int cellIndex)
{
    float startX = -(width - 1) * 0.5f;
    return new Vector3(startX + cellIndex, 0f, 0f);
}

private void SyncChainOverlayRenderer(GameObject overlay)
{
    if (overlay == null)
        return;

    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();
    if (overlayRenderer == null || sr == null)
        return;

    Sprite currentStageSprite = GetCurrentChainSpriteForRenderer(overlayRenderer);
    if (isChained && currentStageSprite == null)
    {
        LogMissingChainSpriteWarningOnce();
    }
    else if (currentStageSprite != null)
    {
        overlayRenderer.sprite = currentStageSprite;
    }

    overlayRenderer.sortingOrder = sr.sortingOrder + 2;
    overlayRenderer.sortingLayerID = sr.sortingLayerID;
    overlayRenderer.drawMode = SpriteDrawMode.Simple;
    overlayRenderer.color = Color.white;
    overlay.transform.localScale = GetChainOverlayScale(overlayRenderer.sprite);
}

private Sprite GetCurrentChainSpriteForRenderer(SpriteRenderer overlayRenderer)
{
    Sprite fallbackSprite = GetFallbackChainOverlaySprite(overlayRenderer);

    if (chainHealth >= MaxChainHealth)
        return chainStage1Sprite != null ? chainStage1Sprite : (chainStage2Sprite != null ? chainStage2Sprite : fallbackSprite);

    if (chainHealth == 1)
        return chainStage2Sprite != null ? chainStage2Sprite : (chainStage1Sprite != null ? chainStage1Sprite : fallbackSprite);

    return fallbackSprite;
}

private Vector3 GetChainOverlayScale(Sprite overlaySprite)
{
    if (overlaySprite == null)
        return Vector3.one;

    Vector2 spriteSize = overlaySprite.bounds.size;
    if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        return Vector3.one;

    float targetWidth = ChainOverlayCellWidth * ChainOverlayPaddingMultiplier;
    float targetHeight = ChainOverlayCellHeight * ChainOverlayPaddingMultiplier;

    return new Vector3(targetWidth / spriteSize.x, targetHeight / spriteSize.y, 1f);
}

private Sprite GetCurrentChainStageSprite()
{
    if (chainHealth >= MaxChainHealth)
        return chainStage1Sprite != null ? chainStage1Sprite : chainStage2Sprite;

    if (chainHealth == 1)
        return chainStage2Sprite != null ? chainStage2Sprite : chainStage1Sprite;

    return null;
}

private bool ShouldShowChainOverlay()
{
    return isChained;
}

private Sprite GetFallbackChainOverlaySprite(SpriteRenderer overlayRenderer)
{
    if (overlayRenderer != null && overlayRenderer.sprite != null)
        return overlayRenderer.sprite;

    if (chainOverlayPrefab != null)
    {
        SpriteRenderer prefabRenderer = chainOverlayPrefab.GetComponent<SpriteRenderer>();
        if (prefabRenderer != null && prefabRenderer.sprite != null)
            return prefabRenderer.sprite;
    }

    return null;
}

private Vector3 GetChainFXPosition()
{
    if (chainOverlayRoot != null && chainOverlayRoot.childCount > 0)
        return chainOverlayRoot.GetChild(0).position;

    return vfxAnchor != null ? vfxAnchor.position : transform.position;
}

private void LogMissingChainSpriteWarningOnce()
{
    if (hasLoggedMissingChainSpriteWarning)
        return;

    hasLoggedMissingChainSpriteWarning = true;
    Debug.LogWarning("Block chain overlay sprites are missing. Assign chainStage1Sprite and chainStage2Sprite on the Block prefab.");
}

    // Bloğu yeni bir koordinata gönder
public void MoveTo(int newX, int newY)
    {
        if (GridManager.Instance.isGameOver) return;
        
        x = newX;
        y = newY;
        
        // Hedeflenen dünya koordinatını hesapla
        // (Genişliğe göre merkezleme mantığını koruyoruz)
        targetPosition = new Vector3(x + (width - 1) * 0.5f, y, 0);
        
        // Eğer zaten oradaysak isMoving'i açmaya gerek yok
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            isMoving = true;
        }
    }
void Update()
    {
        if (isMoving)
        {
            // Sadece yatay hareketlerde (oyuncu kaydırırken) izin şalterini aç.
            if (trail != null)
            {
            trail.Clear();
            trail.enabled = false;
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);

            if (Vector3.Distance(transform.position, targetPosition) < 0.005f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
        else
        {
            // Blok duruyorsa iz bırakıcıyı tamamen kapat
            if (trail != null) trail.enabled = false;
        }
    }

private IEnumerator FireIdleParticleRoutine()
{
    while (true)
    {
        float delay = Random.Range(
            fireParticleMinDelay,
            fireParticleMaxDelay
        );

        yield return new WaitForSeconds(delay);

        if (fireIdleParticlePrefab == null)
            continue;

        int count = Random.Range(fireParticleMinCount, fireParticleMaxCount + 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-fireParticleSpawnRadiusX, fireParticleSpawnRadiusX),
                Random.Range(-fireParticleSpawnRadiusY, fireParticleSpawnRadiusY),
                0f
            );

            Instantiate(
                fireIdleParticlePrefab,
                transform.position + randomOffset,
                Quaternion.Euler(0f, 0f, Random.Range(0f, 360f))
            );
        }
    }
}

public System.Collections.IEnumerator CrunchAndDestroy(GameObject explosionPrefab)
    {
        yield return StartCoroutine(CrunchAndDestroy(explosionPrefab, null, true));
    }

public System.Collections.IEnumerator CrunchAndDestroy(GameObject explosionPrefab, GameObject overrideFxPrefab, bool useDefaultFxIfNull)
    {
        // 1. Patlama Efekti (Partiküller)
        GameObject effectPrefab = overrideFxPrefab;

        if (effectPrefab == null && useDefaultFxIfNull)
            effectPrefab = explosionPrefab;

        if (effectPrefab != null) {
            GameObject effect = Instantiate(effectPrefab, transform.position, Quaternion.identity);

            if (effect.TryGetComponent<ParticleSystem>(out ParticleSystem ps))
            {
                var main = ps.main;
                Color finalColor = blockColor;
                finalColor.a = 1.0f;
                main.startColor = new ParticleSystem.MinMaxGradient(finalColor);
            }

            Destroy(effect, 1f);
        }

        // İzi kapat (Gariplik yapmasın)
        if (trail != null) trail.emitting = false;

        // DİKKAT: O 9-Sliced kapatma (Simple yapma) satırını TAMAMEN SİLDİK!

        // 2. Temiz İçe Çökme (Pürüzsüz Küçülme)
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;
        float duration = 0.15f; // Çok hızlı bir erime

        while (elapsed < duration)
        {
            // Orijinal boyutundan sıfıra doğru, döndürmeden, temizce küçült
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, elapsed / duration);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Gerçekten Yok Et
        Destroy(gameObject);
    }

private void SetupEffect()
{
    switch (blockType)
    {
        case BlockType.Fire:
            blockEffect = new FireEffect();
            break;

        case BlockType.Slice:
            blockEffect = new SliceEffect();
            break;
    }
}
public void TriggerSpecial()
{
    UpdateSpecialVisualState();
    SetupEffect();

    if (blockEffect != null)
    {
        blockEffect.Trigger(this);
    }
}

public System.Collections.IEnumerator SliceFeedback()
{
    Vector3 originalScale = transform.localScale;

    SpriteRenderer sr = GetComponent<SpriteRenderer>();

    Color originalColor = sr.color;

    // Flash
    sr.color = Color.white;

    // Punch scale
    transform.localScale = originalScale * 1.15f;

    yield return new WaitForSeconds(0.06f);

    sr.color = originalColor;
    transform.localScale = originalScale;
}

}
