using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    private const int FallbackMaxAdventureLevel = 50;
    private const string LegacyHighestLevelUnlockedKey = "HighestLevelUnlocked";
    private const string EventProgressKey = "AdventureEventProgressV1";
    private const string EventProgressMigrationKey = "AdventureEventProgressMigratedV1";

    [Header("Legacy Adventure Levels")]
    public LevelData[] allLevels;
    [Header("Generated Adventure Levels")]
    public AdventureLevelConfig[] generatedAdventureLevels;
    [Header("Local Event Source")]
    [Tooltip("Optional local content source. It takes precedence over the legacy arrays but never mutates an already-open attempt snapshot.")]
    [SerializeField] private AdventureEventConfig selectedLocalEvent;

    // Compatibility views for existing UI. Gameplay must use CurrentAdventureAttempt.
    public LevelData currentSelectedLevel { get; private set; }
    public AdventureLevelConfig currentSelectedAdventureConfig { get; private set; }
    public int currentSelectedLevelNumber { get; private set; }
    public int CurrentAdventureAttemptSeed => CurrentAdventureAttempt != null ? CurrentAdventureAttempt.AttemptSeed : 0;
    public AdventureAttemptSnapshot CurrentAdventureAttempt { get; private set; }

    private AdventureEventProgressLedger eventProgress = new AdventureEventProgressLedger();
    private int adventureSeedSequence;
    public static ProgressManager Instance;
    public int highestLevelUnlocked => GetHighestUnlockedForEvent(GetCurrentEventId());
    public AdventureEventConfig SelectedLocalEvent => selectedLocalEvent;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
        }
        else Destroy(gameObject);
    }

    public int GetHighestUnlockedForEvent(string eventId) => eventProgress.GetHighestUnlockedPosition(eventId);
    public bool IsAdventureLevelCompleted(string eventId, string levelId) => eventProgress.IsLevelCompleted(eventId, levelId);

    public void CompleteAdventureAttempt(AdventureAttemptSnapshot attempt)
    {
        if (attempt == null || attempt.Identity == null)
        {
            Debug.LogError("[Adventure] Attempt snapshot missing; progress was not credited.");
            return;
        }

        bool hasNext = TryFindNextPosition(attempt.Identity, out int nextPosition);
        eventProgress.RecordCompletion(attempt.Identity, hasNext ? nextPosition : 0, !hasNext);
        SaveProgress();
        Debug.Log($"[Adventure] Completion credited to event={attempt.Identity.EventId}, content={attempt.Identity.ContentVersion}, level={attempt.Identity.LevelId}, display={attempt.Identity.DisplayedLevelNumber}.");
    }

    // Compatibility entry point. The lifecycle passes its captured attempt explicitly.
    public void UnlockNextLevel() => CompleteAdventureAttempt(CurrentAdventureAttempt);

    public void ResetProgress()
    {
        eventProgress = new AdventureEventProgressLedger();
        PlayerPrefs.DeleteKey(EventProgressKey);
        PlayerPrefs.DeleteKey(EventProgressMigrationKey);
        PlayerPrefs.DeleteKey(LegacyHighestLevelUnlockedKey);
        SaveProgress();
        Debug.LogWarning("Adventure ilerlemesi sıfırlandı.");
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetString(EventProgressKey, JsonUtility.ToJson(eventProgress));
        PlayerPrefs.SetInt(EventProgressMigrationKey, 1);
        // Compatibility mirror for the map fallback path.
        PlayerPrefs.SetInt(LegacyHighestLevelUnlockedKey, highestLevelUnlocked);
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        string serializedProgress = PlayerPrefs.GetString(EventProgressKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(serializedProgress))
        {
            AdventureEventProgressLedger loaded = JsonUtility.FromJson<AdventureEventProgressLedger>(serializedProgress);
            if (loaded != null) eventProgress = loaded;
        }

        if (PlayerPrefs.GetInt(EventProgressMigrationKey, 0) == 0)
        {
            int legacyHighest = Mathf.Max(1, PlayerPrefs.GetInt(LegacyHighestLevelUnlockedKey, 1));
            eventProgress = AdventureEventProgressLedger.FromLegacyHighestUnlocked(legacyHighest);
            SaveProgress();
            Debug.Log($"[Adventure] Migrated legacy HighestLevelUnlocked={legacyHighest} to event '{AdventureIdentity.DefaultEventId}'.");
        }
    }

    public int GetAdventureLevelCount()
    {
        if (selectedLocalEvent != null && selectedLocalEvent.levelConfigs != null)
            return selectedLocalEvent.levelConfigs.Length;
        return Mathf.Max(allLevels != null ? allLevels.Length : 0, generatedAdventureLevels != null ? generatedAdventureLevels.Length : 0);
    }

    // Local source seam: a future remote catalog can supply a validated AdventureEventConfig without changing gameplay selection.
    public bool TrySelectLocalEvent(AdventureEventConfig eventConfig)
    {
        if (eventConfig == null || eventConfig.levelConfigs == null || eventConfig.levelConfigs.Length == 0)
        {
            Debug.LogError("[Adventure] Local event selection requires a non-empty ordered level list.");
            return false;
        }

        if (CurrentAdventureAttempt != null && CurrentAdventureAttempt.Identity != null &&
            !CurrentAdventureAttempt.Identity.EventId.Equals(eventConfig.eventId))
        {
            Debug.LogWarning("[Adventure] Local event was not changed while an attempt snapshot is open. Clear or finish the current attempt first.");
            return false;
        }

        selectedLocalEvent = eventConfig;
        ClearAdventureSelection();
        return true;
    }

    // Map presentation selects an ordered position; position is never persisted as level identity.
    public bool TrySelectAdventureLevel(int displayedPosition)
    {
        int levelIndex = displayedPosition - 1;
        return levelIndex >= 0 && levelIndex < GetAdventureLevelCount() && TryBeginAdventureAttemptAtIndex(levelIndex);
    }

    public bool TryStartNextAdventureAttempt(AdventureAttemptSnapshot completedAttempt)
    {
        return completedAttempt != null && completedAttempt.Identity != null &&
            TryFindNextIndex(completedAttempt.Identity, out int nextIndex) &&
            TryBeginAdventureAttemptAtIndex(nextIndex);
    }

    public bool HasNextAdventureLevel(AdventureAttemptSnapshot attempt)
    {
        return attempt != null && attempt.Identity != null && TryFindNextIndex(attempt.Identity, out _);
    }

    public bool HasAdventureLevel(int displayedPosition)
    {
        int levelIndex = displayedPosition - 1;
        return levelIndex >= 0 && levelIndex < GetAdventureLevelCount() && GetIdentityAtIndex(levelIndex) != null;
    }

    public void ClearAdventureSelection()
    {
        currentSelectedLevel = null;
        currentSelectedAdventureConfig = null;
        currentSelectedLevelNumber = 0;
        CurrentAdventureAttempt = null;
    }

    // Retry restores the attempt captured by the running LevelManager, never a later UI selection.
    public void RestoreAdventureAttempt(AdventureAttemptSnapshot attempt)
    {
        if (attempt == null || attempt.Identity == null || attempt.RuntimeLevel == null)
            return;

        CurrentAdventureAttempt = attempt;
        currentSelectedLevel = attempt.RuntimeLevel;
        currentSelectedAdventureConfig = null;
        currentSelectedLevelNumber = attempt.Identity.DisplayedLevelNumber;
    }

    public AdventureGameplayRng CreateAdventureGameplayRng(AdventureAttemptSnapshot attempt)
    {
        if (attempt == null || attempt.RuntimeLevel == null || attempt.AttemptSeed == 0)
        {
            Debug.LogError("[Adventure] Gameplay RNG requested without an attempt snapshot.");
            return null;
        }
        return new AdventureGameplayRng(attempt.AttemptSeed);
    }

    public AdventureGameplayRng CreateAdventureGameplayRng() => CreateAdventureGameplayRng(CurrentAdventureAttempt);

    private bool TryBeginAdventureAttemptAtIndex(int levelIndex)
    {
        AdventureLevelConfig resolvedConfig;
        LevelData resolvedLevel = ResolveAdventureLevel(levelIndex, out resolvedConfig);
        AdventureContentIdentity identity = GetIdentityAtIndex(levelIndex);
        if (resolvedLevel == null || identity == null)
        {
            Debug.LogError($"[Adventure] Ordered position {levelIndex + 1} could not be resolved.");
            return false;
        }

        resolvedLevel.levelNumber = identity.DisplayedLevelNumber;
        CurrentAdventureAttempt = new AdventureAttemptSnapshot(identity, resolvedLevel, CreateNewAttemptSeed(identity));
        currentSelectedLevel = resolvedLevel;
        currentSelectedAdventureConfig = resolvedConfig;
        currentSelectedLevelNumber = identity.DisplayedLevelNumber;
        return true;
    }

    private int CreateNewAttemptSeed(AdventureContentIdentity identity)
    {
        adventureSeedSequence++;
        long ticks = System.DateTime.UtcNow.Ticks;
        int seed = unchecked((int)(ticks ^ (ticks >> 32) ^ ((long)identity.DisplayedLevelNumber << 16) ^ adventureSeedSequence));
        return seed == 0 ? 1 : seed;
    }

    private bool TryFindNextPosition(AdventureContentIdentity identity, out int nextPosition)
    {
        nextPosition = 0;
        if (!TryFindNextIndex(identity, out int nextIndex)) return false;
        AdventureContentIdentity nextIdentity = GetIdentityAtIndex(nextIndex);
        nextPosition = nextIdentity != null ? nextIdentity.DisplayedLevelNumber : 0;
        return nextPosition > 0;
    }

    private bool TryFindNextIndex(AdventureContentIdentity identity, out int nextIndex)
    {
        nextIndex = -1;
        int nextDisplay = int.MaxValue;
        for (int index = 0; index < GetAdventureLevelCount(); index++)
        {
            AdventureContentIdentity candidate = GetIdentityAtIndex(index);
            if (candidate == null || !identity.MatchesEventContent(candidate) || candidate.DisplayedLevelNumber <= identity.DisplayedLevelNumber)
                continue;
            if (candidate.DisplayedLevelNumber < nextDisplay)
            {
                nextDisplay = candidate.DisplayedLevelNumber;
                nextIndex = index;
            }
        }
        return nextIndex >= 0;
    }

    private AdventureContentIdentity GetIdentityAtIndex(int levelIndex)
    {
        int fallbackPosition = levelIndex + 1;
        if (selectedLocalEvent != null && selectedLocalEvent.levelConfigs != null &&
            levelIndex >= 0 && levelIndex < selectedLocalEvent.levelConfigs.Length && selectedLocalEvent.levelConfigs[levelIndex] != null)
            return AdventureIdentity.FromConfig(selectedLocalEvent.levelConfigs[levelIndex], fallbackPosition);
        if (generatedAdventureLevels != null && levelIndex >= 0 && levelIndex < generatedAdventureLevels.Length && generatedAdventureLevels[levelIndex] != null)
            return AdventureIdentity.FromConfig(generatedAdventureLevels[levelIndex], fallbackPosition);
        if (allLevels != null && levelIndex >= 0 && levelIndex < allLevels.Length && allLevels[levelIndex] != null)
            return AdventureIdentity.FromLegacyLevel(allLevels[levelIndex], fallbackPosition);
        return null;
    }

    private LevelData ResolveAdventureLevel(int levelIndex, out AdventureLevelConfig resolvedConfig)
    {
        resolvedConfig = null;
        if (selectedLocalEvent != null && selectedLocalEvent.levelConfigs != null &&
            levelIndex >= 0 && levelIndex < selectedLocalEvent.levelConfigs.Length && selectedLocalEvent.levelConfigs[levelIndex] != null)
        {
            resolvedConfig = selectedLocalEvent.levelConfigs[levelIndex];
            return AdventureLevelGenerator.GenerateRuntimeLevel(resolvedConfig);
        }
        if (generatedAdventureLevels != null && levelIndex >= 0 && levelIndex < generatedAdventureLevels.Length && generatedAdventureLevels[levelIndex] != null)
        {
            resolvedConfig = generatedAdventureLevels[levelIndex];
            return AdventureLevelGenerator.GenerateRuntimeLevel(resolvedConfig);
        }
        if (allLevels != null && levelIndex >= 0 && levelIndex < allLevels.Length)
            return AdventureLevelGenerator.CopyLegacyRuntimeLevel(allLevels[levelIndex]);
        return null;
    }

    private string GetCurrentEventId()
    {
        return CurrentAdventureAttempt != null && CurrentAdventureAttempt.Identity != null
            ? CurrentAdventureAttempt.Identity.EventId
            : selectedLocalEvent != null && !string.IsNullOrWhiteSpace(selectedLocalEvent.eventId)
                ? selectedLocalEvent.eventId
                : AdventureIdentity.DefaultEventId;
    }
}
