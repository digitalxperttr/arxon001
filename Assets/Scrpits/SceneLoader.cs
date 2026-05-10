using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

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
        LoadScene("MainMenu");
    }

    public void LoadClassicMode()
    {
        LoadScene("GameScene"); // Klasik Mod'un olduğu sahnenin adı
    }

    public void LoadAdventureMap()
    {
        LoadScene("AdventureMap"); // Seviye seçim haritasının olduğu sahne
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