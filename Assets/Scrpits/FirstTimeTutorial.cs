using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FirstTimeTutorial : MonoBehaviour
{
    private const string TutorialCompletedKey = "TutorialCompleted";

    public static FirstTimeTutorial Instance { get; private set; }

    [Header("UI & Visual Settings")]
    [SerializeField] private TMP_FontAsset tutorialFont;
    [SerializeField] private Sprite customHandSprite;
    [SerializeField] private float startDelay = 1.0f;
    [SerializeField] [Range(0.5f, 0.95f)] private float dimAlpha = 0.84f; // Etrafın kararması belirgin (%84)
    [SerializeField] private float dimFadeDuration = 0.35f;
    [SerializeField] private float handFadeDuration = 0.28f;
    [SerializeField] private float handSlideDuration = 0.75f;
    [SerializeField] private float successVisibleDuration = 1.2f;

    private GridManager grid;
    private Block activeBlock;
    private int targetX = 5;
    private int targetY = 1;
    private bool tutorialRunning;
    private bool tutorialCompleted;
    private bool userStartedDragging;
    private bool successShown;
    private bool boardBuilt;

    // Dünya uzayında tahtayı ve arkaplanı karartan katman (SortingOrder = 18)
    // Böylece diğer bloklar kararır, sadece activeBlock (SortingOrder = 24) aydınlık kalır!
    private GameObject worldDimObject;
    private SpriteRenderer worldDimRenderer;
    private Sprite worldDimSprite;

    // HUD Canvas Tebrikler Popup
    private RectTransform tutorialRoot;
    private CanvasGroup successGroup;
    private RectTransform successRect;
    private TextMeshProUGUI successText;

    // El Göstergesi (Büyük El ve Uzatılmış İşaret Parmağı)
    private GameObject handObject;
    private SpriteRenderer handRenderer;
    private Sprite generatedHandSprite;
    private Coroutine tutorialSequenceRoutine;
    private Coroutine handLoopRoutine;

    private int originalBlockSortingOrder = 10;
    private SpriteRenderer activeBlockRenderer;

    public bool IsRunning => tutorialRunning && !tutorialCompleted;

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

        tutorialCompleted = PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
        if (tutorialCompleted)
        {
            tutorialRunning = false;
            enabled = false;
        }

        if (tutorialFont == null)
        {
            tutorialFont = Resources.Load<TMP_FontAsset>("Fonts/SweetToneTR SDF");
#if UNITY_EDITOR
            if (tutorialFont == null)
            {
                tutorialFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ART/TextMesh Pro/Fonts/SweetToneTR SDF.asset");
            }
#endif
        }
    }

    private void Start()
    {
        tutorialCompleted = PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
        if (!ShouldRunTutorial())
            return;

        if (tutorialSequenceRoutine != null)
            StopCoroutine(tutorialSequenceRoutine);

        tutorialSequenceRoutine = StartCoroutine(TutorialSequenceRoutine());
    }

    public bool TryBuildInitialBoard(GridManager gridManager)
    {
        tutorialCompleted = PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
        if (!ShouldRunTutorial() || gridManager == null || boardBuilt)
            return false;

        grid = gridManager;
        tutorialRunning = true;
        boardBuilt = true;

        SpawnTutorialBoard(gridManager);
        return true;
    }

    private bool ShouldRunTutorial()
    {
        if (tutorialCompleted || PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1)
            return false;

        return ProgressManager.Instance == null ||
            ProgressManager.Instance.currentSelectedLevel == null;
    }

    private void SpawnTutorialBoard(GridManager gridManager)
    {
        // 8 sütunlu grid için mükemmel başlangıç senaryosu:
        // Alt Satır (y = 0):
        // x=0..4: 5 birimlik blok
        // x=5: BOŞLUK (1 birim)
        // x=6..7: 2 birimlik blok
        SpawnBlock(gridManager, 0, 0, 5, 2);
        SpawnBlock(gridManager, 6, 0, 2, 0);

        // İkinci Satır (y = 1):
        // x=0..1: 2 birimlik blok
        // x=2..3: 2 birimlik blok
        // x=4: 1 birimlik blok (HEDEF BLOK!)
        // x=5: BOŞLUK
        // x=6..7: 2 birimlik blok
        SpawnBlock(gridManager, 0, 1, 2, 1);
        SpawnBlock(gridManager, 2, 1, 2, 3);
        activeBlock = SpawnBlock(gridManager, 4, 1, 1, 0);
        SpawnBlock(gridManager, 6, 1, 2, 4);

        targetX = 5;
        targetY = 1;

        gridManager.RebuildGridMemory();
    }

    private Block SpawnBlock(GridManager gridManager, int x, int y, int width, int normalGemIndex)
    {
        GridManager.BlockData data = gridManager.CreateSingleCellBlockData(
            x,
            BlockType.Normal,
            normalGemIndex
        );
        data.width = width;

        return gridManager.SpawnConfiguredBlock(data, y);
    }

    private IEnumerator TutorialSequenceRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        if (!IsRunning || userStartedDragging)
            yield break;

        // Hedef blok bulunamadıysa HintSolver ile ara
        if (activeBlock == null)
        {
            if (grid == null)
                grid = GridManager.Instance != null ? GridManager.Instance : FindAnyObjectByType<GridManager>();

            if (grid != null && HintSolver.TryFindBestHint(grid, out HintMove hint))
            {
                activeBlock = hint.block;
                targetX = hint.toX;
                targetY = hint.fromY;
            }
        }

        if (activeBlock == null)
            yield break;

        // 1. Hedef bloğu karartmanın önüne çıkar (SortingOrder = 24 ile SPOTLIGHT)
        activeBlockRenderer = activeBlock.GetComponent<SpriteRenderer>();
        int targetLayerID = 0;
        if (activeBlockRenderer != null)
        {
            originalBlockSortingOrder = activeBlockRenderer.sortingOrder;
            targetLayerID = activeBlockRenderer.sortingLayerID;
            activeBlockRenderer.sortingOrder = 24;
        }
        activeBlock.SetHighlight(true);

        // 2. Dünya uzayında derin karartmayı aç (SortingOrder = 18)
        EnsureWorldDim(targetLayerID);
        yield return FadeWorldDim(0f, dimAlpha, dimFadeDuration);

        // 3. Büyük El animasyon döngüsünü başlat (SortingOrder = 30)
        if (!userStartedDragging)
        {
            EnsureHandObject(targetLayerID);
            handLoopRoutine = StartCoroutine(HandDragLoopRoutine());
        }
    }

    private void EnsureWorldDim(int layerID)
    {
        if (worldDimObject != null)
            return;

        worldDimObject = new GameObject("TutorialWorldDim");
        worldDimRenderer = worldDimObject.AddComponent<SpriteRenderer>();
        worldDimRenderer.sortingLayerID = layerID;
        worldDimRenderer.sortingOrder = 18; // Diğer blokların (10) üstünde, hedef bloğun (24) altında!

        if (worldDimSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply();
            worldDimSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        worldDimRenderer.sprite = worldDimSprite;
        worldDimRenderer.color = new Color(0.01f, 0.02f, 0.04f, 0f);

        // Kameranın tüm görüş alanını fazlasıyla kaplayacak boyut
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

    private void EnsureHandObject(int layerID)
    {
        if (handObject != null)
            return;

        handObject = new GameObject("TutorialHandPointer");
        handRenderer = handObject.AddComponent<SpriteRenderer>();
        handRenderer.sortingLayerID = layerID;
        handRenderer.sortingOrder = 30; // Bloğun da üstünde parlar

        if (customHandSprite == null)
        {
            customHandSprite = Resources.Load<Sprite>("UI/tut_hand");
#if UNITY_EDITOR
            if (customHandSprite == null)
            {
                customHandSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ART/UI/tut_hand.png");
            }
#endif
        }

        if (customHandSprite != null)
        {
            handRenderer.sprite = customHandSprite;
        }
        else if (generatedHandSprite == null)
        {
            generatedHandSprite = GenerateProceduralHandSprite();
            handRenderer.sprite = generatedHandSprite;
        }

        handObject.SetActive(false);
    }

    private IEnumerator HandDragLoopRoutine()
    {
        if (handObject == null || activeBlock == null)
            yield break;

        handObject.SetActive(true);

        bool isCustomHand = (customHandSprite != null || (handRenderer.sprite != null && handRenderer.sprite.name.Contains("tut_hand")));

        // tut_hand.png görselinde sarı işaret parmağının ucunu bloğun merkezine oturtan ofset
        Vector3 fingerTipOffset = isCustomHand
            ? new Vector3(0.38f, -0.76f, -0.2f)
            : new Vector3(0.05f, -0.05f, -0.2f);

        float baseScale = isCustomHand ? 0.52f : 1.30f;
        float pressScale = baseScale * 0.88f;

        const float minIdleAlpha = 0.22f; // Tamamen kaybolmaz, %20-25 aralığında yarı saydam kalır

        Vector3 startPos = activeBlock.transform.position + fingerTipOffset;
        Vector3 endPos = new Vector3(targetX + (activeBlock.width - 1) * 0.5f, targetY, activeBlock.transform.position.z) + fingerTipOffset;

        handObject.transform.position = startPos;
        handObject.transform.localScale = Vector3.one * baseScale;
        SetHandAlpha(0f);

        // İlk açılışta 0'dan 100'e yumuşakça belirme
        float introElapsed = 0f;
        while (introElapsed < handFadeDuration)
        {
            if (userStartedDragging || !IsRunning) yield break;
            introElapsed += Time.deltaTime;
            SetHandAlpha(Mathf.Clamp01(introElapsed / handFadeDuration));
            yield return null;
        }
        SetHandAlpha(1f);

        while (IsRunning && !userStartedDragging)
        {
            if (activeBlock == null || !activeBlock.gameObject.activeInHierarchy)
                yield break;

            startPos = activeBlock.transform.position + fingerTipOffset;
            endPos = new Vector3(targetX + (activeBlock.width - 1) * 0.5f, targetY, activeBlock.transform.position.z) + fingerTipOffset;

            // 1. Bloğa basma (Press down, %100 alfa)
            float elapsed = 0f;
            const float pressDuration = 0.16f;
            while (elapsed < pressDuration)
            {
                if (userStartedDragging || !IsRunning) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / pressDuration;
                handObject.transform.localScale = Vector3.one * Mathf.Lerp(baseScale, pressScale, t);
                yield return null;
            }

            // 2. Hedefe doğru tam görünür sürükleme (Slide to target, %100 alfa)
            elapsed = 0f;
            while (elapsed < handSlideDuration)
            {
                if (userStartedDragging || !IsRunning) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / handSlideDuration));
                handObject.transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
            handObject.transform.position = endPos;

            // 3. Parmak kalkar ve %22 alfaya (yarı saydama) iner
            elapsed = 0f;
            const float liftDuration = 0.18f;
            while (elapsed < liftDuration)
            {
                if (userStartedDragging || !IsRunning) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / liftDuration;
                handObject.transform.localScale = Vector3.one * Mathf.Lerp(pressScale, baseScale, t);
                SetHandAlpha(Mathf.Lerp(1f, minIdleAlpha, t));
                yield return null;
            }
            SetHandAlpha(minIdleAlpha);

            // 4. Yarı saydam halde başladığı noktaya geri süzülür
            elapsed = 0f;
            const float returnDuration = 0.35f;
            while (elapsed < returnDuration)
            {
                if (userStartedDragging || !IsRunning) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
                handObject.transform.position = Vector3.Lerp(endPos, startPos, t);
                yield return null;
            }
            handObject.transform.position = startPos;

            // 5. Başlangıçta tekrar %100 alfaya parlar
            elapsed = 0f;
            const float restoreDuration = 0.15f;
            while (elapsed < restoreDuration)
            {
                if (userStartedDragging || !IsRunning) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / restoreDuration;
                SetHandAlpha(Mathf.Lerp(minIdleAlpha, 1f, t));
                yield return null;
            }
            SetHandAlpha(1f);

            // Kısa duraklama ve döngü tekrarı
            yield return new WaitForSeconds(0.12f);
        }
    }

    private void SetHandAlpha(float alpha)
    {
        if (handRenderer == null) return;
        Color c = handRenderer.color;
        c.a = alpha;
        handRenderer.color = c;
    }

    private void StopHandAnimation()
    {
        if (handLoopRoutine != null)
        {
            StopCoroutine(handLoopRoutine);
            handLoopRoutine = null;
        }

        if (handObject != null)
        {
            Destroy(handObject);
            handObject = null;
        }
    }

    public bool CanSelectBlock(Block block)
    {
        if (!IsRunning)
            return true;

        return block != null && block == activeBlock;
    }

    public bool IsCorrectPlacement(Block block, int snappedX)
    {
        if (!IsRunning)
            return true;

        return block != null &&
            block == activeBlock &&
            snappedX == targetX;
    }

    public bool ShouldUsePreviewDrag(Block block)
    {
        return false;
    }

    public void ResetActivePreviewBlock()
    {
    }

    public bool TryCommitActivePreviewBlock(Vector3 releasePosition)
    {
        return false;
    }

    public void NotifyDragStarted()
    {
        if (!IsRunning)
            return;

        userStartedDragging = true;
        StopHandAnimation();
    }

    public IEnumerator PlaySuccessBeforePushUp()
    {
        if (!IsRunning || successShown)
            yield break;

        successShown = true;
        StopHandAnimation();

        // 1. Karartmayı hızla kaldır
        if (worldDimRenderer != null)
            StartCoroutine(FadeWorldDim(worldDimRenderer.color.a, 0f, 0.20f));

        // 2. Bloğun parlaklık ve katmanını sıfırla
        if (activeBlockRenderer != null)
        {
            activeBlockRenderer.sortingOrder = originalBlockSortingOrder;
            if (activeBlock != null)
                activeBlock.SetHighlight(false);
        }

        // 3. "Tebrikler!" Zoom-In Animasyonunu oynat
        yield return PlayCongratulationsZoomIn();
    }

    private IEnumerator PlayCongratulationsZoomIn()
    {
        EnsureTutorialUI();
        if (tutorialRoot != null)
        {
            tutorialRoot.gameObject.SetActive(true);
            tutorialRoot.SetAsLastSibling(); // En öne çıkar
        }

        if (successGroup == null || successRect == null)
            yield break;

        successRect.anchoredPosition = new Vector2(0f, 0f); // Ekranın tam ortası
        successRect.localScale = Vector3.one * 0.2f;
        successGroup.alpha = 0f;

        // 1. Zoom In (Punch scale 0.2 -> 1.18 -> 1.0)
        float elapsed = 0f;
        const float punchDuration = 0.32f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            float scale = 0.2f + 0.98f * Mathf.Sin(t * Mathf.PI * 0.65f);
            successRect.localScale = Vector3.one * scale;
            successGroup.alpha = Mathf.Clamp01(t * 2.8f);
            yield return null;
        }

        successRect.localScale = Vector3.one;
        successGroup.alpha = 1f;

        // 2. Ekranda parlasın
        yield return new WaitForSeconds(successVisibleDuration);

        // 3. Yavaşça yukarı süzülerek kaybolsun
        elapsed = 0f;
        const float fadeDuration = 0.32f;
        Vector2 startPos = successRect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, 60f);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            successGroup.alpha = 1f - t;
            successRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        successGroup.alpha = 0f;
    }

    public IEnumerator CompleteAfterPushUp()
    {
        if (!IsRunning)
            yield break;

        tutorialCompleted = true;
        tutorialRunning = false;
        PlayerPrefs.SetInt(TutorialCompletedKey, 1);
        PlayerPrefs.Save();

        HideTutorialUI();
    }

    private void EnsureTutorialUI()
    {
        if (tutorialRoot != null)
            return;

        Canvas hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
            return;

        tutorialRoot = CreateRoot(hudCanvas.transform);
        tutorialRoot.SetAsLastSibling();
        successGroup = CreateCongratulationsText(tutorialRoot, out successRect, out successText);

        successGroup.alpha = 0f;
        tutorialRoot.gameObject.SetActive(false);
    }

    private RectTransform CreateRoot(Transform parent)
    {
        GameObject root = new GameObject("TutorialRoot", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        return rect;
    }

    private CanvasGroup CreateCongratulationsText(RectTransform parent, out RectTransform rect, out TextMeshProUGUI label)
    {
        GameObject textObject = new GameObject("TutorialSuccessText", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1000f, 220f);
        rect.anchoredPosition = new Vector2(0f, 0f);

        CanvasGroup group = textObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = "Tebrikler!";
        label.fontSize = 76f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.88f, 0.22f, 1f); // Parlak Altın Sarısı
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        if (tutorialFont == null)
            tutorialFont = Resources.Load<TMP_FontAsset>("Fonts/SweetToneTR SDF");

        if (tutorialFont != null)
            label.font = tutorialFont;

        return group;
    }

    private Canvas FindHudCanvas()
    {
        GameObject hudCanvasObject = GameObject.Find("HUDCanvas");
        if (hudCanvasObject != null)
            return hudCanvasObject.GetComponent<Canvas>();

        return FindAnyObjectByType<Canvas>();
    }

    private void HideTutorialUI()
    {
        StopHandAnimation();

        if (worldDimObject != null)
        {
            Destroy(worldDimObject);
            worldDimObject = null;
        }

        if (successGroup != null)
            successGroup.alpha = 0f;

        if (tutorialRoot != null)
            tutorialRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Klasik oyun standardı: Avuç içi, kapalı parmakları, başparmağı ve 
    /// ileri uzatılmış işaret parmağı olan büyük beyaz el (Casual Hand Pointer).
    /// </summary>
    private Sprite GenerateProceduralHandSprite()
    {
        int size = 160;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color transparent = new Color(0, 0, 0, 0);
        Color gloveWhite = new Color(0.98f, 0.98f, 1.0f, 1f);
        Color shadingWhite = new Color(0.90f, 0.92f, 0.96f, 1f);
        Color outlineColor = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        Color shadowColor = new Color(0f, 0f, 0f, 0.28f);

        // Geometri Tanımları:
        // Parmak ucu (işaret parmağı tepe noktası): (38, 138)
        Vector2 tip = new Vector2(38f, 138f);
        Vector2 knuckle = new Vector2(72f, 78f);
        float indexRadius = 14f;

        // Avuç merkezi
        Vector2 palm = new Vector2(92f, 62f);
        float palmRadius = 32f;

        // Kıvrılmış parmaklar (orta, yüzük, serçe)
        Vector2 curledCenter = new Vector2(106f, 72f);
        float curledRadius = 24f;

        // Başparmak (sol yanda kıvrık)
        Vector2 thumbCenter = new Vector2(66f, 44f);
        float thumbRadius = 16f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);

                // 1. Uzatılmış İşaret Parmağı kapsülü
                Vector2 pa = p - knuckle;
                Vector2 ba = tip - knuckle;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                float distIndex = (pa - ba * h).magnitude - indexRadius;

                // 2. Avuç içi
                float distPalm = (p - palm).magnitude - palmRadius;

                // 3. Kıvrık parmaklar
                float distCurled = (p - curledCenter).magnitude - curledRadius;

                // 4. Başparmak
                float distThumb = (p - thumbCenter).magnitude - thumbRadius;

                float handDist = Mathf.Min(distIndex, Mathf.Min(distPalm, Mathf.Min(distCurled, distThumb)));

                // Yumuşak gölge mesafesi (sağ-aşağı 4px ofset)
                Vector2 pSh = new Vector2(x - 2f, y + 5f);
                Vector2 paSh = pSh - knuckle;
                float hSh = Mathf.Clamp01(Vector2.Dot(paSh, ba) / Vector2.Dot(ba, ba));
                float dIndexSh = (paSh - ba * hSh).magnitude - indexRadius;
                float dPalmSh = (pSh - palm).magnitude - palmRadius;
                float dCurledSh = (pSh - curledCenter).magnitude - curledRadius;
                float dThumbSh = (pSh - thumbCenter).magnitude - thumbRadius;
                float shadowDist = Mathf.Min(dIndexSh, Mathf.Min(dPalmSh, Mathf.Min(dCurledSh, dThumbSh)));

                if (handDist <= 0f)
                {
                    // Dış kontur çizgisi (1.8px)
                    if (handDist > -2.2f)
                    {
                        texture.SetPixel(x, y, outlineColor);
                    }
                    else
                    {
                        // Üstten aydınlık, alttan hafif gölgeli el dolgusu
                        float light = Mathf.Clamp01((y - 30f) / 100f);
                        Color fill = Color.Lerp(shadingWhite, gloveWhite, light);
                        texture.SetPixel(x, y, fill);
                    }
                }
                else if (handDist <= 1.5f)
                {
                    // Antialiasing kontur kenarı
                    float edge = Mathf.Clamp01(1f - handDist / 1.5f);
                    Color c = outlineColor;
                    c.a *= edge;
                    texture.SetPixel(x, y, c);
                }
                else if (shadowDist <= 0f)
                {
                    // Yumuşak el gölgesi
                    texture.SetPixel(x, y, shadowColor);
                }
                else
                {
                    texture.SetPixel(x, y, transparent);
                }
            }
        }

        texture.Apply();
        // Pivot tam parmak ucuna (38 / 160 = 0.24, 138 / 160 = 0.86)
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.24f, 0.86f), 128f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopHandAnimation();

        if (worldDimObject != null)
        {
            Destroy(worldDimObject);
            worldDimObject = null;
        }

        if (worldDimSprite != null && worldDimSprite.texture != null)
        {
            Destroy(worldDimSprite.texture);
            Destroy(worldDimSprite);
        }

        if (generatedHandSprite != null && generatedHandSprite.texture != null)
        {
            Destroy(generatedHandSprite.texture);
            Destroy(generatedHandSprite);
        }
    }
}
