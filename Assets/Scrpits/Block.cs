using UnityEngine;
using System.Collections; // Coroutine için şart
using System.Collections.Generic;


public class Block : MonoBehaviour
{
    private const float ChainBreakOverlayAnimDuration = 0.12f;
    private static readonly Vector3 ChainOverlayDefaultScale = new Vector3(1.5f, 1.5f, 1f);

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
    [Header("Chain System")]
    [SerializeField] private Transform chainOverlayRoot;
    [SerializeField] private GameObject chainOverlayPrefab;
    [SerializeField] private GameObject chainBreakFXPrefab;
    [SerializeField] private SpriteRenderer flashOverlayRenderer;
    [SerializeField] private Transform vfxAnchor;
    [SerializeField] private float chainBreakFlashDuration = 0.10f;
    [SerializeField] private float chainBreakShakeDuration = 0.15f;
    [SerializeField] private float chainBreakShakeStrength = 0.04f;
    [SerializeField] private float chainBreakPunchScale = 1.06f;
    private int chainCount = 0;
    private readonly List<GameObject> spawnedChainOverlays = new List<GameObject>();
    private readonly HashSet<GameObject> animatingChainOverlays = new HashSet<GameObject>();
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
        originalSize = new Vector2(blockWidth - 0.04f, 0.96f);
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
    SetChained(chained ? Mathf.Max(1, width) : 0);
}

public void SetChained(int count)
{
    chainCount = Mathf.Max(0, count);
    isChained = chainCount > 0;

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

    GameObject overlayToBreak = GetOverlayToBreak();
    chainCount--;

    if (overlayToBreak != null)
    {
        PlayChainBreakFX(overlayToBreak.transform.position);
        StartCoroutine(AnimateBrokenChainOverlayRoutine(overlayToBreak));
    }
    else
    {
        PlayChainBreakFX();
    }

    if (chainBreakFeedbackRoutine != null)
        StopCoroutine(chainBreakFeedbackRoutine);

    chainBreakFeedbackRoutine = StartCoroutine(PlayChainBreakFeedback());

    if (chainCount <= 0)
    {
        chainCount = 0;
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
    return chainCount;
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
        chainOverlayRoot = transform;

    if (chainOverlayPrefab == null)
    {
        Transform existingOverlay = transform.Find("ChainOverlay");
        if (existingOverlay != null)
        {
            chainOverlayPrefab = existingOverlay.gameObject;
            chainOverlayPrefab.SetActive(false);
        }
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

        if (animatingChainOverlays.Contains(overlay))
            continue;

        overlay.transform.localPosition = GetChainOverlayLocalPosition(i);
        overlay.SetActive(isChained && i < chainCount);
        SyncChainOverlayRenderer(overlay);
    }
}

private GameObject CreateChainOverlayInstance(int overlayIndex)
{
    if (chainOverlayPrefab == null)
        return null;

    GameObject overlay = Instantiate(chainOverlayPrefab, chainOverlayRoot);
    overlay.name = $"ChainOverlay_{overlayIndex}";
    overlay.transform.localScale = ChainOverlayDefaultScale;
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

    overlayRenderer.sortingOrder = sr.sortingOrder + 2;
    overlay.transform.localScale = ChainOverlayDefaultScale;
}

private GameObject GetOverlayToBreak()
{
    int overlayIndex = Mathf.Clamp(chainCount - 1, 0, spawnedChainOverlays.Count - 1);

    if (overlayIndex < 0 || overlayIndex >= spawnedChainOverlays.Count)
        return null;

    return spawnedChainOverlays[overlayIndex];
}

private IEnumerator AnimateBrokenChainOverlayRoutine(GameObject overlay)
{
    if (overlay == null)
        yield break;

    animatingChainOverlays.Add(overlay);

    Transform overlayTransform = overlay.transform;
    SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();

    Vector3 originalScale = ChainOverlayDefaultScale;
    Vector3 punchScale = originalScale * 1.18f;
    Vector3 originalLocalPosition = overlayTransform.localPosition;
    Color originalColor = overlayRenderer != null ? overlayRenderer.color : Color.white;

    float elapsed = 0f;

    while (elapsed < ChainBreakOverlayAnimDuration)
    {
        float t = elapsed / ChainBreakOverlayAnimDuration;
        float shakeStrength = 0.05f * (1f - t);
        Vector2 shakeOffset = Random.insideUnitCircle * shakeStrength;

        overlayTransform.localScale = Vector3.Lerp(punchScale, Vector3.zero, t);
        overlayTransform.localPosition = originalLocalPosition + new Vector3(shakeOffset.x, shakeOffset.y, 0f);

        if (overlayRenderer != null)
        {
            Color c = originalColor;
            c.a = Mathf.Lerp(originalColor.a, 0f, t);
            overlayRenderer.color = c;
        }

        elapsed += Time.deltaTime;
        yield return null;
    }

    overlayTransform.localPosition = originalLocalPosition;
    overlayTransform.localScale = ChainOverlayDefaultScale;

    if (overlayRenderer != null)
    {
        Color c = originalColor;
        c.a = originalColor.a;
        overlayRenderer.color = c;
    }

    overlay.SetActive(false);
    animatingChainOverlays.Remove(overlay);
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
