using UnityEngine;
using System.Collections; // Coroutine için şart
using System.Collections.Generic;


public class Block : MonoBehaviour
{
    private const float ChainOverlayCellWidth = 1f;
    private const float ChainOverlayCellHeight = 0.99f;
    private const float ChainOverlayPaddingMultiplier = 1.05f;
    private const string FireInternalEnergyRootName = "FireInternalEnergyRoot";
    private const string FireInternalEnergyLeftEmitterName = "FireInternalEnergy_LeftEdge";
    private const string FireInternalEnergyRightEmitterName = "FireInternalEnergy_RightEdge";
    private const string LegacyFireInternalEnergyRootName = "FireInternalEnergyFlow";
    private const float FireInternalEnergyEdgeInset = 0.08f;
    private const float FireInternalEnergyEdgeHeightMultiplier = 0.65f;
    private const float FireInternalEnergyLifetime = 0.9f;
    private const float FireInternalEnergyEmissionRatePerWidth = 3.0f;
    private const int FireInternalEnergyMinParticlesPerEmitter = 8;
    private const int FireInternalEnergyMaxParticlesPerEmitter = 28;
    private const float FireInternalEnergyTrailLifetime = 0.75f;
    // FIRE_V2_CLEANUP: FireInternalEnergyFlow no longer depends on the legacy idle-emitter limit.
    private const int FireInternalEnergyMaxWidth = 6;
    private const string SliceInternalEnergyRootName = "SliceInternalEnergyRoot";
    private const string SliceInternalEnergyLeftEmitterName = "SliceInternalEnergy_LeftEdge";
    private const string SliceInternalEnergyRightEmitterName = "SliceInternalEnergy_RightEdge";
    private const float SliceInternalEnergyEdgeInset = 0.08f;
    private const float SliceInternalEnergyLifetime = 0.9f;
    private const int SliceInternalEnergyMaxWidth = 6;

    #region Fire Block Visual System (Slice Style)
    private const string FireSliceVisualRootName = "FireSliceVisualRoot";
    private const string FireSliceLeftEnergyName = "FireSliceEnergy_Left";
    private const string FireSliceRightEnergyName = "FireSliceEnergy_Right";
    private const string FireSliceGlowName = "FireSliceGlow";
    private const string FireSliceSymbolName = "FireSliceSymbol";
    private const float FireSliceEnergyLifetime = 1.2f;
    private const float FireSliceEnergySpeed = 0.4f;
    private const float FireSliceTrailLifetime = 0.4f;
    #endregion

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
    [SerializeField] private Transform chainOverlayRoot;
    [SerializeField] private GameObject chainOverlayPrefab;
    [SerializeField] private GameObject chainBreakFXPrefab;
    [SerializeField] private SpriteRenderer flashOverlayRenderer;
    [SerializeField] private Transform vfxAnchor;
    [SerializeField] private float chainBreakShakeDuration = 0.15f;
    [SerializeField] private float chainBreakShakeStrength = 0.04f;
    [SerializeField] private float chainBreakPunchScale = 1.06f;
    private const int MaxChainHealth = 2;
    private int chainHealth = 0;
    private Sprite chainIntactSprite;
    private Sprite chainDamagedSprite;
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
    [SerializeField] private Material fireInternalEnergyTrailMaterial;
    // FIRE_V2_CLEANUP: Legacy serialized fields are preserved in source but disabled.
    // [SerializeField] private Sprite fireIdleFlameSprite;
    // [SerializeField] private Sprite[] fireLocalDischargeFrames;
    [SerializeField] private Sprite fireSymbolSprite;
    [SerializeField] private Material fireParticleMaterial;
    [SerializeField] private float firePulseScale = 1.15f;
    [SerializeField] private float firePulseDuration = 0.8f;

    [Header("Fire V2")]
    [SerializeField] private SpriteRenderer fireSymbolRenderer;
    [SerializeField] private GameObject fireInternalEnergyFlowPrefab;
    public GemColor fireTargetColor;

    [Header("Slice Internal Energy V2")]
    [SerializeField] private GameObject sliceInternalEnergyFlowPrefab;

    [Header("Slice Symbol")]
    [SerializeField] private GameObject sliceSymbolRoot;

    // FIRE_V2_CLEANUP: Legacy idle-particle tuning is preserved in source but disabled.
    // [SerializeField] private float fireParticleMinDelay = 3f;
    // [SerializeField] private float fireParticleMaxDelay = 5f;
    // [SerializeField] private int fireParticleMinCount = 3;
    // [SerializeField] private int fireParticleMaxCount = 5;
    // [SerializeField] private float fireParticleSpawnRadiusX = 0.35f;
    // [SerializeField] private float fireParticleSpawnRadiusY = 0.25f;

    private bool isSpecialVisualActive = false;
    private Coroutine fireIdleRoutine;
    private Coroutine fireSurfaceEnergyRoutine;
    private Transform fireIdleFlameRoot;
    private ParticleSystem fireInternalEnergyLeftParticleSystem;
    private ParticleSystem fireInternalEnergyRightParticleSystem;
    private ParticleSystemRenderer fireInternalEnergyLeftRenderer;
    private ParticleSystemRenderer fireInternalEnergyRightRenderer;
    private ParticleSystem sliceInternalEnergyLeftParticleSystem;
    private ParticleSystem sliceInternalEnergyRightParticleSystem;
    private ParticleSystemRenderer sliceInternalEnergyLeftRenderer;
    private ParticleSystemRenderer sliceInternalEnergyRightRenderer;
    // FIRE_V2_CLEANUP: Legacy surface renderer remains disabled until its V2 replacement exists.
    private SpriteRenderer fireSurfaceEnergyRenderer = null;
    private SpriteRenderer fireSymbolGlowRenderer;
    private readonly List<Transform> fireIdleFlames = new List<Transform>();
    private Transform fireSliceVisualRoot;
    private ParticleSystem fireSliceLeftEnergy;
    private ParticleSystem fireSliceRightEnergy;
    private ParticleSystemRenderer fireSliceLeftRenderer;
    private ParticleSystemRenderer fireSliceRightRenderer;
    private SpriteRenderer fireSliceGlowRenderer;
    private SpriteRenderer fireSliceSymbolRenderer;
    private Coroutine fireSlicePulseRoutine;


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
        blockType == BlockType.Fire ||
        blockType == BlockType.Slice;

