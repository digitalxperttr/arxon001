using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ForgeEnergyBreathing : MonoBehaviour
{
    [SerializeField] private float duration = 3f;
    [SerializeField] private float alphaAmplitude = 0.035f;
    [SerializeField] private float scaleAmplitude = 0.008f;
    [SerializeField] private float verticalAmplitude = 0.006f;

    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private float baseAlpha;
    private bool hasCachedBaseState;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        CacheBaseState();
        ApplyBreathing();
    }

    private void Update()
    {
        ApplyBreathing();
    }

    private void OnDisable()
    {
        if (!hasCachedBaseState || spriteRenderer == null)
            return;

        transform.localPosition = baseLocalPosition;
        transform.localScale = baseLocalScale;

        Color color = spriteRenderer.color;
        color.a = baseAlpha;
        spriteRenderer.color = color;
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

    private void ApplyBreathing()
    {
        if (!hasCachedBaseState || spriteRenderer == null)
            return;

        float safeDuration = Mathf.Max(0.01f, duration);
        float pulse = Mathf.Sin((Time.time / safeDuration) * Mathf.PI * 2f);
        float scaleMultiplier = 1f + pulse * scaleAmplitude;

        transform.localScale = new Vector3(
            baseLocalScale.x * scaleMultiplier,
            baseLocalScale.y * scaleMultiplier,
            baseLocalScale.z);
        transform.localPosition = baseLocalPosition + Vector3.up * (pulse * verticalAmplitude);

        Color color = spriteRenderer.color;
        color.a = Mathf.Clamp01(baseAlpha + pulse * alphaAmplitude);
        spriteRenderer.color = color;
    }
}
