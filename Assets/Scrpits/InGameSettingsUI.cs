using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameSettingsUI : MonoBehaviour
{
    private const string MusicEnabledKey = "MusicEnabled";
    private const string SoundEnabledKey = "SoundEnabled";
    private const string HintEnabledKey = "HintEnabled";
    private const string VibrationEnabledKey = "VibrationEnabled";
    private const string TutorialCompletedKey = "TutorialCompleted";

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject settingsModalDim;
    [SerializeField] private Image musicToggleImage;
    [SerializeField] private Image soundToggleImage;
    [SerializeField] private Image hintToggleImage;
    [SerializeField] private Image vibrationToggleImage;
    [SerializeField] private Sprite toggleOnSprite;
    [SerializeField] private Sprite toggleOffSprite;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private InputManager inputManager;

    private void Awake()
    {
        if (inputManager == null)
        {
            inputManager = FindAnyObjectByType<InputManager>();
        }
    }

    private void Start()
    {
        SetSettingsVisible(false);
        ApplySavedSettings();
    }

    public void OpenPanel()
    {
        ApplySavedSettings();
        Time.timeScale = 0f;
        SetGameplayInputEnabled(false);
        SetSettingsVisible(true);
    }

    public void ClosePanel()
    {
        SetSettingsVisible(false);
        Time.timeScale = 1f;
        SetGameplayInputEnabled(true);
    }

    public void ToggleMusic()
    {
        ToggleSetting(MusicEnabledKey, musicToggleImage);
    }

    public void ToggleSound()
    {
        ToggleSetting(SoundEnabledKey, soundToggleImage);
    }

    public void ToggleHint()
    {
        ToggleSetting(HintEnabledKey, hintToggleImage);
    }

    public void ToggleVibration()
    {
        ToggleSetting(VibrationEnabledKey, vibrationToggleImage);
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        SetGameplayInputEnabled(true);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ResetClassicScoreForTesting()
    {
        PlayerPrefs.SetInt(TutorialCompletedKey, 0);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetClassicScoreAndBestForTesting();
            return;
        }

        PlayerPrefs.DeleteKey("ClassicBestScore");
        PlayerPrefs.Save();
    }

    private void SetGameplayInputEnabled(bool isEnabled)
    {
        if (inputManager != null)
        {
            inputManager.enabled = isEnabled;
        }
    }

    private void SetSettingsVisible(bool isVisible)
    {
        if (settingsModalDim != null)
        {
            settingsModalDim.SetActive(isVisible);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(isVisible);
        }
    }

    private void ApplySavedSettings()
    {
        ApplyToggleSprite(musicToggleImage, IsSettingEnabled(MusicEnabledKey));
        ApplyToggleSprite(soundToggleImage, IsSettingEnabled(SoundEnabledKey));
        ApplyToggleSprite(hintToggleImage, IsSettingEnabled(HintEnabledKey));
        ApplyToggleSprite(vibrationToggleImage, IsSettingEnabled(VibrationEnabledKey));
    }

    private void ToggleSetting(string key, Image targetImage)
    {
        bool isEnabled = !IsSettingEnabled(key);
        PlayerPrefs.SetInt(key, isEnabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToggleSprite(targetImage, isEnabled);
    }

    private bool IsSettingEnabled(string key)
    {
        return PlayerPrefs.GetInt(key, 1) == 1;
    }

    private void ApplyToggleSprite(Image targetImage, bool isEnabled)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.sprite = isEnabled ? toggleOnSprite : toggleOffSprite;
    }
}