    if (blockType == BlockType.Fire)
    {
        ClearSliceInternalEnergyFlow();
        SetSliceSymbolActive(false);
        Sprite fireSprite = GridManager.Instance != null
            ? GridManager.Instance.GetSpecialBlockSprite(BlockType.Fire, width)
            : null;
        if (fireSprite != null && sr != null)
            sr.sprite = fireSprite;

        // FIRE_V2_CLEANUP: Keep only the retained Fire V2 identity and internal energy flow.
        // RefreshFireIdleFlameEmitters();
        ConfigureFireSymbol();
        RefreshFireInternalEnergyFlow();
    }
    else if (blockType == BlockType.Slice)
    {
        Sprite sliceSprite = GridManager.Instance != null
            ? GridManager.Instance.GetSpecialBlockSprite(BlockType.Slice, width)
            : null;
        if (sliceSprite != null && sr != null)
            sr.sprite = sliceSprite;

        ClearFireSymbol();
        ClearFireInternalEnergyFlow();
        RefreshSliceInternalEnergyFlow();
        SetSliceSymbolActive(true);
    }
    else
    {
        ClearFireSymbol();
        ClearFireInternalEnergyFlow();
        ClearSliceInternalEnergyFlow();
        SetSliceSymbolActive(false);
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
        RefreshFireSymbolSorting();
        RefreshFireInternalEnergyFlowSorting();
        RefreshSliceInternalEnergyFlowSorting();

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

private void ConfigureFireSymbol()
{
    if (blockType != BlockType.Fire)
    {
        ClearFireSymbol();
        return;
    }

    ResolveFireSymbolRenderer();
    if (fireSymbolRenderer == null)
        return;

    Sprite symbolSprite = FireV2SpriteLibrary.GetFireSymbolSprite(fireTargetColor);
    if (symbolSprite == null)
    {
        ClearFireSymbol();
        return;
    }

    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    float blockHeight = sr != null ? sr.size.y : 1f;
    float spriteHeight = symbolSprite.bounds.size.y;
    float symbolHeight = blockHeight * (width <= 1 ? 0.65f : 0.68f);
    float symbolScale = spriteHeight > 0f
        ? symbolHeight / spriteHeight
        : 1f;

    fireSymbolRenderer.sprite = symbolSprite;
    fireSymbolRenderer.drawMode = SpriteDrawMode.Simple;
    fireSymbolRenderer.color = Color.white;
    fireSymbolRenderer.transform.localPosition = new Vector3(0f, 0f, -0.03f);
    fireSymbolRenderer.transform.localRotation = Quaternion.identity;
    fireSymbolRenderer.transform.localScale = new Vector3(symbolScale, symbolScale, 1f);
    fireSymbolRenderer.enabled = true;

    ConfigureFireSymbolGlow();
    RefreshFireSymbolSorting();
}

private void ConfigureFireSymbolGlow()
{
    if (fireSymbolRenderer == null || fireSymbolRenderer.sprite == null)
        return;

    Transform glowTransform = fireSymbolRenderer.transform.Find("FireSymbolGlow");
    if (glowTransform == null)
        glowTransform = fireSymbolRenderer.transform.Find("Glow");
    bool createdGlow = false;
    if (glowTransform == null)
    {
        GameObject glowObject = new GameObject("FireSymbolGlow");
        glowTransform = glowObject.transform;
        glowTransform.SetParent(fireSymbolRenderer.transform, false);
        createdGlow = true;
    }

    fireSymbolGlowRenderer = glowTransform.GetComponent<SpriteRenderer>();
    if (fireSymbolGlowRenderer == null)
        fireSymbolGlowRenderer = glowTransform.gameObject.AddComponent<SpriteRenderer>();

    fireSymbolGlowRenderer.sprite = fireSymbolRenderer.sprite;
    if (createdGlow)
    {
        fireSymbolGlowRenderer.color = new Color(1f, 1f, 1f, 0.30f);
        fireSymbolGlowRenderer.transform.localPosition = Vector3.zero;
        fireSymbolGlowRenderer.transform.localRotation = Quaternion.identity;
        fireSymbolGlowRenderer.transform.localScale = Vector3.one * 1.20f;
    }
    fireSymbolGlowRenderer.enabled = fireSymbolRenderer.enabled;
}

private void ResolveFireSymbolRenderer()
{
    if (fireSymbolRenderer != null)
        return;

    const string symbolObjectName = "FireSymbolV2";
    Transform symbolTransform = transform.Find(symbolObjectName);
    if (symbolTransform == null)
    {
        GameObject symbolObject = new GameObject(symbolObjectName);
        symbolTransform = symbolObject.transform;
        symbolTransform.SetParent(transform, false);
    }

    fireSymbolRenderer = symbolTransform.GetComponent<SpriteRenderer>();
    if (fireSymbolRenderer == null)
        fireSymbolRenderer = symbolTransform.gameObject.AddComponent<SpriteRenderer>();
}

private void ClearFireSymbol()
{
    if (fireSymbolRenderer != null)
        fireSymbolRenderer.enabled = false;

    if (fireSymbolGlowRenderer != null)
        fireSymbolGlowRenderer.enabled = false;
}

private void RefreshFireSymbolSorting()
{
    if (fireSymbolRenderer == null)
        return;

    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    if (sr == null)
        return;

    fireSymbolRenderer.sortingLayerID = sr.sortingLayerID;
    fireSymbolRenderer.sortingOrder = sr.sortingOrder + 3;

    if (fireSymbolGlowRenderer != null)
    {
        fireSymbolGlowRenderer.sortingLayerID = sr.sortingLayerID;
        fireSymbolGlowRenderer.sortingOrder = sr.sortingOrder + 2;
    }
}

private void SetSliceSymbolActive(bool isActive)
{
    if (sliceSymbolRoot != null)
        sliceSymbolRoot.SetActive(isActive);
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

        Sprite finalSprite = newSprite;
        Sprite specialSprite = GridManager.Instance != null
            ? GridManager.Instance.GetSpecialBlockSprite(blockType, blockWidth)
            : null;
        if (specialSprite != null)
            finalSprite = specialSprite;

        sr.sprite = finalSprite;
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

    Material grayscaleMaterial = GetGameOverGrayscaleMaterial();
    bool canUseGrayscaleShader =
        blockType == BlockType.Normal ||
        blockType == BlockType.Ice ||
        blockType == BlockType.Chained;
    if (canUseGrayscaleShader)
    {
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
    ApplyIceVisualGameOverState(grayscaleMaterial);
    ApplyChainVisualGameOverState(grayscaleMaterial);

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
        if (renderer != null &&
            renderer != sr &&
            !IsIceVisualRenderer(renderer) &&
            !IsChainOverlayRenderer(renderer))
        {
            renderer.enabled = false;
        }
    }
}

private void ApplyIceVisualGameOverState(Material grayscaleMaterial)
{
    if (!isFrozen || iceVisual == null || !iceVisual.activeSelf)
        return;

    SpriteRenderer iceRenderer = iceVisual.GetComponent<SpriteRenderer>();
    if (iceRenderer == null)
        return;

    iceRenderer.enabled = true;
    if (grayscaleMaterial != null)
        iceRenderer.sharedMaterial = grayscaleMaterial;

    iceRenderer.SetPropertyBlock(null);
    iceRenderer.color = Color.white;
}

private bool IsIceVisualRenderer(SpriteRenderer renderer)
{
    return iceVisual != null &&
        renderer != null &&
        renderer.gameObject == iceVisual;
}

private void ApplyChainVisualGameOverState(Material grayscaleMaterial)
{
    if (!isChained)
        return;

    for (int i = 0; i < spawnedChainOverlays.Count; i++)
    {
        GameObject overlay = spawnedChainOverlays[i];
        if (overlay == null || !overlay.activeSelf)
            continue;

        SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();
        if (overlayRenderer == null)
            continue;

        overlayRenderer.enabled = true;
        if (grayscaleMaterial != null)
            overlayRenderer.sharedMaterial = grayscaleMaterial;

        overlayRenderer.SetPropertyBlock(null);
        overlayRenderer.color = Color.white;
    }
}

private bool IsChainOverlayRenderer(SpriteRenderer renderer)
{
    if (renderer == null)
        return false;

    for (int i = 0; i < spawnedChainOverlays.Count; i++)
    {
        GameObject overlay = spawnedChainOverlays[i];
        if (overlay != null && renderer.gameObject == overlay)
            return true;
    }

    return false;
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
        iceSr.sortingLayerID = sr.sortingLayerID;
        iceSr.sortingOrder = sr.sortingOrder + 1;
        
        iceVisual.SetActive(true);
    }
    else
    {
        if (iceVisual != null) iceVisual.SetActive(false);
    }
}

public void ApplyPreviewRendererSorting(int sortingOrder)
{
    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    if (sr == null)
        return;

    int sortingLayerId = sr.sortingLayerID;
    sr.sortingOrder = sortingOrder;

    if (iceVisual != null)
    {
        SpriteRenderer iceSr = iceVisual.GetComponent<SpriteRenderer>();
        if (iceSr != null)
        {
            iceSr.sortingLayerID = sortingLayerId;
            iceSr.sortingOrder = sortingOrder + 1;
        }
    }

    for (int i = 0; i < spawnedChainOverlays.Count; i++)
    {
        GameObject overlay = spawnedChainOverlays[i];
        if (overlay == null)
            continue;

        SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();
        if (overlayRenderer == null)
            continue;

        overlayRenderer.sortingLayerID = sortingLayerId;
        overlayRenderer.sortingOrder = sortingOrder + 2;
    }

    ApplyFireInternalEnergyRendererSorting(fireInternalEnergyLeftRenderer, sortingLayerId, sortingOrder);
    ApplyFireInternalEnergyRendererSorting(fireInternalEnergyRightRenderer, sortingLayerId, sortingOrder);
    ApplySliceInternalEnergyRendererSorting(sliceInternalEnergyLeftRenderer, sortingLayerId, sortingOrder);
    ApplySliceInternalEnergyRendererSorting(sliceInternalEnergyRightRenderer, sortingLayerId, sortingOrder);

    RefreshFireSymbolSorting();

}

    public void SetChained(bool chained)
    {
        SetChained(chained ? MaxChainHealth : 0);
    }

    public void SetChained(bool chained, Sprite intactSprite, Sprite damagedSprite)
    {
        SetChained(chained ? MaxChainHealth : 0, intactSprite, damagedSprite);
    }

    public void SetChained(int count)
    {
        SetChained(count, null, null);
    }

