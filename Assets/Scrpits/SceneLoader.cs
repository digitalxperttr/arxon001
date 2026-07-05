using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    private const string MainMenuSceneName = "MainMenu";
    private const string ClassicGameSceneName = "GameScene";
    private const string AdventureMapSceneName = "AdventureMap";
    private const string AdventureGameSceneName = "AdventureGameScene";

    public GameObject loadingScreen; // Yükleme ekranı paneli (opsiyonel ama şık)
    public float minLoadTime = 1f; // Yüklemenin çok hızlı bitmesi durumunda en az bekleme süresi

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadMainMenu()
    {
        LoadScene(MainMenuSceneName);
    }

    public void LoadClassicMode()
    {
        LoadScene(ClassicGameSceneName); // Klasik Mod'un olduğu sahnenin adı
    }

    public void LoadAdventureGameScene()
    {
        LoadScene(AdventureGameSceneName);
    }

    public void LoadAdventureMap()
    {
        LoadScene(AdventureMapSceneName); // Seviye seçim haritasının olduğu sahne
    }
    
    // Belli bir seviyeyi yüklemek için (Örn: "Level_5")
    public void LoadLevel(string levelName)
    {
        LoadScene(levelName);
    }

    // Genel Sahne Yükleyici
    private void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        float startTime = Time.realtimeSinceStartup;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // Sahne tamamen yüklenene kadar bekle
        while (!operation.isDone)
        {
            yield return null;
        }

        // Çok hızlı yüklendiyse, ani geçiş olmasın diye biraz daha bekle
        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minLoadTime)
        {
            yield return new WaitForSecondsRealtime(minLoadTime - elapsedTime);
        }

        if (loadingScreen != null)
            loadingScreen.SetActive(false);
    }
}
