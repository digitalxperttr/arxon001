using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class FireV2SpriteLibrary
{
    private const float PixelsPerUnit = 100f;
    private const float FireBaseBorderPixels = 48f;

    private static Sprite fireBaseSprite;
    private static readonly Dictionary<GemColor, Sprite> FireSymbolSprites = new Dictionary<GemColor, Sprite>();

    public static Sprite GetFireBaseSprite()
    {
        if (fireBaseSprite == null)
        {
            Vector4 border = new Vector4(FireBaseBorderPixels, FireBaseBorderPixels, FireBaseBorderPixels, FireBaseBorderPixels);
            fireBaseSprite = LoadSpriteMultiPath("Gem_Fire_Base", "FireV2/Gem_Fire_Base", "ART/UI/Gem_Fire_Base", border);
        }

        return fireBaseSprite;
    }

    public static Sprite GetFireSymbolSprite(GemColor targetColor)
    {
        if (!FireSymbolSprites.TryGetValue(targetColor, out Sprite symbolSprite) || symbolSprite == null)
        {
            string fileName = $"FireSymbol_{targetColor}";
            symbolSprite = LoadSpriteMultiPath(
                fileName,
                $"FireV2/{fileName}",
                $"ART/UI/{fileName}",
                Vector4.zero
            );
            FireSymbolSprites[targetColor] = symbolSprite;
        }

        return symbolSprite;
    }

    private static Sprite LoadSpriteMultiPath(string fileName, string resourcePath, string projectRelativePath, Vector4 border)
    {
#if UNITY_EDITOR
        // 1. Editor AssetDatabase (En güvenli, import ayarlarını ve dilimlemeyi tam koruyan yöntem)
        string[] searchPaths = new string[]
        {
            $"Assets/{projectRelativePath}.png",
            $"Assets/ART/UI/{fileName}.png",
            $"Assets/Resources/{resourcePath}.png",
            $"Assets/Resources/FireV2/{fileName}.png"
        };

        foreach (string path in searchPaths)
        {
            Sprite editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (editorSprite != null)
                return editorSprite;
        }
#endif

        // 2. Resources.Load (Eğer Resources klasöründeyse)
        Sprite singleSprite = Resources.Load<Sprite>(resourcePath);
        if (singleSprite != null)
            return singleSprite;

        Sprite[] importedSprites = Resources.LoadAll<Sprite>(resourcePath);
        if (importedSprites != null && importedSprites.Length > 0)
        {
            Sprite importedSprite = importedSprites[0];
            if (border.sqrMagnitude <= 0f)
                return importedSprite;

            return Sprite.Create(
                importedSprite.texture,
                importedSprite.rect,
                new Vector2(0.5f, 0.5f),
                importedSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border);
        }

        Texture2D resourceTex = Resources.Load<Texture2D>(resourcePath);
        if (resourceTex != null)
        {
            Rect rect = new Rect(0f, 0f, resourceTex.width, resourceTex.height);
            return Sprite.Create(
                resourceTex,
                rect,
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border);
        }

        // 3. Disk File Fallback (Assets/ART/UI veya Assets/Resources doğrudan dosya okuma)
        string[] diskPaths = new string[]
        {
            Path.Combine(Application.dataPath, "ART/UI", $"{fileName}.png"),
            Path.Combine(Application.dataPath, "Resources/FireV2", $"{fileName}.png"),
            Path.Combine(Application.dataPath, "Resources", $"{fileName}.png")
        };

        foreach (string diskPath in diskPaths)
        {
            if (File.Exists(diskPath))
            {
                try
                {
                    byte[] fileData = File.ReadAllBytes(diskPath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(fileData))
                    {
                        Rect rect = new Rect(0f, 0f, tex.width, tex.height);
                        return Sprite.Create(
                            tex,
                            rect,
                            new Vector2(0.5f, 0.5f),
                            PixelsPerUnit,
                            0,
                            SpriteMeshType.FullRect,
                            border);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FireV2SpriteLibrary] Failed to load {diskPath}: {ex.Message}");
                }
            }
        }

        return null;
    }
}
