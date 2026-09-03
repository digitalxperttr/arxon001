using System.Collections;
using UnityEngine;

public class HintVisualGuide : MonoBehaviour
{
    private const int DefaultSortingOrder = 35;
    private const float BaseScale = 0.68f;
    private const float SlideDistanceFraction = 0.38f;

    [Header("Visual Settings")]
    [Tooltip("Özel ok/rozet sprite'ı atanmazsa kod prosedürel 'Floating Glass' rozet üretir.")]
    [SerializeField] private Sprite customBadgeSprite;
    [SerializeField] private int sortingOrder = DefaultSortingOrder;

    private SpriteRenderer indicatorRenderer;
    private Sprite generatedGlassBadgeSprite;
    private Coroutine animationRoutine;
    private Block currentTargetBlock;
    private HintMove currentMove;

    private void Awake()
    {
        EnsureRenderer();
        Hide();
    }

    private void EnsureRenderer()
    {
        if (indicatorRenderer == null)
        {
            indicatorRenderer = GetComponent<SpriteRenderer>();
            if (indicatorRenderer == null)
            {
                indicatorRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        indicatorRenderer.sortingOrder = sortingOrder;

        if (customBadgeSprite != null)
        {
            indicatorRenderer.sprite = customBadgeSprite;
        }
        else if (generatedGlassBadgeSprite == null)
        {
            generatedGlassBadgeSprite = GenerateFloatingGlassBadgeSprite();
            indicatorRenderer.sprite = generatedGlassBadgeSprite;
        }

        indicatorRenderer.drawMode = SpriteDrawMode.Simple;
    }

    public void Show(Block block, HintMove move)
    {
        if (block == null)
        {
            Hide();
            return;
        }

        if (currentTargetBlock != null && currentTargetBlock != block)
        {
            ResetBlockPosition(currentTargetBlock);
        }

        EnsureRenderer();

        currentTargetBlock = block;
        currentMove = move;

        // Katman ve sıralamayı bloğa göre ayarla (bloğun önünde parlasın)
        SpriteRenderer blockSr = block.GetComponent<SpriteRenderer>();
        int baseOrder = blockSr != null ? blockSr.sortingOrder : 10;
        int layerID = blockSr != null ? blockSr.sortingLayerID : 0;

        indicatorRenderer.sortingLayerID = layerID;
        indicatorRenderer.sortingOrder = baseOrder + 20;

        // Rozeti bloğun tam merkezine bağla (asla tüm bloğu boyamaz, hep sabit şık boyutta kalır)
        transform.SetParent(block.transform);
        transform.localPosition = new Vector3(0f, 0f, -0.15f);
        transform.localRotation = Quaternion.identity;

        bool movesRight = move.movesRight;
        transform.localScale = new Vector3(movesRight ? BaseScale : -BaseScale, BaseScale, 1f);

        gameObject.SetActive(true);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        float slideDistance = Mathf.Clamp(Mathf.Abs(move.toX - move.fromX) * SlideDistanceFraction, 0.22f, 0.42f);
        animationRoutine = StartCoroutine(BlockSlideHintLoop(block, movesRight, slideDistance));
    }

    public void Hide()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        if (currentTargetBlock != null)
        {
            ResetBlockPosition(currentTargetBlock);
            currentTargetBlock = null;
        }

        transform.SetParent(null);
        gameObject.SetActive(false);
    }

    private void ResetBlockPosition(Block block)
    {
        if (block == null) return;
        block.transform.position = new Vector3(
            block.x + (block.width - 1) * 0.5f,
            block.y,
            block.transform.position.z
        );
    }

    private IEnumerator BlockSlideHintLoop(Block block, bool movesRight, float slideDistance)
    {
        float dir = movesRight ? 1f : -1f;
        Vector3 originPos = new Vector3(
            block.x + (block.width - 1) * 0.5f,
            block.y,
            block.transform.position.z
        );

        const float slideOutDuration = 0.42f;
        const float slideBackDuration = 0.38f;
        const float restDuration = 0.32f;
        const float totalDuration = slideOutDuration + slideBackDuration + restDuration;

        while (true)
        {
            if (block == null || !block.gameObject.activeInHierarchy || block.isBeingDestroyed || block.isMoving)
            {
                Hide();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                if (block == null || !block.gameObject.activeInHierarchy || block.isBeingDestroyed || block.isMoving)
                {
                    Hide();
                    yield break;
                }

                elapsed += Time.deltaTime;
                float currentOffset = 0f;

                if (elapsed < slideOutDuration)
                {
                    float t = elapsed / slideOutDuration;
                    float ease = 1f - Mathf.Pow(1f - t, 2.5f);
                    currentOffset = ease * slideDistance * dir;
                }
                else if (elapsed < slideOutDuration + slideBackDuration)
                {
                    float t = (elapsed - slideOutDuration) / slideBackDuration;
                    float ease = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
                    currentOffset = Mathf.Lerp(slideDistance * dir, 0f, ease);
                }
                else
                {
                    currentOffset = 0f;
                }

                // Hafif nefes alma efekti (Scale pulse)
                float pulse = 1f + Mathf.Sin(elapsed * Mathf.PI * 2f) * 0.04f;
                transform.localScale = new Vector3((movesRight ? BaseScale : -BaseScale) * pulse, BaseScale * pulse, 1f);

                block.transform.position = new Vector3(originPos.x + currentOffset, originPos.y, originPos.z);
                yield return null;
            }
        }
    }

    /// <summary>
    /// Şık, yarı saydam koyu füme cam zemin + altın çerçeve ve parlak altın ok
    /// </summary>
    private Sprite GenerateFloatingGlassBadgeSprite()
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float outerRadius = size * 0.44f;
        float rimRadius = size * 0.39f;

        Color glassBackground = new Color(0.05f, 0.07f, 0.11f, 0.72f); // Koyu füme cam zemin
        Color goldRim = new Color(1.0f, 0.82f, 0.28f, 0.85f);          // İnce altın parlak çerçeve
        Color goldArrow = new Color(1.0f, 0.88f, 0.22f, 1.0f);         // Parlak canlı altın ok
        Color arrowCore = new Color(1.0f, 0.98f, 0.85f, 1.0f);         // Okun iç ışıltısı
        Color transparent = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > outerRadius)
                {
                    texture.SetPixel(x, y, transparent);
                    continue;
                }

