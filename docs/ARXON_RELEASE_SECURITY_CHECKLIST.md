# ARXON – Release Security & Quality Checklist (Release Gate v1)

Bu doküman, ARXON'un mağaza yayınlama (App Store & Google Play) öncesinde güvenlik, veri bütünlüğü, mağaza uyumluluğu ve hile önleme gereksinimlerini doğrulamak için hazırlanmış resmi kontrol listesidir.

---

## A. Kod Güvenliği (Code Security)
- [ ] **Hardcoded Secret / API Key Kontrolü**: Kod içerisinde test veya harici servislerden kalma token, key veya URL bulunmamalıdır.
- [ ] **Debug Log Temizliği**: `Debug.Log`, `Debug.LogWarning` ve test logları release build konfigürasyonunda stripped edilmeli veya `[Conditional("ENABLE_LOGS")]` / `Debug.unityLogger.logEnabled = false` ile devre dışı bırakılmalıdır.
- [ ] **Test / Cheat Kodları**: Geliştirme sürecinde eklenen kısayol hileleri (sonsuz skor, anında level geçişi, spawn zorlama vb.) `#if UNITY_EDITOR` korumasına alınmalı veya prodüksiyondan kaldırılmalıdır.
- [ ] **Editor Only Scriptleri**: Editor scriptleri ve test yardımcıları `Editor/` klasöründe olmalı, runtime assembly'e dahil edilmemelidir.

---

## B. Asset & İçerik Güvenliği (Asset Security)
- [ ] **Lisans & Telif Doğrulaması**: Kullanılan tüm görsel, SFX, BGM ve yazı tipi (font) varlıklarının ticari mobil oyun kullanım lisansları doğrulanmalıdır.
- [ ] **AI Üretimi Varlıklar**: AI ile üretilen görsellerde ve ikonlarda marka/telif ihlali barındıran öğe bulunmadığı kontrol edilmelidir.
- [ ] **Kullanılmayan Asset Temizliği**: Build boyutunu şişirmemek için Resources klasörü veya referenced sahnelerde atıl/test asset kalmadığı teyit edilmelidir.
- [ ] **Sprite Atlas & Slicing**: Sprite atlaslarının doğru sıkıştırma formatlarında (ASTC / ETC2) ve artefakt/çizgi bırakmayacak padding ile paketlendiği kontrol edilmelidir.

---

## C. Unity & Build Güvenliği (Build Settings)
- [ ] **Development Build**: Kapalı.
- [ ] **Script Debugging / Profiler Autoconnect**: Kapalı.
- [ ] **Scripting Backend**: IL2CPP (ARM64 / ARMv7).
- [ ] **Managed Stripping Level**: High / Medium (Stripping kaynaklı kayıp reflection/serialization hataları test edilmeli).
- [ ] **Code Optimization**: Release / Speed.
- [ ] **Burst Compiler**: Aktif, safety checks kapalı / release modda.

---

## D. Platform & Gizlilik Gereksinimleri (iOS & Android)
- [ ] **Bundle Identifier & Package Name**: `com.digitalxpert.arxon` doğrulanmalı.
- [ ] **Version & Build / Version Code**: Store versiyonlama standartlarına uygun artırılmış olmalı.
- [ ] **iOS Privacy Manifest (NSPrivacyTracking & Required Reason APIs)**: iOS 17+ gereği kullanılan File Timestamp, UserDefaults vb. API'lar için `PrivacyInfo.xcprivacy` dosyası tanımlanmış olmalıdır.
- [ ] **App Tracking Transparency (ATT)**: Kullanıcı takibi yapılıyorsa ATT prompt'u eklenmeli; yapılmıyorsa gereksiz izinler Info.plist'ten kaldırılmalıdır.
- [ ] **App Transport Security (ATS)**: Zorunlu olmadıkça `NSAllowsArbitraryLoads` açılmamalı, HTTPS zorunlu tutulmalıdır.
- [ ] **Android Permissions**: `AndroidManifest.xml` içinde kamera, konum, mikrofon gibi gereksiz izinlerin yer almadığından emin olunmalıdır.

---

