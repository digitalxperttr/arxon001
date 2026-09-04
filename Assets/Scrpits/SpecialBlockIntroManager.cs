using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialBlockIntroManager : MonoBehaviour
{
    public static SpecialBlockIntroManager Instance { get; private set; }

    private const string IntroPrefix = "Tutorial_Intro_";

    [Header("UI & Fonts")]
    [SerializeField] private TMP_FontAsset customFont;
    [SerializeField] [Range(0.5f, 0.95f)] private float dimAlpha = 0.84f;

    public bool IsIntroActive { get; private set; }

    private GameObject worldDimObject;
    private SpriteRenderer worldDimRenderer;
    private Sprite worldDimSprite;

    private GameObject arrowPointerObject;
    private SpriteRenderer arrowPointerRenderer;
    private Sprite arrowPointerSprite;

    private RectTransform introRoot;
    private CanvasGroup cardGroup;
    private RectTransform cardRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI descriptionLabel;
    private TextMeshProUGUI tapHintLabel;
    private Button fullScreenDismissButton;

    private Block currentSpecialBlock;
    private int originalBlockSortingOrder = 10;
    private SpriteRenderer currentBlockRenderer;
    private Coroutine introRoutine;
    private bool userDismissed;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (customFont == null)
        {
            customFont = Resources.Load<TMP_FontAsset>("Fonts/SweetToneTR SDF");
#if UNITY_EDITOR
            if (customFont == null)
            {
                customFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ART/TextMesh Pro/Fonts/SweetToneTR SDF.asset");
            }
#endif
        }
    }

    public static void ResetAllIntrosForTesting()
    {
        PlayerPrefs.DeleteKey(IntroPrefix + "Fire");
        PlayerPrefs.DeleteKey(IntroPrefix + "Ice");
        PlayerPrefs.DeleteKey(IntroPrefix + "Slice");
        PlayerPrefs.DeleteKey(IntroPrefix + "Chained");
        PlayerPrefs.DeleteKey(IntroPrefix + "Rock");
        PlayerPrefs.Save();
    }

    public bool HasSeen(BlockType type)
    {
        return PlayerPrefs.GetInt(IntroPrefix + type.ToString(), 0) == 1;
    }

    public void MarkAsSeen(BlockType type)
    {
        PlayerPrefs.SetInt(IntroPrefix + type.ToString(), 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Tahtadaki aktif blokları tarar; ilk kez karşılaşılan özel bir blok varsa tanıtımı tetikler.
    /// </summary>
    public bool CheckActiveBoardForSpecialIntros(GridManager grid)
    {
        if (grid == null || IsIntroActive)
            return false;

        if (FirstTimeTutorial.Instance != null && FirstTimeTutorial.Instance.IsRunning)
            return false;

        if (grid.activeBlocks == null || grid.activeBlocks.Count == 0)
            return false;

        for (int i = 0; i < grid.activeBlocks.Count; i++)
        {
            Block b = grid.activeBlocks[i];
            if (b == null || !b.gameObject.activeInHierarchy || b.isBeingDestroyed || b.isMoving)
                continue;

            BlockType specialType = IdentifySpecialBlockType(b);
            if (specialType != BlockType.Normal && !HasSeen(specialType))
            {
                TriggerIntro(b, specialType);
                return true;
            }
        }

        return false;
    }

    private BlockType IdentifySpecialBlockType(Block b)
    {
        if (b.blockType == BlockType.Fire)
            return BlockType.Fire;

        if (b.blockType == BlockType.Slice)
            return BlockType.Slice;

        if (b.isFrozen || b.blockType == BlockType.Ice)
            return BlockType.Ice;

        if (b.isChained || b.blockType == BlockType.Chained)
            return BlockType.Chained;

        if (b.isRock || b.blockType == BlockType.Rock)
            return BlockType.Rock;

        return BlockType.Normal;
    }

    public void TriggerIntro(Block block, BlockType type)
    {
        if (block == null || IsIntroActive)
            return;

        MarkAsSeen(type);

        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = StartCoroutine(ShowIntroSequenceRoutine(block, type));
    }

    private IEnumerator ShowIntroSequenceRoutine(Block block, BlockType type)
    {
        IsIntroActive = true;
        currentSpecialBlock = block;
        userDismissed = false;

        // 1. Hedef bloğu Spotlight olarak öne çıkar (SortingOrder = 24)
        currentBlockRenderer = block.GetComponent<SpriteRenderer>();
        int targetLayerID = 0;
        if (currentBlockRenderer != null)
        {
            originalBlockSortingOrder = currentBlockRenderer.sortingOrder;
            targetLayerID = currentBlockRenderer.sortingLayerID;
            currentBlockRenderer.sortingOrder = 24;
        }
        block.SetHighlight(true);

        // 2. Derin karartmayı aç (SortingOrder = 18)
        EnsureWorldDim(targetLayerID);
        yield return FadeWorldDim(0f, dimAlpha, 0.30f);

        // 3. Bloğu gösteren aşağı yönlü ok göstergesi (SortingOrder = 30)
        EnsureArrowPointer(targetLayerID);
        StartCoroutine(ArrowPointerPulseRoutine(block));

        // 4. UI Açıklama Kartını oluştur ve ekrana getir
        EnsureIntroUI();
        ConfigureCardContent(type, block);
        yield return AnimateCardIn();

        // 5. Oyuncunun ekrana dokunmasını bekle
        while (!userDismissed)
        {
            yield return null;
        }

        // 6. Kapanış animasyonu ve temizlik
        yield return AnimateCardOut();
        yield return FadeWorldDim(worldDimRenderer.color.a, 0f, 0.20f);

        if (currentBlockRenderer != null)
        {
            currentBlockRenderer.sortingOrder = originalBlockSortingOrder;
            if (currentSpecialBlock != null)
                currentSpecialBlock.SetHighlight(false);
        }

        if (arrowPointerObject != null)
            arrowPointerObject.SetActive(false);

        if (introRoot != null)
            introRoot.gameObject.SetActive(false);

        currentSpecialBlock = null;
        IsIntroActive = false;
        introRoutine = null;
    }

    private void ConfigureCardContent(BlockType type, Block block)
    {
        string title = "";
        string description = "";

        switch (type)
        {
            case BlockType.Fire:
                title = "Ateş Bloğu";
                description = "Eşleştiğinde göğsündeki sembol rengindeki tüm taşları yakar!";
                break;
            case BlockType.Ice:
                title = "Buz Bloğu";
                description = "Dondurulmuş taş! Yanındaki satırı patlatıp buzunu erit ve taşı serbest bırak!";
                break;
            case BlockType.Slice:
                title = "Dilimleme Bloğu";
                description = "Eşleştiğinde altındaki ve üstündeki büyük blokları senin için ortadan ikiye böler!";
                break;
            case BlockType.Chained:
                title = "Kafes Bloğu";
                description = "Demir parmaklıklı kafes! Satır patlatarak parmaklıkları kır. Ama unutma; kafesten kurtulmak öyle kolay değil, 2 patlatmaya ihtiyacın var!";
                break;
            case BlockType.Rock:
                title = "Kaya Bloğu";
                description = "Ağır ve kaydırılamaz engel! Altındaki satırları temizleyip aşağı düşürerek tahtadan çıkar.";
                break;
        }

        if (titleLabel != null) titleLabel.text = title;
        if (descriptionLabel != null) descriptionLabel.text = description;

        // Kartın bloğun üstünü kapatmaması ve oyuncunun göz hizasında durması için:
        // Blok alt yarısında (genelde 3-4 satır dolu) kart 4 hücre aşağıda (+50f) durur;
        // Blok üst yarıdaysa kart alt tarafta (-480f) açılır.
        if (cardRect != null)
        {
            float targetAnchoredY = (block != null && block.y >= 5) ? -480f : 50f;
            cardRect.anchoredPosition = new Vector2(0f, targetAnchoredY);
        }
    }

    private void EnsureWorldDim(int layerID)
    {
        if (worldDimObject != null)
            return;

        worldDimObject = new GameObject("SpecialIntroWorldDim");
        worldDimRenderer = worldDimObject.AddComponent<SpriteRenderer>();
        worldDimRenderer.sortingLayerID = layerID;
        worldDimRenderer.sortingOrder = 18;

        if (worldDimSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply();
            worldDimSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        worldDimRenderer.sprite = worldDimSprite;
        worldDimRenderer.color = new Color(0.01f, 0.02f, 0.04f, 0f);

        Camera cam = Camera.main;
        float height = cam != null ? cam.orthographicSize * 2.5f : 30f;
        float width = height * (cam != null ? cam.aspect : 1.77f) * 1.5f;
        Vector3 camPos = cam != null ? cam.transform.position : new Vector3(3.5f, 4.5f, 0f);

        worldDimObject.transform.position = new Vector3(camPos.x, camPos.y, -0.05f);
        worldDimObject.transform.localScale = new Vector3(width, height, 1f);
    }

    private IEnumerator FadeWorldDim(float from, float to, float duration)
    {
        if (worldDimRenderer == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float a = Mathf.Lerp(from, to, t);
            worldDimRenderer.color = new Color(0.01f, 0.02f, 0.04f, a);
            yield return null;
        }

        worldDimRenderer.color = new Color(0.01f, 0.02f, 0.04f, to);
    }

    private void EnsureArrowPointer(int layerID)
    {
        if (arrowPointerObject != null)
            return;

        arrowPointerObject = new GameObject("SpecialIntroArrowPointer");
        arrowPointerRenderer = arrowPointerObject.AddComponent<SpriteRenderer>();
        arrowPointerRenderer.sortingLayerID = layerID;
        arrowPointerRenderer.sortingOrder = 30;

        if (arrowPointerSprite == null)
        {
            arrowPointerSprite = GenerateDownArrowSprite();
        }

        arrowPointerRenderer.sprite = arrowPointerSprite;
        arrowPointerObject.transform.localScale = Vector3.one * 0.85f;
        arrowPointerObject.SetActive(false);
    }

    private IEnumerator ArrowPointerPulseRoutine(Block block)
    {
        if (arrowPointerObject == null || block == null)
            yield break;

        arrowPointerObject.SetActive(true);
        Vector3 basePos = block.transform.position + new Vector3(0f, 1.25f, -0.25f);

        while (IsIntroActive && !userDismissed)
        {
            if (block == null || !block.gameObject.activeInHierarchy)
                yield break;

            float wave = Mathf.Sin(Time.time * 6f) * 0.12f;
            arrowPointerObject.transform.position = basePos + new Vector3(0f, wave, 0f);
            yield return null;
        }

        arrowPointerObject.SetActive(false);
    }

    private void EnsureIntroUI()
    {
        if (introRoot != null)
        {
            introRoot.gameObject.SetActive(true);
            introRoot.SetAsLastSibling();
            return;
        }

        Canvas hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
            return;

        // 1. Root CanvasGroup
        GameObject rootObj = new GameObject("SpecialBlockIntroRoot", typeof(RectTransform), typeof(CanvasGroup));
        introRoot = rootObj.GetComponent<RectTransform>();
        introRoot.SetParent(hudCanvas.transform, false);
        introRoot.anchorMin = Vector2.zero;
        introRoot.anchorMax = Vector2.one;
        introRoot.offsetMin = Vector2.zero;
        introRoot.offsetMax = Vector2.zero;
        introRoot.SetAsLastSibling();

        // 2. Tam ekran görünmez tıklama butonu (Tap anywhere to dismiss)
        GameObject btnObj = new GameObject("TapDismissButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.SetParent(introRoot, false);
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = Vector2.one;
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0f);
        btnImg.raycastTarget = true;

        fullScreenDismissButton = btnObj.GetComponent<Button>();
        fullScreenDismissButton.transition = Selectable.Transition.None;
        fullScreenDismissButton.onClick.AddListener(OnUserTappedScreen);

        // 3. Şık Tanıtım Kartı Paneli
        GameObject cardObj = new GameObject("IntroCard", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.SetParent(introRoot, false);
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(900f, 370f);
        cardRect.anchoredPosition = new Vector2(0f, 50f);

        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.sprite = GenerateCard9SliceSprite();
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(0.08f, 0.10f, 0.15f, 0.94f);
        cardImg.raycastTarget = false;

        cardGroup = cardObj.GetComponent<CanvasGroup>();
        cardGroup.interactable = false;
        cardGroup.blocksRaycasts = false;

        // 4. Başlık (Daha yukarı alındı, metinle arası açıldı)
        titleLabel = CreateTextElement(cardRect, "Title", "Özel Blok", new Vector2(0f, 120f), new Vector2(820f, 65f), 48f, new Color(1f, 0.88f, 0.22f, 1f), true);

        // 5. Açıklama (Ferah boşluk bırakıldı)
        descriptionLabel = CreateTextElement(cardRect, "Description", "Özel bloğun açıklaması burada yer alır.", new Vector2(0f, -5f), new Vector2(800f, 140f), 29f, new Color(0.92f, 0.94f, 0.97f, 1f), false);
        descriptionLabel.lineSpacing = 6f;

        // 6. "Devam etmek için ekrana dokun"
        tapHintLabel = CreateTextElement(cardRect, "TapHint", "Devam etmek için ekrana dokun", new Vector2(0f, -135f), new Vector2(800f, 35f), 21f, new Color(1f, 1f, 1f, 0.65f), false);
    }

    private TextMeshProUGUI CreateTextElement(RectTransform parent, string name, string text, Vector2 anchoredPos, Vector2 size, float fontSize, Color color, bool isBold)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;

        if (customFont == null)
            customFont = Resources.Load<TMP_FontAsset>("Fonts/SweetToneTR SDF");

        if (customFont != null)
            label.font = customFont;

        return label;
    }

    private IEnumerator AnimateCardIn()
    {
        if (cardGroup == null || cardRect == null)
            yield break;

        cardGroup.alpha = 0f;
        cardRect.localScale = Vector3.one * 0.85f;

        float elapsed = 0f;
        const float duration = 0.26f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 0.85f + 0.15f * Mathf.Sin(t * Mathf.PI * 0.5f);
            cardRect.localScale = Vector3.one * scale;
            cardGroup.alpha = t;
            yield return null;
        }

        cardRect.localScale = Vector3.one;
        cardGroup.alpha = 1f;

        // Alttaki "Dokun" yazısına hafif nefes alma efekti başlat
        StartCoroutine(TapHintBreathingRoutine());
    }

    private IEnumerator AnimateCardOut()
    {
        if (cardGroup == null || cardRect == null)
            yield break;

        float elapsed = 0f;
        const float duration = 0.20f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cardGroup.alpha = 1f - t;
            cardRect.localScale = Vector3.one * (1f - 0.15f * t);
            yield return null;
        }

        cardGroup.alpha = 0f;
    }

    private IEnumerator TapHintBreathingRoutine()
    {
        if (tapHintLabel == null)
            yield break;

        while (IsIntroActive && !userDismissed)
        {
            float alpha = 0.40f + 0.45f * Mathf.PingPong(Time.time * 2f, 1f);
            Color c = tapHintLabel.color;
            c.a = alpha;
            tapHintLabel.color = c;
            yield return null;
        }
    }

    private void OnUserTappedScreen()
    {
        userDismissed = true;
    }

    private Canvas FindHudCanvas()
    {
        GameObject hudCanvasObject = GameObject.Find("HUDCanvas");
        if (hudCanvasObject != null)
            return hudCanvasObject.GetComponent<Canvas>();

        return FindAnyObjectByType<Canvas>();
    }

    /// <summary>
    /// Aşağıyı işaret eden parlak altın sarısı ok sprite'ı
    /// </summary>
    private Sprite GenerateDownArrowSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color transparent = new Color(0, 0, 0, 0);
        Color gold = new Color(1f, 0.88f, 0.22f, 1f);
        Color goldCore = new Color(1f, 0.98f, 0.80f, 1f);
        Color outline = new Color(0.12f, 0.10f, 0.05f, 0.90f);

        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center) / (size * 0.5f);
                float ny = (y - center) / (size * 0.5f);

                // Aşağı bakan ok: Gövde (ny: 0.15 to 0.70, |nx| <= 0.20)
                bool inShaft = (ny >= 0.08f && ny <= 0.65f && Mathf.Abs(nx) <= 0.18f);
                // Ok ucu (ny: -0.65 to 0.12, |nx| <= (ny - (-0.65)) * 0.65)
                bool inHead = (ny >= -0.65f && ny <= 0.12f && Mathf.Abs(nx) <= (ny - (-0.65f)) * 0.68f);

                bool inShaftOutline = (ny >= 0.04f && ny <= 0.69f && Mathf.Abs(nx) <= 0.22f);
                bool inHeadOutline = (ny >= -0.70f && ny <= 0.16f && Mathf.Abs(nx) <= (ny - (-0.70f)) * 0.74f);

                if (inShaft || inHead)
                {
                    float core = 1f - Mathf.Clamp01(Mathf.Abs(nx) / 0.18f);
                    tex.SetPixel(x, y, Color.Lerp(gold, goldCore, core * 0.5f));
                }
                else if (inShaftOutline || inHeadOutline)
                {
                    tex.SetPixel(x, y, outline);
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.2f), 128f);
    }

    /// <summary>
    /// Şık yuvarlatılmış dikdörtgen 9-Slice kart tabanı (İnce altın konturlu)
    /// </summary>
    private Sprite GenerateCard9SliceSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color transparent = new Color(0, 0, 0, 0);
        Color body = Color.white;
        Color rim = new Color(1f, 0.85f, 0.35f, 0.95f);

        int cornerR = 14;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = x < cornerR ? cornerR : (x >= size - cornerR ? size - cornerR - 1 : x);
                int cy = y < cornerR ? cornerR : (y >= size - cornerR ? size - cornerR - 1 : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > cornerR)
                {
                    tex.SetPixel(x, y, transparent);
                }
                else if (dist > cornerR - 2.5f)
                {
                    tex.SetPixel(x, y, rim);
                }
                else
                {
                    tex.SetPixel(x, y, body);
                }
            }
        }

        tex.Apply();
        Vector4 border = new Vector4(cornerR, cornerR, cornerR, cornerR);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (worldDimObject != null)
        {
            Destroy(worldDimObject);
            worldDimObject = null;
        }

        if (arrowPointerObject != null)
        {
            Destroy(arrowPointerObject);
            arrowPointerObject = null;
        }

        if (worldDimSprite != null && worldDimSprite.texture != null)
        {
            Destroy(worldDimSprite.texture);
            Destroy(worldDimSprite);
        }

        if (arrowPointerSprite != null && arrowPointerSprite.texture != null)
        {
            Destroy(arrowPointerSprite.texture);
            Destroy(arrowPointerSprite);
        }
    }
}
