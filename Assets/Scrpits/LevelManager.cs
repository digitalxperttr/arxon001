using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    public TextMeshProUGUI movesText;
    public TextMeshProUGUI targetText;
    public GameObject winPanel;
    
    public LevelData currentLevel { get; private set; }
    private int remainingMoves;
    private int currentTargetLines;
    private int currentTargetScore;
    private bool hasFinishedLevel;
    private bool shouldWriteLegacyHud;
    

    void Awake() 
    { 
        Instance = this; 
    }

    void Start()
    {
        shouldWriteLegacyHud = SceneManager.GetActiveScene().name == "GameScene";

        if (ProgressManager.Instance != null && ProgressManager.Instance.currentSelectedLevel != null)
        {
            // --- MACERA MODU ---
            currentLevel = ProgressManager.Instance.currentSelectedLevel;
            remainingMoves = currentLevel.moveLimit;
            currentTargetLines = currentLevel.targetLines;
            currentTargetScore = currentLevel.targetScore;
            hasFinishedLevel = false;

            AdventureLevelConfig objectiveConfig = ProgressManager.Instance.currentSelectedAdventureConfig;
            if (objectiveConfig == null)
            {
                objectiveConfig = CreateLegacyObjectiveConfig(currentLevel);
            }

            if (objectiveConfig != null)
            {
                ObjectiveManager.EnsureInstance().Initialize(objectiveConfig, currentLevel);
                if (ScoreManager.Instance != null)
                {
                    ObjectiveManager.Instance.ReportScoreChanged(ScoreManager.Instance.CurrentScore);
                }

                ObjectiveHUD objectiveHUD = ObjectiveHUD.EnsureInstance();
                if (objectiveHUD != null)
                {
                    objectiveHUD.BuildFromObjectiveManager();
                }
            }

            UpdateUI();
        }
        else
        {
            // --- KLASİK MOD (Sonsuz) ---
            currentLevel = null;
            if (movesText != null) movesText.gameObject.SetActive(false);
            if (targetText != null) targetText.gameObject.SetActive(false);
            this.enabled = false; // Klasik moddaysak bu script kendini kapatsın, boşuna çalışmasın
        }
    }

    public void PlayerDidMove()
    {
        if (currentLevel == null) return;

        remainingMoves--;
        UpdateUI();

    }

    private AdventureLevelConfig CreateLegacyObjectiveConfig(LevelData levelData)
    {
        if (levelData == null)
        {
            return null;
        }

        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.hideFlags = HideFlags.DontSave;
        config.name = $"LegacyAdventureObjectiveConfig_{levelData.levelNumber}";
        config.objective = levelData.objectiveType;
        config.targetLines = levelData.targetLines;
        config.targetScore = levelData.targetScore;
        config.targetObstacleCount = levelData.targetObstacleCount;
        config.targetComboCount = levelData.targetComboCount;
        return config;
    }

    public void LinesCleared(int count)
    {
        if (currentLevel == null) return;

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.ReportRowsCleared(count);
        }

        currentTargetLines -= count;
        if (currentTargetLines < 0) currentTargetLines = 0; // Eksiye düşmesin
        
        UpdateUI();
        CheckWinLoss();
    }

    private void UpdateUI()
    {
        if (!shouldWriteLegacyHud)
        {
            return;
        }

        if (movesText != null) movesText.text = $"Hamle: {remainingMoves}";
        if (targetText != null)
        {
            if (currentLevel.objectiveType == ObjectiveType.ReachScore && currentTargetScore > 0)
            {
                int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
                targetText.text = $"Hedef: {score}/{currentTargetScore} Puan";
            }
            else
            {
                targetText.text = $"Hedef: {currentTargetLines} Satır";
            }
        }
    }

    private void CheckWinLoss()
    {
        if (hasFinishedLevel)
        {
            return;
        }

        if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsActive)
        {
            if (ObjectiveManager.Instance.AreAllObjectivesComplete())
            {
                CompleteLevel();
            }

            return;
        }

        bool scoreGoalReached =
            currentLevel.objectiveType == ObjectiveType.ReachScore &&
            currentTargetScore > 0 &&
            ScoreManager.Instance != null &&
            ScoreManager.Instance.CurrentScore >= currentTargetScore;

        bool lineGoalReached =
            currentLevel.objectiveType != ObjectiveType.ReachScore &&
            currentTargetLines <= 0;

        if (scoreGoalReached || lineGoalReached)
        {
            CompleteLevel();
            return;
        }


    }

    public void EvaluateObjectiveCompletion()
    {
        if (currentLevel == null) return;

        CheckWinLoss();
    }

    private void CompleteLevel()
    {
        if (hasFinishedLevel)
        {
            return;
        }

        hasFinishedLevel = true;
        Debug.Log("<color=green>BÖLÜM GEÇİLDİ! KAZANDIN!</color>");

        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.UnlockNextLevel();
        }

        // Şimdilik oyunu durduruyoruz, ileride buraya "KAZANDIN" paneli açtıracağız
        StartCoroutine(WinRoutine());
    }

    // === YENİ EKLENEN COROUTINE ===
    private System.Collections.IEnumerator WinRoutine()
    {
        // 1. Oyuncunun yeni hamle yapmasını engellemek için oyunu "bitmiş" işaretle
        if (GridManager.Instance != null) 
        {
            GridManager.Instance.isGameOver = true;
        }

        // 2. Patlamaların, düşen blokların ve uçan yazıların bitmesi için 1.5 saniye bekle
        yield return new WaitForSeconds(1.5f);

        // 3. Her şey durulduktan sonra oyunu durdur ve Kazanma Panelini aç
        Time.timeScale = 0;
        
        if (winPanel != null)
        {
            winPanel.SetActive(true);

            AdventureVictoryPanelUI adventureVictoryPanel = winPanel.GetComponent<AdventureVictoryPanelUI>();
            if (adventureVictoryPanel != null)
                adventureVictoryPanel.Show();
        }
        
        // İleride buraya: winPanel.SetActive(true); gibi Kazandın ekranını açan bir kod ekleyeceğiz.
    }

// === YENİ EKLENEN FONKSİYON ===
// Bu fonksiyon sadece tüm patlamalar ve düşüşler bittikten sonra çağrılacak.
public void EvaluateEndOfTurn()
{
    if (currentLevel == null) return;
    if (hasFinishedLevel) return;

    if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsActive)
    {
        if (ObjectiveManager.Instance.AreAllObjectivesComplete())
        {
            CompleteLevel();
            return;
        }

        if (remainingMoves <= 0)
        {
            Debug.Log("<color=red>Hamle Bitti! KAYBETTİN.</color>");
            if (GridManager.Instance != null) GridManager.Instance.TriggerGameOver();
        }

        return;
    }
    
    // Eğer o el içinde patlayan bloklarla zaten kazandıysak, kaybetme kontrolüne girme
    bool scoreGoalReached =
        currentLevel.objectiveType == ObjectiveType.ReachScore &&
        currentTargetScore > 0 &&
        ScoreManager.Instance != null &&
        ScoreManager.Instance.CurrentScore >= currentTargetScore;

    if (currentTargetLines <= 0 || scoreGoalReached) return;

    // Kazanmadıysak, tahta durulduysa ve hamlemiz de sıfırlandıysa ŞİMDİ kaybettin.
    if (remainingMoves <= 0)
    {
        Debug.Log("<color=red>Hamle Bitti! KAYBETTİN.</color>");
        if (GridManager.Instance != null) GridManager.Instance.TriggerGameOver(); 
    }
}

}
