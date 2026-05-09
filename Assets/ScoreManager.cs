// ScoreManager.cs - DÜZELTİLMİŞ KOD

using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement; // Sahne yönetimi için gerekli

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    // Bu referansı artık kod içinde bulacağız, public olmasına gerek yok.
    private TextMeshProUGUI scoreText; 
    private int currentScore = 0;

    void Awake()
    {
        // Doğru Singleton Deseni: Eğer bir tane zaten varsa, yenisini yok et.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Bu obje sahne değişse bile yok olmasın!
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Sahne yüklendiğinde çağrılacak olaylara abone ol
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Obje yok edildiğinde abonelikten çık (hafıza sızıntısını önler)
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Herhangi bir sahne yüklendiğinde bu fonksiyon otomatik olarak çalışır
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Yeni yüklenen sahnede "ScoreText" etiketine sahip UI elemanını bul
        // ve scoreText değişkenine ata.
        GameObject scoreTextObject = GameObject.FindWithTag("ScoreText");
        if (scoreTextObject != null)
        {
            scoreText = scoreTextObject.GetComponent<TextMeshProUGUI>();
        }
        
        // Yeni oyuna başlarken skoru sıfırla ve UI'ı güncelle
        currentScore = 0;
        UpdateScoreUI();
    }

    public void UpdateScoreUI() 
    {
        if (scoreText == null) 
        {
            Debug.LogError("ScoreText referansı bulunamadı! Lütfen sahnedeki puan yazısının etiketini 'ScoreText' olarak ayarlayın.");
            return;
        }

        string translation = "Puan"; // Varsayılan değer
        if (LocalizationManager.Instance != null)
        {
            translation = LocalizationManager.Instance.GetTranslation("score_label");
            if (string.IsNullOrEmpty(translation) || translation.Contains("KEY_NOT_FOUND"))
            {
                translation = "Puan"; 
            }
        }

        scoreText.text = $"{translation}: {currentScore}";
    }

    public void AddScore(int amount)
    {
        if (this == null) return;
        
        currentScore += amount;
        UpdateScoreUI();
        
        StopAllCoroutines();
        StartCoroutine(PulseScoreText());
    }

    IEnumerator PulseScoreText()
    {
        // Bu fonksiyon aynı kalabilir, bir sorun yok.
        if (scoreText == null) yield break;

        Vector3 originalScale = scoreText.transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;

        float elapsed = 0f;
        float duration = 0.1f;
        while (elapsed < duration)
        {
            scoreText.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            scoreText.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        scoreText.transform.localScale = originalScale;
    }
}