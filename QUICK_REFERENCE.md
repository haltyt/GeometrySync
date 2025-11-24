# GeometrySync Quick Reference

Quick lookup for common tasks and API reference.

## Installation Commands

### Install NumPy (Blender)
```bash
# Windows
"C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin\python.exe" -m pip install numpy

# macOS
/Applications/Blender.app/Contents/Resources/4.5/python/bin/python3.11 -m pip install numpy

# Linux
/usr/share/blender/4.5/python/bin/python3.11 -m pip install numpy
```

### Verify Installation
```python
# In Blender Python Console
import numpy
print(numpy.__version__)
```

## Blender Addon API

### Start/Stop Server (Python)
```python
import bpy

# Start server
bpy.ops.geometrysync.start_server()

# Stop server
bpy.ops.geometrysync.stop_server()

# Check if running
from geometry_sync import server
srv = server.get_server()
print(srv.running, srv.is_connected())
```

### Set Target FPS
```python
from geometry_sync import handlers

# Set to 60 FPS
handlers.set_target_fps(60)

# Set to 30 FPS
handlers.set_target_fps(30)
```

### Manual Mesh Send
```python
from geometry_sync import server, extractor, serializer
import bpy

# Get server
srv = server.get_server()

# Extract mesh from object
obj = bpy.data.objects['Cube']
depsgraph = bpy.context.evaluated_depsgraph_get()
vertices, normals, uvs, indices = extractor.extract_mesh_data_fast(obj, depsgraph)

# Serialize and send
data = serializer.serialize_mesh(vertices, normals, uvs, indices)
srv.send_mesh(data)
```

## Unity C# API

### GeometrySyncManager

```csharp
using GeometrySync;
using UnityEngine;

public class Example : MonoBehaviour
{
    private GeometrySyncManager manager;

    void Start()
    {
        manager = GetComponent<GeometrySyncManager>();

        // Manual connection
        manager.Connect();
    }

    void Update()
    {
        // Check status
        if (manager.IsConnected)
        {
            Debug.Log($"Updates: {manager.MeshUpdateCount}");
            Debug.Log($"Vertices: {manager.LastVertexCount}");
        }
    }

    void OnDestroy()
    {
        // Clean disconnect
        manager.Disconnect();
    }
}
```

### MeshStreamClient (Advanced)

```csharp
using GeometrySync;
using UnityEngine;

public class CustomClient : MonoBehaviour
{
    private MeshStreamClient client;
    private MeshReconstructor reconstructor;

    void Start()
    {
        // Create custom client
        client = new MeshStreamClient("127.0.0.1", 8080);
        client.Connect();

        // Create reconstructor
        reconstructor = new MeshReconstructor();

        // Assign mesh
        GetComponent<MeshFilter>().mesh = reconstructor.Mesh;
    }

    void Update()
    {
        // Process mesh queue
        while (client.TryGetMesh(out MeshData meshData))
        {
            reconstructor.UpdateMesh(meshData);
        }
    }

    void OnDestroy()
    {
        client?.Dispose();
        reconstructor?.Dispose();
    }
}
```

### Custom Deserialization

```csharp
using GeometrySync;
using UnityEngine;

// Deserialize from byte array
byte[] data = ReceiveFromNetwork();
MeshData meshData = MeshDeserializer.Deserialize(data);

// Access data
Debug.Log($"Vertices: {meshData.VertexCount}");
Debug.Log($"Triangles: {meshData.TriangleCount}");

foreach (Vector3 vertex in meshData.Vertices)
{
    Debug.Log($"Vertex: {vertex}");
}
```

## Binary Protocol Reference

### Message Header
```
Byte 0:      Message Type
  0x01 = Full mesh update
  0x02 = Delta update (future)
  0x03 = Instance data (future)

Bytes 1-4:   Payload Length (uint32, little-endian)
```

### Mesh Payload (Type 0x01)
```
Bytes 0-3:   Vertex Count (uint32)
Bytes 4-7:   Index Count (uint32)

For each vertex (32 bytes):
  0-11:   Position (float32 × 3) - Unity space
  12-23:  Normal (float32 × 3) - Unity space
  24-31:  UV (float32 × 2)

For each index (4 bytes):
  0-3:    Index (uint32)
```

### Coordinate Conversion
```
Blender (Z-up, right-handed) → Unity (Y-up, left-handed)

Position:  (x, y, z) → (x, z, -y)
Normal:    (nx, ny, nz) → (nx, nz, -ny)
UV:        (u, v) → (u, v)  [unchanged]
```

## Common Geometry Node Setups

### Animated Noise Deformation
```
Group Input
  → Subdivide Surface (level: 3)
  → Set Position
      Position: [default]
      Offset: Noise Texture (Scale: 2.0, W: keyframed 0→10)
  → Group Output
```

### Scatter Instances (Phase 2)
```
Group Input
  → Distribute Points on Faces (Density: 100)
  → Instance on Points
      Instance: Cube (scaled 0.1)
      Rotation: Random Value (0-360°)
  → Group Output
```

