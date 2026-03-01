using UnityEngine;

namespace GeometrySync
{
    /// <summary>
    /// Main coordinator for GeometrySync system
    /// Manages connection to Blender and mesh updates
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class GeometrySyncManager : MonoBehaviour
    {
        [Header("Connection Settings")]
        [Tooltip("Blender server host address")]
        public string host = "127.0.0.1";

        [Tooltip("Blender server port")]
        public int port = 8080;

        [Tooltip("Auto-connect on start")]
        public bool autoConnect = true;

        [Header("Performance")]
        [Tooltip("Maximum updates per second (0 = unlimited)")]
        [Range(0, 120)]
        public int maxUpdatesPerSecond = 60;

        [Header("Debug")]
        [Tooltip("Show debug statistics")]
        public bool showDebugInfo = true;

        [Tooltip("Log mesh updates")]
        public bool logMeshUpdates = false;

        // Components
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshStreamClient _client;
        private MeshReconstructor _reconstructor;
        private GPUInstanceRenderer _instanceRenderer;

        // Statistics
        private int _meshUpdateCount;
        private int _instanceUpdateCount;
        private float _lastUpdateTime;
        private float _updateInterval;
        private int _lastVertexCount;
        private int _lastTriangleCount;
        private float _averageFps;
        private int _fpsFrameCount;

        public bool IsConnected => _client?.IsConnected ?? false;
        public int MeshUpdateCount => _meshUpdateCount;
        public int InstanceUpdateCount => _instanceUpdateCount;
        public int LastVertexCount => _lastVertexCount;
        public int LastTriangleCount => _lastTriangleCount;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            // Initialize reconstructor
            _reconstructor = new MeshReconstructor();
            _meshFilter.mesh = _reconstructor.Mesh;

            // Initialize instance renderer (Phase 2: GPU Instancing)
            _instanceRenderer = GetComponent<GPUInstanceRenderer>();
            if (_instanceRenderer == null)
            {
                _instanceRenderer = gameObject.AddComponent<GPUInstanceRenderer>();
                Debug.Log("[GeometrySyncManager] Added GPUInstanceRenderer component for Phase 2 instancing support");
            }

            // Assign material from MeshRenderer to GPUInstanceRenderer
            // Phase 3A: Optionally use InstancedIndirect shader if enabled
            if (_meshRenderer.sharedMaterial != null)
            {
                // Phase 3A: Check if we should use the custom indirect shader
                if (_instanceRenderer.useIndirectRendering &&
                    _instanceRenderer.useCustomIndirectShader &&
                    SystemInfo.supportsComputeShaders)
                {
                    Shader indirectShader = Shader.Find("GeometrySync/InstancedIndirect");
                    if (indirectShader != null)
                    {
                        // Change shader on existing material (preserves material settings)
                        _meshRenderer.sharedMaterial.shader = indirectShader;
                        Debug.Log("[GeometrySyncManager] Changed material shader to InstancedIndirect for Phase 3A rendering");
                    }
                    else
                    {
                        Debug.LogWarning("[GeometrySyncManager] InstancedIndirect shader not found, using material's current shader");
                    }
                }
                else if (_instanceRenderer.useIndirectRendering)
                {
                    Debug.Log($"[GeometrySyncManager] Using existing material shader: {_meshRenderer.sharedMaterial.shader.name}");
                }

                // Use the same material for instance rendering
                _instanceRenderer.instanceMaterial = _meshRenderer.sharedMaterial;

                // Enable GPU Instancing on the material if not already enabled
                if (!_meshRenderer.sharedMaterial.enableInstancing)
                {
                    _meshRenderer.sharedMaterial.enableInstancing = true;
                    Debug.Log("[GeometrySyncManager] Enabled GPU Instancing on material: " + _meshRenderer.sharedMaterial.name);
                }
            }
            else
            {
                Debug.LogWarning("[GeometrySyncManager] No material found on MeshRenderer. Please assign a material for GPU Instancing.");
            }

            // Calculate update interval
            _updateInterval = maxUpdatesPerSecond > 0 ? 1f / maxUpdatesPerSecond : 0f;

            Debug.Log("GeometrySync Manager initialized");
        }

        private void Start()
        {
            if (autoConnect)
            {
                Connect();
            }
        }

        private void Update()
        {
            if (_client == null || !_client.IsConnected)
                return;

            // Throttle updates if needed
            if (_updateInterval > 0f && Time.time - _lastUpdateTime < _updateInterval)
                return;

            // Try to get mesh from queue
            if (_client.TryGetMesh(out MeshData meshData))
            {
                Debug.Log($"[GeometrySyncManager] Got mesh from queue: {meshData.VertexCount} vertices");
                _lastUpdateTime = Time.time;
                UpdateMesh(meshData);
            }

            // Process instance data (Phase 2: GPU Instancing)
            if (_client.TryGetInstanceData(out InstanceData instanceData))
            {
                Debug.Log($"[GeometrySyncManager] Got instance data: {instanceData.InstanceCount} instances for mesh {instanceData.MeshId}");
                _lastUpdateTime = Time.time;
                UpdateInstances(instanceData);
            }
        }

