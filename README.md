# GeometrySync

Real-time Blender Geometry Nodes streaming to Unity with custom shader support.

**📑 [Documentation Index](INDEX.md)** | **⚡ [Quick Start](QUICKSTART.md)** | **📦 [Installation](INSTALL.md)**

## Overview

GeometrySync enables real-time visualization of Blender's Geometry Nodes output in Unity, allowing you to see procedural geometry changes instantly with custom URP shaders.

**Features:**
- 🔄 Real-time mesh streaming from Blender to Unity
- ⚡ Optimized for 30-60 FPS with high vertex counts (50k+)
- 🎨 Support for custom attributes from Geometry Nodes
- 🔌 Simple TCP-based protocol (localhost)
- 📦 Geometry Nodes instance support (Phase 2)
- 🎯 Built for Unity 6000 + URP

## System Requirements

- **Blender**: 4.5+ (with Geometry Nodes)
- **Unity**: 6000+ with URP
- **Python**: NumPy library (for Blender addon)
- **Platform**: Windows/Linux/macOS (localhost streaming)

## Installation

**📖 See [INSTALL.md](INSTALL.md) for detailed installation instructions**

**⚡ See [QUICKSTART.md](QUICKSTART.md) for 5-minute setup guide**

### Quick Install

**Blender:**
1. Install NumPy: `blender/python -m pip install numpy`
2. Edit → Preferences → Add-ons → Install
3. Select `Blender/addons/geometry_sync.zip`
4. Enable "GeometrySync" addon

**Unity:**
1. Copy `Unity/Assets/GeometrySync/` to your project
2. Add `GeometrySyncManager` component to GameObject
3. Assign URP material to MeshRenderer
4. Done!

## Quick Start

### 1. Set up Blender

1. Create or open a scene with Geometry Nodes
2. Open the GeometrySync panel (View3D sidebar → GeometrySync)
3. Click **"Start Server"**
4. Wait for "Waiting for Unity client..." message

### 2. Set up Unity

1. Create a new GameObject in your scene
2. Add the `GeometrySyncManager` component
3. Configure settings:
   - **Host**: `127.0.0.1` (localhost)
   - **Port**: `8080` (default)
   - **Auto Connect**: ✓ (enabled)
   - **Max Updates Per Second**: `60`

4. Assign a material to the MeshRenderer component (any URP material)
5. Enter Play mode

### 3. Test Connection

1. In Blender, modify your Geometry Nodes
2. Watch the mesh update in Unity in real-time!
3. Check Unity's console for connection status

## Usage

### Blender Side

**Control Panel:**
- **Start/Stop Server**: Toggle the TCP server
- **Target FPS**: Set streaming frame rate (1-120 FPS)
- **Connection Status**: Shows if Unity client is connected

**Supported Objects:**
- Any mesh with Geometry Nodes modifier
- Multiple objects (streams last modified object)
- Instanced geometry (Phase 2 feature)

### Unity Side

**GeometrySyncManager Component:**

| Property | Description |
|----------|-------------|
| Host | Blender server address (usually `127.0.0.1`) |
| Port | Server port (default `8080`) |
| Auto Connect | Connect automatically on start |
| Max Updates Per Second | Limit mesh updates for performance |
| Show Debug Info | Display on-screen statistics |
| Log Mesh Updates | Print mesh info to console |

**Runtime Control:**
```csharp
GeometrySyncManager manager = GetComponent<GeometrySyncManager>();

// Connect manually
manager.Connect();

// Disconnect
manager.Disconnect();

// Check connection status
if (manager.IsConnected)
{
    Debug.Log($"Received {manager.MeshUpdateCount} updates");
    Debug.Log($"Current mesh: {manager.LastVertexCount} vertices");
}
```

## Architecture

### Data Flow

```
Blender Geometry Nodes
        ↓
depsgraph_update_post handler (throttled to target FPS)
        ↓
Extract mesh data (vertices, normals, UVs, indices)
        ↓
Binary serialization (32 bytes/vertex)
        ↓
TCP socket → Unity
        ↓
Background thread receiver
        ↓
Thread-safe queue
        ↓
Main thread: Mesh reconstruction (NativeArray)
        ↓
MeshFilter + MeshRenderer (URP)
```

### Binary Protocol

**Message Format:**
```
[Type:1byte][Length:4bytes][Payload:N bytes]
```

