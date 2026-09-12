using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AdventureEventLevelFactoryTests
{
    [Test]
    public void NewEventCreationBuildsAValidatedFiftyStageDirectPackageWithoutSelectingIt()
    {
        const string root = "Assets/AdventureEvents/__NewEventCreationTests";
        AssetDatabase.DeleteAsset(root);
        AssetDatabase.CreateFolder("Assets/AdventureEvents", "__NewEventCreationTests");
        CollectibleDatabase database = CreatePersistentDatabase(root + "/Collectibles.asset");
        AdventureEventCreationRequest request = new AdventureEventCreationRequest
        {
            eventName = "Tool Test Event",
            parentFolder = root,
            packageFolderName = "Package",
            collectibleDatabase = database,
            seed = 12573,
            targetObstaclePerRowLimit = 2,
            targetObstacleActiveBoardLimit = 2
        };

        try
        {
            Assert.That(AdventureEventPackageEditorUtility.CreateNewEventPackage(request, out AdventureEventConfig created, out string message), Is.True, message);
            Assert.That(created, Is.Not.Null);
            Assert.That(created.levelConfigs, Has.Length.EqualTo(50));
            Assert.That(created.generationSettings.seed, Is.EqualTo(12573));
            HashSet<string> ids = new HashSet<string>();
            foreach (AdventureLevelConfig level in created.levelConfigs)
            {
                Assert.That(level.authoringMode, Is.EqualTo(AdventureStageAuthoringMode.DirectRecipeAuthoring));
                Assert.That(ids.Add(level.levelId), Is.True);
                Assert.That(level.eventId, Is.EqualTo(created.eventId));
                Assert.That(level.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(2));
                Assert.That(level.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(2));
            }
            Assert.That(AdventureEventPackageEditorUtility.ValidateEvent(created).HasErrors, Is.False);
            Assert.That(AdventureEventPackageEditorUtility.CreateNewEventPackage(request, out _, out string duplicateMessage), Is.False);
            Assert.That(duplicateMessage, Does.Contain("zaten var"));
        }
        finally
        {
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.SaveAssets();
        }
    }

    [Test]
    public void NewEventCreationRejectsMissingEligibleCollectiblesBeforeWriting()
    {
        AdventureEventCreationRequest request = new AdventureEventCreationRequest
        {
            eventName = "Invalid Tool Event",
            parentFolder = "Assets/AdventureEvents",
            packageFolderName = "__InvalidToolEvent",
            collectibleDatabase = ScriptableObject.CreateInstance<CollectibleDatabase>()
        };
        try
        {
            Assert.That(AdventureEventPackageEditorUtility.TryValidateNewEventRequest(request, out _, out _, out string message), Is.False);
            Assert.That(message, Does.Contain("availableInGenerator"));
            Assert.That(AssetDatabase.IsValidFolder("Assets/AdventureEvents/__InvalidToolEvent"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(request.collectibleDatabase);
        }
    }

    [Test]
    public void SameInputsBuildTheSameFiftyDirectRecipesAndIdentities()
    {
        AdventureEventConfig firstEvent = CreateEvent();
        AdventureEventConfig secondEvent = CreateEvent();
        AdventureEventGenerationPlan first = AdventureEventLevelFactory.BuildPlan(firstEvent);
        AdventureEventGenerationPlan second = AdventureEventLevelFactory.BuildPlan(secondEvent);

        Assert.That(first.IsValid, Is.True, string.Join("\n", first.Errors));
        Assert.That(second.IsValid, Is.True, string.Join("\n", second.Errors));
        Assert.AreEqual(50, first.Levels.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.AreEqual(i + 1, first.Levels[i].Config.GetStageNumber());
            Assert.AreEqual("local-test-stage-" + (i + 1).ToString("000"), first.Levels[i].LevelId);
            Assert.AreEqual(AdventureEventLevelFactory.GetRecipeSignature(first.Levels[i].Config), AdventureEventLevelFactory.GetRecipeSignature(second.Levels[i].Config));
            Assert.That(first.Levels[i].Config.authoringMode, Is.EqualTo(AdventureStageAuthoringMode.DirectRecipeAuthoring));
            Assert.That(first.Levels[i].Config.directRecipe.hasMoveLimit, Is.False);
            Assert.That(first.Levels[i].Config.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(2));
            Assert.That(first.Levels[i].Config.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(2));
        }
        DestroyPlan(first); DestroyPlan(second); DestroyEvent(firstEvent); DestroyEvent(secondEvent);
    }

    [Test]
    public void ThresholdAndFollowingRelaxationHaveDifferentPlannedRhythm()
    {
        AdventureEventConfig eventConfig = CreateEvent();
        AdventureEventGenerationPlan plan = AdventureEventLevelFactory.BuildPlan(eventConfig);
        Assert.AreEqual("Eşik", plan.Levels[9].RhythmRole);
        Assert.AreEqual("Rahatlama / alışma", plan.Levels[10].RhythmRole);
        Assert.Greater(plan.Levels[10].Config.objectives[0].requiredAmount, plan.Levels[0].Config.objectives[0].requiredAmount);
        DestroyPlan(plan); DestroyEvent(eventConfig);
    }

    [Test]
    public void EligiblePoolIsStableAndExistingRecipesDoNotChangeWhenPoolGrows()
    {
        AdventureEventConfig eventConfig = CreateEvent();
        AdventureEventGenerationPlan initial = AdventureEventLevelFactory.BuildPlan(eventConfig);
        string before = AdventureEventLevelFactory.GetRecipeSignature(initial.Levels[2].Config);
        eventConfig.collectibleDatabase.collectibles.Add(new CollectibleDefinition { id = "ZZ_NEW", icon = CreateIcon() });
        Assert.AreEqual(before, AdventureEventLevelFactory.GetRecipeSignature(initial.Levels[2].Config));
        DestroyPlan(initial); DestroyEvent(eventConfig);
    }

    [Test]
    public void SelectedRegenerationPreservesIdentityAndProtectionStopsIt()
    {
        AdventureEventConfig eventConfig = CreateEvent();
        AdventureEventGenerationPlan plan = AdventureEventLevelFactory.BuildPlan(eventConfig);
        AdventureLevelConfig target = plan.Levels[17].Config;
        string stableId = target.levelId;
        target.designerNotes = "Manual note that regeneration replaces only when explicitly requested.";

        Assert.That(AdventureEventLevelFactory.RegenerateSelected(eventConfig, target, out string error), Is.True, error);
        Assert.AreEqual(stableId, target.levelId);
        target.protectFromRegeneration = true;
        Assert.That(AdventureEventLevelFactory.RegenerateSelected(eventConfig, target, out error), Is.False);
        Assert.That(error, Does.Contain("protected"));
        DestroyPlan(plan); DestroyEvent(eventConfig);
    }

    [Test]
    public void HistoricalPackageCannotBeAccidentallyRegeneratedWithTheNewRecipe()
    {
        AdventureEventConfig eventConfig = CreateEvent();
        AdventureEventGenerationPlan plan = AdventureEventLevelFactory.BuildPlan(eventConfig);
        eventConfig.generationSettings.generatorVersion = "local-direct-50-v1";

        Assert.That(AdventureEventLevelFactory.RegenerateSelected(eventConfig, plan.Levels[0].Config, out string error), Is.False);
        Assert.That(error, Does.Contain("historical generator version"));

        eventConfig.generationSettings.generatorVersion = "local-direct-50-v3-opening-pressure";
        Assert.That(AdventureEventLevelFactory.RegenerateSelected(eventConfig, plan.Levels[0].Config, out error), Is.False);
        Assert.That(error, Does.Contain("historical generator version"));

        DestroyPlan(plan); DestroyEvent(eventConfig);
    }

    [Test]
    public void ObstacleObjectivesHavePositiveMatchingSpawnAndSupportStaysAtOrBelowTenPercent()
    {
        AdventureEventConfig eventConfig = CreateEvent();
        AdventureEventGenerationPlan plan = AdventureEventLevelFactory.BuildPlan(eventConfig);
        foreach (AdventureEventLevelSpec spec in plan.Levels)
        {
            AdventureLevelConfig config = spec.Config;
            foreach (AdventureObjectiveDefinition objective in config.objectives)
            {
                if (objective.target == AdventureObjectiveTarget.Ice) Assert.Greater(config.directRecipe.ice.chance, 0f);
                if (objective.target == AdventureObjectiveTarget.Rock) Assert.Greater(config.directRecipe.rock.chance, 0f);
                if (objective.target == AdventureObjectiveTarget.Chain) Assert.Greater(config.directRecipe.chain.chance, 0f);
            }
            Assert.LessOrEqual(config.directRecipe.fire.chance + config.directRecipe.slice.chance, .10f);
        }
        DestroyPlan(plan); DestroyEvent(eventConfig);
    }

    [Test]
    public void SeededStageSequenceKeepsEarlyConstraintsAndSupportFrequencies()
    {
        AdventureEventConfig eventConfig = CreateEvent();
        AdventureEventGenerationPlan plan = AdventureEventLevelFactory.BuildPlan(eventConfig);

        Assert.That(plan.Levels[0].Config.objectives, Has.Count.EqualTo(1));
        HashSet<AdventureObjectiveTarget> firstThreeTargets = new HashSet<AdventureObjectiveTarget>();
        for (int i = 0; i < 3; i++)
        {
            AdventureObjectiveDefinition objective = plan.Levels[i].Config.objectives[0];
            Assert.That(firstThreeTargets.Add(objective.target), Is.True);
            Assert.That(objective.target == AdventureObjectiveTarget.Rows || objective.target == AdventureObjectiveTarget.Score || objective.target == AdventureObjectiveTarget.Collectible, Is.True);
            Assert.That(plan.Levels[i].Config.directRecipe.fogDensity, Is.EqualTo(FogDensity.None));
        }
        Assert.That(plan.Levels[0].Config.objectives[0].requiredAmount == 8 || plan.Levels[0].Config.objectives[0].requiredAmount == 1200 || plan.Levels[0].Config.objectives[0].requiredAmount == 5, Is.True);
        for (int group = 0; group < 5; group++)
        {
            AdventureLevelConfig threshold = plan.Levels[group * 10 + 9].Config;
            Assert.That(threshold.generatedRhythmRole, Is.EqualTo("Eşik"));
            Assert.That(threshold.objectives, Has.Count.EqualTo(3));
            int fireCount = 0;
            int sliceCount = 0;
            int fogCount = 0;
            for (int phase = 4; phase <= 9; phase++)
            {
                AdventureStageRecipe recipe = plan.Levels[group * 10 + phase - 1].Config.directRecipe;
                if (recipe.fire.enabled) fireCount++;
                if (recipe.slice.enabled) sliceCount++;
                if (recipe.fogDensity != FogDensity.None) fogCount++;
            }
            Assert.That(fireCount, Is.EqualTo(group == 0 ? 1 : 2));
            Assert.That(sliceCount, Is.EqualTo(1));
            Assert.That(fogCount, Is.EqualTo(group >= 2 ? 2 : 1));
        }

        DestroyPlan(plan); DestroyEvent(eventConfig);
    }

    [Test]
    public void OneHundredSeedsProduceValidDeterministicAndVariedStageSequences()
    {
        AdventureEventConfig eventConfig = CreateEvent();
        HashSet<string> firstStagePatterns = new HashSet<string>();
        HashSet<string> firstTenPatterns = new HashSet<string>();
        HashSet<string> featurePatterns = new HashSet<string>();
        try
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                eventConfig.generationSettings.seed = seed;
                AdventureEventGenerationPlan first = AdventureEventLevelFactory.BuildPlan(eventConfig);
                AdventureEventGenerationPlan second = AdventureEventLevelFactory.BuildPlan(eventConfig);
                try
                {
                    Assert.That(first.IsValid, Is.True, "seed=" + seed + "\n" + string.Join("\n", first.Errors));
                    Assert.That(second.IsValid, Is.True, "seed=" + seed + "\n" + string.Join("\n", second.Errors));
                    Assert.That(SequenceSignature(first, 50), Is.EqualTo(SequenceSignature(second, 50)), "seed=" + seed);

                    eventConfig.levelConfigs = first.Levels.ConvertAll(spec => spec.Config).ToArray();
                    AdventureContentValidationResult validation = AdventureEventPackageEditorUtility.ValidateEvent(eventConfig);
                    Assert.That(validation.HasErrors, Is.False, "seed=" + seed + "\n" + string.Join("\n", validation.Errors));

                    firstStagePatterns.Add(ObjectivePattern(first.Levels[0].Config));
                    firstTenPatterns.Add(SequenceSignature(first, 10));
                    featurePatterns.Add(FeatureSignature(first));
                    for (int stage = 1; stage < first.Levels.Count; stage++)
                        Assert.That(ObjectivePattern(first.Levels[stage].Config), Is.Not.EqualTo(ObjectivePattern(first.Levels[stage - 1].Config)), "seed=" + seed + ", stage=" + (stage + 1));
                    Assert.That(first.Levels[0].Config.objectives, Has.Count.EqualTo(1));
                    Assert.That(first.Levels[0].Config.directRecipe.fogDensity, Is.EqualTo(FogDensity.None));
                    Assert.That(first.Levels[0].Config.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(2));
                    Assert.That(first.Levels[0].Config.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(2));
                }
                finally
                {
                    eventConfig.levelConfigs = null;
                    DestroyPlan(first);
                    DestroyPlan(second);
                }
            }
            Assert.That(firstStagePatterns, Does.Contain("Rows"));
            Assert.That(firstStagePatterns, Does.Contain("Score"));
            Assert.That(firstStagePatterns, Does.Contain("Collectible"));
            Assert.That(firstTenPatterns.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(featurePatterns.Count, Is.GreaterThanOrEqualTo(5));
        }
        finally
        {
            DestroyEvent(eventConfig);
        }
    }

    [Test]
    public void LocalTrial03FogPressureRisesAboveTheComparisonPackageAtLateFogStages()
    {
        const string tuningPath = "Assets/AdventureEvents/LocalTrial_03_Tuning/ARXON_LocalTrial_03_Tuning.asset";
        const string comparisonPath = "Assets/AdventureEvents/LocalTrial_02_Comparison/ARXON_LocalTrial_02_Comparison.asset";
        AdventureEventConfig tuning = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(tuningPath);
        AdventureEventConfig comparison = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(comparisonPath);

        Assert.That(tuning, Is.Not.Null);
        Assert.That(comparison, Is.Not.Null);
        Assert.That(tuning.eventId, Is.EqualTo("local-trial-03-tuning"));

        int[] lateFogStages = { 25, 28, 35, 38, 45, 48 };
        foreach (int stage in lateFogStages)
        {
            AdventureStageRecipe tuningRecipe = tuning.levelConfigs[stage - 1].directRecipe;
            AdventureStageRecipe comparisonRecipe = comparison.levelConfigs[stage - 1].directRecipe;
            Assert.That(tuningRecipe.fogDensity, Is.EqualTo(FogDensity.Light), "Stage " + stage);
            Assert.That(tuningRecipe.fogCoveragePercent, Is.GreaterThan(comparisonRecipe.fogCoveragePercent), "Stage " + stage);
        }

        Assert.That(tuning.levelConfigs[7].directRecipe.fogCoveragePercent, Is.EqualTo(.18f));
        Assert.That(tuning.levelConfigs[17].directRecipe.fogCoveragePercent, Is.EqualTo(.20f));
    }

    [Test]
    public void ExistingPackagesKeepTheirSerializedTargetObstacleSupplySettings()
    {
        AdventureEventConfig tuning = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(
            "Assets/AdventureEvents/LocalTrial_03_Tuning/ARXON_LocalTrial_03_Tuning.asset");
        AdventureEventConfig comparison = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(
            "Assets/AdventureEvents/LocalTrial_02_Comparison/ARXON_LocalTrial_02_Comparison.asset");

        Assert.That(tuning, Is.Not.Null);
        Assert.That(comparison, Is.Not.Null);
        foreach (AdventureLevelConfig level in tuning.levelConfigs)
        {
            Assert.That(level.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(2));
            Assert.That(level.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(2));
        }
        foreach (AdventureLevelConfig level in comparison.levelConfigs)
        {
            Assert.That(level.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(0));
            Assert.That(level.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(0));
        }
    }

    [Test]
    public void GeneratedTargetObstacleSupplyDefaultsApplyToAnyEventIdAndRespectExplicitGenerationSettings()
    {
        AdventureEventConfig firstEvent = CreateEvent();
        AdventureEventConfig secondEvent = CreateEvent();
        secondEvent.eventId = "different-event-id";
        AdventureEventConfig customEvent = CreateEvent();
        customEvent.eventId = "explicit-supply-choice";
        customEvent.generationSettings.defaultTargetObstaclePerRowLimit = 1;
        customEvent.generationSettings.defaultTargetObstacleActiveBoardLimit = 3;
        AdventureEventGenerationPlan firstPlan = AdventureEventLevelFactory.BuildPlan(firstEvent);
        AdventureEventGenerationPlan secondPlan = AdventureEventLevelFactory.BuildPlan(secondEvent);
        AdventureEventGenerationPlan customPlan = AdventureEventLevelFactory.BuildPlan(customEvent);

        Assert.That(firstPlan.IsValid, Is.True, string.Join("\n", firstPlan.Errors));
        Assert.That(secondPlan.IsValid, Is.True, string.Join("\n", secondPlan.Errors));
        Assert.That(customPlan.IsValid, Is.True, string.Join("\n", customPlan.Errors));
        foreach (AdventureEventLevelSpec spec in firstPlan.Levels)
        {
            Assert.That(spec.Config.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(2));
            Assert.That(spec.Config.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(2));
        }
        foreach (AdventureEventLevelSpec spec in secondPlan.Levels)
        {
            Assert.That(spec.Config.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(2));
            Assert.That(spec.Config.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(2));
        }
        foreach (AdventureEventLevelSpec spec in customPlan.Levels)
        {
            Assert.That(spec.Config.directRecipe.targetObstaclePerRowLimit, Is.EqualTo(1));
            Assert.That(spec.Config.directRecipe.targetObstacleActiveBoardLimit, Is.EqualTo(3));
        }

        DestroyPlan(firstPlan); DestroyPlan(secondPlan); DestroyPlan(customPlan);
        DestroyEvent(firstEvent); DestroyEvent(secondEvent); DestroyEvent(customEvent);
    }

    [Test]
    public void ScorePresentationOnlyAppearsForClassicOrReachScoreObjectives()
    {
        List<ObjectiveRuntimeState> rowOnly = new List<ObjectiveRuntimeState>
        {
            new ObjectiveRuntimeState { definition = new AdventureObjectiveDefinition { action = AdventureObjectiveAction.ClearRows } }
        };
        List<ObjectiveRuntimeState> scoreGoal = new List<ObjectiveRuntimeState>
        {
            new ObjectiveRuntimeState { definition = new AdventureObjectiveDefinition { action = AdventureObjectiveAction.ReachScore } }
        };

        Assert.That(AdventureScorePresentation.HasReachScoreObjective(rowOnly), Is.False);
        Assert.That(AdventureScorePresentation.HasReachScoreObjective(scoreGoal), Is.True);
    }

    [Test]
    public void LocalTrial02ComparisonPackageIsASeparateValidFiftyStageEvent()
    {
        const string eventPath = "Assets/AdventureEvents/LocalTrial_02_Comparison/ARXON_LocalTrial_02_Comparison.asset";
        AdventureEventConfig comparison = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(eventPath);

        Assert.That(comparison, Is.Not.Null);
        Assert.That(comparison.eventId, Is.EqualTo("local-trial-02-comparison"));
        Assert.That(comparison.levelConfigs, Has.Length.EqualTo(50));
        Assert.That(AssetDatabase.LoadAssetAtPath<AdventureLevelConfig>("Assets/AdventureEvents/LocalTrial_01/Levels/L001.asset").eventId,
            Is.EqualTo("local-trial-01"));

        AdventureContentValidationResult validation = AdventureEventPackageEditorUtility.ValidateEvent(comparison);
        Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Errors));
    }

    [Test]
    public void LocalSourceSelectionNextAndProgressStayInsideTheSelectedEvent()
    {
        string progress = PlayerPrefs.GetString("AdventureEventProgressV1", null);
        int migration = PlayerPrefs.GetInt("AdventureEventProgressMigratedV1", 0);
        int legacy = PlayerPrefs.GetInt("HighestLevelUnlocked", 0);
        AdventureEventConfig firstEvent = CreateEvent();
        AdventureEventConfig secondEvent = CreateEvent(); secondEvent.eventId = "second-local-test";
        AdventureEventGenerationPlan firstPlan = AdventureEventLevelFactory.BuildPlan(firstEvent);
        AdventureEventGenerationPlan secondPlan = AdventureEventLevelFactory.BuildPlan(secondEvent);
        firstEvent.levelConfigs = firstPlan.Levels.ConvertAll(x => x.Config).ToArray();
        secondEvent.levelConfigs = secondPlan.Levels.ConvertAll(x => x.Config).ToArray();
        GameObject managerObject = new GameObject("LocalEventProgressTest");
        ProgressManager manager = managerObject.AddComponent<ProgressManager>();
        try
        {
            Assert.That(manager.TrySelectLocalEvent(firstEvent), Is.True);
            Assert.That(manager.TrySelectAdventureLevel(1), Is.True);
            AdventureAttemptSnapshot firstAttempt = manager.CurrentAdventureAttempt;
            manager.CompleteAdventureAttempt(firstAttempt);
            Assert.AreEqual(2, manager.GetHighestUnlockedForEvent(firstEvent.eventId));
            Assert.That(manager.TryStartNextAdventureAttempt(firstAttempt), Is.True);
            Assert.AreEqual("local-test-stage-002", manager.CurrentAdventureAttempt.Identity.LevelId);
            Assert.That(manager.TrySelectLocalEvent(secondEvent), Is.False, "An open snapshot must not silently switch source packages.");
            manager.ClearAdventureSelection();
            Assert.That(manager.TrySelectLocalEvent(secondEvent), Is.True);
            Assert.AreEqual(1, manager.GetHighestUnlockedForEvent(secondEvent.eventId));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            if (progress == null) PlayerPrefs.DeleteKey("AdventureEventProgressV1"); else PlayerPrefs.SetString("AdventureEventProgressV1", progress);
            if (migration == 0) PlayerPrefs.DeleteKey("AdventureEventProgressMigratedV1"); else PlayerPrefs.SetInt("AdventureEventProgressMigratedV1", migration);
            if (legacy == 0) PlayerPrefs.DeleteKey("HighestLevelUnlocked"); else PlayerPrefs.SetInt("HighestLevelUnlocked", legacy);
            PlayerPrefs.Save();
            DestroyPlan(firstPlan); DestroyPlan(secondPlan); DestroyEvent(firstEvent); DestroyEvent(secondEvent);
        }
    }

    private static AdventureEventConfig CreateEvent()
    {
        CollectibleDatabase database = ScriptableObject.CreateInstance<CollectibleDatabase>();
        database.collectibles = new List<CollectibleDefinition>
        {
            new CollectibleDefinition { id = "CR_A", displayName = "A", category = CollectibleCategory.Crystal, icon = CreateIcon(), availableInGenerator = true },
            new CollectibleDefinition { id = "GM_B", displayName = "B", category = CollectibleCategory.Gem, icon = CreateIcon(), availableInGenerator = true }
        };
        AdventureEventConfig eventConfig = ScriptableObject.CreateInstance<AdventureEventConfig>();
        eventConfig.eventId = "local-test"; eventConfig.contentVersion = "1"; eventConfig.eventName = "Local Test"; eventConfig.collectibleDatabase = database;
        eventConfig.generationSettings = new AdventureEventGenerationSettings { seed = 45123, defaultLevelCount = 50, generatorVersion = AdventureEventLevelFactory.CurrentGeneratorVersion };
        return eventConfig;
    }

    private static CollectibleDatabase CreatePersistentDatabase(string path)
    {
        CollectibleDatabase database = ScriptableObject.CreateInstance<CollectibleDatabase>();
        database.collectibles = new List<CollectibleDefinition>
        {
            new CollectibleDefinition { id = "TOOL_A", displayName = "Tool A", icon = CreateIcon(), availableInGenerator = true }
        };
        AssetDatabase.CreateAsset(database, path);
        return database;
    }

    private static Sprite CreateIcon()
    {
        Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
    }

    private static string SequenceSignature(AdventureEventGenerationPlan plan, int count)
    {
        List<string> entries = new List<string>();
        for (int i = 0; i < count; i++) entries.Add(ObjectivePattern(plan.Levels[i].Config));
        return string.Join("|", entries);
    }

    private static string ObjectivePattern(AdventureLevelConfig config)
    {
        List<string> targets = new List<string>();
        foreach (AdventureObjectiveDefinition objective in config.objectives) targets.Add(objective.target.ToString());
        return string.Join("+", targets);
    }

    private static string FeatureSignature(AdventureEventGenerationPlan plan)
    {
        List<string> entries = new List<string>();
        for (int i = 0; i < plan.Levels.Count; i++)
        {
            AdventureStageRecipe recipe = plan.Levels[i].Config.directRecipe;
            entries.Add((recipe.fire.enabled ? "F" : "-") + (recipe.slice.enabled ? "S" : "-") + (recipe.fogDensity != FogDensity.None ? "G" : "-"));
        }
        return string.Join("|", entries);
    }

    private static void DestroyPlan(AdventureEventGenerationPlan plan)
    {
        foreach (AdventureEventLevelSpec spec in plan.Levels) Object.DestroyImmediate(spec.Config);
    }

    private static void DestroyEvent(AdventureEventConfig eventConfig)
    {
        foreach (CollectibleDefinition collectible in eventConfig.collectibleDatabase.collectibles)
        {
            if (collectible.icon != null) Object.DestroyImmediate(collectible.icon.texture);
            if (collectible.icon != null) Object.DestroyImmediate(collectible.icon);
        }
        Object.DestroyImmediate(eventConfig.collectibleDatabase);
        Object.DestroyImmediate(eventConfig);
    }
}
