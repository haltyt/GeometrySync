# GeometrySync - Documentation Index

Quick navigation to all documentation files.

## 🚀 Getting Started (Read First)

Start here if you're new to GeometrySync:

1. **[QUICKSTART.md](QUICKSTART.md)** ⚡ **← START HERE**
   - 5-minute setup checklist
   - Step-by-step with checkboxes
   - Immediate test streaming
   - Perfect for first-time users

2. **[INSTALL.md](INSTALL.md)** 📦
   - Detailed installation instructions
   - Platform-specific notes (Windows/Mac/Linux)
   - Troubleshooting installation issues
   - Verification steps

3. **[README.md](README.md)** 📖
   - Project overview
   - Features list
   - Architecture diagram
   - Performance metrics
   - General introduction

## 📚 Comprehensive Guides

Deep-dive documentation for understanding the system:

4. **[SETUP_GUIDE.md](SETUP_GUIDE.md)** 🔧
   - Complete setup walkthrough
   - Creating test scenes (Blender & Unity)
   - Connection verification
   - Advanced configuration
   - Troubleshooting guide

5. **[PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)** 🏗️
   - Complete file hierarchy
   - Architecture deep-dive
   - Data flow diagrams
   - Threading model
   - Memory management
   - Extension points for future development

6. **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** ✅
   - Phase completion status
   - Technical highlights
   - Performance benchmarks
   - Code metrics
   - Known limitations
   - Roadmap for Phases 2-4

## 🎯 Reference Materials

Quick lookups and API documentation:

7. **[QUICK_REFERENCE.md](QUICK_REFERENCE.md)** 📋
   - API cheat sheet (Blender & Unity)
   - Binary protocol specification
   - Common Geometry Node setups
   - Performance tuning presets
   - Debugging commands
   - Keyboard shortcuts
   - Error messages & solutions

## 📄 Project Files

8. **[LICENSE](LICENSE)** ⚖️
   - MIT License
   - Usage terms

## 📁 Source Code

### Blender Addon (`Blender/addons/geometry_sync/`)

- **`__init__.py`** - Addon registration, metadata
- **`server.py`** - TCP server, connection management
- **`extractor.py`** - Mesh extraction from Geometry Nodes
- **`serializer.py`** - Binary serialization, coordinate conversion
- **`handlers.py`** - Depsgraph handlers, FPS throttling
- **`ui.py`** - 3D Viewport panel, operators

### Unity Package (`Unity/Assets/GeometrySync/`)

#### Runtime Scripts

- **`MeshStreamClient.cs`** - TCP client, background threading
- **`MeshDeserializer.cs`** - Binary deserialization, validation
- **`MeshReconstructor.cs`** - Modern Mesh API, NativeArray management
- **`GeometrySyncManager.cs`** - Main coordinator, MonoBehaviour component

#### Shaders

- **`GeometrySyncBasic.shader`** - Example URP PBR shader

## 🗺️ Documentation Roadmap

### For New Users

```
1. QUICKSTART.md (5 min)
   ↓
2. Test streaming works ✅
   ↓
3. README.md (overview)
   ↓
4. Experiment with Geometry Nodes
```

### For Developers

```
1. INSTALL.md (setup)
   ↓
2. PROJECT_STRUCTURE.md (architecture)
   ↓
3. QUICK_REFERENCE.md (API)
   ↓
4. Start customizing/extending
```

### For Troubleshooting

```
1. QUICKSTART.md (basic checks)
   ↓
2. SETUP_GUIDE.md (troubleshooting section)
   ↓
3. QUICK_REFERENCE.md (error messages)
   ↓
4. Check Blender/Unity console logs
```

## 📊 Documentation by Topic

### Installation & Setup

- [INSTALL.md](INSTALL.md) - Installation instructions
- [QUICKSTART.md](QUICKSTART.md) - Quick setup checklist
- [SETUP_GUIDE.md](SETUP_GUIDE.md) - Detailed setup walkthrough

### Architecture & Design

- [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) - Complete architecture
- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - Technical summary
- [README.md](README.md) - High-level overview

### Usage & API

- [QUICK_REFERENCE.md](QUICK_REFERENCE.md) - API reference
- [QUICKSTART.md](QUICKSTART.md) - Basic usage examples
- [README.md](README.md) - Usage examples

### Troubleshooting

- [SETUP_GUIDE.md](SETUP_GUIDE.md) - Setup issues
- [QUICK_REFERENCE.md](QUICK_REFERENCE.md) - Error messages
- [INSTALL.md](INSTALL.md) - Installation problems

### Development

- [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) - Extension points
- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - Phase roadmap
- [QUICK_REFERENCE.md](QUICK_REFERENCE.md) - Code examples

## 🎯 Find What You Need

### "I want to get started quickly"
→ [QUICKSTART.md](QUICKSTART.md)

### "I need to install the addon/package"
→ [INSTALL.md](INSTALL.md)

### "How do I connect Blender to Unity?"
→ [SETUP_GUIDE.md](SETUP_GUIDE.md) - "First Connection Test"

### "What's the API for X?"
→ [QUICK_REFERENCE.md](QUICK_REFERENCE.md)

### "How does the system work?"
→ [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) - "Data Flow Diagram"

### "What are the performance limits?"
→ [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - "Performance Benchmarks"

### "I'm getting error X"
→ [QUICK_REFERENCE.md](QUICK_REFERENCE.md) - "Error Messages"

### "How do I customize/extend this?"
→ [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) - "Extension Points"

### "What features are planned?"
→ [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - "Roadmap"

### "How do I create custom shaders?"
→ [QUICK_REFERENCE.md](QUICK_REFERENCE.md) - "Shader Integration"

## 📈 Documentation Stats

| Document | Words | Purpose |
|----------|-------|---------|
| QUICKSTART.md | ~1,000 | Fast setup checklist |
| INSTALL.md | ~1,500 | Installation guide |
| SETUP_GUIDE.md | ~3,500 | Complete setup walkthrough |
| README.md | ~2,000 | Project overview |
| PROJECT_STRUCTURE.md | ~4,000 | Architecture documentation |
| IMPLEMENTATION_SUMMARY.md | ~3,000 | Technical summary |
| QUICK_REFERENCE.md | ~3,500 | API reference |
| **Total** | **~18,500** | **Complete documentation** |

## 🔄 Keep Updated

All documentation reflects **Version 1.0.0 (Phase 1)**.

When new phases are implemented, check:
- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) for status updates
- [README.md](README.md) for new features
- [QUICK_REFERENCE.md](QUICK_REFERENCE.md) for new APIs

## 🆘 Still Need Help?

1. ✅ Check the relevant documentation above
2. ✅ Search for error message in [QUICK_REFERENCE.md](QUICK_REFERENCE.md)
3. ✅ Check Blender/Unity console for detailed errors
4. ✅ Review [SETUP_GUIDE.md](SETUP_GUIDE.md) troubleshooting section
5. ✅ Create GitHub issue with:
   - What you tried
   - Error messages
   - Blender/Unity versions
   - Platform (Windows/Mac/Linux)

---

**Happy Streaming!** 🚀

Navigate back: [README.md](README.md)