**Message Types:**
- `0x01`: Full mesh update
- `0x02`: Delta update (Phase 3)
- `0x03`: Instance data (Phase 2)

**Mesh Payload:**
```
[VertexCount:4][IndexCount:4]
[Vertex0: x,y,z, nx,ny,nz, u,v (32 bytes)]
[Vertex1: x,y,z, nx,ny,nz, u,v (32 bytes)]
...
[Index0:4][Index1:4][Index2:4]...
```

**Coordinate Conversion:**
- Blender: Z-up, right-handed
- Unity: Y-up, left-handed
- Conversion: `(x, y, z)` → `(x, z, -y)`

## Performance

**Phase 1 Targets:**
- ✅ 30 FPS minimum @ 50k vertices
- ✅ <50ms latency on localhost
- ✅ Zero GC allocations during streaming

**Optimization Tips:**
1. Use **Max Updates Per Second** to limit bandwidth
2. Reduce Geometry Nodes complexity for higher FPS
3. Enable **MarkDynamic** on meshes (automatic)
4. Use **Show Debug Info** to monitor performance

**Typical Performance:**
- 10k vertices: 60 FPS+
- 50k vertices: 30-60 FPS
- 100k vertices: 15-30 FPS

## Roadmap

### ✅ Phase 1: Core Infrastructure (Complete)
- [x] Blender TCP server
- [x] Mesh extraction from Geometry Nodes
- [x] Binary serialization
- [x] Unity TCP client with threading
- [x] Modern Mesh API integration
- [x] URP rendering

### 🚧 Phase 2: Geometry Nodes Instances (In Progress)
- [ ] Instance transform extraction
- [ ] `DrawMeshInstanced` rendering
- [ ] Custom attribute support
- [ ] Per-instance data

### 📋 Phase 3: Optimization
- [ ] Delta encoding (position compression)
- [ ] Octahedron normal encoding
- [ ] Optional LZ4 compression
- [ ] Adaptive quality system

### 📋 Phase 4: Advanced Features
- [ ] Multi-object streaming
- [ ] Material synchronization
- [ ] Scene hierarchy
- [ ] Custom shader attributes

## Troubleshooting

### Blender: "No module named numpy"
Install NumPy for Blender's Python:
```bash
# Find Blender's Python
# Windows: C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin
# macOS: /Applications/Blender.app/Contents/Resources/4.5/python/bin
# Linux: /usr/share/blender/4.5/python/bin

python -m pip install numpy
```

### Unity: "Connection error: Connection refused"
- Ensure Blender server is started (green "Connected" or "Waiting for Unity...")
- Check firewall settings allow localhost connections
- Verify port 8080 is not in use by another application

### Unity: Mesh not updating
- Check Blender console for "Streamed [object]" messages
- Verify object has Geometry Nodes modifier
- Try modifying the Geometry Nodes to trigger update
- Check Unity's **Log Mesh Updates** option

### Performance: Low FPS
- Reduce Geometry Nodes complexity
- Lower **Target FPS** in Blender
- Lower **Max Updates Per Second** in Unity
- Check if mesh exceeds 50k vertices

## Example Scenes

### Blender Test Scene

1. Create a new scene
2. Add a Geometry Nodes modifier to the default cube
3. Create a simple node tree:
   - Mesh → Subdivide Surface (level 3)
   - Set Position → Noise Texture (for animation)
4. Start GeometrySync server
5. Animate the noise scale to see real-time updates

### Unity Test Scene

1. Create empty GameObject named "GeometrySync"
2. Add `GeometrySyncManager` component
3. Create URP/Lit material and assign to MeshRenderer
4. Add directional light for proper normals visualization
5. Enter Play mode

## Credits

**Built with reference to:**
- [unity3d-jp/MeshSync](https://github.com/unity3d-jp/MeshSync) - DCC to Unity synchronization
- [keijiro/NoiseBall6](https://github.com/keijiro/NoiseBall6) - Modern Mesh API usage
- [glowbox/mesh-streamer](https://github.com/glowbox/mesh-streamer) - Mesh streaming architecture

## License

MIT License - See LICENSE file for details

## Support

For issues, feature requests, or questions:
- GitHub Issues: [Create an issue](https://github.com/your-repo/GeometrySync/issues)
- Documentation: See `/docs` folder (coming soon)

---

**Version**: 1.0.0 (Phase 1)
**Last Updated**: 2025-11-24
