using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ForgeEnergyWaveController : MonoBehaviour
{
    private static readonly int WaveTimeId = Shader.PropertyToID("_WaveTime");
    private static readonly int WaveAmplitudeAId = Shader.PropertyToID("_WaveAmplitudeA");
    private static readonly int WaveFrequencyAId = Shader.PropertyToID("_WaveFrequencyA");
    private static readonly int WaveSpeedAId = Shader.PropertyToID("_WaveSpeedA");
    private static readonly int WaveAmplitudeBId = Shader.PropertyToID("_WaveAmplitudeB");
    private static readonly int WaveFrequencyBId = Shader.PropertyToID("_WaveFrequencyB");
    private static readonly int WaveSpeedBId = Shader.PropertyToID("_WaveSpeedB");
    private static readonly int VerticalBiasId = Shader.PropertyToID("_VerticalBias");
    private static readonly int WaveIntensityId = Shader.PropertyToID("_WaveIntensity");

    [SerializeField, Range(0f, 0.08f)] private float waveAmplitudeA = 0.042f;
    [SerializeField] private float waveFrequencyA = 2.1f;
    [SerializeField] private float waveSpeedA = 0.23f;
    [SerializeField, Range(0f, 0.08f)] private float waveAmplitudeB = 0.018f;
    [SerializeField] private float waveFrequencyB = 4.3f;
    [SerializeField] private float waveSpeedB = 0.31f;
    [SerializeField, Range(-0.05f, 0.05f)] private float verticalBias = 0f;
    [SerializeField, Range(0f, 2f)] private float waveIntensity = 1f;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private MaterialPropertyBlock cachedPropertyBlock;
    private bool hasCachedPropertyBlock;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        EnsureReferences();
        CachePropertyBlock();
        ApplyWaveProperties();
    }

    private void Update()
    {
        ApplyWaveProperties();
    }

    private void OnDisable()
    {
        if (spriteRenderer == null || !hasCachedPropertyBlock)
            return;

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

    private void CachePropertyBlock()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(cachedPropertyBlock);
        hasCachedPropertyBlock = true;
    }

    private void ApplyWaveProperties()
    {
        if (spriteRenderer == null)
            EnsureReferences();

        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(WaveTimeId, Time.time);
        propertyBlock.SetFloat(WaveAmplitudeAId, waveAmplitudeA);
        propertyBlock.SetFloat(WaveFrequencyAId, waveFrequencyA);
        propertyBlock.SetFloat(WaveSpeedAId, waveSpeedA);
        propertyBlock.SetFloat(WaveAmplitudeBId, waveAmplitudeB);
        propertyBlock.SetFloat(WaveFrequencyBId, waveFrequencyB);
        propertyBlock.SetFloat(WaveSpeedBId, waveSpeedB);
        propertyBlock.SetFloat(VerticalBiasId, verticalBias);
        propertyBlock.SetFloat(WaveIntensityId, waveIntensity);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
