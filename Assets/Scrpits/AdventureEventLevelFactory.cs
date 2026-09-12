using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Pure authoring layer. It prepares Direct configs; AdventureLevelGenerator remains the runtime converter.
public sealed class AdventureEventLevelSpec
{
    public int StageNumber;
    public string LevelId;
    public string DifficultyLabel;
    public string RhythmRole;
    public AdventureLevelConfig Config;
}

public sealed class AdventureEventGenerationPlan
{
    public readonly List<AdventureEventLevelSpec> Levels = new List<AdventureEventLevelSpec>();
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public bool IsValid => Errors.Count == 0;
}

public static class AdventureEventLevelFactory
{
    public const string CurrentGeneratorVersion = "local-direct-50-v5-seeded-stage-sequence";

    private enum StageTemplate
    {
        Rows,
        Score,
        Collect,
        Ice,
        RowsRock,
        RowsChain,
        CollectIce,
        RowsRockChain,
        ThresholdIce,
        ThresholdChain
    }

    private sealed class StageBlueprint
    {
        public StageTemplate Template;
        public bool Fire;
        public bool Slice;
        public int FogVariant;
    }

    public static AdventureEventGenerationPlan BuildPlan(AdventureEventConfig eventConfig)
    {
        AdventureEventGenerationPlan plan = new AdventureEventGenerationPlan();
        if (eventConfig == null)
        {
            plan.Errors.Add("Event config is missing.");
            return plan;
        }
        if (string.IsNullOrWhiteSpace(eventConfig.eventId)) plan.Errors.Add("Event ID must not be empty.");
        if (string.IsNullOrWhiteSpace(eventConfig.contentVersion)) plan.Errors.Add("Content version must not be empty.");

        int count = eventConfig.generationSettings != null
            ? Mathf.Max(1, eventConfig.generationSettings.defaultLevelCount)
            : Mathf.Max(1, eventConfig.levelCount);
        if (count != 50) plan.Warnings.Add($"This local generator is designed for 50 stages; requested count is {count}.");

        List<CollectibleDefinition> eligible = GetEligibleCollectibles(eventConfig.collectibleDatabase);
        if (eligible.Count == 0)
        {
            plan.Errors.Add("No eligible collectible exists. Assign a database containing entries with availableInGenerator=true plus a non-empty ID and icon.");
            return plan;
        }
        if (plan.Errors.Count > 0) return plan;

        int seed = eventConfig.generationSettings != null ? eventConfig.generationSettings.seed : 1;
        List<StageBlueprint> blueprints = BuildBlueprints(seed, count);
        for (int stage = 1; stage <= count; stage++)
        {
            AdventureGameplayRng rng = new AdventureGameplayRng(DeriveStageSeed(seed, stage));
            AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
            ApplyGeneratedValues(config, eventConfig, stage, eligible, rng, blueprints[stage - 1], preserveLevelId: null);
            plan.Levels.Add(new AdventureEventLevelSpec
            {
                StageNumber = stage,
                LevelId = config.levelId,
                DifficultyLabel = config.generatedDifficultyLabel,
                RhythmRole = config.generatedRhythmRole,
                Config = config
            });
        }
        return plan;
    }

    public static bool RegenerateSelected(AdventureEventConfig eventConfig, AdventureLevelConfig target, out string error)
    {
        error = null;
        if (eventConfig == null || target == null)
        {
            error = "Event config and selected level are required.";
            return false;
        }
        if (eventConfig.generationSettings != null &&
            !string.IsNullOrWhiteSpace(eventConfig.generationSettings.generatorVersion) &&
            eventConfig.generationSettings.generatorVersion != CurrentGeneratorVersion)
        {
            error = $"Selected regeneration is disabled for the historical generator version '{eventConfig.generationSettings.generatorVersion}'. Create a separate comparison package instead.";
            return false;
        }
        if (target.protectFromRegeneration)
        {
            error = "Selected level is protected from regeneration.";
            return false;
        }

        List<CollectibleDefinition> eligible = GetEligibleCollectibles(eventConfig.collectibleDatabase);
        if (eligible.Count == 0)
        {
            error = "No eligible collectible exists. Assign valid availableInGenerator entries before regeneration.";
            return false;
        }

        int stage = target.GetStageNumber();
        int seed = eventConfig.generationSettings != null ? eventConfig.generationSettings.seed : 1;
        int count = eventConfig.generationSettings != null
            ? Mathf.Max(1, eventConfig.generationSettings.defaultLevelCount)
            : Mathf.Max(1, eventConfig.levelCount);
        List<StageBlueprint> blueprints = BuildBlueprints(seed, count);
        if (stage < 1 || stage > blueprints.Count)
        {
            error = "Selected stage is outside this package's generated sequence.";
            return false;
        }
        ApplyGeneratedValues(target, eventConfig, stage, eligible, new AdventureGameplayRng(DeriveStageSeed(seed, stage)), blueprints[stage - 1], target.levelId);
        return AdventureLevelGenerator.ValidateConfig(target, out error);
    }

