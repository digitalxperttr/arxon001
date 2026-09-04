using UnityEngine;
using TMPro;
using System.Collections;

public enum FloatingTextStyle
{
    ScoreCyan,      // Buzul/Cyan canlı puan (+36, +72)
    ComboGold,      // Ateş/Altın alevli kombo (COMBO × 2)
    ChainMagenta,   // Zincirleme patlama (CHAIN × 3)
    BonusPurple,    // Özel bonus / Hedef ("PERFECT!", "LEVEL UP!")
    CoinYellow,     // Altın / Ödül (+50 COINS)
    WarningRed,     // Uyarı / Kilitlenme ("NO MOVES!")
    Custom          // Düz özel renk
}

[RequireComponent(typeof(TextMeshPro))]
public class FloatingText : MonoBehaviour
{
    [Header("Preset Materials")]
    [SerializeField] private Material scoreCyanMat;
    [SerializeField] private Material comboGoldMat;
    [SerializeField] private Material chainMagentaMat;
    [SerializeField] private Material bonusPurpleMat;
    [SerializeField] private Material coinYellowMat;
    [SerializeField] private Material warningRedMat;

    [Header("Font Asset")]
    [SerializeField] private TMP_FontAsset defaultFontAsset;

    [Header("Animation Settings")]
    [SerializeField] private float punchScale = 1.35f;
    [SerializeField] private float punchDuration = 0.12f;
    [SerializeField] private float settleDuration = 0.08f;
    [SerializeField] private float hangDuration = 0.35f;
    [SerializeField] private float floatDuration = 0.40f;
    [SerializeField] private float floatDistance = 1.2f;

    private static FloatingText s_defaultPrefab;
    public static void SetDefaultPrefab(FloatingText prefab) => s_defaultPrefab = prefab;

    private TextMeshPro textMesh;
    private Coroutine animRoutine;

    private void Awake()
    {
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (textMesh == null)
            textMesh = GetComponent<TextMeshPro>();
    }

    /// <summary>
    /// Evrensel Statik Çağırma: Oyunun herhangi bir yerinden tek satırda çağrılabilir.
    /// Örn: FloatingText.Spawn(pos, "+72", FloatingTextStyle.ScoreCyan);
    /// </summary>
    public static FloatingText Spawn(Vector3 worldPos, string text, FloatingTextStyle style = FloatingTextStyle.ScoreCyan, float size = 6f)
    {
        FloatingText prefab = s_defaultPrefab;
        if (prefab == null && GridManager.Instance != null)
        {
            prefab = GridManager.Instance.floatingTextPrefab;
        }

        if (prefab == null)
        {
            prefab = Resources.Load<FloatingText>("FloatingTextPrefab");
        }

        if (prefab == null)
        {
            Debug.LogWarning("[FloatingText] Spawn çağrıldı ancak prefab referansı bulunamadı!");
            return null;
        }

        FloatingText instance = Instantiate(prefab, worldPos, Quaternion.identity);
        instance.SetStyle(text, style, size);
        return instance;
    }

    /// <summary>
    /// Belirtilen hazır görsel stili uygular ve yaylanan (punch/pop) animasyonu başlatır.
    /// </summary>
    public void SetStyle(string text, FloatingTextStyle style, float size = 6f)
    {
        EnsureComponents();
        if (textMesh == null) return;

        if (defaultFontAsset != null)
            textMesh.font = defaultFontAsset;

        Material selectedMat = GetMaterialForStyle(style);
        if (selectedMat != null)
        {
            textMesh.fontSharedMaterial = selectedMat;
        }

        textMesh.text = text;
        textMesh.fontSize = size;
        textMesh.sortingOrder = 30; // Blokların ve efektlerin her zaman önünde

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(JuicePopRoutine());
    }

    /// <summary>
    /// Geriye dönük uyumluluk için: Eski kodları bozmaz.
    /// </summary>
    public void SetText(string text, Color color, float size = 4f)
    {
        EnsureComponents();
        if (textMesh == null) return;

        if (defaultFontAsset != null)
            textMesh.font = defaultFontAsset;

        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = size;
        textMesh.sortingOrder = 30;

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(JuicePopRoutine());
    }

    private Material GetMaterialForStyle(FloatingTextStyle style)
    {
        switch (style)
        {
            case FloatingTextStyle.ScoreCyan: return scoreCyanMat;
            case FloatingTextStyle.ComboGold: return comboGoldMat;
            case FloatingTextStyle.ChainMagenta: return chainMagentaMat;
            case FloatingTextStyle.BonusPurple: return bonusPurpleMat;
            case FloatingTextStyle.CoinYellow: return coinYellowMat;
            case FloatingTextStyle.WarningRed: return warningRedMat;
            default: return scoreCyanMat;
        }
    }

    private IEnumerator JuicePopRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 baseScale = Vector3.one;
        transform.localScale = Vector3.zero;

        Color originalColor = textMesh.color;
        originalColor.a = 1f;
        textMesh.color = originalColor;

        // --- FAZ 1: POP & PUNCH (Ani büyüme ve esnek oturma) ---
        float elapsed = 0f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero, baseScale * punchScale, EaseOutBack(t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            transform.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, t);
            yield return null;
        }
        transform.localScale = baseScale;

        // --- FAZ 2: HANG TIME (Okuma süresi - Çok hafif süzülme) ---
        elapsed = 0f;
        Vector3 hangStartPos = transform.position;
        Vector3 hangEndPos = hangStartPos + new Vector3(0f, 0.15f, 0f);
        while (elapsed < hangDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hangDuration);
            transform.position = Vector3.Lerp(hangStartPos, hangEndPos, t);
            yield return null;
        }

        // --- FAZ 3: FLOAT & FADEOUT (Yukarı süzülüp şeffaflaşma) ---
        elapsed = 0f;
        Vector3 floatStartPos = transform.position;
        Vector3 floatEndPos = floatStartPos + new Vector3(0f, floatDistance, 0f);
        while (elapsed < floatDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / floatDuration);
            
            // Yukarı ivmelen
            transform.position = Vector3.Lerp(floatStartPos, floatEndPos, t * t);
            
            // Şeffaflaş
            Color c = textMesh.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            textMesh.color = c;

            // Hafif küçül
            transform.localScale = Vector3.Lerp(baseScale, baseScale * 0.85f, t);

            yield return null;
        }

        Destroy(gameObject);
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}