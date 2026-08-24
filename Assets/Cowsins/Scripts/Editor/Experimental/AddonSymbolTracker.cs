#if UNITY_EDITOR && (SAVE_LOAD_ADD_ON || INVENTORY_PRO_ADD_ON)

using UnityEditor;
using UnityEngine;

class AddonSymbolTracker : AssetPostprocessor
{
    private static string inventoryTargetFolder;
    private static string saveLoadTargetFolder;

    static AddonSymbolTracker()
    {
        inventoryTargetFolder = EditorPrefs.GetString("InventoryFolder", "Assets/InventoryPro_ADD-ON");
        saveLoadTargetFolder = EditorPrefs.GetString("SaveLoadFolder", "Assets/Save&LoadSystem [ADD-ON]");
    }

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        CheckFolderMoves(movedAssets, movedFromAssetPaths);
        CheckFolderDeletions(deletedAssets);
    }

    private static void CheckFolderMoves(string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < movedAssets.Length; i++)
        {
            if (movedFromAssetPaths[i].StartsWith(inventoryTargetFolder))
            {
                inventoryTargetFolder = movedAssets[i];
                EditorPrefs.SetString("InventoryFolder", inventoryTargetFolder);
            }
            else if (movedFromAssetPaths[i].StartsWith(saveLoadTargetFolder))
            {
                saveLoadTargetFolder = movedAssets[i];
                EditorPrefs.SetString("SaveLoadFolder", saveLoadTargetFolder);
            }
        }
    }

    private static void CheckFolderDeletions(string[] deletedAssets)
    {
        foreach (string deletedAsset in deletedAssets)
        {
            if (deletedAsset.StartsWith(inventoryTargetFolder))
                RemoveDefineSymbol("INVENTORY_PRO_ADD_ON"); 
            else if (deletedAsset.StartsWith(saveLoadTargetFolder))
                RemoveDefineSymbol("SAVE_LOAD_ADD_ON"); 
        }
    }
    private static void RemoveDefineSymbol(string defineSymbol)
    {
        BuildTargetGroup currentBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        if (currentBuildTargetGroup == BuildTargetGroup.Unknown)
            return;
#pragma warning disable CS0618
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(currentBuildTargetGroup);
        if (defines.Contains(defineSymbol))
        {
            defines = defines.Replace(defineSymbol, "").Trim(';');
            PlayerSettings.SetScriptingDefineSymbolsForGroup(currentBuildTargetGroup, defines);
            Debug.LogWarning($"Removed define symbol: {defineSymbol}");
        }
#pragma warning restore CS0618
    }
}

#endif
