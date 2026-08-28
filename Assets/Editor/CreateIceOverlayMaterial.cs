// Bu script Assets/Editor/ klasöründe tutulur.
// Unity Editor açıldığında veya menüden çalıştırıldığında
// M_IceOverlay.mat materyalini otomatik oluşturur.

using UnityEditor;
using UnityEngine;

public static class CreateIceOverlayMaterial
{
    private const string ShaderName    = "ARXON/IceOverlay";
    private const string MaterialPath  = "Assets/Materials/M_IceOverlay.mat";

    [MenuItem("ARXON/Tools/Create Ice Overlay Material")]
    public static void CreateMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[IceOverlay] Shader '{ShaderName}' bulunamadı. " +
                           "Assets/Materials/IceOverlay.shader dosyasının mevcut ve hatasız olduğundan emin ol.");
            return;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        bool isNew = false;
        if (mat == null)
        {
            mat = new Material(shader) { name = "M_IceOverlay" };
            isNew = true;
        }
        else
        {
            mat.shader = shader;
        }

        // Kristal berraklığında buz zırhı varsayılan ayarları
        mat.SetColor("_IceTint",          new Color(0.72f, 0.92f, 1.0f, 1.0f));
        mat.SetFloat("_BodyOpacity",      0.20f);

        mat.SetColor("_CrackColor",       new Color(0.92f, 0.98f, 1.0f, 1.0f));
        mat.SetFloat("_CrackScale",       2.8f);
        mat.SetFloat("_CrackThickness",   0.08f);
        mat.SetFloat("_CrackStrength",    0.75f);

        mat.SetColor("_RimColor",         new Color(0.88f, 0.96f, 1.0f, 1.0f));
        mat.SetFloat("_RimStrength",      0.85f);
        mat.SetFloat("_RimPower",         2.8f);
        mat.SetFloat("_FrameFrostStrength", 0.85f);
        mat.SetFloat("_TopHighlight",     0.30f);

        mat.SetColor("_SheenColor",       new Color(1.0f, 1.0f, 1.0f, 1.0f));
        mat.SetFloat("_SheenStrength",    0.60f);
        mat.SetFloat("_SheenSpeed",       0.75f);
        mat.SetFloat("_SheenWidth",       7.0f);

        mat.SetVector("_BlockSize",       new Vector4(1f, 1f, 0f, 0f));
        mat.SetFloat("_PhaseOffset",      0.0f);

        if (isNew)
        {
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log($"[IceOverlay] Materyal oluşturuldu: {MaterialPath}");
        }
        else
        {
            EditorUtility.SetDirty(mat);
            Debug.Log($"[IceOverlay] Mevcut materyal yeni kristal ayarlarla güncellendi: {MaterialPath}");
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = mat;
        EditorGUIUtility.PingObject(mat);
    }
}
