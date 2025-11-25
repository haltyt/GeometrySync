"""
Mesh data extraction from Blender Geometry Nodes
"""

import bpy
import bmesh
import numpy as np
from typing import Tuple, Dict, Any, Optional


def extract_mesh_data(obj: bpy.types.Object,
                     depsgraph: bpy.types.Depsgraph) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """
    Extract mesh data from evaluated object (with Geometry Nodes applied)

    Args:
        obj: Blender object to extract from
        depsgraph: Evaluated depsgraph

    Returns:
        Tuple of (vertices, normals, uvs, indices) as numpy arrays
    """
    # Get evaluated object with all modifiers applied
    obj_eval = obj.evaluated_get(depsgraph)
    mesh_eval = obj_eval.data

    if not mesh_eval:
        raise ValueError(f"Object {obj.name} has no mesh data")

    # Calculate split normals for proper export
    # Note: calc_normals_split() was removed in Blender 4.1+
    if hasattr(mesh_eval, 'calc_normals_split'):
        mesh_eval.calc_normals_split()
    mesh_eval.calc_loop_triangles()

    # Extract vertices using fast foreach_get
    vertex_count = len(mesh_eval.vertices)
    vertices = np.zeros(vertex_count * 3, dtype=np.float32)
    mesh_eval.vertices.foreach_get('co', vertices)
    vertices = vertices.reshape((-1, 3))

    # Extract loop normals (one per triangle vertex)
    loop_count = len(mesh_eval.loops)
    loop_normals = np.zeros(loop_count * 3, dtype=np.float32)
    mesh_eval.loops.foreach_get('normal', loop_normals)
    loop_normals = loop_normals.reshape((-1, 3))

    # Extract UVs (or create default if none exist)
    if mesh_eval.uv_layers.active:
        uvs_flat = np.zeros(loop_count * 2, dtype=np.float32)
        mesh_eval.uv_layers.active.data.foreach_get('uv', uvs_flat)
        loop_uvs = uvs_flat.reshape((-1, 2))
    else:
        # Default UVs
        loop_uvs = np.zeros((loop_count, 2), dtype=np.float32)

    # Extract triangulated indices
    tri_count = len(mesh_eval.loop_triangles)
    loop_indices = np.zeros(tri_count * 3, dtype=np.int32)
    mesh_eval.loop_triangles.foreach_get('loops', loop_indices)

    # Build per-vertex data (expand from loops)
    # Unity expects per-vertex data, so we need to duplicate vertices per loop
    unique_vertices = vertices[mesh_eval.loops[loop_indices].foreach_get('vertex_index', np.zeros(len(loop_indices), dtype=np.int32))]
    unique_normals = loop_normals[loop_indices]
    unique_uvs = loop_uvs[loop_indices]

    # Indices are just sequential since we expanded vertices
    indices = np.arange(len(unique_vertices), dtype=np.uint32)

    return unique_vertices, unique_normals, unique_uvs, indices


def extract_mesh_data_fast(obj: bpy.types.Object,
                           depsgraph: bpy.types.Depsgraph) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """
    Fast mesh extraction optimized for real-time streaming

    Args:
        obj: Blender object
        depsgraph: Evaluated depsgraph

    Returns:
        Tuple of (vertices, normals, uvs, indices)
    """
    obj_eval = obj.evaluated_get(depsgraph)
    mesh_eval = obj_eval.data

    # Ensure mesh has triangulated data
    # Note: calc_normals_split() was removed in Blender 4.1+
    # Normals are now auto-calculated
    if hasattr(mesh_eval, 'calc_normals_split'):
        mesh_eval.calc_normals_split()
    mesh_eval.calc_loop_triangles()

    # Get triangle loops
    tri_count = len(mesh_eval.loop_triangles)
    if tri_count == 0:
        # Empty mesh
        return (np.zeros((0, 3), dtype=np.float32),
                np.zeros((0, 3), dtype=np.float32),
                np.zeros((0, 2), dtype=np.float32),
                np.zeros(0, dtype=np.uint32))

    # Extract loop indices for triangles
    tri_loops = np.zeros(tri_count * 3, dtype=np.int32)
    mesh_eval.loop_triangles.foreach_get('loops', tri_loops)

    # Get vertex indices from loops
    vertex_indices = np.zeros(len(mesh_eval.loops), dtype=np.int32)
    mesh_eval.loops.foreach_get('vertex_index', vertex_indices)
    tri_vertex_indices = vertex_indices[tri_loops]

    # Extract all vertex positions
    all_positions = np.zeros(len(mesh_eval.vertices) * 3, dtype=np.float32)
    mesh_eval.vertices.foreach_get('co', all_positions)
    all_positions = all_positions.reshape((-1, 3))

    # Expand positions to per-loop
    positions = all_positions[tri_vertex_indices]

    # Extract normals (per-loop)
    all_normals = np.zeros(len(mesh_eval.loops) * 3, dtype=np.float32)
    mesh_eval.loops.foreach_get('normal', all_normals)
    all_normals = all_normals.reshape((-1, 3))
    normals = all_normals[tri_loops]

    # Extract UVs (per-loop)
    if mesh_eval.uv_layers.active:
        all_uvs = np.zeros(len(mesh_eval.loops) * 2, dtype=np.float32)
        mesh_eval.uv_layers.active.data.foreach_get('uv', all_uvs)
        all_uvs = all_uvs.reshape((-1, 2))
        uvs = all_uvs[tri_loops]
    else:
        uvs = np.zeros((len(positions), 2), dtype=np.float32)

    # Sequential indices
    indices = np.arange(len(positions), dtype=np.uint32)

    return positions, normals, uvs, indices


