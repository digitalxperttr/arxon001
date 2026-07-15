using System.Collections.Generic;
using UnityEngine;

public class ModeBasedHudVisibility : MonoBehaviour
{
    [SerializeField] private GameObject[] classicOnlyObjects;
    [SerializeField] private GameObject[] adventureOnlyObjects;

    private readonly List<GameObject> runtimeClassicOnlyObjects = new List<GameObject>();
    private readonly List<GameObject> runtimeAdventureOnlyObjects = new List<GameObject>();

    private void Awake()
    {
        ApplyVisibilityForCurrentMode();
    }

    private void OnEnable()
    {
        ApplyVisibilityForCurrentMode();
    }

    private void Start()
    {
        ApplyVisibilityForCurrentMode();
    }

    public void RegisterClassicOnlyObject(GameObject target)
    {
        RegisterRuntimeObject(runtimeClassicOnlyObjects, target);
        ApplyVisibilityForCurrentMode();
    }

    public void RegisterAdventureOnlyObject(GameObject target)
    {
        RegisterRuntimeObject(runtimeAdventureOnlyObjects, target);
        ApplyVisibilityForCurrentMode();
    }

    public static void RegisterAdventureOnlyObjectInScene(GameObject target)
    {
        ModeBasedHudVisibility[] visibilityComponents = FindObjectsByType<ModeBasedHudVisibility>(FindObjectsInactive.Include);

        for (int i = 0; i < visibilityComponents.Length; i++)
        {
            if (visibilityComponents[i] != null)
            {
                visibilityComponents[i].RegisterAdventureOnlyObject(target);
            }
        }
    }

    public void ApplyVisibilityForCurrentMode()
    {
        bool isAdventureMode =
            ProgressManager.Instance != null &&
            ProgressManager.Instance.currentSelectedLevel != null;

        SetObjectsActive(classicOnlyObjects, !isAdventureMode);
        SetObjectsActive(adventureOnlyObjects, isAdventureMode);
        SetObjectsActive(runtimeClassicOnlyObjects, !isAdventureMode);
        SetObjectsActive(runtimeAdventureOnlyObjects, isAdventureMode);
    }

    private static void RegisterRuntimeObject(List<GameObject> objects, GameObject target)
    {
        if (target == null || objects.Contains(target))
        {
            return;
        }

        objects.Add(target);
    }

    private static void SetObjectsActive(GameObject[] objects, bool isActive)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            SetObjectActive(objects[i], isActive);
        }
    }

    private static void SetObjectsActive(List<GameObject> objects, bool isActive)
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            GameObject target = objects[i];
            if (target == null)
            {
                objects.RemoveAt(i);
                continue;
            }

            SetObjectActive(target, isActive);
        }
    }

    private static void SetObjectActive(GameObject target, bool isActive)
    {
        if (target != null && target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
