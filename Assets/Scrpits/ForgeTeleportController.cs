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

    private static readonly Color TeleportTint = new Color(0.2f, 1f, 0.95f, 1f);

    private SpriteRenderer forgeEnergyRenderer;
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
