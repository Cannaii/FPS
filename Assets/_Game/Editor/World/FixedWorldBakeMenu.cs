using System;
using System.Collections.Generic;
using System.IO;
using AFPS.Bootstrap.World;
using AFPS.Presentation.Characters;
using AFPS.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFPS.Editor.World
{
    public static class FixedWorldBakeMenu
    {
        [Serializable] private sealed class Manifest
        {
            public string scene,sourceDependencyHash,worldHash;
            public int boxes,triangles,bytes;
        }
        [MenuItem("AFPS/World/Bake Saved Active Scene")]
        public static void BakeActiveScene()
        {
            string scenePath=SceneManager.GetActiveScene().path;
            if(string.IsNullOrEmpty(scenePath)) throw new InvalidOperationException("请先将关卡保存为场景资产。");
            string folder="Assets/_Game/Worlds";
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game","Worlds");
            string path=folder+"/"+Path.GetFileNameWithoutExtension(scenePath)+".bytes";
            FixedWorld world=BakeSavedScene(scenePath);
            byte[] snapshot=world.CreateSnapshot();
            File.WriteAllBytes(path,snapshot);
            var manifest=new Manifest {scene=scenePath,sourceDependencyHash=AssetDatabase.GetAssetDependencyHash(scenePath).ToString(),worldHash=world.VersionHash.ToString("X16"),boxes=world.BoxCount,triangles=world.TriangleCount,bytes=snapshot.Length};
            File.WriteAllText(Path.ChangeExtension(path,"bake.json"),JsonUtility.ToJson(manifest,true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"AFPS_WORLD_BAKED path={path} hash={manifest.worldHash} boxes={manifest.boxes} triangles={manifest.triangles} bytes={manifest.bytes}");
        }
        public static FixedWorld BakeSavedScene(string scenePath)
        {
            // 读取磁盘上的保存版本，不保存/更改用户当前场景，也不把运行中的玩家胶囊烘焙进去。
            Scene active=SceneManager.GetActiveScene();
            string temporaryPath="Assets/__AFPSWorldBake_"+Guid.NewGuid().ToString("N")+".unity";
            if(!AssetDatabase.CopyAsset(scenePath,temporaryPath)) throw new IOException("无法创建场景烘焙副本。");
            Scene preview=default;
            try
            {
                preview=EditorSceneManager.OpenScene(temporaryPath,OpenSceneMode.Additive);
                var colliders=new List<Collider>();var excluded=new HashSet<Collider>();
                foreach(GameObject root in preview.GetRootGameObjects())
                {
                    colliders.AddRange(root.GetComponentsInChildren<Collider>(true));
                    foreach(PlayerView player in root.GetComponentsInChildren<PlayerView>(true)) foreach(Collider collider in player.GetComponentsInChildren<Collider>(true)) excluded.Add(collider);
                }
                foreach(Collider collider in colliders)
                {
                    UnityEngine.Object source=collider is TerrainCollider terrain ? terrain.terrainData : collider is MeshCollider mesh ? mesh.sharedMesh : null;
                    if(source!=null && AssetDatabase.Contains(source) && EditorUtility.IsDirty(source)) throw new InvalidOperationException("几何源资产有未保存修改，请先保存后再烘焙："+AssetDatabase.GetAssetPath(source));
                }
                return SceneWorldBaker.Bake(colliders,excluded:excluded);
            }
            finally {if(preview.IsValid()) EditorSceneManager.CloseScene(preview,true);if(active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);AssetDatabase.DeleteAsset(temporaryPath);}
        }
    }
}
