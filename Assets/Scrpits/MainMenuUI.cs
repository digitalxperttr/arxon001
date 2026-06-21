using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private const string MusicEnabledKey = "MusicEnabled";
    private const string SoundEnabledKey = "SoundEnabled";
    private const string HintEnabledKey = "HintEnabled";
    private const string VibrationEnabledKey = "VibrationEnabled";

    public TextMeshProUGUI mainMenuBestScoreText;
    public GameObject settingsPanel;
    public GameObject settingsModalDim;
    public Image musicToggleImage;
    public Image soundToggleImage;
    public Image hintToggleImage;
    public Image vibrationToggleImage;
    public Sprite toggleOnSprite;
    public Sprite toggleOffSprite;

    void Start()
    {
        // YENİ: Ana menü açıldığında hafızadaki rekoru ekrana bas
        if (mainMenuBestScoreText != null)
        {
            int best = PlayerPrefs.GetInt("ClassicBestScore", 0);
            mainMenuBestScoreText.text = $"Klasik Mod Rekoru: {best}";
        }

        SetSettingsVisible(false);
        ApplySavedSettings();
    }

    public void StartClassicMode()
    {
        // YENİ: Oyuna Klasik modda girdiğimizi belirtmek için seçili bölümü sıfırlıyoruz
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.ClearAdventureSelection();
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadClassicMode();
        }
    }
    public void StartAdventureMode()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadAdventureMap();
        }
    }

    public void OpenSettings()
    {
        SetSettingsVisible(true);
    }

    public void CloseSettings()
    {
        SetSettingsVisible(false);
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
