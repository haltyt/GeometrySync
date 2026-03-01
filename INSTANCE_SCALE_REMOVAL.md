# Instance Scale Removal - Complete

**Date**: 2025-12-04
**Status**: ✅ Complete

## Overview

Removed all Instance Scale parameters from both Blender and Unity sides of GeometrySync, leaving only Base Mesh Scale as the primary control for mesh size.

## User Request

"unity, blenderのインスタンススケールの変更は削除" (Remove instance scale changes from both Unity and Blender)

"インスタンスではなく、木のサイズをかえたい" (I want to change the tree size, not the instance scale)

## Problem

Previously, the system had two scale parameters:
- **Base Mesh Scale**: Scaled vertex positions in Blender
- **Instance Scale**: Multiplied transform matrices in both Blender and Unity

This created confusion because:
1. Base Mesh Scale appeared to affect instance scale instead of mesh size
2. Geometry Nodes' transform scale (~7.693) was interfering with the scaling logic
3. Final visual size = (vertices × base_mesh_scale) × transform_scale, making it difficult to control

## Solution

Removed all Instance Scale functionality, keeping only Base Mesh Scale:
- Transform matrices now pass through unchanged (preserving Geometry Nodes' 7.693 scale)
- Base Mesh Scale directly controls vertex positions
- No scale compensation or multiplication logic

## Files Modified

### Blender Side

#### 1. `E:\GeometrySync\Blender\addons\geometry_sync\ui.py`

**Removed**:
- Instance Scale UI property (line 44)
- Instance Scale property registration (lines 119-127)
- Instance Scale from unregister function (line 135)

**Result**: UI now only shows:
- Target FPS
- Base Mesh Scale

#### 2. `E:\GeometrySync\Blender\addons\geometry_sync\handlers.py`

**Changed** (lines 173-177):
```python
# Get scale parameter from UI
base_mesh_scale = context.scene.geometrysync_base_mesh_scale

extract_start = time.time()
instance_result = extractor.extract_instance_transforms(obj, depsgraph, base_mesh_scale)
```

**Result**: No longer passes instance_scale parameter to extraction.

#### 3. `E:\GeometrySync\Blender\addons\geometry_sync\extractor.py`

**Changed Function Signature** (line 286):
```python
def extract_instance_transforms(obj: bpy.types.Object,
                                depsgraph: bpy.types.Depsgraph,
                                base_mesh_scale_multiplier: float = 1.0) -> Optional[Tuple[str, np.ndarray, Tuple]]:
```

**Removed**: `instance_scale_multiplier` parameter

**Changed Transform Logic** (lines 349-350):
```python
# Reconstruct matrix with original scale (no modifications)
local_matrix = Matrix.LocRotScale(translation, rotation, scale)
```

**Result**: Transform matrices preserve original Geometry Nodes scale.

**Updated Debug Logging** (line 364):
```python
print(f"[Extractor] Transform[0] scale (unchanged): ({scale_x:.3f}, {scale_y:.3f}, {scale_z:.3f})")
```

### Unity Side

#### 4. `E:\GeometrySync\Unity\GeometrySync\Assets\GeometrySync\Runtime\GPUInstanceRenderer.cs`

**Removed** (lines 33-35):
```csharp
[Header("Transform")]
[Tooltip("Global scale multiplier for all instances")]
public float instanceScale = 1.0f;  // REMOVED
```

**Result**: Instance Scale field no longer appears in Unity Inspector.

**Changed Transform Processing** (lines 141-159):
```csharp
for (int i = 0; i < transforms.Length; i++)
{
    // Decompose the original transform into TRS (Translation, Rotation, Scale)
    Vector3 position = transforms[i].GetPosition();
    Quaternion rotation = transforms[i].rotation;
    Vector3 scale = transforms[i].lossyScale;

    // DEBUG: Log first transform's scale (no longer modified by instanceScale)
    if (i == 0 && logUpdates)
    {
        Debug.Log($"[GPUInstanceRenderer] Transform scale (unchanged): {scale}");
    }

    // Apply global offset to the position
    position += instanceOffset;

    // Reconstruct the matrix with unchanged scale and adjusted position
    adjustedTransforms[i] = Matrix4x4.TRS(position, rotation, scale);
}
```

**Result**: Transform scales pass through unchanged, no multiplication by instanceScale.

## How Base Mesh Scale Now Works

1. **Blender**:
   - User adjusts Base Mesh Scale slider (default: 1.0, range: 0.001 - 100.0)
   - Vertex positions are multiplied by this scale in `extract_mesh_data_fast()`
   - Transform matrices pass through unchanged with original ~7.693 scale

2. **Unity**:
   - Receives scaled vertex data for base mesh
   - Receives unchanged transform matrices
   - Final visual size = (vertices × base_mesh_scale) × 7.693

3. **Effect**:
   - Changing Base Mesh Scale from 1.0 → 0.5 makes trees appear ~50% smaller
   - Changing Base Mesh Scale from 1.0 → 2.0 makes trees appear ~200% larger
   - Instance placement and spacing remain unchanged

## Testing Procedure

1. **Reload Blender Addon**:
   - Open Blender Script Editor
   - Run the addon reload script:
   ```python
   import bpy
   import importlib
   import sys

   modules_to_remove = [key for key in sys.modules.keys() if key.startswith('geometry_sync')]
   for module in modules_to_remove:
       del sys.modules[module]

   bpy.ops.preferences.addon_disable(module="geometry_sync")
   bpy.ops.preferences.addon_enable(module="geometry_sync")
   ```

2. **Restart Unity**:
   - Close Unity completely
   - Reopen the GeometrySync project to ensure script changes are compiled

3. **Test Base Mesh Scale**:
   - Start GeometrySync server in Blender
   - Connect from Unity
   - Adjust Base Mesh Scale slider in Blender (e.g., 0.5, 2.0)
   - Verify tree sizes change in Unity
   - Verify instance positions/spacing remain unchanged

4. **Verify Instance Scale Removed**:
   - Check Unity Inspector: GPUInstanceRenderer should NOT have Instance Scale field
   - Check Blender UI: GeometrySync panel should NOT have Instance Scale property

## Expected Behavior

✅ **Base Mesh Scale controls tree size directly**
✅ **Instance positions/spacing remain unchanged**
✅ **No interference from removed Instance Scale parameters**
✅ **Transform scale (~7.693) preserved from Geometry Nodes**

## Notes

- The Geometry Nodes transform scale of ~7.693 is now intentionally preserved
- This scale is baked into the instance transforms and multiplies with the base mesh vertices
- To achieve 1:1 scale matching between Blender viewport and Unity, set Base Mesh Scale to ~0.13 (1/7.693)
- For most use cases, the default Base Mesh Scale of 1.0 should work fine

## Related Files

- `PHASE2_COMPLETE.md` - GPU Instancing implementation
- `PHASE3A_COMPLETE.md` - Indirect rendering implementation
- `VFX_GRAPH_COMPLETE.md` - VFX Graph integration
