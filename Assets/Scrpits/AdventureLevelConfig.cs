using UnityEngine;

[System.Serializable]
public class AdventureLevelOverrides
{
    [Header("Hamle Ekonomisi")]
    [InspectorName("Hamle Limitini Geçersiz Kıl")]
    public bool overrideMoveLimit;
    [InspectorName("Hamle Limiti")]
    [Min(1)] public int moveLimit = 25;

    [Header("Tahta Baskısı")]
    [InspectorName("Boşluk Şansını Geçersiz Kıl")]
    public bool overrideBaseGapChance;
    [InspectorName("Temel Boşluk Şansı")]
    [Range(0f, 1f)] public float baseGapChance = 0.3f;
    [InspectorName("Büyük Blok Şansını Geçersiz Kıl")]
    public bool overrideLargeBlockChance;
    [InspectorName("Büyük Blok Şansı")]
    [Range(0f, 1f)] public float largeBlockChance = 0.12f;

    [Header("Engel Şansları")]
    [InspectorName("Buz Şansını Geçersiz Kıl")]
    public bool overrideFrozenChance;
    [InspectorName("Buzlu Blok Şansı")]
    [Range(0f, 1f)] public float frozenBlockChance = 0f;
    [InspectorName("Kaya Şansını Geçersiz Kıl")]
    public bool overrideRockChance;
    [InspectorName("Kaya Blok Şansı")]
    [Range(0f, 1f)] public float rockBlockChance = 0f;
    [InspectorName("Zincir Şansını Geçersiz Kıl")]
    public bool overrideChainedChance;
    [InspectorName("Zincirli Blok Şansı")]
    [Range(0f, 1f)] public float chainedBlockChance = 0f;

    [Header("Özel Bloklar")]
    [InspectorName("Özel Doğum Kurallarını Geçersiz Kıl")]
    public bool overrideCustomSpawnRules;
    [InspectorName("Özel Doğum Kurallarını Kullan")]
    public bool useCustomSpawnRules;
    [InspectorName("Minimum Blok Boyutunu Geçersiz Kıl")]
    public bool overrideMinBlockSize;
    [InspectorName("Minimum Blok Boyutu")]
    [Min(1)] public int minBlockSize = 1;
    [InspectorName("Maksimum Blok Boyutunu Geçersiz Kıl")]
    public bool overrideMaxBlockSize;
    [InspectorName("Maksimum Blok Boyutu")]
    [Min(1)] public int maxBlockSize = 4;
    [InspectorName("Kesme Şansını Geçersiz Kıl")]
    public bool overrideSliceChance;
    [InspectorName("Kesme Bloku Şansı")]
    [Range(0f, 1f)] public float sliceBlockChance = 0f;
    [InspectorName("Ateş Şansını Geçersiz Kıl")]
    public bool overrideFireChance;
    [InspectorName("Ateş Bloku Şansı")]
    [Range(0f, 1f)] public float fireBlockChance = 0f;

    [Header("Sis")]
    [InspectorName("Sis Yoğunluğunu Geçersiz Kıl")]
    public bool overrideFogDensity;
    [InspectorName("Sis Yoğunluğu")]
    public FogDensity fogDensity = FogDensity.None;
    [InspectorName("Sis Kapsama Yüzdesini Geçersiz Kıl")]
    public bool overrideFogCoveragePercent;
    [InspectorName("Sis Kapsama Yüzdesi")]
    [Range(0f, 1f)] public float fogCoveragePercent = 0f;
    [InspectorName("Sis Başlangıç Satırını Geçersiz Kıl")]
    public bool overrideFogStartingRow;
    [InspectorName("Sis Başlangıç Satırı")]
    public int fogStartingRow = -1;
}

[CreateAssetMenu(fileName = "AdventureLevelConfig", menuName = "ARXON/Adventure/Level Config")]
public class AdventureLevelConfig : ScriptableObject
{
    [Header("Kimlik")]
    [InspectorName("Seviye Numarası")]
    [Min(1)] public int levelNumber = 1;
    [InspectorName("Görünen Ad")]
    public string displayName = "Adventure Level";
    [InspectorName("Tasarımcı Notları")]
    [TextArea(2, 4)] public string designerNotes;

