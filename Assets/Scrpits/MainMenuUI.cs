using UnityEngine;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    public TextMeshProUGUI mainMenuBestScoreText; 
    void Start()
    {
        // YENİ: Ana menü açıldığında hafızadaki rekoru ekrana bas
        if (mainMenuBestScoreText != null)
        {
            int best = PlayerPrefs.GetInt("ClassicBestScore", 0);
            mainMenuBestScoreText.text = $"Klasik Mod Rekoru: {best}";
        }
    }
    public void StartClassicMode()
    {
        // YENİ: Oyuna Klasik modda girdiğimizi belirtmek için seçili bölümü sıfırlıyoruz
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.ClearAdventureSelection();
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadClassicMode();
        }
    }
    public void StartAdventureMode()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadAdventureMap();
        }
    }
}
