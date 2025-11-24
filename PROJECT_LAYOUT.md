# GeometrySync - Complete Project Layout

Visual guide to the project structure after reorganization.

## 📁 Complete Directory Structure

```
E:\GeometrySync/                              # Root project directory
│
├── 📄 README.md                              # Main documentation (start here)
├── 📄 QUICKSTART.md                          # 5-minute setup guide
├── 📄 INSTALL.md                             # Installation instructions
├── 📄 SETUP_GUIDE.md                         # Detailed setup walkthrough
├── 📄 INDEX.md                               # Documentation navigation
├── 📄 QUICK_REFERENCE.md                     # API reference
├── 📄 PROJECT_STRUCTURE.md                   # Architecture deep-dive
├── 📄 IMPLEMENTATION_SUMMARY.md              # Phase status & benchmarks
├── 📄 PROJECT_LAYOUT.md                      # This file
├── 📄 LICENSE                                # MIT License
│
├── 📂 Blender/                               # Blender addon
│   └── 📂 addons/
│       ├── 📦 geometry_sync.zip              # ← Install this in Blender
│       └── 📂 geometry_sync/                 # Source files
│           ├── __init__.py                   # Addon registration
│           ├── server.py                     # TCP server
│           ├── extractor.py                  # Mesh extraction
│           ├── serializer.py                 # Binary serialization
│           ├── handlers.py                   # Depsgraph handlers
│           └── ui.py                         # Blender UI panel
│
└── 📂 Unity/                                 # Unity project
    ├── 📄 README.md                          # Unity-specific instructions
    └── 📂 GeometrySync/                      # ← Open this in Unity Hub
        ├── 📂 Assets/
        │   ├── 📂 GeometrySync/              # Main package
        │   │   ├── 📂 Runtime/               # C# scripts
        │   │   │   ├── MeshStreamClient.cs
        │   │   │   ├── MeshDeserializer.cs
        │   │   │   ├── MeshReconstructor.cs
        │   │   │   └── GeometrySyncManager.cs
        │   │   └── 📂 Shaders/
        │   │       └── GeometrySyncBasic.shader
        │   └── 📂 Scenes/
        │       └── 🎬 GeometrySyncDemo.unity # ← Demo scene
        │
        ├── 📂 Packages/
        │   ├── manifest.json                 # URP dependency
        │   └── packages-lock.json
        │
        └── 📂 ProjectSettings/
            ├── ProjectVersion.txt
            └── ProjectSettings.asset
```

## 🎯 Quick Access Paths

### For Blender Installation

```
Install this file:
E:\GeometrySync\Blender\addons\geometry_sync.zip

Or copy this folder manually:
E:\GeometrySync\Blender\addons\geometry_sync\
  → Paste to Blender addons directory
```

### For Unity

**Option 1: Open Ready-Made Project**
```
Open in Unity Hub:
E:\GeometrySync\Unity\GeometrySync\
```

**Option 2: Copy to Existing Project**
```
Copy this folder:
E:\GeometrySync\Unity\GeometrySync\Assets\GeometrySync\

Paste to:
YourProject\Assets\GeometrySync\
```

## 📖 Documentation Files

| File | Purpose | When to Read |
|------|---------|--------------|
| [README.md](README.md) | Overview & quick intro | First read |
| [QUICKSTART.md](QUICKSTART.md) | 5-min setup checklist | Getting started |
| [INSTALL.md](INSTALL.md) | Installation guide | Setup issues |
| [SETUP_GUIDE.md](SETUP_GUIDE.md) | Complete walkthrough | Detailed setup |
| [INDEX.md](INDEX.md) | Doc navigation | Finding specific info |
| [QUICK_REFERENCE.md](QUICK_REFERENCE.md) | API cheat sheet | Development |
| [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) | Architecture | Deep understanding |
| [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) | Technical details | Phase status |
| [PROJECT_LAYOUT.md](PROJECT_LAYOUT.md) | This guide | Directory reference |

## 🚀 Usage Workflow

### 1️⃣ Install Blender Addon

```bash
# Path to install
Blender → Edit → Preferences → Add-ons → Install
Select: E:\GeometrySync\Blender\addons\geometry_sync.zip
```

### 2️⃣ Open Unity Project