    public void SetChained(int count, Sprite intactSprite, Sprite damagedSprite)
    {
        chainHealth = count > 0 ? MaxChainHealth : 0;
        isChained = chainHealth > 0;
        chainIntactSprite = intactSprite;
        chainDamagedSprite = damagedSprite;

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

    int overlayCellCount = isChained ? 1 : 0;

    while (spawnedChainOverlays.Count > overlayCellCount)
    {
        int lastIndex = spawnedChainOverlays.Count - 1;
        GameObject extraOverlay = spawnedChainOverlays[lastIndex];
        spawnedChainOverlays.RemoveAt(lastIndex);

        if (extraOverlay != null)
        {
            extraOverlay.SetActive(false);
            Destroy(extraOverlay);
        }
    }

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

        overlay.transform.localPosition = Vector3.zero;
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

private void SyncChainOverlayRenderer(GameObject overlay)
{
    if (overlay == null)
        return;

    if (sr == null)
        sr = GetComponent<SpriteRenderer>();

    SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();
    if (overlayRenderer == null || sr == null)
        return;

    Sprite currentStageSprite = GetCurrentChainSprite();
    if (isChained && currentStageSprite == null)
    {
        LogMissingChainSpriteWarningOnce();
    }

    overlayRenderer.sprite = currentStageSprite;

    overlayRenderer.sortingOrder = sr.sortingOrder + 2;
    overlayRenderer.sortingLayerID = sr.sortingLayerID;
    overlayRenderer.drawMode = SpriteDrawMode.Simple;
    overlayRenderer.color = Color.white;
    overlay.transform.localScale = GetChainOverlayScale(overlayRenderer.sprite);
}

private Sprite GetCurrentChainSprite()
{
    if (chainHealth >= MaxChainHealth)
        return chainIntactSprite;

    if (chainHealth == 1)
        return chainDamagedSprite != null ? chainDamagedSprite : chainIntactSprite;

    return null;
}

private Vector3 GetChainOverlayScale(Sprite overlaySprite)
{
    if (overlaySprite == null)
        return Vector3.one;

    Vector2 spriteSize = overlaySprite.bounds.size;
    if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        return Vector3.one;

    Vector2 blockSize = originalSize;
    if ((blockSize.x <= 0f || blockSize.y <= 0f) && sr != null)
        blockSize = sr.size;

    float targetWidth = blockSize.x > 0f ? blockSize.x * ChainOverlayPaddingMultiplier : ChainOverlayCellWidth * ChainOverlayPaddingMultiplier;
    float targetHeight = blockSize.y > 0f ? blockSize.y * ChainOverlayPaddingMultiplier : ChainOverlayCellHeight * ChainOverlayPaddingMultiplier;

    return new Vector3(targetWidth / spriteSize.x, targetHeight / spriteSize.y, 1f);
}

private bool ShouldShowChainOverlay()
{
    return isChained;
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
    Debug.LogWarning("Block chain overlay sprites are missing. Assign Chain Length Sprites on the GridManager.");
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

#if false
#region Fire Block Visual System (Slice Style)
private void RefreshFireSliceVisuals()
{
    bool shouldShowFireVisuals = isSpecialVisualActive && blockType == BlockType.Fire;
    if (!shouldShowFireVisuals)
    {
        ClearFireSliceVisuals();
        return;
    }

    // FIRE_V2_CLEANUP: Legacy idle/surface FX cleanup calls are disabled with their systems.
    // ClearFireIdleFlameEmitters();
    ClearFireInternalEnergyFlow();
    // ClearFireSurfaceEnergy();
    RemoveLegacyFireSurfaceVisuals();

    ResolveFireSliceVisuals();
    ConfigureFireSliceEnergy();
    ConfigureFireSliceGlow();
    ConfigureFireSliceSymbol();
    RefreshFireSliceVisualSorting();

    PlayFireSliceEnergy(fireSliceLeftEnergy);
    PlayFireSliceEnergy(fireSliceRightEnergy);
}

private void ResolveFireSliceVisuals()
{
    if (fireSliceVisualRoot == null)
    {
        Transform existingRoot = transform.Find(FireSliceVisualRootName);
        if (existingRoot != null)
        {
            fireSliceVisualRoot = existingRoot;
        }
        else
        {
            GameObject rootObject = new GameObject(FireSliceVisualRootName);
            rootObject.transform.SetParent(transform, false);
            fireSliceVisualRoot = rootObject.transform;
        }
    }

    fireSliceVisualRoot.localPosition = Vector3.zero;
    fireSliceVisualRoot.localRotation = Quaternion.identity;
    fireSliceVisualRoot.localScale = Vector3.one;

    fireSliceLeftEnergy = ResolveFireSliceEnergyEmitter(
        FireSliceLeftEnergyName,
        out fireSliceLeftRenderer);
    fireSliceRightEnergy = ResolveFireSliceEnergyEmitter(
        FireSliceRightEnergyName,
        out fireSliceRightRenderer);
    fireSliceGlowRenderer = ResolveFireSliceRenderer(FireSliceGlowName);

    Transform existingOuterGlow = fireSliceVisualRoot.Find("FireGlowOuter");
    if (existingOuterGlow != null)
        Destroy(existingOuterGlow.gameObject);

    if (fireSymbolSprite != null)
    {
        fireSliceSymbolRenderer = ResolveFireSliceRenderer(FireSliceSymbolName);
    }
    else
    {
        StopFireSlicePulse();
        Transform existingSymbol = fireSliceVisualRoot.Find(FireSliceSymbolName);
        if (existingSymbol != null)
            Destroy(existingSymbol.gameObject);

        fireSliceSymbolRenderer = null;
    }
}

private ParticleSystem ResolveFireSliceEnergyEmitter(
    string emitterName,
    out ParticleSystemRenderer particleRenderer)
{
    particleRenderer = null;
    if (fireSliceVisualRoot == null)
        return null;

    Transform emitterTransform = fireSliceVisualRoot.Find(emitterName);
    if (emitterTransform == null)
    {
        GameObject emitterObject = new GameObject(emitterName);
        emitterObject.transform.SetParent(fireSliceVisualRoot, false);
        emitterTransform = emitterObject.transform;
    }

    ParticleSystem particleSystem = emitterTransform.GetComponent<ParticleSystem>();
    if (particleSystem == null)
        particleSystem = emitterTransform.gameObject.AddComponent<ParticleSystem>();

    particleRenderer = emitterTransform.GetComponent<ParticleSystemRenderer>();
    return particleSystem;
}

private SpriteRenderer ResolveFireSliceRenderer(string rendererName)
{
    if (fireSliceVisualRoot == null)
        return null;

    Transform rendererTransform = fireSliceVisualRoot.Find(rendererName);
    if (rendererTransform == null)
    {
        GameObject rendererObject = new GameObject(rendererName);
        rendererObject.transform.SetParent(fireSliceVisualRoot, false);
        rendererTransform = rendererObject.transform;
    }

    SpriteRenderer renderer = rendererTransform.GetComponent<SpriteRenderer>();
    if (renderer == null)
        renderer = rendererTransform.gameObject.AddComponent<SpriteRenderer>();

    return renderer;
}

private void ConfigureFireSliceEnergy()
{
    Vector2 visualSize = GetFireInternalEnergyVisualSize();
    int clampedWidth = Mathf.Clamp(width, 1, 4);
    float energyWidth = Mathf.Max(0.10f, visualSize.x - 0.35f);
    float energyHeight = Mathf.Max(0.10f, visualSize.y - 0.15f);
    int maxParticles = Mathf.Clamp(20 + Mathf.CeilToInt((clampedWidth - 1) * (10f / 3f)), 20, 30);
    float emissionRate = 3f * clampedWidth;

    ConfigureFireSliceEnergyEmitter(
        fireSliceLeftEnergy,
        fireSliceLeftRenderer,
        1f,
        energyWidth,
        energyHeight,
        emissionRate,
        maxParticles,
        101);
    ConfigureFireSliceEnergyEmitter(
        fireSliceRightEnergy,
        fireSliceRightRenderer,
        -1f,
        energyWidth,
        energyHeight,
        emissionRate,
        maxParticles,
        211);
}

private void ConfigureFireSliceEnergyEmitter(
    ParticleSystem particleSystem,
    ParticleSystemRenderer particleRenderer,
    float direction,
    float energyWidth,
    float energyHeight,
    float emissionRate,
    int maxParticles,
    int seedSalt)
{
    if (particleSystem == null || particleRenderer == null)
        return;

    bool wasPlaying = particleSystem.isPlaying;
    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    particleSystem.transform.localPosition = Vector3.zero;
    particleSystem.transform.localRotation = Quaternion.identity;
    particleSystem.transform.localScale = Vector3.one;

    ParticleSystem.MainModule main = particleSystem.main;
    main.loop = true;
    main.prewarm = true;
    main.playOnAwake = false;
    main.simulationSpace = ParticleSystemSimulationSpace.Local;
    main.gravityModifier = 0f;
    main.startLifetime = FireSliceEnergyLifetime;
    main.startSpeed = 0f;
    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
    main.maxParticles = maxParticles;
    particleSystem.useAutoRandomSeed = false;
    particleSystem.randomSeed = GetFireInternalEnergySeed(seedSalt);

    main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(1f, 1f, 1f, 0.3f),
        new Color(1f, 1f, 0.95f, 0.5f));

    ParticleSystem.EmissionModule emission = particleSystem.emission;
    emission.enabled = true;
    emission.rateOverTime = emissionRate;

    ParticleSystem.ShapeModule shape = particleSystem.shape;
    shape.enabled = true;
    shape.shapeType = ParticleSystemShapeType.Box;
    shape.position = Vector3.zero;
    shape.rotation = Vector3.zero;
    shape.scale = new Vector3(energyWidth, energyHeight, 0f);

    ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
    velocity.enabled = true;
    velocity.space = ParticleSystemSimulationSpace.Local;
    float signedSpeed = direction * FireSliceEnergySpeed;
    velocity.x = new ParticleSystem.MinMaxCurve(signedSpeed * 0.6f);
    velocity.y = new ParticleSystem.MinMaxCurve(0f);
    velocity.z = new ParticleSystem.MinMaxCurve(0f);

    ParticleSystem.NoiseModule noise = particleSystem.noise;
    noise.enabled = true;
    noise.separateAxes = false;
    noise.strength = 0.15f;
    noise.frequency = 0.5f;
    noise.scrollSpeed = 0.1f;
    noise.damping = true;

    ParticleSystem.TrailModule trails = particleSystem.trails;
    trails.enabled = true;
    trails.mode = ParticleSystemTrailMode.PerParticle;
    trails.ratio = 1f;
    trails.lifetime = FireSliceTrailLifetime;
    trails.dieWithParticles = true;
    trails.sizeAffectsWidth = false;
    trails.sizeAffectsLifetime = false;
    trails.minVertexDistance = 0.01f;
    trails.inheritParticleColor = true;
    trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
    trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
        1f,
        new AnimationCurve(new Keyframe(0f, 0.035f), new Keyframe(1f, 0f)));
    Gradient trailColor = new Gradient();
    trailColor.SetKeys(
        new[]
        {
            new GradientColorKey(new Color(1f, 1f, 1f), 0f),
            new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0.5f),
            new GradientColorKey(new Color(1f, 0.8f, 0.4f), 1f)
        },
        new[]
        {
            new GradientAlphaKey(0.7f, 0f),
            new GradientAlphaKey(0.4f, 0.7f),
            new GradientAlphaKey(0f, 1f)
        });
    trails.colorOverTrail = new ParticleSystem.MinMaxGradient(trailColor);

    ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
    colorOverLifetime.enabled = false;

    ParticleSystem.CollisionModule collision = particleSystem.collision;
    collision.enabled = false;

    Material particleMaterial = fireParticleMaterial != null
        ? fireParticleMaterial
        : GetFireInternalEnergySharedMaterial();
    particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    particleRenderer.sharedMaterial = particleMaterial;
    particleRenderer.trailMaterial = particleRenderer.sharedMaterial;

    if (wasPlaying)
        particleSystem.Play(true);
}

private void ConfigureFireSliceGlow()
{
    if (sr == null)
        return;

    ConfigureFireSliceGlowRenderer(
        fireSliceGlowRenderer,
        new Color(1f, 0.6f, 0.15f, 0.18f),
        1.08f);
}

private void ConfigureFireSliceGlowRenderer(SpriteRenderer glowRenderer, Color color, float scale)
{
    if (glowRenderer == null || sr == null)
        return;

    glowRenderer.sprite = sr.sprite;
    glowRenderer.drawMode = SpriteDrawMode.Sliced;
    glowRenderer.size = sr.size;
    glowRenderer.color = color;
    glowRenderer.transform.localPosition = new Vector3(0f, 0f, 0.02f);
    glowRenderer.transform.localRotation = Quaternion.identity;
    glowRenderer.transform.localScale = Vector3.one * scale;
    glowRenderer.enabled = glowRenderer.sprite != null;
}

