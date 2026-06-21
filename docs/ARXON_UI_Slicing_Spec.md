# ARXON UI Slicing Spec

Bu dokuman `Assets/Textures/ARXON_UI.png` icin ilk slicing referansidir.

Kapsam:

- sadece atlas analizi
- sprite isimlendirme
- kullanim amaci
- slice tipi

Kapsam disi:

- scene degisikligi
- script degisikligi
- prefab implementasyonu

Kaynak atlas:

- `Assets/Textures/ARXON_UI.png`

## Import Varsayimi

| Alan | Deger |
| --- | --- |
| Texture Type | `Sprite (2D and UI)` |
| Sprite Mode | `Multiple` |
| Mesh Type | `Full Rect` |
| Mip Maps | `Off` |
| Filter Mode | `Bilinear` |
| Compression | UI kalitesini bozmayacak sekilde dusuk / yok |

## Slicing Kurali

Bu asamada tum sprite'lar `Single` mantiginda ayrilacak.

- `9-slice` yok
- yeniden boyutlandirma beklentisi yok
- Illustrator'da final ebatlar hazirlandigi icin sprite'lar dogrudan kullanilacak

Not:

- Ileride panel veya buton boyutlari degisecekse yeniden export gerekebilir.

## Sprite Listesi

| Sprite Name | Kullanim | Slice Type | Not |
| --- | --- | --- | --- |
| `panel_pause` | Pause overlay ana paneli | `Single` | Buyuk dikey tas panel varyanti |
| `panel_settings` | Settings overlay ana paneli | `Single` | Buyuk dikey tas panel varyanti |
| `hud_classic` | Classic mode ust HUD bar'i | `Single` | Yatay tas bar |
| `hud_adventure` | Adventure mode ust HUD bar'i | `Single` | Yatay tas bar |
| `btn_green` | Ana yesil aksiyon butonu govdesi | `Single` | Text TMP ile ustte gelecek |
| `btn_home` | Ana menu / home aksiyon butonu | `Single` | Ikon baked |
| `btn_restart` | Yeniden baslat aksiyon butonu | `Single` | Ikon baked |
| `btn_menu` | Menu / liste aksiyon butonu | `Single` | Ikon baked |
| `btn_level` | Kare ikon tabanli level / secim butonu | `Single` | Base buton gibi dusunulebilir |
| `icon_music` | Muzik ayari ikonu | `Single` | Settings icinde |
| `icon_sound` | Ses ayari ikonu | `Single` | Settings icinde |
| `icon_hint` | Ipucu ayari veya yardim ikonu | `Single` | Settings icinde |
| `icon_vibration` | Titresim ayari ikonu | `Single` | Settings icinde |
| `icon_purchases` | Satin almayi geri yukle / purchases alani | `Single` | Settings icinde |
| `icon_contact` | Iletisim / sosyal medya / bize ulasin alani | `Single` | Settings icinde |
| `toggle_off` | Pasif toggle durumu | `Single` | Baked `OFF` iceriyor |
| `toggle_on` | Aktif toggle durumu | `Single` | Baked `ON` iceriyor |
| `progress_bar` | Segmentli ilerleme cubugu | `Single` | Mavi segmentli gorunum |

## Uygulama Notlari

- `btn_green` uzerine tum lokalize metinler TMP ile gelecek.
- `btn_home`, `btn_restart`, `btn_menu` ikonlu buton gibi kullanilacak.
- `btn_level` kare form faktorlu secim butonu olarak dusunulmeli.
- `toggle_off` ve `toggle_on` dogrudan state swap ile kullanilabilir.
- `panel_pause` ve `panel_settings` su an tek parca panel olarak ele alinacak.
- `hud_classic` ve `hud_adventure` su an tek parca HUD parcalari olarak ele alinacak.

## Acik Notlar

- Atlas icinde `OK` yazili ayrica bir yesil buton bulunuyor, fakat mevcut naming listesinde yer almiyor.
- Eger bu asset kullanilacaksa sonradan ayrica adlandirilmali:
  - onerilen ad: `btn_ok_baked`
- Mevcut atlas adlandirmasinda `icon_contact`, iletisim ve sosyal linkleri temsil eden ikon olarak yorumlaniyor.
- `icon_purchases`, satin almayi geri yukle amaciyla kullanilacak.

