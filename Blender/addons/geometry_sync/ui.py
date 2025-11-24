"""
Blender UI panel for GeometrySync
"""

import bpy
from . import server, handlers


class GEOMETRYSYNC_PT_MainPanel(bpy.types.Panel):
    """GeometrySync control panel"""
    bl_label = "GeometrySync"
    bl_idname = "GEOMETRYSYNC_PT_main_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'GeometrySync'

    def draw(self, context):
        layout = self.layout
        scene = context.scene

        # Server controls
        box = layout.box()
        box.label(text="Server", icon='NETWORK_DRIVE')

        mesh_server = server.get_server()
        is_running = mesh_server.running
        is_connected = mesh_server.is_connected()

        row = box.row()
        if is_running:
            row.operator("geometrysync.stop_server", text="Stop Server", icon='CANCEL')
            status_icon = 'LINKED' if is_connected else 'UNLINKED'
            status_text = "Connected" if is_connected else "Waiting for Unity..."
            box.label(text=status_text, icon=status_icon)
        else:
            row.operator("geometrysync.start_server", text="Start Server", icon='PLAY')

        # Streaming settings
        box = layout.box()
        box.label(text="Streaming", icon='ARROW_LEFTRIGHT')

        scheduler = handlers.get_scheduler()
        box.prop(scene, "geometrysync_fps", text="Target FPS")

        if scheduler.enabled:
            box.label(text=f"Active: {scheduler.target_fps} FPS", icon='TIME')
        else:
            box.label(text="Inactive", icon='PAUSE')

        # Statistics
        box = layout.box()
        box.label(text="Info", icon='INFO')
        box.label(text=f"Server: {mesh_server.host}:{mesh_server.port}")


class GEOMETRYSYNC_OT_StartServer(bpy.types.Operator):
    """Start the GeometrySync server"""
    bl_idname = "geometrysync.start_server"
    bl_label = "Start Server"

    def execute(self, context):
        mesh_server = server.get_server()
        mesh_server.start()
        handlers.register_handlers()
        return {'FINISHED'}


class GEOMETRYSYNC_OT_StopServer(bpy.types.Operator):
    """Stop the GeometrySync server"""
    bl_idname = "geometrysync.stop_server"
    bl_label = "Stop Server"

    def execute(self, context):
        handlers.unregister_handlers()
        mesh_server = server.get_server()
        mesh_server.stop()
        return {'FINISHED'}


def register_properties():
    """Register scene properties"""
    bpy.types.Scene.geometrysync_fps = bpy.props.IntProperty(
        name="Target FPS",
        description="Target streaming frame rate",
        default=30,
        min=1,
        max=120,
        update=lambda self, context: handlers.set_target_fps(self.geometrysync_fps)
    )


def unregister_properties():
    """Unregister scene properties"""
    del bpy.types.Scene.geometrysync_fps


classes = (
    GEOMETRYSYNC_PT_MainPanel,
    GEOMETRYSYNC_OT_StartServer,
    GEOMETRYSYNC_OT_StopServer,
)


def register():
    """Register UI classes and properties"""
    for cls in classes:
        bpy.utils.register_class(cls)
    register_properties()


def unregister():
    """Unregister UI classes and properties"""
    unregister_properties()
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
