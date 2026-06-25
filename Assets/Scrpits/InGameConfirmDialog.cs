using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InGameConfirmDialog : MonoBehaviour
{
    private const string RestartMessage = "Oyunu yeniden başlatırsan mevcut ilerleme kaybedilecek.";
    private const string HomeMessage = "Ana menüye dönersen mevcut ilerleme kaybedilecek.";

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
        pendingAction = ConfirmAction.None;
        SetPanelVisible(false);

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
        Time.timeScale = 0f;
        SetGameplayInputEnabled(false);
        SetVisible(true);
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
}
