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

        [Header("Transform")]
        [Tooltip("Global position offset for all instances")]
        public Vector3 instanceOffset = Vector3.zero;

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
        private Dictionary<uint, MaterialPropertyBlock> _propertyBlocks = new Dictionary<uint, MaterialPropertyBlock>();

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
                    Debug.Log($"[GPUInstanceRenderer] Replacing existing mesh with ID {meshId}");
                }

                // Destroy old mesh to prevent memory leak
                Mesh oldMesh = _baseMeshes[meshId];
                if (oldMesh != null && oldMesh != mesh)
                {
                    Destroy(oldMesh);
                }
            }

            _baseMeshes[meshId] = mesh;

            if (logUpdates)
            {
                Debug.Log($"[GPUInstanceRenderer] Registered base mesh {meshId}: {mesh.vertexCount} vertices");

                // DEBUG: Log first vertex position to verify mesh data changed
                if (mesh.vertexCount > 0)
                {
                    Vector3 firstVertex = mesh.vertices[0];
                    Debug.Log($"[GPUInstanceRenderer] First vertex: {firstVertex}, Bounds: {mesh.bounds.size}");
                }
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

            // Apply global scale and offset to each transform
            Matrix4x4[] adjustedTransforms = new Matrix4x4[transforms.Length];

            for (int i = 0; i < transforms.Length; i++)
            {
                // Decompose the original transform into TRS (Translation, Rotation, Scale)
                Vector3 position = transforms[i].GetPosition();
                Quaternion rotation = transforms[i].rotation;
                Vector3 scale = transforms[i].lossyScale;

                // DEBUG: Log first transform's scale (no longer modified by instanceScale)
                if (i == 0 && logUpdates)
                {
                    Debug.Log($"[GPUInstanceRenderer] Transform scale (unchanged): {scale}");
                }

                // Apply global offset to the position
                position += instanceOffset;

                // Reconstruct the matrix with unchanged scale and adjusted position
                adjustedTransforms[i] = Matrix4x4.TRS(position, rotation, scale);
            }

            _instanceTransforms[meshId] = adjustedTransforms;

            // Phase 3A: Update ComputeBuffers for indirect rendering
            if (useIndirectRendering && _supportsIndirectRendering)
            {
                UpdateIndirectBuffers(meshId, adjustedTransforms);
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

            // Create or get MaterialPropertyBlock for this mesh
            if (!_propertyBlocks.ContainsKey(meshId))
            {
                _propertyBlocks[meshId] = new MaterialPropertyBlock();
            }

            // Set buffer to property block (per-mesh, not global)
            _propertyBlocks[meshId].SetBuffer("_TransformBuffer", _transformBuffers[meshId]);

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
                {
                    Debug.LogWarning($"[GPUInstanceRenderer] RenderIndirect: No base mesh for ID {meshId}");
                    continue;
                }

                if (!_argsBuffers.ContainsKey(meshId) || _argsBuffers[meshId] == null)
                {
                    Debug.LogWarning($"[GPUInstanceRenderer] RenderIndirect: No args buffer for ID {meshId}");
                    continue;
                }

                if (!_propertyBlocks.ContainsKey(meshId))
                {
                    Debug.LogWarning($"[GPUInstanceRenderer] RenderIndirect: No property block for ID {meshId}");
                    continue;
                }

                if (!_transformBuffers.ContainsKey(meshId) || _transformBuffers[meshId] == null)
                {
                    Debug.LogWarning($"[GPUInstanceRenderer] RenderIndirect: No transform buffer for ID {meshId}");
                    continue;
                }

                if (logUpdates)
                {
                    Debug.Log($"[GPUInstanceRenderer] Drawing {kvp.Value.Length} instances of mesh {meshId}, buffer count: {_transformBuffers[meshId].count}");
                }

                // Single draw call for ALL instances (no 1023 limit!)
                Graphics.DrawMeshInstancedIndirect(
                    mesh,
                    0, // submesh index
                    instanceMaterial,
                    _renderBounds,
                    _argsBuffers[meshId],
                    0, // args offset
                    _propertyBlocks[meshId], // Use property block with _TransformBuffer
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

                // DEBUG: Log transform array details
                if (logUpdates)
                {
                    Debug.Log($"[GPUInstanceRenderer] RenderBatched: meshId={meshId}, total transforms={transforms.Length}");

                    // Log first 3 transform positions to verify they're different
                    for (int i = 0; i < Mathf.Min(3, transforms.Length); i++)
                    {
                        Vector3 pos = transforms[i].GetPosition();
                        Debug.Log($"  Transform[{i}] position: {pos}");
                    }
                }

                // Graphics.DrawMeshInstanced supports max 1023 instances per call
                // Split into batches if needed
                int maxInstancesPerBatch = 1023;
                int batches = Mathf.CeilToInt((float)transforms.Length / maxInstancesPerBatch);

                if (logUpdates)
                {
                    Debug.Log($"[GPUInstanceRenderer] Drawing {batches} batches for {transforms.Length} instances");
                }

                for (int batch = 0; batch < batches; batch++)
                {
                    int startIdx = batch * maxInstancesPerBatch;
                    int count = Mathf.Min(maxInstancesPerBatch,
                                         transforms.Length - startIdx);

                    // Create batch transform array
                    Matrix4x4[] batchTransforms = new Matrix4x4[count];
                    System.Array.Copy(transforms, startIdx, batchTransforms, 0, count);

                    if (logUpdates)
                    {
                        Debug.Log($"[GPUInstanceRenderer] Batch {batch}: startIdx={startIdx}, count={count}");
                    }

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

            if (_propertyBlocks.ContainsKey(meshId))
            {
                _propertyBlocks.Remove(meshId);
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

            _propertyBlocks.Clear();
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
