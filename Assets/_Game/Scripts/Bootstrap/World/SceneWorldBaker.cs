using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap.World
{
    /// <summary>烘焙边界：场景浮点顶点只在这里转换一次，游戏加载规范快照后不再查询 Collider。</summary>
    public static class SceneWorldBaker
    {
        /// <param name="layerMask">参与静态烘焙的 Unity 层位掩码；默认包含所有层。</param>
        /// <param name="excluded">角色等动态对象的碰撞体集合，必须在静态几何检查前排除。</param>
        public static FixedWorld Bake(IEnumerable<Collider> colliders, int layerMask = ~0, ISet<Collider> excluded = null)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            var ordered = new List<Collider>();
            var seen = new HashSet<Collider>();
            foreach (Collider collider in colliders)
            {
                if (collider == null
                    || !seen.Add(collider)
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
                    || (collider.isTrigger && !IsLadder(collider))
                    || (excluded != null
                    && excluded.Contains(collider))
                    || (layerMask & (1 << collider.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (collider.attachedRigidbody != null)
                {
                    throw new NotSupportedException("动态碰撞体必须从静态世界烘焙中排除：" + collider.name);
                }

                ordered.Add(collider);
            }

            // 形状 ID 依赖稳定场景路径与组件顺序，不能依赖 FindObjectsOfType 的枚举顺序。
            ordered.Sort((a, b) => string.CompareOrdinal(Key(a), Key(b)));
            var boxes = new List<FixedWorldBox>();
            var triangles = new List<FixedWorldTriangle>();
            uint id = 1;
            foreach (Collider collider in ordered)
            {
                int layer = collider.gameObject.layer;
                WorldSurfaceAuthoring authoring = collider.GetComponentInParent<WorldSurfaceAuthoring>();
                FixedSurface surface = authoring != null ? authoring.Surfaces : FixedSurface.Solid | FixedSurface.Ground | FixedSurface.Wall | FixedSurface.Grapple;
                if ((surface & FixedSurface.Ladder) != 0 && !(collider is BoxCollider))
                {
                    throw new NotSupportedException("竖直梯子必须使用轴对齐BoxCollider：" + collider.name);
                }
                if (collider.isTrigger)
                {
                    // 梯子触发盒只提供攀爬体积，不能因默认勾选Solid/Ground而变成实体墙或隐形地面。
                    surface = FixedSurface.Ladder;
                }
                if (collider is TerrainCollider terrain)
                {
                    AddTerrain(terrain, triangles, surface, ref id);
                }
                else if (collider is MeshCollider mesh)
                {
                    if (mesh.sharedMesh == null || mesh.convex)
                    {
                        throw new NotSupportedException("只支持有 Mesh 的非凸静态 MeshCollider：" + mesh.name);
                    }

                    Vector3[] vertices = mesh.sharedMesh.vertices;
                    int[] indices = mesh.sharedMesh.triangles;
                    var fixedVertices = new FixVec3[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        fixedVertices[i] = Convert(mesh.transform.TransformPoint(vertices[i]));
                    }

                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Add(triangles, ref id, layer, surface, fixedVertices[indices[i]], fixedVertices[indices[i + 1]], fixedVertices[indices[i + 2]]);
                    }
                }
                else if (collider is BoxCollider box)
                {
                    AddBox(box, boxes, triangles, surface, ref id);
                }
                else
                {
                    throw new NotSupportedException("尚不支持此静态碰撞体，不能静默忽略：" + collider.GetType().Name + " / " + collider.name);
                }

                if (triangles.Count > FixedWorldCodec.MaxTriangleCount || boxes.Count > FixedWorldCodec.MaxShapeCount)
                {
                    throw new InvalidOperationException("烘焙几何超过快照数量上限。");
                }
            }

            return new FixedWorld(boxes.ToArray(), null, triangles.ToArray());
        }

        private static string Key(Collider c)
        {
            string key = "";
            for (Transform t = c.transform; t != null; t = t.parent)
            {
                key = "/" + t.GetSiblingIndex().ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + ":" + t.name + key;
            }

            Collider[] components = c.GetComponents<Collider>();
            int index = Array.IndexOf(components, c);
            return c.gameObject.scene.path + key + "#" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static FixVec3 Convert(Vector3 v) => new FixVec3(Fix.FromFloat(v.x), Fix.FromFloat(v.y), Fix.FromFloat(v.z));
        private static void Add(List<FixedWorldTriangle> triangles, ref uint id, int layer, FixedSurface surface, FixVec3 a, FixVec3 b, FixVec3 c)
        {
            if (triangles.Count >= FixedWorldCodec.MaxTriangleCount)
            {
                throw new InvalidOperationException("烘焙三角形超过数量上限。");
            }

            triangles.Add(new FixedWorldTriangle(id++, layer, surface, a, b, c));
        }

        private static bool IsLadder(Collider collider)
        {
            var authoring = collider.GetComponentInParent<WorldSurfaceAuthoring>();
            return authoring != null && (authoring.Surfaces & FixedSurface.Ladder) != 0;
        }

        private static void AddBox(BoxCollider box, List<FixedWorldBox> boxes, List<FixedWorldTriangle> triangles, FixedSurface surface, ref uint id)
        {
            if ((surface & FixedSurface.Ladder) != 0
                && Mathf.Abs(Vector3.Dot(box.transform.up, Vector3.up)) < 0.99999f)
            {
                // 爬升轴固定为世界Y；即使横倒90°后仍是AABB，也不能误当成一架短竖梯。
                throw new NotSupportedException("梯子本地Y轴必须沿世界竖直方向：" + box.name);
            }
            var vertices = new FixVec3[8];
            Vector3 half = box.size * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                vertices[i] = Convert(box.transform.TransformPoint(box.center + new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z)));
            }

            int minX = int.MaxValue,
                minY = int.MaxValue,
                minZ = int.MaxValue,
                maxX = int.MinValue,
                maxY = int.MinValue,
                maxZ = int.MinValue;
            foreach (var v in vertices)
            {
                minX = System.Math.Min(minX, v.X.Raw);
                minY = System.Math.Min(minY, v.Y.Raw);
                minZ = System.Math.Min(minZ, v.Z.Raw);
                maxX = System.Math.Max(maxX, v.X.Raw);
                maxY = System.Math.Max(maxY, v.Y.Raw);
                maxZ = System.Math.Max(maxZ, v.Z.Raw);
            }

            var corners = new HashSet<int>();
            bool axisAligned = true;
            foreach (var v in vertices)
            {
                if ((v.X.Raw != minX && v.X.Raw != maxX) || (v.Y.Raw != minY && v.Y.Raw != maxY) || (v.Z.Raw != minZ && v.Z.Raw != maxZ))
                {
                    axisAligned = false;
                    break;
                }

                corners.Add((v.X.Raw == maxX ? 1 : 0) | (v.Y.Raw == maxY ? 2 : 0) | (v.Z.Raw == maxZ ? 4 : 0));
            }
            if (!axisAligned && (surface & FixedSurface.Ladder) != 0)
            {
                throw new NotSupportedException("梯子体积不能用旋转三角面近似，请使用世界轴对齐梯盒：" + box.name);
            }

            // 量化后的八个角严格组成 AABB 才保留实体盒；旋转盒不能用更大的包围盒替代。
            if (axisAligned && corners.Count == 8)
            {
                boxes.Add(new FixedWorldBox(
                    id++,
                    box.gameObject.layer,
                    surface,
                    new FixedAabb(new FixVec3(Fix.FromRaw(minX), Fix.FromRaw(minY), Fix.FromRaw(minZ)), new FixVec3(Fix.FromRaw(maxX), Fix.FromRaw(maxY), Fix.FromRaw(maxZ)))));
                return;
            }

            int[] faces =
            {
                0, 2, 3, 0, 3, 1,
                4, 5, 7, 4, 7, 6,
                0, 4, 6, 0, 6, 2,
                1, 3, 7, 1, 7, 5,
                0, 1, 5, 0, 5, 4,
                2, 6, 7, 2, 7, 3
            };
            for (int i = 0; i < faces.Length; i += 3)
            {
                Add(triangles, ref id, box.gameObject.layer, surface, vertices[faces[i]], vertices[faces[i + 1]], vertices[faces[i + 2]]);
            }
        }

        private static void AddTerrain(TerrainCollider collider, List<FixedWorldTriangle> triangles, FixedSurface surface, ref uint id)
        {
            TerrainData data = collider.terrainData;
            if (data == null)
            {
                throw new ArgumentException("TerrainCollider 缺少 TerrainData。");
            }

            if (collider.transform.rotation != Quaternion.identity || collider.transform.lossyScale != Vector3.one)
            {
                throw new NotSupportedException("Terrain 仅支持平移，不能旋转或缩放。");
            }

            int resolution = data.heightmapResolution,
                cells = resolution - 1;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            // Unity GetHoles 的 true 表示存在地表，false 才是洞；索引顺序为 [z, x]。
            bool[,] holes = data.GetHoles(0, 0, data.holesResolution, data.holesResolution);
            var vertices = new FixVec3[resolution, resolution];
            Vector3 position = collider.transform.position,
                size = data.size;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    vertices[z, x] = new FixVec3(
                        Fix.FromDouble(position.x + (double)size.x * x / cells),
                        Fix.FromDouble(position.y + (double)size.y * heights[z, x]),
                        Fix.FromDouble(position.z + (double)size.z * z / cells));
                }
            }

            var used = new bool[cells, cells];
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    if (used[z, x] || !holes[z, x])
                    {
                        continue;
                    }

                    int width = 1,
                        height = 1,
                        h = vertices[z, x].Y.Raw;
                    bool Flat(int xx, int zz) =>
                        vertices[zz, xx].Y.Raw == h && vertices[zz, xx + 1].Y.Raw == h && vertices[zz + 1, xx].Y.Raw == h && vertices[zz + 1, xx + 1].Y.Raw == h;
                    // 仅合并 Raw 高度完全相同的水平单元，斜坡保留原网格，不能用容差抹平地形。
                    if (Flat(x, z))
                    {
                        while (x + width < cells && !used[z, x + width] && holes[z, x + width] && Flat(x + width, z))
                        {
                            width++;
                        }

                        while (z + height < cells)
                        {
                            bool valid = true;
                            for (int i = 0; i < width; i++)
                            {
                                if (used[z + height, x + i] || !holes[z + height, x + i] || !Flat(x + i, z + height))
                                {
                                    valid = false;
                                    break;
                                }
                            }

                            if (!valid)
                            {
                                break;
                            }

                            height++;
                        }
                    }

                    for (int zz = z; zz < z + height; zz++)
                    {
                        for (int xx = x; xx < x + width; xx++)
                        {
                            used[zz, xx] = true;
                        }
                    }

                    // 固定 00→11 对角线。仅合并严格水平的网格矩形，不作有损降采样。
                    Add(triangles, ref id, collider.gameObject.layer, surface, vertices[z, x], vertices[z + height, x], vertices[z + height, x + width]);
                    Add(triangles, ref id, collider.gameObject.layer, surface, vertices[z, x], vertices[z + height, x + width], vertices[z, x + width]);
                }
            }
        }
    }
}
