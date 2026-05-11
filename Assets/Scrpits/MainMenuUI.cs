using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void StartClassicMode()
    {
        // YENİ: Oyuna Klasik modda girdiğimizi belirtmek için seçili bölümü sıfırlıyoruz
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.currentSelectedLevel = null; 
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