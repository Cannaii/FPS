using System;
using System.Collections.Generic;
using AFPS.Bootstrap.World;
using AFPS.Math;
using AFPS.Simulation.World;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class SceneWorldBakerTests
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            owned.Add(go);
            return go;
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[i]);
                }
            }

            owned.Clear();
        }

        private static FixVec3 V(float x, float y, float z) => new FixVec3(Fix.FromFloat(x), Fix.FromFloat(y), Fix.FromFloat(z));
        private TerrainCollider Terrain(bool varying = false)
        {
            var data = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(32, 8, 32)
            };
            owned.Add(data);
            var heights = new float[33, 33];
            if (varying)
            {
                for (int z = 0; z < 33; z++)
                {
                    for (int x = 0; x < 33; x++)
                    {
                        heights[z, x] = ((x * 7 + z * 3 + x * z) % 17) / 40f;
                    }
                }
            }

            data.SetHeights(0, 0, heights);
            var go = UnityEngine.Terrain.CreateTerrainGameObject(data);
            owned.Add(go);
            go.name = "BakerTerrain";
            go.transform.position = new Vector3(2000, 0, 2000);
            return go.GetComponent<TerrainCollider>();
        }

        [Test]
        public void Bake_OrderAndDuplicateEnumerationDoNotChangeCanonicalBytes()
        {
            var a = Create("A").AddComponent<BoxCollider>();
            var b = Create("B").AddComponent<BoxCollider>();
            b.transform.position = Vector3.right * 3;
            CollectionAssert.AreEqual(SceneWorldBaker.Bake(new Collider[] { a, b, a }).CreateSnapshot(), SceneWorldBaker.Bake(new Collider[] { b, a }).CreateSnapshot());
        }

        [Test]
        public void Bake_FiltersDisabledInactiveTriggersLayersAndExplicitExclusions()
        {
            var a = Create("Disabled").AddComponent<BoxCollider>();
            a.enabled = false;
            var b = Create("Inactive").AddComponent<BoxCollider>();
            b.gameObject.SetActive(false);
            var c = Create("Trigger").AddComponent<BoxCollider>();
            c.isTrigger = true;
            var d = Create("Layer").AddComponent<BoxCollider>();
            d.gameObject.layer = 4;
            var e = Create("Player").AddComponent<CapsuleCollider>();
            var world = SceneWorldBaker.Bake(new Collider[] { a, b, c, d, e }, 1, new HashSet<Collider> { e });
            Assert.That(world.BoxCount, Is.Zero);
            Assert.That(world.TriangleCount, Is.Zero);
        }

        [Test]
        public void Bake_UnsupportedAndDynamicCollidersAreNotSilentlyIgnored()
        {
            var capsule = Create("Unsupported").AddComponent<CapsuleCollider>();
            Assert.Throws<NotSupportedException>(() => SceneWorldBaker.Bake(new[] { capsule }));
            var box = Create("Dynamic").AddComponent<BoxCollider>();
            box.gameObject.AddComponent<Rigidbody>();
            Assert.Throws<NotSupportedException>(() => SceneWorldBaker.Bake(new[] { box }));
        }

        [Test]
        public void Bake_AxisAlignedBoxPreservesVolumeAndSurfaceMetadata()
        {
            var go = Create("Wall");
            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(1, 2, 3);
            collider.size = new Vector3(2, 4, 6);
            go.transform.position = Vector3.right * 3;
            go.AddComponent<WorldSurfaceAuthoring>().Surfaces = FixedSurface.Ladder;
            var world = SceneWorldBaker.Bake(new[] { collider });
            Assert.That(world.BoxCount, Is.EqualTo(1));
            Assert.That(world.GetBox(0).Bounds.Min, Is.EqualTo(V(3, 0, 0)));
            Assert.That(world.GetBox(0).Bounds.Max, Is.EqualTo(V(5, 4, 6)));
            Assert.That(world.GetBox(0).Surface, Is.EqualTo(FixedSurface.Ladder));
            Assert.That(world.CapsuleCast(V(0, 0, 3), Fix.Half, Fix.FromInt(2), FixVec3.Right, Fix.FromInt(10)).DidHit, Is.False);
        }

        [Test]
        public void Bake_RotatedBoxKeepsActualFacesRatherThanItsBounds()
        {
            var collider = Create("Rotated").AddComponent<BoxCollider>();
            collider.size = Vector3.one * 2;
            collider.transform.rotation = Quaternion.Euler(0, 45, 0);
            var world = SceneWorldBaker.Bake(new[] { collider });
            Assert.That(world.BoxCount, Is.Zero);
            Assert.That(world.TriangleCount, Is.EqualTo(12));
            Assert.That(world.Raycast(V(1, 3, 1), -FixVec3.Up, Fix.FromInt(5)).DidHit, Is.False);
            Assert.That(world.Raycast(V(0, 3, 0), -FixVec3.Up, Fix.FromInt(5)).Distance, Is.EqualTo(Fix.FromInt(2)));
        }

        [Test]
        public void Bake_MeshTransformAndConvexRejectionAreExplicit()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.forward * 2,
                    Vector3.right * 2
                },
                triangles = new[] { 0, 1, 2 }
            };
            owned.Add(mesh);
            var collider = Create("Mesh").AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.transform.position = new Vector3(3, 1, 0);
            var world = SceneWorldBaker.Bake(new[] { collider });
            Assert.That(world.GetTriangle(0).A, Is.EqualTo(V(3, 1, 0)));
            Assert.That(world.GetTriangle(0).B, Is.EqualTo(V(3, 1, 2)));
            var tetra = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    Vector3.forward
                },
                triangles = new[]
                {
                    0, 2, 1, 0, 1, 3,
                    0, 3, 2, 1, 2, 3
                }
            };
            owned.Insert(owned.IndexOf(collider.gameObject), tetra);
            collider.sharedMesh = tetra;
            collider.convex = true;
            Assert.Throws<NotSupportedException>(() => SceneWorldBaker.Bake(new[] { collider }));
        }

        [Test]
        public void Bake_FlatTerrainIsExactlyMergedAndHolesRemainOpen()
        {
            var collider = Terrain();
            var world = SceneWorldBaker.Bake(new[] { collider });
            Assert.That(world.TriangleCount, Is.EqualTo(2));
            var holes = collider.terrainData.GetHoles(0, 0, 32, 32);
            holes[5, 5] = false;
            collider.terrainData.SetHoles(0, 0, holes);
            world = SceneWorldBaker.Bake(new[] { collider });
            Assert.That(world.Raycast(V(2005.5f, 2, 2005.5f), -FixVec3.Up, Fix.FromInt(4)).DidHit, Is.False);
            Assert.That(world.Raycast(V(2004.5f, 2, 2004.5f), -FixVec3.Up, Fix.FromInt(4)).DidHit, Is.True);
        }

        [Test]
        public void Bake_TerrainTrianglesMatchUnityNativeTerrainCollision()
        {
            var collider = Terrain(true);
            var world = SceneWorldBaker.Bake(new[] { collider });
            Physics.SyncTransforms();
            var random = new FixRandom(82101);
            for (int i = 0; i < 100; i++)
            {
                float x = 2000 + random.NextInt(0, 31) + 0.23f,
                    z = 2000 + random.NextInt(0, 31) + 0.67f;
                var ray = new Ray(new Vector3(x, 16, z), Vector3.down);
                Assert.That(collider.Raycast(ray, out RaycastHit native, 32), Is.True);
                var hit = world.Raycast(V(x, 16, z), -FixVec3.Up, Fix.FromInt(32));
                Assert.That(hit.DidHit, Is.True);
                Assert.That(System.Math.Abs(hit.Point.Y.ToDouble() - native.point.y), Is.LessThan(0.0003), "sample=" + i);
            }
        }

        [Test]
        public void Bake_TerrainSnapshotLoadsToSameRayAndCapsuleResults()
        {
            var world = SceneWorldBaker.Bake(new[] { Terrain(true) });
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(world.CreateSnapshot()), out var restored, out _), Is.True);
            var a = world.CapsuleCast(V(2010, 8, 2010), Fix.Half, Fix.FromInt(2), -FixVec3.Up, Fix.FromInt(10));
            var b = restored.CapsuleCast(V(2010, 8, 2010), Fix.Half, Fix.FromInt(2), -FixVec3.Up, Fix.FromInt(10));
            Assert.That(a.DidHit, Is.True);
            Assert.That(a.Distance, Is.EqualTo(b.Distance));
            Assert.That(a.Normal, Is.EqualTo(b.Normal));
            Assert.That(a.ShapeId, Is.EqualTo(b.ShapeId));
        }

        [Test]
        public void SavedMap_BakeIsCanonicalAndMatchesNativeTerrain()
        {
            string scene = "Assets/_Game/Scenes/MyLevel/SampleScene.unity";
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            var world = AFPS.Editor.World.FixedWorldBakeMenu.BakeSavedScene(scene);
            CollectionAssert.AreEqual(System.IO.File.ReadAllBytes("Assets/_Game/Worlds/SampleScene.bytes"), world.CreateSnapshot());
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle, Is.EqualTo(active.handle));
            Assert.That(active.isDirty, Is.EqualTo(dirty));
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Game/Scenes/MyLevel/New Terrain.asset");
            var collider = Create("SavedMapTerrainProbe").AddComponent<TerrainCollider>();
            collider.terrainData = data;
            collider.transform.position = new Vector3(-500, 0, -500);
            Physics.SyncTransforms();
            var random = new FixRandom(84017);
            for (int i = 0; i < 80; i++)
            {
                float x = random.NextInt(-49900, 49901) / 100f,
                    z = random.NextInt(-49900, 49901) / 100f;
                var ray = new Ray(new Vector3(x, 700, z), Vector3.down);
                bool native = collider.Raycast(ray, out RaycastHit result, 800);
                var hit = world.Raycast(V(x, 700, z), -FixVec3.Up, Fix.FromInt(800));
                Assert.That(hit.DidHit, Is.EqualTo(native));
                if (native)
                {
                    Assert.That(System.Math.Abs(hit.Point.Y.ToDouble() - result.point.y), Is.LessThan(0.0003), "sample=" + i);
                }
            }
        }
    }
}
