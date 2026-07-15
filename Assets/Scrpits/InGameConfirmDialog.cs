using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameConfirmDialog : MonoBehaviour
{
    private const string RestartMessage = "Oyunu yeniden başlatırsan mevcut ilerleme kaybedilecek.";
    private const string HomeMessage = "Ana menüye dönersen mevcut ilerleme kaybedilecek.";
    private const string SettingsOverlayName = "SettingsConfirmOverlay";
    private const float SettingsOverlayAlpha = 0.5f;

    private enum ConfirmAction
    {
        None,
        Restart,
        Home
    }

    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private GameObject confirmDim;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private GameSceneUI gameSceneUI;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool useAdventureRestartLayout;
    [SerializeField] private TextMeshProUGUI cancelButtonText;
    [SerializeField] private GameObject closeButton;
    [SerializeField] private Image settingsConfirmOverlay;

    private ConfirmAction pendingAction = ConfirmAction.None;

    private void Awake()
    {
        if (inputManager == null)
        {
            inputManager = FindAnyObjectByType<InputManager>();
        }

        if (gameSceneUI == null)
        {
            gameSceneUI = FindAnyObjectByType<GameSceneUI>();
        }
    }

    private void Start()
    {
        SetVisible(false);
        ApplyButtonLabels();
    }

    public void ShowRestartConfirm()
    {
        pendingAction = ConfirmAction.Restart;
        SetMessage(RestartMessage);
        Show();
    }

    public void ShowHomeConfirm()
    {
        pendingAction = ConfirmAction.Home;
        SetMessage(HomeMessage);
        Show();
    }

    public void Cancel()
    {
        if (useAdventureRestartLayout && pendingAction == ConfirmAction.Restart)
        {
            GoToAdventureMap();
            return;
        }

        Close();
    }

    public void Close()
    {
        pendingAction = ConfirmAction.None;
        SetPanelVisible(false);
        SetSettingsConfirmOverlayVisible(false);

        bool keepModalOpen = IsPanelOpen(settingsPanel) || IsPanelOpen(resultPanel);
        if (confirmDim != null)
        {
            confirmDim.SetActive(keepModalOpen);
        }

        Time.timeScale = keepModalOpen ? 0f : 1f;
        SetGameplayInputEnabled(!keepModalOpen);
    }

    public void Confirm()
    {
        ConfirmAction action = pendingAction;
        pendingAction = ConfirmAction.None;
        SetVisible(false);
        SetSettingsConfirmOverlayVisible(false);
        Time.timeScale = 1f;
        SetGameplayInputEnabled(true);

        if (action == ConfirmAction.Restart)
        {
            if (gameSceneUI != null)
            {
                gameSceneUI.RestartLevel();
            }
        }
        else if (action == ConfirmAction.Home)
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    private void Show()
    {
        ApplyButtonLabels();
        SetCloseButtonVisible(useAdventureRestartLayout && pendingAction == ConfirmAction.Restart);
        Time.timeScale = 0f;
        SetGameplayInputEnabled(false);
        SetVisible(true);
        SetSettingsConfirmOverlayVisible(IsPanelOpen(settingsPanel));

        if (confirmPanel != null)
        {
            confirmPanel.transform.SetAsLastSibling();
        }
    }

    private void SetVisible(bool isVisible)
    {
        if (confirmDim != null)
        {
            confirmDim.SetActive(isVisible);
        }

        SetPanelVisible(isVisible);
    }

    private void SetPanelVisible(bool isVisible)
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(isVisible);
        }

        if (!isVisible)
        {
            SetCloseButtonVisible(false);
        }
    }

    private void SetSettingsConfirmOverlayVisible(bool isVisible)
    {
        Image overlay = GetSettingsConfirmOverlay();
        if (overlay == null)
        {
            return;
        }

        overlay.gameObject.SetActive(isVisible);

        if (isVisible)
        {
            overlay.transform.SetAsLastSibling();
        }
    }

    private Image GetSettingsConfirmOverlay()
    {
        if (settingsConfirmOverlay != null)
        {
            return settingsConfirmOverlay;
        }

        if (settingsPanel == null)
        {
            return null;
        }

        Transform existingOverlay = settingsPanel.transform.Find(SettingsOverlayName);
        if (existingOverlay != null && existingOverlay.TryGetComponent(out settingsConfirmOverlay))
        {
            ConfigureSettingsConfirmOverlay(settingsConfirmOverlay);
            return settingsConfirmOverlay;
        }

        GameObject overlayObject = new GameObject(SettingsOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.layer = settingsPanel.layer;
        overlayObject.transform.SetParent(settingsPanel.transform, false);

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.pivot = new Vector2(0.5f, 0.5f);

        settingsConfirmOverlay = overlayObject.GetComponent<Image>();
        ConfigureSettingsConfirmOverlay(settingsConfirmOverlay);
        overlayObject.SetActive(false);

        return settingsConfirmOverlay;
    }

    private void ConfigureSettingsConfirmOverlay(Image overlay)
    {
        overlay.color = new Color(0f, 0f, 0f, SettingsOverlayAlpha);
        overlay.raycastTarget = true;
    }

    private bool IsPanelOpen(GameObject panel)
    {
        return panel != null && panel.activeInHierarchy;
    }

    private void SetGameplayInputEnabled(bool isEnabled)
    {
        if (inputManager != null)
        {
            inputManager.enabled = isEnabled;
        }
    }

    private void ApplyButtonLabels()
    {
        if (cancelButtonText != null)
        {
            bool isAdventureRestart = useAdventureRestartLayout && pendingAction == ConfirmAction.Restart;
            cancelButtonText.text = isAdventureRestart ? "HARİTA" : "İPTAL";
        }
    }

    private void SetCloseButtonVisible(bool isVisible)
    {
        if (closeButton != null)
        {
            closeButton.SetActive(isVisible);
        }
    }

    private void GoToAdventureMap()
    {
        pendingAction = ConfirmAction.None;
        SetVisible(false);
        SetSettingsConfirmOverlayVisible(false);
        Time.timeScale = 1f;
        SetGameplayInputEnabled(true);

        if (gameSceneUI != null && SceneLoader.Instance != null)
        {
            gameSceneUI.GoToMap();
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadAdventureMap();
            return;
        }

        SceneManager.LoadScene("AdventureMap");
    }
}
