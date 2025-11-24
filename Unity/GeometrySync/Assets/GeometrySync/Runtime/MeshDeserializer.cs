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
    }
}
