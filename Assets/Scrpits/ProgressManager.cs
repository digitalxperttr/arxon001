using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    [Header("Legacy Adventure Levels")]
    public LevelData[] allLevels; // Eski el yapımı Adventure level assetleri

    [Header("Generated Adventure Levels")]
    public AdventureLevelConfig[] generatedAdventureLevels; // Yeni designer-facing config assetleri

    public LevelData currentSelectedLevel { get; private set; } // Runtime'da çözümlenmiş seviye
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
        // Bir sonraki seviyenin kilidini açar
        if (highestLevelUnlocked < 999) // 999 seviye limiti (istediğin gibi ayarla)
        {
            highestLevelUnlocked++;
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
        PlayerPrefs.SetInt(HighestLevelUnlockedKey, highestLevelUnlocked);
        PlayerPrefs.Save();
        Debug.Log($"İlerleme kaydedildi. En yüksek açık seviye: {highestLevelUnlocked}");
    }

    private void LoadProgress()
    {
        // Telefonun hafızasında kayıtlı bir veri var mı diye kontrol et
        // Eğer yoksa, oyuncu ilk defa oynuyor demektir, Seviye 1'den başlasın.
        highestLevelUnlocked = PlayerPrefs.GetInt(HighestLevelUnlockedKey, 1);
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

        LevelData resolvedLevel = ResolveAdventureLevel(levelIndex);
        if (resolvedLevel == null)
            return false;

        currentSelectedLevel = resolvedLevel;
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
        currentSelectedLevelNumber = 0;
    }

    private LevelData ResolveAdventureLevel(int levelIndex)
    {
        if (generatedAdventureLevels != null &&
            levelIndex >= 0 &&
            levelIndex < generatedAdventureLevels.Length &&
            generatedAdventureLevels[levelIndex] != null)
        {
            return AdventureLevelGenerator.GenerateRuntimeLevel(generatedAdventureLevels[levelIndex]);
        }

        if (allLevels != null && levelIndex >= 0 && levelIndex < allLevels.Length)
            return allLevels[levelIndex];

        return null;
    }
}
