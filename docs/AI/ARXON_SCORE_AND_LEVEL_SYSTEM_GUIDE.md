# ARXON — Puan ve Seviye Sistemi Devir & Rehber Dokümanı (Score & Progression Guide)

Bu doküman, **ARXON** projesindeki güncel **Puan Hesaplama (Scoring)** ve **Seviye İlerleme (Level Progression)** mimarisini detaylandırır. Özellikle **Codex** veya diğer geliştiricilerin **Macera Modu (Adventure Mode)** ve **Klasik Mod (Classic Mode)** üzerinde yapacağı iyileştirmelerde referans alması için hazırlanmıştır.

---

## 1. Sistemin Temel Felsefesi ve Son Güncellemeler

- **Eski Durum:** Satır başına $8$ taban puan veriliyordu. Tek satır patlatıldığında ekranda `+8` belirmesi mobil puzzle standartlarında dopamin eksikliğine ve Seviye 10 sonrasında oyunu tıkayan bir "grind" duvarına yol açıyordu.
- **Güncel Durum ("Yol A" Modeli):**
  - Tek satır taban puanı **$100$** olarak yeniden ölçeklendi.
  - Çoklu satır patlatmalar (Multi-line clear) karesel ve tatmin edici şekilde ödüllendirildi ($1$ satır: $100$, $2$ satır: $300$, $3$ satır: $700$, $4$ satır: $1500$).
  - Klasik mod seviye eşikleri bu yeni tabana göre ölçeklenerek akıcı bir tempo kazandı.
  - Tahta başlangıç doluluğu **$5$ satır** olarak belirlendi ($8 \times 10$ tahtada tam $\%50$ doluluk ve anında kombo fırsatı).

---

## 2. Puanlama Motoru (Scoring Pipeline)

> **Yetkili Kod Dosyası:** [`Assets/Scrpits/GridManager.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/GridManager.cs) (`CheckAndClearRowsRoutine`)

Bir satır temizlendiğinde verilen puan şu formülle hesaplanır:

$$\text{pointsToGive} = \text{baseScore} \times \text{moveMultiplier} \times \text{chainMultiplier} \times \text{levelMultiplier}$$

### A. Taban Satır Puanı (`baseScore` / `GetBaseRowScore`)
Aynı hamlede eşzamanlı patlayan satır sayısına göre belirlenir:
- **1 Satır:** $100$ Puan
- **2 Satır:** $300$ Puan
- **3 Satır:** $700$ Puan
- **4 Satır:** $1.500$ Puan
- **5+ Satır:** $1.500 + (\text{satır} - 4) \times 1.000$ Puan

### B. Kombo Çarpanı (`moveMultiplier` / `ScoreManager.Instance.comboMultiplier`)
- Oyuncunun arka arkaya satır patlattığı her geçerli hamlede `comboCount` $+1$ artar.
- Hiçbir satır patlamayan bir kaydırma yapıldığında kombo anında sıfırlanır (`comboCount = 0`).
- Döküm:
  - 1. Başarılı Hamle: **1x**
  - 2. Ardışık Başarılı Hamle: **2x** (`2x COMBO!`)
  - 3. Ardışık Başarılı Hamle: **3x** (`3x COMBO!`)
  - $N$. Ardışık Başarılı Hamle: **Nx**

### C. Zincirleme / Kaskad Çarpanı (`chainMultiplier` / `chainDepth + 1`)
- Satır temizlendikten sonra yerçekimi (gravity) ile düşen blokların kendi kendine yeni bir satır oluşturup patlatmasıdır.
- Döküm:
  - İlk vuruş: **1x** (`chainDepth = 0`)
  - 1. Düşüş Reaksiyonu: **2x** (`chainDepth = 1`, `CHAIN x2!`)
  - 2. Düşüş Reaksiyonu: **3x** (`chainDepth = 2`, `CHAIN x3!`)
  - 3. Düşüş Reaksiyonu: **4x** (`chainDepth = 3`, `CHAIN x4!` + Kamera sarsıntısı)

### D. Seviye Çarpanı (`levelMultiplier` / `GetClassicScoreMultiplier`)
- **Klasik Modda (`IsClassicRun() == true`):**
  - **Level 1 – 4:** **1x**
  - **Level 5 – 9:** **2x**
  - **Level 10 – 14:** **3x**
  - **Level 15+:** **4x**
- **Macera Modunda (`IsClassicRun() == false`):**
  - **Her zaman 1x**. Seviye çarpanı Macera Modu hedeflerini yapay olarak şişirmemesi için devre dışıdır.

### E. Tahtayı Tamamen Temizleme Bonusu (`GetPerfectClearBonus`)
Tahtada hiçbir blok kalmadığında (`activeBlocks.Count == 0`) anında eklenen tek seferlik ödül:
- **Level 1 – 3:** $+1.000$ Puan
- **Level 4 – 6:** $+1.500$ Puan
- **Level 7 – 9:** $+2.500$ Puan
- **Level 10 – 12:** $+4.000$ Puan
- **Level 13+:** $+8.000$ Puan

---

## 3. Seviye İlerleme Sistemi (Level Progression)

> **Yetkili Kod Dosyası:** [`Assets/Scrpits/ScoreManager.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/ScoreManager.cs)  
> **Prefab:** [`Assets/Prefabs/Managers.prefab`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Prefabs/Managers.prefab)

