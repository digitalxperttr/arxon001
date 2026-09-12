using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A small runtime-created uGUI card keeps the existing MainMenu scene/prefabs intact.
// It appears only in MainMenu and routes play through ProgressManager + SceneLoader.
public sealed class AdventureRemoteEventCard : MonoBehaviour
{
    private const float CardWidth = 390f;
    private readonly Color panelColor = new Color(0.04f, 0.08f, 0.12f, 0.88f);
    private readonly Color actionColor = new Color(0.08f, 0.62f, 0.54f, 1f);
    private AdventureRemoteEventService service;
    private AdventureEventCatalogEntry entry;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI descriptionText;
    private TextMeshProUGUI statusText;
    private Button actionButton;
    private TextMeshProUGUI actionText;

    public static void Ensure(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null || canvas.GetComponent<AdventureRemoteEventCard>() != null) return;
        AdventureRemoteEventCard card = canvas.gameObject.AddComponent<AdventureRemoteEventCard>();
        card.CreateCard(font);
        card.Refresh();
    }

    private void OnEnable()
    {
        service = AdventureRemoteEventService.Instance;
        service.Changed += Refresh;
        Refresh();
        if (service.HasCatalogUrl && service.Catalog == null) service.RefreshCatalog();
    }

    private void OnDisable()
    {
        if (service != null) service.Changed -= Refresh;
    }

    private void CreateCard(TMP_FontAsset font)
    {
        GameObject root = new GameObject("AdventureEventCard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-34f, -250f);
        rect.sizeDelta = new Vector2(CardWidth, 0f);
        root.GetComponent<Image>().color = panelColor;
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 7;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        root.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = CreateText(root.transform, "Başlıklı Etkinlik", 27, font, FontStyles.Bold);
        descriptionText = CreateText(root.transform, string.Empty, 18, font, FontStyles.Normal);
        statusText = CreateText(root.transform, string.Empty, 16, font, FontStyles.Italic);
        actionButton = CreateButton(root.transform, font);
        actionButton.onClick.AddListener(OnAction);
    }

    private TextMeshProUGUI CreateText(Transform parent, string text, int fontSize, TMP_FontAsset font, FontStyles style)
    {
        GameObject child = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        LayoutElement element = child.GetComponent<LayoutElement>();
        element.preferredHeight = fontSize >= 26 ? 40f : fontSize >= 18 ? 48f : 30f;
        TextMeshProUGUI label = child.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.color = Color.white;
        return label;
    }

    private Button CreateButton(Transform parent, TMP_FontAsset font)
    {
        GameObject buttonObject = new GameObject("Action", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 46f;
        buttonObject.GetComponent<Image>().color = actionColor;
        Button button = buttonObject.GetComponent<Button>();
        actionText = CreateText(buttonObject.transform, "Yükleniyor…", 19, font, FontStyles.Bold);
        actionText.alignment = TextAlignmentOptions.Center;
        actionText.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform textRect = actionText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private void Refresh()
    {
        if (titleText == null) return;
        service = service != null ? service : AdventureRemoteEventService.Instance;
        entry = FindPlatformEntry(service.Catalog);
        if (!service.HasCatalogUrl)
        {
            SetView("Adventure Etkinliği", "Etkinlik içeriği bu sürümde henüz yapılandırılmadı.", "Yerel Adventure akışı kullanılabilir.", "Katalog Yok", false);
            return;
        }
        if (entry == null)
        {
            SetView("Adventure Etkinliği", "Yeni etkinlikler kontrol ediliyor.", string.IsNullOrWhiteSpace(service.CatalogError) ? "Katalog bekleniyor." : service.CatalogError, "Tekrar Dene", !service.IsBusy);
            return;
        }

        AdventureRemoteEventState state = service.GetState(entry, out string detail);
        string eventName = string.IsNullOrWhiteSpace(entry.eventName) ? "Adventure Etkinliği" : entry.eventName;
        string description = string.IsNullOrWhiteSpace(entry.eventDescription) ? "Yeni Adventure bölümleri hazır." : entry.eventDescription;
        switch (state)
        {
            case AdventureRemoteEventState.Ready: SetView(eventName, description, "İndirilen içerik çevrimdışı da kullanılabilir.", "Oyna", true); break;
            case AdventureRemoteEventState.Downloadable: SetView(eventName, description, "Hazır olduğunda oynamak için indir.", "İndir", true); break;
            case AdventureRemoteEventState.Downloading: SetView(eventName, description, "İndiriliyor: " + Mathf.RoundToInt(service.DownloadProgress * 100f) + "%", "İndiriliyor", false); break;
            case AdventureRemoteEventState.Incompatible: SetView(eventName, description, detail, "Uyumsuz", false); break;
            case AdventureRemoteEventState.Expired: SetView(eventName, description, detail, "Süresi Bitti", false); break;
            default: SetView(eventName, description, detail, "Tekrar Dene", !service.IsBusy); break;
        }
    }

    private AdventureEventCatalogEntry FindPlatformEntry(AdventureEventCatalog catalog)
    {
        if (catalog == null || catalog.events == null) return null;
        string platform = AdventureEventContentContract.CurrentPlatformKey();
        for (int i = 0; i < catalog.events.Count; i++)
            if (catalog.events[i] != null && catalog.events[i].platform == platform) return catalog.events[i];
        return catalog.events.Count > 0 ? catalog.events[0] : null;
    }

    private void SetView(string title, string description, string status, string action, bool interactable)
    {
        titleText.text = title;
        descriptionText.text = description;
        statusText.text = status;
        actionText.text = action;
        actionButton.interactable = interactable;
    }

    private void OnAction()
    {
        if (entry == null) { service.RefreshCatalog(); return; }
        AdventureRemoteEventState state = service.GetState(entry, out _);
        if (state == AdventureRemoteEventState.Ready)
        {
            if (!service.TryPlay(entry, out string error)) Debug.LogWarning("[Adventure Distribution] " + error);
        }
        else if (state == AdventureRemoteEventState.Downloadable || state == AdventureRemoteEventState.Error)
        {
            service.Download(entry);
        }
        else if (state == AdventureRemoteEventState.Incompatible || state == AdventureRemoteEventState.Expired)
        {
            return;
        }
        else service.RefreshCatalog();
    }
}