```bash
# Open in Unity Hub
Add Project: E:\GeometrySync\Unity\GeometrySync\
```

### 3️⃣ Test Streaming

```
Blender:
  1. Press N → GeometrySync tab
  2. Click "Start Server"

Unity:
  1. Open Assets/Scenes/GeometrySyncDemo.unity
  2. Click Play
  3. Connected! ✅

Blender:
  1. Edit Geometry Nodes
  2. Watch Unity update in real-time! 🎉
```

## 📦 What to Distribute

### For End Users (Ready-to-Use)

**Blender:**
- Distribute: `Blender/addons/geometry_sync.zip`
- Users install via Blender Add-ons manager

**Unity:**
- Distribute: `Unity/GeometrySync/` (entire project)
- Users open in Unity Hub

### For Developers (Source Code)

**Blender:**
- Source: `Blender/addons/geometry_sync/` folder
- Users can modify and contribute

**Unity:**
- Source: `Unity/GeometrySync/Assets/GeometrySync/` folder
- Users can integrate into their projects

## 🔧 Development Structure

### Blender Addon Development

```
Edit files in:
E:\GeometrySync\Blender\addons\geometry_sync\

Test in Blender:
  1. Install from source folder (use manual method)
  2. Edit → Preferences → Add-ons → Reload Scripts (F8)
  3. Or restart Blender

Create distribution:
  1. Zip the geometry_sync folder
  2. Or use provided geometry_sync.zip
```

### Unity Package Development

```
Edit files in:
E:\GeometrySync\Unity\GeometrySync\Assets\GeometrySync\Runtime\

Test:
  1. Open project: E:\GeometrySync\Unity\GeometrySync\
  2. Edit scripts in your IDE
  3. Unity auto-recompiles

Create package:
  1. Select Assets/GeometrySync
  2. Assets → Export Package
  3. Or copy folder to other projects
```

## 📊 File Count Summary

| Component | Files | Purpose |
|-----------|-------|---------|
| Documentation | 10 | Guides & references |
| Blender Addon | 6 + 1 zip | Python scripts |
| Unity Scripts | 4 | C# runtime |
| Unity Shaders | 1 | URP shader |
| Unity Scenes | 1 | Demo scene |
| Unity Config | 4 | Project settings |
| **Total** | **27 files** | **Complete system** |

## 🎯 Key Folders Explained

### `Blender/addons/geometry_sync/`
**Purpose:** Blender addon source code
**Contains:** Python scripts for server, extraction, serialization
**Install:** Via `geometry_sync.zip` or manual copy

### `Unity/GeometrySync/`
**Purpose:** Complete Unity project
**Contains:** Full Unity project structure
**Open:** In Unity Hub as a project

### `Unity/GeometrySync/Assets/GeometrySync/`
**Purpose:** GeometrySync package for Unity
**Contains:** Portable package for any Unity project
**Use:** Copy to existing projects

### `Unity/GeometrySync/Assets/Scenes/`
**Purpose:** Demo and test scenes
**Contains:** Pre-configured scene with GeometrySyncManager
**Use:** Open to start testing immediately

## 🔄 Migration from Old Structure

If you have the old structure, here's what changed:

**Old:**
```
Unity/
├── Assets/GeometrySync/
└── Scenes/
```

**New:**
```
Unity/
└── GeometrySync/          # Now a complete Unity project
    └── Assets/
        ├── GeometrySync/
        └── Scenes/
```

**Impact:** None for end users - just open the new project location.

## 📝 Notes

- **Unity Project:** The `Unity/GeometrySync/` folder is now a complete Unity project that can be opened directly
- **Portable Package:** The `Assets/GeometrySync/` subfolder can still be copied to other projects
- **Demo Scene:** Included `GeometrySyncDemo.unity` is pre-configured and ready to use
- **Documentation:** All docs are at the root for easy access

## 🆘 Need Help?

**Can't find a file?**
→ Use this guide to locate it

**Want to install?**
→ See [INSTALL.md](INSTALL.md)

**Want to understand the code?**
→ See [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)

**Quick reference?**
→ See [QUICK_REFERENCE.md](QUICK_REFERENCE.md)

---

**Last Updated:** 2025-11-24
**Structure Version:** 2.0 (Unity project reorganized)
