using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Collections.Generic;

public class AddressablesQuickAddWindow : EditorWindow
{
    private AddressableAssetGroup _selectedGroup;
    private Vector2 _scrollPos;

    [MenuItem("Tools/Addressables Quick Adder")]
    public static void ShowWindow()
    {
        GetWindow<AddressablesQuickAddWindow>("Addressables Quick Adder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Add Assets to Addressables Group", EditorStyles.boldLabel);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Addressable Asset Settings not found. Please initialize Addressables first in Window > Asset Management > Addressables > Groups.", MessageType.Error);
            return;
        }

        // Group Selection
        List<string> groupNames = new List<string>();
        int selectedIndex = 0;
        for (int i = 0; i < settings.groups.Count; i++)
        {
            // 숨겨진 내장 그룹(Built In Data)은 제외시켜주는 것이 편합니다.
            if (settings.groups[i].IsDefaultGroup() || !settings.groups[i].ReadOnly)
            {
                groupNames.Add(settings.groups[i].Name);
                if (_selectedGroup == settings.groups[i])
                {
                    selectedIndex = groupNames.Count - 1;
                }
            }
        }

        if (groupNames.Count > 0)
        {
            selectedIndex = EditorGUILayout.Popup("Target Group", selectedIndex, groupNames.ToArray());
            string selectedGroupName = groupNames[selectedIndex];
            _selectedGroup = settings.groups.Find(g => g.Name == selectedGroupName);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Selected Assets to Add:", EditorStyles.label);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
        var selectedObjects = Selection.objects;
        bool hasValidAssets = false;

        if (selectedObjects.Length == 0)
        {
            EditorGUILayout.HelpBox("Select assets in the Project window to add them.", MessageType.Info);
        }
        else
        {
            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                // 폴더가 아닌 실제 에셋들만 필터링
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                {
                    EditorGUILayout.LabelField("- " + obj.name + " (" + obj.GetType().Name + ")");
                    hasValidAssets = true;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        GUI.enabled = hasValidAssets && _selectedGroup != null;
        if (GUILayout.Button("Add Selected Assets to Group", GUILayout.Height(40)))
        {
            AddSelectedAssetsToGroup(settings);
        }
        GUI.enabled = true;
    }

    private void AddSelectedAssetsToGroup(AddressableAssetSettings settings)
    {
        int addedCount = 0;
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;

            string guid = AssetDatabase.AssetPathToGUID(path);
            
            // 그룹에 추가 또는 이동
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, _selectedGroup, readOnly: false, postEvent: false);
            if (entry != null)
            {
                // 어드레스를 에셋의 원본 이름으로 설정 (경로 제외)
                entry.SetAddress(obj.name); 
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddressablesQuickAdder] Successfully added {addedCount} assets to group '{_selectedGroup.Name}'.");
        }
    }
}
