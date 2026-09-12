using System.Collections.Generic;
using UnityEngine;

public static class AdventureLevelGenerator
{
    private const int UnsupportedObjectiveFallbackLines = 1;

    public static LevelData GenerateRuntimeLevel(AdventureLevelConfig config)
    {
        if (!ValidateConfig(config, out string error))
        {
            Debug.LogError($"[Adventure] {error}", config);
            return null;
        }

        LevelData runtimeLevel = ScriptableObject.CreateInstance<LevelData>();
        runtimeLevel.hideFlags = HideFlags.DontSave;
        runtimeLevel.name = string.IsNullOrWhiteSpace(config.displayName)
            ? $"GeneratedAdventureLevel_{config.GetStageNumber()}"
            : config.displayName;

        ApplyConfig(config, runtimeLevel);
        return runtimeLevel;
    }

    public static void ApplyConfig(AdventureLevelConfig config, LevelData target)
    {
        if (config == null || target == null)
            return;

        if (!ValidateConfig(config, out string error))
        {
            Debug.LogError($"[Adventure] {error}", config);
            return;
        }

        target.levelNumber = config.GetStageNumber();
        target.objectiveType = config.objective;
        target.specialMechanicFocus = config.authoringMode == AdventureStageAuthoringMode.LegacyMacroAuthoring
            ? config.specialMechanicFocus
            : SpecialMechanicFocus.None;
        target.targetObstacleCount = Mathf.Max(0, config.targetObstacleCount);
        target.targetComboCount = Mathf.Max(0, config.targetComboCount);
        target.isEndless = config.isEndless;
        target.hasMoveLimit = true;

        if (config.authoringMode == AdventureStageAuthoringMode.DirectRecipeAuthoring)
        {
            ApplyDirectRecipe(config.directRecipe, target);
            ApplyDirectObjectives(config, target);
        }
        else
        {
            LevelProfile profile = BuildBaseProfile(config.difficulty);
            ApplyPressure(ref profile, config.pressure, config.pressureOffset);
            ApplyObstacleTheme(ref profile, config.obstacleTheme);
            ApplySpecialFocus(ref profile, config.specialMechanicFocus);
            ApplyFogAuthoring(config, ref profile);
            ApplyObjective(config, target, ref profile);

            target.moveLimit = Mathf.Max(1, profile.moveLimit + config.moveOffset);
            target.baseGapChance = Mathf.Clamp01(profile.baseGapChance);
            target.largeBlockChance = Mathf.Clamp01(profile.largeBlockChance);
            target.frozenBlockChance = Mathf.Clamp01(profile.frozenBlockChance);
            target.rockBlockChance = Mathf.Clamp01(profile.rockBlockChance);
            target.chainedBlockChance = Mathf.Clamp01(profile.chainedBlockChance);
            target.useCustomSpawnRules = profile.useCustomSpawnRules;
            target.minBlockSize = Mathf.Clamp(profile.minBlockSize, 1, 4);
            target.maxBlockSize = Mathf.Clamp(Mathf.Max(target.minBlockSize, profile.maxBlockSize), 1, 4);
            target.sliceBlockChance = Mathf.Clamp01(profile.sliceBlockChance);
            target.fireBlockChance = Mathf.Clamp01(profile.fireBlockChance);
            target.fogDensity = profile.fogDensity;
            target.fogCoveragePercent = Mathf.Clamp01(profile.fogCoveragePercent);
            target.fogStartingRow = profile.fogStartingRow;

            ApplyOverrides(config, target);
        }
        target.CopyRuntimeObjectives(config.HasObjectiveV2() ? config.objectives : BuildLegacyObjectives(target));
        LogGeneratedLevel(config, target);
    }

