using UnityEditor;
using UnityEngine;

public static class CreateIceBreakFXPrefab
{
    private const string PrefabPath = "Assets/ART/VFX/IceBreakFX.prefab";
    private const string FlashMatPath = "Assets/Materials/M_IceFlash.mat";
    private const string RingMatPath = "Assets/Materials/M_FrostRingParticle.mat";
    private const string ShardMatPath = "Assets/Materials/M_IceShardParticle.mat";
    private const string NeedleMatPath = "Assets/Materials/M_IceNeedleParticle.mat";
    private const string SnowflakeMatPath = "Assets/Materials/M_IceSnowflakeParticle.mat";
    private const string SparkleMatPath = "Assets/Materials/M_IceSparkleParticle.mat";
    private const string PuffMatPath = "Assets/Materials/patlamalar.mat";

    [MenuItem("ARXON/Tools/Create Ice Break FX Prefab")]
    public static void Generate()
    {
        Material flashMat = AssetDatabase.LoadAssetAtPath<Material>(FlashMatPath);
        Material ringMat = AssetDatabase.LoadAssetAtPath<Material>(RingMatPath);
        Material shardMat = AssetDatabase.LoadAssetAtPath<Material>(ShardMatPath);
        Material needleMat = AssetDatabase.LoadAssetAtPath<Material>(NeedleMatPath);
        Material snowflakeMat = AssetDatabase.LoadAssetAtPath<Material>(SnowflakeMatPath);
        Material sparkleMat = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatPath);
        Material puffMat = AssetDatabase.LoadAssetAtPath<Material>(PuffMatPath);

        // Root GameObject
        GameObject root = new GameObject("IceBreakFX");

        // Main root ParticleSystem for orchestration and auto-destroy (uzatılmış süre: 1.8s)
        ParticleSystem rootPS = root.AddComponent<ParticleSystem>();
        var rootMain = rootPS.main;
        rootMain.duration = 1.8f;
        rootMain.loop = false;
        rootMain.playOnAwake = true;
        rootMain.stopAction = ParticleSystemStopAction.Destroy;
        rootMain.startLifetime = 1.8f;
        rootMain.startSpeed = 0f;
        rootMain.startSize = 0f;
        var rootEmission = rootPS.emission;
        rootEmission.enabled = false;
        var rootRend = root.GetComponent<ParticleSystemRenderer>();
        rootRend.enabled = false;

        IceBreakFXController controller = root.AddComponent<IceBreakFXController>();

        // ── 0. CENTER FLASH (Merkez Flaş - Katman 0) ─────────────────────────
        GameObject flashObj = new GameObject("CenterFlash");
        flashObj.transform.SetParent(root.transform, false);
        ParticleSystem flashPS = flashObj.AddComponent<ParticleSystem>();
        var flashMain = flashPS.main;
        flashMain.duration = 0.16f;
        flashMain.loop = false;
        flashMain.playOnAwake = false;
        flashMain.simulationSpace = ParticleSystemSimulationSpace.World;
        flashMain.maxParticles = 4;
        flashMain.startLifetime = 0.14f;
        flashMain.startSpeed = 0.1f;
        flashMain.startSize = 2.6f;
        flashMain.startColor = new Color(0.92f, 0.97f, 1.0f, 1.0f);
        flashMain.gravityModifier = 0f;

