# GeometrySync - Unity Project

This is a ready-to-use Unity 6000 project with GeometrySync pre-configured.

## Opening the Project

### Option 1: Open Ready-Made Project (Easiest)

1. **Open Unity Hub**
2. Click **"Add"** (or "Open")
3. Navigate to: `E:\GeometrySync\Unity\GeometrySync\`
4. Click **"Select Folder"**
5. Unity will open the project

### Option 2: Copy to Existing Project

If you want to add GeometrySync to your own Unity project:

1. Copy the folder:
   ```
   E:\GeometrySync\Unity\GeometrySync\Assets\GeometrySync\
   ```

2. Paste into your project's `Assets/` folder:
   ```
   YourProject/Assets/GeometrySync/
   ```

3. Unity will automatically import the scripts

## Project Structure

```
GeometrySync/               # Unity project root
├── Assets/
│   ├── GeometrySync/      # Main package
│   │   ├── Runtime/       # C# scripts
│   │   └── Shaders/       # Shader files
│   └── Scenes/
│       └── GeometrySyncDemo.unity  # Demo scene
├── Packages/
│   └── manifest.json      # URP dependency
└── ProjectSettings/
    └── ...                # Unity settings
```

## Demo Scene

The included `GeometrySyncDemo.unity` scene contains:

- **Main Camera** - Positioned to view the mesh
- **Directional Light** - For proper lighting
- **BlenderMesh** GameObject with:
  - `GeometrySyncManager` component (configured)
  - `MeshFilter` and `MeshRenderer`
  - Default material assigned

**To test:**
1. Open `Assets/Scenes/GeometrySyncDemo.unity`
2. Start Blender server (see main README.md)
3. Click Play in Unity
4. Edit Geometry Nodes in Blender → see updates in Unity!

## Requirements

- **Unity**: 6000.0.28f1 or later
- **URP**: 17.0.3 (automatically installed via Package Manager)
- **Platform**: Windows, macOS, or Linux

## First Time Setup

If this is your first time opening the project:

1. Unity may take a few minutes to import packages
2. Wait for "Importing..." to finish
3. Check Console for any errors (there should be none)
4. Open the demo scene: `Assets/Scenes/GeometrySyncDemo.unity`

## Troubleshooting

### "Scripts won't compile"
- Check Unity version is 6000+
- Check Console for specific errors
- Try: Assets → Reimport All

### "URP not found"
- Window → Package Manager
- Search "Universal RP"
- Install version 17.0+

### "Scene is empty"
- Make sure you opened `Assets/Scenes/GeometrySyncDemo.unity`
- If BlenderMesh is invisible, check material is assigned

## Next Steps

See the main [README.md](../README.md) for:
- Blender addon installation
- Connection setup
- Usage instructions

---

**Quick Start**: [QUICKSTART.md](../QUICKSTART.md)
**Documentation**: [INDEX.md](../INDEX.md)
