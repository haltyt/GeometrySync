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

    /// Validate the mesh header and return zero-copy slices of the payload.
    ///
    /// Binary format:
    /// - Header: vertexCount (uint32 LE), indexCount (uint32 LE)
    /// - Vertex data: interleaved [x,y,z, nx,ny,nz, u,v] as float32 LE (32 bytes per vertex)
    /// - Index data: uint32 LE array, wire winding (i0, i1, i2)
    ///
    /// This is the fast path for the Metal renderer: no per-vertex parsing —
    /// the slices are uploaded to GPU staging buffers as-is and unpacked by
    /// compute kernels (including the RealityKit winding reversal).
    static func rawMesh(from data: Data) throws -> RawMeshData {
        guard data.count >= 8 else {
            throw DeserializerError.dataTooSmall(data.count)
        }

        let (vertexCount, indexCount) = data.withUnsafeBytes { raw -> (UInt32, UInt32) in
            let ptr = raw.baseAddress!
            var offset = 0
            let v = readUInt32(ptr, offset: &offset)
            let i = readUInt32(ptr, offset: &offset)
            return (v, i)
        }

        guard vertexCount <= maxVertexCount, indexCount <= maxIndexCount else {
            throw DeserializerError.meshTooLarge(
                vertices: Int(vertexCount), indices: Int(indexCount))
        }

        let vertexDataSize = Int(vertexCount) * RawMeshData.vertexStride
        let indexDataSize = Int(indexCount) * 4
        let expectedTotalSize = 8 + vertexDataSize + indexDataSize

        guard data.count >= expectedTotalSize else {
            throw DeserializerError.invalidDataSize(
                expected: expectedTotalSize, got: data.count)
        }

        let vertexStart = data.startIndex + 8
        let indexStart = vertexStart + vertexDataSize

        // Slices, not copies — they retain the payload's backing storage,
        // which goes straight into Metal staging buffers.
        return RawMeshData(
            vertexCount: Int(vertexCount),
            indexCount: Int(indexCount),
            vertexData: data[vertexStart..<indexStart],
            indexData: data[indexStart..<(indexStart + indexDataSize)]
        )
    }

    /// CPU fallback: expand a validated raw payload into parsed arrays.
    ///
    /// Applies the winding order reversal for RealityKit (right-hand system):
    /// Blender→Unity produces left-hand winding (i0, i1, i2),
    /// RealityKit needs right-hand winding: (i0, i2, i1).
    static func expand(_ raw: RawMeshData) -> MeshData {
        let vCount = raw.vertexCount
        let iCount = raw.indexCount

        var positions = [SIMD3<Float>]()
        positions.reserveCapacity(vCount)
        var normals = [SIMD3<Float>]()
        normals.reserveCapacity(vCount)
        var uvs = [SIMD2<Float>]()
        uvs.reserveCapacity(vCount)

        raw.vertexData.withUnsafeBytes { rawBytes in
            let ptr = rawBytes.baseAddress!
            var offset = 0
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
        }

        var indices = [UInt32](repeating: 0, count: iCount)
        raw.indexData.withUnsafeBytes { rawBytes in
            let ptr = rawBytes.baseAddress!
            var offset = 0
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
            for i in (triangleCount * 3)..<iCount {
                indices[i] = readUInt32(ptr, offset: &offset)
            }
        }

        return MeshData(
            positions: positions,
            normals: normals,
            uvs: uvs,
            indices: indices
        )
    }

    /// Deserialize mesh data from binary format (validation + full CPU parse).
    static func deserializeMesh(_ data: Data) throws -> MeshData {
        expand(try rawMesh(from: data))
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
