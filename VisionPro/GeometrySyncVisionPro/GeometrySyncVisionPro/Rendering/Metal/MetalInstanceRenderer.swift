import Foundation
import Metal
import RealityKit
import Observation
import simd
import os

/// GPU instancing for RealityKit: bakes N instance transforms into one
/// merged LowLevelMesh per update using compute kernels — a single entity
/// and a single draw instead of the CPU entity pool's N transform writes.
/// visionOS analog of Unity's GPUInstanceRenderer + InstancedIndirect.shader
/// (RealityKit exposes no hardware instancing, so instances are baked).
@MainActor @Observable
final class MetalInstanceRenderer {

    // MARK: - Public state

    private(set) var totalInstanceCount: Int = 0

    /// Budget for the baked mesh (2M verts × 32B = 64MB vertex buffer).
    /// Instance counts that would exceed it are clamped with a warning.
    let maxBakedVertices: Int
    let maxBakedIndices: Int

    // MARK: - Private types

    private struct BaseMesh {
        let vertexCount: Int
        let indexCount: Int
        let vertices: MTLBuffer   // wire layout (GSVertex, 32B stride)
        let indices: MTLBuffer    // winding-corrected uint32
        let bounds: BoundingBox
    }

    private struct Merged {
        var mesh: LowLevelMesh
        var resource: MeshResource
        var vertexCapacity: Int
        var indexCapacity: Int
    }

    // MARK: - Private state

    private let logger = Logger(subsystem: "com.geometrysync.visionpro", category: "MetalInstanceRenderer")
    private let context: MetalContext
    private var container: Entity?
    private var baseMeshes: [UInt32: BaseMesh] = [:]
    private var merged: [UInt32: Merged] = [:]
    private var entities: [UInt32: ModelEntity] = [:]
    private var activeCount: [UInt32: Int] = [:]
    private var transformStaging: MTLBuffer?
    private var material: RealityKit.Material

    // MARK: - Init

    init(context: MetalContext,
         maxBakedVertices: Int = 2_000_000,
         maxBakedIndices: Int = 6_000_000) {
        self.context = context
        self.maxBakedVertices = maxBakedVertices
        self.maxBakedIndices = maxBakedIndices
        self.material = SimpleMaterial(color: .blue, roughness: 0.5, isMetallic: false)
    }

    func setContainer(_ entity: Entity) {
        container = entity
    }

    /// Replace the shared material on all merged-mesh entities (0x04 sync).
    func setMaterial(_ newMaterial: RealityKit.Material) {
        material = newMaterial
        for (_, entity) in entities {
            entity.model?.materials = [newMaterial]
        }
    }

    // MARK: - Base mesh registration

    func hasMesh(_ meshId: UInt32) -> Bool {
        baseMeshes[meshId] != nil
    }

    /// Register the base mesh from a raw wire payload. Vertices are stored
    /// verbatim; the winding reversal is applied once here on the CPU
    /// (registration is rare) so bake passes can replicate indices as-is.
    func registerBaseMesh(meshId: UInt32, raw: RawMeshData) {
        guard raw.vertexCount > 0, raw.indexCount >= 3 else {
            logger.warning("Ignoring empty base mesh for meshId \(meshId)")
            return
        }

        let vertexBytes = raw.vertexCount * RawMeshData.vertexStride
        let indexBytes = raw.indexCount * MemoryLayout<UInt32>.size
        guard let vertexBuffer = context.device.makeBuffer(length: vertexBytes, options: .storageModeShared),
              let indexBuffer = context.device.makeBuffer(length: indexBytes, options: .storageModeShared) else {
            logger.error("Failed to allocate base mesh buffers for meshId \(meshId)")
            return
        }

        raw.vertexData.withUnsafeBytes {
            vertexBuffer.contents().copyMemory(from: $0.baseAddress!, byteCount: vertexBytes)
        }

        raw.indexData.withUnsafeBytes { src in
            let base = src.baseAddress!
            let dst = indexBuffer.contents().bindMemory(to: UInt32.self, capacity: raw.indexCount)
            let triangleCount = raw.indexCount / 3
            for tri in 0..<triangleCount {
                let b = tri * 3
                let i0 = base.loadUnaligned(fromByteOffset: b * 4, as: UInt32.self)
                let i1 = base.loadUnaligned(fromByteOffset: (b + 1) * 4, as: UInt32.self)
                let i2 = base.loadUnaligned(fromByteOffset: (b + 2) * 4, as: UInt32.self)
                dst[b]     = UInt32(littleEndian: i0)
                dst[b + 1] = UInt32(littleEndian: i2)  // swapped for RealityKit
                dst[b + 2] = UInt32(littleEndian: i1)  // swapped for RealityKit
            }
            for i in (triangleCount * 3)..<raw.indexCount {
                let v = base.loadUnaligned(fromByteOffset: i * 4, as: UInt32.self)
                dst[i] = UInt32(littleEndian: v)
            }
        }

        baseMeshes[meshId] = BaseMesh(
            vertexCount: raw.vertexCount,
            indexCount: raw.indexCount,
            vertices: vertexBuffer,
            indices: indexBuffer,
            bounds: raw.computeBounds())
        logger.info("Registered base mesh \(meshId): \(raw.vertexCount) verts")
    }

