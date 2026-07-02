import Foundation
import Metal
import RealityKit
import Observation
import simd
import os

// MARK: - Shared Metal context

/// Device, command queue and compute pipelines shared by the GPU mesh path.
/// `shared` is nil when Metal (or the shader library) is unavailable —
/// callers fall back to the CPU MeshDescriptor pipeline.
@MainActor
final class MetalContext {

    static let shared: MetalContext? = MetalContext()

    let device: MTLDevice
    let commandQueue: MTLCommandQueue
    let copyVertices: MTLComputePipelineState
    let flipWinding: MTLComputePipelineState
    let copyIndicesTail: MTLComputePipelineState
    let bakeInstanceVertices: MTLComputePipelineState
    let bakeInstanceIndices: MTLComputePipelineState

    private init?() {
        let logger = Logger(subsystem: "com.geometrysync.visionpro", category: "MetalContext")

        guard let device = MTLCreateSystemDefaultDevice(),
              let queue = device.makeCommandQueue(),
              let library = device.makeDefaultLibrary() else {
            logger.error("Metal device/library unavailable")
            return nil
        }

        func pipeline(_ name: String) -> MTLComputePipelineState? {
            guard let function = library.makeFunction(name: name) else {
                logger.error("Missing kernel: \(name)")
                return nil
            }
            return try? device.makeComputePipelineState(function: function)
        }

        guard let copyVertices = pipeline("gs_copyVertices"),
              let flipWinding = pipeline("gs_flipWinding"),
              let copyIndicesTail = pipeline("gs_copyIndicesTail"),
              let bakeInstanceVertices = pipeline("gs_bakeInstanceVertices"),
              let bakeInstanceIndices = pipeline("gs_bakeInstanceIndices") else {
            logger.error("Failed to build compute pipelines")
            return nil
        }

        self.device = device
        self.commandQueue = queue
        self.copyVertices = copyVertices
        self.flipWinding = flipWinding
        self.copyIndicesTail = copyIndicesTail
        self.bakeInstanceVertices = bakeInstanceVertices
        self.bakeInstanceIndices = bakeInstanceIndices
    }
}

// MARK: - Errors

enum MetalMeshError: LocalizedError {
    case commandEncodingFailed
    case bufferAllocationFailed(Int)

    var errorDescription: String? {
        switch self {
        case .commandEncodingFailed:
            return "Failed to create Metal command buffer/encoder"
        case .bufferAllocationFailed(let size):
            return "Failed to allocate Metal buffer (\(size) bytes)"
        }
    }
}

// MARK: - Shared helpers

extension Int {
    /// Power-of-two growth, mirroring the Unity NativeArray allocation strategy.
    var gsNextPowerOfTwo: Int {
        guard self > 1 else { return 1 }
        return 1 << (Int.bitWidth - (self - 1).leadingZeroBitCount)
    }
}

extension MTLComputeCommandEncoder {
    /// Dispatch a 1D grid with a reasonable threadgroup width.
    func gsDispatch1D(_ pipeline: MTLComputePipelineState, count: Int) {
        guard count > 0 else { return }
        setComputePipelineState(pipeline)
        let width = min(pipeline.maxTotalThreadsPerThreadgroup, 256)
        dispatchThreads(
            MTLSize(width: count, height: 1, depth: 1),
            threadsPerThreadgroup: MTLSize(width: width, height: 1, depth: 1))
    }
}

extension MTLCommandBuffer {
    /// Commit and await GPU completion. Staging buffers are reused across
    /// updates, so the next CPU write must not start while this command
    /// buffer is still reading them.
    func gsCommitAndWait() async {
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            addCompletedHandler { _ in continuation.resume() }
            commit()
        }
    }
}

