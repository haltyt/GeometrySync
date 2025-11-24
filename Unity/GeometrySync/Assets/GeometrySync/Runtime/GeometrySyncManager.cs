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

        // Statistics
        private int _meshUpdateCount;
        private float _lastUpdateTime;
        private float _updateInterval;
        private int _lastVertexCount;
        private int _lastTriangleCount;
        private float _averageFps;
        private int _fpsFrameCount;

        public bool IsConnected => _client?.IsConnected ?? false;
        public int MeshUpdateCount => _meshUpdateCount;
        public int LastVertexCount => _lastVertexCount;
        public int LastTriangleCount => _lastTriangleCount;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            // Initialize reconstructor
            _reconstructor = new MeshReconstructor();
            _meshFilter.mesh = _reconstructor.Mesh;

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

            GUILayout.Label($"Updates: {_meshUpdateCount}");
            GUILayout.Label($"Vertices: {_lastVertexCount:N0}");
            GUILayout.Label($"Triangles: {_lastTriangleCount:N0}");
            GUILayout.Label($"Queue: {(_client?.QueuedMeshCount ?? 0)}");

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
