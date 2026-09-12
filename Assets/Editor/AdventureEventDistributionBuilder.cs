using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AdventureEventDistributionBuilder
{
    private const string CatalogFileName = "adventure-event-catalog.json";
    private const string BundleName = "event.bundle";

    public static bool PrepareDistributionPackage(AdventureEventConfig eventConfig, string outputRoot, BuildTarget target, out string message)
    {
        message = null;
        if (EditorApplication.isPlaying)
        {
            message = "Dağıtım paketi yalnızca Play Mode dışında hazırlanabilir.";
            return false;
        }
        if (eventConfig == null) { message = "Event config bulunamadı."; return false; }
        if (string.IsNullOrWhiteSpace(outputRoot) || !Path.IsPathRooted(outputRoot)) { message = "Geçerli bir çıktı klasörü seçin."; return false; }

        AdventureContentValidationResult validation = AdventureEventPackageEditorUtility.ValidateEvent(eventConfig);
        if (validation.HasErrors)
        {
            message = "Dağıtım paketi hazırlanmadı; içerik doğrulaması başarısız:\n- " + string.Join("\n- ", validation.Errors);
            return false;
        }
        if (!TryCollectDependencies(eventConfig, out string[] dependencies, out string eventAssetPath, out message)) return false;

        string platform = target.ToString();
        string safeEvent = SafePathPart(eventConfig.eventId);
        string safeVersion = SafePathPart(eventConfig.contentVersion);
        string finalFolder = Path.Combine(outputRoot, safeEvent, safeVersion, platform);
        if (Directory.Exists(finalFolder))
        {
            message = "Bu event ve içerik sürümü için çıktı zaten var; hiçbir dosya değiştirilmedi: " + finalFolder;
            return false;
        }

        // BuildPipeline rejects hidden-dot folder names on some platforms.
        string temporaryRoot = Path.Combine(outputRoot, "arxon-event-package-tmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temporaryRoot);
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = dependencies
            };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                temporaryRoot,
                new[] { build },
                BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ChunkBasedCompression,
                target);
            string temporaryBundle = Path.Combine(temporaryRoot, BundleName);
            if (manifest == null || !File.Exists(temporaryBundle)) throw new InvalidOperationException("Unity AssetBundle çıktısı oluşturulamadı.");

            long size = new FileInfo(temporaryBundle).Length;
            string sha = AdventureEventIntegrity.CalculateSha256(temporaryBundle);
            AdventureEventCatalogEntry entry = new AdventureEventCatalogEntry
            {
                eventId = eventConfig.eventId,
                contentVersion = eventConfig.contentVersion,
                dataSchemaVersion = AdventureEventContentContract.EventDataSchemaVersion,
                gameCompatibilityVersion = AdventureEventContentContract.GameCompatibilityVersion,
                eventName = eventConfig.eventName,
                eventDescription = eventConfig.eventDescription,
                generatorVersion = eventConfig.generationSettings != null ? eventConfig.generationSettings.generatorVersion : string.Empty,
                platform = platform,
                bundleRelativePath = ToCatalogRelativePath(safeEvent, safeVersion, platform, BundleName),
                eventAssetName = eventAssetPath,
                fileSizeBytes = size,
                sha256 = sha,
                startsAtUtc = eventConfig.plannedStartDate,
                endsAtUtc = eventConfig.plannedEndDate
            };
            if (!AdventureEventContentContract.ValidateCatalogEntry(entry, out string contractError)) throw new InvalidOperationException(contractError);

            AdventureEventCatalog catalog = LoadCatalog(outputRoot, out string catalogError);
            if (catalog == null) throw new InvalidOperationException(catalogError);
            if (ContainsConflictingVersion(catalog, entry, out string conflict)) throw new InvalidOperationException(conflict);

            Directory.CreateDirectory(Path.GetDirectoryName(finalFolder));
            Directory.Move(temporaryRoot, finalFolder);
            temporaryRoot = null; // Final folder belongs to the committed package transaction now.
            catalog.events.Add(entry);
            if (!WriteCatalogAtomically(outputRoot, catalog, out catalogError))
            {
                Directory.Delete(finalFolder, true);
                throw new InvalidOperationException(catalogError);
            }

            message = "Dağıtım paketi hazırlandı. Bundle: " + Path.Combine(finalFolder, BundleName) + "\nKatalog: " + Path.Combine(outputRoot, CatalogFileName);
            return true;
        }
        catch (Exception exception)
        {
            message = "Dağıtım paketi hazırlanamadı. Kaynak event değiştirilmedi. " + exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryRoot) && Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }
    }

    private static bool TryCollectDependencies(AdventureEventConfig eventConfig, out string[] dependencies, out string eventAssetPath, out string error)
    {
        dependencies = null;
        eventAssetPath = AssetDatabase.GetAssetPath(eventConfig);
        error = null;
        if (string.IsNullOrWhiteSpace(eventAssetPath) || !eventAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            error = "Dağıtılacak event, proje içindeki bir asset olmalıdır.";
            return false;
        }
        string[] allDependencies = AssetDatabase.GetDependencies(eventAssetPath, true);
        List<string> contentDependencies = new List<string>();
        for (int i = 0; i < allDependencies.Length; i++)
        {
            string dependency = allDependencies[i];
            if (!dependency.StartsWith("Assets/", StringComparison.Ordinal)) continue;
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(dependency);
            // Script references are editor metadata dependencies, not distributable content.
            if (mainAsset is MonoScript || dependency.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) continue;
            if (mainAsset == null)
            {
                error = "Bağımlılık çözümlenemedi: " + dependency;
                return false;
            }
            contentDependencies.Add(dependency);
        }
        if (!contentDependencies.Contains(eventAssetPath)) contentDependencies.Add(eventAssetPath);
        dependencies = contentDependencies.ToArray();
        return true;
    }

    private static AdventureEventCatalog LoadCatalog(string outputRoot, out string error)
    {
        error = null;
        string path = Path.Combine(outputRoot, CatalogFileName);
        if (!File.Exists(path)) return new AdventureEventCatalog();
        try
        {
            AdventureEventCatalog catalog = JsonUtility.FromJson<AdventureEventCatalog>(File.ReadAllText(path));
            if (catalog == null || catalog.catalogSchemaVersion != AdventureEventContentContract.CatalogSchemaVersion)
            {
                error = "Mevcut katalog şeması desteklenmiyor; katalog değiştirilmedi.";
                return null;
            }
            if (catalog.events == null) catalog.events = new List<AdventureEventCatalogEntry>();
            return catalog;
        }
        catch (Exception exception)
        {
            error = "Mevcut katalog okunamadı; katalog değiştirilmedi. " + exception.Message;
            return null;
        }
    }

    private static bool ContainsConflictingVersion(AdventureEventCatalog catalog, AdventureEventCatalogEntry candidate, out string error)
    {
        error = null;
        for (int i = 0; i < catalog.events.Count; i++)
        {
            AdventureEventCatalogEntry existing = catalog.events[i];
            if (existing == null || existing.eventId != candidate.eventId || existing.contentVersion != candidate.contentVersion || existing.platform != candidate.platform) continue;
            error = string.Equals(existing.sha256, candidate.sha256, StringComparison.OrdinalIgnoreCase)
                ? "Bu event ve içerik sürümü katalogda zaten bulunuyor; hiçbir dosya değiştirilmedi."
                : "Aynı event ve içerik sürümü için farklı içerik katalogda bulunuyor; üzerine yazma yapılmadı.";
            return true;
        }
        return false;
    }

    private static bool WriteCatalogAtomically(string outputRoot, AdventureEventCatalog catalog, out string error)
    {
        error = null;
        try
        {
            Directory.CreateDirectory(outputRoot);
            string path = Path.Combine(outputRoot, CatalogFileName);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(catalog, true));
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
            return true;
        }
        catch (Exception exception)
        {
            error = "Katalog güvenle güncellenemedi. " + exception.Message;
            return false;
        }
    }

    private static string SafePathPart(string value) => AdventureEventPackageEditorUtility.SanitizePathPart(value).ToLowerInvariant();
    private static string ToCatalogRelativePath(params string[] parts) => string.Join("/", parts);
}
