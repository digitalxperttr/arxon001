using System.Collections.Generic;
using UnityEngine;

public enum CollectibleCategory
{
    Crystal,
    Gem,
    Coin,
    Relic,
    Nature,
    Magic,
    Event
}

public enum CollectibleColor
{
    None,
    Red,
    Blue,
    Green,
    Yellow,
    Purple,
    Orange,
    Pink,
    Cyan,
    Gold,
    Turquoise,
    Lime,
    Emerald,
    Navy,
    Bronze
}

[System.Serializable]
public class CollectibleDefinition
{
    [Header("Identity")]
    [Tooltip("Stable designer-entered ID used by future Adventure objectives. Example: CR_BLUE_01")]
    public string id;

    [Tooltip("Player-facing collectible name.")]
    public string displayName;

    [Header("Grouping")]
    public CollectibleCategory category;
    public CollectibleColor color;

    [Header("Visual")]
    [Tooltip("Sliced sprite assigned manually from a collectible atlas.")]
    public Sprite icon;

    [Header("Generator")]
    [Tooltip("Future generators should only use this collectible when enabled.")]
    public bool availableInGenerator = true;
}

[CreateAssetMenu(fileName = "CollectibleDatabase", menuName = "ARXON/Adventure/Collectible Database")]
public class CollectibleDatabase : ScriptableObject
{
    [Header("Collectibles")]
    [Tooltip("Designer-authored collectible entries referenced by stable IDs.")]
    public List<CollectibleDefinition> collectibles = new List<CollectibleDefinition>();

    public CollectibleDefinition GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || collectibles == null)
        {
            return null;
        }

        for (int i = 0; i < collectibles.Count; i++)
        {
            CollectibleDefinition collectible = collectibles[i];
            if (collectible != null && collectible.id == id)
            {
                return collectible;
            }
        }

        return null;
    }

    public List<CollectibleDefinition> GetByCategory(CollectibleCategory category)
    {
        List<CollectibleDefinition> results = new List<CollectibleDefinition>();

        if (collectibles == null)
        {
            return results;
        }

        for (int i = 0; i < collectibles.Count; i++)
        {
            CollectibleDefinition collectible = collectibles[i];
            if (collectible != null && collectible.category == category)
            {
                results.Add(collectible);
            }
        }

        return results;
    }

    public List<CollectibleDefinition> GetByColor(CollectibleColor color)
    {
        List<CollectibleDefinition> results = new List<CollectibleDefinition>();

        if (collectibles == null)
        {
            return results;
        }

        for (int i = 0; i < collectibles.Count; i++)
        {
            CollectibleDefinition collectible = collectibles[i];
            if (collectible != null && collectible.color == color)
            {
                results.Add(collectible);
            }
        }

        return results;
    }

    public List<CollectibleDefinition> GetAvailableForGenerator()
    {
        List<CollectibleDefinition> results = new List<CollectibleDefinition>();

        if (collectibles == null)
        {
            return results;
        }

        for (int i = 0; i < collectibles.Count; i++)
        {
            CollectibleDefinition collectible = collectibles[i];
            if (collectible != null && collectible.availableInGenerator)
            {
                results.Add(collectible);
            }
        }

        return results;
    }

    public bool ContainsId(string id)
    {
        return GetById(id) != null;
    }

    [ContextMenu("Validate Collectible Database")]
    public void ValidateCollectibleDatabase()
    {
        LogValidationWarnings();
    }

    private void OnValidate()
    {
        LogValidationWarnings();
    }

    private void LogValidationWarnings()
    {
        if (collectibles == null)
        {
            return;
        }

        HashSet<string> seenIds = new HashSet<string>();

        for (int i = 0; i < collectibles.Count; i++)
        {
            CollectibleDefinition collectible = collectibles[i];
            if (collectible == null)
            {
                continue;
            }

            string entryLabel = string.IsNullOrWhiteSpace(collectible.displayName)
                ? "Entry " + i
                : collectible.displayName;

            if (string.IsNullOrWhiteSpace(collectible.id))
            {
                Debug.LogWarning($"{name}: Collectible '{entryLabel}' has an empty id.", this);
            }
            else if (!seenIds.Add(collectible.id))
            {
                Debug.LogWarning($"{name}: Duplicate collectible id '{collectible.id}' found.", this);
            }

            if (collectible.icon == null)
            {
                Debug.LogWarning($"{name}: Collectible '{entryLabel}' is missing an icon.", this);
            }
        }
    }
}
