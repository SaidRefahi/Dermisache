using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

public static class AsemicGlyphAtlasBaker
{
    private const string FontAssetPath = "Assets/Import/Dermisachefont-Regular SDF.asset";
    private const string OutputPath = "Assets/Shader/AsemicWriting/GlyphAtlas_Font.png";
    private const int GridColumns = 8;
    private const int GridRows = 8;
    private const int CellPixels = 128;
    private const int AtlasSize = CellPixels * GridColumns;
    private const int MaxGlyphs = GridColumns * GridRows;
    private const float SideMarginFraction = 0.085f;
    private const float MaxHeightFraction = 0.85f;
    private const float SdfEdgeSoftness = 0.03f;

    [MenuItem("Tools/Dermisache/Bake Asemic Glyph Atlas from Font")]
    public static void Bake()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font == null)
        {
            Debug.LogError("TMP font asset not found: " + FontAssetPath);
            return;
        }

        Texture2D atlas = font.atlasTextures != null && font.atlasTextures.Length > 0 ? font.atlasTextures[0] : null;
        if (atlas == null)
        {
            Debug.LogError("Font atlas texture is null for: " + FontAssetPath);
            return;
        }

        byte[] sdfBytes = GetSdfBytes(font, atlas);
        if (sdfBytes == null)
        {
            Debug.LogError("Could not read SDF atlas data.");
            return;
        }
        if (sdfBytes.Length != atlas.width * atlas.height)
        {
            Debug.LogError($"SDF data size mismatch: got {sdfBytes.Length} bytes, expected {atlas.width * atlas.height} (single-channel atlas expected).");
            return;
        }
        int atlasW = atlas.width;
        int atlasH = atlas.height;

        List<uint> codes = new List<uint>();
        foreach (TMP_Character c in font.characterTable)
        {
            if (c != null && c.unicode != 32u) codes.Add(c.unicode);
        }
        codes.Sort();
        if (codes.Count == 0)
        {
            Debug.LogError("Font has no characters to bake.");
            return;
        }

        Color32[] outPixels = new Color32[AtlasSize * AtlasSize];
        for (int i = 0; i < outPixels.Length; i++) outPixels[i] = new Color32(255, 255, 255, 255);

        int baked = 0;
        int cursor = 0;
        for (int i = 0; i < MaxGlyphs; i++)
        {
            int attempts = 0;
            while (attempts < codes.Count)
            {
                uint code = codes[cursor % codes.Count];
                cursor++;
                attempts++;

                TMP_Character ch = font.characterTable.Find(x => x.unicode == code);
                if (ch == null) continue;
                // glyphTable is sorted by glyph index, not position-keyed.
                Glyph glyph = font.glyphTable.Find(g => g.index == ch.glyphIndex);
                if (glyph == null) continue;
                GlyphRect rect = glyph.glyphRect;
                if (rect.width <= 0 || rect.height <= 0) continue;

                // TMP rects use top-left origin; the byte array is row-major top-to-bottom.
                int srcTop = rect.y;

                float[,] ink = new float[rect.width, rect.height];
                int minX = rect.width, minY = rect.height, maxX = -1, maxY = -1;
                for (int y = 0; y < rect.height; y++)
                {
                    for (int x = 0; x < rect.width; x++)
                    {
                        float sdf = sdfBytes[(srcTop + y) * atlasW + rect.x + x] / 255f;
                        float a = Mathf.Clamp01((sdf - (0.5f - SdfEdgeSoftness)) / (2f * SdfEdgeSoftness));
                        ink[x, y] = a;
                        if (a > 0.5f)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
                if (maxX < 0) continue;

                int inkW = maxX - minX + 1;
                int inkH = maxY - minY + 1;
                float scale = Mathf.Min((1f - 2f * SideMarginFraction) * CellPixels / inkW, MaxHeightFraction * CellPixels / inkH);
                int drawW = Mathf.Max(1, Mathf.RoundToInt(inkW * scale));
                int drawH = Mathf.Max(1, Mathf.RoundToInt(inkH * scale));
                int drawX = (CellPixels - drawW) / 2;
                int drawY = (CellPixels - drawH) / 2;
                int col = i % GridColumns;
                int row = i / GridColumns;
                int destBaseX = col * CellPixels;
                int destBaseY = row * CellPixels;

                for (int y = 0; y < drawH; y++)
                {
                    int sy = minY + (y * inkH) / drawH;
                    for (int x = 0; x < drawW; x++)
                    {
                        int sx = minX + (x * inkW) / drawW;
                        byte v = (byte)Mathf.RoundToInt((1f - ink[sx, sy]) * 255f);
                        outPixels[(destBaseY + drawY + y) * AtlasSize + destBaseX + drawX + x] = new Color32(v, v, v, 255);
                    }
                }
                baked++;
                break;
            }
        }

        Texture2D tex = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
        tex.SetPixels32(outPixels);
        tex.Apply(false, false);
        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);
        File.WriteAllBytes(OutputPath, png);
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 50;
            importer.SaveAndReimport();
        }

        Texture2D bakedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
        if (bakedTexture == null)
        {
            Debug.LogError("Baked texture failed to import at: " + OutputPath);
            return;
        }

        int assigned = 0;
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in materialGuids)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || !mat.HasProperty("_GlyphAtlas")) continue;
            mat.SetTexture("_GlyphAtlas", bakedTexture);
            EditorUtility.SetDirty(mat);
            assigned++;
        }
        AssetDatabase.SaveAssets();

        Debug.Log($"Baked {baked} glyphs -> {OutputPath} | assigned to {assigned} materials");
    }

    private static byte[] GetSdfBytes(TMP_FontAsset font, Texture2D atlas)
    {
        if (atlas.isReadable)
        {
            try
            {
                return atlas.GetPixelData<byte>(0).ToArray();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("GetPixelData failed, falling back to embedded YAML data: " + e.Message);
            }
        }

        // TMP serializes the SDF atlas as hex text inside the font asset; single channel (Alpha8), row-major top-to-bottom.
        string path = AssetDatabase.GetAssetPath(font);
        string text = File.ReadAllText(path);
        int idx = text.IndexOf("_typelessdata: ", StringComparison.Ordinal);
        if (idx < 0)
        {
            Debug.LogError("No embedded texture data found in " + path);
            return null;
        }
        int start = idx + "_typelessdata: ".Length;
        int end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;
        string hex = text.Substring(start, end - start).Trim();
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((HexVal(hex[i * 2]) << 4) | HexVal(hex[i * 2 + 1]));
        }
        return bytes;
    }

    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return c - 'A' + 10;
    }
}