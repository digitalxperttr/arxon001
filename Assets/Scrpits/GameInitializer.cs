using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    // Unity'den sürükleyip bırakacağımız Managers Prefab'i
    public GameObject managersPrefab;

    void Awake()
    {
        // Oyunda hali hazırda bir SceneLoader var mı?
        // Varsa, yöneticiler zaten kurulmuş demektir, hiçbir şey yapma.
        if (SceneLoader.Instance != null)
        {
            return; 
        }

        // Eğer yoksa (yani oyun ilk defa bu sahneden başlıyorsa),
        // Managers prefab'ini oluştur.
        if (managersPrefab != null)
        {
            Instantiate(managersPrefab);
        }
    }
}