using UnityEditor;
using UnityEngine;

public static class CreateIceBreakFXPrefab
{
    private const string PrefabPath = "Assets/ART/VFX/IceBreakFX.prefab";
    private const string ShardMatPath = "Assets/Materials/M_IceShardParticle.mat";
    private const string RingMatPath = "Assets/Materials/M_FrostRingParticle.mat";
    private const string SparkleMatPath = "Assets/Materials/M_IceSparkleParticle.mat";
    private const string PuffMatPath = "Assets/Materials/patlamalar.mat";

    [MenuItem("ARXON/Tools/Create Ice Break FX Prefab")]
    public static void Generate()
    {
        Material shardMat = AssetDatabase.LoadAssetAtPath<Material>(ShardMatPath);
        Material ringMat = AssetDatabase.LoadAssetAtPath<Material>(RingMatPath);
        Material sparkleMat = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatPath);
        Material puffMat = AssetDatabase.LoadAssetAtPath<Material>(PuffMatPath);

        // Root GameObject
        GameObject root = new GameObject("IceBreakFX");

        // Main root ParticleSystem for orchestration and auto-destroy
        ParticleSystem rootPS = root.AddComponent<ParticleSystem>();
        var rootMain = rootPS.main;
        rootMain.duration = 1.4f;
        rootMain.loop = false;
        rootMain.playOnAwake = true;
        rootMain.stopAction = ParticleSystemStopAction.Destroy;
        rootMain.startLifetime = 1.3f;
        rootMain.startSpeed = 0f;
        rootMain.startSize = 0f;
        var rootEmission = rootPS.emission;
        rootEmission.enabled = false;
        var rootRend = root.GetComponent<ParticleSystemRenderer>();
        rootRend.enabled = false;

        IceBreakFXController controller = root.AddComponent<IceBreakFXController>();

        // ── 1. ICE SHARDS (Keskin Buz Kıymıkları) ───────────────────────────
        GameObject shardsObj = new GameObject("IceShards");
        shardsObj.transform.SetParent(root.transform, false);
        shardsObj.transform.localPosition = Vector3.zero;

