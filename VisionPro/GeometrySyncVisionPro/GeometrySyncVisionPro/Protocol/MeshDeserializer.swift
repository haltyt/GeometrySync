import Foundation
import simd

/// Deserializes binary mesh data from Blender.
/// Port of Unity/GeometrySync/.../MeshDeserializer.cs
enum MeshDeserializer {

    // MARK: - Validation limits

    static let maxVertexCount: UInt32 = 10_000_000
    static let maxIndexCount: UInt32 = 30_000_000
    static let maxInstanceCount: UInt32 = 100_000

    // MARK: - Mesh deserialization

    /// Deserialize mesh data from binary format.
    ///
    /// Binary format:
    /// - Header: vertexCount (uint32 LE), indexCount (uint32 LE)
    /// - Vertex data: interleaved [x,y,z, nx,ny,nz, u,v] as float32 LE (32 bytes per vertex)
    /// - Index data: uint32 LE array
    /// - **Winding order reversal**: indices are swapped (i0, i2, i1) for RealityKit right-hand system
    static func deserializeMesh(_ data: Data) throws -> MeshData {
        guard data.count >= 8 else {
            throw DeserializerError.dataTooSmall(data.count)
        }

        return try data.withUnsafeBytes { raw in
            let ptr = raw.baseAddress!
            var offset = 0

            // Read header
            let vertexCount = readUInt32(ptr, offset: &offset)
            let indexCount = readUInt32(ptr, offset: &offset)

            // Validate counts
            guard vertexCount <= maxVertexCount, indexCount <= maxIndexCount else {
                throw DeserializerError.meshTooLarge(
                    vertices: Int(vertexCount), indices: Int(indexCount))
            }

            let expectedVertexDataSize = Int(vertexCount) * 32  // 8 floats × 4 bytes
            let expectedIndexDataSize = Int(indexCount) * 4
            let expectedTotalSize = 8 + expectedVertexDataSize + expectedIndexDataSize

            guard data.count >= expectedTotalSize else {
                throw DeserializerError.invalidDataSize(
                    expected: expectedTotalSize, got: data.count)
            }

            // Allocate arrays
            let vCount = Int(vertexCount)
            let iCount = Int(indexCount)
            var positions = [SIMD3<Float>]()
            positions.reserveCapacity(vCount)
            var normals = [SIMD3<Float>]()
            normals.reserveCapacity(vCount)
            var uvs = [SIMD2<Float>]()
            uvs.reserveCapacity(vCount)

            // Read interleaved vertex data
            for _ in 0..<vCount {
                let x = readFloat(ptr, offset: &offset)
                let y = readFloat(ptr, offset: &offset)
                let z = readFloat(ptr, offset: &offset)
                positions.append(SIMD3<Float>(x, y, z))

                let nx = readFloat(ptr, offset: &offset)
                let ny = readFloat(ptr, offset: &offset)
                let nz = readFloat(ptr, offset: &offset)
                normals.append(SIMD3<Float>(nx, ny, nz))

                let u = readFloat(ptr, offset: &offset)
                let v = readFloat(ptr, offset: &offset)
                uvs.append(SIMD2<Float>(u, v))
            }

            // Read indices with winding order reversal for RealityKit (right-hand system)
            // Blender→Unity produces left-hand winding (i0, i1, i2)
            // RealityKit needs right-hand winding: (i0, i2, i1)
            var indices = [UInt32](repeating: 0, count: iCount)
            let triangleCount = iCount / 3
            for tri in 0..<triangleCount {
                let baseIdx = tri * 3
                let i0 = readUInt32(ptr, offset: &offset)
                let i1 = readUInt32(ptr, offset: &offset)
                let i2 = readUInt32(ptr, offset: &offset)
                indices[baseIdx]     = i0
                indices[baseIdx + 1] = i2  // swapped
                indices[baseIdx + 2] = i1  // swapped
            }
            // Handle remaining indices (if indexCount is not a multiple of 3)
            let remaining = iCount - triangleCount * 3
            for _ in 0..<remaining {
                indices.append(readUInt32(ptr, offset: &offset))
            }

            return MeshData(
                positions: positions,
                normals: normals,
                uvs: uvs,
                indices: indices
            )
        }
    }

    // MARK: - Instance deserialization

    /// Deserialize instance data from binary format.
    ///
    /// Binary format:
    /// - Header: meshId (uint32 LE), instanceCount (uint32 LE)
    /// - Transform data: array of 4x4 matrices (16 × float32 LE per matrix, column-major)
    static func deserializeInstances(_ data: Data) throws -> InstanceData {
        guard data.count >= 8 else {
            throw DeserializerError.dataTooSmall(data.count)
        }

        return try data.withUnsafeBytes { raw in
            let ptr = raw.baseAddress!
            var offset = 0

            let meshId = readUInt32(ptr, offset: &offset)
            let instanceCount = readUInt32(ptr, offset: &offset)

            guard instanceCount <= maxInstanceCount else {
                throw DeserializerError.instanceCountTooLarge(Int(instanceCount))
            }

            let expectedSize = 8 + Int(instanceCount) * 64
            guard data.count >= expectedSize else {
                throw DeserializerError.invalidDataSize(
                    expected: expectedSize, got: data.count)
            }

            var transforms = [simd_float4x4]()
            transforms.reserveCapacity(Int(instanceCount))

            for _ in 0..<instanceCount {
                // Read 16 floats for 4×4 column-major matrix
                let col0 = SIMD4<Float>(
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset)
                )
                let col1 = SIMD4<Float>(
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset)
                )
                let col2 = SIMD4<Float>(
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset)
                )
                let col3 = SIMD4<Float>(
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset),
                    readFloat(ptr, offset: &offset)
                )
                transforms.append(simd_float4x4(col0, col1, col2, col3))
            }

            return InstanceData(meshId: meshId, transforms: transforms)
        }
    }

    // MARK: - Binary readers (little-endian)

    @inline(__always)
    private static func readUInt32(_ base: UnsafeRawPointer, offset: inout Int) -> UInt32 {
        let value = base.loadUnaligned(fromByteOffset: offset, as: UInt32.self)
        offset += 4
        return UInt32(littleEndian: value)
    }

    @inline(__always)
    private static func readFloat(_ base: UnsafeRawPointer, offset: inout Int) -> Float {
        let bits = base.loadUnaligned(fromByteOffset: offset, as: UInt32.self)
        offset += 4
        return Float(bitPattern: UInt32(littleEndian: bits))
    }
}

// MARK: - Errors

enum DeserializerError: LocalizedError {
    case dataTooSmall(Int)
    case meshTooLarge(vertices: Int, indices: Int)
    case instanceCountTooLarge(Int)
    case invalidDataSize(expected: Int, got: Int)

    var errorDescription: String? {
        switch self {
        case .dataTooSmall(let size):
            return "Invalid data: too small (\(size) bytes)"
        case .meshTooLarge(let v, let i):
            return "Mesh too large: \(v) vertices, \(i) indices"
        case .instanceCountTooLarge(let count):
            return "Instance count too large: \(count) (max \(MeshDeserializer.maxInstanceCount))"
        case .invalidDataSize(let expected, let got):
            return "Invalid data size: expected \(expected), got \(got)"
        }
    }
}
