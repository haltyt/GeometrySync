# GeometrySync Project Structure

Complete overview of the project architecture and file organization.

## Directory Structure

```
GeometrySync/
│
├── README.md                          # Main documentation
├── SETUP_GUIDE.md                     # Step-by-step setup instructions
├── PROJECT_STRUCTURE.md               # This file
│
├── Blender/                           # Blender addon and test files
│   ├── addons/
│   │   └── geometry_sync/             # Main addon directory
│   │       ├── __init__.py            # Addon registration & metadata
│   │       ├── server.py              # TCP server implementation
│   │       ├── extractor.py           # Mesh data extraction from Geometry Nodes
│   │       ├── serializer.py          # Binary serialization
│   │       ├── handlers.py            # Depsgraph handlers & throttling
│   │       └── ui.py                  # Blender UI panel
│   │
│   └── test_scenes/                   # Example Blender scenes
│       ├── basic_mesh.blend           # (Create: Simple subdivided cube)
│       └── instanced_nodes.blend      # (Create: Geometry Nodes instances)
│
└── Unity/                             # Unity package
    ├── Assets/
    │   └── GeometrySync/              # Main package folder
    │       ├── Runtime/               # Runtime scripts
    │       │   ├── MeshStreamClient.cs       # TCP client (background thread)
    │       │   ├── MeshDeserializer.cs       # Binary deserialization
    │       │   ├── MeshReconstructor.cs      # Mesh rebuilding (NativeArray)
    │       │   └── GeometrySyncManager.cs    # Main coordinator component
    │       │
    │       ├── Shaders/               # Example shaders
    │       │   └── GeometrySyncBasic.shader  # Basic URP/Lit shader
    │       │
    │       └── Editor/                # Editor extensions (future)
    │           └── (future: inspector UI)
    │
    └── Scenes/                        # Example Unity scenes
        └── GeometrySyncDemo.unity     # (Create: Demo scene)
```

## File Responsibilities

### Blender Addon

#### `__init__.py`
- **Purpose**: Addon entry point
- **Responsibilities**:
  - Blender addon metadata (bl_info)
  - Register/unregister UI and handlers
  - Cleanup on disable
- **Dependencies**: ui, handlers, server modules

#### `server.py`
- **Purpose**: TCP server for Unity communication
- **Key Classes**:
  - `MeshStreamServer`: Main server managing connections
- **Key Methods**:
  - `start()`: Launch server in background thread
  - `send_mesh(bytes)`: Send binary mesh data
  - `is_connected()`: Check client status
- **Threading**: Background daemon thread for accept loop
- **Protocol**: TCP with `TCP_NODELAY` for low latency

#### `extractor.py`
- **Purpose**: Extract mesh data from evaluated Geometry Nodes
- **Key Functions**:
  - `extract_mesh_data_fast()`: Fast extraction using `foreach_get`
  - `extract_custom_attributes()`: Get Geometry Nodes attributes
  - `extract_instance_transforms()`: Get instance data (Phase 2)
- **Performance**:
  - Uses NumPy for zero-copy operations
  - Direct depsgraph access via `evaluated_get()`
  - Triangulates polygons for Unity

#### `serializer.py`
- **Purpose**: Binary serialization of mesh data
- **Key Functions**:
  - `serialize_mesh()`: Interleaved vertex format
  - `convert_to_unity_space()`: Blender→Unity coordinates
  - `serialize_instance_data()`: Instance transforms (Phase 2)
  - `serialize_custom_attributes()`: Custom data (Phase 2)
- **Binary Format**:
  - Header: vertex_count(4) + index_count(4)
  - Vertices: [x,y,z, nx,ny,nz, u,v] × N (32 bytes each)
  - Indices: [i0,i1,i2...] × M (4 bytes each)

#### `handlers.py`
- **Purpose**: Depsgraph change detection and streaming throttle
- **Key Classes**:
  - `StreamingScheduler`: FPS throttling system
- **Key Functions**:
  - `depsgraph_update_handler()`: Mark objects dirty on change
  - `streaming_timer_function()`: Throttled mesh extraction/send
  - `register_handlers()`: Install Blender handlers
- **Throttling Strategy**:
  - Depsgraph handler: lightweight, just marks dirty
  - Timer function: heavy lifting at target FPS
  - Thread-safe dirty tracking

#### `ui.py`
- **Purpose**: Blender 3D Viewport panel
- **Key Classes**:
  - `GEOMETRYSYNC_PT_MainPanel`: Main UI panel
  - `GEOMETRYSYNC_OT_StartServer`: Start server operator
  - `GEOMETRYSYNC_OT_StopServer`: Stop server operator
- **Properties**:
  - `geometrysync_fps`: Target streaming FPS (1-120)
- **Location**: View3D sidebar → GeometrySync tab

---

### Unity Package

