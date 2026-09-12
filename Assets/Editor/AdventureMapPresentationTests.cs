using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class AdventureMapPresentationTests
{
    [Test]
    public void MapBackgroundUsesEventSpriteThenRestoresSceneFallback()
    {
        GameObject targetObject = new GameObject("Map Background Test", typeof(RectTransform), typeof(Image));
        Texture2D fallbackTexture = new Texture2D(4, 2);
        Texture2D eventTexture = new Texture2D(2, 4);
        Sprite fallbackSprite = Sprite.Create(fallbackTexture, new Rect(0f, 0f, 4f, 2f), new Vector2(.5f, .5f));
        Sprite eventSprite = Sprite.Create(eventTexture, new Rect(0f, 0f, 2f, 4f), new Vector2(.5f, .5f));

        try
        {
            Image image = targetObject.GetComponent<Image>();
            AdventureMapController.ApplyMapBackgroundSprite(image, fallbackSprite, eventSprite);

            Assert.That(image.sprite, Is.SameAs(eventSprite));
            Assert.That(image.raycastTarget, Is.False);
            AspectRatioFitter fitter = targetObject.GetComponent<AspectRatioFitter>();
            Assert.That(fitter, Is.Not.Null);
            Assert.That(fitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            Assert.That(fitter.aspectRatio, Is.EqualTo(.5f));

            AdventureMapController.ApplyMapBackgroundSprite(image, fallbackSprite, null);
            Assert.That(image.sprite, Is.SameAs(fallbackSprite));
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(fallbackSprite);
            Object.DestroyImmediate(eventSprite);
            Object.DestroyImmediate(fallbackTexture);
            Object.DestroyImmediate(eventTexture);
        }
    }

    [Test]
    public void MapBackgroundPathDoesNotSelectGameplayBackground()
    {
        GameObject targetObject = new GameObject("Map Background Test", typeof(RectTransform), typeof(Image));
        AdventureEventConfig eventConfig = ScriptableObject.CreateInstance<AdventureEventConfig>();
        Texture2D mapTexture = new Texture2D(2, 2);
        Texture2D gameplayTexture = new Texture2D(2, 2);
        eventConfig.mapBackgroundVisual = Sprite.Create(mapTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(.5f, .5f));
        eventConfig.gameplayBackgroundVisual = Sprite.Create(gameplayTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(.5f, .5f));

        try
        {
            Image image = targetObject.GetComponent<Image>();
            AdventureMapController.ApplyMapBackgroundSprite(image, null, eventConfig.mapBackgroundVisual);

            Assert.That(image.sprite, Is.SameAs(eventConfig.mapBackgroundVisual));
            Assert.That(image.sprite, Is.Not.SameAs(eventConfig.gameplayBackgroundVisual));
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(eventConfig.mapBackgroundVisual);
            Object.DestroyImmediate(eventConfig.gameplayBackgroundVisual);
            Object.DestroyImmediate(mapTexture);
            Object.DestroyImmediate(gameplayTexture);
            Object.DestroyImmediate(eventConfig);
        }
    }
}
