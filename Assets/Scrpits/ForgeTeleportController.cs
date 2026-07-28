using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForgeTeleportController : MonoBehaviour
{
    [Header("Departure")]
    [SerializeField, Range(0.05f, 0.3f)] private float departureDuration = 0.14f;
    [SerializeField, Range(0.5f, 1f)] private float departureScale = 0.82f;
    [SerializeField, Range(0f, 0.25f)] private float departureCenterPull = 0.08f;

    [Header("Forge Charge")]
    [SerializeField, Range(0.05f, 0.3f)] private float forgeChargeDuration = 0.14f;
    [SerializeField, Range(0f, 1f)] private float forgeChargeIntensity = 0.42f;

    [Header("Arrival")]
    [SerializeField, Range(0.08f, 0.45f)] private float arrivalDuration = 0.34f;
    [SerializeField, Range(0.65f, 1f)] private float arrivalStartScale = 0.9f;
    [SerializeField, Range(1f, 1.2f)] private float arrivalOvershootScale = 1.05f;
    [SerializeField, Range(0f, 1f)] private float arrivalGlowIntensity = 0.45f;
    [SerializeField, Range(0, 6)] private int arrivalParticleCount = 3;

    private static readonly Color TeleportTint = new Color(0.2f, 1f, 0.95f, 1f);
    private static Sprite pixelSprite;

    private SpriteRenderer forgeEnergyRenderer;
    private readonly List<GameObject> activeFxObjects = new List<GameObject>();
    private readonly List<PreviewState> activePreviewStates = new List<PreviewState>();
    private readonly List<RendererState> activeArrivalStates = new List<RendererState>();

    public float EstimatedDuration => departureDuration + arrivalDuration + 0.1f;

    private void Awake()
    {
        ResolveForgeEnergyRenderer();
    }

    private void OnDisable()
    {
        RestorePreviewStates();
        RestoreArrivalStates();
        ClearActiveFx();
    }

    public IEnumerator PlayDepartureRoutine(IReadOnlyList<GameObject> previewVisuals)
    {
        if (previewVisuals == null || previewVisuals.Count == 0)
            yield break;

        ResolveForgeEnergyRenderer();
        StartCoroutine(PlayForgeChargeOverlay());

        RestorePreviewStates();
        Vector3 center = forgeEnergyRenderer != null ? forgeEnergyRenderer.transform.position : transform.position;

        for (int i = 0; i < previewVisuals.Count; i++)
        {
            if (previewVisuals[i] != null)
                activePreviewStates.Add(new PreviewState(previewVisuals[i]));
        }

        float elapsed = 0f;
        while (elapsed < departureDuration)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, departureDuration));
            float eased = Smooth(t);
            float alpha = 1f - eased;
            float flash = Mathf.Sin(eased * Mathf.PI);

            for (int i = 0; i < activePreviewStates.Count; i++)
            {
                activePreviewStates[i].ApplyDeparture(center, departureScale, departureCenterPull, eased, alpha, flash);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < activePreviewStates.Count; i++)
        {
            activePreviewStates[i].ApplyDeparture(center, departureScale, departureCenterPull, 1f, 0f, 0f);
        }

    }

    public void PrepareArrival(IReadOnlyList<Block> blocks)
    {
        RestoreArrivalStates();
        ClearActiveFx();

        if (blocks == null)
            return;

        for (int i = 0; i < blocks.Count; i++)
        {
            Block block = blocks[i];
            if (block == null)
                continue;

            activeArrivalStates.Add(new RendererState(block.gameObject));
            SetRenderersAlpha(block.gameObject, 0f);
            block.transform.localScale = Vector3.one * arrivalStartScale;
            SpawnArrivalFx(block);
        }
    }

    public void ClearCompletedDepartureState()
    {
        activePreviewStates.Clear();
    }

    public IEnumerator PlayArrivalRoutine(IReadOnlyList<Block> blocks)
    {
        if (blocks == null || blocks.Count == 0)
            yield break;

        float elapsed = 0f;
        while (elapsed < arrivalDuration)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, arrivalDuration));
            float alpha = Smooth(t);
            float scale = EvaluateArrivalScale(t);

            for (int i = 0; i < blocks.Count; i++)
            {
                Block block = blocks[i];
                if (block == null)
                    continue;

                block.transform.localScale = Vector3.one * scale;
                SetRenderersAlpha(block.gameObject, alpha);
            }

            AnimateFx(t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            Block block = blocks[i];
            if (block == null)
                continue;

            block.transform.localScale = Vector3.one;
            SetRenderersAlpha(block.gameObject, 1f);
        }

        RestoreArrivalStates(clearOnly: true);
        ClearActiveFx();
    }

    private IEnumerator PlayForgeChargeOverlay()
    {
        if (forgeEnergyRenderer == null || forgeEnergyRenderer.sprite == null)
            yield break;

        GameObject overlay = new GameObject("ForgeTeleportChargeOverlay");
        overlay.transform.SetParent(forgeEnergyRenderer.transform, false);
        overlay.transform.localPosition = Vector3.zero;
        overlay.transform.localRotation = Quaternion.identity;
        overlay.transform.localScale = Vector3.one;

        SpriteRenderer overlayRenderer = overlay.AddComponent<SpriteRenderer>();
        overlayRenderer.sprite = forgeEnergyRenderer.sprite;
        overlayRenderer.drawMode = forgeEnergyRenderer.drawMode;
        overlayRenderer.size = forgeEnergyRenderer.size;
        overlayRenderer.sharedMaterial = forgeEnergyRenderer.sharedMaterial;
        overlayRenderer.sortingLayerID = forgeEnergyRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = forgeEnergyRenderer.sortingOrder;
        overlayRenderer.color = new Color(TeleportTint.r, TeleportTint.g, TeleportTint.b, 0f);

        float elapsed = 0f;
        while (elapsed < forgeChargeDuration)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, forgeChargeDuration));
            float pulse = Mathf.Sin(t * Mathf.PI);
            overlayRenderer.color = new Color(
                TeleportTint.r,
                TeleportTint.g,
                TeleportTint.b,
                pulse * forgeChargeIntensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(overlay);
    }

    private void SpawnArrivalFx(Block block)
    {
        SpriteRenderer blockRenderer = block.GetComponent<SpriteRenderer>();
        int sortingLayerId = blockRenderer != null ? blockRenderer.sortingLayerID : 0;
        int sortingOrder = blockRenderer != null ? blockRenderer.sortingOrder - 1 : 0;
        float width = Mathf.Max(1f, block.width);
        Color arrivalColor = ResolveArrivalColor(block, blockRenderer);

        GameObject root = new GameObject("ForgeTeleportArrivalFx");
        root.transform.position = new Vector3(
            block.x + (block.width - 1) * 0.5f,
            block.y,
            block.transform.position.z);
        activeFxObjects.Add(root);

        CreateFxRenderer(root.transform, "ArrivalGlow", sortingLayerId, sortingOrder, arrivalColor, new Vector3(width * 0.65f, 0.18f, 1f), Vector3.zero);
        CreateFxRenderer(root.transform, "ArrivalStreak", sortingLayerId, sortingOrder, arrivalColor, new Vector3(0.08f, 0.85f, 1f), Vector3.up * 0.22f);

        for (int i = 0; i < arrivalParticleCount; i++)
        {
            float normalized = arrivalParticleCount <= 1 ? 0.5f : i / (float)(arrivalParticleCount - 1);
            float x = Mathf.Lerp(-width * 0.35f, width * 0.35f, normalized);
            float y = Random.Range(-0.12f, 0.18f);
            Transform particle = CreateFxRenderer(
                root.transform,
                "ArrivalParticle",
                sortingLayerId,
                sortingOrder,
                arrivalColor,
                Vector3.one * Random.Range(0.045f, 0.075f),
                new Vector3(x, y, 0f));
            particle.Rotate(0f, 0f, Random.Range(0f, 45f));
        }
    }

    private static Color ResolveArrivalColor(Block block, SpriteRenderer blockRenderer)
    {
        if (block != null)
        {
            if (block.blockType == BlockType.Rock || block.isRock)
                return new Color(0.92f, 0.88f, 0.78f, 1f);

            if (block.blockType == BlockType.Ice || block.isFrozen)
                return new Color(0.72f, 0.94f, 1f, 1f);

            if (block.blockType == BlockType.Chained || block.isChained)
                return new Color(0.86f, 0.9f, 0.92f, 1f);
        }

        Color baseColor = blockRenderer != null ? blockRenderer.color : Color.white;

        if (!IsReliableColor(baseColor) && block != null && IsReliableColor(block.blockColor))
            baseColor = block.blockColor;

        return Color.Lerp(baseColor, Color.white, 0.3f);
    }

    private static bool IsReliableColor(Color color)
    {
        return color.a > 0.01f &&
            (!Mathf.Approximately(color.r, 1f) ||
             !Mathf.Approximately(color.g, 1f) ||
             !Mathf.Approximately(color.b, 1f));
    }

    private Transform CreateFxRenderer(Transform parent, string name, int sortingLayerId, int sortingOrder, Color arrivalColor, Vector3 scale, Vector3 localPosition)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = scale;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPixelSprite();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        renderer.color = new Color(arrivalColor.r, arrivalColor.g, arrivalColor.b, 0f);
        return obj.transform;
    }

    private void AnimateFx(float t)
    {
        float glowAlpha = (1f - Smooth(t)) * arrivalGlowIntensity;

        for (int i = activeFxObjects.Count - 1; i >= 0; i--)
        {
            GameObject fx = activeFxObjects[i];
            if (fx == null)
            {
                activeFxObjects.RemoveAt(i);
                continue;
            }

            SpriteRenderer[] renderers = fx.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Color color = renderers[r].color;
                float alphaMultiplier = renderers[r].gameObject.name == "ArrivalParticle" ? 0.55f : 1f;
                color.a = glowAlpha * alphaMultiplier;
                renderers[r].color = color;
            }

            fx.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.08f, Smooth(t));
        }
    }

    private void ResolveForgeEnergyRenderer()
    {
        if (forgeEnergyRenderer != null)
            return;

        Transform energy = transform.Find("ForgeEnergy");
        if (energy != null)
            forgeEnergyRenderer = energy.GetComponent<SpriteRenderer>();
    }

    private void RestoreArrivalStates(bool clearOnly = false)
    {
        if (!clearOnly)
        {
            for (int i = 0; i < activeArrivalStates.Count; i++)
                activeArrivalStates[i].Restore();
        }

        activeArrivalStates.Clear();
    }

    private void RestorePreviewStates()
    {
        for (int i = 0; i < activePreviewStates.Count; i++)
            activePreviewStates[i].Restore();

        activePreviewStates.Clear();
    }

    private void ClearActiveFx()
    {
        for (int i = activeFxObjects.Count - 1; i >= 0; i--)
        {
            if (activeFxObjects[i] != null)
                Destroy(activeFxObjects[i]);
        }

        activeFxObjects.Clear();
    }

    private static void SetRenderersAlpha(GameObject target, float alpha)
    {
        if (target == null)
            return;

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = renderers[i].color;
            color.a = alpha;
            renderers[i].color = color;
        }
    }

    private float EvaluateArrivalScale(float t)
    {
        if (t < 0.65f)
        {
            float local = Smooth(t / 0.65f);
            return Mathf.Lerp(arrivalStartScale, arrivalOvershootScale, local);
        }

        float settle = Smooth((t - 0.65f) / 0.35f);
        return Mathf.Lerp(arrivalOvershootScale, 1f, settle);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
            return pixelSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        pixelSprite.hideFlags = HideFlags.HideAndDontSave;
        return pixelSprite;
    }

    private readonly struct PreviewState
    {
        private readonly GameObject root;
        private readonly Vector3 startPosition;
        private readonly Vector3 startScale;
        private readonly RendererState rendererState;

        public PreviewState(GameObject root)
        {
            this.root = root;
            startPosition = root.transform.position;
            startScale = root.transform.localScale;
            rendererState = new RendererState(root);
        }

        public void ApplyDeparture(Vector3 center, float scale, float centerPull, float t, float alpha, float flash)
        {
            if (root == null)
                return;

            root.transform.position = Vector3.Lerp(startPosition, center, centerPull * t);
            root.transform.localScale = Vector3.Lerp(startScale, startScale * scale, t);
            rendererState.ApplyTeleportTint(alpha, flash);
        }

        public void Restore()
        {
            if (root == null)
                return;

            root.transform.position = startPosition;
            root.transform.localScale = startScale;
            rendererState.Restore();
        }
    }

    private readonly struct RendererState
    {
        private readonly GameObject root;
        private readonly SpriteRenderer[] renderers;
        private readonly Color[] colors;
        private readonly Vector3 scale;

        public RendererState(GameObject root)
        {
            this.root = root;
            scale = root != null ? root.transform.localScale : Vector3.one;
            renderers = root != null ? root.GetComponentsInChildren<SpriteRenderer>(false) : new SpriteRenderer[0];
            colors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
                colors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        }

        public void ApplyTeleportTint(float alpha, float flash)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = Color.Lerp(colors[i], TeleportTint, flash * 0.55f);
                color.a = colors[i].a * alpha;
                renderers[i].color = color;
            }
        }

        public void Restore()
        {
            if (root != null)
                root.transform.localScale = scale;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = colors[i];
            }
        }
    }
}
