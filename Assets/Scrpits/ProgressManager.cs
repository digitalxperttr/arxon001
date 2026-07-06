using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    private const int FallbackMaxAdventureLevel = 50;

    [Header("Legacy Adventure Levels")]
    public LevelData[] allLevels; // Eski el yapımı Adventure level assetleri

    [Header("Generated Adventure Levels")]
    public AdventureLevelConfig[] generatedAdventureLevels; // Yeni designer-facing config assetleri

    public LevelData currentSelectedLevel { get; private set; } // Runtime'da çözümlenmiş seviye
    public AdventureLevelConfig currentSelectedAdventureConfig { get; private set; }
    public int currentSelectedLevelNumber { get; private set; }
    
    public static ProgressManager Instance;

    private const string HighestLevelUnlockedKey = "HighestLevelUnlocked";

    public int highestLevelUnlocked { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress(); // Oyuncu oyunu açtığında kayıtlı ilerlemesini yükle
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UnlockNextLevel()
    {
        int selectedLevel = Mathf.Max(1, currentSelectedLevelNumber);
        int oldHighestLevelUnlocked = Mathf.Max(1, highestLevelUnlocked);
        int maxAdventureLevel = GetMaxAdventureLevel();
        int newHighestLevelUnlocked = oldHighestLevelUnlocked;

        if (selectedLevel >= oldHighestLevelUnlocked)
        {
            newHighestLevelUnlocked = Mathf.Min(selectedLevel + 1, maxAdventureLevel);
        }

        bool changed = newHighestLevelUnlocked != oldHighestLevelUnlocked;
        highestLevelUnlocked = Mathf.Max(1, newHighestLevelUnlocked);

        Debug.Log(
            $"[ProgressManager] UnlockNextLevel selectedLevel={selectedLevel} oldHighestLevelUnlocked={oldHighestLevelUnlocked} newHighestLevelUnlocked={highestLevelUnlocked} changed={changed}");

        if (changed)
        {
            SaveProgress();
        }
    }

    // DEBUGGING İÇİN: Bütün ilerlemeyi silmek istersen
    public void ResetProgress()
    {
        highestLevelUnlocked = 1;
        PlayerPrefs.DeleteKey(HighestLevelUnlockedKey);
        SaveProgress();
        Debug.LogWarning("OYUNCU İLERLEMESİ SIFIRLANDI!");
    }

    private void SaveProgress()
    {
        highestLevelUnlocked = Mathf.Clamp(highestLevelUnlocked, 1, GetMaxAdventureLevel());
        PlayerPrefs.SetInt(HighestLevelUnlockedKey, highestLevelUnlocked);
        PlayerPrefs.Save();
        Debug.Log($"İlerleme kaydedildi. En yüksek açık seviye: {highestLevelUnlocked}");
    }

    private void LoadProgress()
    {
        // Telefonun hafızasında kayıtlı bir veri var mı diye kontrol et
        // Eğer yoksa, oyuncu ilk defa oynuyor demektir, Seviye 1'den başlasın.
        highestLevelUnlocked = Mathf.Clamp(PlayerPrefs.GetInt(HighestLevelUnlockedKey, 1), 1, GetMaxAdventureLevel());
        Debug.Log($"İlerleme yüklendi. En yüksek açık seviye: {highestLevelUnlocked}");
    }

    public int GetAdventureLevelCount()
    {
        return Mathf.Max(
            allLevels != null ? allLevels.Length : 0,
            generatedAdventureLevels != null ? generatedAdventureLevels.Length : 0);
    }

    public bool TrySelectAdventureLevel(int levelNumber)
    {
        int levelIndex = levelNumber - 1;

        if (levelIndex < 0 || levelIndex >= GetAdventureLevelCount())
            return false;

        AdventureLevelConfig resolvedConfig;
        LevelData resolvedLevel = ResolveAdventureLevel(levelIndex, out resolvedConfig);
        if (resolvedLevel == null)
            return false;

        currentSelectedLevel = resolvedLevel;
        currentSelectedAdventureConfig = resolvedConfig;
        currentSelectedLevelNumber = levelNumber;
        return true;
    }

    public bool HasAdventureLevel(int levelNumber)
    {
        int levelIndex = levelNumber - 1;
        if (levelIndex < 0)
            return false;

        bool hasGeneratedLevel =
            generatedAdventureLevels != null &&
            levelIndex < generatedAdventureLevels.Length &&
            generatedAdventureLevels[levelIndex] != null;

        bool hasLegacyLevel =
            allLevels != null &&
            levelIndex < allLevels.Length &&
            allLevels[levelIndex] != null;

        return hasGeneratedLevel || hasLegacyLevel;
    }

    public void ClearAdventureSelection()
    {
        currentSelectedLevel = null;
        currentSelectedAdventureConfig = null;
        currentSelectedLevelNumber = 0;
    }

    private LevelData ResolveAdventureLevel(int levelIndex, out AdventureLevelConfig resolvedConfig)
    {
        resolvedConfig = null;

        if (generatedAdventureLevels != null &&
            levelIndex >= 0 &&
            levelIndex < generatedAdventureLevels.Length &&
            generatedAdventureLevels[levelIndex] != null)
        {
            resolvedConfig = generatedAdventureLevels[levelIndex];
            return AdventureLevelGenerator.GenerateRuntimeLevel(resolvedConfig);
        }

        if (allLevels != null && levelIndex >= 0 && levelIndex < allLevels.Length)
            return allLevels[levelIndex];

        return null;
    }

    private int GetMaxAdventureLevel()
    {
        int adventureLevelCount = GetAdventureLevelCount();
        return Mathf.Max(1, adventureLevelCount > 0 ? adventureLevelCount : FallbackMaxAdventureLevel);
    }
}
