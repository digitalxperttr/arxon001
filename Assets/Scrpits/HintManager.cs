using UnityEngine;

public class HintManager : MonoBehaviour
{
    private const string HintEnabledKey = "HintEnabled";

    public static HintManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Tahta IDLE iken ipucunun tetiklenmesi için gereken boşta kalma süresi (saniye)")]
    [SerializeField] private float idleDelay = 5.0f;
    [SerializeField] private HintVisualGuide visualGuide;

    private float idleTimer = 0f;
    private bool isHintShowing = false;
    private GridManager grid;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureVisualGuide();
    }

    private void OnEnable()
    {
        InputManager.UserInputStarted += HandleUserInputStarted;
        InputManager.SuccessfulPlacement += HandleSuccessfulPlacement;
    }

    private void OnDisable()
    {
        InputManager.UserInputStarted -= HandleUserInputStarted;
        InputManager.SuccessfulPlacement -= HandleSuccessfulPlacement;
        DismissHint();
    }

    private void Start()
    {
        if (grid == null)
        {
            grid = GridManager.Instance != null ? GridManager.Instance : FindAnyObjectByType<GridManager>();
        }
    }

    private void Update()
    {
        if (FirstTimeTutorial.Instance != null && FirstTimeTutorial.Instance.IsRunning)
        {
            if (isHintShowing) DismissHint();
            idleTimer = 0f;
            return;
        }

        if (!IsHintSettingEnabled())
        {
            if (isHintShowing) DismissHint();
            idleTimer = 0f;
            return;
        }

        if (grid == null)
        {
            grid = GridManager.Instance != null ? GridManager.Instance : FindAnyObjectByType<GridManager>();
            if (grid == null) return;
        }

        if (grid.isGameOver || grid.currentState != GameState.IDLE || grid.IsBoardBusy)
        {
            if (isHintShowing)
            {
                DismissHint();
            }
            idleTimer = 0f;
            return;
        }

        if (isHintShowing)
            return;

        idleTimer += Time.deltaTime;
        if (idleTimer >= idleDelay)
        {
            TryShowHint();
        }
    }

    public void TryShowHint()
    {
        if (grid == null) return;

        if (HintSolver.TryFindBestHint(grid, out HintMove bestHint))
        {
            EnsureVisualGuide();
            if (visualGuide != null && bestHint.block != null)
            {
                visualGuide.Show(bestHint.block, bestHint);
                isHintShowing = true;
            }
        }
    }

    public void DismissHint()
    {
        idleTimer = 0f;
        isHintShowing = false;

        if (visualGuide != null)
        {
            visualGuide.Hide();
        }
    }

    private void HandleUserInputStarted()
    {
        DismissHint();
    }

    private void HandleSuccessfulPlacement()
    {
        DismissHint();
    }

    private bool IsHintSettingEnabled()
    {
        return PlayerPrefs.GetInt(HintEnabledKey, 1) == 1;
    }

    private void EnsureVisualGuide()
    {
        if (visualGuide == null)
        {
            visualGuide = FindAnyObjectByType<HintVisualGuide>();
        }

        if (visualGuide == null)
        {
            GameObject guideObj = new GameObject("HintVisualGuide");
            guideObj.transform.SetParent(transform);
            visualGuide = guideObj.AddComponent<HintVisualGuide>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
