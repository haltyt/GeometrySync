//
//  GeometrySyncKernels.metal
//  GeometrySyncVisionPro
//
//  Compute kernels for the LowLevelMesh GPU pipeline.
//  Vertex layout matches the wire format byte-for-byte
//  (pos:12 + normal:12 + uv:8 = 32 bytes, little-endian float32),
//  so raw network payloads can be uploaded to staging buffers
//  and consumed here without any CPU-side parsing.
//

#include <metal_stdlib>
using namespace metal;

/// Wire/GPU vertex layout — 32 bytes, matches serializer.py output
/// and the LowLevelMesh vertex attributes (offsets 0 / 12 / 24).
struct GSVertex {
    packed_float3 position;
    packed_float3 normal;
    packed_float2 uv;
};

// MARK: - Mesh upload

/// Copy wire vertices from the staging buffer into the LowLevelMesh
/// vertex buffer. Layouts are identical; this kernel exists so the copy
/// happens on the GPU timeline (and is the hook point for future
/// delta application / GPU-side coordinate massaging).
kernel void gs_copyVertices(
    device const GSVertex* src   [[buffer(0)]],
    device GSVertex*       dst   [[buffer(1)]],
    constant uint&         count [[buffer(2)]],
    uint id [[thread_position_in_grid]])
{
    if (id >= count) return;
    dst[id] = src[id];
}

/// Rewrite triangle indices with winding order reversal.
/// The wire carries left-hand winding (i0, i1, i2) targeted at Unity;
/// RealityKit's right-hand system needs (i0, i2, i1).
/// One thread per triangle. Trailing non-triangle indices (if
/// indexCount % 3 != 0) are copied verbatim by gs_copyIndicesTail.
kernel void gs_flipWinding(
    device const uint* src           [[buffer(0)]],
    device uint*       dst           [[buffer(1)]],
    constant uint&     triangleCount [[buffer(2)]],
    uint id [[thread_position_in_grid]])
{
    if (id >= triangleCount) return;
    uint base = id * 3;
    dst[base]     = src[base];
    dst[base + 1] = src[base + 2];
    dst[base + 2] = src[base + 1];
}

/// Copy indices without modification (used for the tail when
/// indexCount is not a multiple of 3).
kernel void gs_copyIndicesTail(
    device const uint* src    [[buffer(0)]],
    device uint*       dst    [[buffer(1)]],
    constant uint&     offset [[buffer(2)]],
    constant uint&     count  [[buffer(3)]],
    uint id [[thread_position_in_grid]])
{
    if (id >= count) return;
    dst[offset + id] = src[offset + id];
}

// MARK: - Instance baking

/// Transform base-mesh vertices by per-instance matrices into one merged
/// vertex buffer — the RealityKit analog of Unity's DrawMeshInstanced
/// (LowLevelMesh has no hardware instancing, so instances are baked
/// into a single mesh on the GPU each update).
///
/// One thread per output vertex: id = instance * baseVertexCount + vertex.
/// Normals use the upper-left 3x3 (assumes uniform-ish scale, same as
/// the Unity instanced shader).
kernel void gs_bakeInstanceVertices(
    device const GSVertex*  baseVerts       [[buffer(0)]],
    device const float4x4*  transforms      [[buffer(1)]],
    device GSVertex*        dst             [[buffer(2)]],
    constant uint&          baseVertexCount [[buffer(3)]],
    constant uint&          instanceCount   [[buffer(4)]],
    uint id [[thread_position_in_grid]])
{
    uint total = baseVertexCount * instanceCount;
    if (id >= total) return;

    uint inst = id / baseVertexCount;
    uint v    = id % baseVertexCount;

    float4x4 m = transforms[inst];
    float3 p = float3(baseVerts[v].position);
    float3 n = float3(baseVerts[v].normal);

    float3x3 nm = float3x3(m[0].xyz, m[1].xyz, m[2].xyz);
    float3 tn = nm * n;
    float len = length(tn);

    dst[id].position = (m * float4(p, 1.0f)).xyz;
    dst[id].normal   = len > 1e-6f ? tn / len : n;
    dst[id].uv       = baseVerts[v].uv;
}

/// Replicate base-mesh indices per instance with a vertex offset.
/// Base indices must already be winding-corrected (gs_flipWinding runs
/// once at base-mesh registration).
/// One thread per output index: id = instance * baseIndexCount + index.
kernel void gs_bakeInstanceIndices(
    device const uint* baseIndices     [[buffer(0)]],
    device uint*       dst             [[buffer(1)]],
    constant uint&     baseIndexCount  [[buffer(2)]],
    constant uint&     baseVertexCount [[buffer(3)]],
    constant uint&     instanceCount   [[buffer(4)]],
    uint id [[thread_position_in_grid]])
{
    uint total = baseIndexCount * instanceCount;
    if (id >= total) return;

    uint inst = id / baseIndexCount;
    uint i    = id % baseIndexCount;

    dst[id] = baseIndices[i] + inst * baseVertexCount;
}
