using System.Collections;
using TMPro;
using UnityEngine;

public class LevelUpFXUI : MonoBehaviour
{
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private CanvasGroup levelUpCanvasGroup;
    [SerializeField] private RectTransform levelUpTransform;
    [SerializeField] private TextMeshProUGUI levelValueText;
    [SerializeField] private GameObject[] suppressWhenActive;
    [SerializeField] private float totalDuration = 1.9f;

    private Coroutine activeRoutine;

    private void Awake()
    {
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }

        if (levelUpCanvasGroup != null)
        {
            levelUpCanvasGroup.alpha = 0f;
            levelUpCanvasGroup.blocksRaycasts = false;
            levelUpCanvasGroup.interactable = false;
        }
    }

    public void ShowLevelUp(int newLevel)
    {
        if (levelUpPanel == null || levelUpCanvasGroup == null || levelUpTransform == null)
        {
            return;
        }

        if (ShouldSuppress())
        {
            return;
        }

        if (levelValueText != null)
        {
            levelValueText.text = $"SEViYE {newLevel}";
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(PlayLevelUpFX());
    }

    private bool ShouldSuppress()
    {
        if (suppressWhenActive == null)
        {
            return false;
        }

        foreach (GameObject suppressTarget in suppressWhenActive)
        {
            if (suppressTarget != null && suppressTarget.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator PlayLevelUpFX()
    {
        levelUpPanel.SetActive(true);

        Vector3 baseScale = Vector3.one;
        levelUpTransform.localScale = baseScale * 0.8f;
        levelUpCanvasGroup.alpha = 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, totalDuration);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = t < 0.2f
                ? Mathf.SmoothStep(0f, 1f, t / 0.2f)
                : Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t - 0.72f) / 0.28f));

            float punch = t < 0.35f
                ? Mathf.Lerp(0.8f, 1.1f, Mathf.SmoothStep(0f, 1f, t / 0.35f))
                : Mathf.Lerp(1.1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.25f)));

            levelUpCanvasGroup.alpha = alpha;
            levelUpTransform.localScale = baseScale * punch;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        levelUpTransform.localScale = baseScale;
        levelUpCanvasGroup.alpha = 0f;
        levelUpPanel.SetActive(false);
        activeRoutine = null;
    }
}
