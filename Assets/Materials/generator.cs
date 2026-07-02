using UnityEngine;
using UnityEditor;
using System.IO;

public class GrainTextureGenerator : EditorWindow
{
    [MenuItem("Assets/Create/Texture/FilmGrain Noise")]
    static void CreateGrainTexture()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.R8, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        var random = new System.Random(123456); // фиксированный сид для повторяемости
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = (float)random.NextDouble();
                tex.SetPixel(x, y, new Color(r, r, r, 1));
            }
        }

        tex.Apply();

        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/FilmGrainNoise.asset");
        AssetDatabase.CreateAsset(tex, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(tex);
    }
}