using UnityEngine;

public class GameSceneUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image adventureGameplayBackground;
    private Sprite defaultGameplayBackground;
    private Sprite appliedGameplayBackground;
    private bool hasAppliedGameplayBackground;

    private void Awake()
    {
        if (adventureGameplayBackground != null)
            defaultGameplayBackground = adventureGameplayBackground.sprite;
    }

    private void LateUpdate()
    {
        // Only the Adventure scene assigns this reference. Classic is untouched.
        if (adventureGameplayBackground == null) return;
        ProgressManager progress = ProgressManager.Instance;
        AdventureEventConfig localEvent = progress != null ? progress.SelectedLocalEvent : null;
        AdventureAttemptSnapshot attempt = progress != null ? progress.CurrentAdventureAttempt : null;
        Sprite selected = defaultGameplayBackground;
        if (localEvent != null && attempt != null && attempt.Identity != null &&
            localEvent.eventId == attempt.Identity.EventId &&
            localEvent.contentVersion == attempt.Identity.ContentVersion &&
            localEvent.gameplayBackgroundVisual != null)
            selected = localEvent.gameplayBackgroundVisual;

        if (hasAppliedGameplayBackground && appliedGameplayBackground == selected) return;
        adventureGameplayBackground.sprite = selected;
        adventureGameplayBackground.raycastTarget = false;
        adventureGameplayBackground.type = UnityEngine.UI.Image.Type.Simple;
        adventureGameplayBackground.preserveAspect = false;
        var fitter = adventureGameplayBackground.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        if (selected != null)
        {
            if (fitter == null)
                fitter = adventureGameplayBackground.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = selected.rect.width / selected.rect.height;
            fitter.enabled = true;
        }
        else if (fitter != null) fitter.enabled = false;
        appliedGameplayBackground = selected;
        hasAppliedGameplayBackground = true;
    }

    // "Tekrar Dene" butonuna bağlanacak
    public void RestartLevel()
    {
        Time.timeScale = 1f; // Donmuş zamanı çöz!
        bool isClassicRun = GridManager.Instance == null || GridManager.Instance.IsClassicRun();

        if (isClassicRun)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetScoreAndLevel();

            if (GridManager.Instance != null)
                GridManager.Instance.ResetClassicRunState();
        }
        else if (ProgressManager.Instance != null && LevelManager.Instance != null)
        {
            ProgressManager.Instance.RestoreAdventureAttempt(LevelManager.Instance.AdventureAttempt);
        }

        if (SceneLoader.Instance != null)
        {
            if (isClassicRun)
            {
                SceneLoader.Instance.LoadClassicMode();
            }
            else
            {
                SceneLoader.Instance.LoadAdventureGameScene();
            }
        }
    }

    // "Ana Menü" butonuna bağlanacak
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadMainMenu();
    }

    // "Haritaya Dön" butonuna bağlanacak
    public void GoToMap()
    {
        Time.timeScale = 1f;
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadAdventureMap();
    }

    // "Sonraki Bölüm" butonuna bağlanacak
    public void NextLevel()
    {
        Time.timeScale = 1f;
        AdventureAttemptSnapshot completedAttempt = LevelManager.Instance != null ? LevelManager.Instance.AdventureAttempt : null;
        if (ProgressManager.Instance != null && completedAttempt != null)
        {
            if (ProgressManager.Instance.TryStartNextAdventureAttempt(completedAttempt))
            {
                if (SceneLoader.Instance != null) SceneLoader.Instance.LoadAdventureGameScene();
            }
            else
            {
                GoToMap();
            }
        }
    }
}
