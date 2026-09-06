using System.Collections;
using TMPro;
using UnityEngine;

public class ComboBadgeController : MonoBehaviour
{
    public static ComboBadgeController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform badgeRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI badgeText;

    [Header("Font & Materials")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Material goldMat;
    [SerializeField] private Material magentaMat;

    [Header("Idle Pulse Settings")]
    [SerializeField] private float idlePulseMin = 0.98f;
    [SerializeField] private float idlePulseMax = 1.07f;
    [SerializeField] private float idlePulseSpeed = 3.5f;

    private Coroutine activeAnimRoutine;
    private Coroutine idlePulseRoutine;
    private RectTransform puanTabloRect;
    private int currentComboLevel = 0;

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

        EnsureBadgeSetup();
    }

    private void OnEnable()
    {
        ScoreManager.OnComboReset += OnComboResetReceived;
    }

    private void OnDisable()
    {
        ScoreManager.OnComboReset -= OnComboResetReceived;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Uçan FloatingText'in hedef alacağı dünya koordinatını (taş panel ile grid arasındaki tam orta nokta) döndürür.
    /// </summary>
    public Vector3 GetDockingWorldPosition()
    {
        EnsureBadgeSetup();
        UpdateBadgePosition();

        Camera cam = Camera.main;
        if (badgeRoot != null && cam != null)
        {
            Vector3 screenPos = badgeRoot.position;
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        }

        return new Vector3(3.5f, 9.65f, 0f);
    }

    /// <summary>
    /// Uçan kombo hedefe ulaştığında veya kombo arttığında çağrılır.
    /// </summary>
    public void Dock(int comboCount)
    {
        if (comboCount < 2) return;
        EnsureBadgeSetup();
        if (badgeRoot == null || badgeText == null) return;
        UpdateBadgePosition();

        currentComboLevel = comboCount;
        badgeText.text = $"COMBO \u00D7 {comboCount}";

        // Kombo seviyesine göre renk/materyal evrimi
        if (comboCount >= 4 && magentaMat != null)
        {
            if (fontAsset != null && magentaMat.mainTexture != fontAsset.atlasTexture)
                magentaMat.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
            badgeText.fontSharedMaterial = magentaMat;
        }
        else if (goldMat != null)
        {
            if (fontAsset != null && goldMat.mainTexture != fontAsset.atlasTexture)
                goldMat.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
            badgeText.fontSharedMaterial = goldMat;
        }

        if (activeAnimRoutine != null)
            StopCoroutine(activeAnimRoutine);

        activeAnimRoutine = StartCoroutine(DockPunchRoutine());
    }

    /// <summary>
    /// Seri bozulduğunda çağrılır; rozet tatlı bir süzülme ve fadeout ile kaybolur.
    /// </summary>
    public void BreakCombo()
    {
        if (currentComboLevel <= 0 && (canvasGroup == null || canvasGroup.alpha <= 0.01f))
            return;

        currentComboLevel = 0;

        if (idlePulseRoutine != null)
        {
            StopCoroutine(idlePulseRoutine);
            idlePulseRoutine = null;
        }

        if (activeAnimRoutine != null)
            StopCoroutine(activeAnimRoutine);

        activeAnimRoutine = StartCoroutine(DissolveBreakRoutine());
    }

    private void OnComboResetReceived()
    {
        BreakCombo();
    }

    private IEnumerator DockPunchRoutine()
    {
        if (idlePulseRoutine != null)
        {
            StopCoroutine(idlePulseRoutine);
            idlePulseRoutine = null;
        }

        canvasGroup.alpha = 1f;
        badgeRoot.gameObject.SetActive(true);

        // 1. DOCK PUNCH: 1.0 -> 1.38 -> 1.0 yaylanma
        const float punchDur = 0.16f;
        float elapsed = 0f;
        Vector3 baseScale = Vector3.one;

        while (elapsed < punchDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / punchDur);
            // Büyüme ve yaylanma
            float s = Mathf.Sin(t * Mathf.PI) * 0.38f + 1.0f;
            badgeRoot.localScale = baseScale * s;
            yield return null;
        }

        badgeRoot.localScale = baseScale;

        // 2. Ritmik nabız moduna geç
        idlePulseRoutine = StartCoroutine(IdlePulseLoopRoutine());
        activeAnimRoutine = null;
    }

    private IEnumerator IdlePulseLoopRoutine()
    {
        Vector3 baseScale = Vector3.one;
        float timer = 0f;

        while (true)
        {
            timer += Time.deltaTime * idlePulseSpeed;
            // Sakin nefes alma (1.0 <-> 1.07)
            float s = Mathf.Lerp(idlePulseMin, idlePulseMax, (Mathf.Sin(timer) + 1f) * 0.5f);
            badgeRoot.localScale = baseScale * s;
            yield return null;
        }
    }

