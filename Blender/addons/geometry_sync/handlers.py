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
        # Cache sent base meshes to avoid resending (mesh_id -> True)
        self.sent_base_meshes: Set[int] = set()

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

    def clear_base_mesh_cache(self):
        """Clear base mesh cache (call when streaming session starts)"""
        with self.lock:
            self.sent_base_meshes.clear()

    def has_sent_base_mesh(self, mesh_id: int) -> bool:
        """Check if base mesh was already sent"""
        with self.lock:
            return mesh_id in self.sent_base_meshes

    def mark_base_mesh_sent(self, mesh_id: int):
        """Mark base mesh as sent"""
        with self.lock:
            self.sent_base_meshes.add(mesh_id)


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
    Only processes visible and selected objects
    """
    scheduler = get_scheduler()

    if not scheduler.enabled:
        return

    # Get selected object names for fast lookup
    selected_names = {obj.name for obj in bpy.context.selected_objects}

    # Check which objects were updated
    for update in depsgraph.updates:
        if isinstance(update.id, bpy.types.Object):
            obj = update.id
            if obj.type == 'MESH' and update.is_updated_geometry:
                # Only mark if object is selected and visible
                if obj.name in selected_names and not obj.hide_viewport and not obj.hide_get():
                    scheduler.mark_dirty(obj.name)


def frame_change_handler(scene: bpy.types.Scene):
    """
    Handler for animation frame changes

    Detects timeline frame changes and marks objects with Geometry Nodes as dirty
    Only processes visible and selected objects
    """
    scheduler = get_scheduler()

    if not scheduler.enabled:
        return

    # Mark selected visible mesh objects with Geometry Nodes modifiers as dirty
    for obj in bpy.context.selected_objects:
        if obj.type == 'MESH':
            # Skip hidden objects
            if obj.hide_viewport or obj.hide_get():
                continue

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

    # Performance timing
    frame_start = time.time()

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
            # Phase 2: Check for instances first (Geometry Nodes)
            # Get scale parameter from UI
            base_mesh_scale = context.scene.geometrysync_base_mesh_scale

            extract_start = time.time()
            instance_result = extractor.extract_instance_transforms(obj, depsgraph, base_mesh_scale)
            extract_time = (time.time() - extract_start) * 1000  # ms

            if instance_result is not None:
                # Object has instances - send base mesh + instance data
                base_mesh_name, transforms, base_mesh_data = instance_result
                vertices, normals, uvs, indices = base_mesh_data

                # Generate mesh ID from base mesh name + scale (so different scales = different meshes)
                # This ensures Unity updates the mesh when scale changes
                mesh_id_string = f"{base_mesh_name}_scale_{base_mesh_scale:.3f}"
                mesh_id = hash(mesh_id_string) & 0xFFFFFFFF  # Keep as uint32

                # Only send base mesh if not already sent (optimization)
                already_sent = scheduler.has_sent_base_mesh(mesh_id)
                print(f"[Handler] mesh_id={mesh_id} ({mesh_id_string}), already_sent={already_sent}, vertices={len(vertices)}")

                if not already_sent:
                    # Serialize and send base mesh
                    mesh_data = serializer.serialize_mesh(vertices, normals, uvs, indices)
                    success = mesh_server.send_mesh(mesh_data)

                    if not success:
                        print(f"Failed to send base mesh for {obj.name}")
                        continue

                    scheduler.mark_base_mesh_sent(mesh_id)
                    print(f"Sent base mesh {base_mesh_name} (mesh_id={mesh_id}): {len(vertices)} vertices")
                else:
                    print(f"Skipping base mesh send (already sent): {base_mesh_name} (mesh_id={mesh_id})")

                # Always send instance data (transforms change every frame)
                serialize_start = time.time()
                instance_data = serializer.serialize_instance_data(mesh_id, transforms)
                serialize_time = (time.time() - serialize_start) * 1000  # ms

                send_start = time.time()
                success = mesh_server.send_instance_data(instance_data)
                send_time = (time.time() - send_start) * 1000  # ms

                if success:
                    total_time = (time.time() - frame_start) * 1000  # ms
                    print(f"Streamed {obj.name}: {len(transforms)} instances (extract: {extract_time:.1f}ms, serialize: {serialize_time:.1f}ms, send: {send_time:.1f}ms, total: {total_time:.1f}ms)")
                else:
                    print(f"Failed to send instances for {obj.name}")

            else:
                # No instances - send evaluated mesh (with all modifiers)
                vertices, normals, uvs, indices = extractor.extract_mesh_data_fast(obj, depsgraph, base_mesh_scale)
                mesh_type = "mesh"

                if len(vertices) == 0:
                    continue

                # Serialize to binary
                mesh_data = serializer.serialize_mesh(vertices, normals, uvs, indices)

                # Send to Unity
                success = mesh_server.send_mesh(mesh_data)

                if success:
                    print(f"Streamed {obj.name} ({mesh_type}): {len(vertices)} vertices, {len(indices)//3} triangles")
                else:
                    print(f"Failed to stream {obj.name}")

        except Exception as e:
            print(f"Error processing {obj.name}: {e}")

    return 0.01  # Check again soon


def register_handlers():
    """Register depsgraph handlers and timer"""
    scheduler = get_scheduler()
    scheduler.enabled = True
    scheduler.clear_base_mesh_cache()  # Clear cache on new session

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


def on_base_mesh_scale_changed():
    """Clear base mesh cache when scale changes and mark objects as dirty"""
    import bpy
    scheduler = get_scheduler()
    scheduler.clear_base_mesh_cache()

    # Mark all selected mesh objects as dirty to trigger immediate update
    dirty_count = 0
    for obj in bpy.context.selected_objects:
        if obj.type == 'MESH':
            scheduler.mark_dirty(obj.name)
            dirty_count += 1

    new_scale = bpy.context.scene.geometrysync_base_mesh_scale
    print(f"[GeometrySync] Base mesh scale changed to {new_scale}, cleared cache, marked {dirty_count} objects as dirty")
