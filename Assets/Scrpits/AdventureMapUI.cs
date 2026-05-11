using UnityEngine;

// Bu script sadece AdventureMap sahnesindeki UI elemanlarını yönetecek.
public class AdventureMapUI : MonoBehaviour
{
    // Bu fonksiyonu "Geri" butonuna bağlayacağız.
    public void GoToMainMenu()
    {
        // Kalıcı olan SceneLoader'a "Ana Menüye git" emrini veriyoruz.
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
        }
        else
        {
            Debug.LogError("SceneLoader bulunamadı! Oyun MainMenu sahnesinden mi başlatıldı?");
        }
    }
}