#### `MeshStreamClient.cs`
- **Purpose**: TCP client for receiving mesh data
- **Key Methods**:
  - `Connect()`: Start background receiver thread
  - `TryGetMesh(out MeshData)`: Dequeue next mesh
  - `Disconnect()`: Clean shutdown
- **Threading**:
  - Background thread: Network I/O
  - Main thread: Mesh queue consumption
- **Queue**: `ConcurrentQueue<MeshData>` (max 2 frames)
- **Protocol**: Reads `[type:1][length:4][payload:N]`

#### `MeshDeserializer.cs`
- **Purpose**: Binary deserialization
- **Key Struct**:
  - `MeshData`: Container for vertices/normals/UVs/indices
- **Key Methods**:
  - `Deserialize(byte[])`: Parse binary mesh format
  - `DeserializeNative(byte[])`: Future NativeArray optimization
- **Validation**: Sanity checks for counts and sizes
- **Output**: Managed arrays (Phase 1), NativeArray (Phase 3)

#### `MeshReconstructor.cs`
- **Purpose**: Rebuild Unity Mesh from received data
- **Key Methods**:
  - `UpdateMesh(MeshData)`: Apply mesh using modern API
  - `UpdateMeshTraditional(MeshData)`: Fallback method
- **Optimization**:
  - Persistent `NativeArray` buffers (reused)
  - `Allocator.Persistent` for zero GC
  - `MeshUpdateFlags`: Skip bounds recalc, validation
- **Buffer Management**:
  - Auto-resize with power-of-two allocation
  - Manual dispose on cleanup

#### `GeometrySyncManager.cs`
- **Purpose**: Main MonoBehaviour coordinator
- **Key Methods**:
  - `Connect()`: Start client connection
  - `Disconnect()`: Stop client
  - `Update()`: Process mesh queue with throttling
- **Inspector Properties**:
  - Connection: host, port, autoConnect
  - Performance: maxUpdatesPerSecond
  - Debug: showDebugInfo, logMeshUpdates
- **Components Required**:
  - MeshFilter (auto-added)
  - MeshRenderer (auto-added)

#### `GeometrySyncBasic.shader`
- **Purpose**: Example URP shader
- **Features**:
  - Universal Render Pipeline compatible
  - PBR lighting (UniversalFragmentPBR)
  - Shadow casting/receiving
  - Properties: BaseColor, Smoothness, Metallic
- **Passes**:
  - ForwardLit: Main rendering
  - ShadowCaster: Shadow maps
  - DepthOnly: Depth pre-pass

---

## Data Flow Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│                          BLENDER SIDE                             │
└──────────────────────────────────────────────────────────────────┘

    User edits Geometry Nodes
            ↓
    depsgraph_update_post handler
            ↓
    Mark object dirty (thread-safe)
            ↓
    Timer function (throttled @ target FPS)
            ↓
    ┌─────────────────────────────┐
    │ extract_mesh_data_fast()     │
    │ • obj.evaluated_get()        │
    │ • foreach_get (fast NumPy)   │
    │ • Triangulation              │
    └─────────────────────────────┘
            ↓
    ┌─────────────────────────────┐
    │ serialize_mesh()             │
    │ • Coordinate conversion      │
    │ • Interleaved format         │
    │ • 32 bytes/vertex            │
    └─────────────────────────────┘
            ↓
    ┌─────────────────────────────┐
    │ TCP Send                     │
    │ [type:1][len:4][payload:N]   │
    └─────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                        NETWORK LAYER                              │
└──────────────────────────────────────────────────────────────────┘

    TCP/IP (localhost:8080)
    • TCP_NODELAY enabled
    • Reliable, ordered delivery
    • ~10-50ms latency

┌──────────────────────────────────────────────────────────────────┐
│                          UNITY SIDE                               │
└──────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────┐
    │ Background Thread            │
    │ • Read header                │
    │ • Read payload               │
    │ • Deserialize                │
    └─────────────────────────────┘
            ↓
    ConcurrentQueue<MeshData>
    (max 2 frames, drop old)
            ↓
    ┌─────────────────────────────┐
    │ Main Thread (Update)         │
    │ • TryGetMesh()               │
    │ • Throttle @ maxUpdates/sec  │
    └─────────────────────────────┘
            ↓
    ┌─────────────────────────────┐
    │ MeshReconstructor            │
    │ • NativeArray buffers        │
    │ • SetVertexBufferData()      │
    │ • SetIndexBufferData()       │
    └─────────────────────────────┘
            ↓
    Mesh → MeshFilter → MeshRenderer
            ↓
    URP Rendering Pipeline
```

## Threading Model

### Blender

```
Main Thread:
  • UI events
  • Depsgraph handler (lightweight marking)
  • Timer function (mesh extraction @ FPS)

Background Thread (Server):
  • Accept client connections
  • Keep-alive loop
  • Actual send happens on main thread
```

### Unity

```
Background Thread (Receiver):
  • TCP read loop
  • Binary deserialization
  • Enqueue to ConcurrentQueue

