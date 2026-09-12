using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ObjectiveRuntimeState
{
    public AdventureObjectiveDefinition definition;
    public int currentAmount;
    public int requiredAmount;

    public bool IsComplete => currentAmount >= requiredAmount;
}

public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    private readonly List<ObjectiveRuntimeState> objectives = new List<ObjectiveRuntimeState>();
    private readonly HashSet<ObjectiveRuntimeState> loggedCompletedObjectives = new HashSet<ObjectiveRuntimeState>();
    private bool allObjectivesLogged;

    public bool IsInitialized { get; private set; }
    public bool HasObjectives => objectives.Count > 0;
    public bool IsActive => IsInitialized && HasObjectives;

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

    public static ObjectiveManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject("ObjectiveManager");
        return managerObject.AddComponent<ObjectiveManager>();
    }

    public LevelData RuntimeLevel { get; private set; }

    public void Initialize(LevelData level)
    {
        objectives.Clear();
        loggedCompletedObjectives.Clear();
        allObjectivesLogged = false;
        IsInitialized = false;
        RuntimeLevel = null;
        if (level == null || !level.IsRuntimeLevel)
        {
            Debug.LogError("[ObjectiveManager] Resolved Adventure runtime data required.");
            return;
        }
        if (!LevelData.ValidateObjectives(level.Objectives, out string error))
        {
            Debug.LogError(error);
            return;
        }
        RuntimeLevel = level;
        for (int i = 0; i < level.Objectives.Count; i++)
            objectives.Add(CreateRuntimeState(level.Objectives[i]));
        IsInitialized = true;
        Debug.Log($"[ObjectiveManager] Loaded {objectives.Count} objective(s) from runtime level {level.levelNumber}.");
    }

    public void ReportRowsCleared(int amount)
    {
        if (!IsActive || amount <= 0)
        {
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveRuntimeState state = objectives[i];
            if (state.definition != null && state.definition.action == AdventureObjectiveAction.ClearRows)
            {
                AddProgress(state, amount);
            }
        }

        CheckCompletionLogs();
    }

    public void ReportScoreChanged(int currentScore)
    {
        if (!IsActive)
        {
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveRuntimeState state = objectives[i];
            if (state.definition != null && state.definition.action == AdventureObjectiveAction.ReachScore)
            {
                state.currentAmount = Mathf.Clamp(currentScore, 0, state.requiredAmount);
            }
        }

        CheckCompletionLogs();
    }

    public void ReportCollectibleCollected(string collectibleId, int amount = 1)
    {
        if (!IsActive || string.IsNullOrWhiteSpace(collectibleId) || amount <= 0)
        {
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveRuntimeState state = objectives[i];
            AdventureObjectiveDefinition definition = state.definition;
            if (definition == null ||
                definition.action != AdventureObjectiveAction.CollectItem ||
                definition.collectibleId != collectibleId)
            {
                continue;
            }

            AddProgress(state, amount);
        }

        CheckCompletionLogs();
    }

    public void ReportObstacleDestroyed(AdventureObjectiveTarget obstacleTarget)
    {
        if (!IsActive || obstacleTarget == AdventureObjectiveTarget.None)
        {
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveRuntimeState state = objectives[i];
            AdventureObjectiveDefinition definition = state.definition;
            if (definition == null)
            {
                continue;
            }

            bool matches = definition.action == AdventureObjectiveAction.DestroyObstacle &&
                (definition.target == obstacleTarget || definition.target == AdventureObjectiveTarget.AnyObstacle);
            bool matchesChainObjective = definition.action == AdventureObjectiveAction.BreakChain &&
                obstacleTarget == AdventureObjectiveTarget.Chain;

            if (matches || matchesChainObjective)
            {
                AddProgress(state, 1);
            }
        }

        CheckCompletionLogs();
        if (LevelManager.Instance != null && LevelManager.Instance.enabled)
        {
            LevelManager.Instance.EvaluateObjectiveCompletion();
        }
    }

    public bool AreAllObjectivesComplete()
    {
        if (!IsActive)
        {
            return false;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            if (!objectives[i].IsComplete)
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<ObjectiveRuntimeState> GetObjectives()
    {
        return objectives;
    }

    private ObjectiveRuntimeState CreateRuntimeState(AdventureObjectiveDefinition definition)
    {
        return new ObjectiveRuntimeState
        {
            definition = definition,
            currentAmount = 0,
            requiredAmount = Mathf.Max(1, definition.requiredAmount)
        };
    }

    private void AddProgress(ObjectiveRuntimeState state, int amount)
    {
        if (state == null || state.IsComplete)
        {
            return;
        }

        state.currentAmount = Mathf.Clamp(state.currentAmount + amount, 0, state.requiredAmount);
    }

    private void CheckCompletionLogs()
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveRuntimeState state = objectives[i];
            if (state.IsComplete && loggedCompletedObjectives.Add(state))
            {
                Debug.Log($"[ObjectiveManager] Objective completed: {GetObjectiveLabel(state)}.");
            }
        }

        if (!allObjectivesLogged && AreAllObjectivesComplete())
        {
            allObjectivesLogged = true;
            Debug.Log("[ObjectiveManager] All objectives completed.");
        }
    }

    private string GetObjectiveLabel(ObjectiveRuntimeState state)
    {
        if (state.definition == null)
        {
            return "Unknown";
        }

        if (!string.IsNullOrWhiteSpace(state.definition.displayLabel))
        {
            return state.definition.displayLabel;
        }

        if (state.definition.action == AdventureObjectiveAction.CollectItem)
        {
            return $"{state.definition.action} {state.definition.collectibleId}";
        }

        return state.definition.action.ToString();
    }
}
