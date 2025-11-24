# GeometrySync Setup Guide

Step-by-step instructions to get GeometrySync running.

## Prerequisites Checklist

- [ ] Blender 4.5 or later installed
- [ ] Unity 6000 or later with URP
- [ ] Python pip available in Blender's Python

## Step 1: Install NumPy for Blender

### Windows

```bash
# Navigate to Blender's Python bin directory
cd "C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin"

# Install NumPy
.\python.exe -m pip install numpy
```

### macOS

```bash
# Navigate to Blender's Python bin directory
cd /Applications/Blender.app/Contents/Resources/4.5/python/bin

# Install NumPy
./python3.11 -m pip install numpy
```

### Linux

```bash
# Navigate to Blender's Python bin directory
cd /usr/share/blender/4.5/python/bin

# Install NumPy
./python3.11 -m pip install numpy
```

**Verify Installation:**
Open Blender → Scripting tab → Python Console:
```python
import numpy
print(numpy.__version__)  # Should print version number
```

## Step 2: Install Blender Addon

1. **Locate the addon:**
   - Path: `GeometrySync/Blender/addons/geometry_sync/`

2. **Install in Blender:**
   - Open Blender
   - Edit → Preferences → Add-ons
   - Click "Install..." button
   - Navigate to `GeometrySync/Blender/addons/`
   - Select `geometry_sync.zip` file
   - Click "Install Add-on"

   **Alternative method** (manual install):
   - Copy the entire `geometry_sync` folder
   - Paste into Blender's addons directory:
     - Windows: `%APPDATA%\Blender Foundation\Blender\4.5\scripts\addons\`
     - macOS: `~/Library/Application Support/Blender/4.5/scripts/addons/`
     - Linux: `~/.config/blender/4.5/scripts/addons/`
   - Restart Blender

3. **Enable the addon:**
   - Search for "GeometrySync" in the add-ons list
   - Check the checkbox to enable it
   - The addon should show: **GeometrySync** (3D View)

4. **Verify installation:**
   - Press `N` in the 3D Viewport to open the sidebar
   - Look for a "GeometrySync" tab
   - You should see the control panel

## Step 3: Set Up Unity Project

### Option A: New Unity Project

1. **Create project:**
   - Unity Hub → New Project
   - Template: **3D (URP)**
   - Name: GeometrySyncTest
   - Create project

2. **Import GeometrySync:**
   - Copy `GeometrySync/Unity/Assets/GeometrySync/` folder
   - Paste into your project's `Assets/` folder
   - Wait for Unity to compile scripts

### Option B: Existing Unity Project

1. **Ensure URP is installed:**
   - Window → Package Manager
   - Search: "Universal RP"
   - Install if not present

2. **Configure URP:**
   - Assets → Create → Rendering → URP Asset
   - Name it "URPSettings"
   - Edit → Project Settings → Graphics
   - Assign "URPSettings" to Scriptable Render Pipeline Settings

3. **Import GeometrySync:**
   - Copy `GeometrySync/Unity/Assets/GeometrySync/` folder
   - Paste into your project's `Assets/` folder

## Step 4: Create Test Scene (Blender)

1. **Open Blender** (or create new scene)

2. **Set up Geometry Nodes:**
   ```
   - Select the default Cube
   - Geometry Nodes workspace
   - Add Modifier → Geometry Nodes
   - Click "New" to create node tree
   ```

3. **Create simple animated node setup:**
   ```
   Group Input → Subdivide Surface (level: 3)
                → Set Position (Offset: Noise Texture)
                → Group Output
   ```

4. **Configure noise animation:**
   - Add Noise Texture node
   - Connect to Set Position's Offset
   - Set Scale: 2.0
   - Add Value node for W coordinate
   - Keyframe W: 0.0 at frame 1, 10.0 at frame 100

5. **Test the setup:**
   - Play animation (Spacebar)
   - The cube should deform with noise

## Step 5: Create Test Scene (Unity)

1. **Create GameObject:**
   - Hierarchy → Create Empty
   - Name: "BlenderMesh"
   - Position: (0, 0, 0)

2. **Add GeometrySyncManager:**
   - Select "BlenderMesh"
   - Inspector → Add Component
   - Search: "GeometrySync Manager"
   - Add component

3. **Configure settings:**
   ```
   Host: 127.0.0.1
   Port: 8080
   Auto Connect: ✓
   Max Updates Per Second: 60
   Show Debug Info: ✓
   ```

4. **Create material:**
   - Project → Create → Material
   - Name: "GeometrySyncMaterial"
   - Shader: "GeometrySync/Basic URP" (or any URP/Lit shader)
   - Assign color of your choice

5. **Assign material:**
   - Select "BlenderMesh"
   - Inspector → Mesh Renderer
   - Drag "GeometrySyncMaterial" to Materials

6. **Add lighting:**
   - Hierarchy → Light → Directional Light (if not exists)
   - Set rotation: (50, -30, 0) for nice angle

## Step 6: First Connection Test

### Start Blender Server

1. **Open GeometrySync panel:**
   - Blender 3D Viewport
   - Press `N` → GeometrySync tab

2. **Start server:**
   - Click **"Start Server"** button
   - Console should show: "GeometrySync server started on 127.0.0.1:8080"
   - Status: "Waiting for Unity client..."

### Connect from Unity

1. **Enter Play mode:**
   - Click Play button (or Ctrl+P)

2. **Check connection:**
   - Blender console: "Unity client connected from ('127.0.0.1', XXXXX)"
   - Unity on-screen display: "Status: Connected"

3. **Test streaming:**
   - In Blender, modify Geometry Nodes:
     - Change Subdivide Surface level
     - Adjust Noise Texture scale
     - Move object
   - Unity should update the mesh in real-time!

## Step 7: Verify Streaming

### What You Should See

**Blender:**
- Console messages: "Streamed Cube: N vertices, M triangles"
- Panel status: "Connected" (green icon)

**Unity:**
- Mesh updates as you edit Geometry Nodes
- On-screen stats show:
  - Status: Connected (green)
  - Updates: incrementing counter
  - Vertices/Triangles: changing with edits
  - Queue: should be 0-1 (low latency)

### Test Checklist

- [ ] Connection establishes automatically
- [ ] Mesh appears in Unity scene
- [ ] Mesh updates when Geometry Nodes change
- [ ] No console errors in Unity
- [ ] Blender console shows "Streamed" messages
- [ ] FPS stays above 30 (check Unity stats)

## Troubleshooting

### "ModuleNotFoundError: No module named 'numpy'"

**Solution:**
```bash
# Reinstall NumPy with correct Python
# Find your Blender's Python first:
# Blender → Scripting → Python Console → type: import sys; print(sys.executable)

