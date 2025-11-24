"""
GeometrySync - Real-time Blender Geometry Nodes streaming to Unity

Streams mesh data from Blender's Geometry Nodes to Unity over TCP,
enabling real-time visualization of procedural geometry with custom shaders.
"""

bl_info = {
    "name": "GeometrySync",
    "author": "GeometrySync Team",
    "version": (1, 0, 0),
    "blender": (4, 5, 0),
    "location": "View3D > Sidebar > GeometrySync",
    "description": "Stream Geometry Nodes output to Unity in real-time",
    "category": "3D View",
    "doc_url": "",
    "tracker_url": "",
}


import bpy
from . import ui, handlers, server


def register():
    """Register addon"""
    print("Registering GeometrySync addon...")

    # Register UI
    ui.register()

    print("GeometrySync addon registered successfully")


def unregister():
    """Unregister addon"""
    print("Unregistering GeometrySync addon...")

    # Stop server and handlers
    handlers.unregister_handlers()
    server.cleanup_server()

    # Unregister UI
    ui.unregister()

    print("GeometrySync addon unregistered")


if __name__ == "__main__":
    register()
