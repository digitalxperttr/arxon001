using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AdventureEventPackageEditorUtility
{
    private const string ComparisonPackageRoot = "Assets/AdventureEvents/LocalTrial_02_Comparison";
    private const string TuningPackageRoot = "Assets/AdventureEvents/LocalTrial_03_Tuning";
    private const string LocalTrial01Path = "Assets/AdventureEvents/LocalTrial_01/ARXON_LocalTrial_01.asset";
    private const string LocalTrial02Path = "Assets/AdventureEvents/LocalTrial_02_Comparison/ARXON_LocalTrial_02_Comparison.asset";

    public static bool CreateNewEventPackage(AdventureEventCreationRequest request, out AdventureEventConfig createdEvent, out string message)
    {
        createdEvent = null;
        message = null;
        if (EditorApplication.isPlaying)
        {
            message = "Yeni event yalnızca Play Mode dışında oluşturulabilir. Açık Adventure attempt'i varken paket değiştirilmez.";
            return false;
        }

        if (!TryValidateNewEventRequest(request, out string packageFolder, out string eventId, out message)) return false;

        AdventureEventConfig eventConfig = BuildNewEventConfig(request, eventId);
        AdventureEventGenerationPlan preflightPlan = AdventureEventLevelFactory.BuildPlan(eventConfig);
        if (!preflightPlan.IsValid)
        {
            message = string.Join("\n", preflightPlan.Errors);
            DestroyPlan(preflightPlan);
            Object.DestroyImmediate(eventConfig);
            return false;
        }
        DestroyPlan(preflightPlan);

        bool folderCreated = false;
        try
        {
            if (!EnsureFolder(packageFolder, out string folderError)) throw new InvalidOperationException(folderError);
            folderCreated = true;

            string eventAssetPath = packageFolder + "/ARXON_" + SanitizePathPart(request.eventName) + ".asset";
            AssetDatabase.CreateAsset(eventConfig, eventAssetPath);
            if (!GenerateInitialPackage(eventConfig, packageFolder + "/Levels", out string generationMessage))
                throw new InvalidOperationException(generationMessage);

            AdventureContentValidationResult validation = ValidateEvent(eventConfig);
            if (validation.HasErrors) throw new InvalidOperationException("Paket doğrulaması başarısız:\n" + string.Join("\n", validation.Errors));

            AssetDatabase.SaveAssets();
            Selection.activeObject = eventConfig;
            EditorGUIUtility.PingObject(eventConfig);
            createdEvent = eventConfig;
            message = "50 Direct bölüm oluşturuldu ve paket doğrulandı. Inspector'dan 'Use This Local Event' ile MainMenu kaynağını seçebilir, ardından 'Play Local Event' ile açabilirsiniz.";
            return true;
        }
        catch (Exception exception)
        {
            if (folderCreated) AssetDatabase.DeleteAsset(packageFolder);
            else Object.DestroyImmediate(eventConfig);
            AssetDatabase.SaveAssets();
            message = "Yeni event oluşturulamadı. Bu işleme ait yarım çıktı temizlendi: " + exception.Message;
            return false;
        }
    }

    public static bool TryValidateNewEventRequest(AdventureEventCreationRequest request, out string packageFolder, out string eventId, out string message)
    {
        packageFolder = null;
        eventId = null;
        message = null;
        if (request == null || string.IsNullOrWhiteSpace(request.eventName))
        {
            message = "Event adı gereklidir.";
            return false;
        }
        if (request.collectibleDatabase == null || AdventureEventLevelFactory.GetEligibleCollectibles(request.collectibleDatabase).Count == 0)
        {
            message = "Collectible Database, boş olmayan ID ve ikon içeren en az bir availableInGenerator öğesi sağlamalıdır.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.parentFolder) || !request.parentFolder.StartsWith("Assets", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(request.parentFolder))
        {
            message = "Ana klasör Assets altında mevcut bir klasör olmalıdır.";
            return false;
        }

        string folderName = SanitizePathPart(string.IsNullOrWhiteSpace(request.packageFolderName) ? request.eventName : request.packageFolderName);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            message = "Paket klasörü adı geçerli karakterler içermelidir.";
            return false;
        }
        packageFolder = request.parentFolder.TrimEnd('/') + "/" + folderName;
        if (AssetDatabase.IsValidFolder(packageFolder) || AssetDatabase.LoadMainAssetAtPath(packageFolder) != null)
        {
            message = "Hedef paket klasörü zaten var: " + packageFolder;
            return false;
        }

        eventId = GetUniqueEventId(request.eventName);
        return true;
    }

    private static AdventureEventConfig BuildNewEventConfig(AdventureEventCreationRequest request, string eventId)
    {
        AdventureEventConfig eventConfig = ScriptableObject.CreateInstance<AdventureEventConfig>();
        eventConfig.eventId = eventId;
        eventConfig.contentVersion = "1.0.0";
        eventConfig.eventName = request.eventName.Trim();
        eventConfig.eventTheme = "Local";
        eventConfig.mapBackgroundVisual = request.mapBackgroundVisual;
        eventConfig.gameplayBackgroundVisual = request.gameplayBackgroundVisual;
        eventConfig.levelCount = 50;
        eventConfig.collectibleDatabase = request.collectibleDatabase;
        eventConfig.generationSettings = new AdventureEventGenerationSettings
        {
            seed = request.seed,
            generatorVersion = AdventureEventLevelFactory.CurrentGeneratorVersion,
            defaultLevelCount = 50,
            defaultTargetObstaclePerRowLimit = Mathf.Max(0, request.targetObstaclePerRowLimit),
            defaultTargetObstacleActiveBoardLimit = Mathf.Max(0, request.targetObstacleActiveBoardLimit)
        };
        return eventConfig;
    }

    public static string GetUniqueEventId(string eventName)
    {
        string baseId = SanitizePathPart(eventName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(baseId)) baseId = "local-event";
        HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:AdventureEventConfig"))
        {
            AdventureEventConfig existing = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (existing != null && !string.IsNullOrWhiteSpace(existing.eventId)) used.Add(existing.eventId);
        }
        if (!used.Contains(baseId)) return baseId;
        for (int suffix = 2; ; suffix++)
        {
            string candidate = baseId + "-" + suffix;
            if (!used.Contains(candidate)) return candidate;
        }
    }

    public static string SanitizePathPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        StringBuilder builder = new StringBuilder();
        bool previousDash = false;
        foreach (char character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }
        return builder.ToString().Trim('-');
    }

    [MenuItem("ARXON/Adventure/Create Local Trial 03 Tuning Package")]
    public static void CreateLocalTrial03TuningPackageMenu()
    {
        if (CreateLocalTrial03TuningPackage(out string message)) Debug.Log("[Adventure] " + message);
        else Debug.LogError("[Adventure] " + message);
    }

    public static bool CreateLocalTrial03TuningPackage(out string message)
    {
        message = null;
        if (AssetDatabase.IsValidFolder(TuningPackageRoot))
        {
            message = "Tuning package already exists. It was not changed.";
            return false;
        }

        AdventureEventConfig source = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(LocalTrial02Path);
        if (source == null || source.collectibleDatabase == null)
        {
            message = "LocalTrial_02 source event or its collectible database is missing.";
            return false;
        }
        if (!EnsureFolder(TuningPackageRoot, out message)) return false;

        AdventureEventConfig tuning = ScriptableObject.CreateInstance<AdventureEventConfig>();
        tuning.eventId = "local-trial-03-tuning";
        tuning.contentVersion = "3.0.0";
        tuning.eventName = "ARXON Yerel Denge Turu";
        tuning.eventTheme = source.eventTheme;
        tuning.eventDescription = "LocalTrial_02 hedefleri korunarak açılış engel arzı, Fire ritmi ve sis baskısı için üretilen karşılaştırma paketi.";
        tuning.mapBackgroundVisual = source.mapBackgroundVisual;
        tuning.gameplayBackgroundVisual = source.gameplayBackgroundVisual;
        tuning.levelCount = 50;
        tuning.startingDifficulty = source.startingDifficulty;
        tuning.endingDifficulty = source.endingDifficulty;
        tuning.featuredTheme = source.featuredTheme;
        tuning.featuredMechanic = source.featuredMechanic;
        tuning.difficultyCurve = source.difficultyCurve;
        tuning.collectibleDatabase = source.collectibleDatabase;
        tuning.generationSettings = new AdventureEventGenerationSettings
        {
            seed = source.generationSettings != null ? source.generationSettings.seed : 20260908,
            generatorVersion = AdventureEventLevelFactory.CurrentGeneratorVersion,
            defaultLevelCount = 50
        };

        string eventPath = TuningPackageRoot + "/ARXON_LocalTrial_03_Tuning.asset";
        AssetDatabase.CreateAsset(tuning, eventPath);
        if (!GenerateInitialPackage(tuning, TuningPackageRoot + "/Levels", out message))
        {
            message = "Tuning event was created but its level package could not be generated: " + message;
            return false;
        }

        Selection.activeObject = tuning;
        EditorGUIUtility.PingObject(tuning);
        message = "Created the LocalTrial_03 tuning package with 50 Direct stages. Select it and use 'Use This Local Event' or 'Play Local Event' in its Inspector.";
        return true;
    }

    [MenuItem("ARXON/Adventure/Create Local Trial 02 Comparison Package")]
    public static void CreateLocalTrial02ComparisonPackageMenu()
    {
        if (CreateLocalTrial02ComparisonPackage(out string message))
        {
            Debug.Log("[Adventure] " + message);
        }
        else
        {
            Debug.LogError("[Adventure] " + message);
        }
    }

    public static bool CreateLocalTrial02ComparisonPackage(out string message)
    {
        message = null;
        if (AssetDatabase.IsValidFolder(ComparisonPackageRoot))
        {
            message = "Comparison package already exists. It was not changed.";
            return false;
        }

        AdventureEventConfig source = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(LocalTrial01Path);
        if (source == null || source.collectibleDatabase == null)
        {
            message = "LocalTrial_01 source event or its collectible database is missing.";
            return false;
        }

        if (!EnsureFolder(ComparisonPackageRoot, out message))
        {
            return false;
        }

        AdventureEventConfig comparison = ScriptableObject.CreateInstance<AdventureEventConfig>();
        comparison.eventId = "local-trial-02-comparison";
        comparison.contentVersion = "2.0.0";
        comparison.eventName = "ARXON Yerel Karşılaştırma";
        comparison.eventTheme = source.eventTheme;
        comparison.eventDescription = "LocalTrial_01 korunarak üretilen, ilk oynanış geri bildirimine göre dengelenmiş 50 bölümlük karşılaştırma paketi.";
        comparison.mapBackgroundVisual = source.mapBackgroundVisual;
        comparison.gameplayBackgroundVisual = source.gameplayBackgroundVisual;
        comparison.levelCount = 50;
        comparison.startingDifficulty = DifficultyTier.Easy;
        comparison.endingDifficulty = DifficultyTier.Expert;
        comparison.featuredTheme = ObstacleTheme.Mixed;
        comparison.featuredMechanic = SpecialMechanicFocus.None;
        comparison.difficultyCurve = source.difficultyCurve;
        comparison.collectibleDatabase = source.collectibleDatabase;
        comparison.generationSettings = new AdventureEventGenerationSettings
        {
            seed = source.generationSettings != null ? source.generationSettings.seed : 20260908,
            generatorVersion = AdventureEventLevelFactory.CurrentGeneratorVersion,
            defaultLevelCount = 50
        };

        string eventPath = ComparisonPackageRoot + "/ARXON_LocalTrial_02_Comparison.asset";
        AssetDatabase.CreateAsset(comparison, eventPath);
        if (!GenerateInitialPackage(comparison, ComparisonPackageRoot + "/Levels", out message))
        {
            message = "Comparison event was created but its level package could not be generated: " + message;
            return false;
        }

        Selection.activeObject = comparison;
        EditorGUIUtility.PingObject(comparison);
        message = "Created the LocalTrial_02 comparison package with 50 Direct stages. Select the event and use 'Use This Local Event' or 'Play Local Event' in its Inspector.";
        return true;
    }

    public static bool GenerateInitialPackage(AdventureEventConfig eventConfig, string outputFolder, out string message)
    {
        message = null;
        if (eventConfig == null) { message = "Event config is missing."; return false; }
        if (eventConfig.HasUsableLevelList) { message = "Generate is first-build only. This event already has levels; use selected regeneration for an unlocked stage."; return false; }

        AdventureEventGenerationPlan plan = AdventureEventLevelFactory.BuildPlan(eventConfig);
        if (!plan.IsValid) { message = string.Join("\n", plan.Errors); DestroyPlan(plan); return false; }
        if (!EnsureFolder(outputFolder, out message)) { DestroyPlan(plan); return false; }
        if (AssetDatabase.FindAssets("t:AdventureLevelConfig", new[] { outputFolder }).Length > 0)
        {
            message = "Output folder already contains AdventureLevelConfig assets. Initial generation requires an empty package folder.";
            DestroyPlan(plan);
            return false;
        }
        for (int i = 0; i < plan.Levels.Count; i++)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/L" + plan.Levels[i].StageNumber.ToString("000") + ".asset");
            if (AssetDatabase.LoadAssetAtPath<AdventureLevelConfig>(path) != null)
            {
                message = "Output path already contains generated levels. Choose a new empty package folder.";
                DestroyPlan(plan);
                return false;
            }
        }

        List<string> createdPaths = new List<string>();
        try
        {
            AdventureLevelConfig[] orderedLevels = new AdventureLevelConfig[plan.Levels.Count];
            for (int i = 0; i < plan.Levels.Count; i++)
            {
                AdventureLevelConfig level = plan.Levels[i].Config;
                string path = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/L" + plan.Levels[i].StageNumber.ToString("000") + ".asset");
                AssetDatabase.CreateAsset(level, path);
                createdPaths.Add(path);
                orderedLevels[i] = level;
            }
            Undo.RecordObject(eventConfig, "Generate Adventure Event Package");
            eventConfig.levelConfigs = orderedLevels;
            eventConfig.levelCount = orderedLevels.Length;
            if (eventConfig.generationSettings == null) eventConfig.generationSettings = new AdventureEventGenerationSettings();
            if (string.IsNullOrWhiteSpace(eventConfig.generationSettings.generatorVersion)) eventConfig.generationSettings.generatorVersion = AdventureEventLevelFactory.CurrentGeneratorVersion;
            EditorUtility.SetDirty(eventConfig);
            AssetDatabase.SaveAssets();
            message = $"Generated {orderedLevels.Length} Direct Adventure stages in {outputFolder}.";
            return true;
        }
        catch (System.Exception exception)
        {
            for (int i = 0; i < createdPaths.Count; i++) AssetDatabase.DeleteAsset(createdPaths[i]);
            message = "Generation stopped before a complete package could be saved: " + exception.Message;
            return false;
        }
    }

    public static AdventureContentValidationResult ValidateEvent(AdventureEventConfig eventConfig)
    {
        AdventureContentValidationResult result = new AdventureContentValidationResult();
        if (eventConfig == null) { result.Errors.Add("Event config is missing."); return result; }
        if (string.IsNullOrWhiteSpace(eventConfig.eventId)) result.Errors.Add("Event ID is empty.");
        if (string.IsNullOrWhiteSpace(eventConfig.contentVersion)) result.Errors.Add("Content version is empty.");
        if (eventConfig.levelConfigs == null || eventConfig.levelConfigs.Length == 0) { result.Errors.Add("Ordered level list is empty."); return result; }

        HashSet<string> ids = new HashSet<string>();
        for (int i = 0; i < eventConfig.levelConfigs.Length; i++)
        {
            AdventureLevelConfig config = eventConfig.levelConfigs[i];
            string prefix = "Stage " + (i + 1) + ": ";
            if (config == null) { result.Errors.Add(prefix + "asset reference is missing."); continue; }
            if (!ids.Add(config.levelId)) result.Errors.Add(prefix + "duplicate or empty stable level ID '" + config.levelId + "'.");
            if (config.eventId != eventConfig.eventId || config.contentVersion != eventConfig.contentVersion) result.Errors.Add(prefix + "event identity does not match the package.");
            if (config.GetStageNumber() != i + 1) result.Errors.Add(prefix + "displayed stage number does not match ordered list.");
            if (!AdventureLevelGenerator.ValidateConfig(config, out string directError)) { result.Errors.Add(prefix + directError); continue; }
            LevelData runtime = AdventureLevelGenerator.GenerateRuntimeLevel(config);
            AdventureContentValidationResult levelResult = AdventureContentValidator.ValidateLevel(runtime, eventConfig.collectibleDatabase,
                new AdventureRowGenerationContext(8, runtime, new[] { Color.red, Color.blue }));
            result.Errors.AddRange(levelResult.Errors);
            result.Warnings.AddRange(levelResult.Warnings);
            result.RiskNotes.AddRange(levelResult.RiskNotes);
            Object.DestroyImmediate(runtime);
        }
        return result;
    }

    private static bool EnsureFolder(string assetPath, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/")) { error = "Package folder must be below Assets/."; return false; }
        string[] parts = assetPath.Split('/');
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        return true;
    }

    private static void DestroyPlan(AdventureEventGenerationPlan plan)
    {
        for (int i = 0; i < plan.Levels.Count; i++) if (plan.Levels[i].Config != null) Object.DestroyImmediate(plan.Levels[i].Config);
    }
}

