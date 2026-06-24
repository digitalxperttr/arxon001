using System.Collections;
using TMPro;
using UnityEngine;

public class ClassicGameOverFlow : MonoBehaviour
{
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject newHighScorePanel;
    [SerializeField] private GameObject newHighScoreDim;
    [SerializeField] private TextMeshProUGUI highScoreValueText;
    [SerializeField] private Transform titleTransform;
    [SerializeField] private Transform scoreTransform;
    [SerializeField] private ParticleSystem sparkleParticles;
    [SerializeField] private float celebrationDuration = 1.5f;
    [SerializeField] private float inputLockDuration = 0.3f;
    [SerializeField] private ScoreManager scoreManager;

    private bool isRunning;
    private CanvasGroup highScoreCanvasGroup;

    private void Awake()
    {
        if (scoreManager == null)
        {
            scoreManager = ScoreManager.Instance;
        }

        if (newHighScorePanel != null)
        {
            highScoreCanvasGroup = newHighScorePanel.GetComponent<CanvasGroup>();
            if (highScoreCanvasGroup == null)
            {
                highScoreCanvasGroup = newHighScorePanel.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnEnable()
    {
        if (!isRunning)
        {
            StartCoroutine(RunGameOverFlow());
        }
    }

    private IEnumerator RunGameOverFlow()
    {
        isRunning = true;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (scoreManager == null)
        {
            scoreManager = ScoreManager.Instance;
        }

        int currentScore = scoreManager != null ? scoreManager.CurrentScore : 0;
        int previousBestScore = scoreManager != null ? scoreManager.PreviousBestScoreAtRunStart : PlayerPrefs.GetInt("ClassicBestScore", 0);
        bool isNewHighScore = currentScore > 0 && currentScore > previousBestScore;

        if (isNewHighScore)
        {
            yield return StartCoroutine(ShowNewHighScoreCelebration(currentScore));
        }

        ShowResultPanel();
        isRunning = false;
    }

    private IEnumerator ShowNewHighScoreCelebration(int currentScore)
    {
        if (highScoreValueText != null)
        {
            highScoreValueText.text = currentScore.ToString();
        }

        if (newHighScoreDim != null)
        {
            newHighScoreDim.SetActive(true);
        }

        if (newHighScorePanel != null)
        {
            newHighScorePanel.SetActive(true);
        }

        if (highScoreCanvasGroup != null)
        {
            highScoreCanvasGroup.alpha = 0f;
            highScoreCanvasGroup.blocksRaycasts = true;
            highScoreCanvasGroup.interactable = true;
        }

        if (sparkleParticles != null)
        {
            sparkleParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparkleParticles.Play(true);
        }

        Vector3 titleBaseScale = titleTransform != null ? titleTransform.localScale : Vector3.one;
        Vector3 scoreBaseScale = scoreTransform != null ? scoreTransform.localScale : Vector3.one;
        float elapsed = 0f;
        float introDuration = Mathf.Max(Mathf.Min(celebrationDuration, 0.45f), inputLockDuration);

        while (true)
        {
            float t = Mathf.Clamp01(elapsed / introDuration);
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.25f));
            float titlePunch = 1f + Mathf.Sin(t * Mathf.PI * 3f) * Mathf.Lerp(0.18f, 0f, t);
            float scorePunch = Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0f, 1f, t));

            if (highScoreCanvasGroup != null)
            {
                highScoreCanvasGroup.alpha = fade;
            }

            if (titleTransform != null)
            {
                titleTransform.localScale = titleBaseScale * titlePunch;
            }

            if (scoreTransform != null)
            {
                scoreTransform.localScale = scoreBaseScale * scorePunch;
            }

            if (elapsed >= inputLockDuration && HasContinueInput())
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (titleTransform != null)
        {
            titleTransform.localScale = titleBaseScale;
        }

        if (scoreTransform != null)
        {
            scoreTransform.localScale = scoreBaseScale;
        }

        if (newHighScorePanel != null)
        {
            newHighScorePanel.SetActive(false);
        }

        if (newHighScoreDim != null)
        {
            newHighScoreDim.SetActive(false);
        }
    }

    private bool HasContinueInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            if (Input.GetTouch(i).phase == TouchPhase.Began)
            {
                return true;
            }
        }

        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
    }

    private void ShowResultPanel()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }
    }
}