extension RawMeshData {
    /// Scan wire-format positions (stride 32, offset 0) for an AABB.
    func computeBounds() -> BoundingBox {
        guard vertexCount > 0 else {
            return BoundingBox(min: .zero, max: .zero)
        }
        var minP = SIMD3<Float>(repeating: .greatestFiniteMagnitude)
        var maxP = -minP
        vertexData.withUnsafeBytes { raw in
            let base = raw.baseAddress!
            for i in 0..<vertexCount {
                let offset = i * RawMeshData.vertexStride
                let x = base.loadUnaligned(fromByteOffset: offset, as: Float.self)
                let y = base.loadUnaligned(fromByteOffset: offset + 4, as: Float.self)
                let z = base.loadUnaligned(fromByteOffset: offset + 8, as: Float.self)
                let p = SIMD3<Float>(x, y, z)
                minP = simd_min(minP, p)
                maxP = simd_max(maxP, p)
            }
        }
        return BoundingBox(min: minP, max: maxP)
    }
}

// MARK: - MetalMeshBuilder

/// GPU mesh pipeline: uploads the raw wire payload to Metal staging buffers
/// and lets compute kernels write vertices/indices directly into a
/// RealityKit LowLevelMesh — no CPU-side vertex parsing, no MeshDescriptor
/// regeneration. GPU counterpart of the CPU `MeshBuilder`
/// (and the visionOS analog of Unity's NativeArray MeshReconstructor).
@MainActor @Observable
final class MetalMeshBuilder {

    private(set) var currentVertexCount: Int = 0
    private(set) var currentTriangleCount: Int = 0

    private let logger = Logger(subsystem: "com.geometrysync.visionpro", category: "MetalMeshBuilder")
    private let context: MetalContext

    private var mesh: LowLevelMesh?
    private var resource: MeshResource?
    private var vertexCapacity = 0
    private var indexCapacity = 0
    private var vertexStaging: MTLBuffer?
    private var indexStaging: MTLBuffer?
    private var material: RealityKit.Material

    init(context: MetalContext) {
        self.context = context
        self.material = SimpleMaterial(color: .blue, roughness: 0.5, isMetallic: false)
    }

    func getMaterial() -> RealityKit.Material { material }

    /// Build or update the LowLevelMesh from a raw wire payload.
    /// Returns the MeshResource to assign to the entity (a new instance
    /// after a capacity grow, the same instance otherwise).
    func buildOrUpdate(from raw: RawMeshData) async -> MeshResource? {
        guard raw.vertexCount > 0, raw.indexCount >= 3 else {
            logger.warning("Empty or invalid mesh data")
            return nil
        }

        do {
            try await ensureCapacity(vertexCount: raw.vertexCount, indexCount: raw.indexCount)
            guard let mesh, let resource else { return nil }

            let vertexBytes = raw.vertexCount * RawMeshData.vertexStride
            let indexBytes = raw.indexCount * MemoryLayout<UInt32>.size
            let vertexStage = try Self.ensureStaging(&vertexStaging, bytes: vertexBytes, device: context.device)
            let indexStage = try Self.ensureStaging(&indexStaging, bytes: indexBytes, device: context.device)

            raw.vertexData.withUnsafeBytes {
                vertexStage.contents().copyMemory(from: $0.baseAddress!, byteCount: vertexBytes)
            }
            raw.indexData.withUnsafeBytes {
                indexStage.contents().copyMemory(from: $0.baseAddress!, byteCount: indexBytes)
            }

            guard let commandBuffer = context.commandQueue.makeCommandBuffer(),
                  let encoder = commandBuffer.makeComputeCommandEncoder() else {
                throw MetalMeshError.commandEncodingFailed
            }

            let vertexBuffer = mesh.replace(bufferIndex: 0, using: commandBuffer)
            let indexBuffer = mesh.replaceIndices(using: commandBuffer)

            // Vertices: staging → LowLevelMesh vertex buffer (identical layout)
            var vertexCount32 = UInt32(raw.vertexCount)
            encoder.setBuffer(vertexStage, offset: 0, index: 0)
            encoder.setBuffer(vertexBuffer, offset: 0, index: 1)
            encoder.setBytes(&vertexCount32, length: 4, index: 2)
            encoder.gsDispatch1D(context.copyVertices, count: raw.vertexCount)

            // Indices: per-triangle winding reversal for RealityKit
            let triangleCount = raw.indexCount / 3
            var triangleCount32 = UInt32(triangleCount)
            encoder.setBuffer(indexStage, offset: 0, index: 0)
            encoder.setBuffer(indexBuffer, offset: 0, index: 1)
            encoder.setBytes(&triangleCount32, length: 4, index: 2)
            encoder.gsDispatch1D(context.flipWinding, count: triangleCount)

            let tail = raw.indexCount - triangleCount * 3
            if tail > 0 {
                var tailOffset32 = UInt32(triangleCount * 3)
                var tail32 = UInt32(tail)
                encoder.setBuffer(indexStage, offset: 0, index: 0)
                encoder.setBuffer(indexBuffer, offset: 0, index: 1)
                encoder.setBytes(&tailOffset32, length: 4, index: 2)
                encoder.setBytes(&tail32, length: 4, index: 3)
                encoder.gsDispatch1D(context.copyIndicesTail, count: tail)
            }

            encoder.endEncoding()
            await commandBuffer.gsCommitAndWait()

            mesh.parts.replaceAll([
                LowLevelMesh.Part(
                    indexOffset: 0,
                    indexCount: raw.indexCount,
                    topology: .triangle,
                    materialIndex: 0,
                    bounds: raw.computeBounds())
            ])

            currentVertexCount = raw.vertexCount
            currentTriangleCount = triangleCount

            return resource
        } catch {
            logger.error("GPU mesh update failed: \(error.localizedDescription)")
            return nil
        }
    }

