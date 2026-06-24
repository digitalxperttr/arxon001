using TMPro;
using UnityEngine;

public class ClassicResultPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreValueText;
    [SerializeField] private TextMeshProUGUI bestScoreValueText;
    [SerializeField] private TextMeshProUGUI levelValueText;
    [SerializeField] private GameObject dimOverlay;
    [SerializeField] private GameObject newRecordText;

    private void OnEnable()
    {
        if (dimOverlay != null)
        {
            dimOverlay.SetActive(true);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (dimOverlay != null)
        {
            dimOverlay.SetActive(false);
        }
    }

    public void Refresh()
    {
        var scoreManager = ScoreManager.Instance;
        int currentScore = scoreManager != null ? scoreManager.CurrentScore : 0;
        int bestScore = scoreManager != null ? scoreManager.bestScore : PlayerPrefs.GetInt("ClassicBestScore", 0);
        int currentLevel = scoreManager != null ? scoreManager.currentLevel : 1;

        if (scoreValueText != null)
        {
            scoreValueText.text = currentScore.ToString();
        }

        if (bestScoreValueText != null)
        {
            bestScoreValueText.text = bestScore.ToString();
        }

        if (levelValueText != null)
        {
            levelValueText.text = currentLevel.ToString();
        }

        if (newRecordText != null)
        {
            newRecordText.SetActive(false);
        }
    }
}
