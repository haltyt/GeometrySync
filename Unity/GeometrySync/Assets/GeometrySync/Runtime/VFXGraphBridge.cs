using UnityEngine;
using UnityEngine.VFX;

namespace GeometrySync
{
    /// <summary>
    /// Bridge component to forward GeometrySync instance data to VFX Graph
    /// Allows VFX Graph to spawn particles at instance positions
    /// Supports both Texture2D (recommended for VFX Graph 17.0.4+) and GraphicsBuffer modes
    /// </summary>
    [RequireComponent(typeof(VisualEffect))]
    public class VFXGraphBridge : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("GPUInstanceRenderer to read instance data from")]
        public GPUInstanceRenderer instanceRenderer;

        [Header("VFX Settings")]
        [Tooltip("Update VFX every N frames (0 = every frame)")]
        [Range(0, 10)]
        public int updateInterval = 1;

        [Tooltip("Maximum number of particles to spawn")]
        public int maxParticles = 10000;

        [Tooltip("Spawn rate multiplier")]
        [Range(0f, 10f)]
        public float spawnRate = 1f;

        [Tooltip("Use Texture2D instead of GraphicsBuffer (recommended for VFX Graph 17.0.4+)")]
        public bool useTexture2D = true;

        [Header("Debug")]
        [Tooltip("Log VFX updates")]
        public bool logUpdates = false;

        // Components
        private VisualEffect _vfx;
        private int _frameCounter = 0;
        private bool _hasInitialized = false;

        // VFX Graph property names
        private static readonly int PropPositionBuffer = Shader.PropertyToID("PositionBuffer");
        private static readonly int PropPositionTexture = Shader.PropertyToID("PositionTexture");
        private static readonly int PropInstanceCount = Shader.PropertyToID("InstanceCount");
        private static readonly int PropSpawnRate = Shader.PropertyToID("SpawnRate");
        private static readonly int PropTextureWidth = Shader.PropertyToID("TextureWidth");

        // GraphicsBuffer mode (legacy)
        private GraphicsBuffer _positionBuffer;

        // Texture2D mode (recommended)
        private Texture2D _positionTexture;

        // Shared data
        private Vector3[] _positions;
        private int _lastInstanceCount = -1;

        private void Awake()
        {
            _vfx = GetComponent<VisualEffect>();

            if (instanceRenderer == null)
            {
                Debug.LogWarning("[VFXGraphBridge] No GPUInstanceRenderer assigned. Please assign one in the inspector.");
            }

            if (logUpdates)
            {
                string mode = useTexture2D ? "Texture2D" : "GraphicsBuffer";
                Debug.Log($"[VFXGraphBridge] Initialized with {mode} mode");
            }
        }

        private void Update()
        {
            if (instanceRenderer == null || _vfx == null)
                return;

            // Throttle updates if needed
            _frameCounter++;
            if (updateInterval > 0 && _frameCounter % updateInterval != 0)
                return;

            UpdateVFXFromInstances();
        }

        private void UpdateVFXFromInstances()
        {
            // Get instance transforms from GPUInstanceRenderer
            int totalInstances = instanceRenderer.GetTotalInstanceCount();

            if (totalInstances == 0)
            {
                if (logUpdates)
                {
                    Debug.Log("[VFXGraphBridge] No instances available");
                }
                return;
            }

            // Limit to max particles
            int instanceCount = Mathf.Min(totalInstances, maxParticles);

            // Extract positions from instance transforms
            if (_positions == null || _positions.Length != instanceCount)
            {
                _positions = new Vector3[instanceCount];
            }

            int posIndex = 0;
            foreach (var meshId in GetInstanceMeshIds())
            {
                var transforms = GetInstanceTransforms(meshId);
                if (transforms == null)
                    continue;

                foreach (var transform in transforms)
                {
                    if (posIndex >= instanceCount)
                        break;

                    // Extract position from transform matrix
                    _positions[posIndex] = new Vector3(
                        transform.m03,
                        transform.m13,
                        transform.m23
                    );
                    posIndex++;
                }

                if (posIndex >= instanceCount)
                    break;
            }

            // Choose rendering path based on useTexture2D flag
            if (useTexture2D)
            {
                UpdateVFXWithTexture2D(instanceCount);
            }
            else
            {
                UpdateVFXWithGraphicsBuffer(instanceCount);
            }

            // Set common VFX properties
            _vfx.SetInt(PropInstanceCount, instanceCount);

            // Only set SpawnRate in Texture2D mode (not needed for GraphicsBuffer/Sample Buffer mode)
            if (useTexture2D)
            {
                _vfx.SetFloat(PropSpawnRate, spawnRate * instanceCount);
            }

            // Reinit VFX if instance count changed (important for Single Burst mode)
            if (_lastInstanceCount != instanceCount)
            {
                _vfx.Reinit();
                _lastInstanceCount = instanceCount;

                if (logUpdates)
                {
                    Debug.Log($"[VFXGraphBridge] VFX Reinit() called due to instance count change: {_lastInstanceCount} -> {instanceCount}");
                }
            }

            if (logUpdates)
            {
                string mode = useTexture2D ? "Texture2D" : "GraphicsBuffer";
                Debug.Log($"[VFXGraphBridge] Updated VFX with {instanceCount} positions ({mode} mode)");
            }
        }

        /// <summary>
        /// Update VFX using Texture2D (recommended for VFX Graph 17.0.4+)
        /// </summary>
        private void UpdateVFXWithTexture2D(int instanceCount)
        {
            // Calculate texture dimensions (square texture)
            int texWidth = Mathf.CeilToInt(Mathf.Sqrt(instanceCount));
            int texHeight = texWidth;

            // Create or resize texture
            if (_positionTexture == null ||
                _positionTexture.width != texWidth ||
                _positionTexture.height != texHeight)
            {
                if (_positionTexture != null)
                {
                    Destroy(_positionTexture);
                }

                _positionTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false);
                _positionTexture.filterMode = FilterMode.Point;
                _positionTexture.wrapMode = TextureWrapMode.Clamp;
            }

            // Pack positions into texture pixels
            Color[] colors = new Color[texWidth * texHeight];
            for (int i = 0; i < instanceCount; i++)
            {
                colors[i] = new Color(
                    _positions[i].x,
                    _positions[i].y,
                    _positions[i].z,
                    1.0f
                );
            }

            // Fill remaining pixels with zeros
            for (int i = instanceCount; i < colors.Length; i++)
            {
                colors[i] = Color.clear;
            }

            _positionTexture.SetPixels(colors);
            _positionTexture.Apply();

            // Set VFX properties
            _vfx.SetTexture(PropPositionTexture, _positionTexture);
            _vfx.SetInt(PropTextureWidth, texWidth);
        }

        /// <summary>
        /// Update VFX using GraphicsBuffer (legacy, may not work with VFX Graph 17.0.4+)
        /// </summary>
        private void UpdateVFXWithGraphicsBuffer(int instanceCount)
        {
            // Update or create GraphicsBuffer
            if (_positionBuffer == null || _positionBuffer.count != instanceCount)
            {
                _positionBuffer?.Release();
                _positionBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    instanceCount,
                    sizeof(float) * 3 // Vector3
                );
            }

            // Upload position data to GPU
            _positionBuffer.SetData(_positions);

            // Set VFX Graph properties
            _vfx.SetGraphicsBuffer(PropPositionBuffer, _positionBuffer);

            if (logUpdates)
            {
                Debug.Log($"[VFXGraphBridge] GraphicsBuffer created: count={_positionBuffer.count}, stride={_positionBuffer.stride}");
                Debug.Log($"[VFXGraphBridge] First 3 positions: {_positions[0]}, {_positions[1]}, {_positions[2]}");
            }
        }

        /// <summary>
        /// Get all mesh IDs from instance renderer
        /// </summary>
        private uint[] GetInstanceMeshIds()
        {
            // Use reflection to access private _instanceTransforms dictionary
            var instanceTransformsField = typeof(GPUInstanceRenderer)
                .GetField("_instanceTransforms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (instanceTransformsField == null)
            {
                Debug.LogError("[VFXGraphBridge] Failed to access _instanceTransforms field");
                return new uint[0];
            }

            var instanceTransforms = instanceTransformsField.GetValue(instanceRenderer)
                as System.Collections.Generic.Dictionary<uint, Matrix4x4[]>;

            if (instanceTransforms == null)
                return new uint[0];

            var keys = new uint[instanceTransforms.Count];
            instanceTransforms.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>
        /// Get instance transforms for a specific mesh ID
        /// </summary>
        private Matrix4x4[] GetInstanceTransforms(uint meshId)
        {
            var instanceTransformsField = typeof(GPUInstanceRenderer)
                .GetField("_instanceTransforms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (instanceTransformsField == null)
                return null;

            var instanceTransforms = instanceTransformsField.GetValue(instanceRenderer)
                as System.Collections.Generic.Dictionary<uint, Matrix4x4[]>;

            if (instanceTransforms == null || !instanceTransforms.TryGetValue(meshId, out var transforms))
                return null;

            return transforms;
        }

        private void OnDestroy()
        {
            _positionBuffer?.Release();
            _positionBuffer = null;

            if (_positionTexture != null)
            {
                Destroy(_positionTexture);
                _positionTexture = null;
            }
        }

        private void Start()
        {
            // Start playing VFX - Reinit() will be called automatically when instances arrive
            if (_vfx != null)
            {
                _vfx.Play();
                _hasInitialized = true;

                if (logUpdates)
                {
                    Debug.Log("[VFXGraphBridge] VFX started - waiting for instance data");
                }
            }
        }

        private void OnDisable()
        {
            // Stop VFX when disabled
            if (_vfx != null)
            {
                _vfx.Stop();
            }
            _hasInitialized = false;
        }

        private void OnEnable()
        {
            // Don't auto-play here - wait for Start() to initialize first
            // This prevents playing with InstanceCount=0
        }
    }
}
