using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    // --- YENİ EKLENEN MACERA MODU DEĞİŞKENLERİ ---[Header("Level Verileri")]
    public LevelData[] allLevels; // Oyundaki tüm bölümlerin verilerini burada tutacağız
    public LevelData currentSelectedLevel; // Oyuncunun haritada tıkladığı bölüm
    // ---------------------------------------------
    
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
}