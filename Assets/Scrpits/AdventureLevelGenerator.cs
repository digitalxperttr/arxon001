using UnityEngine;

public static class AdventureLevelGenerator
{
    private const int UnsupportedObjectiveFallbackLines = 1;

    public static LevelData GenerateRuntimeLevel(AdventureLevelConfig config)
    {
        if (config == null)
            return null;

        LevelData runtimeLevel = ScriptableObject.CreateInstance<LevelData>();
        runtimeLevel.hideFlags = HideFlags.DontSave;
        runtimeLevel.name = string.IsNullOrWhiteSpace(config.displayName)
            ? $"GeneratedAdventureLevel_{config.levelNumber}"
            : config.displayName;

        ApplyConfig(config, runtimeLevel);
        return runtimeLevel;
    }

    public static void ApplyConfig(AdventureLevelConfig config, LevelData target)
    {
        if (config == null || target == null)
            return;

        target.levelNumber = Mathf.Max(1, config.levelNumber);
        target.objectiveType = config.objective;
        target.targetObstacleCount = Mathf.Max(0, config.targetObstacleCount);
        target.targetComboCount = Mathf.Max(0, config.targetComboCount);
        target.isEndless = config.isEndless;

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
        LogGeneratedLevel(config, target);
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

        switch (config.objective)
        {
            case ObjectiveType.ReachScore:
                target.targetScore = config.targetScore > 0 ? config.targetScore : defaultScore;
                target.targetLines = config.targetLines > 0 ? config.targetLines : UnsupportedObjectiveFallbackLines;
                break;
            case ObjectiveType.DestroyObstacles:
                target.targetScore = 0;
                target.targetLines = config.targetLines > 0 ? config.targetLines : defaultLines;
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

    private static void ApplyOverrides(AdventureLevelConfig config, LevelData target)
    {
        if (!config.useHandcraftedOverrides || config.handcraftedOverrides == null)
            return;

        AdventureLevelOverrides overrides = config.handcraftedOverrides;
        bool shouldEnableCustomSpawnRules = false;

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
            shouldEnableCustomSpawnRules = true;
        }
        if (overrides.overrideMaxBlockSize)
        {
            target.maxBlockSize = Mathf.Clamp(Mathf.Max(target.minBlockSize, overrides.maxBlockSize), 1, 4);
            shouldEnableCustomSpawnRules = true;
        }
        if (overrides.overrideSliceChance)
        {
            target.sliceBlockChance = Mathf.Clamp01(overrides.sliceBlockChance);
            shouldEnableCustomSpawnRules |= target.sliceBlockChance > 0f;
        }
        if (overrides.overrideFireChance)
        {
            target.fireBlockChance = Mathf.Clamp01(overrides.fireBlockChance);
            shouldEnableCustomSpawnRules |= target.fireBlockChance > 0f;
        }
        if (overrides.overrideFogDensity) target.fogDensity = overrides.fogDensity;
        if (overrides.overrideFogCoveragePercent) target.fogCoveragePercent = Mathf.Clamp01(overrides.fogCoveragePercent);
        if (overrides.overrideFogStartingRow) target.fogStartingRow = overrides.fogStartingRow;

        if (!target.useCustomSpawnRules && shouldEnableCustomSpawnRules)
            target.useCustomSpawnRules = true;
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
                profile.useCustomSpawnRules = true;
                break;
            case SpecialMechanicFocus.Slice:
                profile.sliceBlockChance += 0.05f;
                profile.useCustomSpawnRules = true;
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
