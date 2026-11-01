#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Oldtimer.ttf fontundan otomatik olarak TextMeshPro SDF Font Asset'i (Oldtimer SDF.asset) oluşturan editör yardımcısı.
/// </summary>
[InitializeOnLoad]
public static class OldtimerFontAssetBuilder
{
    static OldtimerFontAssetBuilder()
    {
        EditorApplication.delayCall += EnsureOldtimerFontAsset;
    }

    [MenuItem("Tools/Oldtimer TMP Fontu Oluştur")]
    public static void EnsureOldtimerFontAsset()
    {
        string ttfPath = "Assets/Models/ui/oldtimer/Oldtimer.ttf";
        string assetPath = "Assets/Models/ui/oldtimer/Oldtimer SDF.asset";

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
            return;

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null) return;

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (fontAsset != null)
        {
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OldtimerFontAssetBuilder] 'Oldtimer SDF.asset' başarıyla oluşturuldu!");
        }
    }
}
#endif

