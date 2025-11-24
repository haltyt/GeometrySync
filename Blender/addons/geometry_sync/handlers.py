"""
Depsgraph update handlers with throttling for real-time streaming
"""

import bpy
import time
import threading
from typing import Optional, Set
from . import extractor, serializer, server


class StreamingScheduler:
    """Throttles mesh updates to target FPS"""

    def __init__(self, target_fps: int = 30):
        self.target_fps = target_fps
        self.frame_interval = 1.0 / target_fps
        self.last_update = 0.0
        self.dirty = False
        self.dirty_objects: Set[str] = set()
        self.lock = threading.Lock()
        self.enabled = False

    def mark_dirty(self, obj_name: str):
        """Mark an object as needing update"""
        with self.lock:
            self.dirty = True
            self.dirty_objects.add(obj_name)

    def should_update(self) -> bool:
        """Check if enough time has passed for next update"""
        current_time = time.time()
        if self.dirty and (current_time - self.last_update >= self.frame_interval):
            self.last_update = current_time
            return True
        return False

    def get_dirty_objects(self) -> Set[str]:
        """Get and clear dirty objects set"""
        with self.lock:
            objects = self.dirty_objects.copy()
            self.dirty_objects.clear()
            self.dirty = False
            return objects

    def set_fps(self, fps: int):
        """Update target FPS"""
        self.target_fps = max(1, min(120, fps))
        self.frame_interval = 1.0 / self.target_fps


# Global scheduler instance
_scheduler: Optional[StreamingScheduler] = None


def get_scheduler() -> StreamingScheduler:
    """Get or create global scheduler"""
    global _scheduler
    if _scheduler is None:
        _scheduler = StreamingScheduler()
    return _scheduler


def depsgraph_update_handler(scene: bpy.types.Scene, depsgraph: bpy.types.Depsgraph):
    """
    Handler for depsgraph updates - marks objects as dirty

    This runs on every depsgraph change, so we just mark objects as needing
    update and let the timer function handle actual extraction/streaming
    """
    scheduler = get_scheduler()

    if not scheduler.enabled:
        return

    # Check which objects were updated
    for update in depsgraph.updates:
        if isinstance(update.id, bpy.types.Object):
            obj = update.id
            if obj.type == 'MESH' and update.is_updated_geometry:
                scheduler.mark_dirty(obj.name)


def frame_change_handler(scene: bpy.types.Scene):
    """
    Handler for animation frame changes

    Detects timeline frame changes and marks objects with Geometry Nodes as dirty
    """
    scheduler = get_scheduler()

    if not scheduler.enabled:
        return

    # Mark all mesh objects with Geometry Nodes modifiers as dirty
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            # Check if object has Geometry Nodes modifier
            has_geo_nodes = any(mod.type == 'NODES' for mod in obj.modifiers)
            if has_geo_nodes:
                scheduler.mark_dirty(obj.name)


def streaming_timer_function():
    """
    Timer function that handles actual mesh extraction and streaming

    This throttles updates to target FPS
    """
    scheduler = get_scheduler()

    if not scheduler.enabled:
        return 0.1  # Check again in 0.1 seconds

    if not scheduler.should_update():
        return 0.01  # Check frequently but don't process

    # Get dirty objects
    dirty_objects = scheduler.get_dirty_objects()

    if not dirty_objects:
        return 0.01

    # Get server instance
    mesh_server = server.get_server()

    if not mesh_server.is_connected():
        return 0.1  # No client connected, wait longer

    # Get current context
    context = bpy.context
    depsgraph = context.evaluated_depsgraph_get()

    # Process dirty objects
    for obj_name in dirty_objects:
        obj = bpy.data.objects.get(obj_name)
        if not obj or obj.type != 'MESH':
            continue

        try:
            # Extract mesh data
            vertices, normals, uvs, indices = extractor.extract_mesh_data_fast(obj, depsgraph)

            if len(vertices) == 0:
                continue

            # Serialize to binary
            mesh_data = serializer.serialize_mesh(vertices, normals, uvs, indices)

            # Send to Unity
            success = mesh_server.send_mesh(mesh_data)

            if success:
                print(f"Streamed {obj.name}: {len(vertices)} vertices, {len(indices)//3} triangles")
            else:
                print(f"Failed to stream {obj.name}")

        except Exception as e:
            print(f"Error processing {obj.name}: {e}")

    return 0.01  # Check again soon


def register_handlers():
    """Register depsgraph handlers and timer"""
    scheduler = get_scheduler()
    scheduler.enabled = True

    # Register depsgraph update handler
    if depsgraph_update_handler not in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.append(depsgraph_update_handler)

    # Register frame change handler (for animations)
    if frame_change_handler not in bpy.app.handlers.frame_change_post:
        bpy.app.handlers.frame_change_post.append(frame_change_handler)

    # Register timer function
    if not bpy.app.timers.is_registered(streaming_timer_function):
        bpy.app.timers.register(streaming_timer_function, persistent=True)

    print("GeometrySync handlers registered")


def unregister_handlers():
    """Unregister depsgraph handlers and timer"""
    scheduler = get_scheduler()
    scheduler.enabled = False

    # Unregister depsgraph handler
    if depsgraph_update_handler in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.remove(depsgraph_update_handler)

    # Unregister frame change handler
    if frame_change_handler in bpy.app.handlers.frame_change_post:
        bpy.app.handlers.frame_change_post.remove(frame_change_handler)

    # Unregister timer
    if bpy.app.timers.is_registered(streaming_timer_function):
        bpy.app.timers.unregister(streaming_timer_function)

    print("GeometrySync handlers unregistered")


def set_target_fps(fps: int):
    """Set target streaming FPS"""
    scheduler = get_scheduler()
    scheduler.set_fps(fps)
    print(f"Streaming FPS set to {scheduler.target_fps}")