    private IEnumerator DissolveBreakRoutine()
    {
        Vector2 startPos = badgeRoot.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, -25f); // Hafif aşağı düşüş
        float elapsed = 0f;
        const float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            badgeRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            badgeRoot.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.88f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        badgeRoot.anchoredPosition = startPos;
        badgeRoot.localScale = Vector3.one;
        badgeRoot.gameObject.SetActive(false);
        activeAnimRoutine = null;
    }

    private void EnsureBadgeSetup()
    {
        if (badgeRoot != null && badgeText != null)
            return;

        // 1. Font ve Materyalleri yükle (Resources güvencesi)
        if (fontAsset == null)
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts/LilitaOne-Regular SDF");
        if (fontAsset == null)
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts/SweetToneTR SDF");

        if (goldMat == null)
            goldMat = Resources.Load<Material>("Materials/Presets/LilitaOne_Combo_Gold");
        if (magentaMat == null)
            magentaMat = Resources.Load<Material>("Materials/Presets/LilitaOne_Chain_Magenta");

        // Atlas uyuşmazlığını (Kiril/bozuk glif sorununu) kökten engelleyen Texture senkronizasyonu
        if (fontAsset != null && goldMat != null && goldMat.mainTexture != fontAsset.atlasTexture)
            goldMat.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
        if (fontAsset != null && magentaMat != null && magentaMat.mainTexture != fontAsset.atlasTexture)
            magentaMat.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);

        // 2. HUD Canvas ve puan_tablo'yu bul
        GameObject tablo = GameObject.Find("puan_tablo");
        if (tablo != null)
            puanTabloRect = tablo.GetComponent<RectTransform>();

        Transform parentTransform = tablo != null ? tablo.transform : null;
        if (parentTransform == null)
        {
            GameObject hud = GameObject.Find("HUDCanvas");
            if (hud != null) parentTransform = hud.transform;
        }

        if (parentTransform == null)
            return;

        // 3. Rozet GameObject'ini oluştur
        Transform existing = parentTransform.Find("ComboBadge");
        GameObject badgeObj = existing != null ? existing.gameObject : new GameObject("ComboBadge", typeof(RectTransform), typeof(CanvasGroup));
        badgeRoot = badgeObj.GetComponent<RectTransform>();
        badgeRoot.SetParent(parentTransform, false);

        canvasGroup = badgeObj.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f; // Başlangıçta gizli

        // 4. TextMeshProUGUI oluştur
        Transform textChild = badgeRoot.Find("ComboBadgeText");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("ComboBadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(badgeRoot, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        badgeText = textObj.GetComponent<TextMeshProUGUI>();
        badgeText.text = "COMBO \u00D7 2";
        badgeText.fontSize = 54f;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.raycastTarget = false;
        badgeText.textWrappingMode = TextWrappingModes.NoWrap;

        if (fontAsset != null)
            badgeText.font = fontAsset;
        if (goldMat != null)
            badgeText.fontSharedMaterial = goldMat;

        UpdateBadgePosition();

        badgeObj.SetActive(false);
    }

    /// <summary>
    /// Rozeti üstteki taş panel (puan_tablo) ile aşağıdaki grid'in tam dikey ortasına yerleştirir.
    /// Tüm cihaz çözünürlükleri ve ekran oranlarında matematiksel olarak mükemmel ortalama sağlar.
    /// </summary>
    public void UpdateBadgePosition()
    {
        if (badgeRoot == null) return;

        GameObject tablo = GameObject.Find("puan_tablo");
        Camera cam = Camera.main;

        float targetLocalY = -330f; // 1080x1920 referans tuvali için hesaplanmış ideal orta nokta

        if (tablo != null && cam != null)
        {
            RectTransform tabloRt = tablo.GetComponent<RectTransform>();
            UnityEngine.UI.Image img = tablo.GetComponent<UnityEngine.UI.Image>();

            // puan_tablo merkezi (pivot (0.5, 1.0) olduğundan -height / 2)
            float tabloCenterY = -tabloRt.rect.height * 0.5f;

            // Görsel taş panelin alt sınırı (preserveAspect hesaba katılarak)
            float stoneVisualBottom = -tabloRt.rect.height;
            if (img != null && img.sprite != null && img.preserveAspect)
            {
                float spriteAspect = img.sprite.rect.width / img.sprite.rect.height;
                float renderedHeight = tabloRt.rect.width / spriteAspect;
                stoneVisualBottom = tabloCenterY - (renderedHeight * 0.5f);
            }
            else
            {
                stoneVisualBottom = -tabloRt.rect.height;
            }

            // Grid'in en üst satırının (row 9) tepe dünya koordinatı
            var gm = GridManager.Instance != null ? GridManager.Instance : Object.FindObjectOfType<GridManager>();
            float gridTopWorldY = 9.5f;
            if (gm != null)
            {
                float topRowCenterY = (gm.height - 1) * gm.cellSize;
                gridTopWorldY = topRowCenterY + (gm.cellSize * 0.5f);
            }

            Vector3 gridTopScreen = cam.WorldToScreenPoint(new Vector3(3.5f, gridTopWorldY, 0f));
            Vector2 gridTopLocal;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tabloRt, gridTopScreen, null, out gridTopLocal))
            {
                targetLocalY = (stoneVisualBottom + gridTopLocal.y) * 0.5f;
            }
        }

        badgeRoot.anchorMin = new Vector2(0.5f, 1f);
        badgeRoot.anchorMax = new Vector2(0.5f, 1f);
        badgeRoot.pivot = new Vector2(0.5f, 0.5f);
        badgeRoot.sizeDelta = new Vector2(500f, 70f);
        badgeRoot.anchoredPosition = new Vector2(0f, targetLocalY);
    }
}
