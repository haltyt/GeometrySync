import Foundation
import simd

/// Mesh data container — mirrors C# MeshData struct
struct MeshData {
    var positions: [SIMD3<Float>]
    var normals: [SIMD3<Float>]
    var uvs: [SIMD2<Float>]
    var indices: [UInt32]

    var vertexCount: Int { positions.count }
    var triangleCount: Int { indices.count / 3 }
}

/// Validated but unparsed mesh payload for the Metal GPU path.
///
/// `vertexData` keeps the wire layout (interleaved pos:12 + normal:12 + uv:8,
/// 32 bytes per vertex) so it can be memcpy'd into a Metal staging buffer and
/// unpacked by compute kernels. `indexData` keeps the wire winding (i0, i1, i2);
/// the RealityKit winding reversal happens on the GPU (or in
/// `MeshDeserializer.expand` on the CPU fallback path).
struct RawMeshData: Sendable {
    let vertexCount: Int
    let indexCount: Int
    let vertexData: Data
    let indexData: Data

    var triangleCount: Int { indexCount / 3 }

    static let vertexStride = 32
}

/// Instance data container — mirrors C# InstanceData struct
struct InstanceData {
    var meshId: UInt32
    var transforms: [simd_float4x4]

    var instanceCount: Int { transforms.count }
}

/// PBR material parameters from Blender's Principled BSDF (message 0x04).
/// Colors are linear (Blender convention); roughness is Blender/RealityKit
/// convention (no smoothness inversion needed).
struct MaterialData: Sendable {
    var materialId: UInt32          // Hash of Blender material name
    var baseColor: SIMD4<Float>     // Linear RGBA
    var metallic: Float
    var roughness: Float
    var emission: SIMD3<Float>      // Linear RGB
    var emissionStrength: Float
    var alpha: Float
}

/// Binary protocol message types
enum MessageType: UInt8 {
    case mesh = 0x01
    case instance = 0x02
    case delta = 0x03     // Reserved, not implemented
    case material = 0x04  // PBR material parameters (M1)
}
