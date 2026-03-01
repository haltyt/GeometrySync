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


def extract_base_mesh_data(obj: bpy.types.Object) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """
    Extract base mesh data WITHOUT Geometry Nodes evaluation

    Args:
        obj: Blender object

    Returns:
        Tuple of (vertices, normals, uvs, indices)
    """
    mesh = obj.data

    # Ensure mesh has triangulated data
    if hasattr(mesh, 'calc_normals_split'):
        mesh.calc_normals_split()
    mesh.calc_loop_triangles()

    # Get triangle loops
    tri_count = len(mesh.loop_triangles)
    if tri_count == 0:
        return (np.zeros((0, 3), dtype=np.float32),
                np.zeros((0, 3), dtype=np.float32),
                np.zeros((0, 2), dtype=np.float32),
                np.zeros(0, dtype=np.uint32))

    # Extract loop indices for triangles
    tri_loops = np.zeros(tri_count * 3, dtype=np.int32)
    mesh.loop_triangles.foreach_get('loops', tri_loops)

    # Get vertex indices from loops
    vertex_indices = np.zeros(len(mesh.loops), dtype=np.int32)
    mesh.loops.foreach_get('vertex_index', vertex_indices)
    tri_vertex_indices = vertex_indices[tri_loops]

    # Extract all vertex positions
    all_positions = np.zeros(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', all_positions)
    all_positions = all_positions.reshape((-1, 3))

    # Expand positions to per-loop
    positions = all_positions[tri_vertex_indices]

    # Extract normals (per-loop)
    all_normals = np.zeros(len(mesh.loops) * 3, dtype=np.float32)
    mesh.loops.foreach_get('normal', all_normals)
    all_normals = all_normals.reshape((-1, 3))
    normals = all_normals[tri_loops]

    # Extract UVs (per-loop)
    if mesh.uv_layers.active:
        all_uvs = np.zeros(len(mesh.loops) * 2, dtype=np.float32)
        mesh.uv_layers.active.data.foreach_get('uv', all_uvs)
        all_uvs = all_uvs.reshape((-1, 2))
        uvs = all_uvs[tri_loops]
    else:
        uvs = np.zeros((len(positions), 2), dtype=np.float32)

    # Sequential indices
    indices = np.arange(len(positions), dtype=np.uint32)

    return positions, normals, uvs, indices


def extract_mesh_data_fast(obj: bpy.types.Object,
                           depsgraph: bpy.types.Depsgraph,
                           scale_multiplier: float = 1.0) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """
    Fast mesh extraction optimized for real-time streaming

    Args:
        obj: Blender object
        depsgraph: Evaluated depsgraph
        scale_multiplier: Global scale multiplier for mesh vertices

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

    # Extract loop indices for triangles (3 indices per triangle)
    tri_loops = np.zeros(tri_count * 3, dtype=np.int32)
    mesh_eval.loop_triangles.foreach_get('loops', tri_loops)

    # Validate loop indices
    if len(tri_loops) > 0 and tri_loops.max() >= len(mesh_eval.loops):
        print(f"[ERROR] Loop index out of bounds: max={tri_loops.max()}, loops={len(mesh_eval.loops)}")
        return (np.zeros((0, 3), dtype=np.float32),
                np.zeros((0, 3), dtype=np.float32),
                np.zeros((0, 2), dtype=np.float32),
                np.zeros(0, dtype=np.uint32))

    # Get vertex indices for each loop
    vertex_indices = np.zeros(len(mesh_eval.loops), dtype=np.int32)
    mesh_eval.loops.foreach_get('vertex_index', vertex_indices)
    tri_vertex_indices = vertex_indices[tri_loops]

    # Extract all vertex positions
    all_positions = np.zeros(len(mesh_eval.vertices) * 3, dtype=np.float32)
    mesh_eval.vertices.foreach_get('co', all_positions)
    all_positions = all_positions.reshape((-1, 3))

    # Expand positions to per-triangle-vertex
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
        uvs = np.zeros((tri_count * 3, 2), dtype=np.float32)

    # Sequential indices (0, 1, 2, 3, 4, 5, ...)
    indices = np.arange(tri_count * 3, dtype=np.uint32)

    # Apply scale multiplier to vertices
    if scale_multiplier != 1.0:
        positions *= scale_multiplier

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
                                depsgraph: bpy.types.Depsgraph,
                                base_mesh_scale_multiplier: float = 1.0) -> Optional[Tuple[str, np.ndarray, Tuple]]:
    """
    Extract instance transforms from Geometry Nodes instances

    Args:
        obj: Blender object with Geometry Nodes
        depsgraph: Evaluated depsgraph
        base_mesh_scale_multiplier: Global scale multiplier for base mesh vertices

    Returns:
        Tuple of (base_mesh_name, transform_matrices, base_mesh_data) or None if no instances
        - base_mesh_name: Name of the base mesh being instanced
        - transform_matrices: NumPy array of shape (N, 4, 4) with transform matrices
        - base_mesh_data: Tuple of (vertices, normals, uvs, indices) for the base mesh
    """
    # Check if object has Geometry Nodes modifier
    has_geo_nodes = any(mod.type == 'NODES' for mod in obj.modifiers)
    if not has_geo_nodes:
        return None

    # Get evaluated object
    obj_eval = obj.evaluated_get(depsgraph)

    # Collect instances using depsgraph.object_instances
    # This includes the full transform including instance offset
    base_object = None
    instance_data = []

    for instance in depsgraph.object_instances:
        # Check if this instance belongs to our object
        if instance.parent and instance.parent.original == obj:
            if instance.is_instance:
                # Get the instance object and its world matrix
                inst_obj = instance.object
                if inst_obj:
                    # Store instance data: (matrix, object)
                    instance_data.append((instance.matrix_world.copy(), inst_obj))
                    if base_object is None:
                        base_object = inst_obj.original

    if not instance_data or base_object is None:
        return None

    instance_count = len(instance_data)

    # Get parent object's world matrix to compute relative transforms
    parent_matrix = obj_eval.matrix_world.copy()
    parent_matrix_inv = parent_matrix.inverted()

    # Convert to numpy array
    transforms = np.empty((instance_count, 4, 4), dtype=np.float32)

    for idx, (matrix, inst_obj) in enumerate(instance_data):
        # Convert matrix to local space relative to parent
        # This gives us the actual instance position
        local_matrix = parent_matrix_inv @ matrix

        # Decompose matrix into translation, rotation, and scale
        from mathutils import Matrix
        translation = local_matrix.translation
        rotation = local_matrix.to_quaternion()
        scale = local_matrix.to_scale()

        # Keep transform scale unchanged (don't modify instance scale)
        # Base mesh scale is applied to vertices instead
        local_matrix = Matrix.LocRotScale(translation, rotation, scale)

        for i in range(4):
            for j in range(4):
                transforms[idx, i, j] = local_matrix[i][j]

    # DEBUG: Log first transform's scale component
    if instance_count > 0:
        # Extract scale from first transform matrix
        import math
        m = transforms[0]
        scale_x = math.sqrt(m[0, 0]**2 + m[1, 0]**2 + m[2, 0]**2)
        scale_y = math.sqrt(m[0, 1]**2 + m[1, 1]**2 + m[2, 1]**2)
        scale_z = math.sqrt(m[0, 2]**2 + m[1, 2]**2 + m[2, 2]**2)
        print(f"[Extractor] Transform[0] scale (unchanged): ({scale_x:.3f}, {scale_y:.3f}, {scale_z:.3f})")

    # Extract base mesh data WITH scale applied to vertices
    try:
        base_mesh_data = extract_mesh_data_fast(base_object, depsgraph, scale_multiplier=base_mesh_scale_multiplier)
        vertices, normals, uvs, indices = base_mesh_data
        print(f"[Extractor] Base mesh vertices scaled by: {base_mesh_scale_multiplier}, Vertex count: {len(vertices)}")
    except Exception as e:
        print(f"Failed to extract base mesh for instances: {e}")
        return None

    return (base_object.name, transforms, base_mesh_data)