        ParticleSystem shardsPS = shardsObj.AddComponent<ParticleSystem>();
        var shardsMain = shardsPS.main;
        shardsMain.duration = 1.0f;
        shardsMain.loop = false;
        shardsMain.playOnAwake = false;
        shardsMain.simulationSpace = ParticleSystemSimulationSpace.World;
        shardsMain.maxParticles = 60;
        shardsMain.startLifetime = new ParticleSystem.MinMaxCurve(0.40f, 0.75f); // Hızlı, tok darbe ömrü
        shardsMain.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);     // Şiddetli patlama fırlaması
        shardsMain.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.46f);    // İrili ufaklı fasetli parçalar
        shardsMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad); // Rastgele kırık açısı
        shardsMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 1f),
            new Color(0.85f, 0.95f, 1f, 0.95f)
        );
        shardsMain.gravityModifier = 2.4f; // Ağır cam kütlesi / hızlı balistik düşüş

        var shardsEmission = shardsPS.emission;
        shardsEmission.enabled = true;
        shardsEmission.rateOverTime = 0f;
        shardsEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 24, 34) });

        var shardsShape = shardsPS.shape;
        shardsShape.enabled = true;
        shardsShape.shapeType = ParticleSystemShapeType.Circle;
        shardsShape.radius = 0.55f;
        shardsShape.radiusThickness = 1.0f;
        shardsShape.arc = 360f;
        shardsShape.randomDirectionAmount = 0.70f;

        var shardsNoise = shardsPS.noise;
        shardsNoise.enabled = false; // Tüy gibi dalgalanma kapalı

        var shardsLimitVel = shardsPS.limitVelocityOverLifetime;
        shardsLimitVel.enabled = true;
        shardsLimitVel.dampen = 0.12f;
        shardsLimitVel.limit = new ParticleSystem.MinMaxCurve(3.0f);

        var shardsRotOL = shardsPS.rotationOverLifetime;
        shardsRotOL.enabled = true;
        shardsRotOL.z = new ParticleSystem.MinMaxCurve(-5f * Mathf.Deg2Rad, 5f * Mathf.Deg2Rad); // 5 derece minimal spin

        var shardsSizeOL = shardsPS.sizeOverLifetime;
        shardsSizeOL.enabled = true;
        shardsSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f,    0.4f),
            new Keyframe(0.10f, 1.0f),
            new Keyframe(0.65f, 0.85f),
            new Keyframe(1.0f,  0.0f)
        ));

        var shardsColorOL = shardsPS.colorOverLifetime;
        shardsColorOL.enabled = true;
        Gradient shardGrad = new Gradient();
        shardGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.80f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.70f), new GradientAlphaKey(0f, 1f) }
        );
        shardsColorOL.color = new ParticleSystem.MinMaxGradient(shardGrad);

        var shardsRend = shardsObj.GetComponent<ParticleSystemRenderer>();
        if (shardMat != null) shardsRend.sharedMaterial = shardMat;
        shardsRend.renderMode = ParticleSystemRenderMode.Billboard;
        shardsRend.maxParticleSize = 0.8f;
        shardsRend.sortingOrder = 105;

        // ── 2. FROST SHOCKWAVE (Soğuk Şok Dalgası Halkaları) ─────────────────
        GameObject ringObj = new GameObject("FrostShockwave");
        ringObj.transform.SetParent(root.transform, false);
        ringObj.transform.localPosition = Vector3.zero;

        ParticleSystem ringPS = ringObj.AddComponent<ParticleSystem>();
        var ringMain = ringPS.main;
        ringMain.duration = 0.5f;
        ringMain.loop = false;
        ringMain.playOnAwake = false;
        ringMain.simulationSpace = ParticleSystemSimulationSpace.World;
        ringMain.maxParticles = 8;
        ringMain.startLifetime = 0.38f;
        ringMain.startSpeed = 0.1f;
        ringMain.startSize = 0.45f;
        ringMain.startColor = new Color(0.90f, 0.96f, 1.0f, 1.0f);
        ringMain.gravityModifier = 0f;

        var ringEmission = ringPS.emission;
        ringEmission.enabled = true;
        ringEmission.rateOverTime = 0f;
        ringEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 2) });

        var ringShape = ringPS.shape;
        ringShape.enabled = true;
        ringShape.shapeType = ParticleSystemShapeType.Circle;
        ringShape.radius = 0.4f;

        var ringSizeOL = ringPS.sizeOverLifetime;
        ringSizeOL.enabled = true;
        ringSizeOL.size = new ParticleSystem.MinMaxCurve(7.0f, new AnimationCurve(
            new Keyframe(0f,   0.25f),
            new Keyframe(1.0f, 1.0f)
        ));

        var ringColorOL = ringPS.colorOverLifetime;
        ringColorOL.enabled = true;
        Gradient ringGrad = new Gradient();
        ringGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.70f, 0.92f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) }
        );
        ringColorOL.color = new ParticleSystem.MinMaxGradient(ringGrad);

        var ringRend = ringObj.GetComponent<ParticleSystemRenderer>();
        if (ringMat != null) ringRend.sharedMaterial = ringMat;
        ringRend.renderMode = ParticleSystemRenderMode.Billboard;
        ringRend.maxParticleSize = 2.5f;
        ringRend.sortingOrder = 102;

        // ── 3. DIAMOND SPARKLE BURST (Pırıltılı Elmas Tozu) ─────────────────
        GameObject sparkleObj = new GameObject("SparkleBurst");
        sparkleObj.transform.SetParent(root.transform, false);
        sparkleObj.transform.localPosition = Vector3.zero;

        ParticleSystem sparklePS = sparkleObj.AddComponent<ParticleSystem>();
        var sparkleMain = sparklePS.main;
        sparkleMain.duration = 0.9f;
        sparkleMain.loop = false;
        sparkleMain.playOnAwake = false;
        sparkleMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkleMain.maxParticles = 60;
        sparkleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        sparkleMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.2f);
        sparkleMain.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        sparkleMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        sparkleMain.startColor = Color.white;
        sparkleMain.gravityModifier = 0.15f;

        var sparkleEmission = sparklePS.emission;
        sparkleEmission.enabled = true;
        sparkleEmission.rateOverTime = 0f;
        sparkleEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 20, 28) });

        var sparkleShape = sparklePS.shape;
        sparkleShape.enabled = true;
        sparkleShape.shapeType = ParticleSystemShapeType.Circle;
        sparkleShape.radius = 0.5f;
        sparkleShape.randomDirectionAmount = 0.4f;

        var sparkleLimitVel = sparklePS.limitVelocityOverLifetime;
        sparkleLimitVel.enabled = true;
        sparkleLimitVel.dampen = 0.20f;
        sparkleLimitVel.limit = new ParticleSystem.MinMaxCurve(1.5f);

        var sparkleRotOL = sparklePS.rotationOverLifetime;
        sparkleRotOL.enabled = true;
        sparkleRotOL.z = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);

        var sparkleSizeOL = sparklePS.sizeOverLifetime;
        sparkleSizeOL.enabled = true;
        sparkleSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f,    0.2f),
            new Keyframe(0.20f, 1.0f),
            new Keyframe(0.65f, 0.85f),
            new Keyframe(1.0f,  0.0f)
        ));

        var sparkleColorOL = sparklePS.colorOverLifetime;
        sparkleColorOL.enabled = true;
        Gradient spGrad = new Gradient();
        spGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.95f, 0.45f), new GradientAlphaKey(0f, 1f) }
        );
        sparkleColorOL.color = new ParticleSystem.MinMaxGradient(spGrad);

        var sparkleRend = sparkleObj.GetComponent<ParticleSystemRenderer>();
        if (sparkleMat != null) sparkleRend.sharedMaterial = sparkleMat;
        sparkleRend.renderMode = ParticleSystemRenderMode.Billboard;
        sparkleRend.maxParticleSize = 0.8f;
        sparkleRend.sortingOrder = 108;

        // ── 4. FROST VAPOR PUFF (Soğuk Sis Pufu) ─────────────────────────────
        GameObject puffObj = new GameObject("FrostPuff");
        puffObj.transform.SetParent(root.transform, false);
        puffObj.transform.localPosition = Vector3.zero;

        ParticleSystem puffPS = puffObj.AddComponent<ParticleSystem>();
        var puffMain = puffPS.main;
        puffMain.duration = 0.8f;
        puffMain.loop = false;
        puffMain.playOnAwake = false;
        puffMain.simulationSpace = ParticleSystemSimulationSpace.World;
        puffMain.maxParticles = 30;
        puffMain.startLifetime = new ParticleSystem.MinMaxCurve(0.40f, 0.75f);
        puffMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        puffMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        puffMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        puffMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.90f, 0.96f, 1.0f, 0.75f),
            new Color(0.75f, 0.90f, 1.0f, 0.50f)
        );
        puffMain.gravityModifier = 0.05f;

        var puffEmission = puffPS.emission;
        puffEmission.enabled = true;
        puffEmission.rateOverTime = 0f;
        puffEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 10, 16) });

        var puffShape = puffPS.shape;
        puffShape.enabled = true;
        puffShape.shapeType = ParticleSystemShapeType.Circle;
        puffShape.radius = 0.4f;
        puffShape.randomDirectionAmount = 0.5f;

        var puffSizeOL = puffPS.sizeOverLifetime;
        puffSizeOL.enabled = true;
        puffSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f,    0.3f),
            new Keyframe(0.35f, 1.0f),
            new Keyframe(1.0f,  1.3f)
        ));

        var puffColorOL = puffPS.colorOverLifetime;
        puffColorOL.enabled = true;
        Gradient puffGrad = new Gradient();
        puffGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.80f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.15f), new GradientAlphaKey(0f, 1f) }
        );
        puffColorOL.color = new ParticleSystem.MinMaxGradient(puffGrad);

        var puffRend = puffObj.GetComponent<ParticleSystemRenderer>();
        if (puffMat != null) puffRend.sharedMaterial = puffMat;
        puffRend.renderMode = ParticleSystemRenderMode.Billboard;
        puffRend.maxParticleSize = 1.0f;
        puffRend.sortingOrder = 100;

        // Bind serialized fields to IceBreakFXController
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("shardsPS").objectReferenceValue = shardsPS;
        so.FindProperty("shockwavePS").objectReferenceValue = ringPS;
        so.FindProperty("sparkleBurstPS").objectReferenceValue = sparklePS;
        so.FindProperty("frostPuffPS").objectReferenceValue = puffPS;
        so.ApplyModifiedProperties();

        // Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[IceBreakFX] Successfully generated multi-module dramatic IceBreakFX.prefab!");
    }
}