# Then install:
/path/to/blender/python -m pip install numpy
```

### "Connection refused" in Unity

**Check:**
1. Blender server is running (Start Server clicked)
2. Firewall allows localhost connections
3. Port 8080 not used by another app

**Test port:**
```bash
# Windows
netstat -ano | findstr :8080

# Mac/Linux
lsof -i :8080
```

### Mesh not appearing in Unity

**Verify:**
1. GameObject has MeshFilter component (auto-added)
2. GameObject has MeshRenderer component (auto-added)
3. Material is assigned to MeshRenderer
4. Object is in camera view (Scene view)

### No mesh updates

**Verify:**
1. Blender object has Geometry Nodes modifier
2. Modifier is enabled (eye icon)
3. Try adding/removing nodes to trigger update
4. Check Blender console for "Streamed" messages

### Low FPS / Stuttering

**Solutions:**
1. Lower subdivisions in Geometry Nodes
2. Reduce **Target FPS** in Blender panel (try 30)
3. Lower **Max Updates Per Second** in Unity (try 30)
4. Check vertex count (should be <50k for smooth 60 FPS)

## Next Steps

✅ Basic streaming working
→ Try animating Geometry Nodes parameters
→ Test with different node setups
→ Experiment with custom attributes (Phase 2)
→ Create custom URP shaders

## Advanced Configuration

### Custom Port

**Blender:**
Edit `server.py`:
```python
def __init__(self, host: str = '127.0.0.1', port: int = 9999):
```

**Unity:**
GeometrySyncManager component:
```
Port: 9999
```

### Multiple Objects

Currently streams last-modified mesh object. Multi-object support coming in Phase 4.

### Performance Tuning

**For high-poly meshes (100k+ vertices):**
- Blender Target FPS: 15-30
- Unity Max Updates: 30
- Consider LOD in Geometry Nodes

**For low-latency (VR/AR):**
- Blender Target FPS: 60
- Unity Max Updates: 120
- Keep mesh <20k vertices

## Support

- Check [README.md](README.md) for detailed documentation
- Review example scenes in test_scenes folder
- Check Unity Console for detailed error messages
- Check Blender System Console (Window → Toggle System Console)

---

**Setup Complete!** 🎉

You now have real-time Blender → Unity geometry streaming working!
