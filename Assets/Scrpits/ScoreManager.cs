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
    private TextMeshProUGUI bestScoreText; // En yüksek skor UI referansı
    public int bestScore { get; private set; } // Hafızada tutacağımız rekor
    private int currentScore = 0;

    // KOMBO DEĞİŞKENLERİ
    public int comboCount = 0;
    // Kombo sıfırsa çarpan 1'dir, değilse kombonun kendisidir.
    public int comboMultiplier { get { return comboCount > 0 ? comboCount : 1; } }

    // SEVİYE (LEVEL) DEĞİŞKENLERİ
    public int currentLevel = 1;
    public int totalLinesCleared = 0;
    public int linesNeededForNextLevel = 10; // İlk seviye için 10 satır patlatmak gereksin

    public void IncrementCombo()
    {
        comboCount++;
        if (comboCount > 1) 
        {
            // Şimdilik Konsola yazdırıyoruz, Adım 4'te bunu Ekranda uçan yazı yapacağız!
            Debug.Log($"<color=orange>HARİKA! {comboCount}x COMBO!</color>");
        }
    }

    public void ResetCombo()
    {
        if (comboCount > 1) {
            Debug.Log("<color=red>Kombo Bozuldu!</color>");
        }
        comboCount = 0;
    }

    public void ResetScoreAndLevel()
    {
        currentScore = 0;
        currentLevel = 1;
        comboCount = 0;
        totalLinesCleared = 0;
        linesNeededForNextLevel = 10;

        UpdateScoreUI();
    }

    public void AddClearedLines(int count)
    {
        if (count <= 0) return;
        
        totalLinesCleared += count;
        
        // Level Up (Seviye Atlama) Kontrolü
        if (totalLinesCleared >= linesNeededForNextLevel)
        {
            currentLevel++;
            totalLinesCleared -= linesNeededForNextLevel; // Kalan satırları çöpe atma, yeni seviyeye aktar
            linesNeededForNextLevel += 5; // Her seviyede gereken satır sayısını 5 artır (Zorluk eğrisi)
            
            // Şimdilik konsola basıyoruz, ileride harika bir UI efekti bağlayacağız!
            Debug.Log($"<color=cyan>SEVİYE ATLADIN! YENİ SEVİYE: {currentLevel}</color>");
        }
    }

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
        if (scene.name == "GameScene")
        {
            GameObject scoreTextObject = GameObject.FindWithTag("ScoreText");
            if (scoreTextObject != null) scoreText = scoreTextObject.GetComponent<TextMeshProUGUI>();

            // YENİ: Rekor yazısını Tag ile bul
            GameObject bestScoreObject = GameObject.FindWithTag("BestScoreText");
            if (bestScoreObject != null) bestScoreText = bestScoreObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            scoreText = null;
            bestScoreText = null;
        }
        
        // YENİ: Oyuncu oyuna girdiğinde veya sahne değiştiğinde rekorunu hafızadan çek!
        bestScore = PlayerPrefs.GetInt("ClassicBestScore", 0);
        
        currentScore = 0;
        UpdateScoreUI();
    }
        
public void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            // Kullanıcının daha önce düzelttiği "score" anahtarı
            string translation = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetTranslation("score") : "Puan";
            if (string.IsNullOrEmpty(translation) || translation.Contains("KEY_NOT_FOUND")) translation = "Puan";
            
            scoreText.text = $"{translation}: {currentScore}";
        }

        // YENİ: Rekor yazısını güncelle
        if (bestScoreText != null)
        {
            string bestTranslation = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetTranslation("best_score") : "En İyi";
            if (string.IsNullOrEmpty(bestTranslation) || bestTranslation.Contains("KEY_NOT_FOUND")) bestTranslation = "En İyi";
            
            bestScoreText.text = $"{bestTranslation}: {bestScore}";
        }
    }

    public void AddScore(int amount)
    {
        if (this == null) return;
        
        currentScore += amount;

        // YENİ: Eğer skorumuz rekoru geçtiyse ve KLASİK MODDAYSAK rekoru kaydet!
        // (LevelManager kapalıysa veya currentLevel null ise Klasik moddayız demektir)
        if (currentScore > bestScore && (LevelManager.Instance == null || LevelManager.Instance.currentLevel == null))
        {
            bestScore = currentScore;
            PlayerPrefs.SetInt("ClassicBestScore", bestScore);
            PlayerPrefs.Save();
        }

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
