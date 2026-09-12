using System.Collections.Generic;
using UnityEngine;

public class LevelData : ScriptableObject
{
    // Resolved once on selection. Progress belongs to ObjectiveManager, never this snapshot.
    [SerializeField, HideInInspector] private List<AdventureObjectiveDefinition> runtimeObjectives = new List<AdventureObjectiveDefinition>();
    public IReadOnlyList<AdventureObjectiveDefinition> Objectives => runtimeObjectives.AsReadOnly();
    public bool IsRuntimeLevel { get; private set; }

    internal void CopyRuntimeObjectives(IReadOnlyList<AdventureObjectiveDefinition> definitions)
    {
        runtimeObjectives = new List<AdventureObjectiveDefinition>(definitions.Count);
        for (int i = 0; i < definitions.Count; i++)
        {
            AdventureObjectiveDefinition source = definitions[i];
            runtimeObjectives.Add(new AdventureObjectiveDefinition
            {
                action = source.action, target = source.target, requiredAmount = source.requiredAmount,
                collectibleId = source.collectibleId, displayLabel = source.displayLabel, displayIcon = source.displayIcon
            });
        }
        IsRuntimeLevel = true;
    }

    public static bool ValidateObjectives(IReadOnlyList<AdventureObjectiveDefinition> definitions, out string error)
    {
        error = null;
        if (definitions == null || definitions.Count == 0)
            error = "Adventure bölümünde en az bir hedef bulunmalı.";
        else for (int i = 0; i < definitions.Count; i++)
        {
            AdventureObjectiveDefinition objective = definitions[i];
            if (objective == null || objective.requiredAmount <= 0)
                error = $"Hedef {i + 1}: tanım eksik veya hedef miktarı sıfırdan küçük/eşit.";
            else switch (objective.action)
            {
                case AdventureObjectiveAction.ClearRows:
                    if (objective.target != AdventureObjectiveTarget.Rows) error = $"Hedef {i + 1}: ClearRows için Rows seçilmeli.";
                    break;
                case AdventureObjectiveAction.ReachScore:
                    if (objective.target != AdventureObjectiveTarget.Score) error = $"Hedef {i + 1}: ReachScore için Score seçilmeli.";
                    break;
                case AdventureObjectiveAction.CollectItem:
                    if (objective.target != AdventureObjectiveTarget.Collectible || string.IsNullOrWhiteSpace(objective.collectibleId))
                        error = $"Hedef {i + 1}: Collectible türü ve geçerli collectible ID gerekli.";
                    break;
                case AdventureObjectiveAction.DestroyObstacle:
                    if (objective.target != AdventureObjectiveTarget.Rock &&
                        objective.target != AdventureObjectiveTarget.Ice &&
                        objective.target != AdventureObjectiveTarget.Chain &&
                        objective.target != AdventureObjectiveTarget.AnyObstacle)
                        error = $"Hedef {i + 1}: DestroyObstacle için Rock, Ice, Chain veya AnyObstacle seçilmeli.";
                    break;
                case AdventureObjectiveAction.BreakChain:
                    if (objective.target != AdventureObjectiveTarget.Chain)
                        error = $"Hedef {i + 1}: BreakChain için Chain seçilmeli.";
                    break;
                default:
                    error = $"Hedef {i + 1}: {objective.action} henüz desteklenmiyor; bölüm başlatılamaz.";
                    break;
            }
            if (error == null && objective.action != AdventureObjectiveAction.CollectItem && !string.IsNullOrWhiteSpace(objective.collectibleId))
                error = $"Hedef {i + 1}: collectible ID yalnız CollectItem hedefinde kullanılabilir.";
            if (error != null) return false;
        }
        return error == null;
    }

