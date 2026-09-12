using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class AdventureEventGenerationSettings
{
    [Tooltip("Deterministic package seed. Changing it only affects a newly generated or explicitly regenerated level.")]
    public int seed = 20260908;
    [Tooltip("Algorithm contract used to reproduce this package.")]
    public string generatorVersion = AdventureEventLevelFactory.CurrentGeneratorVersion;
    [Min(1)] public int defaultLevelCount = 50;
    [Tooltip("Default per-row cap for incomplete Ice/Rock/Chain objective obstacles in newly generated Direct stages. Set 0 to leave the cap disabled.")]
    [Min(0)] public int defaultTargetObstaclePerRowLimit = 2;
    [Tooltip("Default active-board cap for each incomplete Ice/Rock/Chain objective type in newly generated Direct stages. Set 0 to leave the cap disabled.")]
    [Min(0)] public int defaultTargetObstacleActiveBoardLimit = 2;
}

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
    [Tooltip("Sabit insan-okunur event kimliği.")]
    public string eventId = AdventureIdentity.DefaultEventId;
    [Tooltip("Bu event içeriğinin authoring sürümü.")]
    public string contentVersion = AdventureIdentity.DefaultContentVersion;
    public string eventName = "New Adventure Event";
    public string eventTheme = "Seasonal";
    [TextArea(2, 4)] public string eventDescription;

    [Header("Event Visuals")]
    [FormerlySerializedAs("openingVisual")]
    [Tooltip("AdventureMap'te bölüm node'larının arkasında gösterilir. Eski Opening Visual atamaları uyumluluk için korunur; doğru harita görselini buraya atayın.")]
    public Sprite mapBackgroundVisual;
    [FormerlySerializedAs("backgroundVisual")]
    [Tooltip("Adventure bölümünün tahta ekranı için ayrılmış görsel. Haritaya uygulanmaz.")]
    public Sprite gameplayBackgroundVisual;

    [Header("Planned Window")]
    [Tooltip("Scheduling is not implemented yet. These are planning fields for content production.")]
    public string plannedStartDate;
    public string plannedEndDate;

    [Header("Content Shape")]
    [Min(1)] public int levelCount = 50;
    public DifficultyTier startingDifficulty = DifficultyTier.Easy;
    public DifficultyTier endingDifficulty = DifficultyTier.Hard;
    public ObstacleTheme featuredTheme = ObstacleTheme.None;
    public SpecialMechanicFocus featuredMechanic = SpecialMechanicFocus.None;
    public AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Rewards")]
    public AdventureRewardMetadata rewardMetadata = new AdventureRewardMetadata();

    [Header("Optional Authoring Set")]
    [Tooltip("Ordered, authored source assets. Runtime attempts are resolved snapshots and never write progress back here.")]
    public AdventureLevelConfig[] levelConfigs;

    [Header("Local Generation")]
    [Tooltip("Only used by the local editor package generator. Runtime uses the saved level configs above.")]
    public AdventureEventGenerationSettings generationSettings = new AdventureEventGenerationSettings();
    [Tooltip("The existing shared database used to select valid collectible objective IDs.")]
    public CollectibleDatabase collectibleDatabase;

    public bool HasUsableLevelList => levelConfigs != null && levelConfigs.Length > 0;
}