    [Header("Temel Tasarım")]
    [InspectorName("Zorluk")]
    [Tooltip("Bölümün genel zorluk beklentisi.")]
    public DifficultyTier difficulty = DifficultyTier.Easy;
    [InspectorName("Baskı Türü")]
    [Tooltip("Bölümün ana gerilim kaynağı. Jeneratörü hamle baskısı, yoğunluk, kombo oyunu veya kontrollü kaosa yönlendirir.")]
    public PressureType pressure = PressureType.Relaxed;
    [InspectorName("Hedef Türü")]
    [Tooltip("Yüksek seviyeli kazanma koşulu. Bazı hedefler şu an çalışma zamanında en yakın desteklenen hedefe çevrilir.")]
    public ObjectiveType objective = ObjectiveType.ClearRows;
    [InspectorName("Engel Teması")]
    [Tooltip("Jeneratörün kullanacağı ana engel teması.")]
    public ObstacleTheme obstacleTheme = ObstacleTheme.None;
    [InspectorName("Özel Mekanik Odağı")]
    [Tooltip("Ana temanın üstüne eklenen isteğe bağlı mekanik vurgusu.")]
    public SpecialMechanicFocus specialMechanicFocus = SpecialMechanicFocus.None;

    [Header("Sis")]
    [InspectorName("Sis Yoğunluğu")]
    [Tooltip("Tahtanın üst bölümünü örten görsel sis yoğunluğu.")]
    public FogDensity fogDensity = FogDensity.None;
    [InspectorName("Sis Kapsama Yüzdesi")]
    [Tooltip("Tahtanın üstten ne kadarının sisle kaplanacağını belirler. 0.25 = üst %25.")]
    [Range(0f, 1f)] public float fogCoveragePercent = 0f;

    [Header("Hedefler")]
    [InspectorName("Hedef Satır")]
    [Tooltip("Satır temizleme odaklı içerikte doğrudan kullanılır ve desteklenmeyen hedeflerde yedek hedef olarak davranır.")]
    [Min(0)] public int targetLines = 6;
    [InspectorName("Hedef Skor")]
    [Tooltip("Skor odaklı bölüm hedefleri için kullanılır.")]
    [Min(0)] public int targetScore = 0;
    [InspectorName("Hedef Engel Sayısı")]
    [Tooltip("Gelecekteki engel yok etme hedef mantığı için ayrılmıştır.")]
    [Min(0)] public int targetObstacleCount = 0;
    [InspectorName("Hedef Kombo Sayısı")]
    [Tooltip("Gelecekteki kombo hedef mantığı için ayrılmıştır.")]
    [Min(0)] public int targetComboCount = 0;

    [Header("Jeneratör İnce Ayarı")]
    [InspectorName("Hamle Ofseti")]
    [Tooltip("Temel zorluk profili hesaplandıktan sonra birkaç hamle ekler veya çıkarır.")]
    [Range(-8, 8)] public int moveOffset = 0;
    [InspectorName("Baskı Ofseti")]
    [Tooltip("Ham olasılık kaydırıcılarını göstermeden yoğunluk ve engel baskısını ince ayarlar.")]
    [Range(-3, 3)] public int pressureOffset = 0;
    [InspectorName("Sonsuz Mod")]
    [Tooltip("Adventure bölümleri normalde sonlu kalmalıdır, ancak özel içerikler için bu seçenek açık tutulur.")]
    public bool isEndless = false;

    [Header("Gelişmiş")]
    [InspectorName("Gelişmiş Modu Aç")]
    [Tooltip("Hassas kontrol isteyen tasarımcılar için el yapımı geçersiz kılma alanlarını görünür yapar.")]
    public bool enableAdvancedMode = false;
    [InspectorName("El Yapımı Geçersiz Kılmaları Kullan")]
    [Tooltip("Açılırsa, aşağıdaki seçili ham değerler jeneratör çıktısının yerini alır.")]
    public bool useHandcraftedOverrides = false;
    [InspectorName("El Yapımı Ayarlar")]
    public AdventureLevelOverrides handcraftedOverrides = new AdventureLevelOverrides();
}
