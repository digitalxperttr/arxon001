using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Multi-branched procedural lightning arc effect for the Fire Block.
/// Spawns a main glowing lightning bolt along with side branches and target shock sparks.
/// Fades in with intense white-hot core, flickers at high frequency, and cleans itself up.
/// </summary>
public class FireArcFX : MonoBehaviour
{
    // ── Colour constants (White-hot electric core with vibrant gold/amber aura) ──
    private static readonly Color ArcCoreWhite   = new Color(1.00f, 1.00f, 1.00f, 1f); // Blinding white core
    private static readonly Color ArcGoldGlow    = new Color(1.00f, 0.88f, 0.25f, 1f); // Electric gold aura
    private static readonly Color ArcOrangeAura  = new Color(1.00f, 0.55f, 0.05f, 1f); // Fiery orange rim

    // ── Default Tunings ───────────────────────────────────────────────────────
    public const float DefaultDuration    = 0.24f;
    public const float DefaultStartWidth  = 0.16f;
    public const float DefaultEndWidth    = 0.08f;
    private const float BranchStartWidth  = 0.08f;
    private const float BranchEndWidth    = 0.02f;
    private const float ShockSparkWidth   = 0.06f;

    // ── Runtime components ────────────────────────────────────────────────────
    private LineRenderer       mainLine;
    private List<LineRenderer> branchLines = new List<LineRenderer>();
    private List<LineRenderer> shockSparks  = new List<LineRenderer>();

    private Vector3 fromPos;
    private Vector3 toPos;
    private Vector2 targetBlockSize;
    private int     segmentCount;
    private float   displacement;
    private Material resolvedMaterial;