    public static List<CollectibleDefinition> GetEligibleCollectibles(CollectibleDatabase database)
    {
        if (database == null || database.collectibles == null) return new List<CollectibleDefinition>();
        return database.collectibles
            .Where(entry => entry != null && entry.availableInGenerator && !string.IsNullOrWhiteSpace(entry.id) && entry.icon != null)
            .OrderBy(entry => entry.id, StringComparer.Ordinal)
            .ToList();
    }

    public static string GetRecipeSignature(AdventureLevelConfig config)
    {
        if (config == null) return string.Empty;
        return JsonUtility.ToJson(config.directRecipe) + "|" + JsonUtility.ToJson(new ObjectiveSignature { objectives = config.objectives });
    }

    [Serializable]
    private class ObjectiveSignature { public List<AdventureObjectiveDefinition> objectives; }

    private static int DeriveStageSeed(int seed, int stage)
    {
        unchecked { return seed ^ (stage * 0x45d9f3b); }
    }

    private static List<StageBlueprint> BuildBlueprints(int seed, int count)
    {
        AdventureGameplayRng authoringRng = new AdventureGameplayRng(unchecked(seed ^ 0x6e624eb7));
        List<StageBlueprint> blueprints = new List<StageBlueprint>(count);
        for (int stage = 1; stage <= count; stage++) blueprints.Add(new StageBlueprint());

        int groups = Mathf.CeilToInt(count / 10f);
        for (int group = 0; group < groups; group++)
        {
            int first = group * 10;
            AssignPermutation(blueprints, first, authoringRng, StageTemplate.Rows, StageTemplate.Score, StageTemplate.Collect);
            AssignPermutation(blueprints, first + 3, authoringRng, StageTemplate.Ice, StageTemplate.RowsRock, StageTemplate.RowsChain);
            if (first + 6 < count)
                blueprints[first + 6].Template = PickOne(authoringRng, StageTemplate.Rows, StageTemplate.Score, StageTemplate.Collect);
            AssignPermutation(blueprints, first + 7, authoringRng, StageTemplate.CollectIce, StageTemplate.RowsRockChain);
            if (first + 9 < count)
                blueprints[first + 9].Template = PickOne(authoringRng, StageTemplate.ThresholdIce, StageTemplate.ThresholdChain);
            AssignSupportAndFog(blueprints, first, group, authoringRng);
        }
        return blueprints;
    }

    private static void AssignPermutation(List<StageBlueprint> blueprints, int first, AdventureGameplayRng rng, params StageTemplate[] templates)
    {
        List<StageTemplate> remaining = new List<StageTemplate>(templates);
        int assignedCount = remaining.Count;
        for (int i = 0; i < assignedCount && first + i < blueprints.Count; i++)
        {
            int index = rng.NextInt(0, remaining.Count);
            blueprints[first + i].Template = remaining[index];
            remaining.RemoveAt(index);
        }
    }

    private static StageTemplate PickOne(AdventureGameplayRng rng, params StageTemplate[] templates) => templates[rng.NextInt(0, templates.Length)];

    private static void AssignSupportAndFog(List<StageBlueprint> blueprints, int first, int group, AdventureGameplayRng rng)
    {
        List<int> supportSlots = new List<int>();
        for (int phase = 4; phase <= 9 && first + phase - 1 < blueprints.Count; phase++) supportSlots.Add(first + phase - 1);
        int fireCount = group == 0 ? 1 : 2;
        for (int i = 0; i < fireCount && supportSlots.Count > 0; i++)
        {
            int slot = TakeRandomSlot(supportSlots, rng);
            blueprints[slot].Fire = true;
        }
        if (supportSlots.Count > 0) blueprints[TakeRandomSlot(supportSlots, rng)].Slice = true;

        List<int> fogSlots = new List<int>();
        for (int phase = 5; phase <= 9 && first + phase - 1 < blueprints.Count; phase++) fogSlots.Add(first + phase - 1);
        int fogCount = group >= 2 ? 2 : 1;
        for (int i = 0; i < fogCount && fogSlots.Count > 0; i++)
            blueprints[TakeRandomSlot(fogSlots, rng)].FogVariant = i == 0 ? 1 : 2;
    }

    private static int TakeRandomSlot(List<int> slots, AdventureGameplayRng rng)
    {
        int index = rng.NextInt(0, slots.Count);
        int value = slots[index];
        slots.RemoveAt(index);
        return value;
    }

