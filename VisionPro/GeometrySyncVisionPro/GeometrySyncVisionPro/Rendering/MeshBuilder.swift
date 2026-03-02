import RealityKit
import Observation
import os

/// Builds and updates RealityKit MeshResource from deserialized MeshData.
/// Port of Unity/GeometrySync/.../MeshReconstructor.cs
@MainActor @Observable
final class MeshBuilder {

    private(set) var currentVertexCount: Int = 0
    private(set) var currentTriangleCount: Int = 0

    private let logger = Logger(subsystem: "com.geometrysync.visionpro", category: "MeshBuilder")
    private var currentResource: MeshResource?
    private var material: RealityKit.Material?

    init() {
        material = SimpleMaterial(color: .blue, roughness: 0.5, isMetallic: false)
    }

    /// Build or update a MeshResource from the given MeshData.
    /// Returns nil if the mesh data is empty or invalid.
    func buildOrUpdate(from meshData: MeshData) -> MeshResource? {
        guard meshData.vertexCount > 0, meshData.indices.count >= 3 else {
            logger.warning("Empty or invalid mesh data")
            return nil
        }

        do {
            var descriptor = MeshDescriptor(name: "GeometrySyncMesh")

            descriptor.positions = MeshBuffer(meshData.positions)
            descriptor.normals = MeshBuffer(meshData.normals)
            descriptor.textureCoordinates = MeshBuffer(meshData.uvs)
            descriptor.primitives = .triangles(meshData.indices)

            if let existing = currentResource {
                // Generate new contents and replace in-place
                let fresh = try MeshResource.generate(from: [descriptor])
                try existing.replace(with: fresh.contents)
                logger.debug("Mesh updated: \(meshData.vertexCount) verts")
            } else {
                currentResource = try MeshResource.generate(from: [descriptor])
                logger.info("Mesh created: \(meshData.vertexCount) verts")
            }

            currentVertexCount = meshData.vertexCount
            currentTriangleCount = meshData.triangleCount

            return currentResource
        } catch {
            logger.error("Failed to build mesh: \(error.localizedDescription)")
            currentResource = nil
            return nil
        }
    }

    /// Get the shared material for mesh rendering.
    func getMaterial() -> RealityKit.Material {
        if let mat = material { return mat }
        let mat = SimpleMaterial(color: .blue, roughness: 0.5, isMetallic: false)
        material = mat
        return mat
    }

    /// Reset builder state.
    func reset() {
        currentResource = nil
        currentVertexCount = 0
        currentTriangleCount = 0
    }
}
