using System;
using UnityEngine;

namespace GeometrySync
{
    /// <summary>
    /// Mesh data container
    /// </summary>
    public struct MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[] Indices;

        public int VertexCount => Vertices?.Length ?? 0;
        public int TriangleCount => (Indices?.Length ?? 0) / 3;
    }

    /// <summary>
    /// Instance data container for GPU instancing
    /// </summary>
    public struct InstanceData
    {
        public uint MeshId;              // Hash of base mesh name
        public Matrix4x4[] Transforms;   // Instance transform matrices

        public int InstanceCount => Transforms?.Length ?? 0;
    }

    /// <summary>
    /// Deserializes binary mesh data from Blender
    /// </summary>
    public static class MeshDeserializer
    {
        /// <summary>
        /// Deserialize mesh data from binary format
        ///
        /// Binary format:
        /// - Header: vertex_count (uint32), index_count (uint32)
        /// - Vertex data: interleaved [x,y,z, nx,ny,nz, u,v] as float32 (32 bytes per vertex)
        /// - Index data: uint32 array
        /// </summary>
        public static MeshData Deserialize(byte[] data)
        {
            if (data == null || data.Length < 8)
            {
                throw new ArgumentException("Invalid mesh data: too small");
            }

            int offset = 0;

            // Read header
            uint vertexCount = BitConverter.ToUInt32(data, offset);
            offset += 4;

            uint indexCount = BitConverter.ToUInt32(data, offset);
            offset += 4;

            // Validate counts
            if (vertexCount > 10_000_000 || indexCount > 30_000_000)
            {
                throw new ArgumentException($"Mesh too large: {vertexCount} vertices, {indexCount} indices");
            }

            int expectedVertexDataSize = (int)vertexCount * 32; // 8 floats * 4 bytes
            int expectedIndexDataSize = (int)indexCount * 4;    // uint32
            int expectedTotalSize = 8 + expectedVertexDataSize + expectedIndexDataSize;

            if (data.Length < expectedTotalSize)
            {
                throw new ArgumentException($"Invalid mesh data size. Expected {expectedTotalSize}, got {data.Length}");
            }

            // Allocate arrays
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] indices = new int[indexCount];

            // Read interleaved vertex data
            for (int i = 0; i < vertexCount; i++)
            {
                // Position (3 floats)
                float x = BitConverter.ToSingle(data, offset);
                offset += 4;
                float y = BitConverter.ToSingle(data, offset);
                offset += 4;
                float z = BitConverter.ToSingle(data, offset);
                offset += 4;
                vertices[i] = new Vector3(x, y, z);

                // Debug: print first vertex
                if (i == 0)
                {
                    UnityEngine.Debug.Log($"[Deserializer] First vertex: ({x}, {y}, {z})");
                }

                // Normal (3 floats)
                float nx = BitConverter.ToSingle(data, offset);
                offset += 4;
                float ny = BitConverter.ToSingle(data, offset);
                offset += 4;
                float nz = BitConverter.ToSingle(data, offset);
                offset += 4;
                normals[i] = new Vector3(nx, ny, nz);

                // UV (2 floats)
                float u = BitConverter.ToSingle(data, offset);
                offset += 4;
                float v = BitConverter.ToSingle(data, offset);
                offset += 4;
                uvs[i] = new Vector2(u, v);
            }

            // Read indices
            for (int i = 0; i < indexCount; i++)
            {
                indices[i] = (int)BitConverter.ToUInt32(data, offset);
                offset += 4;
            }

            return new MeshData
            {
                Vertices = vertices,
                Normals = normals,
                UVs = uvs,
                Indices = indices
            };
        }

        /// <summary>
        /// Deserialize using Unity's NativeArray for zero-copy (future optimization)
        /// Note: This is a placeholder for Phase 3 optimization
        /// </summary>
        public static MeshData DeserializeNative(byte[] data)
        {
            // This will be implemented in Phase 3 for better performance
            // Currently just calls the standard Deserialize method
            return Deserialize(data);
        }

        /// <summary>
        /// Deserialize instance data from binary format (Phase 2: GPU Instancing)
        ///
        /// Binary format:
        /// - Header: mesh_id (uint32), instance_count (uint32)
        /// - Transform data: array of 4x4 matrices (float32, 16 values per matrix)
        /// </summary>
        public static InstanceData DeserializeInstanceData(byte[] data)
        {
            if (data == null || data.Length < 8)
            {
                throw new ArgumentException("Invalid instance data: too small");
            }

            int offset = 0;

            // Read header
            uint meshId = BitConverter.ToUInt32(data, offset);
            offset += 4;

            uint instanceCount = BitConverter.ToUInt32(data, offset);
            offset += 4;

            // Sanity check: prevent memory issues
            if (instanceCount > 100_000)
            {
                throw new ArgumentException($"Instance count too large: {instanceCount} (max 100,000)");
            }

            // Expected data size: 8 (header) + instanceCount * 16 * 4 (64 bytes per matrix)
            int expectedSize = 8 + (int)instanceCount * 64;
            if (data.Length < expectedSize)
            {
                throw new ArgumentException(
                    $"Invalid instance data size: expected {expectedSize}, got {data.Length}");
            }

            // Read transform matrices (16 floats per matrix)
            Matrix4x4[] transforms = new Matrix4x4[instanceCount];

            for (int i = 0; i < instanceCount; i++)
            {
                // Read 16 floats for 4x4 matrix
                float[] m = new float[16];
                for (int j = 0; j < 16; j++)
                {
                    m[j] = BitConverter.ToSingle(data, offset);
                    offset += 4;
                }

                // Construct Matrix4x4 from column-major data
                // Unity uses column-major order: Matrix4x4(column0, column1, column2, column3)
                transforms[i] = new Matrix4x4(
                    new Vector4(m[0], m[1], m[2], m[3]),      // column 0
                    new Vector4(m[4], m[5], m[6], m[7]),      // column 1
                    new Vector4(m[8], m[9], m[10], m[11]),    // column 2
                    new Vector4(m[12], m[13], m[14], m[15])   // column 3
                );
            }

            return new InstanceData
            {
                MeshId = meshId,
                Transforms = transforms
            };
        }
    }
}