    private static void ApplyGeneratedValues(
        AdventureLevelConfig target,
        AdventureEventConfig eventConfig,
        int stage,
        List<CollectibleDefinition> eligible,
        AdventureGameplayRng rng,
        StageBlueprint blueprint,
        string preserveLevelId)
    {
        int group = (stage - 1) / 10;
        int phase = ((stage - 1) % 10) + 1;
        target.levelId = string.IsNullOrWhiteSpace(preserveLevelId)
            ? $"{eventConfig.eventId.Trim()}-stage-{stage:000}"
            : preserveLevelId;
        target.eventId = eventConfig.eventId.Trim();
        target.contentVersion = eventConfig.contentVersion.Trim();
        target.displayedLevelNumber = stage;
        target.levelNumber = stage;
        target.displayName = $"{eventConfig.eventName} Bölüm {stage}";
        target.authoringMode = AdventureStageAuthoringMode.DirectRecipeAuthoring;
        target.isEndless = false;
        target.objectives = BuildObjectives(group, blueprint.Template, eligible, rng);
        target.directRecipe = BuildRecipe(group, blueprint, rng, eventConfig.generationSettings);
        target.difficulty = ToDifficulty(group, phase);
        target.pressure = PressureType.Relaxed;
        target.obstacleTheme = ToTheme(blueprint.Template);
        target.specialMechanicFocus = SpecialMechanicFocus.None;
        target.fogDensity = target.directRecipe.fogDensity;
        target.fogCoveragePercent = target.directRecipe.fogCoveragePercent;
        target.generatedDifficultyLabel = DifficultyLabel(group, phase);
        target.generatedRhythmRole = RhythmRole(phase);
        target.generatedWithVersion = eventConfig.generationSettings != null && !string.IsNullOrWhiteSpace(eventConfig.generationSettings.generatorVersion)
            ? eventConfig.generationSettings.generatorVersion
            : CurrentGeneratorVersion;
        target.designerNotes = $"Generated local draft | {target.generatedRhythmRole} | Planned difficulty: {target.generatedDifficultyLabel}. Values are authoring starting points, not measured player difficulty.";
    }

    private static AdventureStageRecipe BuildRecipe(int group, StageBlueprint blueprint, AdventureGameplayRng rng, AdventureEventGenerationSettings generationSettings)
    {
        float jitter = (rng.NextFloat01() - .5f) * .018f;
        AdventureStageRecipe recipe = new AdventureStageRecipe
        {
            hasMoveLimit = false,
            moveLimit = 0,
            gapChance = Mathf.Clamp(.38f - group * .022f + jitter, .26f, .40f),
            minBlockWidth = 1,
            maxBlockWidth = 4,
            useCustomWidthRules = false,
            largeBlockChance = Mathf.Clamp(.08f + group * .015f, .08f, .15f),
            openingTargetObstacleLimit = 2,
            targetObstaclePerRowLimit = generationSettings != null ? Mathf.Max(0, generationSettings.defaultTargetObstaclePerRowLimit) : 2,
            targetObstacleActiveBoardLimit = generationSettings != null ? Mathf.Max(0, generationSettings.defaultTargetObstacleActiveBoardLimit) : 2,
            rock = new AdventureStageSpawnSetting(), chain = new AdventureStageSpawnSetting(), ice = new AdventureStageSpawnSetting(),
            fire = new AdventureStageSpawnSetting(), slice = new AdventureStageSpawnSetting(),
            fogDensity = FogDensity.None, fogCoveragePercent = 0f, fogStartingRow = -1
        };

        if (UsesIce(blueprint.Template)) Enable(recipe.ice, .09f + group * .012f);
        if (UsesRock(blueprint.Template)) Enable(recipe.rock, .08f + group * .014f);
        if (UsesChain(blueprint.Template)) Enable(recipe.chain, .07f + group * .014f);
        if (blueprint.Fire) Enable(recipe.fire, .025f);
        if (blueprint.Slice) Enable(recipe.slice, .025f);

        if (blueprint.FogVariant > 0)
        {
            recipe.fogDensity = FogDensity.Light;
            recipe.fogCoveragePercent = blueprint.FogVariant == 2
                ? .22f + group * .025f
                : .18f + group * .02f;
            recipe.fogStartingRow = 4;
        }
        return recipe;
    }

    private static void Enable(AdventureStageSpawnSetting setting, float chance)
    {
        setting.enabled = true;
        setting.chance = Mathf.Clamp01(chance);
    }