private void ConfigureFireSliceSymbol()
{
    if (fireSymbolSprite == null || fireSliceSymbolRenderer == null)
        return;

    Vector2 visualSize = GetFireInternalEnergyVisualSize();
    fireSliceSymbolRenderer.sprite = fireSymbolSprite;
    fireSliceSymbolRenderer.drawMode = SpriteDrawMode.Simple;
    fireSliceSymbolRenderer.color = new Color(1f, 1f, 1f, 0.75f);
    fireSliceSymbolRenderer.transform.localPosition = new Vector3(0f, visualSize.y * 0.16f, -0.08f);
    fireSliceSymbolRenderer.transform.localRotation = Quaternion.identity;
    fireSliceSymbolRenderer.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
    fireSliceSymbolRenderer.enabled = true;

    if (fireSlicePulseRoutine == null && Application.isPlaying)
        fireSlicePulseRoutine = StartCoroutine(FireSliceSymbolPulseRoutine());
}

private IEnumerator FireSliceSymbolPulseRoutine()
{
    while (isSpecialVisualActive && blockType == BlockType.Fire && fireSliceSymbolRenderer != null && fireSymbolSprite != null)
    {
        float duration = Mathf.Max(0.01f, firePulseDuration);
        float pulse = (Mathf.Sin((Time.time / duration) * Mathf.PI * 2f) + 1f) * 0.5f;
        float scale = Mathf.Lerp(1f, Mathf.Max(1f, firePulseScale), pulse) * 0.5f;
        fireSliceSymbolRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        yield return null;
    }

    fireSlicePulseRoutine = null;
}

private void RefreshFireSliceVisualSorting()
{
    int sortingLayerId = sr != null ? sr.sortingLayerID : 0;
    int sortingOrder = sr != null ? sr.sortingOrder : 0;

    if (fireSliceGlowRenderer != null)
    {
        fireSliceGlowRenderer.sortingLayerID = sortingLayerId;
        fireSliceGlowRenderer.sortingOrder = sortingOrder - 1;
    }

    ApplyFireSliceEnergySorting(fireSliceLeftRenderer, sortingLayerId, sortingOrder);
    ApplyFireSliceEnergySorting(fireSliceRightRenderer, sortingLayerId, sortingOrder);

    if (fireSliceSymbolRenderer != null)
    {
        fireSliceSymbolRenderer.sortingLayerID = sortingLayerId;
        fireSliceSymbolRenderer.sortingOrder = sortingOrder + 3;
    }
}

private void ApplyFireSliceEnergySorting(
    ParticleSystemRenderer particleRenderer,
    int sortingLayerId,
    int baseSortingOrder)
{
    if (particleRenderer == null)
        return;

    particleRenderer.sortingLayerID = sortingLayerId;
    particleRenderer.sortingOrder = baseSortingOrder + 2;
}

private void PlayFireSliceEnergy(ParticleSystem particleSystem)
{
    if (particleSystem != null && !particleSystem.isPlaying)
        particleSystem.Play(true);
}

private void StopFireSlicePulse()
{
    if (fireSlicePulseRoutine == null)
        return;

    StopCoroutine(fireSlicePulseRoutine);
    fireSlicePulseRoutine = null;
}

private void ClearFireSliceVisuals()
{
    StopFireSlicePulse();
    StopFireSliceEnergy(fireSliceLeftEnergy);
    StopFireSliceEnergy(fireSliceRightEnergy);

    Transform existingRoot = fireSliceVisualRoot != null
        ? fireSliceVisualRoot
        : transform.Find(FireSliceVisualRootName);
    if (existingRoot != null)
        Destroy(existingRoot.gameObject);

    fireSliceVisualRoot = null;
    fireSliceLeftEnergy = null;
    fireSliceRightEnergy = null;
    fireSliceLeftRenderer = null;
    fireSliceRightRenderer = null;
    fireSliceGlowRenderer = null;
    fireSliceSymbolRenderer = null;
}

private void StopFireSliceEnergy(ParticleSystem particleSystem)
{
    if (particleSystem == null)
        return;

    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    particleSystem.Clear(true);
}
#endregion
#endif

// FIRE_V2_CLEANUP: Legacy Fire Idle Flame method preserved but disabled.
#if false
private void RefreshFireIdleFlameEmitters()
{
    if (!isSpecialVisualActive || blockType != BlockType.Fire)
    {
        ClearFireIdleFlameEmitters();
        return;
    }

    RemoveLegacyFireSurfaceVisuals();
    ResolveFireIdleFlameRoot();

    int emitterCount = Mathf.Clamp(width, 1, MaxFireIdleFlameEmitters);
    while (fireIdleFlames.Count > emitterCount)
    {
        int lastIndex = fireIdleFlames.Count - 1;
        Transform extraFlame = fireIdleFlames[lastIndex];
        fireIdleFlames.RemoveAt(lastIndex);

        if (extraFlame != null)
            Destroy(extraFlame.gameObject);
    }

    while (fireIdleFlames.Count < emitterCount)
    {
        Transform flame = CreateFireIdleFlame(fireIdleFlames.Count);
        fireIdleFlames.Add(flame);
    }

    for (int i = 0; i < fireIdleFlames.Count; i++)
    {
        Transform flame = fireIdleFlames[i];
        if (flame == null)
            continue;

        flame.localPosition = GetFireIdleFlameEmitterPosition(i, emitterCount);
        flame.localRotation = Quaternion.identity;
        flame.localScale = Vector3.one;

        ConfigureFireIdleFlame(flame, i);
        flame.gameObject.SetActive(true);
    }
}

// FIRE_V2_CLEANUP: Legacy Fire Idle Flame method preserved but disabled.
private void ResolveFireIdleFlameRoot()
{
    if (fireIdleFlameRoot == null)
    {
        Transform existingRoot = transform.Find(FireIdleFlameRootName);
        if (existingRoot != null)
        {
            fireIdleFlameRoot = existingRoot;
        }
        else
        {
            GameObject rootObject = new GameObject(FireIdleFlameRootName);
            rootObject.transform.SetParent(transform, false);
            fireIdleFlameRoot = rootObject.transform;
        }
    }

    fireIdleFlameRoot.localPosition = Vector3.zero;
    fireIdleFlameRoot.localRotation = Quaternion.identity;
    fireIdleFlameRoot.localScale = Vector3.one;
}

// FIRE_V2_CLEANUP: Legacy Fire Idle Flame method preserved but disabled.
private Transform CreateFireIdleFlame(int index)
{
    ResolveFireIdleFlameRoot();

    GameObject flameObject = new GameObject($"{FireIdleFlameNamePrefix}{index}");
    flameObject.transform.SetParent(fireIdleFlameRoot, false);
    flameObject.SetActive(false);
    return flameObject.transform;
}

// FIRE_V2_CLEANUP: Legacy Fire Idle Flame method preserved but disabled.
private Vector3 GetFireIdleFlameEmitterPosition(int index, int emitterCount)
{
    float leftmostCellCenter = -((emitterCount - 1) * 0.5f);
    float xOffset = leftmostCellCenter + index;
    float yOffset = 0.25f + ((index % 2 == 0) ? 0.015f : -0.01f);
    return new Vector3(xOffset, yOffset, -0.05f);
}

// FIRE_V2_CLEANUP: Legacy Fire Idle Flame method preserved but disabled.
private void ConfigureFireIdleFlame(Transform flame, int index)
{
    if (flame == null)
        return;

    Transform spriteTransform = flame.Find(FireIdleFlameSpriteName);
    SpriteRenderer flameRenderer;
    if (spriteTransform == null)
    {
        GameObject spriteObject = new GameObject(FireIdleFlameSpriteName);
        spriteObject.transform.SetParent(flame, false);
        flameRenderer = spriteObject.AddComponent<SpriteRenderer>();
    }
    else
    {
        flameRenderer = spriteTransform.GetComponent<SpriteRenderer>();
        if (flameRenderer == null)
            flameRenderer = spriteTransform.gameObject.AddComponent<SpriteRenderer>();
    }

    if (fireIdleFlameSprite != null)
    {
        flameRenderer.sprite = fireIdleFlameSprite;
        flameRenderer.color = Color.white;
    }
    else if (sr != null && sr.sprite != null)
    {
        flameRenderer.sprite = sr.sprite;
        flameRenderer.color = new Color(1f, 0.45f, 0.05f, 0.9f);
    }
    flameRenderer.sortingLayerID = sr != null ? sr.sortingLayerID : 0;
    flameRenderer.sortingOrder = sr != null ? sr.sortingOrder + 4 : 4;
    flameRenderer.transform.localPosition = Vector3.zero;
    flameRenderer.transform.localRotation = Quaternion.identity;
    flameRenderer.transform.localScale = new Vector3(FireIdleFlameBaseScale * 1.5f, FireIdleFlameBaseScale * 1.5f, 1f);

    ParticleSystem legacyParticleSystem = flame.GetComponent<ParticleSystem>();
    if (legacyParticleSystem != null)
        Destroy(legacyParticleSystem);

    ParticleSystemRenderer legacyRenderer = flame.GetComponent<ParticleSystemRenderer>();
    if (legacyRenderer != null)
        Destroy(legacyRenderer);

    RemoveFireIdleFlameChild(flame, FireIdleFlameGlowName);
    RemoveFireIdleFlameChild(flame, FireIdleEmberEmitterName);
}

// FIRE_V2_CLEANUP: Legacy Fire Idle Flame method preserved but disabled.
private void RemoveFireIdleFlameChild(Transform flame, string childName)
{
    Transform child = flame.Find(childName);
    if (child == null)
        return;

    child.gameObject.SetActive(false);
    Destroy(child.gameObject);
}
#endif

private float GetFireIdleHash01(int index, int salt)
{
    float value = Mathf.Sin(((x + 11) * 12.9898f) + ((y + 17) * 78.233f) + ((index + 3) * 37.719f) + (salt * 19.371f)) * 43758.5453f;
    return Mathf.Repeat(value, 1f);
}

