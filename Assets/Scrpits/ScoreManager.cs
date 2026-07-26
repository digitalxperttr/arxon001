// ScoreManager.cs - DÜZELTİLMİŞ KOD

using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement; // Sahne yönetimi için gerekli
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    

    // Bu referansı artık kod içinde bulacağız, public olmasına gerek yok.
    private TextMeshProUGUI scoreText; 
    private TextMeshProUGUI bestScoreText; // En yüksek skor UI referansı
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI xpText;
    private Image levelProgressBarFill;
    private GameObject levelProgressBarBG;
    private GameObject targetTextObject;
    private GameObject movesTextObject;
    private LevelUpFXUI levelUpFXUI;
    private bool shouldUseClassicHud;
    public int bestScore { get; private set; } // Hafızada tutacağımız rekor
    public int PreviousBestScoreAtRunStart { get; private set; }
    private int currentScore = 0;
    public int CurrentScore => currentScore;

    // KOMBO DEĞİŞKENLERİ
    public int comboCount = 0;
    // Kombo sıfırsa çarpan 1'dir, değilse kombonun kendisidir.
    public int comboMultiplier { get { return comboCount > 0 ? comboCount : 1; } }

    // SEVİYE (LEVEL) DEĞİŞKENLERİ
    [SerializeField] private int[] scoreLevelThresholds = { 0, 250, 600, 1000, 1300, 1800, 2600, 3200, 4000, 5000 };
    [SerializeField] private int postThresholdBaseGap = 1200;
    [SerializeField] private int postThresholdGapIncrease = 200;
    public int currentLevel = 1;

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
        currentLevel = GetLevelForScore(currentScore);
        comboCount = 0;

        UpdateScoreUI();
    }

    public void ResetClassicScoreAndBestForTesting()
    {
        PlayerPrefs.DeleteKey("ClassicBestScore");
        PlayerPrefs.Save();

        bestScore = 0;
        PreviousBestScoreAtRunStart = 0;
        ResetScoreAndLevel();
    }

    public void AddClearedLines(int count)
    {
        // Classic level progression is score based now; this remains for existing callers.
    }

    void Awake()
    {
        ValidateLevelProgressionSettings();

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
        ClearClassicHudReferences();
        shouldUseClassicHud = scene.name == "GameScene";

        if (shouldUseClassicHud)
        {
            GameObject scoreTextObject = GameObject.FindWithTag("ScoreText");
            if (scoreTextObject != null) scoreText = scoreTextObject.GetComponent<TextMeshProUGUI>();

            // YENİ: Rekor yazısını Tag ile bul
            GameObject bestScoreObject = GameObject.FindWithTag("BestScoreText");
            if (bestScoreObject != null) bestScoreText = bestScoreObject.GetComponent<TextMeshProUGUI>();

            GameObject levelTextObject = GameObject.Find("LevelText");
            if (levelTextObject != null) levelText = levelTextObject.GetComponent<TextMeshProUGUI>();

            GameObject xpTextObject = GameObject.Find("XPText");
            if (xpTextObject != null) xpText = xpTextObject.GetComponent<TextMeshProUGUI>();

            levelProgressBarBG = GameObject.Find("LevelProgressBarBG");

            GameObject levelProgressBarFillObject = GameObject.Find("LevelProgressBarFill");
            if (levelProgressBarFillObject != null)
            {
                levelProgressBarFill = levelProgressBarFillObject.GetComponent<Image>();
            }

            targetTextObject = GameObject.Find("TargetText");
            movesTextObject = GameObject.Find("MovesText");
            levelUpFXUI = FindAnyObjectByType<LevelUpFXUI>();

            bool isClassicMode = ProgressManager.Instance == null || ProgressManager.Instance.currentSelectedLevel == null;
            SetClassicHudState(isClassicMode);
        }
        
        // YENİ: Oyuncu oyuna girdiğinde veya sahne değiştiğinde rekorunu hafızadan çek!
        bestScore = PlayerPrefs.GetInt("ClassicBestScore", 0);
        PreviousBestScoreAtRunStart = bestScore;
        
        currentScore = 0;
        currentLevel = GetLevelForScore(currentScore);
        UpdateScoreUI();
    }
        