                // Dış kenar antialiasing yumuşatma
                float edgeAlpha = Mathf.Clamp01((outerRadius - dist) / 1.2f);

                // 1. Ok Şekli (Gövde + Ok Ucu)
                float nx = dx / (size * 0.42f);
                float ny = dy / (size * 0.42f);

                bool insideShaft = (nx >= -0.46f && nx <= 0.06f && Mathf.Abs(ny) <= 0.15f);
                bool insideHead = (nx >= -0.06f && nx <= 0.50f && Mathf.Abs(ny) <= (0.50f - nx) * 0.86f);

                if (insideShaft || insideHead)
                {
                    // Okun merkezinde hafif beyaz ışıltı, kenarlarında altın renk
                    float centerGlow = 1f - Mathf.Clamp01(Mathf.Abs(ny) / 0.14f);
                    Color pixelArrow = Color.Lerp(goldArrow, arrowCore, centerGlow * 0.5f);
                    texture.SetPixel(x, y, pixelArrow);
                }
                // 2. Altın Çerçeve Çemberi
                else if (dist > rimRadius)
                {
                    Color rimCol = goldRim;
                    rimCol.a *= edgeAlpha;
                    texture.SetPixel(x, y, rimCol);
                }
                // 3. Füme Cam Zemin
                else
                {
                    // Üstten hafif ışık vurması (Glass reflection)
                    float vFactor = (y - center) / outerRadius;
                    Color curGlass = Color.Lerp(glassBackground, glassBackground * 1.35f, Mathf.Clamp01(vFactor * 0.5f + 0.3f));
                    texture.SetPixel(x, y, curGlass);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }

    private void OnDestroy()
    {
        if (currentTargetBlock != null)
        {
            ResetBlockPosition(currentTargetBlock);
        }

        if (generatedGlassBadgeSprite != null && generatedGlassBadgeSprite.texture != null)
        {
            Destroy(generatedGlassBadgeSprite.texture);
            Destroy(generatedGlassBadgeSprite);
        }
    }
}
