using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

// Deliberately small, data-only distribution contract. An event bundle contains
// authored ScriptableObjects and their visual dependencies; it never contains code.
public static class AdventureEventContentContract
{
    public const int CatalogSchemaVersion = 1;
    public const int EventDataSchemaVersion = 1;
    public const string GameCompatibilityVersion = "1";

    public static bool ValidateCatalogEntry(AdventureEventCatalogEntry entry, out string error)
    {
        error = null;
        if (entry == null) error = "Etkinlik kaydı eksik.";
        else if (entry.dataSchemaVersion != EventDataSchemaVersion) error = "Bu etkinlik verisi bu oyun sürümüyle uyumlu değil.";
        else if (!string.Equals(entry.gameCompatibilityVersion, GameCompatibilityVersion, StringComparison.Ordinal)) error = "Bu etkinlik bu oyun sürümü için hazırlanmadı.";
        else if (string.IsNullOrWhiteSpace(entry.eventId) || string.IsNullOrWhiteSpace(entry.contentVersion)) error = "Etkinlik kimliği veya içerik sürümü eksik.";
        else if (string.IsNullOrWhiteSpace(entry.bundleRelativePath) || string.IsNullOrWhiteSpace(entry.eventAssetName)) error = "Etkinlik paketi tanımı eksik.";
        else if (entry.fileSizeBytes <= 0 || !AdventureEventIntegrity.IsSha256(entry.sha256)) error = "Etkinlik paketi bütünlük bilgisi geçersiz.";
        return error == null;
    }

    public static string CurrentPlatformKey()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer: return "iOS";
            case RuntimePlatform.Android: return "Android";
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor: return "StandaloneOSX";
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor: return "StandaloneWindows64";
            case RuntimePlatform.LinuxPlayer: return "StandaloneLinux64";
            case RuntimePlatform.WebGLPlayer: return "WebGL";
            default: return Application.platform.ToString();
        }
    }
}

[Serializable]
public sealed class AdventureEventCatalog
{
    public int catalogSchemaVersion = AdventureEventContentContract.CatalogSchemaVersion;
    public List<AdventureEventCatalogEntry> events = new List<AdventureEventCatalogEntry>();
}

[Serializable]
public sealed class AdventureEventCatalogEntry
{
    public string eventId;
    public string contentVersion;
    public int dataSchemaVersion = AdventureEventContentContract.EventDataSchemaVersion;
    public string gameCompatibilityVersion = AdventureEventContentContract.GameCompatibilityVersion;
    public string eventName;
    public string eventDescription;
    public string generatorVersion;
    public string platform;
    public string bundleRelativePath;
    public string eventAssetName;
    public long fileSizeBytes;
    public string sha256;
    public string startsAtUtc;
    public string endsAtUtc;

    public bool IsAvailableNow(out string reason)
    {
        reason = null;
        if (TryParseUtc(startsAtUtc, out DateTime startsAt) && DateTime.UtcNow < startsAt)
        {
            reason = "Bu etkinlik henüz başlamadı.";
            return false;
        }
        if (TryParseUtc(endsAtUtc, out DateTime endsAt) && DateTime.UtcNow >= endsAt)
        {
            reason = "Bu etkinliğin süresi sona erdi.";
            return false;
        }
        return true;
    }

    private static bool TryParseUtc(string value, out DateTime parsed) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed);
}

public static class AdventureEventIntegrity
{
    public static bool IsSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
        for (int i = 0; i < value.Length; i++)
            if (!Uri.IsHexDigit(value[i])) return false;
        return true;
    }

    public static string CalculateSha256(string filePath)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(filePath))
        {
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    public static bool VerifyFile(string filePath, long expectedSize, string expectedSha256, out string error)
    {
        error = null;
        if (!File.Exists(filePath)) error = "İndirilen etkinlik paketi bulunamadı.";
        else if (expectedSize > 0 && new FileInfo(filePath).Length != expectedSize) error = "İndirilen etkinlik paketinin boyutu doğrulanamadı.";
        else if (!string.Equals(CalculateSha256(filePath), expectedSha256, StringComparison.OrdinalIgnoreCase)) error = "İndirilen etkinlik paketinin bütünlüğü doğrulanamadı.";
        return error == null;
    }
}

