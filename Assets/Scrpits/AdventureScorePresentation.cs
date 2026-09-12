using System.Collections.Generic;

/// <summary>
/// Keeps Adventure score feedback tied to the authored runtime objective set.
/// Score calculation itself remains shared with Classic.
/// </summary>
public static class AdventureScorePresentation
{
    public static bool ShouldShowForCurrentRun()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || grid.IsClassicRun())
        {
            return true;
        }

        ObjectiveManager objectives = ObjectiveManager.Instance;
        return objectives != null && objectives.IsActive && HasReachScoreObjective(objectives.GetObjectives());
    }

    public static bool HasReachScoreObjective(IReadOnlyList<ObjectiveRuntimeState> objectives)
    {
        if (objectives == null)
        {
            return false;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveRuntimeState state = objectives[i];
            if (state != null && state.definition != null &&
                state.definition.action == AdventureObjectiveAction.ReachScore)
            {
                return true;
            }
        }

        return false;
    }
}
