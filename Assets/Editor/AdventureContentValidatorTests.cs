using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AdventureContentValidatorTests
{
    [Test]
    public void ValidOneObjectiveLevelHasNoErrors()
    {
        AdventureContentValidationResult result = Validate(CreateLevel(Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 5)));

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void ValidThreeObjectiveLevelHasNoErrors()
    {
        LevelData level = CreateLevel(
            Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 5),
            Objective(AdventureObjectiveAction.ReachScore, AdventureObjectiveTarget.Score, 500),
            Objective(AdventureObjectiveAction.CollectItem, AdventureObjectiveTarget.Collectible, 2, "CR_BLUE"));

        AdventureContentValidationResult result = Validate(level);

        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void MoreThanThreeObjectivesIsRejected()
    {
        AdventureContentValidationResult result = Validate(CreateLevel(
            Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 1),
            Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 1),
            Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 1),
            Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 1)));

        Assert.That(result.Errors, Has.Some.Contains("at most 3 objectives"));
    }

    [Test]
    public void DuplicateCollectibleIdIsRejected()
    {
        AdventureContentValidationResult result = Validate(CreateLevel(
            Objective(AdventureObjectiveAction.CollectItem, AdventureObjectiveTarget.Collectible, 1, "CR_BLUE"),
            Objective(AdventureObjectiveAction.CollectItem, AdventureObjectiveTarget.Collectible, 1, "CR_BLUE")));

        Assert.That(result.Errors, Has.Some.Contains("duplicate CollectItem"));
    }

    [Test]
    public void UnsupportedObjectiveIsRejected()
    {
        AdventureContentValidationResult result = Validate(CreateLevel(
            Objective(AdventureObjectiveAction.ComboTarget, AdventureObjectiveTarget.Combo, 1)));

        Assert.That(result.Errors, Has.Some.Contains("not supported"));
    }

    [Test]
    public void MissingOrUnknownCollectibleIsRejected()
    {
        AdventureContentValidationResult missingId = Validate(CreateLevel(
            Objective(AdventureObjectiveAction.CollectItem, AdventureObjectiveTarget.Collectible, 1)));
        AdventureContentValidationResult unknownId = Validate(CreateLevel(
            Objective(AdventureObjectiveAction.CollectItem, AdventureObjectiveTarget.Collectible, 1, "UNKNOWN")));

        Assert.That(missingId.Errors, Has.Some.Contains("requires a collectible ID"));
        Assert.That(unknownId.Errors, Has.Some.Contains("was not found"));
    }

    [Test]
    public void CollectibleRequirementDoesNotHaveToFitOnInitialBoard()
    {
        AdventureContentValidationResult result = Validate(CreateLevel(
            Objective(AdventureObjectiveAction.CollectItem, AdventureObjectiveTarget.Collectible, 36, "CR_BLUE")));

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.RiskNotes, Has.Some.Contains("full objective amount no longer has to fit"));
    }

    [Test]
    public void FireSliceMismatchWarnsWithoutError()
    {
        LevelData level = CreateLevel(Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 1));
        level.fireBlockChance = 0.25f;
        level.specialMechanicFocus = SpecialMechanicFocus.Fire;

        AdventureContentValidationResult result = Validate(level);

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Warnings, Has.Some.Contains("effectively disabled"));
    }

    [Test]
    public void LegacyObjectiveMismatchWarnsWithoutError()
    {
        LevelData level = CreateLevel(Objective(AdventureObjectiveAction.ClearRows, AdventureObjectiveTarget.Rows, 1));
        level.objectiveType = ObjectiveType.ReachScore;

        AdventureContentValidationResult result = Validate(level);

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Warnings, Has.Some.Contains("legacy objectiveType"));
    }

    private static AdventureContentValidationResult Validate(LevelData level)
    {
        return AdventureContentValidator.ValidateLevel(
            level,
            CreateDatabase(),
            new AdventureRowGenerationContext(8, level, new[] { Color.red }, 5));
    }

    private static LevelData CreateLevel(params AdventureObjectiveDefinition[] objectives)
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        level.objectiveType = ObjectiveType.ClearRows;
        SetRuntimeObjectives(level, objectives);
        return level;
    }

    private static AdventureObjectiveDefinition Objective(
        AdventureObjectiveAction action,
        AdventureObjectiveTarget target,
        int requiredAmount,
        string collectibleId = "")
    {
        return new AdventureObjectiveDefinition
        {
            action = action,
            target = target,
            requiredAmount = requiredAmount,
            collectibleId = collectibleId
        };
    }

    private static CollectibleDatabase CreateDatabase()
    {
        CollectibleDatabase database = ScriptableObject.CreateInstance<CollectibleDatabase>();
        database.collectibles = new List<CollectibleDefinition>
        {
            new CollectibleDefinition { id = "CR_BLUE" }
        };
        return database;
    }

    private static void SetRuntimeObjectives(LevelData level, IEnumerable<AdventureObjectiveDefinition> objectives)
    {
        FieldInfo field = typeof(LevelData).GetField("runtimeObjectives", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(level, new List<AdventureObjectiveDefinition>(objectives));
    }
}
