using System.Collections.Generic;
using UnityEngine;

namespace GeometrySync
{
    /// <summary>
    /// GPU Instancing renderer for Geometry Nodes instances (Phase 2)
    /// Uses Graphics.DrawMeshInstanced for efficient rendering of thousands of instances
    /// </summary>
    public class GPUInstanceRenderer : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Material for instanced rendering (must enable GPU Instancing)")]
        public Material instanceMaterial;

        [Tooltip("Shadow casting mode")]
        public UnityEngine.Rendering.ShadowCastingMode shadowCasting =
            UnityEngine.Rendering.ShadowCastingMode.On;

        [Tooltip("Receive shadows")]
        public bool receiveShadows = true;

        [Tooltip("Layer for instanced rendering")]
        public int renderLayer = 0;

        [Header("Debug")]
        [Tooltip("Log instance updates")]
        public bool logUpdates = false;

        // Base meshes cache: meshId -> Mesh
        private Dictionary<uint, Mesh> _baseMeshes = new Dictionary<uint, Mesh>();

        // Instance transforms: meshId -> Matrix4x4[]
        private Dictionary<uint, Matrix4x4[]> _instanceTransforms =
            new Dictionary<uint, Matrix4x4[]>();

        // Bounds for culling (large default to include all instances)
        private Bounds _renderBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

        /// <summary>
        /// Register a base mesh that instances will reference
        /// </summary>
        /// <param name="meshId">Unique ID for the mesh (hash of mesh name)</param>
        /// <param name="mesh">The base mesh to be instanced</param>
        public void RegisterBaseMesh(uint meshId, Mesh mesh)
        {
            if (_baseMeshes.ContainsKey(meshId))
            {
                if (logUpdates)
                {
                    Debug.LogWarning($"[GPUInstanceRenderer] Replacing existing mesh with ID {meshId}");
                }
            }

            _baseMeshes[meshId] = mesh;

            if (logUpdates)
            {
                Debug.Log($"[GPUInstanceRenderer] Registered base mesh {meshId}: {mesh.vertexCount} vertices");
            }
        }

        /// <summary>
        /// Update instance transforms for a specific mesh
        /// </summary>
        /// <param name="meshId">ID of the base mesh</param>
        /// <param name="transforms">Array of transform matrices for each instance</param>
        public void UpdateInstances(uint meshId, Matrix4x4[] transforms)
        {
            if (!_baseMeshes.ContainsKey(meshId))
            {
                Debug.LogWarning($"[GPUInstanceRenderer] No base mesh registered for ID {meshId}. " +
                                "Make sure to call RegisterBaseMesh() before UpdateInstances().");
                return;
            }

            if (transforms == null || transforms.Length == 0)
            {
                Debug.LogWarning($"[GPUInstanceRenderer] Empty transforms array for mesh {meshId}");
                return;
            }

            _instanceTransforms[meshId] = transforms;

            if (logUpdates)
            {
                Debug.Log($"[GPUInstanceRenderer] Updated {transforms.Length} instances for mesh {meshId}");
            }
        }

        /// <summary>
        /// Render all instances using GPU instancing
        /// Called every frame by Unity
        /// </summary>
        private void Update()
        {
            if (instanceMaterial == null)
            {
                Debug.LogWarning("[GPUInstanceRenderer] No instance material assigned. " +
                                "Please assign a material with GPU Instancing enabled.");
                return;
            }

            // Check if material has GPU instancing enabled
            if (!instanceMaterial.enableInstancing)
            {
                Debug.LogWarning("[GPUInstanceRenderer] Material does not have GPU Instancing enabled. " +
                                "Enable it in the material inspector.");
            }

            // Draw each mesh's instances
            int drawCallCount = 0;
            foreach (var kvp in _instanceTransforms)
            {
                uint meshId = kvp.Key;
                Matrix4x4[] transforms = kvp.Value;

                if (!_baseMeshes.TryGetValue(meshId, out Mesh mesh))
                {
                    Debug.LogWarning($"[GPUInstanceRenderer] Mesh {meshId} not found in base meshes");
                    continue;
                }

                if (transforms == null || transforms.Length == 0)
                {
                    Debug.LogWarning($"[GPUInstanceRenderer] No transforms for mesh {meshId}");
                    continue;
                }

                // Graphics.DrawMeshInstanced supports max 1023 instances per call
                // Split into batches if needed
                int maxInstancesPerBatch = 1023;
                int batches = Mathf.CeilToInt((float)transforms.Length / maxInstancesPerBatch);

                for (int batch = 0; batch < batches; batch++)
                {
                    int startIdx = batch * maxInstancesPerBatch;
                    int count = Mathf.Min(maxInstancesPerBatch,
                                         transforms.Length - startIdx);

                    // Create batch transform array
                    Matrix4x4[] batchTransforms = new Matrix4x4[count];
                    System.Array.Copy(transforms, startIdx, batchTransforms, 0, count);

                    // Draw instanced mesh
                    Graphics.DrawMeshInstanced(
                        mesh,
                        0, // submesh index
                        instanceMaterial,
                        batchTransforms,
                        count,
                        null, // material property block
                        shadowCasting,
                        receiveShadows,
                        renderLayer,
                        null, // camera (null = all cameras)
                        UnityEngine.Rendering.LightProbeUsage.BlendProbes,
                        null  // light probe proxy volume
                    );

                    drawCallCount++;
                }
            }
        }

        /// <summary>
        /// Clear all instance data
        /// </summary>
        public void Clear()
        {
            _instanceTransforms.Clear();
            _baseMeshes.Clear();

            if (logUpdates)
            {
                Debug.Log("[GPUInstanceRenderer] Cleared all instance data");
            }
        }

        /// <summary>
        /// Remove instances for a specific mesh
        /// </summary>
        /// <param name="meshId">ID of the mesh to remove</param>
        public void RemoveInstances(uint meshId)
        {
            _instanceTransforms.Remove(meshId);
            _baseMeshes.Remove(meshId);

            if (logUpdates)
            {
                Debug.Log($"[GPUInstanceRenderer] Removed instances for mesh {meshId}");
            }
        }

        /// <summary>
        /// Get current instance count for a mesh
        /// </summary>
        public int GetInstanceCount(uint meshId)
        {
            if (_instanceTransforms.TryGetValue(meshId, out Matrix4x4[] transforms))
            {
                return transforms?.Length ?? 0;
            }
            return 0;
        }

        /// <summary>
        /// Get total instance count across all meshes
        /// </summary>
        public int GetTotalInstanceCount()
        {
            int total = 0;
            foreach (var transforms in _instanceTransforms.Values)
            {
                total += transforms?.Length ?? 0;
            }
            return total;
        }

        /// <summary>
        /// Check if a mesh is registered
        /// </summary>
        public bool HasMesh(uint meshId)
        {
            return _baseMeshes.ContainsKey(meshId);
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void OnDisable()
        {
            // Instances will stop rendering when component is disabled
        }

        private void OnGUI()
        {
            if (logUpdates && _instanceTransforms.Count > 0)
            {
                // Display debug info
                GUILayout.BeginArea(new Rect(10, 10, 300, 200));
                GUILayout.Label($"GPU Instance Renderer Stats:", new GUIStyle { normal = { textColor = Color.white } });
                GUILayout.Label($"Meshes: {_baseMeshes.Count}");
                GUILayout.Label($"Total Instances: {GetTotalInstanceCount()}");

                foreach (var kvp in _instanceTransforms)
                {
                    GUILayout.Label($"  Mesh {kvp.Key}: {kvp.Value.Length} instances");
                }
                GUILayout.EndArea();
            }
        }
    }
}
