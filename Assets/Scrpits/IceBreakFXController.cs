using UnityEngine;

public class IceBreakFXController : MonoBehaviour
{
    [Header("8-Layered AAA Glacial Shatter FX")]
    [SerializeField] private ParticleSystem centerFlashPS;
    [SerializeField] private ParticleSystem shockwavePS;
    [SerializeField] private ParticleSystem shardsPS;
    [SerializeField] private ParticleSystem needlesPS;
    [SerializeField] private ParticleSystem powderCloudPS;
    [SerializeField] private ParticleSystem snowflakesPS;
    [SerializeField] private ParticleSystem vaporPS;
    [SerializeField] private ParticleSystem sparkleBurstPS;

    public void Initialize(int blockWidth, int sortingLayerId = 0, int baseSortingOrder = 100)
    {
        float w = Mathf.Max(1f, blockWidth);

        // ── 0. Merkez Flaş (Overexposed Instant Flash) ──────────────────
        if (centerFlashPS != null)
        {
            var rend = centerFlashPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 15; // 115
            }
            var shape = centerFlashPS.shape;
            shape.scale = new Vector3(w * 0.90f, 1.0f, 1f);
            centerFlashPS.Play();
        }

        // ── 1. Şok Dalgası Halkaları (Glacial Shockwave) ─────────────────
        if (shockwavePS != null)
        {
            var rend = shockwavePS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 3; // 103
            }
            var shape = shockwavePS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.35f, w * 0.25f);

            var emission = shockwavePS.emission;
            short waveCount = (short)Mathf.Max(2, blockWidth + 1);
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, waveCount) });
            shockwavePS.Play();
        }

        // ── 2. Fasetli Geometrik Kristal Parçaları (Faceted Shards) ────
        if (shardsPS != null)
        {
            var rend = shardsPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 8; // 108
            }
            var shape = shardsPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.5f, w * 0.35f);

            var emission = shardsPS.emission;
            short shardCount = (short)(14 + (blockWidth - 1) * 8); // 1x1: 14, 1x4: 38
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, shardCount) });
            shardsPS.Play();
        }

        // ── 3. Keskin Buzul İğneleri & Şarapneller (Glacial Needles) ────
        if (needlesPS != null)
        {
            var rend = needlesPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 10; // 110
            }
            var shape = needlesPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.4f, w * 0.30f);

            var emission = needlesPS.emission;
            short needleCount = (short)(8 + (blockWidth - 1) * 4); // 1x1: 8, 1x4: 20
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, needleCount) });
            needlesPS.Play();
        }

        // ── 4. Buz Tozu Bulutu (Glacial Powder Cloud) ───────────────────
        if (powderCloudPS != null)
        {
            var rend = powderCloudPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 2; // 102
            }
            var shape = powderCloudPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.45f, w * 0.35f);

            var emission = powderCloudPS.emission;
            short powderCount = (short)(35 + (blockWidth - 1) * 16); // 1x1: 35, 1x4: 83
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, powderCount) });
            powderCloudPS.Play();
        }

        // ── 5. Kar Taneleri & Rün Saçılması (Floating Snowflakes) ───────
        if (snowflakesPS != null)
        {
            var rend = snowflakesPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 7; // 107
            }
            var shape = snowflakesPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.4f, w * 0.30f);

            var emission = snowflakesPS.emission;
            short snowCount = (short)(7 + (blockWidth - 1) * 3); // 1x1: 7, 1x4: 16
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.03f, snowCount) });
            snowflakesPS.Play();
        }

        // ── 6. Yükselen Yoğun Soğuk Sis / Buz Buharı (Rising Ice Fog) ──
        if (vaporPS != null)
        {
            var rend = vaporPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 1; // 101
            }
            var shape = vaporPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.45f, w * 0.35f);

            var emission = vaporPS.emission;
            short vaporCount = (short)(18 + (blockWidth - 1) * 10); // 1x1: 18, 1x4: 48
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.05f, vaporCount) });
            vaporPS.Play();
        }

        // ── 7. Elmas Pırıltıları (Diamond Sparkles) ─────────────────────
        if (sparkleBurstPS != null)
        {
            var rend = sparkleBurstPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 12; // 112
            }
            var shape = sparkleBurstPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.5f, w * 0.40f);

            var emission = sparkleBurstPS.emission;
            short sparkleCount = (short)(18 + (blockWidth - 1) * 10); // 1x1: 18, 1x4: 48
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, sparkleCount) });
            sparkleBurstPS.Play();
        }
    }
}
