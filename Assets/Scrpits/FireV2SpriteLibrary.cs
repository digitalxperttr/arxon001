using System.Collections.Generic;
using UnityEngine;

public static class FireV2SpriteLibrary
{
    private const string FireBaseResourcePath = "FireV2/Gem_Fire_Base";
    private const float PixelsPerUnit = 100f;
    private const float FireBaseBorderPixels = 48f;

    private static Sprite fireBaseSprite;
    private static readonly Dictionary<GemColor, Sprite> FireSymbolSprites = new Dictionary<GemColor, Sprite>();

    public static Sprite GetFireBaseSprite()
    {
        if (fireBaseSprite == null)
        {
            fireBaseSprite = LoadRuntimeSprite(
                FireBaseResourcePath,
                new Vector4(FireBaseBorderPixels, FireBaseBorderPixels, FireBaseBorderPixels, FireBaseBorderPixels));
        }

        return fireBaseSprite;
    }

    public static Sprite GetFireSymbolSprite(GemColor targetColor)
    {
        if (!FireSymbolSprites.TryGetValue(targetColor, out Sprite symbolSprite) || symbolSprite == null)
        {
            symbolSprite = LoadRuntimeSprite($"FireV2/FireSymbol_{targetColor}", Vector4.zero);
            FireSymbolSprites[targetColor] = symbolSprite;
        }

        return symbolSprite;
    }

    private static Sprite LoadRuntimeSprite(string resourcePath, Vector4 border)
    {
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

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
        {
            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            return Sprite.Create(
                texture,
                rect,
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border);
        }

        return null;
    }
}
