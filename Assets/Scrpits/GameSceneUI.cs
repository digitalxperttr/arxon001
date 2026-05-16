using UnityEngine;

public class GameSceneUI : MonoBehaviour
{
    // "Tekrar Dene" butonuna bağlanacak
    public void RestartLevel()
    {
        Time.timeScale = 1f; // Donmuş zamanı çöz!
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadClassicMode(); // GameScene'i yeniden yükler (Macera verisi hafızada kalır, aynı bölüm baştan açılır)
    }

    // "Ana Menü" butonuna bağlanacak
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadMainMenu();
    }

    // "Haritaya Dön" butonuna bağlanacak
    public void GoToMap()
    {
        Time.timeScale = 1f;
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadAdventureMap();
    }

    // "Sonraki Bölüm" butonuna bağlanacak
    public void NextLevel()
    {
        Time.timeScale = 1f;
        if (ProgressManager.Instance != null && ProgressManager.Instance.currentSelectedLevel != null)
        {
            // Mevcut bölüm numarasını al (Örn: 1)
            int currentLevelNum = ProgressManager.Instance.currentSelectedLevel.levelNumber;
            
            // Sonraki bölüm listede var mı diye kontrol et
            if (currentLevelNum < ProgressManager.Instance.allLevels.Length)
            {
                // Bir sonraki bölümü seçili hale getir (Diziler 0'dan başladığı için index doğrudan currentLevelNum olur)
                ProgressManager.Instance.currentSelectedLevel = ProgressManager.Instance.allLevels[currentLevelNum];
                
                // Oyunu yeniden başlat (Yeni verilerle açılacaktır)
                if (SceneLoader.Instance != null) SceneLoader.Instance.LoadClassicMode();
            }
            else
            {
                // Bütün bölümler bittiyse haritaya dön
                GoToMap();
            }
        }
    }
}