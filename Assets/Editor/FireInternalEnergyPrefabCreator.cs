#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FireInternalEnergyPrefabCreator
{
    private const string PrefabPath = "Assets/ART/VFX/FireInternalEnergyFlow.prefab";
    private const string BlockPrefabPath = "Assets/Prefabs/Block.prefab";
    private const string FireSymbolGlowShaderName = "ARXON/Fire V2/Symbol Glow";
    private const string FireSymbolGlowMaterialPath = "Assets/ART/VFX/M_FireSymbolGlow.mat";

    [MenuItem("ARXON/Fire V2/Create Internal Energy Flow Prefab")]
    private static void CreateInternalEnergyFlowPrefab()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab == null)
        {
            GameObject root = new GameObject("FireInternalEnergyRoot");
            CreateEmitter(root.transform, "FireInternalEnergy_LeftEdge", 1f, 17);
            CreateEmitter(root.transform, "FireInternalEnergy_RightEdge", -1f, 43);

            existingPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        Material glowMaterial = ResolveFireSymbolGlowMaterial();
        AssignPrefabToBlock(existingPrefab, glowMaterial);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = existingPrefab;
        Debug.Log($"Created Fire V2 prefab at {PrefabPath} and assigned it to Block.prefab.");
    }

    private static void CreateEmitter(Transform parent, string name, float direction, uint seed)
    {
        GameObject emitterObject = new GameObject(name);
        emitterObject.transform.SetParent(parent, false);

        ParticleSystem particleSystem = emitterObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = emitterObject.GetComponent<ParticleSystemRenderer>();

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.startSpeed = 0f;
        main.startLifetime = 0.9f;
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-15f, 15f);
        main.maxParticles = 28;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.98f, 0.92f, 0.95f),
            new Color(1f, 0.96f, 0.82f, 0.90f));

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 1.2f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 1),
            new ParticleSystem.Burst(0.2f, 1),
            new ParticleSystem.Burst(0.5f, 1)
        });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.015f, 0.325f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(direction * 0.6f, direction * 1f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.95f), 0f),
                new GradientColorKey(new Color(1f, 0.98f, 0.85f), 0.5f),
                new GradientColorKey(new Color(1f, 0.90f, 0.60f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0.3f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            });

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        ParticleSystem.TrailModule trails = particleSystem.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = 0.75f;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;
        trails.sizeAffectsLifetime = false;
        trails.minVertexDistance = 0.003f;
        trails.inheritParticleColor = true;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1.2f),
                new Keyframe(0.3f, 0.8f),
                new Keyframe(0.7f, 0.4f),
                new Keyframe(1f, 0f)));
        trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(colorGradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 0.85f),
                new Keyframe(1f, 0.5f)));

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.OldestInFront;

        particleSystem.useAutoRandomSeed = false;
        particleSystem.randomSeed = seed;
    }

    private static Material ResolveFireSymbolGlowMaterial()
    {
        Material glowMaterial = AssetDatabase.LoadAssetAtPath<Material>(FireSymbolGlowMaterialPath);
        if (glowMaterial != null)
            return glowMaterial;

        Shader glowShader = Shader.Find(FireSymbolGlowShaderName);
        if (glowShader == null)
        {
            Debug.LogError($"Fire symbol glow shader was not found: {FireSymbolGlowShaderName}");
            return null;
        }

        glowMaterial = new Material(glowShader)
        {
            name = "M_FireSymbolGlow"
        };
        glowMaterial.SetColor("_GlowColor", new Color(1f, 0.92f, 0.65f, 0.65f));
        glowMaterial.SetFloat("_GlowIntensity", 1.35f);
        glowMaterial.SetFloat("_GlowRadius", 2.5f);
        glowMaterial.SetFloat("_GlowSoftness", 1.25f);
        AssetDatabase.CreateAsset(glowMaterial, FireSymbolGlowMaterialPath);
        return glowMaterial;
    }

    private static void AssignPrefabToBlock(GameObject flowPrefab, Material glowMaterial)
    {
        GameObject blockContents = PrefabUtility.LoadPrefabContents(BlockPrefabPath);
        try
        {
            Block block = blockContents.GetComponent<Block>();
            if (block == null)
                return;

            Transform symbolTransform = blockContents.transform.Find("FireSymbolV2");
            if (symbolTransform == null)
            {
                GameObject symbolObject = new GameObject("FireSymbolV2");
                symbolTransform = symbolObject.transform;
                symbolTransform.SetParent(blockContents.transform, false);
            }

            SpriteRenderer symbolRenderer = symbolTransform.GetComponent<SpriteRenderer>();
            if (symbolRenderer == null)
                symbolRenderer = symbolTransform.gameObject.AddComponent<SpriteRenderer>();
            symbolRenderer.enabled = false;

            Transform glowTransform = symbolTransform.Find("FireSymbolGlow");
            if (glowTransform == null)
            {
                GameObject glowObject = new GameObject("FireSymbolGlow");
                glowTransform = glowObject.transform;
                glowTransform.SetParent(symbolTransform, false);
            }

            SpriteRenderer glowRenderer = glowTransform.GetComponent<SpriteRenderer>();
            if (glowRenderer == null)
                glowRenderer = glowTransform.gameObject.AddComponent<SpriteRenderer>();
            bool migrateLegacyGlow = glowMaterial != null && glowRenderer.sharedMaterial != glowMaterial;
            glowRenderer.enabled = false;
            if (glowMaterial != null)
                glowRenderer.sharedMaterial = glowMaterial;

            // FIRE_V2_CLEANUP: One-time migration from the legacy enlarged sprite copy.
            // Once the material is assigned, Inspector-authored values are preserved on later runs.
            if (migrateLegacyGlow)
            {
                glowRenderer.color = Color.white;
                glowRenderer.transform.localPosition = Vector3.zero;
                glowRenderer.transform.localRotation = Quaternion.identity;
                glowRenderer.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
            }

            SerializedObject serializedBlock = new SerializedObject(block);
            SerializedProperty property = serializedBlock.FindProperty("fireInternalEnergyFlowPrefab");
            if (property == null)
                return;

            property.objectReferenceValue = flowPrefab;
            SerializedProperty symbolProperty = serializedBlock.FindProperty("fireSymbolRenderer");
            if (symbolProperty != null)
                symbolProperty.objectReferenceValue = symbolRenderer;
            serializedBlock.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(blockContents, BlockPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(blockContents);
        }
    }
}
#endif
