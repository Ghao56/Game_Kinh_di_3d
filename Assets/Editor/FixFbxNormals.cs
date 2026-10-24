using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FixFbxNormals
{
    [MenuItem("Tools/Fix Abandoned House FBX Normals")]
    public static void Fix()
    {
        const string folder = "Assets/Map/Abandoned_House";
        var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
        int fixedCount = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx")) continue;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.importNormals == ModelImporterNormals.Calculate) continue;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.SaveAndReimport();
            fixedCount++;
            Debug.Log("[FixFbxNormals] Fixed: " + path);
        }
        Debug.Log($"[FixFbxNormals] Done. {fixedCount} file(s) reimported.");
    }
}
