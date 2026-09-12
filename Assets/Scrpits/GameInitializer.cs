using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameInitializer : MonoBehaviour
{
    // Unity'den sürükleyip bırakacağımız Managers Prefab'i
    public GameObject managersPrefab;
    [SerializeField] private AdventureEventConfig defaultLocalEvent;
    public AdventureEventConfig DefaultLocalEvent => defaultLocalEvent;

    public void SetDefaultLocalEvent(AdventureEventConfig eventConfig) => defaultLocalEvent = eventConfig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureFrameRate()
    {
        // Apply the frame budget before any scene, including menu/map and direct Play starts.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    void Awake()
    {
        // Managers yalnızca ilk başlangıçta kurulur; yerel paket seçimi her MainMenu başlangıcında uygulanır.
        if (SceneLoader.Instance == null && managersPrefab != null)
        {
            Instantiate(managersPrefab);
        }

        ApplyDefaultLocalEvent();
#if UNITY_EDITOR
        if (SessionState.GetBool("ARXON.PlayLocalEvent", false))
        {
            SessionState.EraseBool("ARXON.PlayLocalEvent");
            StartCoroutine(OpenSelectedLocalEventMapNextFrame());
        }
#endif
    }

    private void ApplyDefaultLocalEvent()
    {
        if (defaultLocalEvent == null) return;
        ProgressManager manager = ProgressManager.Instance != null ? ProgressManager.Instance : FindFirstObjectByType<ProgressManager>();
        if (manager == null) { Debug.LogError("[Adventure] Local event could not be selected because ProgressManager was not created.", this); return; }
        if (!manager.TrySelectLocalEvent(defaultLocalEvent)) Debug.LogError($"[Adventure] Local event '{defaultLocalEvent.eventId}' was not selected.", defaultLocalEvent);
    }

    private IEnumerator OpenSelectedLocalEventMapNextFrame()
    {
        yield return null;
        if (ProgressManager.Instance == null || ProgressManager.Instance.SelectedLocalEvent == null)
        {
            Debug.LogError("[Adventure] Play Local Event stopped because the selected local event was not applied.", this);
            yield break;
        }
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadAdventureMap();
    }
}
