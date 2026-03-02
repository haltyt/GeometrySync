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

/// Instance data container — mirrors C# InstanceData struct
struct InstanceData {
    var meshId: UInt32
    var transforms: [simd_float4x4]

    var instanceCount: Int { transforms.count }
}

/// Binary protocol message types
enum MessageType: UInt8 {
    case mesh = 0x01
    case instance = 0x02
    case delta = 0x03  // Reserved, not implemented
}
