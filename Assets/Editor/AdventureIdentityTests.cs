using NUnit.Framework;
using UnityEngine;

public class AdventureIdentityTests
{
    [Test]
    public void TwoEventsCanBothContainDisplayedLevelOneWithoutCollision()
    {
        AdventureContentIdentity first = new AdventureContentIdentity("event-a", "1", "a-level-1", 1);
        AdventureContentIdentity second = new AdventureContentIdentity("event-b", "1", "b-level-1", 1);
        AdventureEventProgressLedger ledger = new AdventureEventProgressLedger();

        ledger.RecordCompletion(first, 2, false);

        Assert.AreEqual(2, ledger.GetHighestUnlockedPosition("event-a"));
        Assert.AreEqual(1, ledger.GetHighestUnlockedPosition("event-b"));
        Assert.IsFalse(first.MatchesEventContent(second));
    }

    [Test]
    public void ReorderingDoesNotChangeExplicitLevelIdentity()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.eventId = "event-a";
        config.contentVersion = "2";
        config.levelId = "stable-boss";
        config.displayedLevelNumber = 4;

        AdventureContentIdentity before = AdventureIdentity.FromConfig(config, 1);
        AdventureContentIdentity after = AdventureIdentity.FromConfig(config, 19);

        Assert.AreEqual(before.LevelId, after.LevelId);
        Assert.AreEqual(before.DisplayedLevelNumber, after.DisplayedLevelNumber);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void DisplayedStageNumberIsAuthoritativeForIdentityAndRuntimeLevel()
    {
        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.authoringMode = AdventureStageAuthoringMode.DirectRecipeAuthoring;
        config.displayedLevelNumber = 7;
        config.levelNumber = 2;
        config.objectives.Add(new AdventureObjectiveDefinition
        {
            action = AdventureObjectiveAction.ClearRows,
            requiredAmount = 1
        });

        AdventureContentIdentity identity = AdventureIdentity.FromConfig(config, 1);
        LevelData runtimeLevel = AdventureLevelGenerator.GenerateRuntimeLevel(config);

        Assert.AreEqual(7, identity.DisplayedLevelNumber);
        Assert.NotNull(runtimeLevel);
        Assert.AreEqual(7, runtimeLevel.levelNumber);
        Object.DestroyImmediate(runtimeLevel);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void RetryKeepsTheSameSnapshotIdentityAndSeed()
    {
        AdventureContentIdentity identity = new AdventureContentIdentity("event-a", "1", "level-1", 1);
        LevelData runtime = ScriptableObject.CreateInstance<LevelData>();
        AdventureAttemptSnapshot attempt = new AdventureAttemptSnapshot(identity, runtime, 12345);

        AdventureAttemptSnapshot retry = attempt;

        Assert.AreSame(attempt.RuntimeLevel, retry.RuntimeLevel);
        Assert.AreEqual(attempt.AttemptSeed, retry.AttemptSeed);
        Assert.AreSame(attempt.Identity, retry.Identity);
        Object.DestroyImmediate(runtime);
    }

    [Test]
    public void NextLevelStaysInEventContentAndUsesNewSeed()
    {
        AdventureContentIdentity current = new AdventureContentIdentity("event-a", "3", "level-1", 1);
        AdventureContentIdentity next = new AdventureContentIdentity("event-a", "3", "level-2", 2);
        AdventureAttemptSnapshot first = new AdventureAttemptSnapshot(current, ScriptableObject.CreateInstance<LevelData>(), 10);
        AdventureAttemptSnapshot second = new AdventureAttemptSnapshot(next, ScriptableObject.CreateInstance<LevelData>(), 11);

        Assert.IsTrue(first.Identity.MatchesEventContent(second.Identity));
        Assert.Greater(second.Identity.DisplayedLevelNumber, first.Identity.DisplayedLevelNumber);
        Assert.AreNotEqual(first.AttemptSeed, second.AttemptSeed);
        Object.DestroyImmediate(first.RuntimeLevel);
        Object.DestroyImmediate(second.RuntimeLevel);
    }

    [Test]
    public void VictoryCreditsStartedAttemptRatherThanLaterSelection()
    {
        AdventureEventProgressLedger ledger = new AdventureEventProgressLedger();
        AdventureContentIdentity started = new AdventureContentIdentity("event-a", "1", "a-level-1", 1);
        AdventureContentIdentity laterSelection = new AdventureContentIdentity("event-b", "1", "b-level-1", 1);

        ledger.RecordCompletion(started, 2, false);

        Assert.AreEqual(2, ledger.GetHighestUnlockedPosition(started.EventId));
        Assert.AreEqual(1, ledger.GetHighestUnlockedPosition(laterSelection.EventId));
    }

    [Test]
    public void FinalLevelCompletionIsDifferentFromBeingUnlocked()
    {
        AdventureEventProgressLedger ledger = new AdventureEventProgressLedger();
        AdventureContentIdentity finalLevel = new AdventureContentIdentity("event-a", "1", "final", 50);

        ledger.RecordCompletion(finalLevel, 0, true);

        Assert.IsTrue(ledger.GetOrCreate("event-a").isCompleted);
        Assert.IsTrue(ledger.IsLevelCompleted("event-a", "final"));
        Assert.AreEqual(1, ledger.GetHighestUnlockedPosition("event-a"));
    }

    [Test]
    public void LegacyHighestUnlockedMigrationPreservesProgress()
    {
        AdventureEventProgressLedger migrated = AdventureEventProgressLedger.FromLegacyHighestUnlocked(17);

        Assert.AreEqual(17, migrated.GetHighestUnlockedPosition(AdventureIdentity.DefaultEventId));
    }
}
