using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AdventureContentValidationResult
{
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public readonly List<string> RiskNotes = new List<string>();

    public bool HasErrors => Errors.Count > 0;
}

public static class AdventureContentValidator
{
    private const int MaximumSupportedObjectives = 3;

    public static AdventureContentValidationResult ValidateLevel(
        LevelData level,
        CollectibleDatabase database,
        AdventureRowGenerationContext generationContext)
    {
        AdventureContentValidationResult result = new AdventureContentValidationResult();
        if (level == null)
        {
            result.Errors.Add("Adventure level data is missing.");
            return result;
        }

        if (generationContext == null)
        {
            result.Errors.Add("Adventure row generation context is missing.");
            return result;
        }

        IReadOnlyList<AdventureObjectiveDefinition> objectives = level.Objectives;
        if (objectives == null || objectives.Count == 0)
        {
            result.Errors.Add("Adventure level requires at least one objective.");
            return result;
        }

        if (objectives.Count > MaximumSupportedObjectives)
        {
            result.Errors.Add($"Adventure supports at most {MaximumSupportedObjectives} objectives; this level has {objectives.Count}.");
        }

        HashSet<string> collectibleIds = new HashSet<string>();
        for (int i = 0; i < objectives.Count; i++)
        {
            AdventureObjectiveDefinition objective = objectives[i];
            string label = $"Objective {i + 1}";
            if (objective == null)
            {
                result.Errors.Add($"{label} is missing.");
                continue;
            }

            if (objective.requiredAmount <= 0)
            {
                result.Errors.Add($"{label} requiredAmount must be greater than zero.");
            }

            switch (objective.action)
            {
                case AdventureObjectiveAction.ClearRows:
                    if (objective.target != AdventureObjectiveTarget.Rows)
                        result.Errors.Add($"{label}: ClearRows requires the Rows target.");
                    ValidateNoCollectibleId(objective, label, result);
                    break;

                case AdventureObjectiveAction.ReachScore:
                    if (objective.target != AdventureObjectiveTarget.Score)
                        result.Errors.Add($"{label}: ReachScore requires the Score target.");
                    ValidateNoCollectibleId(objective, label, result);
                    break;

                case AdventureObjectiveAction.CollectItem:
                    if (objective.target != AdventureObjectiveTarget.Collectible)
                        result.Errors.Add($"{label}: CollectItem requires the Collectible target.");

                    if (string.IsNullOrWhiteSpace(objective.collectibleId))
                    {
                        result.Errors.Add($"{label}: CollectItem requires a collectible ID.");
                        break;
                    }

                    if (database == null || database.GetById(objective.collectibleId) == null)
                    {
                        result.Errors.Add($"{label}: collectible ID '{objective.collectibleId}' was not found in the database.");
                    }

                    if (!collectibleIds.Add(objective.collectibleId))
                    {
                        result.Errors.Add($"{label}: duplicate CollectItem objective for collectible ID '{objective.collectibleId}'.");
                    }

                    CollectibleDefinition collectible = database != null ? database.GetById(objective.collectibleId) : null;
                    if (collectible != null && collectible.icon == null)
                    {
                        result.Warnings.Add($"{label}: collectible ID '{objective.collectibleId}' has no icon; the objective can run, but its HUD and carrier visual will be empty.");
                    }
                    break;

                case AdventureObjectiveAction.DestroyObstacle:
                    ValidateObstacleTarget(objective, label, generationContext, result);
                    ValidateNoCollectibleId(objective, label, result);
                    break;

                case AdventureObjectiveAction.BreakChain:
                    if (objective.target != AdventureObjectiveTarget.Chain)
                        result.Errors.Add($"{label}: BreakChain requires the Chain target.");
                    else if (generationContext.ChainedChance <= 0f)
                        result.Errors.Add($"{label}: Chain objective cannot be produced because chained block chance is zero.");
                    ValidateNoCollectibleId(objective, label, result);
                    break;

                default:
                    result.Errors.Add($"{label}: objective action '{objective.action}' is not supported at runtime.");
                    ValidateNoCollectibleId(objective, label, result);
                    break;
            }
        }

        if (collectibleIds.Count > 0)
        {
            result.RiskNotes.Add(
                "Collectibles use a small initial supply and continue on eligible carriers in later generated rows; the full objective amount no longer has to fit on the initial board.");
        }

        if (!generationContext.UseCustomSpawnRules &&
            (level.fireBlockChance > 0f || level.sliceBlockChance > 0f ||
             level.specialMechanicFocus == SpecialMechanicFocus.Fire ||
             level.specialMechanicFocus == SpecialMechanicFocus.Slice))
        {
            result.Warnings.Add(
                "Fire/Slice generation is effectively disabled because useCustomSpawnRules is false. Current row-generation semantics ignore Fire and Slice chances in this mode.");
        }

        float authoredSupportChance = level.fireBlockChance + level.sliceBlockChance;
        if (authoredSupportChance > 0.10f)
        {
            result.Warnings.Add(
                $"Combined authored Fire + Slice chance is {authoredSupportChance:P0}, above the normal Stage-authoring range of 10%. This reduces the Normal-block and collectible-carrier pool when custom spawn rules are active.");
        }

        AddLegacyObjectiveMismatchWarning(level, objectives, result);
        return result;
    }