// FIRE_V2_CLEANUP: Legacy Fire Idle Flame method preserved but disabled.
#if false
private void ClearFireIdleFlameEmitters()
{
    for (int i = fireIdleFlames.Count - 1; i >= 0; i--)
    {
        Transform flame = fireIdleFlames[i];
        if (flame != null)
            Destroy(flame.gameObject);
    }

    fireIdleFlames.Clear();

    if (fireIdleFlameRoot != null)
    {
        Destroy(fireIdleFlameRoot.gameObject);
        fireIdleFlameRoot = null;
    }
}
#endif

private void RefreshFireInternalEnergyFlow()
{
    bool shouldShowInternalEnergy =
        isSpecialVisualActive &&
        blockType == BlockType.Fire &&
        width >= 1 &&
        // FIRE_V2_CLEANUP: Use the retained system's own width limit.
        width <= FireInternalEnergyMaxWidth;

    if (!shouldShowInternalEnergy)
    {
        ClearFireInternalEnergyFlow();
        return;
    }

    ResolveFireInternalEnergyFlow();
    ConfigureFireInternalEnergyEmitters();
    RefreshFireInternalEnergyFlowSorting();

    PlayFireInternalEnergyFlow(fireInternalEnergyLeftParticleSystem);
    PlayFireInternalEnergyFlow(fireInternalEnergyRightParticleSystem);
}

private void ResolveFireInternalEnergyFlow()
{
    if (fireInternalEnergyLeftParticleSystem != null && fireInternalEnergyRightParticleSystem != null)
        return;

    RemoveLegacyFireInternalEnergyFlow();

    Transform flowRoot = transform.Find(FireInternalEnergyRootName);
    if (flowRoot == null)
    {
        GameObject rootObject = fireInternalEnergyFlowPrefab != null
            ? Instantiate(fireInternalEnergyFlowPrefab, transform)
            : new GameObject(FireInternalEnergyRootName);
        rootObject.name = FireInternalEnergyRootName;
        if (rootObject.transform.parent != transform)
            rootObject.transform.SetParent(transform, false);
        flowRoot = rootObject.transform;
    }

    flowRoot.localPosition = Vector3.zero;
    flowRoot.localRotation = Quaternion.identity;
    flowRoot.localScale = Vector3.one;

    fireInternalEnergyLeftParticleSystem = ResolveFireInternalEnergyEmitter(
        flowRoot,
        FireInternalEnergyLeftEmitterName,
        out fireInternalEnergyLeftRenderer);
    fireInternalEnergyRightParticleSystem = ResolveFireInternalEnergyEmitter(
        flowRoot,
        FireInternalEnergyRightEmitterName,
        out fireInternalEnergyRightRenderer);
}

private ParticleSystem ResolveFireInternalEnergyEmitter(
    Transform flowRoot,
    string emitterName,
    out ParticleSystemRenderer particleRenderer)
{
    particleRenderer = null;

    if (flowRoot == null)
        return null;

    Transform particleTransform = flowRoot.Find(emitterName);
    if (particleTransform == null)
    {
        GameObject particleObject = new GameObject(emitterName);
        particleObject.transform.SetParent(flowRoot, false);
        particleTransform = particleObject.transform;
    }

    particleTransform.localRotation = Quaternion.identity;
    particleTransform.localScale = Vector3.one;

    ParticleSystem particleSystem = particleTransform.GetComponent<ParticleSystem>();
    if (particleSystem == null)
        particleSystem = particleTransform.gameObject.AddComponent<ParticleSystem>();

    particleRenderer = particleTransform.GetComponent<ParticleSystemRenderer>();
    return particleSystem;
}

private void ConfigureFireInternalEnergyEmitters()
{
    Vector2 visualSize = GetFireInternalEnergyVisualSize();
    float visualWidth = Mathf.Max(0.01f, visualSize.x);
    float visualHeight = Mathf.Max(0.01f, visualSize.y);
    float edgeInset = Mathf.Min(FireInternalEnergyEdgeInset, visualWidth * 0.35f);
    float leftX = (-visualWidth * 0.5f) + edgeInset;
    float rightX = (visualWidth * 0.5f) - edgeInset;
    float travelDistance = Mathf.Max(0.05f, visualWidth - (edgeInset * 2f));
    float horizontalSpeed = travelDistance / FireInternalEnergyLifetime;
    float edgeHeight = visualHeight * FireInternalEnergyEdgeHeightMultiplier;
    // FIRE_V2_CLEANUP: Use the retained system's own width limit.
    int clampedWidth = Mathf.Clamp(width, 1, FireInternalEnergyMaxWidth);
    float emissionRate = FireInternalEnergyEmissionRatePerWidth * clampedWidth;
    int maxParticles = Mathf.Clamp(
        Mathf.CeilToInt(emissionRate * FireInternalEnergyLifetime * 2.0f),
        FireInternalEnergyMinParticlesPerEmitter,
        FireInternalEnergyMaxParticlesPerEmitter);

    ConfigureFireInternalEnergyEmitter(
        fireInternalEnergyLeftParticleSystem,
        fireInternalEnergyLeftRenderer,
        new Vector3(leftX, 0f, -0.06f),
        1f,
        horizontalSpeed,
        edgeHeight,
        emissionRate,
        maxParticles,
        17);
    ConfigureFireInternalEnergyEmitter(
        fireInternalEnergyRightParticleSystem,
        fireInternalEnergyRightRenderer,
        new Vector3(rightX, 0f, -0.06f),
        -1f,
        horizontalSpeed,
        edgeHeight,
        emissionRate,
        maxParticles,
        43);
}

private Vector2 GetFireInternalEnergyVisualSize()
{
    Vector2 visualSize = originalSize;
    if ((visualSize.x <= 0f || visualSize.y <= 0f) && sr != null)
        visualSize = sr.size;

    if (visualSize.x <= 0f)
        visualSize.x = Mathf.Max(1, width) - 0.01f;

    if (visualSize.y <= 0f)
        visualSize.y = 0.99f;

    return visualSize;
}

private void ConfigureFireInternalEnergyEmitter(
    ParticleSystem particleSystem,
    ParticleSystemRenderer particleRenderer,
    Vector3 localPosition,
    float direction,
    float horizontalSpeed,
    float edgeHeight,
    float emissionRate,
    int maxParticles,
    int seedSalt)
{
    if (particleSystem == null || particleRenderer == null) return;
    if (particleRenderer.sharedMaterial == null)
        particleRenderer.sharedMaterial = GetFireInternalEnergySharedMaterial();

    bool wasPlaying = particleSystem.isPlaying;
    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    particleSystem.transform.localPosition = localPosition;
    bool usePrefabSettings = fireInternalEnergyFlowPrefab != null;

    // FIRE_V2_CLEANUP: Rebuilt internal energy flow uses small amber particles and organic bursts.
    ParticleSystem.MainModule main = particleSystem.main;
    if (!usePrefabSettings)
    {
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.duration = 1.0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.startSpeed = 0f;
        main.startLifetime = FireInternalEnergyLifetime;
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-15f, 15f);
        main.maxParticles = maxParticles;
    }
    particleSystem.useAutoRandomSeed = false;
    particleSystem.randomSeed = GetFireInternalEnergySeed(seedSalt);
    if (!usePrefabSettings)
    {
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.98f, 0.90f, 0.95f),
            new Color(1f, 0.95f, 0.75f, 0.90f));
    }

    ParticleSystem.EmissionModule emission = particleSystem.emission;
    if (!usePrefabSettings)
    {
        emission.enabled = true;
        emission.rateOverTime = emissionRate * 0.4f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0.0f, Mathf.Max(1, Mathf.FloorToInt(emissionRate * 0.2f))),
            new ParticleSystem.Burst(0.2f, Mathf.Max(1, Mathf.FloorToInt(emissionRate * 0.3f))),
            new ParticleSystem.Burst(0.5f, Mathf.Max(1, Mathf.FloorToInt(emissionRate * 0.2f)))
        });
    }

    ParticleSystem.ShapeModule shape = particleSystem.shape;
    if (!usePrefabSettings)
    {
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(0.015f, edgeHeight * 0.5f, 0f);
    }

    ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
    velocity.enabled = true;
    velocity.space = ParticleSystemSimulationSpace.Local;
    float signedSpeed = horizontalSpeed * Mathf.Sign(direction);
    velocity.x = new ParticleSystem.MinMaxCurve(signedSpeed * 0.6f, signedSpeed * 1.0f);
    if (!usePrefabSettings)
    {
        velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
    if (!usePrefabSettings)
    {
        colorOverLifetime.enabled = true;
        Gradient alphaEnvelope = new Gradient();
        alphaEnvelope.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1.0f, 0.95f), 0f),
                new GradientColorKey(new Color(1f, 0.98f, 0.85f), 0.5f),
                new GradientColorKey(new Color(1f, 0.90f, 0.60f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0.3f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaEnvelope);
    }

    if (!usePrefabSettings)
    {
        ParticleSystem.TrailModule trails = particleSystem.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = FireInternalEnergyTrailLifetime;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;
        trails.sizeAffectsLifetime = false;
        trails.minVertexDistance = 0.003f;
        trails.inheritParticleColor = true;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1.2f),
                new Keyframe(0.3f, 0.8f),
                new Keyframe(0.7f, 0.4f),
                new Keyframe(1f, 0f)));
        trails.colorOverLifetime = colorOverLifetime.color;
    }

    if (!usePrefabSettings)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 0.85f),
                new Keyframe(1f, 0.5f)));

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;
    }

    ParticleSystem.CollisionModule collision = particleSystem.collision;
    if (!usePrefabSettings)
        collision.enabled = false;

    ParticleSystem.TextureSheetAnimationModule textureSheetAnimation = particleSystem.textureSheetAnimation;
    if (!usePrefabSettings)
    {
        textureSheetAnimation.enabled = false;
        while (textureSheetAnimation.spriteCount > 0)
            textureSheetAnimation.RemoveSprite(0);
    }

    if (!usePrefabSettings)
    {
        Material sharedMaterial = GetFireInternalEnergySharedMaterial();
        SetFireInternalEnergyMaterialTint(sharedMaterial);
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.OldestInFront;
        particleRenderer.sharedMaterial = sharedMaterial;
        particleRenderer.trailMaterial = sharedMaterial;
    }
    else
    {
        if (particleRenderer.sharedMaterial == null)
        {
            Material sharedMaterial = GetFireInternalEnergySharedMaterial();
            particleRenderer.sharedMaterial = sharedMaterial;
        }
        if (particleRenderer.trailMaterial == null)
            particleRenderer.trailMaterial = particleRenderer.sharedMaterial;
    }

    if (wasPlaying)
        particleSystem.Play(true);
}

