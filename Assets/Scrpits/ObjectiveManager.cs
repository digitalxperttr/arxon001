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

    public void Initialize(AdventureLevelConfig config)
    {
        Initialize(config, null);
    }

    public void Initialize(AdventureLevelConfig config, LevelData legacyRuntimeLevel)
    {
        objectives.Clear();
        loggedCompletedObjectives.Clear();
        allObjectivesLogged = false;
        IsInitialized = false;

        if (config == null)
        {
            Debug.LogWarning("[ObjectiveManager] Initialize called with no AdventureLevelConfig.");
            return;
        }

        if (config.HasObjectiveV2())
        {
            LoadObjectiveV2(config.objectives);
        }
        else
        {
            LoadLegacyObjectives(config, legacyRuntimeLevel);
        }

        IsInitialized = true;
        Debug.Log($"[ObjectiveManager] Loaded {objectives.Count} objective(s) from {config.name}.");
        CheckCompletionLogs();
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

    private void LoadObjectiveV2(List<AdventureObjectiveDefinition> definitions)
    {
        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            AdventureObjectiveDefinition definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            objectives.Add(CreateRuntimeState(definition));
        }
    }

    private void LoadLegacyObjectives(AdventureLevelConfig config, LevelData legacyRuntimeLevel)
    {
        int targetLines = legacyRuntimeLevel != null ? legacyRuntimeLevel.targetLines : config.targetLines;
        int targetScore = legacyRuntimeLevel != null ? legacyRuntimeLevel.targetScore : config.targetScore;
        ObjectiveType objectiveType = legacyRuntimeLevel != null ? legacyRuntimeLevel.objectiveType : config.objective;

        if (objectiveType == ObjectiveType.ReachScore && targetScore > 0)
        {
            objectives.Add(CreateRuntimeState(new AdventureObjectiveDefinition
            {
                action = AdventureObjectiveAction.ReachScore,
                target = AdventureObjectiveTarget.Score,
                requiredAmount = targetScore,
                displayLabel = "Reach Score"
            }));

            return;
        }

        if (targetLines > 0)
        {
            objectives.Add(CreateRuntimeState(new AdventureObjectiveDefinition
            {
                action = AdventureObjectiveAction.ClearRows,
                target = AdventureObjectiveTarget.Rows,
                requiredAmount = targetLines,
                displayLabel = "Clear Rows"
            }));
        }
        else if (targetScore > 0)
        {
            objectives.Add(CreateRuntimeState(new AdventureObjectiveDefinition
            {
                action = AdventureObjectiveAction.ReachScore,
                target = AdventureObjectiveTarget.Score,
                requiredAmount = targetScore,
                displayLabel = "Reach Score"
            }));
        }
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