    /// Replace an already-registered base mesh (topology/shape changed).
    func updateBaseMesh(meshId: UInt32, raw: RawMeshData) {
        guard baseMeshes[meshId] != nil else { return }
        registerBaseMesh(meshId: meshId, raw: raw)
    }

    // MARK: - Instance updates

    func updateInstances(_ data: InstanceData) async {
        let meshId = data.meshId
        guard let base = baseMeshes[meshId], let container else { return }

        // Clamp to the baked-mesh budget
        var count = data.instanceCount
        let capacity = min(maxBakedVertices / base.vertexCount,
                           maxBakedIndices / base.indexCount)
        if count > capacity {
            logger.warning("Clamping instances \(count) → \(capacity) (baked mesh budget)")
            count = max(0, capacity)
        }

        if count == 0 {
            entities[meshId]?.isEnabled = false
            activeCount[meshId] = 0
            totalInstanceCount = activeCount.values.reduce(0, +)
            return
        }

        do {
            let totalVertices = base.vertexCount * count
            let totalIndices = base.indexCount * count
            try await ensureMergedCapacity(
                meshId: meshId,
                vertexCount: totalVertices,
                indexCount: totalIndices,
                in: container)
            guard let target = merged[meshId] else { return }

            // Upload transforms (simd_float4x4 is 64B column-major —
            // identical to Metal float4x4, memcpy is enough)
            let transformBytes = count * MemoryLayout<simd_float4x4>.stride
            let transformStage = try MetalMeshBuilder.ensureStaging(
                &transformStaging, bytes: transformBytes, device: context.device)
            data.transforms.withUnsafeBytes {
                transformStage.contents().copyMemory(from: $0.baseAddress!, byteCount: transformBytes)
            }

            guard let commandBuffer = context.commandQueue.makeCommandBuffer(),
                  let encoder = commandBuffer.makeComputeCommandEncoder() else {
                throw MetalMeshError.commandEncodingFailed
            }

            let vertexDst = target.mesh.replace(bufferIndex: 0, using: commandBuffer)
            let indexDst = target.mesh.replaceIndices(using: commandBuffer)

            var baseVertexCount32 = UInt32(base.vertexCount)
            var baseIndexCount32 = UInt32(base.indexCount)
            var instanceCount32 = UInt32(count)

            encoder.setBuffer(base.vertices, offset: 0, index: 0)
            encoder.setBuffer(transformStage, offset: 0, index: 1)
            encoder.setBuffer(vertexDst, offset: 0, index: 2)
            encoder.setBytes(&baseVertexCount32, length: 4, index: 3)
            encoder.setBytes(&instanceCount32, length: 4, index: 4)
            encoder.gsDispatch1D(context.bakeInstanceVertices, count: totalVertices)

            encoder.setBuffer(base.indices, offset: 0, index: 0)
            encoder.setBuffer(indexDst, offset: 0, index: 1)
            encoder.setBytes(&baseIndexCount32, length: 4, index: 2)
            encoder.setBytes(&baseVertexCount32, length: 4, index: 3)
            encoder.setBytes(&instanceCount32, length: 4, index: 4)
            encoder.gsDispatch1D(context.bakeInstanceIndices, count: totalIndices)

            encoder.endEncoding()
            await commandBuffer.gsCommitAndWait()

            target.mesh.parts.replaceAll([
                LowLevelMesh.Part(
                    indexOffset: 0,
                    indexCount: totalIndices,
                    topology: .triangle,
                    materialIndex: 0,
                    bounds: Self.mergedBounds(base: base.bounds,
                                              transforms: data.transforms,
                                              count: count))
            ])

            entities[meshId]?.isEnabled = true
            activeCount[meshId] = count
            totalInstanceCount = activeCount.values.reduce(0, +)
        } catch {
            logger.error("GPU instance bake failed: \(error.localizedDescription)")
        }
    }

