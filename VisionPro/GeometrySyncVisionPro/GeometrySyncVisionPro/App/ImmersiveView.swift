import SwiftUI
import RealityKit
import os

struct ImmersiveView: View {
    @Environment(AppModel.self) private var appModel
    @State private var meshBuilder = MeshBuilder()
    @State private var instanceManager = InstanceManager()
    @State private var rootEntity = Entity()
    @State private var meshEntity: ModelEntity?
    @State private var latestMeshResource: MeshResource?
    @State private var lastInstanceApplyTime: CFAbsoluteTime = 0

    /// Target FPS for instance updates (lower = smoother, less CPU pressure)
    private let targetInstanceFPS: Double = 15

    private let logger = Logger(subsystem: "com.geometrysync.visionpro", category: "ImmersiveView")

    var body: some View {
        RealityView { content in
            rootEntity.position = SIMD3<Float>(0, 1.2, -1.5)
            content.add(rootEntity)
            instanceManager.setContainer(rootEntity)
            logger.info("RealityView initialized")
        }
        .task(id: appModel.client?.host) {
            guard let client = appModel.client else { return }
            for await meshData in client.meshStream {
                applyMesh(meshData)
            }
        }
        .task(id: appModel.client?.port) {
            guard let client = appModel.client else { return }
            for await instanceData in client.instanceStream {
                // Throttle: skip if too soon since last apply
                let now = CFAbsoluteTimeGetCurrent()
                let minInterval = 1.0 / targetInstanceFPS
                if now - lastInstanceApplyTime >= minInterval {
                    applyInstances(instanceData)
                    lastInstanceApplyTime = now
                }
                await Task.yield()
            }
        }
    }

    // MARK: - Mesh application

    @MainActor
    private func applyMesh(_ data: MeshData) {
        guard let resource = meshBuilder.buildOrUpdate(from: data) else { return }

        latestMeshResource = resource

        if let existing = meshEntity {
            existing.model?.mesh = resource
        } else {
            let entity = ModelEntity(mesh: resource, materials: [meshBuilder.getMaterial()])
            rootEntity.addChild(entity)
            meshEntity = entity
            logger.info("Created mesh entity")
        }
    }

    // MARK: - Instance application

    @MainActor
    private func applyInstances(_ data: InstanceData) {
        if !instanceManager.hasMesh(data.meshId), let resource = latestMeshResource {
            instanceManager.registerBaseMesh(meshId: data.meshId, mesh: resource)
            logger.info("Auto-registered base mesh for meshId \(data.meshId)")
        }

        if data.instanceCount > 0 {
            meshEntity?.isEnabled = false
        }

        instanceManager.updateInstances(data)
    }
}