    public bool ValidateRuntime(CollectibleDatabase database, out string error)
    {
        if (!IsRuntimeLevel)
        {
            error = "Adventure seçimi çözümlenmiş runtime bölüm verisi içermiyor.";
            return false;
        }
        if (!ValidateObjectives(runtimeObjectives, out error)) return false;
        if (levelNumber < 1 || (hasMoveLimit && moveLimit < 1) || isEndless ||
            openingTargetObstacleLimit < 0 || targetObstaclePerRowLimit < 0 || targetObstacleActiveBoardLimit < 0 ||
            minBlockSize < 1 || maxBlockSize > 4 || minBlockSize > maxBlockSize ||
            !IsChance(baseGapChance) || !IsChance(largeBlockChance) || !IsChance(frozenBlockChance) ||
            !IsChance(rockBlockChance) || !IsChance(chainedBlockChance) || !IsChance(fireBlockChance) ||
            !IsChance(sliceBlockChance) || !IsChance(fogCoveragePercent) || !System.Enum.IsDefined(typeof(FogDensity), fogDensity))
        {
            error = "Adventure runtime profilinde geçersiz hamle, genişlik, sis veya olasılık değeri var.";
            return false;
        }
        foreach (AdventureObjectiveDefinition objective in runtimeObjectives)
            if (objective.action == AdventureObjectiveAction.CollectItem && (database == null || database.GetById(objective.collectibleId) == null))
            {
                error = $"Collectible bulunamadı: {objective.collectibleId}. Adventure GridManager veritabanını kontrol edin.";
                return false;
            }
        return true;
    }

    private static bool IsChance(float value) => !float.IsNaN(value) && value >= 0f && value <= 1f;
    [Header("Bölüm Bilgileri")]
    public int levelNumber;

    [Header("Adventure Metadata")]
    public ObjectiveType objectiveType = ObjectiveType.ClearRows;
    public SpecialMechanicFocus specialMechanicFocus = SpecialMechanicFocus.None;
    public int targetObstacleCount = 0;
    public int targetComboCount = 0;
   
    [Header("Bölüm Hedefleri")]
    public int targetScore = 0;       // 0 ise bu hedeften muaf demektir
    public int targetLines = 0;       // 0 ise bu hedeften muaf demektir
    
    [Header("Kısıtlamalar")]
    public bool hasMoveLimit = true;
    public int moveLimit = 30;        // Oyuncunun kaç hamle hakkı var?
    public bool isEndless = false;    // True ise klasik mod gibi sonsuz oynanır
    
    [Header("Zorluk Ayarları")]
    [Range(0f, 1f)] public float baseGapChance = 0.4f;     // Boşluk çıkma ihtimali
    [Range(0f, 1f)] public float largeBlockChance = 0.1f;  // 4'lü dev blok çıkma ihtimali
    [HideInInspector] public int openingTargetObstacleLimit = 0;
    [HideInInspector] public int targetObstaclePerRowLimit = 0;
    [HideInInspector] public int targetObstacleActiveBoardLimit = 0;
    [Range(0f, 1f)] public float frozenBlockChance = 0f;   // Buzlu blok çıkma ihtimali
    [Range(0f, 1f)] public float rockBlockChance = 0.0f;     // YENİ: Kaya blok çıkma ihtimali
    [Range(0f, 1f)] public float chainedBlockChance = 0f;  // YENİ: Zincirli blok çıkma ihtimali

    [Header("Özel Spawn Ayarları")]
    public bool useCustomSpawnRules = false;
    [Min(1)] public int minBlockSize = 1;
    [Min(1)] public int maxBlockSize = 4;
    [Range(0f, 1f)] public float sliceBlockChance = 0f;
    [Range(0f, 1f)] public float fireBlockChance = 0f;

    [Header("Sis (Fog) Ayarları")]
    public FogDensity fogDensity = FogDensity.None;
    [Range(0f, 1f)] public float fogCoveragePercent = 0f;
    public int fogStartingRow = -1; // -1 ise sis yok demektir. Örn 4 yazarsan 4. satır ve yukarısı zifiri karanlık başlar.
}