    func reset() {
        mesh = nil
        resource = nil
        vertexCapacity = 0
        indexCapacity = 0
        currentVertexCount = 0
        currentTriangleCount = 0
    }

    // MARK: - Capacity management

    private func ensureCapacity(vertexCount: Int, indexCount: Int) async throws {
        let neededVertices = max(1024, vertexCount.gsNextPowerOfTwo)
        let neededIndices = max(3072, indexCount.gsNextPowerOfTwo)

        if mesh != nil, neededVertices <= vertexCapacity, neededIndices <= indexCapacity {
            return
        }

        let newMesh = try LowLevelMesh(descriptor: Self.makeDescriptor(
            vertexCapacity: neededVertices, indexCapacity: neededIndices))
        let newResource = try await MeshResource(from: newMesh)

        mesh = newMesh
        resource = newResource
        vertexCapacity = neededVertices
        indexCapacity = neededIndices
        logger.info("LowLevelMesh capacity: \(neededVertices) verts / \(neededIndices) indices")
    }

    static func makeDescriptor(vertexCapacity: Int, indexCapacity: Int) -> LowLevelMesh.Descriptor {
        var descriptor = LowLevelMesh.Descriptor()
        descriptor.vertexCapacity = vertexCapacity
        // Matches GSVertex in GeometrySyncKernels.metal (= wire format)
        descriptor.vertexAttributes = [
            .init(semantic: .position, format: .float3, offset: 0),
            .init(semantic: .normal, format: .float3, offset: 12),
            .init(semantic: .uv0, format: .float2, offset: 24),
        ]
        descriptor.vertexLayouts = [
            .init(bufferIndex: 0, bufferStride: RawMeshData.vertexStride),
        ]
        descriptor.indexCapacity = indexCapacity
        descriptor.indexType = .uint32
        return descriptor
    }

    static func ensureStaging(_ buffer: inout MTLBuffer?, bytes: Int, device: MTLDevice) throws -> MTLBuffer {
        if let existing = buffer, existing.length >= bytes { return existing }
        let size = max(4096, bytes.gsNextPowerOfTwo)
        guard let fresh = device.makeBuffer(length: size, options: .storageModeShared) else {
            throw MetalMeshError.bufferAllocationFailed(size)
        }
        buffer = fresh
        return fresh
    }
}
