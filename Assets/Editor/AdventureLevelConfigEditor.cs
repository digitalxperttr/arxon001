using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AdventureLevelConfig))]
public class AdventureLevelConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty authoringMode = serializedObject.FindProperty("authoringMode");
        DrawIdentity((AdventureStageAuthoringMode)authoringMode.enumValueIndex);
        if ((AdventureStageAuthoringMode)authoringMode.enumValueIndex == AdventureStageAuthoringMode.LegacyMacroAuthoring)
            DrawLegacyAuthoring(authoringMode);
        else
            DrawDirectAuthoring();

        serializedObject.ApplyModifiedProperties();
        if (!AdventureLevelGenerator.ValidateConfig((AdventureLevelConfig)target, out string error))
            EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    private void DrawIdentity(AdventureStageAuthoringMode authoringMode)
    {
        EditorGUILayout.LabelField("Kimlik", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelId"), new GUIContent("Stage Id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eventId"), new GUIContent("Event Id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("contentVersion"), new GUIContent("Content Version"));
        DrawStageNumber(authoringMode);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Display Name"));
        EditorGUILayout.Space();
    }

    private void DrawStageNumber(AdventureStageAuthoringMode authoringMode)
    {
        SerializedProperty displayedLevelNumber = serializedObject.FindProperty("displayedLevelNumber");
        SerializedProperty legacyLevelNumber = serializedObject.FindProperty("levelNumber");
        int stageNumber = displayedLevelNumber.intValue > 0 ? displayedLevelNumber.intValue : legacyLevelNumber.intValue;

        EditorGUI.BeginChangeCheck();
        int editedStageNumber = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Stage Number"), stageNumber));
        bool changedByDesigner = EditorGUI.EndChangeCheck();
        bool requiresSynchronization = displayedLevelNumber.intValue != editedStageNumber || legacyLevelNumber.intValue != editedStageNumber;
        if (changedByDesigner || requiresSynchronization)
        {
            Undo.RecordObject(target, "Synchronize Adventure Stage Number");
            displayedLevelNumber.intValue = editedStageNumber;
            legacyLevelNumber.intValue = editedStageNumber;
        }

        if (authoringMode == AdventureStageAuthoringMode.DirectRecipeAuthoring)
        {
            EditorGUILayout.HelpBox(
                "Stage Number harita/UI sırası için kullanılır. Eski internal number uyumluluk için saklanır ve bu değerle otomatik eşitlenir; kalıcı kimlik Stage Id'dir.",
                MessageType.Info);
        }
    }

    private void DrawLegacyAuthoring(SerializedProperty authoringMode)
    {
        EditorGUILayout.HelpBox(
            "Bu config eski macro authoring kullanıyor. Mevcut generator sonucu korunarak Direct Stage Recipe'ye bir kez bake edilebilir.",
            MessageType.Warning);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("difficulty"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressure"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("obstacleTheme"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("specialMechanicFocus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("moveOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressureOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("objectives"), new GUIContent("Objectives"), true);

        if (GUILayout.Button("Bake Legacy Settings into Direct Stage Recipe"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(target, "Bake Adventure Stage Recipe");
            if (!AdventureLevelGenerator.BakeLegacyMacroAuthoringIntoDirectRecipe((AdventureLevelConfig)target, out string error))
                Debug.LogError($"[Adventure] {error}", target);
            else
                EditorUtility.SetDirty(target);

            serializedObject.Update();
            Repaint();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawDirectAuthoring()
    {
        SerializedProperty recipe = serializedObject.FindProperty("directRecipe");
        EditorGUILayout.LabelField("Objectives", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("objectives"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stage Rules / Bölüm Kuralları", EditorStyles.boldLabel);
        SerializedProperty hasMoveLimit = recipe.FindPropertyRelative("hasMoveLimit");
        EditorGUILayout.PropertyField(hasMoveLimit, new GUIContent("Has Move Limit"));
        using (new EditorGUI.DisabledScope(!hasMoveLimit.boolValue))
            EditorGUILayout.PropertyField(recipe.FindPropertyRelative("moveLimit"), new GUIContent("Move Limit"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target Obstacle Supply", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(recipe.FindPropertyRelative("targetObstaclePerRowLimit"), new GUIContent("New Target Obstacles Per Row"));
        EditorGUILayout.PropertyField(recipe.FindPropertyRelative("targetObstacleActiveBoardLimit"), new GUIContent("Target Obstacles Allowed On Board"));
        EditorGUILayout.HelpBox(
            "0 keeps this constraint disabled for compatibility. LocalTrial_03 starts with 2 / 2: at most two incomplete objective obstacles per new row, and a target type pauses while two are already on the board.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Obstacles / Engeller", EditorStyles.boldLabel);
        DrawSpawnSetting(recipe, "rock", "Rock");
        DrawSpawnSetting(recipe, "chain", "Chain");
        DrawSpawnSetting(recipe, "ice", "Ice");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Support Blocks / Destek Blokları", EditorStyles.boldLabel);
        DrawSpawnSetting(recipe, "fire", "Fire", true);
        DrawSpawnSetting(recipe, "slice", "Slice", true);
        EditorGUILayout.HelpBox(
            "Fire ve Slice support-block olasılıklarıdır: 0.10 = %10. Yüksek birleşik değerler Normal blok ve collectible carrier havuzunu azaltır. Fire veya Slice etkinse custom spawn rules otomatik etkinleşir.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fog / Sis", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(recipe.FindPropertyRelative("fogDensity"), new GUIContent("Density"));
        EditorGUILayout.PropertyField(recipe.FindPropertyRelative("fogCoveragePercent"), new GUIContent("Coverage"));
        EditorGUILayout.PropertyField(recipe.FindPropertyRelative("fogStartingRow"), new GUIContent("Start Row"));

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Designer Notes", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("designerNotes"));

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Revert yalnızca active authoring mode'u Legacy Macro Authoring'e döndürür. Direct Stage Recipe ve Designer Notes saklanır, ancak Legacy macro alanları yeniden authoritative olur.",
            MessageType.Warning);
        if (GUILayout.Button("Revert to Legacy Authoring"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(target, "Revert Adventure Stage to Legacy Authoring");
            if (!AdventureLevelGenerator.RevertDirectRecipeAuthoringToLegacyMacroAuthoring((AdventureLevelConfig)target, out string error))
                Debug.LogError($"[Adventure] {error}", target);
            else
                EditorUtility.SetDirty(target);

            serializedObject.Update();
            Repaint();
            GUIUtility.ExitGUI();
        }
    }

    private static void DrawSpawnSetting(SerializedProperty recipe, string name, string label, bool isSupportBlock = false)
    {
        SerializedProperty setting = recipe.FindPropertyRelative(name);
        SerializedProperty enabled = setting.FindPropertyRelative("enabled");
        SerializedProperty chance = setting.FindPropertyRelative("chance");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(enabled, new GUIContent(label));
        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            if (isSupportBlock && chance.floatValue > 0.10f)
            {
                EditorGUILayout.PropertyField(chance, GUIContent.none);
            }
            else if (isSupportBlock)
            {
                EditorGUI.BeginChangeCheck();
                float value = EditorGUILayout.Slider(chance.floatValue, 0f, 0.10f);
                if (EditorGUI.EndChangeCheck()) chance.floatValue = value;
            }
            else
            {
                EditorGUILayout.PropertyField(chance, GUIContent.none);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (isSupportBlock && chance.floatValue > 0.10f)
        {
            EditorGUILayout.HelpBox(
                $"{label} chance is {chance.floatValue:P0}, above the normal Stage-authoring range of 10%. The existing value is preserved.",
                MessageType.Warning);
        }
    }
}

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LevelData level = (LevelData)target;
        bool readOnly = level.IsRuntimeLevel || (level.hideFlags & HideFlags.DontSave) != 0;
        if (readOnly)
            EditorGUILayout.HelpBox("Çözümlenen runtime çıktı. Kaynak ayarları AdventureLevelConfig üzerinden düzenleyin; sayaçlar bu veriden ayrıdır.", MessageType.Info);
        using (new EditorGUI.DisabledScope(readOnly))
            DrawDefaultInspector();
        if (readOnly)
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("runtimeObjectives"), new GUIContent("Runtime Hedefleri"), true);
        }
    }
}
