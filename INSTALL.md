# GeometrySync - Installation Instructions

## Quick Install (Recommended)

### Blender Addon

**Method 1: ZIP Installation (Easiest)**

1. **Install NumPy** for Blender's Python:
   ```bash
   # Windows
   "C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin\python.exe" -m pip install numpy

   # macOS
   /Applications/Blender.app/Contents/Resources/4.5/python/bin/python3.11 -m pip install numpy

   # Linux
   /usr/share/blender/4.5/python/bin/python3.11 -m pip install numpy
   ```

2. **Install the addon**:
   - Open Blender
   - Edit → Preferences → Add-ons
   - Click **"Install..."** button
   - Navigate to `GeometrySync/Blender/addons/`
   - Select **`geometry_sync.zip`** file
   - Click "Install Add-on"
   - ✅ Check the checkbox to enable "GeometrySync"

3. **Verify installation**:
   - Press `N` in 3D Viewport
   - Look for **"GeometrySync"** tab
   - You should see the control panel ✅

**Method 2: Manual Installation**

1. Locate Blender's addons directory:
   - Windows: `%APPDATA%\Blender Foundation\Blender\4.5\scripts\addons\`
   - macOS: `~/Library/Application Support/Blender/4.5/scripts/addons/`
   - Linux: `~/.config/blender/4.5/scripts/addons/`

2. Copy the **entire `geometry_sync` folder** to that directory

3. Restart Blender

4. Edit → Preferences → Add-ons → Enable "GeometrySync"

---

### Unity Package

**Simple Copy Method**

1. Copy the folder:
   ```
   GeometrySync/Unity/Assets/GeometrySync/
   ```

2. Paste into your Unity project's `Assets/` folder:
   ```
   YourProject/Assets/GeometrySync/
   ```

3. Wait for Unity to compile scripts ✅

**Package Manager Method (Alternative)**

If you prefer UPM:
1. Copy `Unity/Assets/GeometrySync/` to your project's `Packages/` folder
2. Rename to `com.geometrysync.core`
3. Unity will detect it as a package

---

## Verify Installation

### Blender

1. Open Blender
2. Press `N` in 3D Viewport (sidebar)
3. Click "GeometrySync" tab
4. You should see:
   - Server section with "Start Server" button
   - Streaming section with "Target FPS" slider
   - Info section

**If addon doesn't appear:**
- Check Edit → Preferences → Add-ons
- Search for "GeometrySync"
- Make sure it's enabled (checkbox)
- Check console for errors (Window → Toggle System Console)

### Unity

1. Open your Unity project
2. Check Project window: `Assets/GeometrySync/`
3. You should see:
   - `Runtime/` folder with 4 C# scripts
   - `Shaders/` folder with shader file

4. Create test GameObject:
   - Hierarchy → Create Empty
   - Inspector → Add Component
   - Search "GeometrySyncManager"
   - Should appear in list ✅

**If scripts don't compile:**
- Check Unity version is 6000+
- Check Console for errors
- Ensure URP is installed (Window → Package Manager)

---

## Test Connection

### 1. Start Blender Server

1. In Blender GeometrySync panel:
   - Click **"Start Server"**
   - Status should show: "Waiting for Unity client..."

2. Check Blender console:
   - Window → Toggle System Console (Windows)
   - Should show: `GeometrySync server started on 127.0.0.1:8080`

### 2. Connect from Unity

1. Create GameObject with GeometrySyncManager component
2. Assign any URP material to MeshRenderer
3. Enter Play mode

4. Check on-screen display:
   - Status: **Connected** (green) ✅
   - Server: 127.0.0.1:8080

5. Check Blender console:
   - Should show: `Unity client connected from ('127.0.0.1', XXXXX)`

### 3. Test Streaming

1. In Blender:
   - Select default Cube
   - Add Geometry Nodes modifier
   - Add Subdivide Surface node (level 3)

2. In Unity:
   - Mesh should appear and update in real-time ✅

---

## Troubleshooting Installation

### "ModuleNotFoundError: No module named 'numpy'"

**Problem:** NumPy not installed for Blender's Python

**Solution:**
```bash
# Find Blender's Python executable
# Open Blender → Scripting → Python Console → type:
import sys
print(sys.executable)

# Then install NumPy using that path:
/path/to/blender/python -m pip install numpy
```

**Verify:**
```python
# In Blender Python Console:
import numpy
print(numpy.__version__)  # Should print version
```

### "Add-on not found" in Blender

**Problem:** Wrong installation path

**Solution:** Use Method 2 (manual installation) and ensure folder name is exactly `geometry_sync`

### Unity Scripts Won't Compile

**Problem:** Missing dependencies or wrong Unity version

**Solutions:**
1. Check Unity version is 6000+
2. Install URP via Package Manager
3. Check Console for specific errors
4. Try reimporting: Right-click `GeometrySync` folder → Reimport

### "Connection refused" when testing

**Problem:** Firewall or server not started

**Solutions:**
1. Ensure "Start Server" clicked in Blender
2. Check Windows Firewall allows Python
3. Try disabling firewall temporarily to test
4. Check port 8080 not in use: `netstat -ano | findstr :8080`

---

## Platform-Specific Notes

### Windows

- NumPy install requires Visual C++ redistributables
- If pip fails, try: `python.exe -m ensurepip`
- Firewall may block first connection (allow Python)

### macOS

- May need to give Blender network permissions
- Python path: `/Applications/Blender.app/.../python/bin/python3.11`
- Use `sudo` if permission denied

### Linux

- Install via package manager if pip fails: `pip3 install numpy`
- Check SELinux if connection fails
- Python path typically: `/usr/share/blender/.../python/bin/python3.11`

---

## Next Steps After Installation

1. ✅ Follow [SETUP_GUIDE.md](SETUP_GUIDE.md) for creating test scenes
2. ✅ Try the quick start in [README.md](README.md)
3. ✅ Check [QUICK_REFERENCE.md](QUICK_REFERENCE.md) for API usage

---

## Files Included

```
GeometrySync/
├── Blender/addons/
│   ├── geometry_sync/          # Addon source files
│   └── geometry_sync.zip       # ← Install this file
│
└── Unity/Assets/
    └── GeometrySync/           # ← Copy this folder
        ├── Runtime/
        └── Shaders/
```

**For Blender:** Use `geometry_sync.zip`
**For Unity:** Copy the `GeometrySync` folder

---

**Installation Complete!** 🎉

Continue with [SETUP_GUIDE.md](SETUP_GUIDE.md) to create your first streaming scene.
