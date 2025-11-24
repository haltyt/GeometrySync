"""
Test script to manually trigger mesh streaming from Blender

Run this in Blender's Scripting workspace to test if streaming works
"""

import bpy
from geometry_sync import extractor, serializer, server, handlers

def test_stream_cube():
    """Test streaming the default cube"""

    # Get cube
    cube = bpy.data.objects.get("Cube")
    if not cube:
        print("ERROR: No 'Cube' object found. Create one first.")
        return

    print(f"\n{'='*60}")
    print("Testing GeometrySync Streaming")
    print(f"{'='*60}\n")

    # Check server
    mesh_server = server.get_server()
    print(f"1. Server running: {mesh_server.running}")
    print(f"   Server connected: {mesh_server.is_connected()}")

    if not mesh_server.running:
        print("   ERROR: Server not running! Start it from the GeometrySync panel.")
        return

    if not mesh_server.is_connected():
        print("   WARNING: No Unity client connected!")
        print("   Make sure Unity is in Play mode and connected.")
        return

    # Check handlers
    scheduler = handlers.get_scheduler()
    print(f"\n2. Scheduler enabled: {scheduler.enabled}")
    print(f"   Target FPS: {scheduler.target_fps}")

    if not scheduler.enabled:
        print("   ERROR: Scheduler not enabled! Handlers not registered.")
        return

    # Extract mesh manually
    print(f"\n3. Extracting mesh from {cube.name}...")
    try:
        depsgraph = bpy.context.evaluated_depsgraph_get()
        vertices, normals, uvs, indices = extractor.extract_mesh_data_fast(cube, depsgraph)

        print(f"   ✓ Extracted: {len(vertices)} vertices, {len(indices)//3} triangles")
        print(f"   Vertex positions shape: {vertices.shape}")
        print(f"   Normals shape: {normals.shape}")
        print(f"   UVs shape: {uvs.shape}")
        print(f"   Indices count: {len(indices)}")

    except Exception as e:
        print(f"   ERROR extracting mesh: {e}")
        import traceback
        traceback.print_exc()
        return

    # Serialize
    print(f"\n4. Serializing mesh data...")
    try:
        mesh_data = serializer.serialize_mesh(vertices, normals, uvs, indices)
        print(f"   ✓ Serialized to {len(mesh_data)} bytes")

    except Exception as e:
        print(f"   ERROR serializing: {e}")
        import traceback
        traceback.print_exc()
        return

    # Send
    print(f"\n5. Sending to Unity...")
    try:
        success = mesh_server.send_mesh(mesh_data)
        if success:
            print(f"   ✓ Successfully sent mesh to Unity!")
            print(f"\n{'='*60}")
            print("SUCCESS! Check Unity Console for received data logs.")
            print(f"{'='*60}\n")
        else:
            print(f"   ERROR: Failed to send mesh")

    except Exception as e:
        print(f"   ERROR sending: {e}")
        import traceback
        traceback.print_exc()
        return


if __name__ == "__main__":
    test_stream_cube()