    // MARK: - Cleanup

    func clear() {
        for (_, entity) in entities {
            entity.removeFromParent()
        }
        entities.removeAll()
        merged.removeAll()
        baseMeshes.removeAll()
        activeCount.removeAll()
        totalInstanceCount = 0
    }

    // MARK: - Merged mesh management

    private func ensureMergedCapacity(
        meshId: UInt32,
        vertexCount: Int,
        indexCount: Int,
        in container: Entity
    ) async throws {
        let neededVertices = max(1024, vertexCount.gsNextPowerOfTwo)
        let neededIndices = max(3072, indexCount.gsNextPowerOfTwo)

        if let existing = merged[meshId],
           neededVertices <= existing.vertexCapacity,
           neededIndices <= existing.indexCapacity {
            return
        }

        let mesh = try LowLevelMesh(descriptor: MetalMeshBuilder.makeDescriptor(
            vertexCapacity: neededVertices, indexCapacity: neededIndices))
        let resource = try await MeshResource(from: mesh)
        merged[meshId] = Merged(
            mesh: mesh,
            resource: resource,
            vertexCapacity: neededVertices,
            indexCapacity: neededIndices)

        if let entity = entities[meshId] {
            entity.model?.mesh = resource
        } else {
            let entity = ModelEntity(mesh: resource, materials: [material])
            container.addChild(entity)
            // Instance transforms arrive in world space (parity with the
            // CPU entity-pool path) — cancel out the container's offset.
            entity.setTransformMatrix(matrix_identity_float4x4, relativeTo: nil)
            entities[meshId] = entity
        }
        logger.info("Merged mesh \(meshId) capacity: \(neededVertices) verts / \(neededIndices) indices")
    }

    /// Conservative AABB: base-mesh bounding sphere placed at each instance,
    /// scaled by the largest column norm of its transform.
    private static func mergedBounds(
        base: BoundingBox,
        transforms: [simd_float4x4],
        count: Int
    ) -> BoundingBox {
        let center = (base.min + base.max) * 0.5
        let radius = simd_length(base.max - center)

        var minP = SIMD3<Float>(repeating: .greatestFiniteMagnitude)
        var maxP = -minP

        for i in 0..<count {
            let m = transforms[i]
            let c4 = m * SIMD4<Float>(center.x, center.y, center.z, 1)
            let c = SIMD3<Float>(c4.x, c4.y, c4.z)
            let scale = max(
                simd_length(SIMD3<Float>(m.columns.0.x, m.columns.0.y, m.columns.0.z)),
                max(simd_length(SIMD3<Float>(m.columns.1.x, m.columns.1.y, m.columns.1.z)),
                    simd_length(SIMD3<Float>(m.columns.2.x, m.columns.2.y, m.columns.2.z))))
            let r = SIMD3<Float>(repeating: radius * scale)
            minP = simd_min(minP, c - r)
            maxP = simd_max(maxP, c + r)
        }
        return BoundingBox(min: minP, max: maxP)
    }
}
