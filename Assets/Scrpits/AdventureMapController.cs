using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AdventureMapController : MonoBehaviour
{
    private const int LevelsPerPage = 10;
    private const int TotalPages = 5;
    private const int TotalLevels = LevelsPerPage * TotalPages;
    private const string FallbackUnlockedLevelKey = "HighestLevelUnlocked";
    private const string FallbackSelectedLevelKey = "AdventureSelectedLevel";

    [Header("Page")]
    [SerializeField] private int currentPage;

    [Header("UI References")]
    public TMP_Text eventNameText;
    public Button leftPageButton;
    public Button rightPageButton;
    public Button backButton;
    public List<TMP_Text> nodeTexts = new List<TMP_Text>(LevelsPerPage);
    public List<Button> nodeButtons = new List<Button>(LevelsPerPage);
    public List<Image> nodeAmberImages = new List<Image>(LevelsPerPage);

    [Header("Text")]
    [SerializeField] private string defaultEventName = "EVENT NAME";

    [Header("Node Overlay")]
    [SerializeField] private Color completedAmberColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color currentAmberColor = new Color(0.05f, 1f, 0.88f, 1f);
    [SerializeField] private float currentPulseScale = 1.35f;
    [SerializeField] private float currentPulseSpeed = 4.5f;
    [SerializeField] private Color levelTextColor = new Color(0.08f, 0.07f, 0.06f, 1f);

    public int CurrentPage => currentPage;

    private RectTransform currentAmberTransform;
    private Image currentAmberImage;
    private Vector3 currentAmberBaseScale = Vector3.one;

    private void Awake()
    {
        WireButtons();
    }

    private void Start()
    {
        if (eventNameText != null && string.IsNullOrWhiteSpace(eventNameText.text))
            eventNameText.text = defaultEventName;

        SetPage(currentPage);
    }

    private void OnValidate()
    {
        currentPage = Mathf.Clamp(currentPage, 0, TotalPages - 1);
    }

    public void GoToMainMenu()
    {
        Debug.Log("[AdventureMap] Back button pressed. Loading MainMenu.");

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
            return;
        }

        SceneManager.LoadScene("MainMenu");
    }

    public void OnLeftPageButtonClicked()
    {
        SetPage(currentPage - 1);
    }

    public void OnRightPageButtonClicked()
    {
        SetPage(currentPage + 1);
    }

    public void OnNodeButtonClicked(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= LevelsPerPage)
            return;

        int levelNumber = GetLevelNumberForNode(nodeIndex);
        int highestLevelUnlocked = GetHighestLevelUnlocked();

        if (levelNumber > highestLevelUnlocked)
        {
            Debug.Log($"[AdventureMap] Level {levelNumber} is locked. Highest unlocked level: {highestLevelUnlocked}.");
            return;
        }

        Debug.Log($"[AdventureMap] Level {levelNumber} selected.");

        if (ProgressManager.Instance != null)
        {
            if (ProgressManager.Instance.TrySelectAdventureLevel(levelNumber))
            {
                LoadAdventureGameScene();
            }
            else
            {
                Debug.LogWarning($"[AdventureMap] Level {levelNumber} is unlocked but no Adventure level data was found.");
            }

            return;
        }

        PlayerPrefs.SetInt(FallbackSelectedLevelKey, levelNumber);
        PlayerPrefs.Save();
        Debug.Log($"[AdventureMap] ProgressManager not found. Stored fallback selected level: {levelNumber}.");
        LoadAdventureGameScene();
    }

    public void SetPage(int page)
    {
        currentPage = Mathf.Clamp(page, 0, TotalPages - 1);
        UpdatePage();
        Debug.Log($"[AdventureMap] Page changed to {currentPage + 1}/{TotalPages}.");
    }

    private void WireButtons()
    {
        CacheMissingNodeAmberImages();

        if (backButton != null && backButton.onClick.GetPersistentEventCount() == 0)
            backButton.onClick.AddListener(GoToMainMenu);

        if (leftPageButton != null && leftPageButton.onClick.GetPersistentEventCount() == 0)
            leftPageButton.onClick.AddListener(OnLeftPageButtonClicked);

        if (rightPageButton != null && rightPageButton.onClick.GetPersistentEventCount() == 0)
            rightPageButton.onClick.AddListener(OnRightPageButtonClicked);

        for (int i = 0; i < nodeButtons.Count; i++)
        {
            Button nodeButton = nodeButtons[i];
            if (nodeButton == null)
                continue;

            nodeButton.transition = Selectable.Transition.None;

            if (nodeButton.onClick.GetPersistentEventCount() > 0)
                continue;

            int capturedIndex = i;
            nodeButton.onClick.AddListener(() => OnNodeButtonClicked(capturedIndex));
        }
    }

    private void UpdatePage()
    {
        int highestLevelUnlocked = GetHighestLevelUnlocked();
        int visibleAmberNodes = 0;
        int currentLevel = highestLevelUnlocked;

        currentAmberTransform = null;
        currentAmberImage = null;

        for (int i = 0; i < nodeTexts.Count; i++)
        {
            TMP_Text nodeText = nodeTexts[i];
            int levelNumber = GetLevelNumberForNode(i);
            bool isUnlocked = levelNumber <= highestLevelUnlocked;

            if (nodeText != null)
            {
                nodeText.text = levelNumber.ToString();
                nodeText.color = levelTextColor;
            }

            Image amberImage = GetNodeAmberImage(i);
            if (amberImage != null)
            {
                bool showAmber = levelNumber <= highestLevelUnlocked;
                amberImage.gameObject.SetActive(showAmber);

                if (showAmber)
                {
                    visibleAmberNodes++;
                    bool isCurrent = levelNumber == highestLevelUnlocked;
                    amberImage.color = isCurrent ? currentAmberColor : completedAmberColor;
                    amberImage.transform.localScale = Vector3.one;

                    if (isCurrent)
                    {
                        currentAmberImage = amberImage;
                        currentAmberTransform = amberImage.rectTransform;
                        currentAmberBaseScale = currentAmberTransform.localScale;
                    }
                }
            }

            if (i < nodeButtons.Count && nodeButtons[i] != null)
                nodeButtons[i].interactable = isUnlocked;
        }

        if (leftPageButton != null)
            leftPageButton.gameObject.SetActive(currentPage > 0);

        if (rightPageButton != null)
            rightPageButton.gameObject.SetActive(currentPage < TotalPages - 1);

        Debug.Log($"[AdventureMap] Refresh page={currentPage}, highestLevelUnlocked={highestLevelUnlocked}, visibleAmberNodes={visibleAmberNodes}, currentLevel={currentLevel}");
    }

    private void Update()
    {
        if (currentAmberTransform == null || currentAmberImage == null || !currentAmberImage.gameObject.activeInHierarchy)
            return;

        float pulse = (Mathf.Sin(Time.unscaledTime * currentPulseSpeed) + 1f) * 0.5f;
        float scale = Mathf.Lerp(1f, currentPulseScale, pulse);
        currentAmberTransform.localScale = currentAmberBaseScale * scale;

        Color color = currentAmberColor;
        color.a = Mathf.Lerp(0.95f, currentAmberColor.a, pulse);
        currentAmberImage.color = color;
    }

    private int GetLevelNumberForNode(int nodeIndex)
    {
        return currentPage * LevelsPerPage + nodeIndex + 1;
    }

    private int GetHighestLevelUnlocked()
    {
        int highestLevelUnlocked = ProgressManager.Instance != null
            ? ProgressManager.Instance.highestLevelUnlocked
            : PlayerPrefs.GetInt(FallbackUnlockedLevelKey, 1);

        return Mathf.Clamp(highestLevelUnlocked, 1, TotalLevels);
    }

    private Image GetNodeAmberImage(int nodeIndex)
    {
        if (nodeIndex >= 0 && nodeIndex < nodeAmberImages.Count && nodeAmberImages[nodeIndex] != null)
            return nodeAmberImages[nodeIndex];

        return null;
    }

    private void CacheMissingNodeAmberImages()
    {
        while (nodeAmberImages.Count < LevelsPerPage)
            nodeAmberImages.Add(null);

        Image[] childImages = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < LevelsPerPage; i++)
        {
            if (nodeAmberImages[i] != null)
                continue;

            string expectedName = $"NodeAmber_{i + 1:00}";
            for (int imageIndex = 0; imageIndex < childImages.Length; imageIndex++)
            {
                if (childImages[imageIndex].name == expectedName)
                {
                    nodeAmberImages[i] = childImages[imageIndex];
                    break;
                }
            }

            if (nodeAmberImages[i] == null)
                Debug.LogWarning($"[AdventureMap] Amber image missing for node index {i}. Assign nodeAmberImages in the Inspector.");
        }
    }

    private void LoadAdventureGameScene()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadAdventureGameScene();
            return;
        }

        SceneManager.LoadScene("AdventureGameScene");
    }
}
