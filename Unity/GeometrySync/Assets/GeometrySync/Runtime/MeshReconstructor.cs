using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometrySync
{
    /// <summary>
    /// Reconstructs Unity mesh from streamed data using modern Mesh API
    /// Optimized for Unity 6000+ with zero-copy NativeArray updates
    /// </summary>
    public class MeshReconstructor
    {
        private Mesh _mesh;
        private NativeArray<Vector3> _vertexBuffer;
        private NativeArray<Vector3> _normalBuffer;
        private NativeArray<Vector2> _uvBuffer;
        private NativeArray<int> _indexBuffer;
        private bool _isInitialized;
        private int _currentVertexCapacity;
        private int _currentIndexCapacity;

        public Mesh Mesh => _mesh;

        public MeshReconstructor()
        {
            _mesh = new Mesh
            {
                name = "GeometrySync Mesh"
            };
            _mesh.MarkDynamic(); // Hint to Unity that this mesh will be updated frequently
        }

        /// <summary>
        /// Update mesh with new data from stream
        /// Uses traditional API for reliability (Modern API for Phase 3)
        /// </summary>
        public void UpdateMesh(MeshData meshData)
        {
            int vertexCount = meshData.VertexCount;
            int indexCount = meshData.Indices.Length;

            if (vertexCount == 0 || indexCount == 0)
            {
                Debug.LogWarning("Received empty mesh data");
                return;
            }

            // Use traditional API for now - it's more reliable
            UpdateMeshTraditional(meshData);
        }

        private void ReallocateBuffers(int vertexCount, int indexCount)
        {
            // Dispose old buffers
            DisposeBuffers();

            // Allocate with some headroom to avoid frequent reallocations
            _currentVertexCapacity = Mathf.NextPowerOfTwo(vertexCount);
            _currentIndexCapacity = Mathf.NextPowerOfTwo(indexCount);

            _vertexBuffer = new NativeArray<Vector3>(_currentVertexCapacity, Allocator.Persistent);
            _normalBuffer = new NativeArray<Vector3>(_currentVertexCapacity, Allocator.Persistent);
            _uvBuffer = new NativeArray<Vector2>(_currentVertexCapacity, Allocator.Persistent);
            _indexBuffer = new NativeArray<int>(_currentIndexCapacity, Allocator.Persistent);

            _isInitialized = true;

            Debug.Log($"Allocated mesh buffers: {_currentVertexCapacity} vertices, {_currentIndexCapacity} indices");
        }

        private void UpdateMeshBuffers(int vertexCount, int indexCount)
        {
            // Clear mesh
            _mesh.Clear();

            // Set vertex buffer layout
            var vertexAttributes = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
            };

            _mesh.SetVertexBufferParams(vertexCount, vertexAttributes);

            // Set vertex data
            _mesh.SetVertexBufferData(_vertexBuffer, 0, 0, vertexCount, 0,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices |
                MeshUpdateFlags.DontNotifyMeshUsers);

            // Set index buffer
            _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            _mesh.SetIndexBufferData(_indexBuffer, 0, 0, indexCount,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);

            // Set submesh
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
            {
                firstVertex = 0,
                vertexCount = vertexCount
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            // Recalculate bounds
            _mesh.RecalculateBounds();

            // Apply normals separately for proper lighting
            _mesh.SetNormals(_normalBuffer.GetSubArray(0, vertexCount));
            _mesh.SetUVs(0, _uvBuffer.GetSubArray(0, vertexCount));
        }

        /// <summary>
        /// Alternative update method using traditional API (fallback)
        /// </summary>
        public void UpdateMeshTraditional(MeshData meshData)
        {
            _mesh.Clear();

            // Set index format FIRST before setting any data
            _mesh.indexFormat = meshData.VertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            _mesh.vertices = meshData.Vertices;
            _mesh.normals = meshData.Normals;
            _mesh.uv = meshData.UVs;

            // Use SetIndices for 32-bit index support
            _mesh.SetIndices(meshData.Indices, MeshTopology.Triangles, 0);

            _mesh.RecalculateBounds();
        }

        public void Dispose()
        {
            DisposeBuffers();

            if (_mesh != null)
            {
                Object.Destroy(_mesh);
                _mesh = null;
            }
        }

        private void DisposeBuffers()
        {
            if (_vertexBuffer.IsCreated) _vertexBuffer.Dispose();
            if (_normalBuffer.IsCreated) _normalBuffer.Dispose();
            if (_uvBuffer.IsCreated) _uvBuffer.Dispose();
            if (_indexBuffer.IsCreated) _indexBuffer.Dispose();
        }

        ~MeshReconstructor()
        {
            Dispose();
        }
    }
}
