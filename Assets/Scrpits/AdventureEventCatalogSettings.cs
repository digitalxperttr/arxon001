using UnityEngine;

[CreateAssetMenu(fileName = "AdventureEventCatalogSettings", menuName = "ARXON/Adventure/Event Catalog Settings")]
public sealed class AdventureEventCatalogSettings : ScriptableObject
{
    [Tooltip("HTTPS catalog URL. The downloaded event bundle URL is resolved relative to this catalog.")]
    public string catalogUrl;
    [Tooltip("Only for Editor local-server verification. Player builds always require HTTPS.")]
    public bool allowInsecureLocalHttpInEditor;
    [Min(5)] public int requestTimeoutSeconds = 25;
}
