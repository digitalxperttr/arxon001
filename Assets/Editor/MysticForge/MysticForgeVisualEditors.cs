using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ForgeEnergyBreathing))]
[CanEditMultipleObjects]
public class ForgeEnergyBreathingEditor : Editor
{
    private SerializedProperty scriptProperty;
    private SerializedProperty duration;
    private SerializedProperty alphaAmplitude;
    private SerializedProperty scaleAmplitude;
    private SerializedProperty verticalAmplitude;

    private static readonly GUIContent DurationLabel = new GUIContent("Döngü Süresi");
    private static readonly GUIContent AlphaAmplitudeLabel = new GUIContent("Alfa Değişimi");
    private static readonly GUIContent ScaleAmplitudeLabel = new GUIContent("Ölçek Genliği");
    private static readonly GUIContent VerticalAmplitudeLabel = new GUIContent("Dikey Hareket Genliği");

    private void OnEnable()
    {
        if (!CanUseSerializedObject())
            return;

        scriptProperty = serializedObject.FindProperty("m_Script");
        duration = serializedObject.FindProperty("duration");
        alphaAmplitude = serializedObject.FindProperty("alphaAmplitude");
        scaleAmplitude = serializedObject.FindProperty("scaleAmplitude");
        verticalAmplitude = serializedObject.FindProperty("verticalAmplitude");
    }

    public override void OnInspectorGUI()
    {
        if (!CanUseSerializedObject())
            return;

        if (duration == null)
            OnEnable();

        serializedObject.Update();
        DrawScriptReference(scriptProperty);
        EditorGUILayout.PropertyField(duration, DurationLabel);
        EditorGUILayout.PropertyField(alphaAmplitude, AlphaAmplitudeLabel);
        EditorGUILayout.PropertyField(scaleAmplitude, ScaleAmplitudeLabel);
        EditorGUILayout.PropertyField(verticalAmplitude, VerticalAmplitudeLabel);
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawScriptReference(SerializedProperty property)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(property);
        }
    }

    private bool CanUseSerializedObject()
    {
        if (target == null || targets == null || targets.Length == 0)
            return false;

        foreach (Object inspectedTarget in targets)
        {
            if (inspectedTarget == null)
                return false;
        }

        return true;
    }
}

[CustomEditor(typeof(ForgeEnergyWaveController))]
[CanEditMultipleObjects]
public class ForgeEnergyWaveControllerEditor : Editor
{
    private SerializedProperty scriptProperty;
    private SerializedProperty waveAmplitudeA;
    private SerializedProperty waveFrequencyA;
    private SerializedProperty waveSpeedA;
    private SerializedProperty waveAmplitudeB;
    private SerializedProperty waveFrequencyB;
    private SerializedProperty waveSpeedB;
    private SerializedProperty verticalBias;
    private SerializedProperty waveIntensity;

    private static readonly GUIContent WaveAmplitudeALabel = new GUIContent("Dalga A Genliği");
    private static readonly GUIContent WaveFrequencyALabel = new GUIContent("Dalga A Sıklığı");
    private static readonly GUIContent WaveSpeedALabel = new GUIContent("Dalga A Hızı");
    private static readonly GUIContent WaveAmplitudeBLabel = new GUIContent("Dalga B Genliği");
    private static readonly GUIContent WaveFrequencyBLabel = new GUIContent("Dalga B Sıklığı");
    private static readonly GUIContent WaveSpeedBLabel = new GUIContent("Dalga B Hızı");
    private static readonly GUIContent VerticalBiasLabel = new GUIContent("Dikey Kaydırma");
    private static readonly GUIContent WaveIntensityLabel = new GUIContent("Dalga Şiddeti");

    private void OnEnable()
    {
        if (!CanUseSerializedObject())
            return;

        scriptProperty = serializedObject.FindProperty("m_Script");
        waveAmplitudeA = serializedObject.FindProperty("waveAmplitudeA");
        waveFrequencyA = serializedObject.FindProperty("waveFrequencyA");
        waveSpeedA = serializedObject.FindProperty("waveSpeedA");
        waveAmplitudeB = serializedObject.FindProperty("waveAmplitudeB");
        waveFrequencyB = serializedObject.FindProperty("waveFrequencyB");
        waveSpeedB = serializedObject.FindProperty("waveSpeedB");
        verticalBias = serializedObject.FindProperty("verticalBias");
        waveIntensity = serializedObject.FindProperty("waveIntensity");
    }

    public override void OnInspectorGUI()
    {
        if (!CanUseSerializedObject())
            return;

        if (waveAmplitudeA == null)
            OnEnable();

        serializedObject.Update();
        DrawScriptReference(scriptProperty);
        EditorGUILayout.PropertyField(waveAmplitudeA, WaveAmplitudeALabel);
        EditorGUILayout.PropertyField(waveFrequencyA, WaveFrequencyALabel);
        EditorGUILayout.PropertyField(waveSpeedA, WaveSpeedALabel);
        EditorGUILayout.PropertyField(waveAmplitudeB, WaveAmplitudeBLabel);
        EditorGUILayout.PropertyField(waveFrequencyB, WaveFrequencyBLabel);
        EditorGUILayout.PropertyField(waveSpeedB, WaveSpeedBLabel);
        EditorGUILayout.PropertyField(verticalBias, VerticalBiasLabel);
        EditorGUILayout.PropertyField(waveIntensity, WaveIntensityLabel);
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawScriptReference(SerializedProperty property)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(property);
        }
    }

    private bool CanUseSerializedObject()
    {
        if (target == null || targets == null || targets.Length == 0)
            return false;

        foreach (Object inspectedTarget in targets)
        {
            if (inspectedTarget == null)
                return false;
        }

        return true;
    }
}
