using System.Collections.Generic;
using UnityEngine;

namespace GeometrySync
{
    /// <summary>
    /// GPU Instancing renderer for Geometry Nodes instances (Phase 2/3)
    /// Phase 2: Uses Graphics.DrawMeshInstanced (up to 1023 instances per batch)
    /// Phase 3A: Uses Graphics.DrawMeshInstancedIndirect (unlimited instances, single draw call)
    /// </summary>
    public class GPUInstanceRenderer : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Material for instanced rendering (must enable GPU Instancing)")]
        public Material instanceMaterial;

        [Tooltip("Use GPU Indirect rendering (Phase 3A) instead of batched rendering (Phase 2)")]
        public bool useIndirectRendering = false;

        [Tooltip("Use custom InstancedIndirect shader (Phase 3A only). If false, uses the material's existing shader (e.g., URP/Lit)")]
        public bool useCustomIndirectShader = false;

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

        [Tooltip("Show rendering stats on screen")]
        public bool showDebugInfo = true;

        // Base meshes cache: meshId -> Mesh
        private Dictionary<uint, Mesh> _baseMeshes = new Dictionary<uint, Mesh>();

        // Instance transforms: meshId -> Matrix4x4[]
        private Dictionary<uint, Matrix4x4[]> _instanceTransforms =
            new Dictionary<uint, Matrix4x4[]>();

        // Phase 3A: GPU buffers for indirect rendering
        private Dictionary<uint, ComputeBuffer> _transformBuffers = new Dictionary<uint, ComputeBuffer>();
        private Dictionary<uint, ComputeBuffer> _argsBuffers = new Dictionary<uint, ComputeBuffer>();

        // Bounds for culling (large default to include all instances)
        private Bounds _renderBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

        // GPU capability flags
        private bool _supportsIndirectRendering = false;

        private void Awake()
        {
            // Check GPU capabilities for indirect rendering
            _supportsIndirectRendering = SystemInfo.supportsComputeShaders &&
                                         SystemInfo.supportsInstancing;

            if (useIndirectRendering && !_supportsIndirectRendering)
            {
                Debug.LogWarning("[GPUInstanceRenderer] GPU Indirect rendering not supported on this hardware. " +
                                "Falling back to batched DrawMeshInstanced.");
                useIndirectRendering = false;
            }

            if (logUpdates)
            {
                Debug.Log($"[GPUInstanceRenderer] Initialized - Mode: {(useIndirectRendering ? "Indirect" : "Batched")}");
            }
        }

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

            // Phase 3A: Update ComputeBuffers for indirect rendering
            if (useIndirectRendering && _supportsIndirectRendering)
            {
                UpdateIndirectBuffers(meshId, transforms);
            }