Main Thread (MonoBehaviour):
  • Dequeue mesh data
  • Mesh reconstruction
  • Rendering
```

## Memory Management

### Blender
- **NumPy arrays**: Stack-allocated, auto-freed
- **Binary buffers**: Created per-frame, GC'd by Python
- **Optimization**: Use `foreach_get` to avoid Python object creation

### Unity
- **NativeArray**: Persistent allocation, manual dispose
- **Managed arrays**: Initial deserialize (Phase 1)
- **ComputeBuffer**: Future use for custom attributes (Phase 2)
- **Target**: Zero GC allocations after warmup

## Performance Characteristics

### Typical Mesh Sizes

| Vertices | Triangles | Binary Size | Blender FPS | Unity FPS | Latency |
|----------|-----------|-------------|-------------|-----------|---------|
| 1,000    | 500       | ~32 KB      | 120+        | 120+      | 10ms    |
| 10,000   | 5,000     | ~320 KB     | 60+         | 60+       | 20ms    |
| 50,000   | 25,000    | ~1.6 MB     | 30-60       | 30-60     | 30ms    |
| 100,000  | 50,000    | ~3.2 MB     | 15-30       | 15-30     | 50ms    |

### Bandwidth Usage

```
Bandwidth = VertexCount × 32 bytes × FPS

Examples:
• 10k verts @ 30 FPS = 9.6 MB/s
• 50k verts @ 30 FPS = 48 MB/s
• 100k verts @ 15 FPS = 48 MB/s
```

## Extension Points (Future Phases)

### Phase 2: Instances & Attributes

**New Files:**
- `Blender/addons/geometry_sync/instance_extractor.py`
- `Unity/Assets/GeometrySync/Runtime/InstanceRenderer.cs`
- `Unity/Assets/GeometrySync/Runtime/AttributeBuffer.cs`

**Changes:**
- `extractor.py`: Implement instance extraction
- `serializer.py`: Add attribute serialization
- `MeshStreamClient.cs`: Handle type 0x03 messages
- New shader: `GeometrySyncInstanced.shader`

### Phase 3: Optimization

**New Files:**
- `Blender/addons/geometry_sync/compression.py` (delta, octahedron)
- `Unity/Assets/GeometrySync/Runtime/DecompressionShader.compute`
- `Unity/Assets/GeometrySync/Runtime/AdaptiveQuality.cs`

**Changes:**
- `serializer.py`: Add compression options
- `MeshDeserializer.cs`: Decompression support
- `handlers.py`: Adaptive FPS based on latency

### Phase 4: Multi-Object

**New Files:**
- `Blender/addons/geometry_sync/scene_manager.py`
- `Unity/Assets/GeometrySync/Runtime/SceneSync.cs`

**Changes:**
- Protocol: Add object ID to messages
- Unity: Dynamic GameObject creation
- Hierarchy synchronization

## Configuration Files

### Blender Addon Preferences (Future)

```python
# In __init__.py
class GeometrySyncPreferences(bpy.types.AddonPreferences):
    bl_idname = __name__

    default_port: IntProperty(name="Default Port", default=8080)
    default_fps: IntProperty(name="Default FPS", default=30)
    # ...
```

### Unity ScriptableObject Settings (Future)

```csharp
// GeometrySyncSettings.asset
public class GeometrySyncSettings : ScriptableObject
{
    public string defaultHost = "127.0.0.1";
    public int defaultPort = 8080;
    public int bufferPoolSize = 10;
    // ...
}
```

## Build & Distribution

### Blender Addon Distribution

**Create .zip for Blender Add-on Manager:**
```bash
cd Blender/addons
zip -r geometry_sync.zip geometry_sync/
```

**Install in Blender:**
- Edit → Preferences → Add-ons → Install
- Select `geometry_sync.zip`

### Unity Package Distribution

**Create .unitypackage:**
1. Select `Assets/GeometrySync` folder
2. Assets → Export Package
3. Include dependencies
4. Save as `GeometrySync.unitypackage`

**Or use UPM (Package Manager):**
Create `package.json` in `Assets/GeometrySync/`:
```json
{
  "name": "com.geometrysync.core",
  "version": "1.0.0",
  "displayName": "GeometrySync",
  "description": "Real-time Blender Geometry Nodes streaming",
  "unity": "6000.0"
}
```

## Testing Strategy

### Unit Tests (Future)

**Blender:**
- Test mesh extraction accuracy
- Test coordinate conversion
- Test binary serialization round-trip

**Unity:**
- Test deserialization with known data
- Test mesh reconstruction
- Test threading safety

### Integration Tests

**Manual Test Checklist:**
- [ ] Connection establishment
- [ ] Simple cube streaming
- [ ] Animated Geometry Nodes
- [ ] Reconnection after disconnect
- [ ] High vertex count (100k+)
- [ ] Rapid parameter changes
- [ ] Multiple objects (Phase 4)

---

**Last Updated**: 2025-11-24
**Version**: 1.0.0 (Phase 1)