        private void UpdateMesh(MeshData meshData)
        {
            try
            {
                _reconstructor.UpdateMesh(meshData);

                _meshUpdateCount++;
                _lastVertexCount = meshData.VertexCount;
                _lastTriangleCount = meshData.TriangleCount;

                // Calculate FPS
                _fpsFrameCount++;
                if (_fpsFrameCount >= 10)
                {
                    _averageFps = _fpsFrameCount / (Time.time - _lastUpdateTime + 0.0001f);
                    _fpsFrameCount = 0;
                }

                if (logMeshUpdates)
                {
                    Debug.Log($"Mesh updated: {_lastVertexCount} vertices, {_lastTriangleCount} triangles");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to update mesh: {e}");
            }
        }

        private void UpdateInstances(InstanceData instanceData)
        {
            try
            {
                // Register base mesh if we received it recently
                // The base mesh should arrive before or with the instance data
                // We use the reconstructor's mesh as the base mesh for now
                // In a full implementation, we would maintain a dictionary of meshes by ID

                uint meshId = instanceData.MeshId;

                Debug.Log($"[GeometrySyncManager] UpdateInstances called for mesh {meshId}, {instanceData.InstanceCount} instances");
                Debug.Log($"[GeometrySyncManager] Current mesh vertex count: {(_reconstructor.Mesh != null ? _reconstructor.Mesh.vertexCount : 0)}");
                Debug.Log($"[GeometrySyncManager] Has mesh registered: {_instanceRenderer.HasMesh(meshId)}");

                // Always update base mesh if we have new mesh data
                // This ensures scale changes and other mesh modifications are applied
                if (_reconstructor.Mesh != null && _reconstructor.Mesh.vertexCount > 0)
                {
                    bool isNew = !_instanceRenderer.HasMesh(meshId);
                    string action = isNew ? "Registering" : "Updating";

                    // Create a copy of the mesh to avoid reference issues
                    Mesh meshCopy = new Mesh();
                    meshCopy.name = $"InstanceBaseMesh_{meshId}";
                    meshCopy.vertices = _reconstructor.Mesh.vertices;
                    meshCopy.normals = _reconstructor.Mesh.normals;
                    meshCopy.uv = _reconstructor.Mesh.uv;
                    meshCopy.triangles = _reconstructor.Mesh.triangles;
                    meshCopy.RecalculateBounds();

                    Debug.Log($"[GeometrySyncManager] {action} base mesh {meshId} with {meshCopy.vertexCount} vertices");
                    _instanceRenderer.RegisterBaseMesh(meshId, meshCopy);
                }
                else
                {
                    Debug.LogWarning($"[GeometrySyncManager] Cannot register base mesh {meshId}: no mesh available");
                    return;
                }

                // Update instance transforms
                Debug.Log($"[GeometrySyncManager] Calling UpdateInstances for mesh {meshId}");
                _instanceRenderer.UpdateInstances(meshId, instanceData.Transforms);

                _instanceUpdateCount++;

                if (logMeshUpdates)
                {
                    Debug.Log($"Instances updated: {instanceData.InstanceCount} instances for mesh {meshId}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to update instances: {e}");
            }
        }

        /// <summary>
        /// Connect to Blender server
        /// </summary>
        public void Connect()
        {
            if (_client != null)
            {
                Debug.LogWarning("Already connected or connecting");
                return;
            }

            _client = new MeshStreamClient(host, port);
            _client.Connect();

            Debug.Log($"Connecting to Blender at {host}:{port}");
        }

        /// <summary>
        /// Disconnect from Blender server
        /// </summary>
        public void Disconnect()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
                Debug.Log("Disconnected from Blender");
            }
        }

        private void OnDestroy()
        {
            Disconnect();
            _reconstructor?.Dispose();
        }

        private void OnGUI()
        {
            if (!showDebugInfo)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>GeometrySync Status</b>");
            GUILayout.Space(5);

            string status = IsConnected ? "<color=green>Connected</color>" : "<color=red>Disconnected</color>";
            GUILayout.Label($"Status: {status}");
            GUILayout.Label($"Server: {host}:{port}");
            GUILayout.Space(5);

            GUILayout.Label($"Mesh Updates: {_meshUpdateCount}");
            GUILayout.Label($"Instance Updates: {_instanceUpdateCount}");
            GUILayout.Label($"Vertices: {_lastVertexCount:N0}");
            GUILayout.Label($"Triangles: {_lastTriangleCount:N0}");
            GUILayout.Label($"Mesh Queue: {(_client?.QueuedMeshCount ?? 0)}");
            GUILayout.Label($"Instance Queue: {(_client?.QueuedInstanceCount ?? 0)}");

            if (_instanceRenderer != null)
            {
                int totalInstances = _instanceRenderer.GetTotalInstanceCount();
                if (totalInstances > 0)
                {
                    GUILayout.Label($"<color=yellow>Instances: {totalInstances:N0}</color>");
                }
            }

            if (_averageFps > 0)
            {
                GUILayout.Label($"FPS: {_averageFps:F1}");
            }

            GUILayout.Space(5);

            if (!IsConnected)
            {
                if (GUILayout.Button("Connect"))
                {
                    Connect();
                }
            }
            else
            {
                if (GUILayout.Button("Disconnect"))
                {
                    Disconnect();
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Update interval when max FPS changes
            _updateInterval = maxUpdatesPerSecond > 0 ? 1f / maxUpdatesPerSecond : 0f;
        }
#endif
    }
}