            if (logUpdates)
            {
                Debug.Log($"[GPUInstanceRenderer] Updated {transforms.Length} instances for mesh {meshId}");
            }
        }

        /// <summary>
        /// Update ComputeBuffers for indirect rendering (Phase 3A)
        /// </summary>
        private void UpdateIndirectBuffers(uint meshId, Matrix4x4[] transforms)
        {
            // Update or create transform buffer
            if (!_transformBuffers.ContainsKey(meshId) ||
                _transformBuffers[meshId] == null ||
                _transformBuffers[meshId].count != transforms.Length)
            {
                // Release old buffer if exists
                if (_transformBuffers.ContainsKey(meshId) && _transformBuffers[meshId] != null)
                {
                    _transformBuffers[meshId].Release();
                }

                // Create new buffer: stride = sizeof(float) * 16 (Matrix4x4)
                _transformBuffers[meshId] = new ComputeBuffer(transforms.Length, sizeof(float) * 16);
            }

            // Upload transform data to GPU
            _transformBuffers[meshId].SetData(transforms);

            // Set buffer to material
            if (instanceMaterial != null)
            {
                instanceMaterial.SetBuffer("_TransformBuffer", _transformBuffers[meshId]);
            }

            // Update args buffer for DrawMeshInstancedIndirect
            UpdateArgsBuffer(meshId, transforms.Length);
        }

        /// <summary>
        /// Update args buffer for DrawMeshInstancedIndirect
        /// </summary>
        private void UpdateArgsBuffer(uint meshId, int instanceCount)
        {
            if (!_baseMeshes.TryGetValue(meshId, out Mesh mesh))
                return;

            // Args buffer: [indexCount, instanceCount, startIndex, baseVertex, startInstance]
            uint[] args = new uint[5];
            args[0] = mesh.GetIndexCount(0); // Index count per instance
            args[1] = (uint)instanceCount;    // Instance count
            args[2] = 0;                      // Start index
            args[3] = 0;                      // Base vertex
            args[4] = 0;                      // Start instance

            // Create or update args buffer
            if (!_argsBuffers.ContainsKey(meshId) || _argsBuffers[meshId] == null)
            {
                _argsBuffers[meshId] = new ComputeBuffer(1, args.Length * sizeof(uint),
                                                        ComputeBufferType.IndirectArguments);
            }

            _argsBuffers[meshId].SetData(args);
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

            // Choose rendering path: Indirect (Phase 3A) or Batched (Phase 2)
            if (useIndirectRendering && _supportsIndirectRendering)
            {
                RenderIndirect();
            }
            else
            {
                RenderBatched();
            }
        }

        /// <summary>
        /// Phase 3A: Render using DrawMeshInstancedIndirect (unlimited instances, single draw call)
        /// </summary>
        private void RenderIndirect()
        {
            foreach (var kvp in _instanceTransforms)
            {
                uint meshId = kvp.Key;

                if (!_baseMeshes.TryGetValue(meshId, out Mesh mesh))
                    continue;

                if (!_argsBuffers.ContainsKey(meshId) || _argsBuffers[meshId] == null)
                    continue;

                // Single draw call for ALL instances (no 1023 limit!)
                Graphics.DrawMeshInstancedIndirect(
                    mesh,
                    0, // submesh index
                    instanceMaterial,
                    _renderBounds,
                    _argsBuffers[meshId],
                    0, // args offset
                    null, // material property block
                    shadowCasting,
                    receiveShadows,
                    renderLayer,
                    null // camera (null = all cameras)
                );
            }
        }

        /// <summary>
        /// Phase 2: Render using DrawMeshInstanced (fallback with batching for 1023 limit)
        /// </summary>
        private void RenderBatched()
        {
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

            // Phase 3A: Release ComputeBuffers
            ReleaseAllBuffers();

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

            // Phase 3A: Release buffers for this mesh
            ReleaseBuffersForMesh(meshId);

            if (logUpdates)
            {
                Debug.Log($"[GPUInstanceRenderer] Removed instances for mesh {meshId}");
            }
        }

        /// <summary>
        /// Release ComputeBuffers for a specific mesh
        /// </summary>
        private void ReleaseBuffersForMesh(uint meshId)
        {
            if (_transformBuffers.ContainsKey(meshId) && _transformBuffers[meshId] != null)
            {
                _transformBuffers[meshId].Release();
                _transformBuffers.Remove(meshId);
            }

            if (_argsBuffers.ContainsKey(meshId) && _argsBuffers[meshId] != null)
            {
                _argsBuffers[meshId].Release();
                _argsBuffers.Remove(meshId);
            }
        }

        /// <summary>
        /// Release all ComputeBuffers
        /// </summary>
        private void ReleaseAllBuffers()
        {
            foreach (var buffer in _transformBuffers.Values)
            {
                buffer?.Release();
            }
            _transformBuffers.Clear();

            foreach (var buffer in _argsBuffers.Values)
            {
                buffer?.Release();
            }
            _argsBuffers.Clear();
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
            if (showDebugInfo && _instanceTransforms.Count > 0)
            {
                // Display debug info
                GUILayout.BeginArea(new Rect(10, 200, 350, 250));
                GUILayout.Box("GPU Instance Renderer Stats", GUILayout.Width(340));

                string mode = useIndirectRendering && _supportsIndirectRendering ? "Indirect (Phase 3A)" : "Batched (Phase 2)";
                GUILayout.Label($"Rendering Mode: {mode}", new GUIStyle { normal = { textColor = Color.yellow } });
                GUILayout.Label($"Meshes: {_baseMeshes.Count}", new GUIStyle { normal = { textColor = Color.white } });
                GUILayout.Label($"Total Instances: {GetTotalInstanceCount():N0}", new GUIStyle { normal = { textColor = Color.white } });

                foreach (var kvp in _instanceTransforms)
                {
                    int instanceCount = kvp.Value.Length;
                    int drawCalls = useIndirectRendering && _supportsIndirectRendering ? 1 : Mathf.CeilToInt((float)instanceCount / 1023);
                    GUILayout.Label($"  Mesh {kvp.Key}: {instanceCount:N0} instances ({drawCalls} draw call{(drawCalls > 1 ? "s" : "")})",
                                   new GUIStyle { normal = { textColor = Color.cyan } });
                }

                GUILayout.EndArea();
            }
        }
    }
}
