using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveHUD : MonoBehaviour
{
    public static ObjectiveHUD Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CollectibleDatabase collectibleDatabase;
    [SerializeField] private RectTransform objectivePanel;
    [SerializeField] private RectTransform largeObjectiveArea;
    [SerializeField] private RectTransform levelNumberArea;
    [SerializeField] private TMP_Text levelNumberText;
    [SerializeField] private TMP_Text objectiveDescriptionText;

    [Header("Scene Authored HUD")]
    [SerializeField] private ObjectiveSlot[] objectiveSlots;

    [Header("Prefabs")]
    [SerializeField] private ObjectiveSlot objectiveSlotPrefab;
    [SerializeField] private ObjectiveRow rowPrefab;

    [Header("Icons")]
    [SerializeField] private Sprite genericRowIcon;
    [SerializeField] private Sprite genericScoreIcon;
    [SerializeField] private Sprite checkmarkSprite;

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.15f;

    private readonly List<ObjectiveSlot> slots = new List<ObjectiveSlot>();
    private readonly List<ObjectiveRuntimeState> states = new List<ObjectiveRuntimeState>();
    private readonly List<int> lastCurrentAmounts = new List<int>();
    private readonly List<bool> lastCompleteStates = new List<bool>();
    private Coroutine refreshRoutine;
    private bool hasBuilt;

    private static readonly Color InProgressColor = Color.white;
    private static readonly Color CompleteColor = new Color(0.25f, 1f, 0.45f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        StartCoroutine(DelayedBuildRoutine());
    }

    private IEnumerator DelayedBuildRoutine()
    {
        if (!ShouldShowForCurrentRun())
        {
            if (ProgressManager.Instance == null || ProgressManager.Instance.currentSelectedLevel == null)
            {
                gameObject.SetActive(false);
                yield break;
            }

            float elapsed = 0f;
            while (!ShouldShowForCurrentRun() && elapsed < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (hasBuilt)
        {
            yield break;
        }

        if (!ShouldShowForCurrentRun())
        {
            gameObject.SetActive(false);
            yield break;
        }

        BuildFromObjectiveManager();
    }

    public static ObjectiveHUD EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ObjectiveHUD sceneAuthoredHud = FindSceneAuthoredInstance();
        if (sceneAuthoredHud != null)
        {
            sceneAuthoredHud.gameObject.SetActive(true);
            Instance = sceneAuthoredHud;
            return sceneAuthoredHud;
        }

        RectTransform centerContentHost = FindExistingCenterObjectiveHost();
        if (centerContentHost == null)
        {
            Debug.LogWarning("ObjectiveHUD: Existing center HUD content area was not found.");
            return null;
        }

        centerContentHost.gameObject.SetActive(true);

        GameObject hudObject = new GameObject("ObjectiveHUDContent", typeof(RectTransform));
        hudObject.transform.SetParent(centerContentHost, false);

        RectTransform rect = hudObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return hudObject.AddComponent<ObjectiveHUD>();
    }

    public void BuildFromObjectiveManager()
    {
        if (!ShouldShowForCurrentRun() || ObjectiveManager.Instance == null || !ObjectiveManager.Instance.IsActive)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ResolveReferences();
        PrepareSlotsForBuild();
        hasBuilt = true;
        SetLevelNumberText();

        bool usingSceneAuthoredSlots = HasSceneAuthoredObjectiveSlots();
        IReadOnlyList<ObjectiveRuntimeState> objectiveStates = ObjectiveManager.Instance.GetObjectives();
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null)
            {
                continue;
            }

            ObjectiveSlot slot = usingSceneAuthoredSlots ? GetSceneAuthoredSlot(i) : CreateSlot();
            if (slot == null)
            {
                Debug.LogWarning($"ObjectiveHUD: Missing scene-authored objective slot for objective index {i}.");
                continue;
            }

            slot.gameObject.SetActive(true);
            slots.Add(slot);
            states.Add(state);
            lastCurrentAmounts.Add(-1);
            lastCompleteStates.Add(false);
            RefreshSlot(slots.Count - 1, true);
        }

        SetObjectiveDescriptionText();

        if (usingSceneAuthoredSlots)
        {
            DeactivateUnusedSceneAuthoredSlots(objectiveStates.Count);
        }

        Debug.Log($"Objective HUD Created\nObjectives:\n{slots.Count}");

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
        }

        refreshRoutine = StartCoroutine(RefreshRoutine());
    }

    public void RefreshLocalizedText()
    {
        SetObjectiveDescriptionText();
    }

    private IEnumerator RefreshRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, refreshInterval));

        while (true)
        {
            RefreshChangedRows();
            yield return wait;
        }
    }

    private void RefreshChangedRows()
    {
        bool shouldRefreshDescription = false;

        for (int i = 0; i < states.Count; i++)
        {
            ObjectiveRuntimeState state = states[i];
            if (state == null)
            {
                continue;
            }

            if (state.currentAmount != lastCurrentAmounts[i] ||
                state.IsComplete != lastCompleteStates[i])
            {
                shouldRefreshDescription = true;
                RefreshSlot(i, false);
            }
        }

        if (shouldRefreshDescription)
        {
            SetObjectiveDescriptionText();
        }
    }

    private void RefreshSlot(int index, bool forceStaticContent)
    {
        if (index < 0 || index >= slots.Count || index >= states.Count)
        {
            return;
        }

        ObjectiveSlot slot = slots[index];
        ObjectiveRuntimeState state = states[index];
        if (slot == null || state == null)
        {
            return;
        }

        if (forceStaticContent)
        {
            if (slot.descriptionText != null)
            {
                slot.descriptionText.gameObject.SetActive(false);
            }

            if (slot.iconImage != null)
            {
                Sprite icon = GetIcon(state);
                slot.iconImage.sprite = icon;
                slot.iconImage.enabled = ShouldShowIcon(state) && icon != null;
                slot.iconImage.gameObject.SetActive(slot.iconImage.enabled);
                slot.iconImage.preserveAspect = true;
            }
        }

        if (slot.progressText != null)
        {
            slot.progressText.text = $"{state.currentAmount}/{state.requiredAmount}";
            slot.progressText.color = state.IsComplete ? CompleteColor : InProgressColor;
            slot.progressText.alignment = ShouldShowIcon(state)
                ? TextAlignmentOptions.MidlineLeft
                : TextAlignmentOptions.Center;
        }

        if (slot.completedCheckmarkImage != null)
        {
            slot.completedCheckmarkImage.sprite = checkmarkSprite;
            slot.completedCheckmarkImage.enabled = false;
            slot.completedCheckmarkImage.gameObject.SetActive(false);
            slot.completedCheckmarkImage.preserveAspect = true;
        }

        lastCurrentAmounts[index] = state.currentAmount;
        lastCompleteStates[index] = state.IsComplete;
    }

    private ObjectiveSlot CreateSlot()
    {
        if (objectiveSlotPrefab != null)
        {
            ObjectiveSlot slot = Instantiate(objectiveSlotPrefab, largeObjectiveArea);
            slot.gameObject.SetActive(true);
            if (slot.descriptionText != null)
            {
                slot.descriptionText.gameObject.SetActive(false);
            }

            return slot;
        }

        if (rowPrefab != null)
        {
            ObjectiveRow row = Instantiate(rowPrefab, largeObjectiveArea);
            row.gameObject.SetActive(true);
            if (row.descriptionText != null)
            {
                row.descriptionText.gameObject.SetActive(false);
            }

            ObjectiveSlot slot = row.gameObject.GetComponent<ObjectiveSlot>();
            if (slot == null)
            {
                slot = row.gameObject.AddComponent<ObjectiveSlot>();
            }

            slot.iconImage = row.iconImage;
            slot.progressText = row.progressText;
            slot.completedCheckmarkImage = row.completedCheckmarkImage;
            slot.descriptionText = row.descriptionText;
            return slot;
        }

        return CreateDefaultSlot();
    }

    private ObjectiveSlot CreateDefaultSlot()
    {
        GameObject slotObject = new GameObject("ObjectiveSlot", typeof(RectTransform));
        slotObject.transform.SetParent(largeObjectiveArea, false);

        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(148f, 54f);

        HorizontalLayoutGroup horizontalLayout = slotObject.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
        horizontalLayout.spacing = 8f;
        horizontalLayout.padding = new RectOffset(4, 4, 3, 3);
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;

        ObjectiveSlot slot = slotObject.AddComponent<ObjectiveSlot>();
        slot.iconImage = CreateImage("Icon", slotObject.transform, new Vector2(42f, 42f));

        slot.progressText = CreateText("Progress", slotObject.transform, 30, TextAlignmentOptions.MidlineLeft);
        LayoutElement progressLayout = slot.progressText.gameObject.AddComponent<LayoutElement>();
        progressLayout.preferredWidth = 76f;

        slot.completedCheckmarkImage = CreateImage("CompletedCheckmark", slotObject.transform, new Vector2(28f, 28f));
        slot.completedCheckmarkImage.enabled = false;
        slot.completedCheckmarkImage.gameObject.SetActive(false);

        slot.descriptionText = CreateText("Description", slotObject.transform, 20, TextAlignmentOptions.MidlineLeft);
        slot.descriptionText.gameObject.SetActive(false);

        return slot;
    }

    private bool ShouldShowIcon(ObjectiveRuntimeState state)
    {
        return state != null &&
               state.definition != null &&
               state.definition.action == AdventureObjectiveAction.CollectItem;
    }

    private Image CreateImage(string name, Transform parent, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        LayoutElement layout = imageObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private TMP_Text CreateText(string name, Transform parent, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void ResolveReferences()
    {
        if (objectivePanel == null)
        {
            objectivePanel = transform as RectTransform;
        }

        if (largeObjectiveArea == null)
        {
            largeObjectiveArea = objectivePanel;
        }

        if (!HasSceneAuthoredObjectiveSlots())
        {
            DisableReplacedCenterText();
            ConfigureObjectiveAreaLayout();
        }

        if (levelNumberText == null)
        {
            levelNumberText = CreateLevelNumberTextInExistingRightStone();
        }

        if (levelNumberText != null)
        {
            levelNumberArea = levelNumberText.transform.parent as RectTransform;
        }

        if (objectiveDescriptionText == null)
        {
            RectTransform existingDescription = FindInactiveRectTransformByName("ObjectiveDescriptionText");
            if (existingDescription != null)
            {
                objectiveDescriptionText = existingDescription.GetComponent<TMP_Text>();
            }
        }

        if (collectibleDatabase == null && GridManager.Instance != null)
        {
            collectibleDatabase = GridManager.Instance.CollectibleDatabase;
        }
    }

    private void DisableReplacedCenterText()
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            return;
        }

        TMP_Text centerText = parent.GetComponent<TMP_Text>();
        if (centerText != null)
        {
            centerText.enabled = false;
        }
    }

    private void ConfigureObjectiveAreaLayout()
    {
        if (largeObjectiveArea == null)
        {
            return;
        }

        Image accidentalImage = largeObjectiveArea.GetComponent<Image>();
        if (accidentalImage != null)
        {
            accidentalImage.enabled = false;
        }

        HorizontalLayoutGroup horizontalLayout = largeObjectiveArea.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout == null)
        {
            horizontalLayout = largeObjectiveArea.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
        horizontalLayout.spacing = 16f;
        horizontalLayout.padding = new RectOffset(8, 8, 4, 4);
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = largeObjectiveArea.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            Destroy(fitter);
        }
    }

    private TMP_Text CreateLevelNumberTextInExistingRightStone()
    {
        RectTransform bestScoreRect = FindInactiveRectTransformByName("BestScoreText");
        if (bestScoreRect == null || bestScoreRect.parent == null)
        {
            return null;
        }

        RectTransform existingLevelText = FindInactiveRectTransformByName("AdventureLevelNumberText");
        if (existingLevelText != null && existingLevelText.TryGetComponent(out TMP_Text existingText))
        {
            ModeBasedHudVisibility.RegisterAdventureOnlyObjectInScene(existingLevelText.gameObject);
            return existingText;
        }

        GameObject textObject = new GameObject("AdventureLevelNumberText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(bestScoreRect.parent, false);
        ModeBasedHudVisibility.RegisterAdventureOnlyObjectInScene(textObject);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = bestScoreRect.anchorMin;
        rect.anchorMax = bestScoreRect.anchorMax;
        rect.pivot = bestScoreRect.pivot;
        rect.anchoredPosition = bestScoreRect.anchoredPosition + new Vector2(0f, -44f);
        rect.sizeDelta = new Vector2(bestScoreRect.sizeDelta.x, 36f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text bestScoreText = bestScoreRect.GetComponent<TMP_Text>();
        if (bestScoreText != null)
        {
            text.font = bestScoreText.font;
            text.fontSharedMaterial = bestScoreText.fontSharedMaterial;
            text.color = bestScoreText.color;
        }
        else
        {
            text.color = Color.white;
        }

        text.raycastTarget = false;
        text.fontSize = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void PrepareSlotsForBuild()
    {
        if (HasSceneAuthoredObjectiveSlots())
        {
            DeactivateUnusedSceneAuthoredSlots(0);
            ClearSlotState();
            return;
        }

        ClearRuntimeSlots();
    }

    private void ClearRuntimeSlots()
    {
        if (largeObjectiveArea == null)
        {
            ClearSlotState();
            return;
        }

        for (int i = largeObjectiveArea.childCount - 1; i >= 0; i--)
        {
            Destroy(largeObjectiveArea.GetChild(i).gameObject);
        }

        ClearSlotState();
    }

    private void ClearSlotState()
    {
        slots.Clear();
        states.Clear();
        lastCurrentAmounts.Clear();
        lastCompleteStates.Clear();
    }

    private bool HasSceneAuthoredObjectiveSlots()
    {
        if (objectiveSlots == null)
        {
            return false;
        }

        for (int i = 0; i < objectiveSlots.Length; i++)
        {
            if (objectiveSlots[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private ObjectiveSlot GetSceneAuthoredSlot(int index)
    {
        if (objectiveSlots == null || index < 0 || index >= objectiveSlots.Length)
        {
            return null;
        }

        return objectiveSlots[index];
    }

    private void DeactivateUnusedSceneAuthoredSlots(int usedCount)
    {
        if (objectiveSlots == null)
        {
            return;
        }

        for (int i = 0; i < objectiveSlots.Length; i++)
        {
            ObjectiveSlot slot = objectiveSlots[i];
            if (slot != null)
            {
                slot.gameObject.SetActive(i < usedCount);
            }
        }
    }

    private static ObjectiveHUD FindSceneAuthoredInstance()
    {
        ObjectiveHUD[] huds = FindObjectsByType<ObjectiveHUD>(FindObjectsInactive.Include);

        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null)
            {
                return huds[i];
            }
        }

        return null;
    }

    private void SetObjectiveDescriptionText()
    {
        if (objectiveDescriptionText == null)
        {
            return;
        }

        string descriptionKey = GetObjectiveGroupDescriptionKey();
        if (string.IsNullOrWhiteSpace(descriptionKey))
        {
            objectiveDescriptionText.gameObject.SetActive(false);
            return;
        }

        objectiveDescriptionText.gameObject.SetActive(true);
        objectiveDescriptionText.text = GetLocalizedText(descriptionKey);
    }

    private string GetObjectiveGroupDescriptionKey()
    {
        string descriptionKey = null;

        for (int i = 0; i < states.Count; i++)
        {
            ObjectiveRuntimeState state = states[i];
            if (state == null || state.definition == null)
            {
                continue;
            }

            string stateKey = GetObjectiveDescriptionKey(state.definition);
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                continue;
            }

            if (descriptionKey == null)
            {
                descriptionKey = stateKey;
                continue;
            }

            if (descriptionKey != stateKey)
            {
                return "objective_description_multiple";
            }
        }

        return descriptionKey;
    }

    private string GetObjectiveDescriptionKey(AdventureObjectiveDefinition definition)
    {
        switch (definition.action)
        {
            case AdventureObjectiveAction.ReachScore:
                return "objective_description_reach_score";
            case AdventureObjectiveAction.ClearRows:
                return "objective_description_clear_rows";
            case AdventureObjectiveAction.CollectItem:
                return GetCollectibleObjectiveDescriptionKey(definition.collectibleId);
            case AdventureObjectiveAction.DestroyObstacle:
                return GetObstacleObjectiveDescriptionKey(definition.target);
            case AdventureObjectiveAction.BreakChain:
                return "objective_description_break_chains";
            case AdventureObjectiveAction.ComboTarget:
                return "objective_description_combo";
            default:
                return "objective_description_default";
        }
    }

    private string GetCollectibleObjectiveDescriptionKey(string collectibleId)
    {
        CollectibleDefinition collectible = GetCollectible(collectibleId);
        if (collectible == null || collectible.category != CollectibleCategory.Crystal)
        {
            return "objective_description_collect_items";
        }

        switch (collectible.color)
        {
            case CollectibleColor.Blue:
                return "objective_description_collect_blue_crystals";
            case CollectibleColor.Purple:
                return "objective_description_collect_purple_crystals";
            case CollectibleColor.Pink:
                return "objective_description_collect_pink_crystals";
            case CollectibleColor.Yellow:
                return "objective_description_collect_yellow_crystals";
            case CollectibleColor.Orange:
                return "objective_description_collect_orange_crystals";
            case CollectibleColor.Green:
                return "objective_description_collect_green_crystals";
            default:
                return "objective_description_collect_crystals";
        }
    }

    private string GetObstacleObjectiveDescriptionKey(AdventureObjectiveTarget target)
    {
        switch (target)
        {
            case AdventureObjectiveTarget.Rock:
                return "objective_description_destroy_rocks";
            case AdventureObjectiveTarget.Ice:
                return "objective_description_destroy_ice";
            default:
                return "objective_description_destroy_obstacles";
        }
    }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance == null)
        {
            return key;
        }

        string localized = LocalizationManager.Instance.GetTranslation(key);
        return localized.StartsWith("KEY_NOT_FOUND:") ? key : localized;
    }

    private Sprite GetIcon(ObjectiveRuntimeState state)
    {
        AdventureObjectiveDefinition definition = state.definition;
        if (definition == null)
        {
            return null;
        }

        if (definition.action == AdventureObjectiveAction.CollectItem)
        {
            if (definition.displayIcon != null)
            {
                return definition.displayIcon;
            }

            CollectibleDefinition collectible = GetCollectible(definition.collectibleId);
            return collectible != null ? collectible.icon : null;
        }

        if (definition.action == AdventureObjectiveAction.ReachScore)
        {
            return genericScoreIcon;
        }

        return genericRowIcon;
    }

    private string GetCollectibleDisplayName(string collectibleId)
    {
        CollectibleDefinition collectible = GetCollectible(collectibleId);
        if (collectible != null && !string.IsNullOrWhiteSpace(collectible.displayName))
        {
            return collectible.displayName;
        }

        return collectibleId;
    }

    private CollectibleDefinition GetCollectible(string collectibleId)
    {
        if (collectibleDatabase == null || string.IsNullOrWhiteSpace(collectibleId))
        {
            return null;
        }

        return collectibleDatabase.GetById(collectibleId);
    }

    private void SetLevelNumberText()
    {
        if (levelNumberText == null)
        {
            return;
        }

        levelNumberText.text = GetCurrentAdventureLevelNumber().ToString();
    }

    private int GetCurrentAdventureLevelNumber()
    {
        if (ProgressManager.Instance == null)
        {
            return 1;
        }

        AdventureLevelConfig config = ProgressManager.Instance.currentSelectedAdventureConfig;
        if (config != null)
        {
            return Mathf.Max(1, config.levelNumber);
        }

        if (ProgressManager.Instance.currentSelectedLevelNumber > 0)
        {
            return ProgressManager.Instance.currentSelectedLevelNumber;
        }

        LevelData levelData = ProgressManager.Instance.currentSelectedLevel;
        return levelData != null ? Mathf.Max(1, levelData.levelNumber) : 1;
    }

    private bool ShouldShowForCurrentRun()
    {
        return ProgressManager.Instance != null &&
               ProgressManager.Instance.currentSelectedLevel != null &&
               ObjectiveManager.Instance != null &&
               ObjectiveManager.Instance.IsActive;
    }

    private static RectTransform FindExistingCenterObjectiveHost()
    {
        RectTransform targetText = FindInactiveRectTransformByName("TargetText");
        if (targetText != null)
        {
            return targetText;
        }

        RectTransform levelText = FindInactiveRectTransformByName("LevelText");
        if (levelText != null)
        {
            return levelText;
        }

        return FindInactiveRectTransformByName("puan_tablo");
    }

    private static RectTransform FindInactiveRectTransformByName(string objectName)
    {
        RectTransform[] rectTransforms = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform != null && rectTransform.name == objectName)
            {
                return rectTransform;
            }
        }

        return null;
    }
}
