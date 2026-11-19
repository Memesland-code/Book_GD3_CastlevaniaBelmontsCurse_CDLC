using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class WallRunMaterialGenerator
{
    [MenuItem("Tools/Generate Wall Run Material")]
    public static void Generate()
    {
        const string folderPath = "Assets/Graphics/Meshes/GameplayElements/WallRunWall";
        const string baseTexPath = folderPath + "/T_WallRunChevrons_Base.png";
        const string emissiveTexPath = folderPath + "/T_WallRunChevrons_Emissive.png";
        const string matPath = folderPath + "/M_WallRun.mat";

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        int size = 512;
        Texture2D baseTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D emissiveTex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Color bgColor = new Color(0.11f, 0.11f, 0.12f, 1f);
        Color chevronColor = new Color(1f, 0.42f, 0.05f, 1f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;

                // Two stacked upward chevrons per tile
                float localV = (v * 2f) % 1f;
                float distFromCenterX = Mathf.Abs(u - 0.5f);

                // Upward chevron: peak at center (u = 0.5), sloping down toward edges
                float chevronPeakY = 0.78f - distFromCenterX * 1.1f;
                float distToLine = Mathf.Abs(localV - chevronPeakY);

                bool insideHorizontalBounds = distFromCenterX <= 0.42f;
                float alpha = insideHorizontalBounds
                    ? 1f - Mathf.Clamp01((distToLine - 0.08f) / 0.015f)
                    : 0f;

                baseTex.SetPixel(x, y, Color.Lerp(bgColor, chevronColor, alpha));
                emissiveTex.SetPixel(x, y, Color.Lerp(Color.black, Color.white, alpha));
            }
        }

        baseTex.Apply();
        emissiveTex.Apply();

        File.WriteAllBytes(baseTexPath, baseTex.EncodeToPNG());
        File.WriteAllBytes(emissiveTexPath, emissiveTex.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(baseTex);
        UnityEngine.Object.DestroyImmediate(emissiveTex);

        ConfigureTexture(baseTexPath, true);
        ConfigureTexture(emissiveTexPath, true);

        Texture2D savedBaseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexPath);
        Texture2D savedEmissiveTex = AssetDatabase.LoadAssetAtPath<Texture2D>(emissiveTexPath);

        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
        {
            AssetDatabase.DeleteAsset(matPath);
        }

        Material mat = new Material(Shader.Find("HDRP/Lit"));
        AssetDatabase.CreateAsset(mat, matPath);

        mat.SetTexture("_BaseColorMap", savedBaseTex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.25f);

        // Warm orange HDRP emission (1200 nits avoids blowing out to white)
        mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
        mat.SetTexture("_EmissiveColorMap", savedEmissiveTex);
        mat.SetColor("_EmissiveColorLDR", chevronColor);
        mat.SetFloat("_UseEmissiveIntensity", 1f);
        mat.SetFloat("_EmissiveIntensityUnit", 0f);
        mat.SetFloat("_EmissiveIntensity", 1200f);
        mat.SetColor("_EmissiveColor", chevronColor * 1200f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;

        ValidateHdrpMaterial(mat);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = mat;
        Debug.Log("Orange Wall Run Material generated at: " + matPath);
    }

    private static void ConfigureTexture(string path, bool sRGB)
    {
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;

        importer.sRGBTexture = sRGB;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();
    }

    private static void ValidateHdrpMaterial(Material mat)
    {
        Type hdMaterialType = Type.GetType("UnityEditor.Rendering.HighDefinition.HDMaterial, Unity.RenderPipelines.HighDefinition.Editor");
        MethodInfo validateMethod = hdMaterialType?.GetMethod("ValidateMaterial", BindingFlags.Public | BindingFlags.Static);
        validateMethod?.Invoke(null, new object[] { mat });
    }
}