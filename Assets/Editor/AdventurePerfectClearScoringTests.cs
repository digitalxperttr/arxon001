using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class AdventurePerfectClearScoringTests
{
    private static void InvokeAwakeForEditMode(MonoBehaviour component)
    {
        MethodInfo method = component.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"{component.GetType().Name} must retain its singleton Awake setup.");
        method.Invoke(component, null);
    }

    private static int GetPerfectClearBonusForMode(bool isClassicRun, int classicScoreLevel)
    {
        MethodInfo method = typeof(GridManager).GetMethod(
            "GetPerfectClearBonusForMode",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "GridManager must retain the mode-aware Perfect Clear bonus selector.");
        return (int)method.Invoke(null, new object[] { isClassicRun, classicScoreLevel });
    }

    [Test]
    public void AdventurePerfectClearIsFixedAtAllClassicScoreLevels()
    {
        Assert.AreEqual(1000, GetPerfectClearBonusForMode(false, 1));
        Assert.AreEqual(1000, GetPerfectClearBonusForMode(false, 5));
        Assert.AreEqual(1000, GetPerfectClearBonusForMode(false, 13));
        Assert.AreEqual(1000, GetPerfectClearBonusForMode(false, 99));
    }

    [TestCase(1, 1000)]
    [TestCase(3, 1000)]
    [TestCase(4, 1500)]
    [TestCase(6, 1500)]
    [TestCase(7, 2500)]
    [TestCase(9, 2500)]
    [TestCase(10, 4000)]
    [TestCase(12, 4000)]
    [TestCase(13, 8000)]
    [TestCase(99, 8000)]
    public void ClassicPerfectClearTiersRemainUnchanged(int level, int expectedBonus)
    {
        Assert.AreEqual(expectedBonus, GetPerfectClearBonusForMode(true, level));
    }

    [Test]
    public void AdventureReachScoreReceivesTheFixedPerfectClearBonusThroughAddScore()
    {
        GameObject scoreObject = null;
        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
        {
            scoreObject = new GameObject("ScoreManagerTest");
            scoreManager = scoreObject.AddComponent<ScoreManager>();
        }

        scoreManager.ResetScoreAndLevel();
        GameObject objectiveObject = null;
        ObjectiveManager objectiveManager = ObjectiveManager.Instance;
        if (objectiveManager == null)
        {
            objectiveManager = ObjectiveManager.EnsureInstance();
            InvokeAwakeForEditMode(objectiveManager);
            objectiveObject = objectiveManager.gameObject;
        }

        AdventureLevelConfig config = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        config.objectives.Add(new AdventureObjectiveDefinition
        {
            action = AdventureObjectiveAction.ReachScore,
            target = AdventureObjectiveTarget.Score,
            requiredAmount = 1000
        });

        LevelData runtimeLevel = AdventureLevelGenerator.GenerateRuntimeLevel(config);
        objectiveManager.Initialize(runtimeLevel);

        Assert.AreSame(objectiveManager, ObjectiveManager.Instance);
        Assert.IsTrue(objectiveManager.IsActive);
        Assert.AreEqual(AdventureObjectiveAction.ReachScore, objectiveManager.GetObjectives()[0].definition.action);

        scoreManager.AddScore(GetPerfectClearBonusForMode(false, 13));

        Assert.AreEqual(1000, scoreManager.CurrentScore);
        Assert.AreEqual(1000, objectiveManager.GetObjectives()[0].currentAmount);
        Assert.IsTrue(objectiveManager.AreAllObjectivesComplete());

        Object.DestroyImmediate(runtimeLevel);
        Object.DestroyImmediate(config);
        scoreManager.ResetScoreAndLevel();
        if (scoreObject != null)
        {
            Object.DestroyImmediate(scoreObject);
        }
        if (objectiveObject != null)
        {
            Object.DestroyImmediate(objectiveObject);
        }
    }
}
