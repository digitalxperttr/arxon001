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
    private bool hasMoveLimit;
    private int currentTargetLines;
    private int currentTargetScore;
    private bool hasFinishedLevel;
    private bool isVictoryPending;
    private bool shouldWriteLegacyHud;
    private AdventureAttemptSnapshot adventureAttempt;

    public bool IsVictoryPending => isVictoryPending;
    public AdventureAttemptSnapshot AdventureAttempt => adventureAttempt;
    public bool HasMoveLimit => hasMoveLimit;
    public int RemainingMoves => remainingMoves;
    

    void Awake() 
    { 
        Instance = this; 
    }

    // GridManager is the single startup entry point, after every scene Awake has run.
    public bool TryInitializeAdventure(GridManager grid, out string error)
    {
        error = null;
        currentLevel = null;
        shouldWriteLegacyHud = false;
        adventureAttempt = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentAdventureAttempt : null;
        LevelData selected = adventureAttempt != null ? adventureAttempt.RuntimeLevel : null;
        if (selected == null)
        {
            error = "Adventure seçimi eksik. Bölümü Adventure haritasından seçin.";
            return false;
        }
        if (!selected.ValidateRuntime(grid.CollectibleDatabase, out error)) return false;

        AdventureContentValidationResult validation = AdventureContentValidator.ValidateLevel(
            selected,
            grid.CollectibleDatabase,
            grid.CreateAdventureRowGenerationContext(selected));
        foreach (string warning in validation.Warnings)
            Debug.LogWarning($"[Adventure Validation] {warning}");
        foreach (string riskNote in validation.RiskNotes)
            Debug.LogWarning($"[Adventure Validation Risk] {riskNote}");
        if (validation.HasErrors)
        {
            error = "Adventure content validation failed:\n- " + string.Join("\n- ", validation.Errors);
            Debug.LogError($"[Adventure Validation] {error}");
            return false;
        }

        currentLevel = selected;
        enabled = true;
        hasMoveLimit = selected.hasMoveLimit;
        remainingMoves = selected.moveLimit;
        if (!hasMoveLimit && movesText != null)
            movesText.gameObject.SetActive(false);
        currentTargetLines = selected.targetLines;
        currentTargetScore = selected.targetScore;
        hasFinishedLevel = false;
        isVictoryPending = false;
        ObjectiveManager.EnsureInstance().Initialize(selected);
        return ObjectiveManager.Instance.IsActive;
    }

    public void InitializeClassic()
    {
        currentLevel = null;
        adventureAttempt = null;
        shouldWriteLegacyHud = false;
        if (movesText != null) movesText.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);
        enabled = false;
    }

    public void PlayerDidMove()
    {
        if (currentLevel == null) return;

        if (hasMoveLimit)
            remainingMoves--;
        UpdateUI();

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

        if (movesText != null)
        {
            movesText.gameObject.SetActive(hasMoveLimit);
            if (hasMoveLimit) movesText.text = $"Hamle: {remainingMoves}";
        }
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
        if (hasFinishedLevel || isVictoryPending)
        {
            return;
        }

        if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsActive)
        {
            if (ObjectiveManager.Instance.AreAllObjectivesComplete())
            {
                RequestVictory();
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
            RequestVictory();
            return;
        }


    }

    public void EvaluateObjectiveCompletion()
    {
        if (currentLevel == null) return;

        CheckWinLoss();
    }

    private void RequestVictory()
    {
        if (hasFinishedLevel || isVictoryPending || currentLevel == null)
        {
            return;
        }

        isVictoryPending = true;
        Debug.Log("<color=green>BÖLÜM TAMAMLANDI. Tahta çözülmesinin bitmesi bekleniyor.</color>");

        if (GridManager.Instance != null)
        {
            GridManager.Instance.TryFinalizePendingAdventureVictory();
        }
    }

    public void TryFinalizePendingVictory(GridManager grid)
    {
        if (!isVictoryPending || hasFinishedLevel || currentLevel == null || grid == null || !grid.IsCurrentResolutionSettled())
        {
            return;
        }

        isVictoryPending = false;
        hasFinishedLevel = true;
        Debug.Log("<color=green>BÖLÜM GEÇİLDİ! KAZANDIN!</color>");

        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.CompleteAdventureAttempt(adventureAttempt);
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
    if (hasFinishedLevel || isVictoryPending) return;

    if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsActive)
    {
        if (ObjectiveManager.Instance.AreAllObjectivesComplete())
        {
            RequestVictory();
            return;
        }

        if (IsMoveLimitExhausted())
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
    if (IsMoveLimitExhausted())
    {
        Debug.Log("<color=red>Hamle Bitti! KAYBETTİN.</color>");
        if (GridManager.Instance != null) GridManager.Instance.TriggerGameOver(); 
    }
}

private bool IsMoveLimitExhausted() => hasMoveLimit && remainingMoves <= 0;

}