    public static bool ValidateConfig(AdventureLevelConfig config, out string error)
    {
        error = null;
        if (config == null) error = "Adventure bölüm config'i bulunamadı.";
        else if (config.isEndless) error = "Adventure bölümü sonlu olmalı; isEndless desteklenmiyor.";
        else if (config.authoringMode == AdventureStageAuthoringMode.DirectRecipeAuthoring)
        {
            if (!config.HasObjectiveV2()) error = "Direct Stage authoring requires at least one V2 objective.";
            else if (config.objectives.Count > 3) error = "Adventure supports at most 3 objectives.";
            else if (!ValidateDirectRecipe(config.directRecipe, out error)) return false;
            else return LevelData.ValidateObjectives(config.objectives, out error);
        }
        else if (config.HasObjectiveV2()) return LevelData.ValidateObjectives(config.objectives, out error);
        else if (config.objective != ObjectiveType.ClearRows &&
                 config.objective != ObjectiveType.ReachScore &&
                 config.objective != ObjectiveType.DestroyObstacles)
            error = $"Legacy hedef {config.objective} desteklenmiyor; satır hedefine dönüştürülmeyecek.";
        return error == null;
    }

    public static bool BakeLegacyMacroAuthoringIntoDirectRecipe(AdventureLevelConfig config, out string error)
    {
        error = null;
        if (config == null)
        {
            error = "Adventure Stage config is missing.";
            return false;
        }

        if (config.authoringMode == AdventureStageAuthoringMode.DirectRecipeAuthoring)
            return ValidateConfig(config, out error);

        LevelData resolvedLegacyLevel = GenerateRuntimeLevel(config);
        if (resolvedLegacyLevel == null)
        {
            error = "Legacy Stage recipe could not be resolved.";
            return false;
        }

        if (config.directRecipe == null)
            config.directRecipe = new AdventureStageRecipe();

        config.directRecipe.CopyFrom(resolvedLegacyLevel);
        if (!config.HasObjectiveV2())
        {
            config.objectives = CopyObjectives(resolvedLegacyLevel.Objectives);
        }

        config.authoringMode = AdventureStageAuthoringMode.DirectRecipeAuthoring;
        AppendLegacySourceNote(config, resolvedLegacyLevel);
        return ValidateConfig(config, out error);
    }

    public static bool RevertDirectRecipeAuthoringToLegacyMacroAuthoring(AdventureLevelConfig config, out string error)
    {
        error = null;
        if (config == null)
        {
            error = "Adventure Stage config is missing.";
            return false;
        }

        config.authoringMode = AdventureStageAuthoringMode.LegacyMacroAuthoring;
        return true;
    }

    private static List<AdventureObjectiveDefinition> CopyObjectives(IReadOnlyList<AdventureObjectiveDefinition> source)
    {
        List<AdventureObjectiveDefinition> copy = new List<AdventureObjectiveDefinition>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            AdventureObjectiveDefinition objective = source[i];
            copy.Add(new AdventureObjectiveDefinition
            {
                action = objective.action,
                target = objective.target,
                requiredAmount = objective.requiredAmount,
                collectibleId = objective.collectibleId,
                displayLabel = objective.displayLabel,
                displayIcon = objective.displayIcon
            });
        }

