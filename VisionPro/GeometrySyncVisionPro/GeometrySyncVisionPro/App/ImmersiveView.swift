import SwiftUI
import RealityKit
import os

struct ImmersiveView: View {
    @Environment(AppModel.self) private var appModel

    // Metal GPU path (LowLevelMesh + compute shaders) — primary
    @State private var metalMeshBuilder: MetalMeshBuilder?
    @State private var metalInstanceRenderer: MetalInstanceRenderer?
    @State private var rendererResolved = false

    // CPU fallback path (MeshDescriptor + entity pool)
    @State private var meshBuilder = MeshBuilder()
    @State private var instanceManager = InstanceManager()

    @State private var rootEntity = Entity()
    @State private var meshEntity: ModelEntity?
    @State private var latestMeshResource: MeshResource?
    @State private var latestRawMesh: RawMeshData?
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
            for await rawMesh in client.meshStream {
                await applyMesh(rawMesh)
            }
        }
        .task(id: appModel.client?.host) {
            guard let client = appModel.client else { return }
            for await materialData in client.materialStream {
                applyMaterial(materialData)
            }
        }
        .task(id: appModel.client?.port) {
            guard let client = appModel.client else { return }
            for await instanceData in client.instanceStream {
                // Throttle: skip if too soon since last apply
                let now = CFAbsoluteTimeGetCurrent()
                let minInterval = 1.0 / targetInstanceFPS
                if now - lastInstanceApplyTime >= minInterval {
                    await applyInstances(instanceData)
                    lastInstanceApplyTime = now
                }
                await Task.yield()
            }
        }
    }

    // MARK: - Renderer selection

    @MainActor
    private func resolveRenderers() {
        guard !rendererResolved else { return }
        rendererResolved = true

        if let context = MetalContext.shared {
            let builder = MetalMeshBuilder(context: context)
            let instances = MetalInstanceRenderer(context: context)
            instances.setContainer(rootEntity)
            metalMeshBuilder = builder
            metalInstanceRenderer = instances
            logger.info("Using Metal GPU pipeline (LowLevelMesh + compute shaders)")
        } else {
            logger.warning("Metal unavailable — using CPU MeshDescriptor pipeline")
        }
    }

    // MARK: - Mesh application

    @MainActor
    private func applyMesh(_ raw: RawMeshData) async {
        resolveRenderers()

        if let gpu = metalMeshBuilder {
            guard let resource = await gpu.buildOrUpdate(from: raw) else { return }
            latestRawMesh = raw
            attachOrUpdate(resource, material: gpu.getMaterial())
        } else {
            let meshData = MeshDeserializer.expand(raw)
            guard let resource = meshBuilder.buildOrUpdate(from: meshData) else { return }
            latestMeshResource = resource
            attachOrUpdate(resource, material: meshBuilder.getMaterial())
        }
    }

    @MainActor
    private func attachOrUpdate(_ resource: MeshResource, material: RealityKit.Material) {
        if let existing = meshEntity {
            existing.model?.mesh = resource
        } else {
            let entity = ModelEntity(mesh: resource, materials: [material])
            rootEntity.addChild(entity)
            meshEntity = entity
            logger.info("Created mesh entity")
        }
    }

    // MARK: - Material application

    @MainActor
    private func applyMaterial(_ data: MaterialData) {
        resolveRenderers()

        let material = data.makeMaterial()

        // Existing entities
        meshEntity?.model?.materials = [material]

        // Future entities created by either pipeline
        metalMeshBuilder?.setMaterial(material)
        meshBuilder.setMaterial(material)
        metalInstanceRenderer?.setMaterial(material)
        instanceManager.setMaterial(material)

        logger.info("Applied material \(data.materialId)")
    }

    // MARK: - Instance application

    @MainActor
    private func applyInstances(_ data: InstanceData) async {
        resolveRenderers()

        if data.instanceCount > 0 {
            meshEntity?.isEnabled = false
        }

        if let gpu = metalInstanceRenderer {
            if !gpu.hasMesh(data.meshId), let raw = latestRawMesh {
                gpu.registerBaseMesh(meshId: data.meshId, raw: raw)
                logger.info("Auto-registered base mesh for meshId \(data.meshId)")
            }
            await gpu.updateInstances(data)
        } else {
            if !instanceManager.hasMesh(data.meshId), let resource = latestMeshResource {
                instanceManager.registerBaseMesh(meshId: data.meshId, mesh: resource)
                logger.info("Auto-registered base mesh for meshId \(data.meshId)")
            }
            instanceManager.updateInstances(data)
        }
    }
}
