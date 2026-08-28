using UnityEngine;

public class IceBreakFXController : MonoBehaviour
{
    [SerializeField] private ParticleSystem shardsPS;
    [SerializeField] private ParticleSystem shockwavePS;
    [SerializeField] private ParticleSystem sparkleBurstPS;
    [SerializeField] private ParticleSystem frostPuffPS;

    public void Initialize(int blockWidth, int sortingLayerId = 0, int baseSortingOrder = 100)
    {
        float w = Mathf.Max(1f, blockWidth);

        // ── 1. Keskin Buz Kıymıkları (Tüm genişliğe 360 derece savrulan) ──
        if (shardsPS != null)
        {
            var rend = shardsPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 5; // 105
            }

            var shape = shardsPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.5f, w * 0.35f);
            shape.radiusThickness = 1.0f;
            shape.arc = 360f;
            shape.randomDirectionAmount = 0.85f; // Yüksek kaotik fırlama açısı

            var emission = shardsPS.emission;
            short shardCount = (short)(18 + (blockWidth - 1) * 10); // 1x1: 18, 1x4: 48
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, shardCount) });
            shardsPS.Play();
        }

        // ── 2. Şok Dalgası Halkaları ────────────────────────────────────
        if (shockwavePS != null)
        {
            var rend = shockwavePS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 2; // 102
            }

            var shape = shockwavePS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.35f, w * 0.25f);

            var emission = shockwavePS.emission;
            short waveCount = (short)Mathf.Max(1, blockWidth); // 1x1: 1, 1x4: 4
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, waveCount) });
            shockwavePS.Play();
        }

        // ── 3. Kristal & Elmas Yıldız Tozları ───────────────────────────
        if (sparkleBurstPS != null)
        {
            var rend = sparkleBurstPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder + 8; // 108
            }

            var shape = sparkleBurstPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.5f, w * 0.4f);
            shape.randomDirectionAmount = 0.45f;

            var emission = sparkleBurstPS.emission;
            short sparkleCount = (short)(16 + (blockWidth - 1) * 12); // 1x1: 16, 1x4: 52
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, sparkleCount) });
            sparkleBurstPS.Play();
        }

        // ── 4. Soğuk Sis / Donma Buharı Pufu ────────────────────────────
        if (frostPuffPS != null)
        {
            var rend = frostPuffPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerID = sortingLayerId;
                rend.sortingOrder = baseSortingOrder; // 100
            }

            var shape = frostPuffPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.4f, w * 0.35f);
            shape.randomDirectionAmount = 0.5f;

            var emission = frostPuffPS.emission;
            short puffCount = (short)(10 + (blockWidth - 1) * 6); // 1x1: 10, 1x4: 28
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, puffCount) });
            frostPuffPS.Play();
        }
    }
}
