using UnityEngine;
using System.Collections; // Coroutine için şart


public class Block : MonoBehaviour
{
    public int x, y, width;
    public bool isMoving = false;

    public Color blockColor;
    
    [Header("Hareket Ayarları")]
    public float moveSpeed = 15f; // Kayma hızı, ihtiyaca göre artırabilirsin
    private Vector3 targetPosition;

    public bool isFrozen = false;
    public bool isRock = false;
    public bool isChained = false;
    private GameObject chainVisual;
    private GameObject iceVisual; // Üzerine eklenecek buz katmanı
    private MaterialPropertyBlock mpb;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Shader'daki renk değişkeninin adı, URP'de genelde "_BaseColor" veya "_EmissionColor" olabilir.
    
    [Header("Görsel Efektler")]
    public float glowIntensity = 0.7f; // Seçilince parlaklık çarpanı
    private TrailRenderer trail; // Hız izi (Motion Trail) için


void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.emitting = false; // Başlangıçta iz kapalı
    }

public void SetHighlight(bool isHighlighted)
    {
        // Seçiliyse rengin gücünü artır (URP'de Bloom parlaklığı verir), değilse normale dön
        Color targetColor = isHighlighted ? blockColor * glowIntensity : blockColor;
        
        if (mpb == null) mpb = new MaterialPropertyBlock();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorProperty, targetColor);
        sr.SetPropertyBlock(mpb);
    }

public void SetRock(bool rockStatus)
    {
        isRock = rockStatus;
        if (isRock)
        {
            // Şimdilik kaya olduğunu anlamak için koyu gri/taş rengi yapıyoruz.
            // (İleride buraya kendi gerçek grafik dosyasını (sprite) atayacağız)
            SetBlockColor(new Color(0.3f, 0.3f, 0.3f, 1f)); 
        }
    }

public void SetBlockColor(Color c)
    {
        blockColor = c; 
        
        if (mpb == null) mpb = new MaterialPropertyBlock();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorProperty, c);
        sr.SetPropertyBlock(mpb);

        // === İZ (TRAIL) RENGİNİ DİNAMİK AYARLAMA ===
        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            Gradient gradient = new Gradient();
            
            // Sistemi çökerten kısmı adım adım yazarak aşıyoruz:
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(c, 0.0f);
            colorKeys[1] = new GradientColorKey(c, 1.0f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.6f, 0.0f); // Başı %60 opak
            alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f); // Sonu tam şeffaf
            
            gradient.SetKeys(colorKeys, alphaKeys);
            trail.colorGradient = gradient;
        }
    }
public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        if (isFrozen)
        {
            // Eğer buz görseli henüz oluşturulmadıysa oluştur
            if (iceVisual == null)
            {
                iceVisual = new GameObject("IceVisual");
                iceVisual.transform.SetParent(this.transform);
                iceVisual.transform.localPosition = Vector3.zero;
                
                SpriteRenderer mySr = GetComponent<SpriteRenderer>();
                SpriteRenderer iceSr = iceVisual.AddComponent<SpriteRenderer>();
                
                // Ana bloğun şeklini ve boyutunu birebir kopyala
                iceSr.sprite = mySr.sprite;
                iceSr.drawMode = mySr.drawMode;
                iceSr.size = mySr.size; 
                
                // Rengini yarı şeffaf bir buz mavisi yap
                iceSr.color = new Color(0.5f, 0.9f, 1f, 0.6f); 
                iceSr.sortingOrder = mySr.sortingOrder + 1; // Ana bloğun hemen önünde dursun
                
                iceVisual.transform.localScale = Vector3.one; 
            }
            iceVisual.SetActive(true);
        }
        else
        {
            // Buzu kır (Kapat)
            if (iceVisual != null) iceVisual.SetActive(false);
        }
    }

public void SetChained(bool chained)
    {
        isChained = chained;
        if (isChained)
        {
            if (chainVisual == null)
            {
                chainVisual = new GameObject("ChainVisual");
                chainVisual.transform.SetParent(this.transform);
                chainVisual.transform.localPosition = Vector3.zero;
                
                SpriteRenderer mySr = GetComponent<SpriteRenderer>();
                SpriteRenderer chainSr = chainVisual.AddComponent<SpriteRenderer>();
                
                chainSr.sprite = mySr.sprite;
                chainSr.drawMode = mySr.drawMode;
                chainSr.size = mySr.size;
                
                // Şimdilik zinciri temsil etmesi için koyu metalik ve yarı saydam yapalım
                chainSr.color = new Color(0.1f, 0.1f, 0.1f, 0.7f); 
                chainSr.sortingOrder = mySr.sortingOrder + 2; // En önde dursun
                
                chainVisual.transform.localScale = Vector3.one; 
            }
            chainVisual.SetActive(true);
        }
        else
        {
            // Zinciri Kır (Kapat)
            if (chainVisual != null) chainVisual.SetActive(false);
        }
    }

    // Bloğu yeni bir koordinata gönder
public void MoveTo(int newX, int newY)
    {
        if (GridManager.Instance.isGameOver) return;
        
        x = newX;
        y = newY;
        
        // Hedeflenen dünya koordinatını hesapla
        // (Genişliğe göre merkezleme mantığını koruyoruz)
        targetPosition = new Vector3(x + (width - 1) * 0.5f, y, 0);
        
        // Eğer zaten oradaysak isMoving'i açmaya gerek yok
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            isMoving = true;
        }
    }
void Update()
    {
        if (isMoving)
        {
            // YENİ: Blok aşağı düşerken iz bırakmasın! Sadece yatayda (oyuncu çekerken) bıraksın.
            if (trail != null) 
            {
                float yFarki = Mathf.Abs(targetPosition.y - transform.position.y);
                trail.emitting = (yFarki < 0.01f);
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);

            if (Vector3.Distance(transform.position, targetPosition) < 0.005f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
        else
        {
            if (trail != null) trail.emitting = false;
        }
    }

public System.Collections.IEnumerator CrunchAndDestroy(GameObject explosionPrefab)
    {
        // 1. Patlama Efekti (Partiküller)
        if (explosionPrefab != null) {
            GameObject effect = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            var main = effect.GetComponent<ParticleSystem>().main;
            Color finalColor = blockColor;
            finalColor.a = 1.0f;
            main.startColor = new ParticleSystem.MinMaxGradient(finalColor);
            Destroy(effect, 1f);
        }

        // İzi kapat (Gariplik yapmasın)
        if (trail != null) trail.emitting = false;

        // DİKKAT: O 9-Sliced kapatma (Simple yapma) satırını TAMAMEN SİLDİK!

        // 2. Temiz İçe Çökme (Pürüzsüz Küçülme)
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;
        float duration = 0.15f; // Çok hızlı bir erime

        while (elapsed < duration)
        {
            // Orijinal boyutundan sıfıra doğru, döndürmeden, temizce küçült
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, elapsed / duration);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Gerçekten Yok Et
        Destroy(gameObject);
    }
    
    }