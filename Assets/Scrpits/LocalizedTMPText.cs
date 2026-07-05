using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedTMPText : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    private TMP_Text targetText;

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
        ApplyLocalization();
    }

    private void OnEnable()
    {
        ApplyLocalization();
    }

    public void ApplyLocalization()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (targetText == null || string.IsNullOrWhiteSpace(localizationKey))
        {
            return;
        }

        if (LocalizationManager.Instance != null)
        {
            targetText.text = LocalizationManager.Instance.GetTranslation(localizationKey);
        }
    }
}
