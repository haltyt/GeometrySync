# GeometrySync - 5-Minute Quick Start

Get streaming in 5 minutes! ⚡

## ✅ Checklist

### Prerequisites (2 minutes)

- [ ] Blender 4.5+ installed
- [ ] Unity 6000+ installed with URP
- [ ] NumPy installed for Blender Python

**Install NumPy (if needed):**
```bash
# Windows
"C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin\python.exe" -m pip install numpy

# macOS
/Applications/Blender.app/Contents/Resources/4.5/python/bin/python3.11 -m pip install numpy

# Linux
/usr/share/blender/4.5/python/bin/python3.11 -m pip install numpy
```

---

### Blender Setup (2 minutes)

**Step 1: Install Addon**
- [ ] Open Blender
- [ ] Edit → Preferences → Add-ons
- [ ] Click "Install..."
- [ ] Select `Blender/addons/geometry_sync.zip`
- [ ] Enable "GeometrySync" (check the box)

**Step 2: Create Test Scene**
- [ ] Open new Blender scene (default cube is fine)
- [ ] Select Cube
- [ ] Add Modifier → Geometry Nodes
- [ ] Click "New" in Geometry Nodes modifier
- [ ] Add node: Mesh → Subdivide Surface
- [ ] Connect: Group Input → Subdivide Surface → Group Output
- [ ] Set Subdivide level to **3**

**Step 3: Start Server**
- [ ] Press `N` to open 3D Viewport sidebar
- [ ] Click "GeometrySync" tab
- [ ] Click **"Start Server"** button
- [ ] Status shows: "Waiting for Unity client..."

✅ **Blender ready!**

---

### Unity Setup (1 minute)

**Step 1: Import Package**
- [ ] Copy `Unity/Assets/GeometrySync/` folder
- [ ] Paste into your project's `Assets/` folder
- [ ] Wait for scripts to compile

**Step 2: Create Receiver GameObject**
- [ ] Hierarchy → Create Empty GameObject
- [ ] Name it: "BlenderMesh"
- [ ] Inspector → Add Component → "GeometrySyncManager"

**Step 3: Configure**
- [ ] In GeometrySyncManager:
  - Host: `127.0.0.1` ✅ (default)
  - Port: `8080` ✅ (default)
  - Auto Connect: ✅ (checked)
  - Show Debug Info: ✅ (checked)

**Step 4: Add Material**
- [ ] Project → Create → Material (name: "GeometrySyncMat")
- [ ] Set Shader: "Universal Render Pipeline/Lit" (or use GeometrySync/Basic URP)
- [ ] Drag material to BlenderMesh's Mesh Renderer

✅ **Unity ready!**

---

### Test Streaming (30 seconds)

**Step 1: Connect**
- [ ] Unity: Click **Play** button (▶)
- [ ] Check Blender console: "Unity client connected" ✅
- [ ] Check Unity on-screen: "Status: Connected" (green) ✅

**Step 2: Stream**
- [ ] In Blender, modify Geometry Nodes:
  - Change Subdivide Surface level (2 → 4 → 3)
- [ ] Watch Unity Scene view update in real-time! 🎉

**Step 3: Verify**
- [ ] Unity shows subdivided cube mesh
- [ ] Mesh updates when you change Blender nodes
- [ ] On-screen stats show vertex/triangle count

✅ **Streaming works!**

---

## 🎨 Try These Next

### Animated Noise (1 minute)

**In Blender Geometry Nodes:**
1. Add node: Utilities → Noise Texture
2. Add node: Geometry → Set Position
3. Connect: Noise Texture → Set Position (Offset input)
4. Connect: Subdivide Surface → Set Position → Group Output
5. Set Noise Scale: **2.0**
6. Animate the Noise "W" value:
   - Frame 1: W = 0.0 (set keyframe: `I` key)
   - Frame 100: W = 10.0 (set keyframe: `I` key)
7. Play animation (Spacebar)

**Watch Unity mesh deform in real-time!** 🌊

### Color by Height (Phase 2 feature)

Coming soon - custom attributes support!

---

## 📊 Performance Check

**Check on-screen stats in Unity:**

| Metric | Good | Great | Excellent |
|--------|------|-------|-----------|
| FPS | 30+ | 60+ | 120+ |
| Latency | <100ms | <50ms | <30ms |
| Queue | 0-2 | 0-1 | 0 |

**If FPS is low:**
- Reduce Subdivide Surface level in Blender
- Lower "Target FPS" in Blender panel (try 30)
- Lower "Max Updates Per Second" in Unity (try 30)

---

## 🐛 Quick Troubleshooting

### "Connection refused"
- ✅ Click "Start Server" in Blender
- ✅ Check firewall allows localhost
- ✅ Verify port 8080 not in use

### "No mesh appears"
- ✅ Ensure material assigned to MeshRenderer
- ✅ Check GameObject is in camera view (press `F` in Scene view)
- ✅ Verify Blender console shows "Streamed Cube: N vertices"

### "ModuleNotFoundError: numpy"
- ✅ Install NumPy using Blender's Python (see Prerequisites)
- ✅ Restart Blender after installation

---

## 📚 Learn More

Now that it works, explore:

1. **[README.md](README.md)** - Full feature overview
2. **[QUICK_REFERENCE.md](QUICK_REFERENCE.md)** - API documentation
3. **[SETUP_GUIDE.md](SETUP_GUIDE.md)** - Advanced setup options

---

## 💡 Tips & Tricks

**Blender:**
- Use `Shift+D` to duplicate nodes quickly
- Try different Geometry Nodes: Distribute Points, Instance on Points
- Animate any node parameter for real-time preview

**Unity:**
- Enable "Log Mesh Updates" to see every update in Console
- Try different URP shaders for different looks
- Use Unity's Post-Processing for beautiful renders

**Performance:**
- Keep vertex count <50k for smooth 60 FPS
- Use "Target FPS" slider in Blender to limit bandwidth
- Lower subdivisions for faster iteration

---

**🎉 Congratulations!**

You now have real-time Blender → Unity geometry streaming!

Share your creations and report any issues. Happy creating! 🚀

---

**Quick Links:**
- 📖 [Full Documentation](README.md)
- 🔧 [Installation Guide](INSTALL.md)
- 📋 [Setup Guide](SETUP_GUIDE.md)
- 🎯 [API Reference](QUICK_REFERENCE.md)
