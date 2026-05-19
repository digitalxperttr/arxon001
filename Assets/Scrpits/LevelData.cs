using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "ARXON/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Bölüm Bilgileri")]
    public int levelNumber;
   
    [Header("Bölüm Hedefleri")]
    public int targetScore = 0;       // 0 ise bu hedeften muaf demektir
    public int targetLines = 0;       // 0 ise bu hedeften muaf demektir
    
    [Header("Kısıtlamalar")]
    public int moveLimit = 30;        // Oyuncunun kaç hamle hakkı var?
    public bool isEndless = false;    // True ise klasik mod gibi sonsuz oynanır
    
    [Header("Zorluk Ayarları")]
    [Range(0f, 1f)] public float baseGapChance = 0.4f;     // Boşluk çıkma ihtimali
    [Range(0f, 1f)] public float largeBlockChance = 0.1f;  // 4'lü dev blok çıkma ihtimali
    [Range(0f, 1f)] public float frozenBlockChance = 0f;   // Buzlu blok çıkma ihtimali
    [Range(0f, 1f)] public float rockBlockChance = 0.0f;     // YENİ: Kaya blok çıkma ihtimali
    [Range(0f, 1f)] public float chainedBlockChance = 0f;  // YENİ: Zincirli blok çıkma ihtimali

    [Header("Sis (Fog) Ayarları")]
    public int fogStartingRow = -1; // -1 ise sis yok demektir. Örn 4 yazarsan 4. satır ve yukarısı zifiri karanlık başlar.
}