    // ─────────────────────────────────────────────────────────────────────────
    // Public factory
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a multi-branching electric lightning arc from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static FireArcFX Spawn(
        Vector3  from,
        Vector3  to,
        Vector2  targetSize,
        float    duration     = DefaultDuration,
        int      segments     = 14,
        float    displacement = 0.22f,
        float    startWidth   = DefaultStartWidth,
        float    endWidth     = DefaultEndWidth,
        Material lineMaterial = null)
    {
        if (from == to) return null;

        GameObject go = new GameObject("FireArcFX");
        FireArcFX fx = go.AddComponent<FireArcFX>();
        fx.Initialize(from, to, targetSize, duration, segments, displacement, startWidth, endWidth, lineMaterial);
        return fx;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialisation
    // ─────────────────────────────────────────────────────────────────────────

    private void Initialize(
        Vector3  from,
        Vector3  to,
        Vector2  targetSize,
        float    duration,
        int      segments,
        float    disp,
        float    startWidth,
        float    endWidth,
        Material lineMaterial)
    {
        fromPos          = from;
        toPos            = to;
        targetBlockSize  = targetSize;
        segmentCount     = Mathf.Max(3, segments);
        displacement     = disp;
        resolvedMaterial = ResolveMaterial(lineMaterial);

        // 1. Setup Main Bolt LineRenderer
        mainLine = CreateLine("MainBolt", startWidth, endWidth, 65);

        // 2. Setup 2 Branch Bolts (forks splitting off the main bolt)
        for (int i = 0; i < 2; i++)
        {
            LineRenderer branch = CreateLine($"Branch_{i}", BranchStartWidth, BranchEndWidth, 64);
            branchLines.Add(branch);
        }

        // 3. Setup 3 Target Shock Sparks (crawling around target block boundary)
        for (int i = 0; i < 3; i++)
        {
            LineRenderer spark = CreateLine($"ShockSpark_{i}", ShockSparkWidth, 0.01f, 66);
            shockSparks.Add(spark);
        }

        // Initial draw
        RebuildAllGeometry();
        ApplyGlobalAlpha(1f);

        // Run animation
        StartCoroutine(PlayArcAnimation(duration));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Line Creation Helper
    // ─────────────────────────────────────────────────────────────────────────

    private LineRenderer CreateLine(string lineName, float startW, float endW, int sortOrder)
    {
        GameObject child = new GameObject(lineName);
        child.transform.SetParent(transform, false);

        LineRenderer lr = child.AddComponent<LineRenderer>();
        lr.useWorldSpace     = true;
        lr.startWidth        = startW;
        lr.endWidth          = endW;
        lr.numCapVertices    = 4;
        lr.numCornerVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.sharedMaterial    = resolvedMaterial;
        lr.sortingLayerName  = "Default";
        lr.sortingOrder      = sortOrder;

        return lr;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Geometry Generation
    // ─────────────────────────────────────────────────────────────────────────

    private void RebuildAllGeometry()
    {
        // 1. Main Bolt
        Vector3[] mainPts = BuildZigzagPoints(fromPos, toPos, segmentCount, displacement);
        if (mainLine != null)
        {
            mainLine.positionCount = mainPts.Length;
            mainLine.SetPositions(mainPts);
        }

        // 2. Branch Bolts
        if (mainPts.Length >= 4)
        {
            for (int i = 0; i < branchLines.Count; i++)
            {
                LineRenderer branch = branchLines[i];
                if (branch == null) continue;

                // Pick a joint on the main line (e.g. at 1/3 or 2/3)
                int startIdx = Mathf.Clamp((i + 1) * (mainPts.Length / (branchLines.Count + 1)), 1, mainPts.Length - 2);
                Vector3 branchStart = mainPts[startIdx];

                Vector3 mainDir = (toPos - fromPos).normalized;
                Vector3 perp = new Vector3(-mainDir.y, mainDir.x, 0f);
                float sideSign = (i % 2 == 0) ? 1f : -1f;

                Vector3 branchEnd = branchStart + (mainDir * 0.4f + perp * (0.35f * sideSign)) + (Vector3)(Random.insideUnitCircle * 0.15f);

                Vector3[] branchPts = BuildZigzagPoints(branchStart, branchEnd, 4, displacement * 0.5f);
                branch.positionCount = branchPts.Length;
                branch.SetPositions(branchPts);
            }
        }

        // 3. Target Shock Sparks (crawling around the target block perimeter)
        float halfW = Mathf.Max(0.4f, targetBlockSize.x * 0.5f);
        float halfH = Mathf.Max(0.4f, targetBlockSize.y * 0.5f);

        for (int i = 0; i < shockSparks.Count; i++)
        {
            LineRenderer spark = shockSparks[i];
            if (spark == null) continue;

            // Pick two points around the target block perimeter
            float angle1 = Random.Range(0f, Mathf.PI * 2f);
            float angle2 = angle1 + Random.Range(0.8f, 1.8f);

            Vector3 p1 = toPos + new Vector3(Mathf.Cos(angle1) * halfW * Random.Range(0.7f, 1.1f), Mathf.Sin(angle1) * halfH * Random.Range(0.7f, 1.1f), 0f);
            Vector3 p2 = toPos + new Vector3(Mathf.Cos(angle2) * halfW * Random.Range(0.8f, 1.2f), Mathf.Sin(angle2) * halfH * Random.Range(0.8f, 1.2f), 0f);

            Vector3[] sparkPts = BuildZigzagPoints(p1, p2, 4, displacement * 0.4f);
            spark.positionCount = sparkPts.Length;
            spark.SetPositions(sparkPts);
        }
    }

    private static Vector3[] BuildZigzagPoints(Vector3 from, Vector3 to, int segments, float disp)
    {
        Vector3[] pts = new Vector3[segments + 1];
        pts[0]        = from;
        pts[segments] = to;

        Vector3 dir  = to - from;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / segments;
            float envelope = Mathf.Sin(t * Mathf.PI);
            float noise = Random.Range(-1f, 1f);
            pts[i] = Vector3.Lerp(from, to, t) + perp * (noise * disp * envelope);
        }

        return pts;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Animation Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator PlayArcAnimation(float totalDuration)
    {
        float flashInDuration = totalDuration * 0.15f; // Fast strike
        float holdDuration    = totalDuration * 0.50f; // Sustained shock
        float fadeOutDuration = totalDuration * 0.35f; // Dissipation

        float elapsed = 0f;

        // Strike in
        while (elapsed < flashInDuration)
        {
            RebuildAllGeometry();
            ApplyGlobalAlpha(Mathf.Clamp01(elapsed / flashInDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Sustained electric flicker hold
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            RebuildAllGeometry();
            float flickerAlpha = Random.Range(0.80f, 1.0f);
            ApplyGlobalAlpha(flickerAlpha);
            holdElapsed += Time.deltaTime;
            yield return null;
        }

        // Fade out with erratic flicker
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            RebuildAllGeometry();
            float t = 1f - (elapsed / fadeOutDuration);
            float flicker = Random.Range(0.6f, 1.0f);
            ApplyGlobalAlpha(t * flicker);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyGlobalAlpha(0f);
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Color & Alpha Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyGlobalAlpha(float alpha)
    {
        if (mainLine != null)
            SetLineGradient(mainLine, alpha, 1.0f);

        for (int i = 0; i < branchLines.Count; i++)
        {
            if (branchLines[i] != null)
                SetLineGradient(branchLines[i], alpha * 0.85f, 0.7f);
        }

        for (int i = 0; i < shockSparks.Count; i++)
        {
            if (shockSparks[i] != null)
                SetLineGradient(shockSparks[i], alpha * 0.90f, 0.8f);
        }
    }

    private static void SetLineGradient(LineRenderer lr, float alpha, float coreIntensity)
    {
        Color startCol = Color.Lerp(ArcOrangeAura, ArcGoldGlow, coreIntensity);
        Color midCol   = Color.Lerp(ArcGoldGlow, ArcCoreWhite, coreIntensity);
        Color endCol   = ArcGoldGlow;

        lr.colorGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(startCol, 0.0f),
                new GradientColorKey(midCol,   0.4f),
                new GradientColorKey(midCol,   0.7f),
                new GradientColorKey(endCol,   1.0f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(alpha,                0.0f),
                new GradientAlphaKey(alpha * 0.95f,        0.7f),
                new GradientAlphaKey(alpha * 0.30f,        1.0f)
            }
        };
    }

    private static Material ResolveMaterial(Material provided)
    {
        if (provided != null)
            return provided;

        Material loaded = Resources.Load<Material>("M_FireArc");
        if (loaded != null)
            return loaded;

        // Try Additive or Unlit Particles first for bright blooming glow
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Mobile/Particles/Additive")
                     ?? Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material mat = new Material(shader) { name = "FireArc_RuntimeMaterial" };
            return mat;
        }

        return null;
    }
}