private uint GetFireInternalEnergySeed(int salt)
{
    return (uint)Mathf.Max(1, Mathf.FloorToInt(GetFireIdleHash01(2, salt) * 2147483646f));
}

private static void SetFireInternalEnergyMaterialTint(Material material)
{
    if (material == null)
        return;

    Color warmWhiteTint = new Color(1f, 0.95f, 0.85f, 1f);
    if (material.HasProperty("_Color"))
        material.SetColor("_Color", warmWhiteTint);

    if (material.HasProperty("_TintColor"))
        material.SetColor("_TintColor", warmWhiteTint);

    if (material.HasProperty("_BaseColor"))
        material.SetColor("_BaseColor", warmWhiteTint);
}

private Material GetFireInternalEnergySharedMaterial()
{
    if (fireIdleParticlePrefab != null)
    {
        ParticleSystemRenderer idleParticleRenderer = fireIdleParticlePrefab.GetComponent<ParticleSystemRenderer>();
        if (idleParticleRenderer != null && idleParticleRenderer.sharedMaterial != null)
            return idleParticleRenderer.sharedMaterial;
    }

    // Fallback: Runtime'da ateş rengi materyal oluştur
    Shader spriteShader = Shader.Find("Sprites/Default");
    if (spriteShader != null)
    {
        Material fallbackMaterial = new Material(spriteShader);
        fallbackMaterial.color = new Color(1f, 0.55f, 0.1f, 0.85f);
        fallbackMaterial.name = "Runtime_FireEnergyFallback";
        return fallbackMaterial;
    }

    return null;
}

private void ClearFireInternalEnergyFlow()
{
    StopFireInternalEnergyFlow();

    Transform flowRoot = transform.Find(FireInternalEnergyRootName);
    if (flowRoot != null)
        Destroy(flowRoot.gameObject);

    Transform legacyFlowRoot = transform.Find(LegacyFireInternalEnergyRootName);
    if (legacyFlowRoot != null)
        Destroy(legacyFlowRoot.gameObject);

    fireInternalEnergyLeftParticleSystem = null;
    fireInternalEnergyRightParticleSystem = null;
    fireInternalEnergyLeftRenderer = null;
    fireInternalEnergyRightRenderer = null;
}

private void StopFireInternalEnergyFlow()
{
    StopFireInternalEnergyEmitter(fireInternalEnergyLeftParticleSystem);
    StopFireInternalEnergyEmitter(fireInternalEnergyRightParticleSystem);
}

private void StopFireInternalEnergyEmitter(ParticleSystem particleSystem)
{
    if (particleSystem == null)
        return;

    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    particleSystem.Clear(true);
}

private void PlayFireInternalEnergyFlow(ParticleSystem particleSystem)
{
    if (particleSystem != null && !particleSystem.isPlaying)
        particleSystem.Play(true);
}

private void RefreshFireInternalEnergyFlowSorting()
{
    int sortingLayerId = sr != null ? sr.sortingLayerID : 0;
    int sortingOrder = sr != null ? sr.sortingOrder : 0;

    ApplyFireInternalEnergyRendererSorting(fireInternalEnergyLeftRenderer, sortingLayerId, sortingOrder);
    ApplyFireInternalEnergyRendererSorting(fireInternalEnergyRightRenderer, sortingLayerId, sortingOrder);
}

private void ApplyFireInternalEnergyRendererSorting(
    ParticleSystemRenderer particleRenderer,
    int sortingLayerId,
    int baseSortingOrder)
{
    if (particleRenderer == null)
        return;

    particleRenderer.sortingLayerID = sortingLayerId;
    particleRenderer.sortingOrder = baseSortingOrder + 1;
}

private void RefreshSliceInternalEnergyFlow()
{
    bool shouldShowInternalEnergy =
        isSpecialVisualActive &&
        blockType == BlockType.Slice &&
        width >= 1 &&
        width <= SliceInternalEnergyMaxWidth;

    if (!shouldShowInternalEnergy)
    {
        ClearSliceInternalEnergyFlow();
        return;
    }

    ResolveSliceInternalEnergyFlow();
    ConfigureSliceInternalEnergyEmitters();
    RefreshSliceInternalEnergyFlowSorting();

    PlaySliceInternalEnergyFlow(sliceInternalEnergyLeftParticleSystem);
    PlaySliceInternalEnergyFlow(sliceInternalEnergyRightParticleSystem);
}

private void ResolveSliceInternalEnergyFlow()
{
    if (sliceInternalEnergyLeftParticleSystem != null && sliceInternalEnergyRightParticleSystem != null)
        return;

    Transform flowRoot = transform.Find(SliceInternalEnergyRootName);
    if (flowRoot == null)
    {
        GameObject rootObject = sliceInternalEnergyFlowPrefab != null
            ? Instantiate(sliceInternalEnergyFlowPrefab, transform)
            : new GameObject(SliceInternalEnergyRootName);
        rootObject.name = SliceInternalEnergyRootName;
        if (rootObject.transform.parent != transform)
            rootObject.transform.SetParent(transform, false);
        flowRoot = rootObject.transform;
    }

    flowRoot.localPosition = Vector3.zero;
    flowRoot.localRotation = Quaternion.identity;
    flowRoot.localScale = Vector3.one;

    sliceInternalEnergyLeftParticleSystem = ResolveSliceInternalEnergyEmitter(
        flowRoot,
        SliceInternalEnergyLeftEmitterName,
        out sliceInternalEnergyLeftRenderer);
    sliceInternalEnergyRightParticleSystem = ResolveSliceInternalEnergyEmitter(
        flowRoot,
        SliceInternalEnergyRightEmitterName,
        out sliceInternalEnergyRightRenderer);
}

private ParticleSystem ResolveSliceInternalEnergyEmitter(
    Transform flowRoot,
    string emitterName,
    out ParticleSystemRenderer particleRenderer)
{
    particleRenderer = null;

    if (flowRoot == null)
        return null;

    Transform particleTransform = flowRoot.Find(emitterName);
    if (particleTransform == null)
    {
        GameObject particleObject = new GameObject(emitterName);
        particleObject.transform.SetParent(flowRoot, false);
        particleTransform = particleObject.transform;
    }

    particleTransform.localRotation = Quaternion.identity;
    particleTransform.localScale = Vector3.one;

    ParticleSystem particleSystem = particleTransform.GetComponent<ParticleSystem>();
    if (particleSystem == null)
        particleSystem = particleTransform.gameObject.AddComponent<ParticleSystem>();

    particleRenderer = particleTransform.GetComponent<ParticleSystemRenderer>();
    return particleSystem;
}

private void ConfigureSliceInternalEnergyEmitters()
{
    Vector2 visualSize = GetSliceInternalEnergyVisualSize();
    float visualWidth = Mathf.Max(0.01f, visualSize.x);
    float edgeInset = Mathf.Min(SliceInternalEnergyEdgeInset, visualWidth * 0.35f);
    float leftX = (-visualWidth * 0.5f) + edgeInset;
    float rightX = (visualWidth * 0.5f) - edgeInset;
    float travelDistance = Mathf.Max(0.05f, visualWidth - (edgeInset * 2f));
    float horizontalSpeed = travelDistance / SliceInternalEnergyLifetime;

    ConfigureSliceInternalEnergyEmitter(
        sliceInternalEnergyLeftParticleSystem,
        sliceInternalEnergyLeftRenderer,
        new Vector3(leftX, 0f, -0.06f),
        1f,
        horizontalSpeed,
        17);
    ConfigureSliceInternalEnergyEmitter(
        sliceInternalEnergyRightParticleSystem,
        sliceInternalEnergyRightRenderer,
        new Vector3(rightX, 0f, -0.06f),
        -1f,
        horizontalSpeed,
        43);
}

private Vector2 GetSliceInternalEnergyVisualSize()
{
    Vector2 visualSize = originalSize;
    if ((visualSize.x <= 0f || visualSize.y <= 0f) && sr != null)
        visualSize = sr.size;

    if (visualSize.x <= 0f)
        visualSize.x = Mathf.Max(1, width) - 0.01f;

    if (visualSize.y <= 0f)
        visualSize.y = 0.99f;

    return visualSize;
}

private void ConfigureSliceInternalEnergyEmitter(
    ParticleSystem particleSystem,
    ParticleSystemRenderer particleRenderer,
    Vector3 localPosition,
    float direction,
    float horizontalSpeed,
    int seedSalt)
{
    if (particleSystem == null || particleRenderer == null)
        return;

    bool wasPlaying = particleSystem.isPlaying;
    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    particleSystem.transform.localPosition = localPosition;
    bool usePrefabSettings = sliceInternalEnergyFlowPrefab != null;

    if (!usePrefabSettings)
        ConfigureSliceInternalEnergyFallback(particleSystem, particleRenderer);

    particleSystem.useAutoRandomSeed = false;
    particleSystem.randomSeed = GetSliceInternalEnergySeed(seedSalt);

    // Width-aware movement is runtime-owned; all particle styling remains prefab-owned.
    ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
    velocity.enabled = true;
    velocity.space = ParticleSystemSimulationSpace.Local;
    float signedSpeed = horizontalSpeed * Mathf.Sign(direction);
    velocity.x = new ParticleSystem.MinMaxCurve(signedSpeed * 0.6f, signedSpeed * 1.0f);

    if (particleRenderer.sharedMaterial == null)
        particleRenderer.sharedMaterial = GetSliceInternalEnergyFallbackMaterial();
    if (particleRenderer.trailMaterial == null)
        particleRenderer.trailMaterial = particleRenderer.sharedMaterial;

    if (wasPlaying)
        particleSystem.Play(true);
}

