using System;
using System.Collections.Generic;
using UnityEngine;

public static class AdventureIdentity
{
    public const string DefaultEventId = "default-adventure";
    public const string DefaultContentVersion = "1";

    public static AdventureContentIdentity FromConfig(AdventureLevelConfig config, int fallbackPosition)
    {
        if (config == null)
            return null;

        string eventId = string.IsNullOrWhiteSpace(config.eventId) ? DefaultEventId : config.eventId.Trim();
        string contentVersion = string.IsNullOrWhiteSpace(config.contentVersion) ? DefaultContentVersion : config.contentVersion.Trim();
        string levelId = string.IsNullOrWhiteSpace(config.levelId)
            ? "legacy-config-" + config.name
            : config.levelId.Trim();
        int displayedLevelNumber = config.GetStageNumber();

        return new AdventureContentIdentity(eventId, contentVersion, levelId, displayedLevelNumber);
    }

    public static AdventureContentIdentity FromLegacyLevel(LevelData level, int fallbackPosition)
    {
        string levelId = level != null && !string.IsNullOrWhiteSpace(level.name)
            ? "legacy-level-" + level.name
            : "legacy-level-" + fallbackPosition;
        int displayedLevelNumber = level != null && level.levelNumber > 0 ? level.levelNumber : fallbackPosition;
        return new AdventureContentIdentity(DefaultEventId, DefaultContentVersion, levelId, displayedLevelNumber);
    }
}

public sealed class AdventureContentIdentity
{
    public string EventId { get; }
    public string ContentVersion { get; }
    public string LevelId { get; }
    public int DisplayedLevelNumber { get; }

    public AdventureContentIdentity(string eventId, string contentVersion, string levelId, int displayedLevelNumber)
    {
        EventId = eventId;
        ContentVersion = contentVersion;
        LevelId = levelId;
        DisplayedLevelNumber = displayedLevelNumber;
    }

    public bool MatchesEventContent(AdventureContentIdentity other)
    {
        return other != null && EventId == other.EventId && ContentVersion == other.ContentVersion;
    }
}

public sealed class AdventureAttemptSnapshot
{
    public const string Mode = "Adventure";

    public AdventureContentIdentity Identity { get; }
    public LevelData RuntimeLevel { get; }
    public int AttemptSeed { get; }

    public AdventureAttemptSnapshot(AdventureContentIdentity identity, LevelData runtimeLevel, int attemptSeed)
    {
        Identity = identity;
        RuntimeLevel = runtimeLevel;
        AttemptSeed = attemptSeed;
    }
}

[Serializable]
public class AdventureEventProgress
{
    public string eventId;
    public int highestUnlockedPosition = 1;
    public bool isCompleted;
    public List<string> completedLevelIds = new List<string>();
}

[Serializable]
public class AdventureEventProgressLedger
{
    [SerializeField] private List<AdventureEventProgress> events = new List<AdventureEventProgress>();

    public AdventureEventProgress GetOrCreate(string eventId)
    {
        if (events == null)
            events = new List<AdventureEventProgress>();

        string normalizedEventId = string.IsNullOrWhiteSpace(eventId) ? AdventureIdentity.DefaultEventId : eventId;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i] != null && events[i].eventId == normalizedEventId)
                return events[i];
        }

        AdventureEventProgress progress = new AdventureEventProgress { eventId = normalizedEventId };
        events.Add(progress);
        return progress;
    }

    public static AdventureEventProgressLedger FromLegacyHighestUnlocked(int legacyHighestUnlocked)
    {
        AdventureEventProgressLedger ledger = new AdventureEventProgressLedger();
        ledger.GetOrCreate(AdventureIdentity.DefaultEventId).highestUnlockedPosition = Mathf.Max(1, legacyHighestUnlocked);
        return ledger;
    }

    public int GetHighestUnlockedPosition(string eventId)
    {
        return Mathf.Max(1, GetOrCreate(eventId).highestUnlockedPosition);
    }

    public void RecordCompletion(AdventureContentIdentity identity, int nextDisplayedLevelNumber, bool isFinalLevel)
    {
        if (identity == null)
            return;

        AdventureEventProgress progress = GetOrCreate(identity.EventId);
        if (!progress.completedLevelIds.Contains(identity.LevelId))
            progress.completedLevelIds.Add(identity.LevelId);

        if (nextDisplayedLevelNumber > 0)
            progress.highestUnlockedPosition = Mathf.Max(progress.highestUnlockedPosition, nextDisplayedLevelNumber);

        if (isFinalLevel)
            progress.isCompleted = true;
    }

    public bool IsLevelCompleted(string eventId, string levelId)
    {
        return GetOrCreate(eventId).completedLevelIds.Contains(levelId);
    }
}
