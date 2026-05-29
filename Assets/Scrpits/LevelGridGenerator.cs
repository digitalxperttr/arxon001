using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelGridGenerator : MonoBehaviour
{
    public GameObject levelButtonPrefab;
    public Transform gridParent; // Butonların içine dizileceği Layout Group
    public int totalLevels = 50; // Macera modundaki toplam seviye sayısı

    void Start()
    {
        GenerateButtons();
    }

    void GenerateButtons()
    {
        // 1. Kalıcı yöneticiden oyuncunun ilerlemesini çek
        int unlockedCount = 1;
        int availableLevelCount = totalLevels;
        if (ProgressManager.Instance != null)
        {
            unlockedCount = ProgressManager.Instance.highestLevelUnlocked;
            availableLevelCount = ProgressManager.Instance.GetAdventureLevelCount();
        }

        // 2. Butonları yarat ve diz
        int buttonCount = Mathf.Max(totalLevels, availableLevelCount);
        for (int i = 1; i <= buttonCount; i++)
        {
            GameObject buttonObj = Instantiate(levelButtonPrefab, gridParent);
            buttonObj.name = "Level_" + i;

            // Butonun içindeki yazıyı bul ve seviye numarasını bas
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = i.ToString();
            
            Button button = buttonObj.GetComponent<Button>();

            bool hasBackingLevel =
                ProgressManager.Instance == null ||
                ProgressManager.Instance.HasAdventureLevel(i);

            if (i <= unlockedCount && hasBackingLevel)
            {
                // SEVİYE AÇIK: Tıklanabilir yap ve fonksiyon bağla
                button.interactable = true;
                int levelIndex = i; // Closure (C# bellek kuralı) için yerel değişkene alıyoruz
                button.onClick.AddListener(() => OnLevelButtonClick(levelIndex));
            }
            else
            {
                // SEVİYE KİLİTLİ: Rengini soluk ve tıklanamaz yap
                button.interactable = false;
            }
        }
    }

    void OnLevelButtonClick(int levelNumber)
    {
        Debug.Log($"<color=green>{levelNumber}. Seviye Başlatılıyor!</color>");
        
        if (ProgressManager.Instance != null && ProgressManager.Instance.TrySelectAdventureLevel(levelNumber))
        {
            // Veriyi hafızaya attık, şimdi oyun sahnesine geçiyoruz!
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadClassicMode(); // (GameScene sahnesini yüklüyor)
        }
        else
        {
            Debug.LogError("Bu seviye için LevelData bulunamadı! ProgressManager'daki listeyi kontrol et.");
        }
    }}
