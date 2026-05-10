using UnityEngine;
using TMPro;
using System.Collections;

public class FloatingText : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float fadeSpeed = 1.5f;
    public float lifeTime = 1.2f;

    private TextMeshPro textMesh;
    private Color textColor;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void SetText(string text, Color color, float size = 4f)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();
        
        textMesh.text = text;
        textMesh.color = color;
        textColor = color;
        textMesh.fontSize = size;

        // Yazı her zaman diğer blokların önünde çıksın
        textMesh.sortingOrder = 20; 
        
        StartCoroutine(FadeAndDestroy());
    }

    void Update()
    {
        // Yukarı doğru hareket et
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;
        
        // Yavaşça şeffaflaş (Alpha değerini düşür)
        textColor.a -= fadeSpeed * Time.deltaTime;
        textMesh.color = textColor;
    }

    IEnumerator FadeAndDestroy()
    {
        yield return new WaitForSeconds(lifeTime);
        Destroy(gameObject);
    }
}