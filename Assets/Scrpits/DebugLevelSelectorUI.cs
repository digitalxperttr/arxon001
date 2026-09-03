using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class DebugLevelSelectorUI : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool autoCreateButton = true;
    [SerializeField] private Vector2 buttonAnchoredPosition = new Vector2(25f, -65f); // Sol üst köşe
    [SerializeField] private Vector2 buttonSize = new Vector2(85f, 44f);

    private Canvas targetCanvas;
    private GameObject cornerButtonObj;
    private GameObject modalPanelObj;
    private TextMeshProUGUI levelDisplayText;
    private int selectedLevel = 1;

    private void Start()
    {
        InitializeUI();
    }

    public void InitializeUI()
    {
        if (targetCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas c in canvases)
            {
                if (c.name.Contains("HUD") || c.name.Contains("Canvas"))
                {
                    targetCanvas = c;
                    break;
                }
            }

            if (targetCanvas == null && canvases.Length > 0)
            {
                targetCanvas = canvases[0];
            }
        }

        if (targetCanvas == null)
        {
            Debug.LogWarning("[DebugLevelSelectorUI] Canvas bulunamadı!");
            return;
        }

        if (ScoreManager.Instance != null)
        {
            selectedLevel = Mathf.Max(1, ScoreManager.Instance.currentLevel);
        }
        else if (GridManager.SessionDebugStartLevel > 1)
        {
            selectedLevel = GridManager.SessionDebugStartLevel;
        }

        if (autoCreateButton && cornerButtonObj == null)
        {
            CreateCornerButton();
            CreateModalPanel();
        }
    }

    private void CreateCornerButton()
    {
        cornerButtonObj = new GameObject("Debug_Lvl_CornerButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cornerButtonObj.transform.SetParent(targetCanvas.transform, false);

        RectTransform rect = cornerButtonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = buttonAnchoredPosition;
        rect.sizeDelta = buttonSize;

        Image img = cornerButtonObj.GetComponent<Image>();
        img.color = new Color(0.08f, 0.10f, 0.14f, 0.75f);

        Button btn = cornerButtonObj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ToggleModal);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(cornerButtonObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "<b><color=#00E5FF>LVL</color></b>";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    private void CreateModalPanel()
    {
        // Karartma arkaplanı
        modalPanelObj = new GameObject("Debug_Level_ModalPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalPanelObj.transform.SetParent(targetCanvas.transform, false);

        RectTransform modalRect = modalPanelObj.GetComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.sizeDelta = Vector2.zero;

        Image modalImg = modalPanelObj.GetComponent<Image>();
        modalImg.color = new Color(0f, 0f, 0f, 0.7f);

        // Ana diyalog kutusu
        GameObject dialogObj = new GameObject("DialogBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        dialogObj.transform.SetParent(modalPanelObj.transform, false);

        RectTransform dialogRect = dialogObj.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(560f, 520f);
        dialogRect.anchoredPosition = Vector2.zero;

        Image dialogImg = dialogObj.GetComponent<Image>();
        dialogImg.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

        VerticalLayoutGroup layout = dialogObj.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 1. Başlık
        CreateTextElement(dialogObj.transform, "<b><color=#FFD700>TEST SEVİYE SEÇİCİ</color></b>", 24, 40f);

        // 2. Seviye Göstergesi
        GameObject levelTextObj = CreateTextElement(dialogObj.transform, GetLevelDisplayText(), 30, 45f);
        levelDisplayText = levelTextObj.GetComponent<TextMeshProUGUI>();

        // 3. Adım butonları satırı (-5, -1, +1, +5)
        GameObject stepRow = CreateHorizontalRow(dialogObj.transform, 50f, 10f);
        CreateButton(stepRow.transform, "-5", () => AdjustLevel(-5), new Color(0.25f, 0.28f, 0.35f));
        CreateButton(stepRow.transform, "-1", () => AdjustLevel(-1), new Color(0.25f, 0.28f, 0.35f));
        CreateButton(stepRow.transform, "+1", () => AdjustLevel(1), new Color(0.25f, 0.28f, 0.35f));
        CreateButton(stepRow.transform, "+5", () => AdjustLevel(5), new Color(0.25f, 0.28f, 0.35f));

        // 4. Hızlı seçim butonları satırı (1, 3, 5, 10, 15, 20)
        GameObject quickRow = CreateHorizontalRow(dialogObj.transform, 45f, 8f);
        int[] quickLevels = { 1, 3, 5, 10, 15, 20 };
        foreach (int lvl in quickLevels)
        {
            int targetLvl = lvl;
            CreateButton(quickRow.transform, $"Lv{targetLvl}", () => SetLevel(targetLvl), new Color(0.18f, 0.24f, 0.32f));
        }

        // 5. Uygulama Butonu
        CreateButton(dialogObj.transform, "<b><color=#00FF99>ANINDA UYGULA</color></b>", ApplyLevelImmediately, new Color(0.12f, 0.45f, 0.25f), 50f);

        // 6. Yeniden Başlat Butonu
        CreateButton(dialogObj.transform, "<b><color=#00E5FF>SEVİYEYLE YENİDEN BAŞLAT</color></b>", RestartWithSelectedLevel, new Color(0.15f, 0.35f, 0.55f), 50f);

        // 7. Kapat Butonu
        CreateButton(dialogObj.transform, "Kapat ✕", ToggleModal, new Color(0.35f, 0.15f, 0.18f), 40f);

        modalPanelObj.SetActive(false);
    }

    private string GetLevelDisplayText()
    {
        return $"Seçilen Seviye: <b><color=#00E5FF>{selectedLevel}</color></b>";
    }

    private void UpdateLevelDisplay()
    {
        if (levelDisplayText != null)
        {
            levelDisplayText.text = GetLevelDisplayText();
        }
    }

    public void AdjustLevel(int delta)
    {
        selectedLevel = Mathf.Clamp(selectedLevel + delta, 1, 50);
        UpdateLevelDisplay();
    }

    public void SetLevel(int level)
    {
        selectedLevel = Mathf.Clamp(level, 1, 50);
        UpdateLevelDisplay();
    }

    public void ToggleModal()
    {
        if (modalPanelObj != null)
        {
            bool isOpening = !modalPanelObj.activeSelf;
            if (isOpening && ScoreManager.Instance != null)
            {
                selectedLevel = Mathf.Max(1, ScoreManager.Instance.currentLevel);
                UpdateLevelDisplay();
            }

            modalPanelObj.SetActive(isOpening);
        }
    }

    public void ApplyLevelImmediately()
    {
        GridManager.SessionDebugStartLevel = selectedLevel;

        if (ScoreManager.Instance != null && (GridManager.Instance == null || GridManager.Instance.IsClassicRun()))
        {
            ScoreManager.Instance.SetDebugStartLevel(selectedLevel);
        }

        ToggleModal();
    }

    public void RestartWithSelectedLevel()
    {
        GridManager.SessionDebugStartLevel = selectedLevel;

        if (GridManager.Instance != null)
        {
            GridManager.Instance.ResetClassicRunState();
        }

        ToggleModal();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private GameObject CreateHorizontalRow(Transform parent, float height, float spacing)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, height);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        return row;
    }

    private GameObject CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick, Color bgColor, float height = 0f)
    {
        GameObject btnObj = new GameObject($"Btn_{text}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);

        if (height > 0f)
        {
            LayoutElement le = btnObj.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        Image img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btnObj;
    }

    private GameObject CreateTextElement(Transform parent, string text, float fontSize, float height)
    {
        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObj.transform.SetParent(parent, false);

        LayoutElement le = textObj.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return textObj;
    }
}
