using UnityEngine;


public class Block : MonoBehaviour
{
    public int x, y, width;
    public bool isMoving = false;

    public Color blockColor;
    
    [Header("Hareket Ayarları")]
    public float moveSpeed = 15f; // Kayma hızı, ihtiyaca göre artırabilirsin
    private Vector3 targetPosition;

    public bool isFrozen = false;
    private GameObject iceVisual; // Üzerine eklenecek buz katmanı
    private MaterialPropertyBlock mpb;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Shader'daki renk değişkeninin adı, URP'de genelde "_BaseColor" veya "_EmissionColor" olabilir.


public void SetBlockColor(Color c)
    {
        blockColor = c; // YENİ EKLENEN: Rengi hafızaya alıyoruz!
        
        // MPB sadece bir kez oluşturulur, obje başına değil!
        if (mpb == null) 
        {
            mpb = new MaterialPropertyBlock();
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        
        // 1. Mevcut ayarları al
        sr.GetPropertyBlock(mpb); 
        
        // 2. Rengi ayarla (Shader'da Emission'ı besleyen özellik adını buraya yaz)
        mpb.SetColor(ColorProperty, c); 
        
        // 3. Bloğa geri ver
        sr.SetPropertyBlock(mpb); 
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
            // İŞTE SİHİRLİ LERP SATIRI:
            // Mevcut pozisyondan hedef pozisyona yumuşak bir geçiş yapar
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);

            // Hedefe çok yaklaştıysak hareketi durdur ve pozisyonu sabitle
            if (Vector3.Distance(transform.position, targetPosition) < 0.005f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
    }}