        return copy;
    }

    private static bool ValidateDirectRecipe(AdventureStageRecipe recipe, out string error)
    {
        error = null;
        if (recipe == null) error = "Direct Stage recipe is missing.";
        else if ((recipe.hasMoveLimit && recipe.moveLimit < 1) || recipe.openingTargetObstacleLimit < 0 ||
                 recipe.targetObstaclePerRowLimit < 0 || recipe.targetObstacleActiveBoardLimit < 0 ||
                 recipe.minBlockWidth < 1 || recipe.maxBlockWidth > 4 || recipe.minBlockWidth > recipe.maxBlockWidth)
            error = "Direct Stage recipe has an invalid move limit or block-width range.";
        else if (!IsChance(recipe.gapChance) || !IsChance(recipe.largeBlockChance) ||
                 !IsSpawnSettingValid(recipe.rock) || !IsSpawnSettingValid(recipe.chain) || !IsSpawnSettingValid(recipe.ice) ||
                 !IsSpawnSettingValid(recipe.fire) || !IsSpawnSettingValid(recipe.slice) ||
                 !IsChance(recipe.fogCoveragePercent) || !System.Enum.IsDefined(typeof(FogDensity), recipe.fogDensity))
            error = "Direct Stage recipe has an invalid chance, fog, or spawn setting.";
        return error == null;
    }

    private static bool IsSpawnSettingValid(AdventureStageSpawnSetting setting) =>
        setting != null && IsChance(setting.chance);

    private static bool IsChance(float value) => !float.IsNaN(value) && value >= 0f && value <= 1f;

    private static void ApplyDirectRecipe(AdventureStageRecipe recipe, LevelData target)
    {
        target.hasMoveLimit = recipe.hasMoveLimit;
        target.moveLimit = recipe.hasMoveLimit ? Mathf.Max(1, recipe.moveLimit) : 0;
        target.baseGapChance = Mathf.Clamp01(recipe.gapChance);
        target.largeBlockChance = Mathf.Clamp01(recipe.largeBlockChance);
        target.openingTargetObstacleLimit = Mathf.Max(0, recipe.openingTargetObstacleLimit);
        target.targetObstaclePerRowLimit = Mathf.Max(0, recipe.targetObstaclePerRowLimit);
        target.targetObstacleActiveBoardLimit = Mathf.Max(0, recipe.targetObstacleActiveBoardLimit);
        target.minBlockSize = Mathf.Clamp(recipe.minBlockWidth, 1, 4);
        target.maxBlockSize = Mathf.Clamp(Mathf.Max(target.minBlockSize, recipe.maxBlockWidth), 1, 4);
        target.rockBlockChance = GetEffectiveChance(recipe.rock);
        target.chainedBlockChance = GetEffectiveChance(recipe.chain);
        target.frozenBlockChance = GetEffectiveChance(recipe.ice);
        target.fireBlockChance = GetEffectiveChance(recipe.fire);
        target.sliceBlockChance = GetEffectiveChance(recipe.slice);
        target.useCustomSpawnRules = recipe.useCustomWidthRules || recipe.fire.enabled || recipe.slice.enabled;
        target.fogDensity = recipe.fogDensity;
        target.fogCoveragePercent = Mathf.Clamp01(recipe.fogCoveragePercent);
        target.fogStartingRow = recipe.fogStartingRow;
    }

    private static void AppendLegacySourceNote(AdventureLevelConfig config, LevelData resolvedLegacyLevel)
    {
        string summary =
            "Legacy Source:\n" +
            $"Difficulty={config.difficulty}\n" +
            $"Pressure={config.pressure}\n" +
            $"Obstacle={config.obstacleTheme}\n" +
            $"Focus={config.specialMechanicFocus}";

        if (config.moveOffset != 0) summary += $"\nMoveOffset={config.moveOffset}";
        if (config.pressureOffset != 0) summary += $"\nPressureOffset={config.pressureOffset}";
        if (config.useHandcraftedOverrides) summary += "\nHandcraftedOverrides=Enabled";
        if ((config.specialMechanicFocus == SpecialMechanicFocus.Fire || config.specialMechanicFocus == SpecialMechanicFocus.Slice) &&
            !resolvedLegacyLevel.useCustomSpawnRules)
        {
            summary += "\nEffectiveSupport=Disabled (legacy custom spawn rules were off)";
        }

        if (config.designerNotes != null && config.designerNotes.Contains(summary))
            return;

        config.designerNotes = string.IsNullOrWhiteSpace(config.designerNotes)
            ? summary
            : config.designerNotes.TrimEnd() + "\n\n" + summary;
    }

    private static float GetEffectiveChance(AdventureStageSpawnSetting setting) =>
        setting != null && setting.enabled ? Mathf.Clamp01(setting.chance) : 0f;

    private static void ApplyDirectObjectives(AdventureLevelConfig config, LevelData target)
    {
        ResetLegacyTargets(target);
        AdventureObjectiveDefinition fallbackObjective = GetFirstRuntimeCompatibleObjective(config);
        if (fallbackObjective == null)
        {
            target.objectiveType = ObjectiveType.ClearRows;
            target.targetLines = UnsupportedObjectiveFallbackLines;
            return;
        }

        int requiredAmount = Mathf.Max(1, fallbackObjective.requiredAmount);
        if (fallbackObjective.action == AdventureObjectiveAction.ReachScore)
        {
            target.objectiveType = ObjectiveType.ReachScore;
            target.targetScore = requiredAmount;
            target.targetLines = UnsupportedObjectiveFallbackLines;
        }
        else
        {
            target.objectiveType = ObjectiveType.ClearRows;
            target.targetLines = requiredAmount;
        }
    }

    private static AdventureObjectiveDefinition[] BuildLegacyObjectives(LevelData level)
    {
        if (level.objectiveType == ObjectiveType.DestroyObstacles)
        {
            return new[] { new AdventureObjectiveDefinition
            {
                action = AdventureObjectiveAction.DestroyObstacle,
                target = AdventureObjectiveTarget.AnyObstacle,
                requiredAmount = Mathf.Max(1, level.targetObstacleCount)
            }};
        }

        bool score = level.objectiveType == ObjectiveType.ReachScore;
        return new[] { new AdventureObjectiveDefinition
        {
            action = score ? AdventureObjectiveAction.ReachScore : AdventureObjectiveAction.ClearRows,
            target = score ? AdventureObjectiveTarget.Score : AdventureObjectiveTarget.Rows,
            requiredAmount = score ? level.targetScore : level.targetLines
        }};
    }

    public static LevelData CopyLegacyRuntimeLevel(LevelData source)
    {
        if (source == null) return null;
        if (source.objectiveType != ObjectiveType.ClearRows &&
            source.objectiveType != ObjectiveType.ReachScore &&
            source.objectiveType != ObjectiveType.DestroyObstacles)
        {
            Debug.LogError($"[Adventure] Legacy hedef {source.objectiveType} desteklenmiyor.", source);
            return null;
        }
        AdventureObjectiveDefinition[] definitions = BuildLegacyObjectives(source);
        if (!LevelData.ValidateObjectives(definitions, out string error))
        {
            Debug.LogError($"[Adventure] {error}", source);
            return null;
        }
        LevelData runtime = Object.Instantiate(source);
        runtime.hideFlags = HideFlags.DontSave;
        runtime.CopyRuntimeObjectives(definitions);
        return runtime;
    }

    private static void ApplyObjective(AdventureLevelConfig config, LevelData target, ref LevelProfile profile)
    {
        int defaultLines;
        int defaultScore;

        switch (config.difficulty)
        {
            case DifficultyTier.Medium:
                defaultLines = 8;
                defaultScore = 2500;
                break;
            case DifficultyTier.Hard:
                defaultLines = 10;
                defaultScore = 4500;
                break;
            case DifficultyTier.Expert:
                defaultLines = 12;
                defaultScore = 7000;
                break;
            default:
                defaultLines = 6;
                defaultScore = 1500;
                break;
        }

        if (config.HasObjectiveV2())
        {
            ApplyObjectiveV2Fallback(config, target, ref profile, defaultLines, defaultScore);
            return;
        }

        switch (config.objective)
        {
            case ObjectiveType.ReachScore:
                target.targetScore = config.targetScore > 0 ? config.targetScore : defaultScore;
                target.targetLines = config.targetLines > 0 ? config.targetLines : UnsupportedObjectiveFallbackLines;
                break;
            case ObjectiveType.DestroyObstacles:
                target.targetScore = 0;
                target.targetLines = config.targetLines > 0 ? config.targetLines : defaultLines;
                target.targetObstacleCount = config.targetObstacleCount > 0 ? config.targetObstacleCount : defaultLines;
                profile.rockBlockChance += 0.04f;
                profile.frozenBlockChance += 0.04f;
                profile.chainedBlockChance += 0.04f;
                break;
            case ObjectiveType.ComboTarget:
                target.targetScore = config.targetScore > 0 ? config.targetScore : defaultScore;
                target.targetLines = config.targetLines > 0 ? config.targetLines : Mathf.Max(1, defaultLines - 1);
                profile.largeBlockChance += 0.05f;
                profile.baseGapChance -= 0.03f;
                break;
            default:
                target.targetScore = config.targetScore > 0 ? config.targetScore : 0;
                target.targetLines = config.targetLines > 0 ? config.targetLines : defaultLines;
                break;
        }
    }

    private static void ApplyObjectiveV2Fallback(
        AdventureLevelConfig config,
        LevelData target,
        ref LevelProfile profile,
        int defaultLines,
        int defaultScore)
    {
        ResetLegacyTargets(target);

        AdventureObjectiveDefinition fallbackObjective = GetFirstRuntimeCompatibleObjective(config);
        if (fallbackObjective == null)
        {
            target.objectiveType = ObjectiveType.ClearRows;
            target.targetLines = UnsupportedObjectiveFallbackLines;
            target.targetScore = 0;
            return;
        }

        int requiredAmount = Mathf.Max(1, fallbackObjective.requiredAmount);

        switch (fallbackObjective.action)
        {
            case AdventureObjectiveAction.ReachScore:
                target.objectiveType = ObjectiveType.ReachScore;
                target.targetScore = requiredAmount > 0 ? requiredAmount : defaultScore;
                target.targetLines = UnsupportedObjectiveFallbackLines;
                break;
            case AdventureObjectiveAction.DestroyObstacle:
                target.objectiveType = ObjectiveType.DestroyObstacles;
                target.targetObstacleCount = requiredAmount;
                target.targetLines = UnsupportedObjectiveFallbackLines;
                profile.rockBlockChance += 0.04f;
                profile.frozenBlockChance += 0.04f;
                profile.chainedBlockChance += 0.04f;
                break;
            case AdventureObjectiveAction.BreakChain:
                target.objectiveType = ObjectiveType.DestroyObstacles;
                target.targetObstacleCount = requiredAmount;
                target.targetLines = UnsupportedObjectiveFallbackLines;
                profile.chainedBlockChance += 0.08f;
                break;
            case AdventureObjectiveAction.ComboTarget:
                target.objectiveType = ObjectiveType.ComboTarget;
                target.targetComboCount = requiredAmount;
                target.targetScore = config.targetScore > 0 ? config.targetScore : defaultScore;
                target.targetLines = Mathf.Max(1, defaultLines - 1);
                profile.largeBlockChance += 0.05f;
                profile.baseGapChance -= 0.03f;
                break;
            default:
                target.objectiveType = ObjectiveType.ClearRows;
                target.targetLines = requiredAmount > 0 ? requiredAmount : defaultLines;
                target.targetScore = 0;
                break;
        }
    }

    private static AdventureObjectiveDefinition GetFirstRuntimeCompatibleObjective(AdventureLevelConfig config)
    {
        if (config.objectives == null)
        {
            return null;
        }

        for (int i = 0; i < config.objectives.Count; i++)
        {
            AdventureObjectiveDefinition objectiveDefinition = config.objectives[i];
            if (objectiveDefinition == null)
            {
                continue;
            }

            if (objectiveDefinition.action == AdventureObjectiveAction.CollectItem)
            {
                continue;
            }

            return objectiveDefinition;
        }

        return null;
    }

    private static void ResetLegacyTargets(LevelData target)
    {
        target.targetScore = 0;
        target.targetLines = 0;
        target.targetObstacleCount = 0;
        target.targetComboCount = 0;
    }

    private static void ApplyOverrides(AdventureLevelConfig config, LevelData target)
    {
        if (!config.useHandcraftedOverrides || config.handcraftedOverrides == null)
            return;

        AdventureLevelOverrides overrides = config.handcraftedOverrides;
        if (overrides.overrideMoveLimit) target.moveLimit = Mathf.Max(1, overrides.moveLimit);
        if (overrides.overrideBaseGapChance) target.baseGapChance = Mathf.Clamp01(overrides.baseGapChance);
        if (overrides.overrideLargeBlockChance) target.largeBlockChance = Mathf.Clamp01(overrides.largeBlockChance);
        if (overrides.overrideFrozenChance) target.frozenBlockChance = Mathf.Clamp01(overrides.frozenBlockChance);
        if (overrides.overrideRockChance) target.rockBlockChance = Mathf.Clamp01(overrides.rockBlockChance);
        if (overrides.overrideChainedChance) target.chainedBlockChance = Mathf.Clamp01(overrides.chainedBlockChance);
        if (overrides.overrideCustomSpawnRules) target.useCustomSpawnRules = overrides.useCustomSpawnRules;
        if (overrides.overrideMinBlockSize)
        {
            target.minBlockSize = Mathf.Clamp(overrides.minBlockSize, 1, 4);
        }
        if (overrides.overrideMaxBlockSize)
        {
            target.maxBlockSize = Mathf.Clamp(Mathf.Max(target.minBlockSize, overrides.maxBlockSize), 1, 4);
        }
        if (overrides.overrideSliceChance)
        {
            target.sliceBlockChance = Mathf.Clamp01(overrides.sliceBlockChance);
        }
        if (overrides.overrideFireChance)
        {
            target.fireBlockChance = Mathf.Clamp01(overrides.fireBlockChance);
        }
        if (overrides.overrideFogDensity) target.fogDensity = overrides.fogDensity;
        if (overrides.overrideFogCoveragePercent) target.fogCoveragePercent = Mathf.Clamp01(overrides.fogCoveragePercent);
        if (overrides.overrideFogStartingRow) target.fogStartingRow = overrides.fogStartingRow;

    }

    private static LevelProfile BuildBaseProfile(DifficultyTier difficulty)
    {
        LevelProfile profile = new LevelProfile
        {
            moveLimit = 30,
            baseGapChance = 0.38f,
            largeBlockChance = 0.09f,
            frozenBlockChance = 0f,
            rockBlockChance = 0f,
            chainedBlockChance = 0f,
            useCustomSpawnRules = false,
            minBlockSize = 1,
            maxBlockSize = 4,
            sliceBlockChance = 0f,
            fireBlockChance = 0f,
            fogDensity = FogDensity.None,
            fogCoveragePercent = 0f,
            fogStartingRow = -1
        };

        switch (difficulty)
        {
            case DifficultyTier.Medium:
                profile.moveLimit = 26;
                profile.baseGapChance = 0.30f;
                profile.largeBlockChance = 0.12f;
                profile.frozenBlockChance = 0.02f;
                profile.rockBlockChance = 0.02f;
                profile.chainedBlockChance = 0.02f;
                break;
            case DifficultyTier.Hard:
                profile.moveLimit = 22;
                profile.baseGapChance = 0.23f;
                profile.largeBlockChance = 0.15f;
                profile.frozenBlockChance = 0.04f;
                profile.rockBlockChance = 0.05f;
                profile.chainedBlockChance = 0.04f;
                break;
            case DifficultyTier.Expert:
                profile.moveLimit = 18;
                profile.baseGapChance = 0.17f;
                profile.largeBlockChance = 0.18f;
                profile.frozenBlockChance = 0.05f;
                profile.rockBlockChance = 0.07f;
                profile.chainedBlockChance = 0.06f;
                break;
        }

        return profile;
    }

    private static void ApplyPressure(ref LevelProfile profile, PressureType pressure, int pressureOffset)
    {
        switch (pressure)
        {
            case PressureType.TightMoves:
                profile.moveLimit -= 4;
                profile.baseGapChance -= 0.02f;
                break;
            case PressureType.ObstacleDense:
                profile.moveLimit -= 2;
                profile.frozenBlockChance += 0.03f;
                profile.rockBlockChance += 0.03f;
                profile.chainedBlockChance += 0.03f;
                break;
            case PressureType.ComboHeavy:
                profile.baseGapChance -= 0.05f;
                profile.largeBlockChance += 0.07f;
                break;
            case PressureType.Chaos:
                profile.moveLimit -= 3;
                profile.baseGapChance -= 0.04f;
                profile.largeBlockChance += 0.04f;
                profile.frozenBlockChance += 0.03f;
                profile.rockBlockChance += 0.03f;
                profile.chainedBlockChance += 0.03f;
                break;
            default:
                profile.moveLimit += 3;
                profile.baseGapChance += 0.06f;
                break;
        }

        if (pressureOffset != 0)
        {
            profile.moveLimit -= pressureOffset;
            profile.baseGapChance -= pressureOffset * 0.02f;
            float obstacleDelta = pressureOffset * 0.015f;
            profile.frozenBlockChance += obstacleDelta;
            profile.rockBlockChance += obstacleDelta;
            profile.chainedBlockChance += obstacleDelta;
        }
    }

    private static void ApplyObstacleTheme(ref LevelProfile profile, ObstacleTheme theme)
    {
        switch (theme)
        {
            case ObstacleTheme.Ice:
                profile.frozenBlockChance += 0.10f;
                break;
            case ObstacleTheme.Rock:
                profile.rockBlockChance += 0.10f;
                break;
            case ObstacleTheme.Chain:
                profile.chainedBlockChance += 0.10f;
                break;
            case ObstacleTheme.Fog:
                profile.fogDensity = FogDensity.Light;
                profile.fogCoveragePercent = 0.25f;
                profile.baseGapChance -= 0.02f;
                break;
            case ObstacleTheme.Mixed:
                profile.frozenBlockChance += 0.05f;
                profile.rockBlockChance += 0.05f;
                profile.chainedBlockChance += 0.05f;
                profile.fogDensity = FogDensity.Light;
                profile.fogCoveragePercent = 0.20f;
                break;
        }
    }

    private static void ApplySpecialFocus(ref LevelProfile profile, SpecialMechanicFocus focus)
    {
        switch (focus)
        {
            case SpecialMechanicFocus.Fire:
                profile.fireBlockChance += 0.05f;
                break;
            case SpecialMechanicFocus.Slice:
                profile.sliceBlockChance += 0.05f;
                break;
            case SpecialMechanicFocus.Fog:
                if (profile.fogDensity == FogDensity.None)
                    profile.fogDensity = FogDensity.Light;

                profile.fogCoveragePercent = Mathf.Max(profile.fogCoveragePercent, 0.35f);
                break;
            case SpecialMechanicFocus.LargeBlocks:
                profile.largeBlockChance += 0.08f;
                profile.minBlockSize = 2;
                break;
            case SpecialMechanicFocus.MoveEfficiency:
                profile.moveLimit -= 2;
                break;
            case SpecialMechanicFocus.ChainBreaking:
                profile.chainedBlockChance += 0.08f;
                break;
        }
    }

    private static void ApplyFogAuthoring(AdventureLevelConfig config, ref LevelProfile profile)
    {
        if (config.fogDensity != FogDensity.None)
            profile.fogDensity = config.fogDensity;

        if (config.fogCoveragePercent > 0f)
            profile.fogCoveragePercent = Mathf.Clamp01(config.fogCoveragePercent);

        if (profile.fogDensity == FogDensity.None)
            profile.fogCoveragePercent = 0f;
    }

    private static void LogGeneratedLevel(AdventureLevelConfig config, LevelData target)
    {
        Debug.Log(
            $"[AdventureLevelGenerator] Level {target.levelNumber} generated from AdventureLevelConfig | " +
            $"Objective={config.objective} | " +
            $"TargetLines={target.targetLines} | TargetScore={target.targetScore} | MoveLimit={target.moveLimit} | " +
            $"Gap={target.baseGapChance:F2} | Large={target.largeBlockChance:F2} | " +
            $"Frozen={target.frozenBlockChance:F2} | Rock={target.rockBlockChance:F2} | Chain={target.chainedBlockChance:F2} | " +
            $"Fire={target.fireBlockChance:F2} | Slice={target.sliceBlockChance:F2} | FogDensity={target.fogDensity} | FogCoverage={target.fogCoveragePercent:F2} | FogStart={target.fogStartingRow}");
    }

    private struct LevelProfile
    {
        public int moveLimit;
        public float baseGapChance;
        public float largeBlockChance;
        public float frozenBlockChance;
        public float rockBlockChance;
        public float chainedBlockChance;
        public bool useCustomSpawnRules;
        public int minBlockSize;
        public int maxBlockSize;
        public float sliceBlockChance;
        public float fireBlockChance;
        public FogDensity fogDensity;
        public float fogCoveragePercent;
        public int fogStartingRow;
    }
}
