using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ForgeEnergyLeaksController : MonoBehaviour
{
    private static readonly int LeakTimeId = Shader.PropertyToID("_LeakTime");
    private static readonly int LeakIntensityId = Shader.PropertyToID("_LeakIntensity");
    private static readonly int HighlightSpeedAId = Shader.PropertyToID("_HighlightSpeedA");
    private static readonly int HighlightSpeedBId = Shader.PropertyToID("_HighlightSpeedB");
    private static readonly int HighlightWidthAId = Shader.PropertyToID("_HighlightWidthA");
    private static readonly int HighlightWidthBId = Shader.PropertyToID("_HighlightWidthB");
    private static readonly int HighlightIntensityAId = Shader.PropertyToID("_HighlightIntensityA");
    private static readonly int HighlightIntensityBId = Shader.PropertyToID("_HighlightIntensityB");
    private static readonly int AlphaMinId = Shader.PropertyToID("_AlphaMin");
    private static readonly int AlphaMaxId = Shader.PropertyToID("_AlphaMax");
    private static readonly int AlphaPulseSpeedId = Shader.PropertyToID("_AlphaPulseSpeed");
    private static readonly int PhaseSpreadId = Shader.PropertyToID("_PhaseSpread");
    private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");

    [SerializeField, Range(0f, 2f)] private float leakIntensity = 1f;
    [SerializeField] private float highlightSpeedA = 0.16f;
    [SerializeField] private float highlightSpeedB = 0.095f;
    [SerializeField, Range(0.01f, 0.3f)] private float highlightWidthA = 0.085f;
    [SerializeField, Range(0.01f, 0.3f)] private float highlightWidthB = 0.055f;
    [SerializeField, Range(0f, 1f)] private float highlightIntensityA = 0.42f;
    [SerializeField, Range(0f, 1f)] private float highlightIntensityB = 0.26f;
    [SerializeField, Range(0f, 1f)] private float alphaMin = 0.72f;
    [SerializeField, Range(0f, 1f)] private float alphaMax = 1f;
    [SerializeField] private float alphaPulseSpeed = 0.13f;
    [SerializeField] private float phaseSpread = 18f;
    [SerializeField, Range(0f, 0.2f)] private float edgeFade = 0.025f;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private MaterialPropertyBlock cachedPropertyBlock;
    private Color baseColor;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private bool hasCachedBaseState;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        EnsureReferences();
        CacheBaseState();
        ApplyLeakProperties();
    }

    private void Update()
    {
        ApplyLeakProperties();
    }

    private void OnDisable()
    {
        if (!hasCachedBaseState || spriteRenderer == null)
            return;

        transform.localPosition = baseLocalPosition;
        transform.localScale = baseLocalScale;
        spriteRenderer.color = baseColor;
        spriteRenderer.SetPropertyBlock(cachedPropertyBlock);
    }

    private void EnsureReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (cachedPropertyBlock == null)
            cachedPropertyBlock = new MaterialPropertyBlock();
    }

    private void CacheBaseState()
    {
        if (spriteRenderer == null)
            return;

        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        baseColor = spriteRenderer.color;
        spriteRenderer.GetPropertyBlock(cachedPropertyBlock);
        hasCachedBaseState = true;
    }

    private void ApplyLeakProperties()
    {
        if (spriteRenderer == null)
            EnsureReferences();

        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(LeakTimeId, Time.time);
        propertyBlock.SetFloat(LeakIntensityId, leakIntensity);
        propertyBlock.SetFloat(HighlightSpeedAId, highlightSpeedA);
        propertyBlock.SetFloat(HighlightSpeedBId, highlightSpeedB);
        propertyBlock.SetFloat(HighlightWidthAId, highlightWidthA);
        propertyBlock.SetFloat(HighlightWidthBId, highlightWidthB);
        propertyBlock.SetFloat(HighlightIntensityAId, highlightIntensityA);
        propertyBlock.SetFloat(HighlightIntensityBId, highlightIntensityB);
        propertyBlock.SetFloat(AlphaMinId, alphaMin);
        propertyBlock.SetFloat(AlphaMaxId, alphaMax);
        propertyBlock.SetFloat(AlphaPulseSpeedId, alphaPulseSpeed);
        propertyBlock.SetFloat(PhaseSpreadId, phaseSpread);
        propertyBlock.SetFloat(EdgeFadeId, edgeFade);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