public static class AdventureRemoteEventValidator
{
    public static bool ValidateLoadedEvent(AdventureEventConfig eventConfig, AdventureEventCatalogEntry entry, out string error)
    {
        error = null;
        if (eventConfig == null) error = "Etkinlik paketi ana verisi okunamadı.";
        else if (!string.Equals(eventConfig.eventId, entry.eventId, StringComparison.Ordinal) ||
                 !string.Equals(eventConfig.contentVersion, entry.contentVersion, StringComparison.Ordinal)) error = "İndirilen etkinlik paketi katalog kimliğiyle eşleşmiyor.";
        else if (eventConfig.collectibleDatabase == null) error = "Etkinlik paketi collectible veritabanını içermiyor.";
        else if (eventConfig.levelConfigs == null || eventConfig.levelConfigs.Length == 0) error = "Etkinlik paketi bölüm listesini içermiyor.";
        if (error != null) return false;

        HashSet<string> ids = new HashSet<string>();
        for (int i = 0; i < eventConfig.levelConfigs.Length; i++)
        {
            AdventureLevelConfig config = eventConfig.levelConfigs[i];
            string prefix = "Bölüm " + (i + 1) + ": ";
            if (config == null) { error = prefix + "veri eksik."; return false; }
            if (string.IsNullOrWhiteSpace(config.levelId) || !ids.Add(config.levelId)) { error = prefix + "sabit bölüm kimliği geçersiz."; return false; }
            if (config.eventId != eventConfig.eventId || config.contentVersion != eventConfig.contentVersion) { error = prefix + "etkinlik kimliği eşleşmiyor."; return false; }
            if (config.GetStageNumber() != i + 1) { error = prefix + "sıralı bölüm numarası eşleşmiyor."; return false; }
            if (!AdventureLevelGenerator.ValidateConfig(config, out string configError)) { error = prefix + configError; return false; }

            LevelData runtime = AdventureLevelGenerator.GenerateRuntimeLevel(config);
            if (runtime == null) { error = prefix + "runtime verisi üretilemedi."; return false; }
            AdventureContentValidationResult validation = AdventureContentValidator.ValidateLevel(
                runtime, eventConfig.collectibleDatabase, new AdventureRowGenerationContext(8, runtime, new[] { Color.red, Color.blue }));
            DestroyRuntimeValidationCopy(runtime);
            if (validation.HasErrors)
            {
                error = prefix + string.Join(" ", validation.Errors);
                return false;
            }
        }
        return true;
    }

    private static void DestroyRuntimeValidationCopy(LevelData runtime)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(runtime);
        else UnityEngine.Object.Destroy(runtime);
#else
        UnityEngine.Object.Destroy(runtime);
#endif
    }
}

[Serializable]
public sealed class AdventureInstalledEventRecord
{
    public string eventId;
    public string contentVersion;
    public string platform;
    public string sha256;
    public string bundlePath;
    public string eventAssetName;
    public long fileSizeBytes;
}

[Serializable]
internal sealed class AdventureInstalledEventIndex
{
    public List<AdventureInstalledEventRecord> records = new List<AdventureInstalledEventRecord>();
}

public enum AdventureRemoteEventState
{
    Unavailable,
    Downloadable,
    Downloading,
    Ready,
    Incompatible,
    Expired,
    Error
}

// Keeps the selected event bundle alive while its event/stage/sprite objects can be used by the map or attempt.
public sealed class AdventureRemoteEventService : MonoBehaviour
{
    private const string SettingsResourcePath = "AdventureEventCatalogSettings";
    private const string CachedCatalogFileName = "adventure-event-catalog.json";
    private const string InstalledIndexFileName = "adventure-event-index.json";
    private static AdventureRemoteEventService instance;

    private readonly Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();
    private readonly Dictionary<string, AdventureEventConfig> loadedEvents = new Dictionary<string, AdventureEventConfig>();
    private AdventureInstalledEventIndex installedIndex = new AdventureInstalledEventIndex();
    private AdventureEventCatalog catalog;
    private string catalogError;
    private bool isBusy;
    private float downloadProgress;

    public static AdventureRemoteEventService Instance
    {
        get
        {
            if (instance != null) return instance;
            GameObject root = new GameObject("AdventureRemoteEventService");
            instance = root.AddComponent<AdventureRemoteEventService>();
            return instance;
        }
    }

