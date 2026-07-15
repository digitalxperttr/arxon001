using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class AdventureObjectiveResultRow
{
    [SerializeField] private GameObject rowRoot;
    [SerializeField] private Image statusImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text progressText;

    public GameObject RowRoot => rowRoot;

    public bool HasAnyReference =>
        rowRoot != null || statusImage != null || iconImage != null || nameText != null || progressText != null;

    public void Hide()
    {
        SetActive(false);
    }

    public void SetData(
        ObjectiveRuntimeState state,
        CollectibleDatabase collectibleDatabase,
        Sprite genericRowIcon,
        Sprite genericScoreIcon,
        Sprite completedStatusSprite,
        Sprite incompleteStatusSprite,
        bool isCompleted)
    {
        if (state == null || state.definition == null)
        {
            Hide();
            return;
        }

        SetActive(true);

        if (statusImage != null)
        {
            Sprite statusSprite = isCompleted ? completedStatusSprite : incompleteStatusSprite;
            statusImage.sprite = statusSprite;
            statusImage.enabled = statusSprite != null;

            if (statusSprite == null)
            {
                Debug.LogWarning($"[AdventureObjectiveResultList] Missing {(isCompleted ? "completed" : "incomplete")} status sprite.");
            }
        }

        if (iconImage != null)
        {
            Sprite icon = AdventureObjectiveResultList.GetIcon(state, collectibleDatabase, genericRowIcon, genericScoreIcon);
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
        {
            nameText.text = AdventureObjectiveResultList.GetLabel(state, collectibleDatabase);
            nameText.color = AdventureObjectiveResultList.TextColor;
        }

        if (progressText != null)
        {
            progressText.text = $"{state.currentAmount} / {state.requiredAmount}";
            progressText.color = AdventureObjectiveResultList.TextColor;
        }
    }

    private void SetActive(bool active)
    {
        if (rowRoot != null)
        {
            rowRoot.SetActive(active);
            return;
        }

        if (statusImage != null)
            statusImage.gameObject.SetActive(active);

        if (iconImage != null)
            iconImage.gameObject.SetActive(active);

        if (nameText != null)
            nameText.gameObject.SetActive(active);

        if (progressText != null)
            progressText.gameObject.SetActive(active);
    }
}

public static class AdventureObjectiveResultList
{
    public static readonly Color TextColor = new Color(0.18f, 0.13f, 0.08f, 1f);
    public static readonly Color CompleteColor = new Color(0.05f, 0.72f, 0.58f, 1f);
    public static readonly Color FailedColor = new Color(0.8f, 0.16f, 0.08f, 1f);

    public static void ApplyPanelSprite(Image panelBackground, Sprite panelSprite)
    {
        if (panelBackground == null || panelSprite == null)
        {
            return;
        }

        panelBackground.sprite = panelSprite;
        panelBackground.preserveAspect = true;
    }

    public static void Build(
        AdventureObjectiveResultRow[] rows,
        CollectibleDatabase collectibleDatabase,
        Sprite genericRowIcon,
        Sprite genericScoreIcon,
        Sprite completedStatusSprite,
        Sprite incompleteStatusSprite,
        bool forceCompletedStatus)
    {
        HideRows(rows);

        if (rows == null || rows.Length == 0)
        {
            Debug.LogWarning("[AdventureObjectiveResultList] Objective rows are not assigned. Add ObjectiveRow1/2/3 in the panel and bind them in the Inspector.");
            return;
        }

        if (ObjectiveManager.Instance == null || !ObjectiveManager.Instance.IsActive)
        {
            return;
        }

        IReadOnlyList<ObjectiveRuntimeState> objectives = ObjectiveManager.Instance.GetObjectives();
        int rowIndex = 0;

        for (int i = 0; i < objectives.Count && rowIndex < rows.Length; i++)
        {
            ObjectiveRuntimeState state = objectives[i];
            if (state == null || state.definition == null)
            {
                continue;
            }

            if (rows[rowIndex] == null || !rows[rowIndex].HasAnyReference)
            {
                Debug.LogWarning($"[AdventureObjectiveResultList] Objective row {rowIndex + 1} is not assigned.");
                rowIndex++;
                continue;
            }

            bool rowCompleted = forceCompletedStatus || state.IsComplete;

            rows[rowIndex].SetData(
                state,
                collectibleDatabase,
                genericRowIcon,
                genericScoreIcon,
                completedStatusSprite,
                incompleteStatusSprite,
                rowCompleted);
            rowIndex++;
        }
    }

    public static Sprite GetIcon(
        ObjectiveRuntimeState state,
        CollectibleDatabase collectibleDatabase,
        Sprite genericRowIcon,
        Sprite genericScoreIcon)
    {
        AdventureObjectiveDefinition definition = state.definition;
        if (definition.displayIcon != null)
        {
            return definition.displayIcon;
        }

        if (definition.action == AdventureObjectiveAction.CollectItem)
        {
            CollectibleDefinition collectible = GetCollectible(collectibleDatabase, definition.collectibleId);
            return collectible != null ? collectible.icon : null;
        }

        if (definition.action == AdventureObjectiveAction.ReachScore)
        {
            return genericScoreIcon;
        }

        return genericRowIcon;
    }

    public static string GetLabel(ObjectiveRuntimeState state, CollectibleDatabase collectibleDatabase)
    {
        AdventureObjectiveDefinition definition = state.definition;
        if (!string.IsNullOrWhiteSpace(definition.displayLabel))
        {
            return definition.displayLabel;
        }

        if (definition.action == AdventureObjectiveAction.CollectItem)
        {
            CollectibleDefinition collectible = GetCollectible(collectibleDatabase, definition.collectibleId);
            if (collectible != null && !string.IsNullOrWhiteSpace(collectible.displayName))
            {
                return collectible.displayName;
            }

            return "Kristal Topla";
        }

        switch (definition.action)
        {
            case AdventureObjectiveAction.ReachScore:
                return "Puan Topla";
            case AdventureObjectiveAction.ClearRows:
                return "Satır Temizle";
            case AdventureObjectiveAction.DestroyObstacle:
                return GetObstacleLabel(definition.target);
            case AdventureObjectiveAction.BreakChain:
                return "Zincir Kır";
            case AdventureObjectiveAction.ComboTarget:
                return "Kombo Yap";
            default:
                return "Hedef";
        }
    }

    private static void HideRows(AdventureObjectiveResultRow[] rows)
    {
        if (rows == null)
        {
            return;
        }

        for (int i = 0; i < rows.Length; i++)
        {
            rows[i]?.Hide();
        }
    }

    private static string GetObstacleLabel(AdventureObjectiveTarget target)
    {
        switch (target)
        {
            case AdventureObjectiveTarget.Rock:
                return "Kaya Kır";
            case AdventureObjectiveTarget.Ice:
                return "Buz Kır";
            case AdventureObjectiveTarget.Chain:
                return "Zincir Kır";
            default:
                return "Engel Kır";
        }
    }

    private static CollectibleDefinition GetCollectible(CollectibleDatabase collectibleDatabase, string collectibleId)
    {
        if (collectibleDatabase == null && GridManager.Instance != null)
        {
            collectibleDatabase = GridManager.Instance.CollectibleDatabase;
        }

        if (collectibleDatabase == null || string.IsNullOrWhiteSpace(collectibleId))
        {
            return null;
        }

        return collectibleDatabase.GetById(collectibleId);
    }
}