def extract_custom_attributes(obj: bpy.types.Object,
                             depsgraph: bpy.types.Depsgraph) -> Dict[str, Any]:
    """
    Extract custom attributes from Geometry Nodes

    Args:
        obj: Blender object
        depsgraph: Evaluated depsgraph

    Returns:
        Dictionary of attribute_name -> {'type': str, 'data': np.ndarray, 'domain': str}
    """
    obj_eval = obj.evaluated_get(depsgraph)
    mesh_eval = obj_eval.data

    attributes = {}

    for attr_name in mesh_eval.attributes.keys():
        # Skip internal attributes
        if attr_name.startswith('.'):
            continue

        attr = mesh_eval.attributes[attr_name]

        # Determine data size based on type
        data_size = {
            'FLOAT': 1,
            'FLOAT2': 2,
            'FLOAT_VECTOR': 3,
            'FLOAT_COLOR': 4,
            'INT': 1,
            'BOOLEAN': 1
        }.get(attr.data_type, 1)

        # Extract data using foreach_get
        data_count = len(attr.data)

        if attr.data_type in ['FLOAT', 'INT', 'BOOLEAN']:
            data = np.zeros(data_count, dtype=np.float32 if attr.data_type == 'FLOAT' else np.int32)
            attr.data.foreach_get('value', data)
        elif attr.data_type == 'FLOAT2':
            data = np.zeros(data_count * 2, dtype=np.float32)
            attr.data.foreach_get('vector', data)  # FLOAT2 uses 'vector'
            data = data.reshape((-1, 2))
        elif attr.data_type == 'FLOAT_VECTOR':
            data = np.zeros(data_count * 3, dtype=np.float32)
            attr.data.foreach_get('vector', data)
            data = data.reshape((-1, 3))
        elif attr.data_type == 'FLOAT_COLOR':
            data = np.zeros(data_count * 4, dtype=np.float32)
            attr.data.foreach_get('color', data)
            data = data.reshape((-1, 4))
        else:
            continue

        attributes[attr_name] = {
            'type': attr.data_type,
            'data': data,
            'domain': attr.domain  # POINT, FACE, CORNER, EDGE
        }

    return attributes


def extract_instance_transforms(obj: bpy.types.Object,
                                depsgraph: bpy.types.Depsgraph) -> Optional[Tuple[str, np.ndarray, Tuple]]:
    """
    Extract instance transforms from Geometry Nodes instances

    Args:
        obj: Blender object with Geometry Nodes
        depsgraph: Evaluated depsgraph

    Returns:
        Tuple of (base_mesh_name, transform_matrices, base_mesh_data) or None if no instances
        - base_mesh_name: Name of the base mesh being instanced
        - transform_matrices: NumPy array of shape (N, 4, 4) with transform matrices
        - base_mesh_data: Tuple of (vertices, normals, uvs, indices) for the base mesh
    """
    # Check if object has Geometry Nodes modifier
    has_geo_nodes = any(mod.type == 'NODES' for mod in obj.modifiers)
    if not has_geo_nodes:
        print(f"[extract_instance_transforms] {obj.name} has no Geometry Nodes modifier")
        return None

    instances = []
    base_object = None

    print(f"[extract_instance_transforms] Checking {obj.name} for instances...")

    # Iterate through depsgraph instances
    # The depsgraph contains all evaluated object instances
    instance_count = 0
    for instance in depsgraph.object_instances:
        instance_count += 1
        # Check if this instance belongs to our object
        # instance.parent is the object that generated the instance
        if instance.parent and instance.parent.original == obj:
            print(f"  Found instance from {obj.name}: is_instance={instance.is_instance}")
            if instance.is_instance:
                # Get the 4x4 transform matrix (world space)
                # Convert Blender's Matrix to NumPy array
                matrix = np.array(instance.matrix_world, dtype=np.float32).reshape(4, 4)
                instances.append(matrix)

                # Get base object reference (the instanced mesh)
                if base_object is None and instance.object:
                    base_object = instance.object.original
                    print(f"  Base object: {base_object.name if base_object else 'None'}")

    print(f"[extract_instance_transforms] Total depsgraph instances: {instance_count}, Found {len(instances)} instances for {obj.name}")

    if not instances or base_object is None:
        print(f"[extract_instance_transforms] No instances found or no base object")
        return None

    # Stack matrices into (N, 4, 4) array
    transforms = np.stack(instances, axis=0)

    # Extract base mesh data
    try:
        base_mesh_data = extract_mesh_data_fast(base_object, depsgraph)
    except Exception as e:
        print(f"Failed to extract base mesh for instances: {e}")
        return None

    return (base_object.name, transforms, base_mesh_data)
