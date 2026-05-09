using UnityEngine;


public class Block : MonoBehaviour
{
    public int x, y, width;
    public bool isMoving = false;
    
    [Header("Hareket Ayarları")]
    public float moveSpeed = 15f; // Kayma hızı, ihtiyaca göre artırabilirsin
    private Vector3 targetPosition;


public void SetBlockColor(Color newColor)
{
    SpriteRenderer sr = GetComponent<SpriteRenderer>();
    
    // YENİ EKLE: Kodun rengi buradan okuyabilmesi için sr.color'ı da eşitleyelim
    sr.color = newColor; 

    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
    sr.GetPropertyBlock(mpb);

    mpb.SetColor("_BaseColor", newColor);
    // Emission şiddetini ihtiyacına göre (2.5f veya daha yüksek) ayarlayabilirsin
    mpb.SetColor("_EmissionColor", newColor * 2.5f); 

    sr.SetPropertyBlock(mpb);
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