    public event Action Changed;
    public AdventureEventCatalog Catalog => catalog;
    public string CatalogError => catalogError;
    public bool IsBusy => isBusy;
    public float DownloadProgress => downloadProgress;
    public AdventureEventCatalogSettings Settings => Resources.Load<AdventureEventCatalogSettings>(SettingsResourcePath);
    public bool HasCatalogUrl => Settings != null && !string.IsNullOrWhiteSpace(Settings.catalogUrl);

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadInstalledIndex();
        LoadCachedCatalog();
    }

    public void RefreshCatalog()
    {
        if (isBusy) return;
        if (!TryGetCatalogUri(out Uri uri, out string error)) { catalogError = error; NotifyChanged(); return; }
        StartCoroutine(RefreshCatalogRoutine(uri));
    }

    public void Download(AdventureEventCatalogEntry entry)
    {
        if (isBusy || entry == null) return;
        if (!AdventureEventContentContract.ValidateCatalogEntry(entry, out string error)) { catalogError = error; NotifyChanged(); return; }
        if (!string.Equals(entry.platform, AdventureEventContentContract.CurrentPlatformKey(), StringComparison.Ordinal))
        {
            catalogError = "Bu etkinlik bu cihaz platformu için hazır değil.";
            NotifyChanged();
            return;
        }
        if (!entry.IsAvailableNow(out error)) { catalogError = error; NotifyChanged(); return; }
        if (IsReady(entry)) { NotifyChanged(); return; }
        if (!TryGetCatalogUri(out Uri catalogUri, out error)) { catalogError = error; NotifyChanged(); return; }
        StartCoroutine(DownloadRoutine(entry, new Uri(catalogUri, entry.bundleRelativePath)));
    }

    public AdventureRemoteEventState GetState(AdventureEventCatalogEntry entry, out string detail)
    {
        detail = null;
        if (entry == null) return AdventureRemoteEventState.Unavailable;
        if (!AdventureEventContentContract.ValidateCatalogEntry(entry, out detail)) return AdventureRemoteEventState.Incompatible;
        if (!string.Equals(entry.platform, AdventureEventContentContract.CurrentPlatformKey(), StringComparison.Ordinal)) { detail = "Bu cihaz için içerik paketi yok."; return AdventureRemoteEventState.Incompatible; }
        if (!entry.IsAvailableNow(out detail)) return AdventureRemoteEventState.Expired;
        if (isBusy) return AdventureRemoteEventState.Downloading;
        if (IsReady(entry)) return AdventureRemoteEventState.Ready;
        if (!string.IsNullOrWhiteSpace(catalogError)) { detail = catalogError; return AdventureRemoteEventState.Error; }
        return AdventureRemoteEventState.Downloadable;
    }

    public bool TryPlay(AdventureEventCatalogEntry entry, out string error)
    {
        error = null;
        if (entry == null) { error = "Etkinlik seçilemedi."; return false; }

        ProgressManager progress = ProgressManager.Instance;
        if (progress == null) { error = "Adventure yöneticisi henüz hazır değil."; return false; }
        if (progress.CurrentAdventureAttempt != null)
        {
            error = "Açık Adventure denemesi bitmeden veya haritaya dönmeden yeni sürüm uygulanamaz.";
            return false;
        }
        if (!entry.IsAvailableNow(out error)) return false; // Expiry blocks new entry but never interrupts an open attempt.
        if (!TryGetLoadedEvent(entry, out AdventureEventConfig eventConfig, out error)) return false;

        if (!progress.TrySelectLocalEvent(eventConfig)) { error = "Etkinlik güvenli seçim akışına bağlanamadı."; return false; }
        ReleaseInactiveBundles();
        if (SceneLoader.Instance == null) { error = "Sahne yükleyicisi hazır değil."; return false; }
        SceneLoader.Instance.LoadAdventureMap();
        return true;
    }

    private IEnumerator RefreshCatalogRoutine(Uri uri)
    {
        isBusy = true;
        catalogError = null;
        NotifyChanged();
        using (UnityWebRequest request = UnityWebRequest.Get(uri))
        {
            request.timeout = Settings != null ? Settings.requestTimeoutSeconds : 25;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                catalogError = "Etkinlik kataloğuna ulaşılamadı. Daha önce indirilen içerik çevrimdışı kullanılabilir.";
            }
            else if (!TryAcceptCatalog(request.downloadHandler.text, out string error))
            {
                catalogError = error;
            }
            else
            {
                SaveCachedCatalog(request.downloadHandler.text);
            }
        }
        isBusy = false;
        NotifyChanged();
    }

    private IEnumerator DownloadRoutine(AdventureEventCatalogEntry entry, Uri bundleUri)
    {
        isBusy = true;
        catalogError = null;
        downloadProgress = 0f;
        NotifyChanged();

        string finalPath = GetBundlePath(entry);
        string temporaryPath = finalPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath));
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);

        using (UnityWebRequest request = UnityWebRequest.Get(bundleUri))
        {
            request.timeout = Settings != null ? Settings.requestTimeoutSeconds : 25;
            request.downloadHandler = new DownloadHandlerFile(temporaryPath);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                downloadProgress = request.downloadProgress;
                NotifyChanged();
                yield return null;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                catalogError = "Etkinlik indirilemedi. Lütfen tekrar deneyin.";
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            else if (!AdventureEventIntegrity.VerifyFile(temporaryPath, entry.fileSizeBytes, entry.sha256, out string integrityError))
            {
                catalogError = integrityError;
                File.Delete(temporaryPath);
            }
            else if (!InstallVerifiedBundle(entry, temporaryPath, finalPath, out string installError))
            {
                catalogError = installError;
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        downloadProgress = 0f;
        isBusy = false;
        NotifyChanged();
    }

    private bool InstallVerifiedBundle(AdventureEventCatalogEntry entry, string temporaryPath, string finalPath, out string error)
    {
        error = null;
        // A different hash for the same semantic version is never silently substituted.
        AdventureInstalledEventRecord existing = FindInstalled(entry.eventId, entry.contentVersion, entry.platform);
        if (existing != null && !string.Equals(existing.sha256, entry.sha256, StringComparison.OrdinalIgnoreCase))
        {
            error = "Aynı etkinlik sürümü için farklı içerik bulundu; çalışan paket korunuyor.";
            return false;
        }
        if (File.Exists(finalPath))
        {
            error = "Bu etkinlik sürümü zaten indirildi; mevcut paket değiştirilmedi.";
            return false;
        }

        File.Move(temporaryPath, finalPath);
        if (!TryLoadBundle(entry, finalPath, out _, out error))
        {
            File.Delete(finalPath);
            return false;
        }

        if (existing == null)
        {
            installedIndex.records.Add(new AdventureInstalledEventRecord
            {
                eventId = entry.eventId, contentVersion = entry.contentVersion, platform = entry.platform,
                sha256 = entry.sha256, bundlePath = finalPath, eventAssetName = entry.eventAssetName, fileSizeBytes = entry.fileSizeBytes
            });
            SaveInstalledIndex();
        }
        ReleaseInactiveBundles();
        return true;
    }

    private bool TryGetLoadedEvent(AdventureEventCatalogEntry entry, out AdventureEventConfig eventConfig, out string error)
    {
        eventConfig = null;
        error = null;
        string key = GetKey(entry.eventId, entry.contentVersion, entry.platform);
        if (loadedEvents.TryGetValue(key, out eventConfig) && eventConfig != null) return true;
        AdventureInstalledEventRecord record = FindInstalled(entry.eventId, entry.contentVersion, entry.platform);
        if (record == null || !AdventureEventIntegrity.VerifyFile(record.bundlePath, record.fileSizeBytes, record.sha256, out error))
        {
            if (string.IsNullOrWhiteSpace(error)) error = "Etkinlik henüz indirilmedi.";
            return false;
        }
        return TryLoadBundle(entry, record.bundlePath, out eventConfig, out error);
    }

    private bool TryLoadBundle(AdventureEventCatalogEntry entry, string bundlePath, out AdventureEventConfig eventConfig, out string error)
    {
        eventConfig = null;
        error = null;
        string key = GetKey(entry.eventId, entry.contentVersion, entry.platform);
        if (loadedEvents.TryGetValue(key, out eventConfig) && eventConfig != null) return true;
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null) { error = "Etkinlik paketi açılamadı."; return false; }
        eventConfig = bundle.LoadAsset<AdventureEventConfig>(entry.eventAssetName);
        if (!AdventureRemoteEventValidator.ValidateLoadedEvent(eventConfig, entry, out error))
        {
            bundle.Unload(true);
            eventConfig = null;
            return false;
        }
        loadedBundles[key] = bundle;
        loadedEvents[key] = eventConfig;
        return true;
    }

    // A downloaded-but-unselected bundle is only needed for validation. Once no attempt is open,
    // retain exactly the currently selected remote event and release every other loaded bundle.
    // The verified files and index remain on disk for offline replay.
    private void ReleaseInactiveBundles()
    {
        ProgressManager progress = ProgressManager.Instance;
        if (progress != null && progress.CurrentAdventureAttempt != null) return;

        AdventureEventConfig selectedEvent = progress != null ? progress.SelectedLocalEvent : null;
        List<string> keysToRelease = new List<string>();
        foreach (KeyValuePair<string, AdventureEventConfig> pair in loadedEvents)
            if (!ReferenceEquals(pair.Value, selectedEvent)) keysToRelease.Add(pair.Key);

        for (int i = 0; i < keysToRelease.Count; i++)
        {
            string key = keysToRelease[i];
            if (loadedBundles.TryGetValue(key, out AssetBundle bundle) && bundle != null) bundle.Unload(true);
            loadedBundles.Remove(key);
            loadedEvents.Remove(key);
        }
    }

    private bool IsReady(AdventureEventCatalogEntry entry)
    {
        AdventureInstalledEventRecord record = FindInstalled(entry.eventId, entry.contentVersion, entry.platform);
        return record != null && string.Equals(record.sha256, entry.sha256, StringComparison.OrdinalIgnoreCase) &&
               AdventureEventIntegrity.VerifyFile(record.bundlePath, record.fileSizeBytes, record.sha256, out _);
    }

    private AdventureInstalledEventRecord FindInstalled(string eventId, string contentVersion, string platform)
    {
        for (int i = 0; i < installedIndex.records.Count; i++)
        {
            AdventureInstalledEventRecord record = installedIndex.records[i];
            if (record != null && record.eventId == eventId && record.contentVersion == contentVersion && record.platform == platform)
                return record;
        }
        return null;
    }

    private bool TryAcceptCatalog(string text, out string error)
    {
        error = null;
        AdventureEventCatalog parsed = JsonUtility.FromJson<AdventureEventCatalog>(text);
        if (parsed == null || parsed.catalogSchemaVersion != AdventureEventContentContract.CatalogSchemaVersion)
        {
            error = "Etkinlik kataloğu bu oyun sürümüyle uyumlu değil.";
            return false;
        }
        if (parsed.events == null) parsed.events = new List<AdventureEventCatalogEntry>();
        for (int i = 0; i < parsed.events.Count; i++)
        {
            if (!AdventureEventContentContract.ValidateCatalogEntry(parsed.events[i], out error)) return false;
        }
        catalog = parsed;
        return true;
    }

    private bool TryGetCatalogUri(out Uri uri, out string error)
    {
        uri = null;
        error = null;
        AdventureEventCatalogSettings settings = Settings;
        if (settings == null || string.IsNullOrWhiteSpace(settings.catalogUrl)) { error = "Etkinlik kataloğu henüz yapılandırılmadı."; return false; }
        if (!Uri.TryCreate(settings.catalogUrl, UriKind.Absolute, out uri)) { error = "Etkinlik kataloğu adresi geçersiz."; return false; }
        if (uri.Scheme == Uri.UriSchemeHttps) return true;
#if UNITY_EDITOR
        if (uri.Scheme == Uri.UriSchemeHttp && settings.allowInsecureLocalHttpInEditor &&
            (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1")) return true;
#endif
        error = "Etkinlik kataloğu güvenli HTTPS adresi kullanmalı.";
        return false;
    }

    private string GetBundlePath(AdventureEventCatalogEntry entry) => Path.Combine(
        Application.persistentDataPath, "AdventureEvents", Sanitize(entry.eventId), Sanitize(entry.contentVersion), Sanitize(entry.platform), entry.sha256 + ".bundle");
    private static string GetKey(string eventId, string contentVersion, string platform) => eventId + "|" + contentVersion + "|" + platform;
    private static string Sanitize(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
    private string CachedCatalogPath => Path.Combine(Application.persistentDataPath, CachedCatalogFileName);
    private string InstalledIndexPath => Path.Combine(Application.persistentDataPath, InstalledIndexFileName);

    private void LoadCachedCatalog()
    {
        if (File.Exists(CachedCatalogPath)) TryAcceptCatalog(File.ReadAllText(CachedCatalogPath), out _);
    }
    private void SaveCachedCatalog(string text) => File.WriteAllText(CachedCatalogPath, text);
    private void LoadInstalledIndex()
    {
        if (!File.Exists(InstalledIndexPath)) return;
        AdventureInstalledEventIndex loaded = JsonUtility.FromJson<AdventureInstalledEventIndex>(File.ReadAllText(InstalledIndexPath));
        if (loaded != null && loaded.records != null) installedIndex = loaded;
    }
    private void SaveInstalledIndex() => File.WriteAllText(InstalledIndexPath, JsonUtility.ToJson(installedIndex));
    private void NotifyChanged() => Changed?.Invoke();
}
