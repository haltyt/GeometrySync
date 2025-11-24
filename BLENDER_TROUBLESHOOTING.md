# Blender GeometrySync Troubleshooting

## Connection Successful but No Mesh Displayed

If Unity shows "Connected to Blender server" but nothing appears in the scene, follow these steps:

---

## ✅ Step 1: Verify Blender Setup

### Check Server Status
In Blender's 3D Viewport sidebar (press `N`):
1. Find the **GeometrySync** tab
2. Verify **"Stop Server"** button is showing (green)
3. Status should say **"Connected"** with a linked icon

### Check Streaming Status
- **Active: 30 FPS** should be displayed (or your target FPS)
- If it says **"Inactive"**, the handlers aren't registered

---

## ✅ Step 2: Create or Select a Mesh Object

The addon streams **any mesh object** that gets updated. You need:

### Option A: Use an Existing Mesh
1. Select any mesh object (cube, sphere, etc.)
2. **Modify it** to trigger an update:
   - Move it (`G` key)
   - Rotate it (`R` key)
   - Edit mode: move vertices
   - Add modifier

### Option B: Use Geometry Nodes (Recommended)
1. Select a mesh object
2. Add **Geometry Nodes** modifier
3. In Geometry Nodes editor, create a simple node tree:
   ```
   Group Input → Transform → Group Output
   ```
4. **Adjust any parameter** (e.g., translation, scale)
5. Each parameter change triggers an update

### Option C: Create a Test Cube
```python
# In Blender's Python Console (Shift+F4)
import bpy
bpy.ops.mesh.primitive_cube_add(location=(0, 0, 0))
cube = bpy.context.active_object
# Now move the cube with G key
```

---

## ✅ Step 3: Trigger Geometry Updates

**IMPORTANT:** The addon only sends data when geometry **changes**. Try these:

### Method 1: Edit Mode Changes
1. Select mesh object
2. Press `Tab` to enter Edit mode
3. Select vertices (`1` key)
4. Move them (`G` key)
5. Exit Edit mode (`Tab`)
6. **Result:** Mesh update sent to Unity

### Method 2: Modifier Changes
1. Add **Subdivision Surface** modifier
2. Change subdivision level
3. **Result:** Mesh update sent

### Method 3: Geometry Nodes
1. With Geometry Nodes modifier active
2. Change **any node parameter**
3. **Result:** Real-time updates to Unity

---

## ✅ Step 4: Check Blender Console

Open Blender's **System Console** (Windows → Toggle System Console):

### Expected Output When Working:
```
[GeometrySync] Depsgraph update: Cube geometry changed
[GeometrySync] Processing 1 dirty objects: {'Cube'}
Streamed Cube: 8 vertices, 12 triangles
```

### If You See This:
```
[GeometrySync] No client connected, skipping
```
**Solution:** Unity isn't connected. Restart Unity Play mode.

### If You See Nothing:
- Handlers aren't registered
- No geometry changes are happening
- Try **stopping and starting** the server in Blender

---

## ✅ Step 5: Restart Sequence

If nothing works, try this complete restart:

### In Blender:
1. Click **"Stop Server"**
2. Wait 2 seconds
3. Click **"Start Server"**
4. Wait for "Waiting for Unity..." message

### In Unity:
1. **Stop Play mode**
2. Wait 2 seconds
3. **Start Play mode**
4. Console should show: "Connected to Blender server at 127.0.0.1:8080"

### In Blender:
1. Status should change to **"Connected"**
2. Select a mesh object
3. **Move it** (`G` key, then move mouse, then click)
4. **Watch Unity Scene view** - mesh should appear!

---

## 🔍 Debug Checklist

Use this checklist to diagnose issues:

### Blender Side
- [ ] GeometrySync addon is installed and enabled
- [ ] Server is started (green "Stop Server" button)
- [ ] Status shows "Connected" (not "Waiting for Unity...")
- [ ] Streaming shows "Active: 30 FPS" (not "Inactive")
- [ ] You have a mesh object in the scene
- [ ] You're actively modifying the mesh
- [ ] Blender console shows `[GeometrySync]` messages

### Unity Side
- [ ] Play mode is active
- [ ] Console shows "Connected to Blender server"
- [ ] BlenderMesh GameObject exists in Hierarchy
- [ ] GeometrySyncManager component is attached
- [ ] Auto Connect is checked
- [ ] Host is "127.0.0.1"
- [ ] Port is "8080"
- [ ] Material is assigned (GeometrySyncMat)

### Expected Debug Logs

**When you modify a mesh in Blender, Unity Console should show:**
```
[MeshStreamClient] Received mesh data: XXXX bytes
[MeshStreamClient] Deserialized: 8 vertices, 36 indices
[GeometrySyncManager] Got mesh from queue: 8 vertices
```

**If you don't see these logs:**
- Blender isn't sending data
- Check Blender console for errors
- Make sure you're actually modifying geometry

---

## 💡 Quick Test Procedure

**5-Minute Test:**

1. **Blender:** Start Server → Status: Connected
2. **Unity:** Start Play mode → Console: "Connected to Blender server"
3. **Blender:** Select default Cube
4. **Blender:** Press `G` key (grab/move)
5. **Blender:** Move mouse slightly
6. **Blender:** Left-click to confirm
7. **Unity:** Check Scene view → Cube should appear!
8. **Blender:** Press `G` again and move
9. **Unity:** Watch cube update in real-time!

---

## 🐛 Common Issues

### Issue: "Connected" but No Updates

**Cause:** Not modifying geometry, just selecting objects

**Solution:** You must **change** the mesh:
- Move vertices in Edit mode
- Change modifier settings
- Adjust Geometry Nodes parameters

### Issue: Blender Console Shows No Messages

**Cause:** Handlers not registered or scheduler disabled

**Solution:**
```python
# In Blender Python Console
from geometry_sync import handlers
scheduler = handlers.get_scheduler()
print(f"Enabled: {scheduler.enabled}")  # Should be True
```

If False:
1. Stop server
2. Start server again

### Issue: Unity Console Shows Connection Errors

**Cause:** Firewall or port conflict

**Solution:**
1. Check Windows Firewall
2. Try different port (edit in both Blender and Unity)
3. Restart both applications

---

## 📝 Example Workflow

### Real-time Geometry Nodes Streaming

1. **Blender:**
   - Create cube: `Shift+A` → Mesh → Cube
   - Add Geometry Nodes modifier
   - Create node: Transform Geometry
   - Connect: Input → Transform → Output

2. **Unity:**
   - Start Play mode
   - Select BlenderMesh in Hierarchy
   - Watch Scene view

3. **Blender:**
   - In Geometry Nodes, adjust Transform > Translation > X
   - **Watch Unity update in real-time!**
   - Adjust other parameters
   - See instant updates in Unity

---

## 🎯 Expected Behavior

When working correctly:

- **Blender:** Any geometry change triggers update
- **Blender Console:** Shows "Streamed [object]: X vertices, Y triangles"
- **Unity Console:** Shows received data logs
- **Unity Scene:** Mesh updates in real-time
- **Performance:** 30-60 FPS for meshes under 10k vertices

---

## 📞 Still Not Working?

Check these files for additional info:
- [QUICKSTART.md](QUICKSTART.md) - Quick setup guide
- [SETUP_GUIDE.md](SETUP_GUIDE.md) - Detailed setup
- [README.md](README.md) - Project overview

**Last Updated:** 2025-11-24