## E. Save Data & Veri Bütünlüğü (Save Integrity & Storage)
- [ ] **PlayerPrefs & Save File Tamper Koruması**: Skor, level ilerlemesi, kazanılan puanlar ham metin olarak bırakılmamalı; checksum / HMAC / şifreleme ile veri değiştirme teşebbüslerine karşı korunmalıdır.
- [ ] **Save File Corruption & Atomic Write**: Oyun aniden kapandığında veya pil bittiğinde save dosyasının bozulmaması için atomic write (geçici dosyaya yazıp replace etme) ve backup mekanizması olmalıdır.
- [ ] **Save Migration / Versioning**: Eski sürümlerden gelen save verisi yeni sürümde çökmeye yol açmadan migrate edilebilmelidir.
- [ ] **Offline Veri Bütünlüğü**: Çevrimdışı oynanışta üretilen verilerin tutarlılığı korunmalıdır.

---

## F. Yayın Öncesi AI Kod Audit Yönergeleri
Release öncesi kod tabanına uygulanacak güvenlik ve kalite tarama promptları:
- *Prompt 1 (Güvenlik)*: "ARXON kod tabanını mobil güvenlik ve exploit riski açısından tara. Hardcoded secret, açıkta kalan save manipülasyonu, unutulmuş hile metotları veya editor sızıntılarını listele."
- *Prompt 2 (Stabilite & Performans)*: "Tüm singleton, coroutine ve event aboneliklerini incele. Bellek sızıntısı (memory leak), sonsuz döngü, unhandled null exception veya GC allocation oluşturabilecek noktaları raporla."

---

## G. Hukuki & Mağaza Uyumluluğu (Legal & Store Policies)
- [ ] **Gizlilik Politikası (Privacy Policy)**: Canlı ve erişilebilir bir URL'de barındırılmalı.
- [ ] **Kullanım Koşulları (Terms of Service)**: Gerekli linkler oyunda ve mağazada yer almalı.
- [ ] **Third-Party Lisans Bildirimi**: Açık kaynak kütüphaneler ve font lisansları oyun içi Ayarlar/Hakkında kısmında veya yasal metinlerde listelenmelidir.
- [ ] **Yaş Derecelendirmesi (Age Rating)**: IARC / Apple Yaş Anketi (şiddet, kumar, sansür vb. unsurların bulunmadığı) doğru doldurulmalıdır.
- [ ] **Data Safety / App Privacy Nutrition Labels**: Toplanan veya toplanmayan veriler (Analytics, Crashlytics vb.) mağaza konsollarında eksiksiz beyan edilmelidir.
- [ ] **Export Compliance & Şifreleme**: HTTPS dışı özel şifreleme kullanılmıyorsa standart muafiyet seçilmeli.

---

## H. Anti-Cheat & Oyuncu Manipülasyonu (Mobile Game Specifics)
- [ ] **PlayerPrefs Edit Exploit**: Cihaz root/jailbreak olmadan da erişilebilen save dosyaları değiştirildiğinde oyun çökmemeli ve anormallikler sıfırlanmalıdır.
- [ ] **Time Travel Exploit**: Cihaz saatini ileri alma ile ödül / kilit açma suiistimali yapılıyorsa yerel zaman doğrulaması veya mantıksal limitler uygulanmalıdır.
- [ ] **Level Unlock & Skor Doğrulama**: Level veya puan atlama isteklerinde mantık sınırları (ulaşılamayacak skorların engellenmesi) bulunmalıdır.
- [ ] **Root / Jailbreak Toleransı**: Uygulama rootlu cihazlarda gereksiz yere çökmek yerine güvenli/izole modda çalışabilmelidir.

---

## I. Son 30 Dakika Yayın Kapısı Checklist (Final 30-Minute Gate)
1. [ ] Release build konfigürasyonu seçildi (Development Build = OFF).
2. [ ] Test/Debug GUI ve Overlay'ler sahnelerden kaldırıldı/inaktif edildi.
3. [ ] Console'da hiçbir kırmızı hata (Error) veya unhandled exception kalmadı.
4. [ ] Yeni temiz bir cihazda sıfırdan kurulum (Clean Install) testi yapıldı.
5. [ ] Eski sürümün üzerine güncelleme (Upgrade / Overwrite) testi yapıldı (Save korundu mu?).
6. [ ] Cihaz internetsiz (Airplane Mode) test edildi; takılma/çökme yok.
7. [ ] Ses açma/kapama, arka plana alma (App Pause / Resume), kilit ekranı ve çağrı gelme senaryoları test edildi.
8. [ ] Notch / Dynamic Island / Safe Area taşmaları fiziksel cihazda doğrulandı.
9. [ ] Mağaza ekran görüntüleri ve açıklamalar güncel.
10. [ ] Gizlilik politikası URL'si aktif ve çalışıyor.
