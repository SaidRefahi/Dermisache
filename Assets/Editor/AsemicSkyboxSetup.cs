using UnityEditor;
using UnityEngine;

public static class AsemicSkyboxSetup
{
    private const string ShaderName = "Skybox/AsemicGlyphs";
    private const string MaterialPath = "Assets/Shader/AsemicWriting/AsemicSky.mat";
    private const string DefaultAtlasPath = "Assets/Shader/AsemicWriting/GlyphAtlas_Font.png";

    [MenuItem("Tools/Dermisache/Setup Asemic Skybox")]
    public static void Setup()
    {
        Shader skyShader = Shader.Find(ShaderName);
        if (skyShader == null)
        {
            Debug.LogError("Skybox shader not found: " + ShaderName + ". Make sure AsemicSky.shader is imported and compiled.");
            return;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(skyShader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.shader = skyShader;

        if (mat.GetTexture("_GlyphAtlas") == null)
        {
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultAtlasPath);
            if (atlas != null) mat.SetTexture("_GlyphAtlas", atlas);
        }

        // dynamic day -> afternoon -> night loop
        mat.SetFloat("_AutoCycle", 1f);
        mat.SetFloat("_TimeOfDay", 0f);

        RenderSettings.skybox = mat;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        Debug.Log("Asemic skybox assigned to RenderSettings.skybox -> " + MaterialPath + " (Auto Cycle ON)");
    }
}