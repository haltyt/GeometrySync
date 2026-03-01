# CLAUDE.md

## Project Overview

GeometrySync — Blender → Unity リアルタイムメッシュストリーミング。Blender側がTCPサーバー(localhost:8080)としてGeometry Nodesの出力をバイナリシリアライズし、Unity側がバックグラウンドスレッドで受信・メインスレッドでメッシュ再構築する。

## Repository Structure

Two independent codebases:

**Blender/addons/geometry_sync/** (Python)
- `server.py` — TCP server
- `extractor.py` — mesh extraction (NumPy)
- `serializer.py` — binary encoding + coordinate conversion
- `handlers.py` — depsgraph update + FPS throttling
- `ui.py` — panel / operators
- `__init__.py` — addon registration

**Unity/GeometrySync/Assets/GeometrySync/Runtime/** (C#)
- `GeometrySyncManager.cs` — coordinator MonoBehaviour
- `MeshStreamClient.cs` — TCP client + background thread
- `MeshDeserializer.cs` — binary parsing
- `MeshReconstructor.cs` — NativeArray mesh rebuild
- `GPUInstanceRenderer.cs` — DrawMeshInstanced / Indirect
- `VFXGraphBridge.cs` — VFX Graph integration

**Unity/GeometrySync/Assets/GeometrySync/Shaders/**
- `GeometrySyncBasic.shader` — URP/Lit
- `InstancedIndirect.shader` — compute-based indirect rendering

## Build & Run

No traditional build system. Both sides are interpreted/managed.

- **Blender**: Install addon via Preferences → Add-ons. Requires NumPy. Blender 4.5+.
- **Unity**: Open Unity/GeometrySync/ in Unity 6000+ with URP. Dependencies: URP 17.0.4, VFX Graph 17.0.4.
- **Testing**: Manual only. `Blender/test_streaming.py` for Blender-side tests. Unity demo scenes: GeometrySyncDemo, VFXIntegrationDemo.

## Key Architecture

**Binary protocol**: `[Type:1B][Length:4B][Payload:NB]`
- 0x01 = full mesh, 0x02 = instance data, 0x03 = instance transforms
- Vertex stride: 32 bytes (pos:12 + normal:12 + uv:8)
- 32-bit indices for meshes >65k vertices

**Coordinate conversion**: Blender Z-up → Unity Y-up: `(x, y, z) → (x, z, -y)` in serializer.py.

**Threading**: Blender depsgraph_update_post + timer throttling. Unity receives on background thread → ConcurrentQueue → main thread. NativeArray persistent buffers with power-of-two allocation (zero GC goal).

## Conventions

- Python: PEP 8 informal / C#: standard Unity conventions
- Git commits: conventional prefixes (feat:, fix:)
- Documentation: INDEX.md is navigation hub for 25+ markdown docs in project root