### Custom Attributes (Phase 2)
```
Group Input
  → Set Position (with custom offset)
  → Store Named Attribute
      Name: "weight"
      Type: Float
      Value: Noise Texture
  → Group Output
```

## Performance Tuning

### Low Latency (VR/AR)
```
Blender:
  Target FPS: 60-120

Unity:
  Max Updates Per Second: 120

Constraints:
  Vertices: <20,000
  Expected latency: 10-20ms
```

### High Quality (Desktop)
```
Blender:
  Target FPS: 30

Unity:
  Max Updates Per Second: 60

Constraints:
  Vertices: <100,000
  Expected latency: 30-50ms
```

### Bandwidth Optimization
```
Reduce FPS:
  30 FPS = half bandwidth of 60 FPS

Reduce Geometry:
  Use Decimate modifier before Geometry Nodes
  Lower subdivision levels

Future (Phase 3):
  Enable delta encoding
  Enable octahedron normals
```

## Debugging

### Blender Console Output
```python
# Enable system console (Windows)
# Window → Toggle System Console

# Check server status
from geometry_sync import server
srv = server.get_server()
print(f"Running: {srv.running}")
print(f"Connected: {srv.is_connected()}")

# Check handler status
from geometry_sync import handlers
sch = handlers.get_scheduler()
print(f"Enabled: {sch.enabled}")
print(f"Target FPS: {sch.target_fps}")
print(f"Dirty objects: {sch.dirty_objects}")
```

### Unity Debug Info
```csharp
// Enable debug display
manager.showDebugInfo = true;

// Enable console logging
manager.logMeshUpdates = true;

// Manual stats access
Debug.Log($"Connected: {manager.IsConnected}");
Debug.Log($"Updates: {manager.MeshUpdateCount}");
Debug.Log($"Vertices: {manager.LastVertexCount}");
Debug.Log($"Triangles: {manager.LastTriangleCount}");
```

### Network Monitoring

**Check if port is in use:**
```bash
# Windows
netstat -ano | findstr :8080

# macOS/Linux
lsof -i :8080
```

**Test TCP connection:**
```bash
# Using telnet
telnet 127.0.0.1 8080

# Using nc (netcat)
nc -zv 127.0.0.1 8080
```

## Error Messages

### "ModuleNotFoundError: No module named 'numpy'"
**Solution**: Install NumPy for Blender's Python (see Installation Commands above)

### "Connection refused"
**Causes**:
- Blender server not started
- Wrong port number
- Firewall blocking

**Solution**:
1. Click "Start Server" in Blender
2. Verify port matches (default 8080)
3. Check firewall allows localhost

### "Invalid mesh data size"
**Causes**:
- Network transmission error
- Binary format mismatch
- Corrupted data

**Solution**:
1. Restart both Blender and Unity
2. Check Unity console for exact error
3. Verify Blender addon version matches Unity package

### "Failed to read complete payload"
**Causes**:
- Connection interrupted
- Mesh too large
- Timeout

**Solution**:
1. Check network stability
2. Reduce mesh complexity
3. Lower streaming FPS

## Shader Integration (URP)

### Basic Shader Usage
```csharp
// Assign shader
Material mat = new Material(Shader.Find("GeometrySync/Basic URP"));
GetComponent<MeshRenderer>().material = mat;

// Set properties
mat.SetColor("_BaseColor", Color.white);
mat.SetFloat("_Smoothness", 0.5f);
mat.SetFloat("_Metallic", 0.0f);
```

### Custom Shader Template
```hlsl
Shader "Custom/GeometrySyncCustom"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
```

## Keyboard Shortcuts

### Blender
| Key | Action |
|-----|--------|
| `N` | Toggle sidebar (access GeometrySync panel) |
| `Spacebar` | Play/pause animation |
| `Shift+A` | Add menu (for Geometry Nodes) |

### Unity
| Key | Action |
|-----|--------|
| `Ctrl+P` | Enter/exit Play mode |
| `F` | Frame selected object |
| `Ctrl+Shift+F` | Align scene view to game view |

## File Locations

### Blender
```
Addon: Blender/addons/geometry_sync/
Config: (none - runtime only)
Logs: System Console (Window → Toggle System Console)
```

### Unity
```
Scripts: Assets/GeometrySync/Runtime/
Shaders: Assets/GeometrySync/Shaders/
Logs: Console window (Ctrl+Shift+C)
Player log: %USERPROFILE%\AppData\LocalLow\<Company>\<Project>\Player.log
```

## Version Info

**Current Version**: 1.0.0 (Phase 1)

**Compatibility**:
- Blender: 4.5+
- Unity: 6000+
- URP: 17.0+
- NumPy: 1.20+

**Phase Status**:
- ✅ Phase 1: Core streaming
- 🚧 Phase 2: Instances & attributes
- 📋 Phase 3: Optimization
- 📋 Phase 4: Multi-object

---

**Last Updated**: 2025-11-24
