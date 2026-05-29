using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AdventureLevelConfig))]
public class AdventureLevelConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "enableAdvancedMode",
            "useHandcraftedOverrides",
            "handcraftedOverrides");

        SerializedProperty advancedMode = serializedObject.FindProperty("enableAdvancedMode");
        SerializedProperty useOverrides = serializedObject.FindProperty("useHandcraftedOverrides");
        SerializedProperty overrides = serializedObject.FindProperty("handcraftedOverrides");
        SerializedProperty objective = serializedObject.FindProperty("objective");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Gelişmiş Kontroller", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(advancedMode, new GUIContent("Gelişmiş Modu Aç"));

        if (advancedMode.boolValue)
        {
            EditorGUILayout.HelpBox(
                "El yapımı geçersiz kılmaları yalnızca jeneratörün hassas bir dokunuşa ihtiyaç duyduğu durumlarda kullanın. Varsayılan iş akışı yüksek seviyeli tasarım katmanında kalmalıdır.",
                MessageType.Info);

            EditorGUILayout.PropertyField(useOverrides, new GUIContent("El Yapımı Geçersiz Kılmaları Kullan"));

            if (useOverrides.boolValue)
                EditorGUILayout.PropertyField(overrides, new GUIContent("El Yapımı Ayarlar"), true);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Ham doğum ve olasılık ayarları, Gelişmiş Mod açılana kadar gizli tutulur.",
                MessageType.None);
        }

        if ((ObjectiveType)objective.enumValueIndex == ObjectiveType.DestroyObstacles ||
            (ObjectiveType)objective.enumValueIndex == ObjectiveType.ComboTarget)
        {
            EditorGUILayout.HelpBox(
                "Bu hedef türleri yeni içerik hattı için hazırlandı, ancak şu anda çalışma zamanında desteklenen en yakın LevelData hedeflerine çevriliyor.",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
