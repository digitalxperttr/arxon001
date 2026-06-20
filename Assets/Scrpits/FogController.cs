using System.Collections;
using UnityEngine;

public class FogController : MonoBehaviour
{
    private const string OverlayName = "FogOverlay";
    private const string FogShaderName = "ARXON/FogRevealSprite";

    private SpriteRenderer overlayRenderer;
    private Coroutine transitionRoutine;
    private Material overlayMaterialInstance;
    private float currentRevealProgress;
    private float targetRevealProgress;
    private int boardWidth;
    private int boardHeight;
    private float transitionDuration = 0.2f;
    private float revealSoftness = 0.15f;
    private float baseAlpha = 1f;
    private float distortionStrength = 0.035f;
    private float distortionSpeed = 0.55f;

    public void Configure(
        Transform parent,
        GameObject fogOverlayPrefab,
        int width,
        int height,
        FogDensity density,
        float coveragePercent,
        Sprite fogLightSprite,
        Sprite fogDenseSprite,
        int lightAlpha,
        int denseAlpha,
        float animationDuration,
        float initialRevealProgress,
        float fogRevealSoftness,
        float fogDistortionStrength,
        float fogDistortionSpeed)
    {
        boardWidth = width;
        boardHeight = height;
        transitionDuration = animationDuration;
        revealSoftness = fogRevealSoftness;
        distortionStrength = fogDistortionStrength;
        distortionSpeed = fogDistortionSpeed;

        EnsureOverlay(parent, fogOverlayPrefab);

        if (overlayRenderer == null)
            return;

        if (density == FogDensity.None || coveragePercent <= 0f)
        {
            HideOverlay();
            return;
        }

        overlayRenderer.sprite = density == FogDensity.Dense ? fogDenseSprite : fogLightSprite;
        overlayRenderer.drawMode = SpriteDrawMode.Sliced;
        overlayRenderer.sortingOrder = 40;
        overlayRenderer.color = new Color(
            1f,
            1f,
            1f,
            Mathf.Clamp01((density == FogDensity.Dense ? denseAlpha : lightAlpha) / 255f));
        baseAlpha = overlayRenderer.color.a;

        overlayRenderer.gameObject.SetActive(true);
        EnsureOverlayMaterial();
        ApplyOverlayTransform();

        currentRevealProgress = Mathf.Clamp01(initialRevealProgress);
        SetRevealProgressInstant(currentRevealProgress);
        SetCoverageAnimated(coveragePercent);
    }

    public void RevealRows(int clearedRowCount, float revealPerRow)
    {
        if (overlayRenderer == null || !overlayRenderer.gameObject.activeSelf)
            return;

        if (clearedRowCount <= 0 || revealPerRow <= 0f)
            return;

        float nextCoverage = Mathf.Max(0f, (1f - targetRevealProgress) - (clearedRowCount * revealPerRow));
        SetCoverageAnimated(nextCoverage);
    }

    private void EnsureOverlay(Transform parent, GameObject fogOverlayPrefab)
    {
        if (overlayRenderer != null)
            return;

        GameObject overlayObject = null;

        if (fogOverlayPrefab != null)
        {
            overlayObject = Instantiate(fogOverlayPrefab, parent);
            overlayObject.name = OverlayName;
        }
        else
        {
            Transform existing = parent != null ? parent.Find(OverlayName) : null;
            if (existing != null)
            {
                overlayObject = existing.gameObject;
            }
            else
            {
                overlayObject = new GameObject(OverlayName);
                if (parent != null)
                    overlayObject.transform.SetParent(parent);
            }
        }

        overlayRenderer = overlayObject.GetComponent<SpriteRenderer>();
        if (overlayRenderer == null)
            overlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
    }

    private void SetCoverageAnimated(float newCoveragePercent)
    {
        float clampedCoverage = Mathf.Clamp01(newCoveragePercent);
        targetRevealProgress = 1f - clampedCoverage;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(AnimateCoverageRoutine());
    }

    private IEnumerator AnimateCoverageRoutine()
    {
        float startReveal = currentRevealProgress;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            float t = transitionDuration > 0f ? elapsed / transitionDuration : 1f;
            currentRevealProgress = Mathf.Lerp(startReveal, targetRevealProgress, t);
            ApplyRevealProgress(currentRevealProgress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        currentRevealProgress = targetRevealProgress;
        ApplyRevealProgress(currentRevealProgress);

        if (currentRevealProgress >= 1f)
            HideOverlay();

        transitionRoutine = null;
    }

    private void ApplyOverlayTransform()
    {
        if (overlayRenderer == null)
            return;

        overlayRenderer.gameObject.SetActive(true);
        overlayRenderer.size = new Vector2(boardWidth, boardHeight);
        float centerX = (boardWidth - 1) * 0.5f;
        float centerY = (boardHeight - 1) * 0.5f;
        overlayRenderer.transform.position = new Vector3(centerX, centerY, 0f);
        overlayRenderer.transform.localScale = Vector3.one;
    }

    private void EnsureOverlayMaterial()
    {
        if (overlayRenderer == null)
            return;

        if (overlayMaterialInstance != null)
            return;

        Shader fogShader = Shader.Find(FogShaderName);
        if (fogShader == null)
            return;

        overlayMaterialInstance = new Material(fogShader);
        overlayRenderer.material = overlayMaterialInstance;
    }

    private void SetRevealProgressInstant(float revealProgress)
    {
        ApplyRevealProgress(revealProgress);
    }

    private void ApplyRevealProgress(float revealProgress)
    {
        EnsureOverlayMaterial();

        if (overlayMaterialInstance == null)
            return;

        overlayMaterialInstance.SetFloat("_RevealProgress", Mathf.Clamp01(revealProgress));
        overlayMaterialInstance.SetFloat("_RevealSoftness", revealSoftness);
        overlayMaterialInstance.SetFloat("_FogDistortionStrength", distortionStrength);
        overlayMaterialInstance.SetFloat("_FogDistortionSpeed", distortionSpeed);
    }

    private void HideOverlay()
    {
        currentRevealProgress = 1f;
        targetRevealProgress = 1f;

        if (overlayRenderer != null)
            overlayRenderer.gameObject.SetActive(false);
    }
}
