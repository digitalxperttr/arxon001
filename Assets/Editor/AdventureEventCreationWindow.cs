using System;
using UnityEditor;
using UnityEngine;

public sealed class AdventureEventCreationRequest
{
    public string eventName;
    public string parentFolder;
    public string packageFolderName;
    public Sprite mapBackgroundVisual;
    public Sprite gameplayBackgroundVisual;
    public CollectibleDatabase collectibleDatabase;
    public int seed;
    public int targetObstaclePerRowLimit = 2;
    public int targetObstacleActiveBoardLimit = 2;
}

public class AdventureEventCreationWindow : EditorWindow
{
    private string eventName = "Yeni Yerel Event";
    private string parentFolder = "Assets/AdventureEvents";
    private string packageFolderName = "Yeni-Yerel-Event";
    private Sprite mapBackgroundVisual;
    private Sprite gameplayBackgroundVisual;
    private CollectibleDatabase collectibleDatabase;
    private int seed;
    private int targetObstaclePerRowLimit = 2;
    private int targetObstacleActiveBoardLimit = 2;
    private bool showAdvanced;
    private bool isCreating;
    private string status;
    private MessageType statusType = MessageType.Info;

    [MenuItem("ARXON/Adventure/Yeni Event Oluştur")]
    public static void ShowWindow()
    {
        AdventureEventCreationWindow window = GetWindow<AdventureEventCreationWindow>("Yeni Event Oluştur");
        window.minSize = new Vector2(440f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        if (seed == 0) seed = CreateSeed();
        if (string.IsNullOrWhiteSpace(packageFolderName)) packageFolderName = AdventureEventPackageEditorUtility.SanitizePathPart(eventName);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Yerel Adventure Event", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bu araç yalnızca yeni bir yerel paket üretir; aktif event'i veya oyuncu ilerlemesini değiştirmez. " +
            "Görseller boş bırakılabilir; sahnenin mevcut fallback görseli kullanılır.", MessageType.Info);

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode veya açık bir Adventure attempt'i sırasında yeni paket oluşturulamaz. Play Mode'dan çıkıp yeniden deneyin.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        eventName = EditorGUILayout.TextField("Event Adı", eventName);
        if (EditorGUI.EndChangeCheck()) packageFolderName = AdventureEventPackageEditorUtility.SanitizePathPart(eventName);

        mapBackgroundVisual = (Sprite)EditorGUILayout.ObjectField("Map Background Visual", mapBackgroundVisual, typeof(Sprite), false);
        gameplayBackgroundVisual = (Sprite)EditorGUILayout.ObjectField("Gameplay Background Visual", gameplayBackgroundVisual, typeof(Sprite), false);
        collectibleDatabase = (CollectibleDatabase)EditorGUILayout.ObjectField("Collectible Database", collectibleDatabase, typeof(CollectibleDatabase), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Yeni Seed", GUILayout.Width(90f))) seed = CreateSeed();
        }

        EditorGUILayout.LabelField("Bölüm Sayısı", "50 (mevcut generator sözleşmesi)");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Oluşturulacak Klasör", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            parentFolder = EditorGUILayout.TextField("Ana Klasör", parentFolder);
            if (GUILayout.Button("Seç...", GUILayout.Width(62f))) SelectParentFolder();
        }
        packageFolderName = EditorGUILayout.TextField("Paket Klasör Adı", packageFolderName);
        EditorGUILayout.LabelField("Hedef", GetTargetFolder(), EditorStyles.wordWrappedMiniLabel);

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Gelişmiş Generator Ayarları", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            targetObstaclePerRowLimit = Mathf.Max(0, EditorGUILayout.IntField("New Target Obstacles Per Row", targetObstaclePerRowLimit));
            targetObstacleActiveBoardLimit = Mathf.Max(0, EditorGUILayout.IntField("Target Obstacles Allowed On Board", targetObstacleActiveBoardLimit));
            EditorGUILayout.HelpBox("Varsayılan 2/2 yeni paketlere uygulanır. Bu değerler yalnızca bu yeni paketin Direct tariflerine yazılır.", MessageType.None);
            EditorGUI.indentLevel--;
        }

        AdventureEventCreationRequest request = BuildRequest();
        bool valid = AdventureEventPackageEditorUtility.TryValidateNewEventRequest(request, out _, out string eventId, out string validationMessage);
        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(valid
                ? "Oluşturulacak Event ID: " + eventId + ". Başarıdan sonra asset seçilir; Inspector'daki 'Use This Local Event' ve 'Play Local Event' düğmelerini kullanın."
                : validationMessage,
            valid ? MessageType.Info : MessageType.Warning);

        using (new EditorGUI.DisabledScope(!valid || isCreating))
        {
            if (GUILayout.Button("Oluştur ve 50 Bölüm Üret", GUILayout.Height(30f))) CreatePackage(request);
        }

        if (!string.IsNullOrWhiteSpace(status)) EditorGUILayout.HelpBox(status, statusType);
    }

    private AdventureEventCreationRequest BuildRequest()
    {
        return new AdventureEventCreationRequest
        {
            eventName = eventName,
            parentFolder = parentFolder,
            packageFolderName = packageFolderName,
            mapBackgroundVisual = mapBackgroundVisual,
            gameplayBackgroundVisual = gameplayBackgroundVisual,
            collectibleDatabase = collectibleDatabase,
            seed = seed,
            targetObstaclePerRowLimit = targetObstaclePerRowLimit,
            targetObstacleActiveBoardLimit = targetObstacleActiveBoardLimit
        };
    }

    private void CreatePackage(AdventureEventCreationRequest request)
    {
        isCreating = true;
        try
        {
            if (AdventureEventPackageEditorUtility.CreateNewEventPackage(request, out AdventureEventConfig created, out string message))
            {
                status = message;
                statusType = MessageType.Info;
                Selection.activeObject = created;
            }
            else
            {
                status = message;
                statusType = MessageType.Error;
            }
        }
        finally
        {
            isCreating = false;
        }
    }

    private string GetTargetFolder()
    {
        string name = AdventureEventPackageEditorUtility.SanitizePathPart(packageFolderName);
        return string.IsNullOrWhiteSpace(parentFolder) || string.IsNullOrWhiteSpace(name) ? "—" : parentFolder.TrimEnd('/') + "/" + name;
    }

    private void SelectParentFolder()
    {
        string absolute = EditorUtility.OpenFolderPanel("Event ana klasörünü seç", Application.dataPath, string.Empty);
        if (string.IsNullOrWhiteSpace(absolute)) return;
        string projectRelative = FileUtil.GetProjectRelativePath(absolute);
        if (string.IsNullOrWhiteSpace(projectRelative) || !projectRelative.StartsWith("Assets", StringComparison.Ordinal))
        {
            status = "Ana klasör proje içindeki Assets klasörü veya onun bir alt klasörü olmalıdır.";
            statusType = MessageType.Error;
            return;
        }
        parentFolder = projectRelative.TrimEnd('/');
    }

    private static int CreateSeed()
    {
        unchecked { return (int)(DateTime.UtcNow.Ticks ^ Environment.TickCount); }
    }
}