        var flashEmission = flashPS.emission;
        flashEmission.enabled = true;
        flashEmission.rateOverTime = 0f;
        flashEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 1) });

        var flashSizeOL = flashPS.sizeOverLifetime;
        flashSizeOL.enabled = true;
        flashSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.25f, 1.25f),
            new Keyframe(1.0f, 0.0f)
        ));

        var flashRend = flashObj.GetComponent<ParticleSystemRenderer>();
        if (flashMat != null) flashRend.sharedMaterial = flashMat;
        else if (ringMat != null) flashRend.sharedMaterial = ringMat;
        flashRend.renderMode = ParticleSystemRenderMode.Billboard;
        flashRend.sortingOrder = 115;

        // ── 1. FROST SHOCKWAVE (Şok Dalgası Halkaları - Katman 1) ───────────
        GameObject ringObj = new GameObject("FrostShockwave");
        ringObj.transform.SetParent(root.transform, false);
        ParticleSystem ringPS = ringObj.AddComponent<ParticleSystem>();
        var ringMain = ringPS.main;
        ringMain.duration = 0.35f;
        ringMain.loop = false;
        ringMain.playOnAwake = false;
        ringMain.simulationSpace = ParticleSystemSimulationSpace.World;
        ringMain.maxParticles = 8;
        ringMain.startLifetime = 0.30f;
        ringMain.startSpeed = 0.2f;
        ringMain.startSize = 0.40f;
        ringMain.startColor = new Color(0.85f, 0.95f, 1.0f, 0.90f);
        ringMain.gravityModifier = 0f;

        var ringEmission = ringPS.emission;
        ringEmission.enabled = true;
        ringEmission.rateOverTime = 0f;
        ringEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 2) });

        var ringSizeOL = ringPS.sizeOverLifetime;
        ringSizeOL.enabled = true;
        ringSizeOL.size = new ParticleSystem.MinMaxCurve(8.5f, new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(1.0f, 1.0f)
        ));

        var ringColorOL = ringPS.colorOverLifetime;
        ringColorOL.enabled = true;
        Gradient ringGrad = new Gradient();
        ringGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.60f, 0.90f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) }
        );
        ringColorOL.color = new ParticleSystem.MinMaxGradient(ringGrad);

        var ringRend = ringObj.GetComponent<ParticleSystemRenderer>();
        if (ringMat != null) ringRend.sharedMaterial = ringMat;
        ringRend.renderMode = ParticleSystemRenderMode.Billboard;
        ringRend.maxParticleSize = 3.5f;
        ringRend.sortingOrder = 103;

        // ── 2. FACETED SHARDS (Geometrik Kristal Parçaları - Katman 2) ───────
        GameObject shardsObj = new GameObject("FacetedShards");
        shardsObj.transform.SetParent(root.transform, false);
        ParticleSystem shardsPS = shardsObj.AddComponent<ParticleSystem>();
        var shardsMain = shardsPS.main;
        shardsMain.duration = 1.0f;
        shardsMain.loop = false;
        shardsMain.playOnAwake = false;
        shardsMain.simulationSpace = ParticleSystemSimulationSpace.World;
        shardsMain.maxParticles = 60;
        shardsMain.startLifetime = new ParticleSystem.MinMaxCurve(0.70f, 1.15f);
        shardsMain.startSpeed = new ParticleSystem.MinMaxCurve(3.8f, 8.0f);
        shardsMain.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.46f);
        shardsMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        shardsMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 1f),
            new Color(0.80f, 0.95f, 1f, 0.95f)
        );
        shardsMain.gravityModifier = 1.8f;

        var shardsEmission = shardsPS.emission;
        shardsEmission.enabled = true;
        shardsEmission.rateOverTime = 0f;
        shardsEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 16, 24) });

        var shardsShape = shardsPS.shape;
        shardsShape.enabled = true;
        shardsShape.shapeType = ParticleSystemShapeType.Circle;
        shardsShape.radius = 0.5f;
        shardsShape.randomDirectionAmount = 0.70f;

        var shardsLimitVel = shardsPS.limitVelocityOverLifetime;
        shardsLimitVel.enabled = true;
        shardsLimitVel.dampen = 0.08f; // Akıcı süzülme
        shardsLimitVel.limit = new ParticleSystem.MinMaxCurve(3.0f);

        var shardsRotOL = shardsPS.rotationOverLifetime;
        shardsRotOL.enabled = true;
        shardsRotOL.z = new ParticleSystem.MinMaxCurve(-45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);

        var shardsSizeOL = shardsPS.sizeOverLifetime;
        shardsSizeOL.enabled = true;
        shardsSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.15f, 1.0f),
            new Keyframe(0.75f, 0.85f),
            new Keyframe(1.0f, 0.0f)
        ));

        var shardsColorOL = shardsPS.colorOverLifetime;
        shardsColorOL.enabled = true;
        Gradient shardGrad = new Gradient();
        shardGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.80f), new GradientAlphaKey(0f, 1f) }
        );
        shardsColorOL.color = new ParticleSystem.MinMaxGradient(shardGrad);

        var shardsRend = shardsObj.GetComponent<ParticleSystemRenderer>();
        if (shardMat != null) shardsRend.sharedMaterial = shardMat;
        shardsRend.renderMode = ParticleSystemRenderMode.Billboard;
        shardsRend.maxParticleSize = 0.8f;
        shardsRend.sortingOrder = 108;

        // ── 3. GLACIAL NEEDLES (Keskin Buzul İğneleri - Katman 3) ───────────
        GameObject needleObj = new GameObject("GlacialNeedles");
        needleObj.transform.SetParent(root.transform, false);
        ParticleSystem needlePS = needleObj.AddComponent<ParticleSystem>();
        var needleMain = needlePS.main;
        needleMain.duration = 0.85f;
        needleMain.loop = false;
        needleMain.playOnAwake = false;
        needleMain.simulationSpace = ParticleSystemSimulationSpace.World;
        needleMain.maxParticles = 35;
        needleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.60f, 0.95f);
        needleMain.startSpeed = new ParticleSystem.MinMaxCurve(5.5f, 9.8f);
        needleMain.startSize = new ParticleSystem.MinMaxCurve(0.26f, 0.48f);
        needleMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        needleMain.startColor = new Color(0.90f, 0.98f, 1.0f, 1.0f);
        needleMain.gravityModifier = 1.2f;

        var needleEmission = needlePS.emission;
        needleEmission.enabled = true;
        needleEmission.rateOverTime = 0f;
        needleEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 10, 15) });

        var needleShape = needlePS.shape;
        needleShape.enabled = true;
        needleShape.shapeType = ParticleSystemShapeType.Circle;
        needleShape.radius = 0.4f;
        needleShape.randomDirectionAmount = 0.55f;

        var needleRend = needleObj.GetComponent<ParticleSystemRenderer>();
        if (needleMat != null) needleRend.sharedMaterial = needleMat;
        else if (shardMat != null) needleRend.sharedMaterial = shardMat;
        needleRend.renderMode = ParticleSystemRenderMode.Billboard;
        needleRend.sortingOrder = 110;

        // ── 4. ICE POWDER CLOUD (Buz Tozu Bulutu - Katman 4) ────────────────
        GameObject powderObj = new GameObject("IcePowderCloud");
        powderObj.transform.SetParent(root.transform, false);
        ParticleSystem powderPS = powderObj.AddComponent<ParticleSystem>();
        var powderMain = powderPS.main;
        powderMain.duration = 1.2f;
        powderMain.loop = false;
        powderMain.playOnAwake = false;
        powderMain.simulationSpace = ParticleSystemSimulationSpace.World;
        powderMain.maxParticles = 120;
        powderMain.startLifetime = new ParticleSystem.MinMaxCurve(0.80f, 1.30f);
        powderMain.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.8f);
        powderMain.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
        powderMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        powderMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.95f, 1.0f, 0.85f),
            new Color(0.65f, 0.88f, 1.0f, 0.60f)
        );
        powderMain.gravityModifier = 0.15f;

        var powderEmission = powderPS.emission;
        powderEmission.enabled = true;
        powderEmission.rateOverTime = 0f;
        powderEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 40, 60) });

        var powderShape = powderPS.shape;
        powderShape.enabled = true;
        powderShape.shapeType = ParticleSystemShapeType.Circle;
        powderShape.radius = 0.45f;
        powderShape.randomDirectionAmount = 0.8f;

        var powderSizeOL = powderPS.sizeOverLifetime;
        powderSizeOL.enabled = true;
        powderSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.35f, 1.0f),
            new Keyframe(1.0f, 0.0f)
        ));

        var powderRend = powderObj.GetComponent<ParticleSystemRenderer>();
        if (snowflakeMat != null) powderRend.sharedMaterial = snowflakeMat;
        else if (sparkleMat != null) powderRend.sharedMaterial = sparkleMat;
        powderRend.renderMode = ParticleSystemRenderMode.Billboard;
        powderRend.sortingOrder = 102;

        // ── 5. FLOATING SNOWFLAKES (Kar Taneleri Saçılması - Katman 5) ───────
        GameObject snowObj = new GameObject("FloatingSnowflakes");
        snowObj.transform.SetParent(root.transform, false);
        ParticleSystem snowPS = snowObj.AddComponent<ParticleSystem>();
        var snowMain = snowPS.main;
        snowMain.duration = 1.4f;
        snowMain.loop = false;
        snowMain.playOnAwake = false;
        snowMain.simulationSpace = ParticleSystemSimulationSpace.World;
        snowMain.maxParticles = 30;
        snowMain.startLifetime = new ParticleSystem.MinMaxCurve(0.90f, 1.50f);
        snowMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        snowMain.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
        snowMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        snowMain.startColor = new Color(0.92f, 0.98f, 1.0f, 0.90f);
        snowMain.gravityModifier = -0.06f; // Havada süzülerek yükselir

        var snowEmission = snowPS.emission;
        snowEmission.enabled = true;
        snowEmission.rateOverTime = 0f;
        snowEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.03f, 8, 12) });

        var snowShape = snowPS.shape;
        snowShape.enabled = true;
        snowShape.shapeType = ParticleSystemShapeType.Circle;
        snowShape.radius = 0.4f;

        var snowRotOL = snowPS.rotationOverLifetime;
        snowRotOL.enabled = true;
        snowRotOL.z = new ParticleSystem.MinMaxCurve(-120f * Mathf.Deg2Rad, 120f * Mathf.Deg2Rad);

        var snowSizeOL = snowPS.sizeOverLifetime;
        snowSizeOL.enabled = true;
        snowSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.20f, 1.0f),
            new Keyframe(0.75f, 0.85f),
            new Keyframe(1.0f, 0.0f)
        ));

        var snowRend = snowObj.GetComponent<ParticleSystemRenderer>();
        if (snowflakeMat != null) snowRend.sharedMaterial = snowflakeMat;
        snowRend.renderMode = ParticleSystemRenderMode.Billboard;
        snowRend.sortingOrder = 107;

        // ── 6. RISING VAPOR (Yükselen Yoğun Soğuk Sis / Buz Buharı - Katman 6) ─
        GameObject vaporObj = new GameObject("RisingVapor");
        vaporObj.transform.SetParent(root.transform, false);
        ParticleSystem vaporPS = vaporObj.AddComponent<ParticleSystem>();
        var vaporMain = vaporPS.main;
        vaporMain.duration = 1.7f;
        vaporMain.loop = false;
        vaporMain.playOnAwake = false;
        vaporMain.simulationSpace = ParticleSystemSimulationSpace.World;
        vaporMain.maxParticles = 60;
        vaporMain.startLifetime = new ParticleSystem.MinMaxCurve(1.10f, 1.70f);
        vaporMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
        vaporMain.startSize = new ParticleSystem.MinMaxCurve(0.60f, 1.20f); // İhtişamlı büyük sis pufu
        vaporMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        vaporMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.95f, 1.0f, 0.70f),
            new Color(0.65f, 0.85f, 1.0f, 0.40f)
        );
        vaporMain.gravityModifier = -0.18f; // Soğuk don dumanı gibi yukarı süzülür

        var vaporEmission = vaporPS.emission;
        vaporEmission.enabled = true;
        vaporEmission.rateOverTime = 0f;
        vaporEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.05f, 18, 26) });

        var vaporShape = vaporPS.shape;
        vaporShape.enabled = true;
        vaporShape.shapeType = ParticleSystemShapeType.Circle;
        vaporShape.radius = 0.45f;

        var vaporRotOL = vaporPS.rotationOverLifetime;
        vaporRotOL.enabled = true;
        vaporRotOL.z = new ParticleSystem.MinMaxCurve(-30f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);

        var vaporSizeOL = vaporPS.sizeOverLifetime;
        vaporSizeOL.enabled = true;
        vaporSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.35f, 1.0f),
            new Keyframe(1.0f, 1.50f) // Genişleyerek dağılır
        ));

        var vaporColorOL = vaporPS.colorOverLifetime;
        vaporColorOL.enabled = true;
        Gradient vaporGrad = new Gradient();
        vaporGrad.SetKeys(
            new[] { new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.60f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.70f, 0.20f), new GradientAlphaKey(0.50f, 0.60f), new GradientAlphaKey(0f, 1f) }
        );
        vaporColorOL.color = new ParticleSystem.MinMaxGradient(vaporGrad);

        var vaporRend = vaporObj.GetComponent<ParticleSystemRenderer>();
        if (puffMat != null) vaporRend.sharedMaterial = puffMat;
        else if (ringMat != null) vaporRend.sharedMaterial = ringMat;
        vaporRend.renderMode = ParticleSystemRenderMode.Billboard;
        vaporRend.sortingOrder = 101;

        // ── 7. DIAMOND SPARKLES (Elmas Pırıltıları - Katman 7) ─────────────────
        GameObject sparkleObj = new GameObject("DiamondSparkles");
        sparkleObj.transform.SetParent(root.transform, false);
        ParticleSystem sparklePS = sparkleObj.AddComponent<ParticleSystem>();
        var sparkleMain = sparklePS.main;
        sparkleMain.duration = 1.0f;
        sparkleMain.loop = false;
        sparkleMain.playOnAwake = false;
        sparkleMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkleMain.maxParticles = 70;
        sparkleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.60f, 1.05f);
        sparkleMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
        sparkleMain.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
        sparkleMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        sparkleMain.startColor = Color.white;
        sparkleMain.gravityModifier = 0.08f;

        var sparkleEmission = sparklePS.emission;
        sparkleEmission.enabled = true;
        sparkleEmission.rateOverTime = 0f;
        sparkleEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 20, 30) });

        var sparkleShape = sparklePS.shape;
        sparkleShape.enabled = true;
        sparkleShape.shapeType = ParticleSystemShapeType.Circle;
        sparkleShape.radius = 0.5f;

        var sparkleRotOL = sparklePS.rotationOverLifetime;
        sparkleRotOL.enabled = true;
        sparkleRotOL.z = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);

        var sparkleSizeOL = sparklePS.sizeOverLifetime;
        sparkleSizeOL.enabled = true;
        sparkleSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.25f, 1.0f),
            new Keyframe(0.70f, 0.85f),
            new Keyframe(1.0f, 0.0f)
        ));

        var sparkleRend = sparkleObj.GetComponent<ParticleSystemRenderer>();
        if (sparkleMat != null) sparkleRend.sharedMaterial = sparkleMat;
        sparkleRend.renderMode = ParticleSystemRenderMode.Billboard;
        sparkleRend.sortingOrder = 112;

        // Bind serialized fields to IceBreakFXController
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("centerFlashPS").objectReferenceValue = flashPS;
        so.FindProperty("shockwavePS").objectReferenceValue = ringPS;
        so.FindProperty("shardsPS").objectReferenceValue = shardsPS;
        so.FindProperty("needlesPS").objectReferenceValue = needlePS;
        so.FindProperty("powderCloudPS").objectReferenceValue = powderPS;
        so.FindProperty("snowflakesPS").objectReferenceValue = snowPS;
        so.FindProperty("vaporPS").objectReferenceValue = vaporPS;
        so.FindProperty("sparkleBurstPS").objectReferenceValue = sparklePS;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("<color=#00E5FF>[IceBreakFX]</color> Successfully regenerated enhanced 8-layered AAA Glacial Shatter Prefab at " + PrefabPath);
    }
}
