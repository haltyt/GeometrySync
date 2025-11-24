"""
Quick reload script for GeometrySync addon

Run this in Blender's Scripting workspace to reload the addon
"""

import bpy
import addon_utils
import importlib
import sys

def reload_geometrysync():
    """Reload GeometrySync addon completely"""

    print("\n" + "="*60)
    print("Reloading GeometrySync Addon")
    print("="*60 + "\n")

    # Step 1: Disable addon
    print("1. Disabling addon...")
    try:
        addon_utils.disable("geometry_sync", default_set=True)
        print("   ✓ Addon disabled")
    except Exception as e:
        print(f"   Warning: {e}")

    # Step 2: Remove from sys.modules
    print("\n2. Clearing Python modules cache...")
    modules_to_remove = [key for key in sys.modules.keys() if key.startswith('geometry_sync')]
    for module in modules_to_remove:
        del sys.modules[module]
        print(f"   ✓ Removed {module}")

    # Step 3: Re-enable addon
    print("\n3. Re-enabling addon...")
    try:
        addon_utils.enable("geometry_sync", default_set=True)
        print("   ✓ Addon enabled")
    except Exception as e:
        print(f"   ERROR: {e}")
        return False

    # Step 4: Verify
    print("\n4. Verifying...")
    from geometry_sync import extractor

    # Check if the fix is loaded
    import inspect
    source = inspect.getsource(extractor.extract_mesh_data_fast)
    if "hasattr(mesh_eval, 'calc_normals_split')" in source:
        print("   ✓ Blender 4.5 compatibility fix loaded!")
    else:
        print("   ✗ WARNING: Old code still loaded")
        return False

    print("\n" + "="*60)
    print("SUCCESS! Addon reloaded with Blender 4.5 fix")
    print("="*60 + "\n")

    print("Now run test_streaming.py again!")

    return True

if __name__ == "__main__":
    reload_geometrysync()
