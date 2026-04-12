using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class FontChangerWindow : EditorWindow
{
    private Font _targetLegacyFont;
    private TMP_FontAsset _targetTMPFont;

    [MenuItem("Tools/Font Changer")]
    public static void ShowWindow()
    {
        GetWindow<FontChangerWindow>("Font Changer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Font Replacer Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _targetLegacyFont = (Font)EditorGUILayout.ObjectField("Legacy Font", _targetLegacyFont, typeof(Font), false);
        _targetTMPFont = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font Asset", _targetTMPFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Replace fonts on all Text/TextMeshPro components.\n(BMJUA_ttf requires a TMP_FontAsset if you use TextMeshPro!)", MessageType.Info);

        if (GUILayout.Button("Change Fonts in Active Scene", GUILayout.Height(30)))
        {
            ChangeFontsInActiveScene();
        }

        if (GUILayout.Button("Change Fonts in All Prefabs", GUILayout.Height(30)))
        {
            ChangeFontsInPrefabs();
        }
    }

    private void ChangeFontsInActiveScene()
    {
        int count = 0;

        // TextMeshPro UGUI (UI Text)
        if (_targetTMPFont != null)
        {
            TextMeshProUGUI[] tmpTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tmp in tmpTexts)
            {
                Undo.RecordObject(tmp, "Change TMP Font");
                tmp.font = _targetTMPFont;
                EditorUtility.SetDirty(tmp);
                count++;
            }

            // TextMeshPro (3D Text)
            TextMeshPro[] tmp3DTexts = FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tmp in tmp3DTexts)
            {
                Undo.RecordObject(tmp, "Change TMP 3D Font");
                tmp.font = _targetTMPFont;
                EditorUtility.SetDirty(tmp);
                count++;
            }
        }

        // Legacy Text
        if (_targetLegacyFont != null)
        {
            Text[] legacyTexts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var txt in legacyTexts)
            {
                Undo.RecordObject(txt, "Change Legacy Font");
                txt.font = _targetLegacyFont;
                EditorUtility.SetDirty(txt);
                count++;
            }
        }

        Debug.Log($"[Font Changer] Successfully changed fonts on {count} text objects in the active scene!");
    }

    private void ChangeFontsInPrefabs()
    {
        int prefabCount = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                EditorUtility.DisplayProgressBar("Changing Fonts in Prefabs", $"Processing {prefab.name}", (float)i / guids.Length);

                bool isModified = false;

                if (_targetTMPFont != null)
                {
                    var tmpTexts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var tmp in tmpTexts)
                    {
                        tmp.font = _targetTMPFont;
                        isModified = true;
                    }

                    var tmp3DTexts = prefab.GetComponentsInChildren<TextMeshPro>(true);
                    foreach (var tmp in tmp3DTexts)
                    {
                        tmp.font = _targetTMPFont;
                        isModified = true;
                    }
                }

                if (_targetLegacyFont != null)
                {
                    var legacyTexts = prefab.GetComponentsInChildren<Text>(true);
                    foreach (var txt in legacyTexts)
                    {
                        txt.font = _targetLegacyFont;
                        isModified = true;
                    }
                }

                if (isModified)
                {
                    EditorUtility.SetDirty(prefab);
                    PrefabUtility.SavePrefabAsset(prefab);
                    prefabCount++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Font Changer] Successfully modified {prefabCount} prefabs in the project!");
    }
}
