using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AdventureEventDistributionTests
{
    [Test]
    public void DistributionBuildIncludesEventStagesAndCollectibleDatabase()
    {
        string assetRoot = "Assets/AdventureEvents/__DistributionTests_" + Guid.NewGuid().ToString("N");
        string outputRoot = Path.Combine(Path.GetTempPath(), "arxon-distribution-" + Guid.NewGuid().ToString("N"));
        try
        {
            AdventureEventConfig eventConfig = CreatePersistentEvent(assetRoot);
            Assert.That(AdventureEventDistributionBuilder.PrepareDistributionPackage(eventConfig, outputRoot, BuildTarget.StandaloneOSX, out string message), Is.True, message);

            string catalogPath = Path.Combine(outputRoot, "adventure-event-catalog.json");
            AdventureEventCatalog catalog = JsonUtility.FromJson<AdventureEventCatalog>(File.ReadAllText(catalogPath));
            Assert.That(catalog.events, Has.Count.EqualTo(1));
            AdventureEventCatalogEntry entry = catalog.events[0];
            Assert.That(entry.eventId, Is.EqualTo(eventConfig.eventId));
            Assert.That(entry.generatorVersion, Is.EqualTo(eventConfig.generationSettings.generatorVersion));
            Assert.That(AdventureEventIntegrity.VerifyFile(Path.Combine(outputRoot, entry.bundleRelativePath), entry.fileSizeBytes, entry.sha256, out string integrityError), Is.True, integrityError);

            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(outputRoot, entry.bundleRelativePath));
            Assert.That(bundle, Is.Not.Null);
            AdventureEventConfig loaded = bundle.LoadAsset<AdventureEventConfig>(entry.eventAssetName);
            Assert.That(AdventureRemoteEventValidator.ValidateLoadedEvent(loaded, entry, out string validationError), Is.True, validationError);
            Assert.That(loaded.levelConfigs, Has.Length.EqualTo(1));
            Assert.That(loaded.collectibleDatabase, Is.Not.Null);
            bundle.Unload(true);
        }
        finally
        {
            AssetDatabase.DeleteAsset(assetRoot);
            AssetDatabase.SaveAssets();
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
        }
    }

    [Test]
    public void SameEventVersionDoesNotOverwriteDistributionOutput()
    {
        string assetRoot = "Assets/AdventureEvents/__DistributionTests_" + Guid.NewGuid().ToString("N");
        string outputRoot = Path.Combine(Path.GetTempPath(), "arxon-distribution-" + Guid.NewGuid().ToString("N"));
        try
        {
            AdventureEventConfig eventConfig = CreatePersistentEvent(assetRoot);
            Assert.That(AdventureEventDistributionBuilder.PrepareDistributionPackage(eventConfig, outputRoot, BuildTarget.StandaloneOSX, out string firstMessage), Is.True, firstMessage);
            eventConfig = AssetDatabase.LoadAssetAtPath<AdventureEventConfig>(assetRoot + "/Event.asset");
            Assert.That(eventConfig, Is.Not.Null);
            Assert.That(AdventureEventDistributionBuilder.PrepareDistributionPackage(eventConfig, outputRoot, BuildTarget.StandaloneOSX, out string secondMessage), Is.False);
            Assert.That(secondMessage, Does.Contain("zaten var"));
        }
        finally
        {
            AssetDatabase.DeleteAsset(assetRoot);
            AssetDatabase.SaveAssets();
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
        }
    }

    [Test]
    public void CorruptOrPartialFileFailsIntegrityWithoutChangingVerifiedFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "arxon-integrity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string verified = Path.Combine(root, "verified.bundle");
        string partial = Path.Combine(root, "download.part");
        try
        {
            File.WriteAllBytes(verified, new byte[] { 1, 2, 3, 4, 5 });
            string hash = AdventureEventIntegrity.CalculateSha256(verified);
            File.WriteAllBytes(partial, new byte[] { 1, 2, 3 });
            Assert.That(AdventureEventIntegrity.VerifyFile(verified, 5, hash, out string verifiedError), Is.True, verifiedError);
            Assert.That(AdventureEventIntegrity.VerifyFile(partial, 5, hash, out _), Is.False);
            Assert.That(AdventureEventIntegrity.VerifyFile(verified, 5, hash, out verifiedError), Is.True, verifiedError);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public void IncompatibleContentIsRejectedBeforeDownload()
    {
        AdventureEventCatalogEntry entry = ValidEntry();
        entry.gameCompatibilityVersion = "future-game";
        Assert.That(AdventureEventContentContract.ValidateCatalogEntry(entry, out string error), Is.False);
        Assert.That(error, Does.Contain("oyun sürümü"));
    }

    [Test]
    public void CatalogEntryUsesCurrentPlatformAndCanBeCheckedForOfflineReadyContent()
    {
        AdventureEventCatalogEntry entry = ValidEntry();
        entry.platform = AdventureEventContentContract.CurrentPlatformKey();
        Assert.That(AdventureEventContentContract.ValidateCatalogEntry(entry, out string error), Is.True, error);
        Assert.That(entry.IsAvailableNow(out string windowError), Is.True, windowError);
    }

    private static AdventureEventCatalogEntry ValidEntry() => new AdventureEventCatalogEntry
    {
        eventId = "test-event", contentVersion = "1.0.0", dataSchemaVersion = AdventureEventContentContract.EventDataSchemaVersion,
        gameCompatibilityVersion = AdventureEventContentContract.GameCompatibilityVersion, platform = "StandaloneOSX",
        bundleRelativePath = "test/event.bundle", eventAssetName = "Assets/Test.asset", fileSizeBytes = 5,
        sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    };

    private static AdventureEventConfig CreatePersistentEvent(string root)
    {
        AssetDatabase.CreateFolder("Assets/AdventureEvents", Path.GetFileName(root));
        CollectibleDatabase database = ScriptableObject.CreateInstance<CollectibleDatabase>();
        database.collectibles = new List<CollectibleDefinition> { new CollectibleDefinition { id = "REMOTE_TEST", icon = null, availableInGenerator = true } };
        AssetDatabase.CreateAsset(database, root + "/Collectibles.asset");

        AdventureLevelConfig stage = ScriptableObject.CreateInstance<AdventureLevelConfig>();
        stage.eventId = "distribution-test";
        stage.contentVersion = "1.0.0";
        stage.levelId = "distribution-test-stage-001";
        stage.displayedLevelNumber = 1;
        stage.levelNumber = 1;
        stage.authoringMode = AdventureStageAuthoringMode.DirectRecipeAuthoring;
        stage.directRecipe = new AdventureStageRecipe { hasMoveLimit = false };
        stage.objectives = new List<AdventureObjectiveDefinition>
        {
            new AdventureObjectiveDefinition { action = AdventureObjectiveAction.ClearRows, target = AdventureObjectiveTarget.Rows, requiredAmount = 1 }
        };
        AssetDatabase.CreateAsset(stage, root + "/L001.asset");

        AdventureEventConfig eventConfig = ScriptableObject.CreateInstance<AdventureEventConfig>();
        eventConfig.eventId = "distribution-test";
        eventConfig.contentVersion = "1.0.0";
        eventConfig.eventName = "Distribution Test";
        eventConfig.levelConfigs = new[] { stage };
        eventConfig.levelCount = 1;
        eventConfig.collectibleDatabase = database;
        eventConfig.generationSettings = new AdventureEventGenerationSettings { generatorVersion = AdventureEventLevelFactory.CurrentGeneratorVersion };
        AssetDatabase.CreateAsset(eventConfig, root + "/Event.asset");
        AssetDatabase.SaveAssets();
        return eventConfig;
    }
}