[CustomEditor(typeof(AdventureEventConfig))]
public class AdventureEventConfigEditor : Editor
{
    private int selectedIndex;
    private AdventureContentValidationResult validation;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        AdventureEventConfig eventConfig = (AdventureEventConfig)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Map Background yalnızca AdventureMap'te bölüm node'larının arkasında kullanılır. " +
            "Gameplay Background haritaya uygulanmaz. Eski Opening Visual atamaları uyumluluk için korunmuştur; yayınlamadan önce doğru harita görselini doğrulayın.",
            MessageType.Info);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Local Event Package", EditorStyles.boldLabel);
        if (!eventConfig.HasUsableLevelList)
        {
            if (GUILayout.Button("Generate Initial Direct Package"))
            {
                string root = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(eventConfig)).Replace('\\', '/');
                if (AdventureEventPackageEditorUtility.GenerateInitialPackage(eventConfig, root + "/Levels", out string message)) Debug.Log("[Adventure] " + message, eventConfig);
                else Debug.LogError("[Adventure] " + message, eventConfig);
            }
        }
        else
        {
            string[] options = new string[eventConfig.levelConfigs.Length];
            for (int i = 0; i < options.Length; i++)
            {
                AdventureLevelConfig level = eventConfig.levelConfigs[i];
                options[i] = level == null ? (i + 1) + " - Missing" : level.GetStageNumber() + " - " + level.generatedRhythmRole + (level.protectFromRegeneration ? " [Protected]" : "");
            }
            selectedIndex = EditorGUILayout.Popup("Selected Stage", Mathf.Clamp(selectedIndex, 0, options.Length - 1), options);
            AdventureLevelConfig selected = eventConfig.levelConfigs[selectedIndex];
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Selected") && selected != null) Selection.activeObject = selected;
                if (GUILayout.Button(selected != null && selected.protectFromRegeneration ? "Unprotect Selected" : "Protect Selected") && selected != null)
                {
                    Undo.RecordObject(selected, "Toggle Adventure Stage Protection");
                    selected.protectFromRegeneration = !selected.protectFromRegeneration;
                    EditorUtility.SetDirty(selected);
                }
                using (new EditorGUI.DisabledScope(selected == null || selected.protectFromRegeneration))
                    if (GUILayout.Button("Regenerate Selected"))
                    {
                        Undo.RecordObject(selected, "Regenerate Adventure Stage");
                        if (AdventureEventLevelFactory.RegenerateSelected(eventConfig, selected, out string error)) EditorUtility.SetDirty(selected);
                        else Debug.LogError("[Adventure] " + error, selected);
                    }
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Package")) validation = AdventureEventPackageEditorUtility.ValidateEvent(eventConfig);
            if (GUILayout.Button("Use This Local Event")) SelectForMainMenu(eventConfig);
            if (GUILayout.Button("Play Local Event")) PlayLocalEvent(eventConfig);
        }
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Distribution", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bu işlem mevcut Event ve Stage asset'lerini değiştirmez. Seçilen klasöre platforma özel AssetBundle ve katalog kaydı üretir; aynı event+sürüm üzerine yazılmaz.",
            MessageType.None);
        if (GUILayout.Button("Dağıtım Paketi Hazırla"))
        {
            string outputRoot = EditorUtility.SaveFolderPanel("Adventure dağıtım çıktısı", Application.dataPath, string.Empty);
            if (!string.IsNullOrWhiteSpace(outputRoot))
            {
                if (AdventureEventDistributionBuilder.PrepareDistributionPackage(eventConfig, outputRoot, EditorUserBuildSettings.activeBuildTarget, out string distributionMessage))
                    Debug.Log("[Adventure Distribution] " + distributionMessage, eventConfig);
                else
                    Debug.LogError("[Adventure Distribution] " + distributionMessage, eventConfig);
            }
        }
        DrawActiveLocalEventStatus(eventConfig);
        DrawValidation();
        DrawBulkSummary(eventConfig);
    }

    private static bool SelectForMainMenu(AdventureEventConfig eventConfig)
    {
        ProgressManager manager = Object.FindFirstObjectByType<ProgressManager>();
        if (EditorApplication.isPlaying)
        {
            bool selected = manager != null && manager.TrySelectLocalEvent(eventConfig);
            if (selected) Debug.Log("[Adventure] Local event selected through ProgressManager.", eventConfig);
            else Debug.LogError("[Adventure] Local event was not selected; Play was not started.", eventConfig);
            return selected;
        }

        GameInitializer initializer = Object.FindFirstObjectByType<GameInitializer>();
        if (initializer == null)
        {
            Debug.LogError("[Adventure] Open MainMenu to set the local event source.", eventConfig);
            return false;
        }
        Undo.RecordObject(initializer, "Select Local Adventure Event");
        initializer.SetDefaultLocalEvent(eventConfig);
        EditorUtility.SetDirty(initializer);
        Debug.Log("[Adventure] Local event will be selected through ProgressManager when MainMenu starts.", eventConfig);
        return true;
    }

    private static void PlayLocalEvent(AdventureEventConfig eventConfig)
    {
        if (!SelectForMainMenu(eventConfig)) return;
        if (EditorApplication.isPlaying)
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadAdventureMap();
            return;
        }
        SessionState.SetBool("ARXON.PlayLocalEvent", true);
        EditorApplication.isPlaying = true;
    }

    private static void DrawActiveLocalEventStatus(AdventureEventConfig inspectedEvent)
    {
        AdventureEventConfig active = null;
        ProgressManager manager = Object.FindFirstObjectByType<ProgressManager>();
        if (manager != null) active = manager.SelectedLocalEvent;
        if (active == null)
        {
            GameInitializer initializer = Object.FindFirstObjectByType<GameInitializer>();
            if (initializer != null) active = initializer.DefaultLocalEvent;
        }
        string message = active == null
            ? "Aktif yerel paket: yok. 'Use This Local Event' MainMenu başlangıcı için bu paketi seçer."
            : $"Aktif yerel paket: {active.eventName} ({active.eventId})" + (active == inspectedEvent ? " — bu Inspector asset'i." : string.Empty);
        EditorGUILayout.HelpBox(message, active == inspectedEvent ? MessageType.Info : MessageType.None);
    }

    private void DrawValidation()
    {
        if (validation == null) return;
        foreach (string error in validation.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        foreach (string warning in validation.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        foreach (string note in validation.RiskNotes) EditorGUILayout.HelpBox(note, MessageType.Info);
        if (!validation.HasErrors && validation.Warnings.Count == 0) EditorGUILayout.HelpBox("Package validation passed.", MessageType.Info);
    }

    private static void DrawBulkSummary(AdventureEventConfig eventConfig)
    {
        if (!eventConfig.HasUsableLevelList) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bulk Review", EditorStyles.boldLabel);
        for (int i = 0; i < eventConfig.levelConfigs.Length; i++)
        {
            AdventureLevelConfig level = eventConfig.levelConfigs[i];
            if (level == null) { EditorGUILayout.HelpBox((i + 1) + ": missing", MessageType.Error); continue; }
            AdventureStageRecipe r = level.directRecipe;
            string objectives = level.objectives == null ? "none" : string.Join(", ", level.objectives.ConvertAll(o => o == null ? "?" : o.target + " " + o.requiredAmount));
            EditorGUILayout.LabelField($"{level.GetStageNumber():00} | {objectives} | R {r.rock.chance:P0} C {r.chain.chance:P0} I {r.ice.chance:P0} | F/S {(r.fire.chance + r.slice.chance):P0} | Fog {r.fogDensity} | Moves {(r.hasMoveLimit ? r.moveLimit.ToString() : "off")} | {level.generatedDifficultyLabel} | {level.generatedRhythmRole}{(level.protectFromRegeneration ? " | protected" : "")}", EditorStyles.miniLabel);
        }
    }
}