    private static void ValidateObstacleTarget(
        AdventureObjectiveDefinition objective,
        string label,
        AdventureRowGenerationContext context,
        AdventureContentValidationResult result)
    {
        bool canProduce = objective.target == AdventureObjectiveTarget.AnyObstacle
            ? context.RockChance > 0f || context.FrozenChance > 0f || context.ChainedChance > 0f
            : objective.target == AdventureObjectiveTarget.Rock
                ? context.RockChance > 0f
                : objective.target == AdventureObjectiveTarget.Ice
                    ? context.FrozenChance > 0f
                    : objective.target == AdventureObjectiveTarget.Chain && context.ChainedChance > 0f;

        if (objective.target != AdventureObjectiveTarget.Rock &&
            objective.target != AdventureObjectiveTarget.Ice &&
            objective.target != AdventureObjectiveTarget.Chain &&
            objective.target != AdventureObjectiveTarget.AnyObstacle)
        {
            result.Errors.Add($"{label}: DestroyObstacle requires Rock, Ice, Chain, or AnyObstacle.");
        }
        else if (!canProduce)
        {
            result.Errors.Add($"{label}: obstacle target '{objective.target}' cannot be produced by this row configuration.");
        }
    }

    public static int CalculateMaximumInitialCollectibleCarriers(AdventureRowGenerationContext context)
    {
        if (context == null || context.GridWidth <= 1 || context.InitialRowCount <= 0 || CannotProduceEligibleNormalBlocks(context))
            return 0;

        int minimumWidth = context.UseCustomSpawnRules ? context.MinBlockSize : 1;
        int maximumBlocksPerRow = Mathf.Max(0, (context.GridWidth - 1) / Mathf.Max(1, minimumWidth));
        return context.InitialRowCount * maximumBlocksPerRow;
    }

    private static bool CannotProduceEligibleNormalBlocks(AdventureRowGenerationContext context)
    {
        if (context.RockChance >= 1f)
            return true;

        if (context.FireChance + context.SliceChance >= 1f)
            return true;

        if (context.ChainedChance >= 1f)
            return true;

        return context.FrozenChance >= 1f;
    }

    private static void ValidateNoCollectibleId(
        AdventureObjectiveDefinition objective,
        string label,
        AdventureContentValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(objective.collectibleId))
            result.Errors.Add($"{label}: collectible ID is only valid for CollectItem objectives.");
    }

    private static void AddLegacyObjectiveMismatchWarning(
        LevelData level,
        IReadOnlyList<AdventureObjectiveDefinition> objectives,
        AdventureContentValidationResult result)
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            AdventureObjectiveDefinition objective = objectives[i];
            if (objective == null || objective.action == AdventureObjectiveAction.CollectItem)
                continue;

            ObjectiveType expectedLegacyType = objective.action == AdventureObjectiveAction.ReachScore
                ? ObjectiveType.ReachScore
                : ObjectiveType.ClearRows;
            if (level.objectiveType != expectedLegacyType)
            {
                result.Warnings.Add(
                    $"V2 objectives are active, but legacy objectiveType is {level.objectiveType}; the first V2 objective implies {expectedLegacyType}. The V2 list remains authoritative.");
            }

            return;
        }
    }
}