private static void ConfigureSliceInternalEnergyFallback(
    ParticleSystem particleSystem,
    ParticleSystemRenderer particleRenderer)
{
    ParticleSystem.MainModule main = particleSystem.main;
    main.loop = true;
    main.prewarm = true;
    main.playOnAwake = false;
    main.duration = 1.0f;
    main.simulationSpace = ParticleSystemSimulationSpace.Local;
    main.gravityModifier = 0f;
    main.startSpeed = 0f;
    main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 0.8f);
    main.startSize3D = false;
    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
    main.maxParticles = 18;
    main.startColor = new ParticleSystem.MinMaxGradient(
        new Color(0.55f, 0.92f, 1f, 0.78f),
        new Color(0.92f, 1f, 1f, 0.9f));

    ParticleSystem.EmissionModule emission = particleSystem.emission;
    emission.enabled = true;
    emission.rateOverTime = 1.0f;

    ParticleSystem.ShapeModule shape = particleSystem.shape;
    shape.enabled = true;
    shape.shapeType = ParticleSystemShapeType.Box;
    shape.scale = new Vector3(0.015f, 0.3f, 0f);

    ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
    colorOverLifetime.enabled = true;
    Gradient color = new Gradient();
    color.SetKeys(
        new[]
        {
            new GradientColorKey(new Color(0.5f, 0.9f, 1f), 0f),
            new GradientColorKey(Color.white, 0.5f),
            new GradientColorKey(new Color(0.35f, 0.8f, 1f), 1f)
        },
        new[]
        {
            new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(0.88f, 0.12f),
            new GradientAlphaKey(0.45f, 0.65f),
            new GradientAlphaKey(0f, 1f)
        });
    colorOverLifetime.color = new ParticleSystem.MinMaxGradient(color);

    ParticleSystem.TrailModule trails = particleSystem.trails;
    trails.enabled = true;
    trails.mode = ParticleSystemTrailMode.PerParticle;
    trails.ratio = 1f;
    trails.lifetime = 0.55f;
    trails.dieWithParticles = true;
    trails.sizeAffectsWidth = true;
    trails.sizeAffectsLifetime = false;
    trails.minVertexDistance = 0.003f;
    trails.inheritParticleColor = true;
    trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
    trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
        1f,
        new AnimationCurve(
            new Keyframe(0f, 0.85f),
            new Keyframe(0.35f, 0.6f),
            new Keyframe(0.75f, 0.28f),
            new Keyframe(1f, 0f)));
    trails.colorOverLifetime = colorOverLifetime.color;

    particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    particleRenderer.alignment = ParticleSystemRenderSpace.View;
    particleRenderer.sortMode = ParticleSystemSortMode.OldestInFront;
}

private uint GetSliceInternalEnergySeed(int salt)
{
    return (uint)Mathf.Max(1, Mathf.FloorToInt(GetFireIdleHash01(3, salt) * 2147483646f));
}

private static Material GetSliceInternalEnergyFallbackMaterial()
{
    Shader spriteShader = Shader.Find("Sprites/Default");
    if (spriteShader == null)
        return null;

    Material fallbackMaterial = new Material(spriteShader);
    fallbackMaterial.name = "Runtime_SliceEnergyFallback";
    fallbackMaterial.color = Color.white;
    return fallbackMaterial;
}

private void ClearSliceInternalEnergyFlow()
{
    StopSliceInternalEnergyFlow();

    Transform flowRoot = transform.Find(SliceInternalEnergyRootName);
    if (flowRoot != null)
        Destroy(flowRoot.gameObject);

    sliceInternalEnergyLeftParticleSystem = null;
    sliceInternalEnergyRightParticleSystem = null;
    sliceInternalEnergyLeftRenderer = null;
    sliceInternalEnergyRightRenderer = null;
}

private void StopSliceInternalEnergyFlow()
{
    StopSliceInternalEnergyEmitter(sliceInternalEnergyLeftParticleSystem);
    StopSliceInternalEnergyEmitter(sliceInternalEnergyRightParticleSystem);
}

private static void StopSliceInternalEnergyEmitter(ParticleSystem particleSystem)
{
    if (particleSystem == null)
        return;

    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    particleSystem.Clear(true);
}

private static void PlaySliceInternalEnergyFlow(ParticleSystem particleSystem)
{
    if (particleSystem != null && !particleSystem.isPlaying)
        particleSystem.Play(true);
}

private void RefreshSliceInternalEnergyFlowSorting()
{
    int sortingLayerId = sr != null ? sr.sortingLayerID : 0;
    int sortingOrder = sr != null ? sr.sortingOrder : 0;

    ApplySliceInternalEnergyRendererSorting(sliceInternalEnergyLeftRenderer, sortingLayerId, sortingOrder);
    ApplySliceInternalEnergyRendererSorting(sliceInternalEnergyRightRenderer, sortingLayerId, sortingOrder);
}

private static void ApplySliceInternalEnergyRendererSorting(
    ParticleSystemRenderer particleRenderer,
    int sortingLayerId,
    int baseSortingOrder)
{
    if (particleRenderer == null)
        return;

    particleRenderer.sortingLayerID = sortingLayerId;
    particleRenderer.sortingOrder = baseSortingOrder + 1;
}

