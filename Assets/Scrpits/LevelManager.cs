using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    public TextMeshProUGUI movesText;
    public TextMeshProUGUI targetText;
    public GameObject winPanel;
    
    public LevelData currentLevel { get; private set; }
    private int remainingMoves;
    private int currentTargetLines;
    

    void Awake() 
    { 
        Instance = this; 
    }

    void Start()
    {
        if (ProgressManager.Instance != null && ProgressManager.Instance.currentSelectedLevel != null)
        {
            // --- MACERA MODU ---
            currentLevel = ProgressManager.Instance.currentSelectedLevel;
            remainingMoves = currentLevel.moveLimit;
            currentTargetLines = currentLevel.targetLines;
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

    public void LinesCleared(int count)
    {
        if (currentLevel == null) return;

        currentTargetLines -= count;
        if (currentTargetLines < 0) currentTargetLines = 0; // Eksiye düşmesin
        
        UpdateUI();
        CheckWinLoss();
    }

    private void UpdateUI()
    {
        if (movesText != null) movesText.text = $"Hamle: {remainingMoves}";
        if (targetText != null) targetText.text = $"Hedef: {currentTargetLines} Satır";
    }

    private void CheckWinLoss()
    {
        // 1. Önce kazanma kontrolü (Hedef 0'a ulaştı mı?)
        if (currentTargetLines <= 0)
        {
            Debug.Log("<color=green>BÖLÜM GEÇİLDİ! KAZANDIN!</color>");
            ProgressManager.Instance.UnlockNextLevel();
            
            // Şimdilik oyunu durduruyoruz, ileride buraya "KAZANDIN" paneli açtıracağız
            StartCoroutine(WinRoutine()); 
            return;
        }


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
        
        if (winPanel != null) winPanel.SetActive(true); // <--- YENİ EKLENDİ
        
        // İleride buraya: winPanel.SetActive(true); gibi Kazandın ekranını açan bir kod ekleyeceğiz.
    }

// === YENİ EKLENEN FONKSİYON ===
// Bu fonksiyon sadece tüm patlamalar ve düşüşler bittikten sonra çağrılacak.
public void EvaluateEndOfTurn()
{
    if (currentLevel == null) return;
    
    // Eğer o el içinde patlayan bloklarla zaten kazandıysak, kaybetme kontrolüne girme
    if (currentTargetLines <= 0) return;

    // Kazanmadıysak, tahta durulduysa ve hamlemiz de sıfırlandıysa ŞİMDİ kaybettin.
    if (remainingMoves <= 0)
    {
        Debug.Log("<color=red>Hamle Bitti! KAYBETTİN.</color>");
        if (GridManager.Instance != null) GridManager.Instance.TriggerGameOver(); 
    }
}

}