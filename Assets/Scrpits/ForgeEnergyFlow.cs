using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(100)]
public class ForgeEnergyFlow : MonoBehaviour
{
    [SerializeField] private float cycleDuration = 8f;
    [SerializeField] private float horizontalAmplitude = 0.018f;
    [SerializeField] private float verticalAmplitude = 0f;
    [SerializeField] private float horizontalScaleAmplitude = 0f;
    [SerializeField] private float verticalScaleAmplitude = 0f;
    [SerializeField] private float alphaAmplitude = 0f;
    [SerializeField] private float phaseOffset = 0.25f;

    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private float baseAlpha;
    private bool hasCachedBaseState;
    private float lastHorizontalOffset;
    private float lastVerticalOffset;
    private float lastHorizontalScaleMultiplier = 1f;
    private float lastVerticalScaleMultiplier = 1f;
    private float lastAlphaOffset;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        CacheBaseState();
        ApplyFlow();
    }

    private void Update()
    {
        ApplyFlow();
    }

    private void OnDisable()
    {
        if (!hasCachedBaseState || spriteRenderer == null)
            return;

        RemoveLastFlow();

        Vector3 restoredPosition = transform.localPosition;
        restoredPosition.x = baseLocalPosition.x;
        transform.localPosition = restoredPosition;

        Color color = spriteRenderer.color;
        color.a = alphaAmplitude > 0f ? baseAlpha : Mathf.Clamp01(color.a - lastAlphaOffset);
        spriteRenderer.color = color;

        if (horizontalScaleAmplitude > 0f || verticalScaleAmplitude > 0f)
            transform.localScale = baseLocalScale;
    }

    private void CacheBaseState()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        baseAlpha = spriteRenderer.color.a;
        hasCachedBaseState = true;
    }

    private void ApplyFlow()
    {
        if (!hasCachedBaseState || spriteRenderer == null)
            return;

        float safeDuration = Mathf.Max(0.01f, cycleDuration);
        float angle = ((Time.time / safeDuration) + phaseOffset) * Mathf.PI * 2f;
        float primary = Mathf.Sin(angle);
        float secondary = Mathf.Sin(angle + Mathf.PI * 0.5f);

        RemoveLastFlow();

        Vector3 currentPosition = transform.localPosition;
        lastHorizontalOffset = primary * horizontalAmplitude;
        lastVerticalOffset = secondary * verticalAmplitude;
        currentPosition.x += lastHorizontalOffset;
        currentPosition.y += lastVerticalOffset;
        transform.localPosition = currentPosition;

        Vector3 currentScale = transform.localScale;
        lastHorizontalScaleMultiplier = 1f + secondary * horizontalScaleAmplitude;
        lastVerticalScaleMultiplier = 1f + primary * verticalScaleAmplitude;
        currentScale.x *= lastHorizontalScaleMultiplier;
        currentScale.y *= lastVerticalScaleMultiplier;
        transform.localScale = currentScale;

        if (alphaAmplitude > 0f)
        {
            Color color = spriteRenderer.color;
            lastAlphaOffset = secondary * alphaAmplitude;
            color.a = Mathf.Clamp01(color.a + lastAlphaOffset);
            spriteRenderer.color = color;
        }
        else
        {
            lastAlphaOffset = 0f;
        }
    }

    private void RemoveLastFlow()
    {
        Vector3 currentPosition = transform.localPosition;
        currentPosition.x -= lastHorizontalOffset;
        currentPosition.y -= lastVerticalOffset;
        transform.localPosition = currentPosition;

        Vector3 currentScale = transform.localScale;
        if (!Mathf.Approximately(lastHorizontalScaleMultiplier, 0f))
            currentScale.x /= lastHorizontalScaleMultiplier;
        if (!Mathf.Approximately(lastVerticalScaleMultiplier, 0f))
            currentScale.y /= lastVerticalScaleMultiplier;
        transform.localScale = currentScale;

        lastHorizontalOffset = 0f;
        lastVerticalOffset = 0f;
        lastHorizontalScaleMultiplier = 1f;
        lastVerticalScaleMultiplier = 1f;
    }
}