private void RemoveLegacyFireInternalEnergyFlow()
{
    Transform legacyFlowRoot = transform.Find(LegacyFireInternalEnergyRootName);
    if (legacyFlowRoot != null)
        Destroy(legacyFlowRoot.gameObject);
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
#if false
private void RefreshFireSurfaceEnergy()
{
    bool shouldShowSurfaceEnergy =
        isSpecialVisualActive &&
        blockType == BlockType.Fire &&
        width >= 1 &&
        width <= MaxFireIdleFlameEmitters &&
        fireLocalDischargeFrames != null &&
        fireLocalDischargeFrames.Length == FireSurfaceEnergyFrameCount;

    if (!shouldShowSurfaceEnergy)
    {
        ClearFireSurfaceEnergy();
        return;
    }

    ResolveFireSurfaceEnergyRenderer();
    ConfigureFireSurfaceEnergyRenderer();

    if (fireSurfaceEnergyRoutine == null)
        fireSurfaceEnergyRoutine = StartCoroutine(FireSurfaceEnergyRoutine());
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private void ResolveFireSurfaceEnergyRenderer()
{
    if (fireSurfaceEnergyRenderer != null)
        return;

    Transform surfaceEnergyRoot = transform.Find(FireSurfaceEnergyRootName);
    if (surfaceEnergyRoot == null)
    {
        GameObject rootObject = new GameObject(FireSurfaceEnergyRootName);
        rootObject.transform.SetParent(transform, false);
        surfaceEnergyRoot = rootObject.transform;
    }

    Transform eventRendererTransform = surfaceEnergyRoot.Find(FireSurfaceEnergyRendererName);
    if (eventRendererTransform == null)
    {
        GameObject rendererObject = new GameObject(FireSurfaceEnergyRendererName);
        rendererObject.transform.SetParent(surfaceEnergyRoot, false);
        eventRendererTransform = rendererObject.transform;
    }

    fireSurfaceEnergyRenderer = eventRendererTransform.GetComponent<SpriteRenderer>();
    if (fireSurfaceEnergyRenderer == null)
        fireSurfaceEnergyRenderer = eventRendererTransform.gameObject.AddComponent<SpriteRenderer>();

    fireSurfaceEnergyRenderer.sprite = null;
    fireSurfaceEnergyRenderer.enabled = false;
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private void ConfigureFireSurfaceEnergyRenderer()
{
    if (fireSurfaceEnergyRenderer == null)
        return;

    Transform surfaceEnergyRoot = fireSurfaceEnergyRenderer.transform.parent;
    if (surfaceEnergyRoot != null)
    {
        surfaceEnergyRoot.localPosition = Vector3.zero;
        surfaceEnergyRoot.localRotation = Quaternion.identity;
        surfaceEnergyRoot.localScale = Vector3.one;
    }

    Transform surfaceEnergyTransform = fireSurfaceEnergyRenderer.transform;
    surfaceEnergyTransform.localRotation = Quaternion.identity;
    surfaceEnergyTransform.localScale = new Vector3(FireSurfaceEnergyScale, FireSurfaceEnergyScale, 1f);

    fireSurfaceEnergyRenderer.color = Color.white;
    fireSurfaceEnergyRenderer.drawMode = SpriteDrawMode.Simple;
    fireSurfaceEnergyRenderer.sortingLayerID = sr != null ? sr.sortingLayerID : 0;
    fireSurfaceEnergyRenderer.sortingOrder = sr != null ? sr.sortingOrder + 2 : 2;
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private IEnumerator FireSurfaceEnergyRoutine()
{
    float initialDelay = Mathf.Lerp(
        FireSurfaceEnergyInitialDelayMin,
        FireSurfaceEnergyInitialDelayMax,
        GetFireIdleHash01(0, 31)
    );
    yield return new WaitForSeconds(initialDelay);

    int eventSequence = Mathf.FloorToInt(GetFireIdleHash01(0, 41) * 1024f);
    int previousCellIndex = -1;
    int previousAnchorIndex = -1;
    while (isSpecialVisualActive && blockType == BlockType.Fire && width >= 1 && width <= MaxFireIdleFlameEmitters)
    {
        if (fireSurfaceEnergyRenderer == null ||
            fireLocalDischargeFrames == null ||
            fireLocalDischargeFrames.Length != FireSurfaceEnergyFrameCount)
        {
            break;
        }

        int cellIndex = GetFireSurfaceEnergyCellIndex(eventSequence, previousCellIndex);
        int anchorIndex = GetFireSurfaceEnergyAnchorIndex(eventSequence, cellIndex, previousCellIndex, previousAnchorIndex);
        fireSurfaceEnergyRenderer.transform.localPosition = GetFireSurfaceEnergyLocalPosition(cellIndex, anchorIndex);
        float eventAlpha = Mathf.Lerp(
            FireSurfaceEnergyAlphaMin,
            FireSurfaceEnergyAlphaMax,
            GetFireIdleHash01(eventSequence, 67)
        );
        fireSurfaceEnergyRenderer.color = new Color(1f, 1f, 1f, eventAlpha);
        fireSurfaceEnergyRenderer.enabled = true;
        for (int frameIndex = 0; frameIndex < FireSurfaceEnergyFrameCount; frameIndex++)
        {
            fireSurfaceEnergyRenderer.sprite = fireLocalDischargeFrames[frameIndex];
            yield return new WaitForSeconds(1f / FireSurfaceEnergyFramesPerSecond);
        }

        fireSurfaceEnergyRenderer.sprite = null;
        fireSurfaceEnergyRenderer.enabled = false;

        previousCellIndex = cellIndex;
        previousAnchorIndex = anchorIndex;
        eventSequence++;
        float idleDelay = GetFireSurfaceEnergyIdleDelay(eventSequence);
        yield return new WaitForSeconds(idleDelay);
    }

    if (fireSurfaceEnergyRenderer != null)
    {
        fireSurfaceEnergyRenderer.sprite = null;
        fireSurfaceEnergyRenderer.enabled = false;
    }

    fireSurfaceEnergyRoutine = null;
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private int GetFireSurfaceEnergyCellIndex(int eventSequence, int previousCellIndex)
{
    int cellCount = Mathf.Clamp(width, 1, MaxFireIdleFlameEmitters);
    if (cellCount == 1)
        return 0;

    int cellIndex = Mathf.FloorToInt(GetFireIdleHash01(eventSequence, 43) * cellCount);
    if (cellIndex != previousCellIndex)
        return cellIndex;

    int alternateOffset = 1 + Mathf.FloorToInt(GetFireIdleHash01(eventSequence, 47) * (cellCount - 1));
    return (cellIndex + alternateOffset) % cellCount;
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private int GetFireSurfaceEnergyAnchorIndex(int eventSequence, int cellIndex, int previousCellIndex, int previousAnchorIndex)
{
    int firstCandidate = Mathf.FloorToInt(GetFireIdleHash01(eventSequence + cellIndex, 53) * FireSurfaceEnergyAnchorOffsets.Length);
    for (int offset = 0; offset < FireSurfaceEnergyAnchorOffsets.Length; offset++)
    {
        int anchorIndex = (firstCandidate + offset) % FireSurfaceEnergyAnchorOffsets.Length;
        bool repeatsPreviousCombination = cellIndex == previousCellIndex && anchorIndex == previousAnchorIndex;
        if (!repeatsPreviousCombination && IsFireSurfaceEnergyAnchorSafe(cellIndex, anchorIndex))
            return anchorIndex;
    }

    return 0;
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private bool IsFireSurfaceEnergyAnchorSafe(int cellIndex, int anchorIndex)
{
    if (width == 1)
        return true;

    float anchorX = FireSurfaceEnergyAnchorOffsets[anchorIndex].x;
    if (cellIndex == 0 && anchorX < 0f)
        return false;

    if (cellIndex == width - 1 && anchorX > 0f)
        return false;

    return true;
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private Vector3 GetFireSurfaceEnergyLocalPosition(int cellIndex, int anchorIndex)
{
    float leftmostCellCenter = -((width - 1) * 0.5f);
    Vector2 anchorOffset = FireSurfaceEnergyAnchorOffsets[anchorIndex];
    float localX = width == 1 ? 0f : leftmostCellCenter + cellIndex + anchorOffset.x;
    return new Vector3(localX, anchorOffset.y, -0.025f);
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private float GetFireSurfaceEnergyIdleDelay(int eventSequence)
{
    float minimumDelay;
    float maximumDelay;

    switch (width)
    {
        case 1:
            minimumDelay = 0.90f;
            maximumDelay = 1.40f;
            break;
        case 2:
            minimumDelay = 0.70f;
            maximumDelay = 1.10f;
            break;
        case 3:
            minimumDelay = 0.55f;
            maximumDelay = 0.90f;
            break;
        default:
            minimumDelay = 0.45f;
            maximumDelay = 0.80f;
            break;
    }

    return Mathf.Lerp(minimumDelay, maximumDelay, GetFireIdleHash01(eventSequence, 61));
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private void ClearFireSurfaceEnergy()
{
    StopFireSurfaceEnergyRoutine();

    Transform existingSurfaceEnergyRoot = transform.Find(FireSurfaceEnergyRootName);
    if (existingSurfaceEnergyRoot != null)
        Destroy(existingSurfaceEnergyRoot.gameObject);

    fireSurfaceEnergyRenderer = null;
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
private void StopFireSurfaceEnergyRoutine()
{
    if (fireSurfaceEnergyRoutine != null)
    {
        StopCoroutine(fireSurfaceEnergyRoutine);
        fireSurfaceEnergyRoutine = null;
    }

    if (fireSurfaceEnergyRenderer != null)
    {
        fireSurfaceEnergyRenderer.sprite = null;
        fireSurfaceEnergyRenderer.enabled = false;
    }
}
#endif

private void OnDisable()
{
    StopFireInternalEnergyFlow();
    StopSliceInternalEnergyFlow();
    SetSliceSymbolActive(false);
}

private void OnEnable()
{
    if (!Application.isPlaying)
        return;

    if (isSpecialVisualActive && blockType == BlockType.Fire)
    {
        // FIRE_V2_CLEANUP: Re-enable the retained FireInternalEnergyFlow only.
        // RefreshFireSliceVisuals();
        ConfigureFireSymbol();
        ClearSliceInternalEnergyFlow();
        SetSliceSymbolActive(false);
        RefreshFireInternalEnergyFlow();
    }
    else if (isSpecialVisualActive && blockType == BlockType.Slice)
    {
        ClearFireSymbol();
        ClearFireInternalEnergyFlow();
        RefreshSliceInternalEnergyFlow();
        SetSliceSymbolActive(true);
    }
    else
    {
        ClearSliceInternalEnergyFlow();
        SetSliceSymbolActive(false);
    }
}

private void OnDestroy()
{
    ClearSliceInternalEnergyFlow();
    SetSliceSymbolActive(false);
}

// FIRE_V2_CLEANUP: Legacy Fire Surface Energy method preserved but disabled.
#if false
private void RefreshFireSurfaceEnergySorting()
{
    if (fireSurfaceEnergyRenderer == null)
        return;

    fireSurfaceEnergyRenderer.sortingLayerID = sr != null ? sr.sortingLayerID : 0;
    fireSurfaceEnergyRenderer.sortingOrder = sr != null ? sr.sortingOrder + 2 : 2;
}
#endif

#if false
// FIRE_V2_CLEANUP: Legacy Fire surface scrubber disabled with the old surface system.
private void RemoveLegacyFireSurfaceVisuals()
{
    RemoveLegacyFireSurfaceVisual("FireSurfaceEnergy");
    RemoveLegacyFireSurfaceVisual("FireEnergyOverlay");
    RemoveLegacyFireSurfaceVisual("FireEnergyFilaments");
    RemoveLegacyFireSurfaceVisual("FireEnergyFilamentRoot");
    RemoveLegacyFireSurfaceVisual("FireIdleMarks");

    LineRenderer[] lineRenderers = GetComponentsInChildren<LineRenderer>(true);
    for (int i = 0; i < lineRenderers.Length; i++)
    {
        LineRenderer lineRenderer = lineRenderers[i];
        if (lineRenderer == null)
            continue;

        string objectName = lineRenderer.gameObject.name;
        if (objectName.Contains("FireEnergyFilament") || objectName.Contains("FireEnergy"))
            Destroy(lineRenderer.gameObject);
    }
}

// FIRE_V2_CLEANUP: Retained as the helper for the required active legacy-surface cleanup call.
private void RemoveLegacyFireSurfaceVisual(string childName)
{
    Transform child = transform.Find(childName);
    if (child != null)
        Destroy(child.gameObject);
}
#endif

// FIRE_V2_CLEANUP: Legacy Fire Idle Particle method preserved but disabled.
#if false
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
#endif

public System.Collections.IEnumerator CrunchAndDestroy(GameObject explosionPrefab)
    {
        yield return StartCoroutine(CrunchAndDestroy(explosionPrefab, null, true));
    }

public System.Collections.IEnumerator CrunchAndDestroy(GameObject explosionPrefab, GameObject overrideFxPrefab, bool useDefaultFxIfNull, float duration = 0.15f)
    {
        yield return StartCoroutine(CrunchAndDestroy(explosionPrefab, overrideFxPrefab, useDefaultFxIfNull, duration, false));
    }

public System.Collections.IEnumerator CrunchAndDestroy(GameObject explosionPrefab, GameObject overrideFxPrefab, bool useDefaultFxIfNull, float duration, bool collapseDownward)
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
        Vector3 originalPosition = transform.position;
        float originalVisualHeight = 0f;
        if (collapseDownward)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            originalVisualHeight = sr != null ? sr.bounds.size.y : 0f;
        }
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Orijinal boyutundan sıfıra doğru, döndürmeden, temizce küçült
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float remainingScale = 1f - progress;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);

            if (collapseDownward && originalVisualHeight > 0f)
            {
                transform.position = originalPosition + Vector3.down * (originalVisualHeight * (1f - remainingScale) * 0.5f);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Gerçekten Yok Et
        transform.localScale = Vector3.zero;
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
