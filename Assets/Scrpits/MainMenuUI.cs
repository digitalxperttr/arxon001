using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void StartClassicMode()
    {
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