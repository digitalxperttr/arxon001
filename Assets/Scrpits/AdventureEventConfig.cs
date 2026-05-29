using UnityEngine;

[System.Serializable]
public class AdventureRewardMetadata
{
    public string rewardId;
    public string rewardDisplayName;
    [TextArea(2, 3)] public string rewardDescription;
}

[CreateAssetMenu(fileName = "AdventureEventConfig", menuName = "ARXON/Adventure/Event Config")]
public class AdventureEventConfig : ScriptableObject
{
    [Header("Event Identity")]
    public string eventName = "New Adventure Event";
    public string eventTheme = "Seasonal";
    [TextArea(2, 4)] public string eventDescription;

    [Header("Planned Window")]
    [Tooltip("Scheduling is not implemented yet. These are planning fields for content production.")]
    public string plannedStartDate;
    public string plannedEndDate;

    [Header("Content Shape")]
    [Min(1)] public int levelCount = 10;
    public DifficultyTier startingDifficulty = DifficultyTier.Easy;
    public DifficultyTier endingDifficulty = DifficultyTier.Hard;
    public ObstacleTheme featuredTheme = ObstacleTheme.None;
    public SpecialMechanicFocus featuredMechanic = SpecialMechanicFocus.None;
    public AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Rewards")]
    public AdventureRewardMetadata rewardMetadata = new AdventureRewardMetadata();

    [Header("Optional Authoring Set")]
    [Tooltip("Optional level configs that belong to this event pack.")]
    public AdventureLevelConfig[] levelConfigs;
}