Klasik mod seviye atlama şartı **kesinlikle PUANA (Score)** bağlıdır (satır sayısı şartı eski bir kuraldır ve kaldırılmıştır).

### Seviye Eşik Tablosu (Level Thresholds)

| Seviye | Giriş Puanı | Sonraki Seviye Hedefi | Seviye İçi Gereken Puan | Hesaplama Tipi |
| :--- | :---: | :---: | :---: | :--- |
| **Level 1** | **0** | **1.000** | 1.000 | Sabit Dizi (`scoreLevelThresholds`) |
| **Level 2** | **1.000** | **2.500** | 1.500 | Sabit Dizi |
| **Level 3** | **2.500** | **4.500** | 2.000 | Sabit Dizi |
| **Level 4** | **4.500** | **7.000** | 2.500 | Sabit Dizi |
| **Level 5** | **7.000** | **10.500** | 3.500 | Sabit Dizi |
| **Level 6** | **10.500** | **15.000** | 4.500 | Sabit Dizi |
| **Level 7** | **15.000** | **20.500** | 5.500 | Sabit Dizi |
| **Level 8** | **20.500** | **27.000** | 6.500 | Sabit Dizi |
| **Level 9** | **27.000** | **35.000** | 8.000 | Sabit Dizi |
| **Level 10** | **35.000** | **47.000** | 12.000 | Geçiş Eşiği |
| **Level 11** | **47.000** | **61.000** | 14.000 | Dinamik Formül *(+12.000 + 2.000)* |
| **Level 12** | **61.000** | **77.000** | 16.000 | Dinamik Formül *(+14.000 + 2.000)* |
| **Level 13** | **77.000** | **95.000** | 18.000 | Dinamik Formül *(+16.000 + 2.000)* |
| **Level 14** | **95.000** | **115.000** | 20.000 | Dinamik Formül *(+18.000 + 2.000)* |
| **Level 15** | **115.000** | **137.000** | 22.000 | Dinamik Formül *(+20.000 + 2.000)* |

- **Seviye 10+ Sonsuz Formül Parametreleri:**
  - `postThresholdBaseGap = 12000`
  - `postThresholdGapIncrease = 2000`
- **HUD İlerleme Çubuğu (XP Bar):**
  - $\text{progressScore} = \text{currentScore} - \text{currentLevelStartScore}$
  - $\text{requiredScore} = \text{nextLevelScore} - \text{currentLevelStartScore}$
  - HUD üzerinde: `progressScore / requiredScore` (Örn: `3.500 / 12.000`).

---

