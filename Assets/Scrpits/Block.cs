using UnityEngine;
using System.Collections; // Coroutine için şart


public class Block : MonoBehaviour
{
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
    private GameObject chainVisual;
    private GameObject iceVisual; // Üzerine eklenecek buz katmanı
    private MaterialPropertyBlock mpb;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Shader'daki renk değişkeninin adı, URP'de genelde "_BaseColor" veya "_EmissionColor" olabilir.
    
    [Header("Görsel Efektler")]
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
        blockColor = c; 
        
        if (mpb == null) mpb = new MaterialPropertyBlock();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorProperty, c);
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

        transform.localScale = Vector3.one;
        UpdateSpecialVisualState();
        UpdateTrailColor(colorData);
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
        isChained = chained;
        if (isChained)
            blockType = BlockType.Chained;
        else if (!isRock && !isFrozen)
            blockType = BlockType.Normal;

        if (isChained)
        {
            if (chainVisual == null)
            {
                chainVisual = new GameObject("ChainVisual");
                chainVisual.transform.SetParent(this.transform);
                chainVisual.transform.localPosition = Vector3.zero;
                
                SpriteRenderer mySr = GetComponent<SpriteRenderer>();
                SpriteRenderer chainSr = chainVisual.AddComponent<SpriteRenderer>();
                
                chainSr.sprite = mySr.sprite;
                chainSr.drawMode = mySr.drawMode;
                chainSr.size = mySr.size;
                
                // Şimdilik zinciri temsil etmesi için koyu metalik ve yarı saydam yapalım
                chainSr.color = new Color(0.1f, 0.1f, 0.1f, 0.7f); 
                chainSr.sortingOrder = mySr.sortingOrder + 2; // En önde dursun
                
                chainVisual.transform.localScale = Vector3.one; 
            }
            chainVisual.SetActive(true);
        }
        else
        {
            // Zinciri Kır (Kapat)
            if (chainVisual != null) chainVisual.SetActive(false);
        }
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
        // 1. Patlama Efekti (Partiküller)
        if (explosionPrefab != null) {
            GameObject effect = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            var main = effect.GetComponent<ParticleSystem>().main;
            Color finalColor = blockColor;
            finalColor.a = 1.0f;
            main.startColor = new ParticleSystem.MinMaxGradient(finalColor);
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
