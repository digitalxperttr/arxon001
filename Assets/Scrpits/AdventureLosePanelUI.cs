using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdventureLosePanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Image panelBackground;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private RectTransform objectiveListRoot;
    [SerializeField] private AdventureObjectiveResultRow[] objectiveRows = new AdventureObjectiveResultRow[3];
    [SerializeField] private CollectibleDatabase collectibleDatabase;
    [SerializeField] private Sprite genericRowIcon;
    [SerializeField] private Sprite genericScoreIcon;
    [SerializeField] private Sprite completedStatusSprite;
    [SerializeField] private Sprite incompleteStatusSprite;
    [SerializeField] private GameObject sharedModalDim;

    private InputManager disabledInputManager;

    private void Awake()
    {
        ResolveMissingReferences();
    }

    private void OnEnable()
    {
        Show();
    }

    private void OnDisable()
    {
        SetSharedModalDimVisible(false);

        if (disabledInputManager != null)
        {
            disabledInputManager.enabled = true;
            disabledInputManager = null;
        }

        Time.timeScale = 1f;
    }

    public void Show()
    {
        ResolveMissingReferences();

        if (titleText != null)
            titleText.text = "TEKRAR DENE!";

        HideOtherAdventurePanels();
        SetSharedModalDimVisible(true);
        transform.SetAsLastSibling();

        AdventureObjectiveResultList.ApplyPanelSprite(panelBackground, panelSprite);
        AdventureObjectiveResultList.Build(
            objectiveRows,
            collectibleDatabase,
            genericRowIcon,
            genericScoreIcon,
            completedStatusSprite,
            incompleteStatusSprite,
            false);

        Time.timeScale = 0f;

        InputManager inputManager = FindAnyObjectByType<InputManager>();
        if (inputManager != null && inputManager.enabled)
        {
            inputManager.enabled = false;
            disabledInputManager = inputManager;
        }
    }

    private void ResolveMissingReferences()
    {
        if (panelRoot == null)
            panelRoot = transform as RectTransform;

        if (panelBackground == null)
            panelBackground = GetComponent<Image>();

        if (titleText == null)
            titleText = transform.Find("kazandin")?.GetComponent<TextMeshProUGUI>();

        if (objectiveListRoot == null)
            objectiveListRoot = transform.Find("LoseObjectiveResults") as RectTransform;

        if (sharedModalDim == null)
            sharedModalDim = FindSharedModalDim();

        if (collectibleDatabase == null && GridManager.Instance != null)
            collectibleDatabase = GridManager.Instance.CollectibleDatabase;
    }

    private GameObject FindSharedModalDim()
    {
        Transform current = transform;
        while (current != null)
        {
            Transform dim = current.Find("SharedModalDim");
            if (dim != null)
                return dim.gameObject;

            current = current.parent;
        }

        return null;
    }

    private void SetSharedModalDimVisible(bool isVisible)
    {
        if (sharedModalDim != null)
            sharedModalDim.SetActive(isVisible);
    }

    private void HideOtherAdventurePanels()
    {
        if (transform.parent == null)
            return;

        for (int i = 0; i < transform.parent.childCount; i++)
        {
            Transform sibling = transform.parent.GetChild(i);
            if (sibling == transform || !sibling.gameObject.activeSelf)
                continue;

            if (sibling.GetComponent<AdventureVictoryPanelUI>() != null)
                sibling.gameObject.SetActive(false);
        }
    }
}
