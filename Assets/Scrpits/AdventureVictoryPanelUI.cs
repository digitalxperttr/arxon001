using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdventureVictoryPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Image panelBackground;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private RectTransform objectiveListRoot;
    [SerializeField] private AdventureObjectiveResultRow[] objectiveRows = new AdventureObjectiveResultRow[3];
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button mapButton;
    [SerializeField] private CollectibleDatabase collectibleDatabase;
    [SerializeField] private Sprite genericRowIcon;
    [SerializeField] private Sprite genericScoreIcon;
    [SerializeField] private Sprite completedStatusSprite;
    [SerializeField] private Sprite incompleteStatusSprite;
    [SerializeField] private GameObject sharedModalDim;

    [Header("Animation")]
    [SerializeField] private float popStartScale = 0.9f;
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float buttonStagger = 0.05f;

    private CanvasGroup titleCanvasGroup;
    private CanvasGroup nextButtonCanvasGroup;
    private CanvasGroup mapButtonCanvasGroup;
    private Coroutine showRoutine;
    private InputManager disabledInputManager;

    private void Awake()
    {
        ResolveMissingReferences();
        PrepareCanvasGroups();
    }

    private void OnEnable()
    {
        Show();
    }

    private void OnDisable()
    {
        SetSharedModalDimVisible(false);

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (disabledInputManager != null)
        {
            disabledInputManager.enabled = true;
            disabledInputManager = null;
        }

        Time.timeScale = 1f;
    }

    public void Show()
    {
        ResolveMissingReferences();
        PrepareCanvasGroups();

        if (titleText != null)
            titleText.text = "MÜKEMMEL!";

        HideOtherAdventurePanels();
        SetSharedModalDimVisible(true);
        transform.SetAsLastSibling();

        AdventureObjectiveResultList.ApplyPanelSprite(panelBackground, panelSprite);
        AdventureObjectiveResultList.Build(
            objectiveRows,
            collectibleDatabase,
            genericRowIcon,
            genericScoreIcon,
            completedStatusSprite,
            incompleteStatusSprite,
            true);

        Time.timeScale = 0f;

        if (GridManager.Instance != null)
            GridManager.Instance.isGameOver = true;

        InputManager inputManager = FindAnyObjectByType<InputManager>();
        if (inputManager != null && inputManager.enabled)
        {
            inputManager.enabled = false;
            disabledInputManager = inputManager;
        }

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(PlayShowAnimation());
    }

    public void OnNextLevelPressed()
    {
        SetSharedModalDimVisible(false);
        Time.timeScale = 1f;

        if (ProgressManager.Instance == null || ProgressManager.Instance.currentSelectedLevel == null)
        {
            LoadAdventureMap();
            return;
        }

        int completedLevel = ProgressManager.Instance.currentSelectedLevelNumber;
        if (completedLevel > 0 && completedLevel % 10 == 0)
        {
            Debug.Log($"[AdventureVictoryPanelUI] Level {completedLevel} completed. Returning to AdventureMap for next page.");
            LoadAdventureMap();
            return;
        }

        int nextLevel = completedLevel + 1;
        if (completedLevel > 0 &&
            completedLevel < ProgressManager.Instance.GetAdventureLevelCount() &&
            ProgressManager.Instance.TrySelectAdventureLevel(nextLevel))
        {
            Debug.Log($"[AdventureVictoryPanelUI] Loading next Adventure level: {nextLevel}");
            LoadAdventureGameScene();
            return;
        }

        LoadAdventureMap();
    }

    public void OnMapPressed()
    {
        SetSharedModalDimVisible(false);
        Time.timeScale = 1f;
        Debug.Log("[AdventureVictoryPanelUI] Returning to AdventureMap.");
        LoadAdventureMap();
    }

    private IEnumerator PlayShowAnimation()
    {
        if (panelRoot != null)
            panelRoot.localScale = Vector3.one * popStartScale;

        SetAlpha(titleCanvasGroup, 0f);
        SetAlpha(nextButtonCanvasGroup, 0f);
        SetAlpha(mapButtonCanvasGroup, 0f);

        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (panelRoot != null)
                panelRoot.localScale = Vector3.one * Mathf.Lerp(popStartScale, 1f, eased);

            SetAlpha(titleCanvasGroup, t);
            yield return null;
        }

        if (panelRoot != null)
            panelRoot.localScale = Vector3.one;

        SetAlpha(titleCanvasGroup, 1f);
        yield return new WaitForSecondsRealtime(buttonStagger);
        SetAlpha(nextButtonCanvasGroup, 1f);
        yield return new WaitForSecondsRealtime(buttonStagger);
        SetAlpha(mapButtonCanvasGroup, 1f);

        showRoutine = null;
    }

    private void ResolveMissingReferences()
    {
        if (panelRoot == null)
            panelRoot = transform as RectTransform;

        if (panelBackground == null)
            panelBackground = GetComponent<Image>();

        if (titleText == null)
            titleText = transform.Find("kazandin")?.GetComponent<TextMeshProUGUI>();

        if (objectiveListRoot == null)
            objectiveListRoot = transform.Find("VictoryObjectiveResults") as RectTransform;

        if (nextLevelButton == null)
            nextLevelButton = transform.Find("sonraki")?.GetComponent<Button>();

        if (mapButton == null)
            mapButton = transform.Find("harita")?.GetComponent<Button>();

        if (sharedModalDim == null)
            sharedModalDim = FindSharedModalDim();

        if (collectibleDatabase == null && GridManager.Instance != null)
            collectibleDatabase = GridManager.Instance.CollectibleDatabase;
    }

    private GameObject FindSharedModalDim()
    {
        Transform current = transform;
        while (current != null)
        {
            Transform dim = current.Find("SharedModalDim");
            if (dim != null)
                return dim.gameObject;

            current = current.parent;
        }

        return null;
    }

    private void SetSharedModalDimVisible(bool isVisible)
    {
        if (sharedModalDim != null)
            sharedModalDim.SetActive(isVisible);
    }

    private void HideOtherAdventurePanels()
    {
        if (transform.parent == null)
            return;

        for (int i = 0; i < transform.parent.childCount; i++)
        {
            Transform sibling = transform.parent.GetChild(i);
            if (sibling == transform || !sibling.gameObject.activeSelf)
                continue;

            if (sibling.GetComponent<AdventureLosePanelUI>() != null)
                sibling.gameObject.SetActive(false);
        }
    }

    private void PrepareCanvasGroups()
    {
        if (titleText != null && titleCanvasGroup == null)
            titleCanvasGroup = GetOrAddCanvasGroup(titleText.gameObject);

        if (nextLevelButton != null && nextButtonCanvasGroup == null)
            nextButtonCanvasGroup = GetOrAddCanvasGroup(nextLevelButton.gameObject);

        if (mapButton != null && mapButtonCanvasGroup == null)
            mapButtonCanvasGroup = GetOrAddCanvasGroup(mapButton.gameObject);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private void SetAlpha(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }

    private void LoadAdventureGameScene()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadAdventureGameScene();
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("AdventureGameScene");
    }

    private void LoadAdventureMap()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadAdventureMap();
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("AdventureMap");
    }
}