    private static List<AdventureObjectiveDefinition> BuildObjectives(int group, StageTemplate template, List<CollectibleDefinition> eligible, AdventureGameplayRng rng)
    {
        int rows = 6 + group * 2;
        int obstacle = 5 + group * 2;
        List<AdventureObjectiveDefinition> objectives = new List<AdventureObjectiveDefinition>();
        switch (template)
        {
            case StageTemplate.Rows: objectives.Add(Rows(8 + group * 2)); break;
            case StageTemplate.Score: objectives.Add(Score(1200 + group * 600)); break;
            case StageTemplate.Collect: objectives.Add(Collect(eligible[rng.NextInt(0, eligible.Count)], 5 + group)); break;
            case StageTemplate.Ice: objectives.Add(Obstacle(AdventureObjectiveTarget.Ice, obstacle)); break;
            case StageTemplate.RowsRock: objectives.Add(Rows(rows)); objectives.Add(Obstacle(AdventureObjectiveTarget.Rock, obstacle)); break;
            case StageTemplate.RowsChain: objectives.Add(Rows(rows)); objectives.Add(Chain(obstacle)); break;
            case StageTemplate.CollectIce: objectives.Add(Collect(eligible[rng.NextInt(0, eligible.Count)], 5 + group)); objectives.Add(Obstacle(AdventureObjectiveTarget.Ice, obstacle)); break;
            case StageTemplate.RowsRockChain: objectives.Add(Rows(rows)); objectives.Add(Obstacle(AdventureObjectiveTarget.Rock, obstacle)); objectives.Add(Chain(obstacle)); break;
            case StageTemplate.ThresholdIce:
                objectives.Add(Rows(rows + 3));
                objectives.Add(Score(2200 + group * 1000));
                objectives.Add(Obstacle(AdventureObjectiveTarget.Ice, obstacle + 1));
                break;
            default:
                objectives.Add(Rows(rows + 3));
                objectives.Add(Score(2200 + group * 1000));
                objectives.Add(Chain(obstacle + 1));
                break;
        }
        return objectives;
    }

    private static AdventureObjectiveDefinition Rows(int value) => new AdventureObjectiveDefinition { action = AdventureObjectiveAction.ClearRows, target = AdventureObjectiveTarget.Rows, requiredAmount = value, displayLabel = "Satır" };
    private static AdventureObjectiveDefinition Score(int value) => new AdventureObjectiveDefinition { action = AdventureObjectiveAction.ReachScore, target = AdventureObjectiveTarget.Score, requiredAmount = value, displayLabel = "Puan" };
    private static AdventureObjectiveDefinition Collect(CollectibleDefinition item, int value) => new AdventureObjectiveDefinition { action = AdventureObjectiveAction.CollectItem, target = AdventureObjectiveTarget.Collectible, collectibleId = item.id, requiredAmount = value, displayLabel = item.displayName };
    private static AdventureObjectiveDefinition Obstacle(AdventureObjectiveTarget target, int value) => new AdventureObjectiveDefinition { action = AdventureObjectiveAction.DestroyObstacle, target = target, requiredAmount = value, displayLabel = target.ToString() };
    private static AdventureObjectiveDefinition Chain(int value) => new AdventureObjectiveDefinition { action = AdventureObjectiveAction.BreakChain, target = AdventureObjectiveTarget.Chain, requiredAmount = value, displayLabel = "Zincir" };

    private static DifficultyTier ToDifficulty(int group, int phase)
    {
        int score = group + (phase == 10 ? 2 : phase >= 8 ? 1 : 0);
        return score == 0 ? DifficultyTier.Easy : score <= 2 ? DifficultyTier.Medium : score <= 4 ? DifficultyTier.Hard : DifficultyTier.Expert;
    }
    private static string DifficultyLabel(int group, int phase) => ToDifficulty(group, phase).ToString() + " (planned)";
    private static string RhythmRole(int phase)
    {
        if (phase <= 3) return phase == 1 ? "Rahatlama / alışma" : "Alışma";
        if (phase <= 6) return "Yükselme";
        if (phase == 7) return "Nefes alma";
        if (phase <= 9) return "Hazırlık";
        return "Eşik";
    }
    private static bool UsesIce(StageTemplate template) => template == StageTemplate.Ice || template == StageTemplate.CollectIce || template == StageTemplate.ThresholdIce;
    private static bool UsesRock(StageTemplate template) => template == StageTemplate.RowsRock || template == StageTemplate.RowsRockChain;
    private static bool UsesChain(StageTemplate template) => template == StageTemplate.RowsChain || template == StageTemplate.RowsRockChain || template == StageTemplate.ThresholdChain;

    private static ObstacleTheme ToTheme(StageTemplate template)
    {
        if (UsesIce(template) && (UsesRock(template) || UsesChain(template))) return ObstacleTheme.Mixed;
        if (UsesIce(template)) return ObstacleTheme.Ice;
        if (UsesRock(template) && UsesChain(template)) return ObstacleTheme.Mixed;
        if (UsesRock(template)) return ObstacleTheme.Rock;
        if (UsesChain(template)) return ObstacleTheme.Chain;
        return ObstacleTheme.None;
    }
}
