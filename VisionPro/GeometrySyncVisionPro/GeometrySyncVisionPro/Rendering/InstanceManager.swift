import RealityKit
import Observation
import simd
import os

/// Manages a pool of RealityKit Entities for GPU-style instancing.
/// Port of Unity/GeometrySync/.../GPUInstanceRenderer.cs
@MainActor @Observable
final class InstanceManager {

    // MARK: - Public state

    private(set) var totalInstanceCount: Int = 0

    // MARK: - Configuration

    let maxInstances: Int

    // MARK: - Private

    private let logger = Logger(subsystem: "com.geometrysync.visionpro", category: "InstanceManager")
    private var container: Entity?
    private var baseMeshes: [UInt32: MeshResource] = [:]
    private var entityPools: [UInt32: [ModelEntity]] = [:]
    private var activeCount: [UInt32: Int] = [:]
    private var sharedMaterial: RealityKit.Material

    // MARK: - Init

    init(maxInstances: Int = 5000) {
        self.maxInstances = maxInstances
        self.sharedMaterial = SimpleMaterial(color: .blue, roughness: 0.5, isMetallic: false)
    }

    func setContainer(_ entity: Entity) {
        container = entity
    }

    // MARK: - Base mesh registration

    func registerBaseMesh(meshId: UInt32, mesh: MeshResource) {
        baseMeshes[meshId] = mesh
        if entityPools[meshId] == nil {
            entityPools[meshId] = []
        }
        logger.info("Registered base mesh \(meshId)")
    }

    func hasMesh(_ meshId: UInt32) -> Bool {
        baseMeshes[meshId] != nil
    }

    // MARK: - Instance updates

    func updateInstances(_ data: InstanceData) {
        let meshId = data.meshId

        guard let meshResource = baseMeshes[meshId] else { return }
        guard let container else { return }

        let count = min(data.instanceCount, maxInstances)
        ensurePoolCapacity(meshId: meshId, needed: count, mesh: meshResource, parent: container)

        guard let pool = entityPools[meshId] else { return }

        // Update transforms — only set isEnabled when state actually changes
        let previousCount = activeCount[meshId] ?? 0

        for i in 0..<count {
            let entity = pool[i]
            if i >= previousCount {
                entity.isEnabled = true
            }
            entity.setTransformMatrix(data.transforms[i], relativeTo: nil)
        }

        // Deactivate surplus
        if previousCount > count {
            for i in count..<min(previousCount, pool.count) {
                pool[i].isEnabled = false
            }
        }

        activeCount[meshId] = count
        totalInstanceCount = activeCount.values.reduce(0, +)
    }

    // MARK: - Pool management

    private func ensurePoolCapacity(
        meshId: UInt32,
        needed: Int,
        mesh: MeshResource,
        parent: Entity
    ) {
        var pool = entityPools[meshId] ?? []
        let existing = pool.count

        if existing >= needed { return }

        // Batch-create entities
        let materials = [sharedMaterial]
        for _ in existing..<needed {
            let entity = ModelEntity(mesh: mesh, materials: materials)
            entity.isEnabled = false
            parent.addChild(entity)
            pool.append(entity)
        }

        entityPools[meshId] = pool
        logger.info("Pool for mesh \(meshId): \(existing) → \(pool.count)")
    }

    func updateBaseMesh(meshId: UInt32, mesh: MeshResource) {
        baseMeshes[meshId] = mesh
        guard let pool = entityPools[meshId] else { return }
        for entity in pool {
            entity.model?.mesh = mesh
        }
    }

    // MARK: - Cleanup

    func clear() {
        for (_, pool) in entityPools {
            for entity in pool {
                entity.removeFromParent()
            }
        }
        entityPools.removeAll()
        baseMeshes.removeAll()
        activeCount.removeAll()
        totalInstanceCount = 0
    }
}
