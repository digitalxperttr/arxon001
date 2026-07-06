using UnityEngine;

public class GameSceneUI : MonoBehaviour
{
    // "Tekrar Dene" butonuna bağlanacak
    public void RestartLevel()
    {
        Time.timeScale = 1f; // Donmuş zamanı çöz!
        bool isClassicRun = GridManager.Instance == null || GridManager.Instance.IsClassicRun();

        if (isClassicRun)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetScoreAndLevel();

            if (GridManager.Instance != null)
                GridManager.Instance.ResetClassicRunState();
        }

        if (SceneLoader.Instance != null)
        {
            if (isClassicRun)
            {
                SceneLoader.Instance.LoadClassicMode();
            }
            else
            {
                SceneLoader.Instance.LoadAdventureGameScene();
            }
        }
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
            int currentLevelNum = ProgressManager.Instance.currentSelectedLevelNumber;

            if (currentLevelNum > 0 && currentLevelNum % 10 == 0)
            {
                GoToMap();
                return;
            }
            
            // Sonraki bölüm listede var mı diye kontrol et
            if (currentLevelNum < ProgressManager.Instance.GetAdventureLevelCount() &&
                ProgressManager.Instance.TrySelectAdventureLevel(currentLevelNum + 1))
            {
                // Oyunu yeniden başlat (Yeni verilerle açılacaktır)
                if (SceneLoader.Instance != null) SceneLoader.Instance.LoadAdventureGameScene();
            }
            else
            {
                // Bütün bölümler bittiyse haritaya dön
                GoToMap();
            }
        }
    }
}
