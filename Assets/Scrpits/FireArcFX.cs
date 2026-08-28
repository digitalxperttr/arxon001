using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Multi-branched procedural lightning arc effect for the Fire Block.
/// Spawns a main glowing lightning bolt along with side branches and target box outline shock sparks.
/// Stays active with continuous electric flow until dismissed (when target block explodes).
/// </summary>
public class FireArcFX : MonoBehaviour
{
    // ── Colour constants (White-hot electric core with vibrant gold/amber aura) ──
    private static readonly Color ArcCoreWhite   = new Color(1.00f, 1.00f, 1.00f, 1f); // Blinding white core
    private static readonly Color ArcGoldGlow    = new Color(1.00f, 0.88f, 0.25f, 1f); // Electric gold aura
    private static readonly Color ArcOrangeAura  = new Color(1.00f, 0.55f, 0.05f, 1f); // Fiery orange rim

    // ── Default Tunings ───────────────────────────────────────────────────────
    public const float DefaultStartWidth  = 0.16f;
    public const float DefaultEndWidth    = 0.08f;
    private const float BranchStartWidth  = 0.08f;
    private const float BranchEndWidth    = 0.02f;
    private const float ShockSparkWidth   = 0.07f;

    // ── Runtime components ────────────────────────────────────────────────────
    private LineRenderer       mainLine;
    private List<LineRenderer> branchLines = new List<LineRenderer>();
    private List<LineRenderer> shockSparks  = new List<LineRenderer>(); // 4 outline lines for rectangle edges

    private Vector3 fromPos;
    private Vector3 toPos;
    private Vector2 targetBlockSize;
    private int     segmentCount;
    private float   displacement;
    private Material resolvedMaterial;

    private bool isDismissed = false;
    private Coroutine activeLoopCoroutine;

    // ─────────────────────────────────────────────────────────────────────────
    // Public factory
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a sustained multi-branching electric lightning arc from <paramref name="from"/> to <paramref name="to"/>.
    /// Stays active until <see cref="Dismiss"/> is called.
    /// </summary>
    public static FireArcFX Spawn(
        Vector3  from,
        Vector3  to,
        Vector2  targetSize,
        float    duration     = 0.50f,
        int      segments     = 14,
        float    displacement = 0.20f,
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

        // 3. Setup 4 Box Outline Shock Sparks (Top, Bottom, Left, Right edges of block)
        for (int i = 0; i < 4; i++)
        {
            LineRenderer spark = CreateLine($"OutlineEdge_{i}", ShockSparkWidth, ShockSparkWidth * 0.7f, 66);
            shockSparks.Add(spark);
        }

        // Initial draw
        RebuildAllGeometry();
        ApplyGlobalAlpha(1f);

        // Start continuous live streaming loop
        activeLoopCoroutine = StartCoroutine(ContinuousStreamingRoutine());
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
    // Dismiss API (Called when the target block explodes)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dismisses and fades out the lightning arc and shock box rapidly.
    /// </summary>
    public void Dismiss(float fadeDuration = 0.08f)
    {
        if (isDismissed) return;
        isDismissed = true;

        if (activeLoopCoroutine != null)
            StopCoroutine(activeLoopCoroutine);

        StartCoroutine(FadeOutAndDestroyRoutine(fadeDuration));
    }

    private IEnumerator FadeOutAndDestroyRoutine(float fadeDuration)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            float alpha = 1f - (elapsed / fadeDuration);
            ApplyGlobalAlpha(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyGlobalAlpha(0f);
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Geometry Generation (Box Outline Form for Rectangular Blocks)
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

                int startIdx = Mathf.Clamp((i + 1) * (mainPts.Length / (branchLines.Count + 1)), 1, mainPts.Length - 2);
                Vector3 branchStart = mainPts[startIdx];

                Vector3 mainDir = (toPos - fromPos).normalized;
                Vector3 perp = new Vector3(-mainDir.y, mainDir.x, 0f);
                float sideSign = (i % 2 == 0) ? 1f : -1f;

                Vector3 branchEnd = branchStart + (mainDir * 0.35f + perp * (0.30f * sideSign)) + (Vector3)(Random.insideUnitCircle * 0.10f);

                Vector3[] branchPts = BuildZigzagPoints(branchStart, branchEnd, 4, displacement * 0.5f);
                branch.positionCount = branchPts.Length;
                branch.SetPositions(branchPts);
            }
        }

        // 3. Rectangular Box Outline Shock Sparks (Strictly following the 4 block edges)
        float halfW = Mathf.Max(0.48f, targetBlockSize.x * 0.5f);
        float halfH = Mathf.Max(0.48f, targetBlockSize.y * 0.5f);

        // Define the 4 corners of the block box
        Vector3 topLeft     = toPos + new Vector3(-halfW,  halfH, 0f);
        Vector3 topRight    = toPos + new Vector3( halfW,  halfH, 0f);
        Vector3 bottomRight = toPos + new Vector3( halfW, -halfH, 0f);
        Vector3 bottomLeft  = toPos + new Vector3(-halfW, -halfH, 0f);

        // Edge 0: Top edge (TopLeft -> TopRight)
        if (shockSparks.Count > 0 && shockSparks[0] != null)
        {
            int segs = Mathf.Max(3, Mathf.RoundToInt(targetBlockSize.x * 3f));
            Vector3[] pts = BuildZigzagPoints(topLeft, topRight, segs, 0.06f);
            shockSparks[0].positionCount = pts.Length;
            shockSparks[0].SetPositions(pts);
        }

        // Edge 1: Right edge (TopRight -> BottomRight)
        if (shockSparks.Count > 1 && shockSparks[1] != null)
        {
            Vector3[] pts = BuildZigzagPoints(topRight, bottomRight, 3, 0.06f);
            shockSparks[1].positionCount = pts.Length;
            shockSparks[1].SetPositions(pts);
        }

        // Edge 2: Bottom edge (BottomRight -> BottomLeft)
        if (shockSparks.Count > 2 && shockSparks[2] != null)
        {
            int segs = Mathf.Max(3, Mathf.RoundToInt(targetBlockSize.x * 3f));
            Vector3[] pts = BuildZigzagPoints(bottomRight, bottomLeft, segs, 0.06f);
            shockSparks[2].positionCount = pts.Length;
            shockSparks[2].SetPositions(pts);
        }

        // Edge 3: Left edge (BottomLeft -> TopLeft)
        if (shockSparks.Count > 3 && shockSparks[3] != null)
        {
            Vector3[] pts = BuildZigzagPoints(bottomLeft, topLeft, 3, 0.06f);
            shockSparks[3].positionCount = pts.Length;
            shockSparks[3].SetPositions(pts);
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
    // Continuous Electric Streaming Loop
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator ContinuousStreamingRoutine()
    {
        // Initial flash strike in (0.04s)
        float elapsed = 0f;
        while (elapsed < 0.04f)
        {
            RebuildAllGeometry();
            ApplyGlobalAlpha(Mathf.Clamp01(elapsed / 0.04f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Sustained electric flicker (remains alive until Dismiss() is called)
        while (!isDismissed)
        {
            RebuildAllGeometry();
            float flicker = Random.Range(0.85f, 1.0f);
            ApplyGlobalAlpha(flicker);
            yield return null;
        }
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
                SetLineGradient(shockSparks[i], alpha * 0.95f, 0.9f);
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