## 4. Macera Modu (Adventure Mode) ile Etkileşim ve Codex İçin Kurallar

Codex ile Macera Modu üzerinde çalışırken aşağıdaki mimari kurallara dikkat edilmelidir:

### A. Macera Modunda Puan Hedefleri (`ObjectiveType.ReachScore`)
- [`Assets/Scrpits/AdventureLevelGenerator.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/AdventureLevelGenerator.cs) içerisinde seviyeler için `defaultScore` değerleri tanımlıdır:
  - Kolay / Başlangıç Seviyeleri: $\sim 1.500 - 2.500$ Puan ($\sim 10-15$ temizleme)
  - Orta Seviyeler: $\sim 4.500$ Puan ($\sim 15-20$ temizleme)
  - Zor Seviyeler: $\sim 7.000$ Puan ($\sim 25$ temizleme veya kombo gereksinimi)
- Yeni puan skalasında ortalama tek satır $100$, ikili satır $300$ puan verdiğinden bu hedefler tam uyumludur. Yeni seviyeler tasarlanırken bu baz değerler göz önünde bulundurulmalıdır.

### B. Skor Olaylarının Raporlanması (Event Flow)
- Oyuncu satır patlattığında veya Perfect Clear aldığında `ScoreManager.Instance.AddScore(points)` çağrılır.
- `ScoreManager`, Macera Modu açıkken otomatik olarak:
  ```csharp
  if (ObjectiveManager.Instance != null)
  {
      ObjectiveManager.Instance.ReportScoreChanged(currentScore);
      if (LevelManager.Instance != null && LevelManager.Instance.enabled)
      {
          LevelManager.Instance.EvaluateObjectiveCompletion();
      }
  }
  ```
  çağrısını tetikler. Bu akış bozulmamalıdır.

### C. HUD Ayrımı (`shouldUseClassicHud`)
- Macera Modunda `ScoreText`, `LevelText`, `XPText`, `LevelProgressBar` gibi Klasik Mod HUD objeleri gizlenir (`SetClassicHudState(false)`).
- Bunun yerine Macera Moduna özel `ObjectiveHUD`, `TargetText` ve `MovesText` devreye girer.

### D. Klasik / Macera Modu Kontrolü
- Kodda mod ayrımı `GridManager.IsClassicRun()` üzerinden yapılır:
  ```csharp
  public bool IsClassicRun() =>
      ProgressManager.Instance == null ||
      ProgressManager.Instance.currentSelectedLevel == null &&
      LevelManager.Instance != null &&
      LevelManager.Instance.currentLevel == null;
  ```
- Yapılacak Macera Modu değişikliklerinde `IsClassicRun()` kontrolü korunmalı, Klasik Mod akışına yan etki yapılmamalıdır.

---

## 5. İlgili Dosyalar Özeti

| Dosya | Görevi / Rolü |
| :--- | :--- |
| [`GridManager.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/GridManager.cs) | `GetBaseRowScore()`, `GetClassicScoreMultiplier()`, `GetPerfectClearBonus()`, `CheckAndClearRowsRoutine()`, `initialRowCount = 5` |
| [`ScoreManager.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/ScoreManager.cs) | `scoreLevelThresholds`, `postThresholdBaseGap`, `comboMultiplier`, `AddScore()`, XP Bar hesaplamaları |
| [`Managers.prefab`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Prefabs/Managers.prefab) | `ScoreManager` serialized array ve gap değerleri |
| [`AdventureLevelGenerator.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/AdventureLevelGenerator.cs) | Macera modu hedef puanları (`defaultScore = 1500, 2500, 4500, 7000`) |
| [`ObjectiveManager.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/ObjectiveManager.cs) | Macera modu `ReachScore` ve diğer görevlerin takibi |
| [`FloatingText.cs`](file:///Users/bayramsanli/Desktop/unity%20projects/arxon001/Assets/Scrpits/FloatingText.cs) | Ekranda uçan `+100`, `2x COMBO!`, `CHAIN x2!` görsel metinleri |
