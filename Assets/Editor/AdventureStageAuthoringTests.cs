using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AdventureStageAuthoringTests
{
    private static readonly Color[] Palette = { Color.red, Color.blue };

    [Test]
    public void LegacyResolutionRemainsStableBeforeMigration()
    {
        AdventureLevelConfig config = CreateLegacyConfig();
        LevelData first = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        LevelData second = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        AssertResolvedRecipeEqual(first, second);
        Assert.That(config.authoringMode, Is.EqualTo(AdventureStageAuthoringMode.LegacyMacroAuthoring));
    }

    [Test]
    public void BakingLegacyConfigPreservesResolvedGameplayRecipe()
    {
        AdventureLevelConfig config = CreateLegacyConfig();
        LevelData legacy = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out string error), Is.True, error);
        LevelData direct = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(config.authoringMode, Is.EqualTo(AdventureStageAuthoringMode.DirectRecipeAuthoring));
        AssertResolvedRecipeEqual(legacy, direct);
        AssertRowsEqual(
            AdventureRowGenerator.GenerateRowData(new AdventureRowGenerationContext(8, legacy, Palette), new AdventureGameplayRng(91)),
            AdventureRowGenerator.GenerateRowData(new AdventureRowGenerationContext(8, direct, Palette), new AdventureGameplayRng(91)));
    }

    [Test]
    public void BakingInactiveLegacyFireFocusDoesNotEnableFireGeneration()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.authoringMode = AdventureStageAuthoringMode.LegacyMacroAuthoring;
        config.specialMechanicFocus = SpecialMechanicFocus.Fire;
        config.objectives.Add(ClearRowsObjective(3));
        LevelData legacy = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out string error), Is.True, error);
        LevelData direct = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext legacyContext = new AdventureRowGenerationContext(8, legacy, Palette);
        AdventureRowGenerationContext directContext = new AdventureRowGenerationContext(8, direct, Palette);

        Assert.AreEqual(0f, legacyContext.FireChance);
        Assert.AreEqual(0f, directContext.FireChance);
        AssertRowsEqual(
            AdventureRowGenerator.GenerateRowData(legacyContext, new AdventureGameplayRng(22)),
            AdventureRowGenerator.GenerateRowData(directContext, new AdventureGameplayRng(22)));
    }

    [Test]
    public void BakingInactiveLegacySliceFocusDoesNotEnableSliceGeneration()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.authoringMode = AdventureStageAuthoringMode.LegacyMacroAuthoring;
        config.specialMechanicFocus = SpecialMechanicFocus.Slice;
        config.objectives.Add(ClearRowsObjective(3));
        LevelData legacy = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out string error), Is.True, error);
        LevelData direct = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext legacyContext = new AdventureRowGenerationContext(8, legacy, Palette);
        AdventureRowGenerationContext directContext = new AdventureRowGenerationContext(8, direct, Palette);

        Assert.AreEqual(0f, legacyContext.SliceChance);
        Assert.AreEqual(0f, directContext.SliceChance);
        AssertRowsEqual(
            AdventureRowGenerator.GenerateRowData(legacyContext, new AdventureGameplayRng(23)),
            AdventureRowGenerator.GenerateRowData(directContext, new AdventureGameplayRng(23)));
    }

    [Test]
    public void BakingAppendsOneLegacySummaryWithoutReplacingExistingDesignerNotes()
    {
        AdventureLevelConfig config = CreateLegacyConfig();
        config.designerNotes = "Keep this design note.";

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out string error), Is.True, error);
        string notesAfterFirstBake = config.designerNotes;
        Assert.That(notesAfterFirstBake, Does.StartWith("Keep this design note."));
        Assert.That(notesAfterFirstBake, Does.Contain("Legacy Source:\nDifficulty=Hard\nPressure=Chaos\nObstacle=Chain\nFocus=LargeBlocks"));
        Assert.That(notesAfterFirstBake, Does.Contain("MoveOffset=2"));
        Assert.That(notesAfterFirstBake, Does.Contain("PressureOffset=1"));
        Assert.That(notesAfterFirstBake, Does.Contain("HandcraftedOverrides=Enabled"));

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out error), Is.True, error);
        Assert.AreEqual(notesAfterFirstBake, config.designerNotes);
    }

    [Test]
    public void BakeRevertBakePreservesLegacyResolutionDirectRecipeAndDesignerNotes()
    {
        AdventureLevelConfig config = CreateLegacyConfig();
        config.designerNotes = "Keep this design note.";
        LevelData legacy = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out string error), Is.True, error);
        LevelData firstBaked = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        string directRecipeBeforeRevert = JsonUtility.ToJson(config.directRecipe);
        string notesBeforeRevert = config.designerNotes;

        Assert.That(AdventureLevelGenerator.RevertDirectRecipeAuthoringToLegacyMacroAuthoring(config, out error), Is.True, error);
        Assert.That(config.authoringMode, Is.EqualTo(AdventureStageAuthoringMode.LegacyMacroAuthoring));
        Assert.AreEqual(directRecipeBeforeRevert, JsonUtility.ToJson(config.directRecipe));
        Assert.AreEqual(notesBeforeRevert, config.designerNotes);
        AssertResolvedRecipeEqual(legacy, AdventureLevelGenerator.GenerateRuntimeLevel(config));

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out error), Is.True, error);
        AssertResolvedRecipeEqual(firstBaked, AdventureLevelGenerator.GenerateRuntimeLevel(config));
        Assert.AreEqual(notesBeforeRevert, config.designerNotes);
    }

    [Test]
    public void RevertingS04StyleLegacyFireStagePreservesDisabledFireAndLegacyMacros()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.authoringMode = AdventureStageAuthoringMode.LegacyMacroAuthoring;
        config.difficulty = DifficultyTier.Hard;
        config.pressure = PressureType.ObstacleDense;
        config.obstacleTheme = ObstacleTheme.Rock;
        config.specialMechanicFocus = SpecialMechanicFocus.Fire;
        config.objectives.Add(ClearRowsObjective(3));

        LevelData legacy = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext legacyContext = new AdventureRowGenerationContext(8, legacy, Palette);
        Assert.AreEqual(0f, legacyContext.FireChance);

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out string error), Is.True, error);
        Assert.IsFalse(config.directRecipe.fire.enabled);

        Assert.That(AdventureLevelGenerator.RevertDirectRecipeAuthoringToLegacyMacroAuthoring(config, out error), Is.True, error);
        Assert.That(config.authoringMode, Is.EqualTo(AdventureStageAuthoringMode.LegacyMacroAuthoring));
        Assert.AreEqual(DifficultyTier.Hard, config.difficulty);
        Assert.AreEqual(PressureType.ObstacleDense, config.pressure);
        Assert.AreEqual(ObstacleTheme.Rock, config.obstacleTheme);
        Assert.AreEqual(SpecialMechanicFocus.Fire, config.specialMechanicFocus);
        Assert.AreEqual(0f, new AdventureRowGenerationContext(8, AdventureLevelGenerator.GenerateRuntimeLevel(config), Palette).FireChance);

        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out error), Is.True, error);
        Assert.IsFalse(config.directRecipe.fire.enabled);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void DirectRecipeIgnoresLegacyMacros()
    {
        AdventureLevelConfig config = CreateDirectConfig();
        LevelData first = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        config.difficulty = DifficultyTier.Expert;
        config.pressure = PressureType.Chaos;
        config.obstacleTheme = ObstacleTheme.Mixed;
        config.specialMechanicFocus = SpecialMechanicFocus.Fire;
        config.moveOffset = -8;
        config.pressureOffset = 3;
        LevelData second = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        AssertResolvedRecipeEqual(first, second);
        Assert.That(second.specialMechanicFocus, Is.EqualTo(SpecialMechanicFocus.None));
    }

    [TestCase(true, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public void DirectObstacleCombinationsUseIndependentEffectiveChances(bool rockEnabled, bool chainEnabled, bool iceEnabled)
    {
        AdventureLevelConfig config = CreateDirectConfig();
        config.directRecipe.rock.enabled = rockEnabled;
        config.directRecipe.rock.chance = .11f;
        config.directRecipe.chain.enabled = chainEnabled;
        config.directRecipe.chain.chance = .22f;
        config.directRecipe.ice.enabled = iceEnabled;
        config.directRecipe.ice.chance = .33f;

        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.AreEqual(rockEnabled ? .11f : 0f, level.rockBlockChance);
        Assert.AreEqual(chainEnabled ? .22f : 0f, level.chainedBlockChance);
        Assert.AreEqual(iceEnabled ? .33f : 0f, level.frozenBlockChance);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void DirectSupportSelectionsEnableCustomSpawnRules(bool fireEnabled, bool sliceEnabled)
    {
        AdventureLevelConfig config = CreateDirectConfig();
        config.directRecipe.fire.enabled = fireEnabled;
        config.directRecipe.fire.chance = .10f;
        config.directRecipe.slice.enabled = sliceEnabled;
        config.directRecipe.slice.chance = .20f;

        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext context = new AdventureRowGenerationContext(8, level, Palette);

        Assert.That(level.useCustomSpawnRules, Is.True);
        Assert.AreEqual(fireEnabled ? .10f : 0f, context.FireChance);
        Assert.AreEqual(sliceEnabled ? .20f : 0f, context.SliceChance);
    }

    [Test]
    public void DisabledDirectSpawnSettingsHaveZeroEffectiveChance()
    {
        AdventureLevelConfig config = CreateDirectConfig();
        config.directRecipe.rock.chance = .5f;
        config.directRecipe.fire.chance = .5f;
        config.directRecipe.slice.chance = .5f;

        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext context = new AdventureRowGenerationContext(8, level, Palette);

        Assert.AreEqual(0f, level.rockBlockChance);
        Assert.AreEqual(0f, context.FireChance);
        Assert.AreEqual(0f, context.SliceChance);
        Assert.That(level.useCustomSpawnRules, Is.False);
    }

    [Test]
    public void DirectStageRulesReachTheGenerationContext()
    {
        AdventureLevelConfig config = CreateDirectConfig();
        config.directRecipe.hasMoveLimit = true;
        config.directRecipe.moveLimit = 17;
        config.directRecipe.gapChance = .27f;
        config.directRecipe.minBlockWidth = 2;
        config.directRecipe.maxBlockWidth = 3;
        config.directRecipe.useCustomWidthRules = true;
        config.directRecipe.largeBlockChance = .41f;

        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext context = new AdventureRowGenerationContext(8, level, Palette);

        Assert.AreEqual(17, level.moveLimit);
        Assert.AreEqual(.27f, context.GapChance);
        Assert.AreEqual(2, context.MinBlockSize);
        Assert.AreEqual(3, context.MaxBlockSize);
        Assert.AreEqual(.41f, context.LargeBlockChance);
        Assert.That(context.UseCustomSpawnRules, Is.True);
    }

    [Test]
    public void DirectStageCanDisableMoveLimitWithoutUsingAPlaceholderValue()
    {
        AdventureLevelConfig config = CreateDirectConfig();
        config.directRecipe.hasMoveLimit = false;
        config.directRecipe.moveLimit = 0;

        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(level.hasMoveLimit, Is.False);
        Assert.AreEqual(0, level.moveLimit);
        Assert.That(level.ValidateRuntime(null, out string error), Is.True, error);
    }

    [TestCase(true, 0, true)]
    [TestCase(true, 1, false)]
    [TestCase(false, 0, false)]
    [TestCase(false, -3, false)]
    public void MoveLossPredicateOnlyAppliesWhenMoveLimitIsEnabled(bool hasMoveLimit, int remainingMoves, bool expected)
    {
        GameObject managerObject = new GameObject("LevelManagerMoveLimitTest");
        LevelManager manager = managerObject.AddComponent<LevelManager>();
        typeof(LevelManager).GetField("hasMoveLimit", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, hasMoveLimit);
        typeof(LevelManager).GetField("remainingMoves", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, remainingMoves);
        MethodInfo predicate = typeof(LevelManager).GetMethod("IsMoveLimitExhausted", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.AreEqual(expected, (bool)predicate.Invoke(manager, null));

        Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void DirectStagesRemainSampleableAndValidateEffectiveValues()
    {
        AdventureLevelConfig config = CreateDirectConfig();
        config.directRecipe.rock.enabled = true;
        config.directRecipe.rock.chance = .1f;
        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext context = new AdventureRowGenerationContext(8, level, Palette, 5);

        AdventureRowSamplingReport report = AdventureRowSampling.Sample(context, 1, 10, 3);
        AdventureContentValidationResult validation = AdventureContentValidator.ValidateLevel(level, null, context);

        Assert.AreEqual(30, report.TotalRows);
        Assert.That(validation.Errors, Is.Empty);
    }

    [Test]
    public void CombinedDirectFireAndSliceAboveTenPercentWarnsWithoutBlocking()
    {
        AdventureLevelConfig config = CreateDirectConfig();
        config.directRecipe.fire.enabled = true;
        config.directRecipe.fire.chance = .06f;
        config.directRecipe.slice.enabled = true;
        config.directRecipe.slice.chance = .06f;
        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        AdventureRowGenerationContext context = new AdventureRowGenerationContext(8, level, Palette);

        AdventureContentValidationResult validation = AdventureContentValidator.ValidateLevel(level, null, context);

        Assert.That(validation.Errors, Is.Empty);
        Assert.That(validation.Warnings, Has.Some.Contains("Combined authored Fire + Slice chance"));
        Assert.AreEqual(.06f, context.FireChance);
        Assert.AreEqual(.06f, context.SliceChance);
    }

    private static AdventureLevelConfig CreateLegacyConfig()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.authoringMode = AdventureStageAuthoringMode.LegacyMacroAuthoring;
        config.difficulty = DifficultyTier.Hard;
        config.pressure = PressureType.Chaos;
        config.obstacleTheme = ObstacleTheme.Chain;
        config.specialMechanicFocus = SpecialMechanicFocus.LargeBlocks;
        config.moveOffset = 2;
        config.pressureOffset = 1;
        config.objectives.Add(ClearRowsObjective(7));
        config.useHandcraftedOverrides = true;
        config.handcraftedOverrides.overrideCustomSpawnRules = true;
        config.handcraftedOverrides.useCustomSpawnRules = true;
        config.handcraftedOverrides.overrideFireChance = true;
        config.handcraftedOverrides.fireBlockChance = .12f;
        config.handcraftedOverrides.overrideSliceChance = true;
        config.handcraftedOverrides.sliceBlockChance = .08f;
        return config;
    }

    private static AdventureLevelConfig CreateDirectConfig()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.authoringMode = AdventureStageAuthoringMode.DirectRecipeAuthoring;
        config.objectives.Add(ClearRowsObjective(3));
        return config;
    }

    [Test]
    public void NewStageConfigsStartInDirectAuthoringMode()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        Assert.That(config.authoringMode, Is.EqualTo(AdventureStageAuthoringMode.DirectRecipeAuthoring));
        Assert.That(config.directRecipe.hasMoveLimit, Is.False);
    }

    [Test]
    public void ExistingDirectRecipeDefaultPreservesItsMoveConstraint()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.authoringMode = AdventureStageAuthoringMode.DirectRecipeAuthoring;
        config.directRecipe = new AdventureStageRecipe();
        config.objectives.Add(ClearRowsObjective(3));

        LevelData level = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(level.hasMoveLimit, Is.True);
        Assert.AreEqual(30, level.moveLimit);
    }

    [Test]
    public void BakingLimitedLegacyStageRetainsItsMoveConstraint()
    {
        AdventureLevelConfig config = CreateLegacyConfig();
        LevelData legacy = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.That(legacy.hasMoveLimit, Is.True);
        Assert.Greater(legacy.moveLimit, 0);
        Assert.That(AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe(config, out string error), Is.True, error);
        Assert.That(config.directRecipe.hasMoveLimit, Is.True);
        Assert.AreEqual(legacy.moveLimit, config.directRecipe.moveLimit);

        LevelData baked = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        Assert.That(baked.hasMoveLimit, Is.True);
        Assert.AreEqual(legacy.moveLimit, baked.moveLimit);
    }

    private static AdventureObjectiveDefinition ClearRowsObjective(int requiredAmount)
    {
        return new AdventureObjectiveDefinition
        {
            action = AdventureObjectiveAction.ClearRows,
            target = AdventureObjectiveTarget.Rows,
            requiredAmount = requiredAmount
        };
    }

    private static void AssertResolvedRecipeEqual(LevelData expected, LevelData actual)
    {
        Assert.AreEqual(expected.hasMoveLimit, actual.hasMoveLimit);
        Assert.AreEqual(expected.moveLimit, actual.moveLimit);
        Assert.AreEqual(expected.baseGapChance, actual.baseGapChance);
        Assert.AreEqual(expected.largeBlockChance, actual.largeBlockChance);
        Assert.AreEqual(expected.minBlockSize, actual.minBlockSize);
        Assert.AreEqual(expected.maxBlockSize, actual.maxBlockSize);
        Assert.AreEqual(expected.useCustomSpawnRules, actual.useCustomSpawnRules);
        Assert.AreEqual(expected.rockBlockChance, actual.rockBlockChance);
        Assert.AreEqual(expected.chainedBlockChance, actual.chainedBlockChance);
        Assert.AreEqual(expected.frozenBlockChance, actual.frozenBlockChance);
        Assert.AreEqual(expected.fireBlockChance, actual.fireBlockChance);
        Assert.AreEqual(expected.sliceBlockChance, actual.sliceBlockChance);
        Assert.AreEqual(expected.fogDensity, actual.fogDensity);
        Assert.AreEqual(expected.fogCoveragePercent, actual.fogCoveragePercent);
        Assert.AreEqual(expected.fogStartingRow, actual.fogStartingRow);
    }

    private static void AssertRowsEqual(List<GridManager.BlockData> expected, List<GridManager.BlockData> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].x, actual[i].x);
            Assert.AreEqual(expected[i].width, actual[i].width);
            Assert.AreEqual(expected[i].blockType, actual[i].blockType);
            Assert.AreEqual(expected[i].isRock, actual[i].isRock);
            Assert.AreEqual(expected[i].isChained, actual[i].isChained);
            Assert.AreEqual(expected[i].isFrozen, actual[i].isFrozen);
        }
    }
}
