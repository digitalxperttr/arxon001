using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LanguageData", menuName = "Localization/LanguageData")]
public class LanguageData : ScriptableObject
{
    public List<LocalizationEntry> entries = new List<LocalizationEntry>();

    // Anahtara (key) göre doğru çeviriyi bulan yardımcı fonksiyon
    public string GetText(string key, Language lang)
    {
        LocalizationEntry entry = entries.Find(e => e.key == key);
        if (entry == null) return "KEY_NOT_FOUND: " + key;

        return lang == Language.TR ? entry.tr : entry.en;
    }
}

[System.Serializable]
public class LocalizationEntry
{
    public string key;
    public string tr;
    public string en;
}