public void UpdateScoreUI()
    {
        if (!shouldUseClassicHud)
        {
            return;
        }

        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }

        if (bestScoreText != null)
        {
            bestScoreText.text = bestScore.ToString();
        }

        UpdateClassicLevelUI();
    }

    public void AddScore(int amount)
    {
        if (this == null) return;

        int previousScore = currentScore;
        int previousLevel = currentLevel;
        currentScore += amount;
        UpdateLevelFromScore();

        LogClassicProgressionDiagnostics(previousScore, currentScore, previousLevel, currentLevel);

        // YENİ: Eğer skorumuz rekoru geçtiyse ve KLASİK MODDAYSAK rekoru kaydet!
        // (LevelManager kapalıysa veya currentLevel null ise Klasik moddayız demektir)
        if (currentScore > bestScore && (LevelManager.Instance == null || LevelManager.Instance.currentLevel == null))
        {
            bestScore = currentScore;
            PlayerPrefs.SetInt("ClassicBestScore", bestScore);
            PlayerPrefs.Save();
        }

        UpdateScoreUI();

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.ReportScoreChanged(currentScore);

            if (LevelManager.Instance != null && LevelManager.Instance.enabled)
            {
                LevelManager.Instance.EvaluateObjectiveCompletion();
            }
        }
        
        StopAllCoroutines();
        StartCoroutine(PulseScoreText());
    }

    IEnumerator PulseScoreText()
    {
        if (!shouldUseClassicHud) yield break;

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

    private void UpdateLevelFromScore()
    {
        int previousLevel = currentLevel;
        currentLevel = GetLevelForScore(currentScore);

        if (shouldUseClassicHud && currentLevel > previousLevel)
        {
            Debug.Log($"<color=cyan>SEVİYE ATLADIN! YENİ SEVİYE: {currentLevel}</color>");
            if (levelUpFXUI != null)
            {
                levelUpFXUI.ShowLevelUp(currentLevel);
            }
        }
    }

    private int GetLevelForScore(int score)
    {
        int sanitizedScore = Mathf.Max(0, score);

        if (scoreLevelThresholds == null || scoreLevelThresholds.Length == 0)
        {
            return GetGeneratedLevelForScore(sanitizedScore, 0, 0);
        }

        int level = 1;
        for (int i = 0; i < scoreLevelThresholds.Length; i++)
        {
            if (sanitizedScore >= scoreLevelThresholds[i])
            {
                level = i + 1;
            }
            else
            {
                return Mathf.Max(1, level);
            }
        }

        int lastSerializedThreshold = scoreLevelThresholds[scoreLevelThresholds.Length - 1];
        return GetGeneratedLevelForScore(sanitizedScore, scoreLevelThresholds.Length, lastSerializedThreshold);
    }

    private int GetNextLevelThreshold()
    {
        int currentLevelStartThreshold = GetCurrentLevelStartThreshold();
        int nextLevel = currentLevel < int.MaxValue ? currentLevel + 1 : int.MaxValue;
        int nextLevelThreshold = GetThresholdForLevelStart(nextLevel);

        if (nextLevelThreshold <= currentLevelStartThreshold)
        {
            long fallbackThreshold = (long)currentLevelStartThreshold + postThresholdBaseGap;
            return fallbackThreshold >= int.MaxValue ? int.MaxValue : (int)fallbackThreshold;
        }

        return nextLevelThreshold;
    }

    private int GetCurrentLevelStartThreshold()
    {
        return GetThresholdForLevelStart(currentLevel);
    }

    private int GetThresholdForLevelStart(int level)
    {
        int sanitizedLevel = Mathf.Max(1, level);

        if (scoreLevelThresholds == null || scoreLevelThresholds.Length == 0)
        {
            return GetGeneratedThresholdForLevel(sanitizedLevel, 0, 0);
        }

        int serializedIndex = sanitizedLevel - 1;
        if (serializedIndex < scoreLevelThresholds.Length)
        {
            return scoreLevelThresholds[serializedIndex];
        }

        int lastSerializedThreshold = scoreLevelThresholds[scoreLevelThresholds.Length - 1];
        return GetGeneratedThresholdForLevel(sanitizedLevel, scoreLevelThresholds.Length, lastSerializedThreshold);
    }

    private int GetGeneratedLevelForScore(int score, int serializedLevelCount, int lastSerializedThreshold)
    {
        int minimumLevel = Mathf.Max(1, serializedLevelCount);
        int lowLevel = minimumLevel;
        int highLevel = minimumLevel + 1;
        int highThreshold = GetGeneratedThresholdForLevel(highLevel, serializedLevelCount, lastSerializedThreshold);

        while (score >= highThreshold && highThreshold < int.MaxValue && highLevel < int.MaxValue)
        {
            lowLevel = highLevel;
            highLevel = highLevel <= int.MaxValue / 2 ? highLevel * 2 : int.MaxValue;
            highThreshold = GetGeneratedThresholdForLevel(highLevel, serializedLevelCount, lastSerializedThreshold);
        }

        while (lowLevel + 1 < highLevel)
        {
            int midLevel = lowLevel + (highLevel - lowLevel) / 2;
            int midThreshold = GetGeneratedThresholdForLevel(midLevel, serializedLevelCount, lastSerializedThreshold);

            if (score >= midThreshold)
            {
                lowLevel = midLevel;
            }
            else
            {
                highLevel = midLevel;
            }
        }

        return lowLevel;
    }

    private int GetGeneratedThresholdForLevel(int level, int serializedLevelCount, int lastSerializedThreshold)
    {
        int generatedGapCount = serializedLevelCount > 0
            ? Mathf.Max(0, level - serializedLevelCount)
            : Mathf.Max(0, level - 1);
        long generatedGapTotal =
            (long)generatedGapCount * postThresholdBaseGap +
            (long)postThresholdGapIncrease * generatedGapCount * (generatedGapCount - 1) / 2;
        long threshold = (long)lastSerializedThreshold + generatedGapTotal;

        if (threshold >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Mathf.Max(0, (int)threshold);
    }

    private void GetCurrentLevelProgress(out int progressScore, out int requiredScore)
    {
        int currentLevelStartScore = GetCurrentLevelStartThreshold();
        int nextLevelScore = GetNextLevelThreshold();

        requiredScore = Mathf.Max(1, nextLevelScore - currentLevelStartScore);
        progressScore = Mathf.Clamp(currentScore - currentLevelStartScore, 0, requiredScore);
    }

    private void ValidateLevelProgressionSettings()
    {
        postThresholdBaseGap = Mathf.Max(1, postThresholdBaseGap);
        postThresholdGapIncrease = Mathf.Max(0, postThresholdGapIncrease);

#if UNITY_EDITOR
        if (scoreLevelThresholds == null || scoreLevelThresholds.Length == 0)
        {
            Debug.LogWarning("[ScoreManager] scoreLevelThresholds is empty. Classic score progression will use generated thresholds from 0.");
            return;
        }

        if (scoreLevelThresholds[0] != 0)
        {
            Debug.LogWarning($"[ScoreManager] scoreLevelThresholds should start at 0 for Level 1. Current first threshold: {scoreLevelThresholds[0]}.");
        }

        for (int i = 1; i < scoreLevelThresholds.Length; i++)
        {
            if (scoreLevelThresholds[i] <= scoreLevelThresholds[i - 1])
            {
                Debug.LogWarning(
                    $"[ScoreManager] scoreLevelThresholds must be strictly ascending. Invalid pair at indices {i - 1}/{i}: {scoreLevelThresholds[i - 1]} >= {scoreLevelThresholds[i]}.");
                return;
            }
        }
#endif
    }

    private void LogClassicProgressionDiagnostics(int previousScore, int newScore, int previousLevel, int newLevel)
    {
#if UNITY_EDITOR
        bool isClassicMode = LevelManager.Instance == null || LevelManager.Instance.currentLevel == null;
        if (!isClassicMode)
        {
            return;
        }

        int currentLevelStartScore = GetCurrentLevelStartThreshold();
        int nextLevelScore = GetNextLevelThreshold();
        GetCurrentLevelProgress(out int progressScore, out int requiredScore);

        Debug.Log(
            $"[ClassicProgression] score {previousScore}->{newScore} | level {previousLevel}->{newLevel} | " +
            $"levelStart={currentLevelStartScore} | nextThreshold={nextLevelScore} | progress={progressScore}/{requiredScore}");
#endif
    }

    private void UpdateClassicLevelUI()
    {
        if (levelText != null)
        {
            levelText.text = $"Seviye {currentLevel}";
        }

        if (levelProgressBarFill != null)
        {
            GetCurrentLevelProgress(out int progressScore, out int requiredScore);
            float progress = Mathf.Clamp01(progressScore / (float)requiredScore);
            levelProgressBarFill.fillAmount = progress;
        }

        if (xpText != null)
        {
            GetCurrentLevelProgress(out int progressScore, out int requiredScore);
            xpText.text = $"{progressScore} / {requiredScore}";
        }
    }

    private void SetClassicHudState(bool isClassicMode)
    {
        if (!shouldUseClassicHud)
        {
            return;
        }

        if (levelText != null)
        {
            levelText.gameObject.SetActive(isClassicMode);
        }

        if (levelProgressBarBG != null)
        {
            levelProgressBarBG.SetActive(isClassicMode);
        }

        if (xpText != null)
        {
            xpText.gameObject.SetActive(isClassicMode);
        }

        if (targetTextObject != null)
        {
            targetTextObject.SetActive(false);
        }

        if (movesTextObject != null)
        {
            movesTextObject.SetActive(false);
        }
    }

    private void ClearClassicHudReferences()
    {
        scoreText = null;
        bestScoreText = null;
        levelText = null;
        xpText = null;
        levelProgressBarFill = null;
        levelProgressBarBG = null;
        targetTextObject = null;
        movesTextObject = null;
        levelUpFXUI = null;
    }
}
