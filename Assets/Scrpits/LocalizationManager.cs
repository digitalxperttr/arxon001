using UnityEngine;

public enum Language { TR, EN }

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;
    
    public Language currentLanguage = Language.TR;
    public LanguageData languageData;
    public bool isReady = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Dil seçimini burada yapıyoruz
            SetupLanguage();
            
            isReady = true;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void SetupLanguage()
    {
        // Cihaz diline göre otomatik seçim
        if (Application.systemLanguage == SystemLanguage.Turkish)
            currentLanguage = Language.TR;
        else
            currentLanguage = Language.EN;
    }

    public string GetTranslation(string key)
    {
        if (languageData == null) return key;
        return languageData.GetText(key, currentLanguage);
    }

    // Dili çalışma anında değiştirmek istersen diye (Butonlar için)
    public void ChangeLanguage(Language newLang)
    {
        currentLanguage = newLang;
        // Puan yazısını hemen güncellemesi için ScoreManager'a haber verilebilir
        if(ScoreManager.Instance != null) ScoreManager.Instance.UpdateScoreUI();
    }
}