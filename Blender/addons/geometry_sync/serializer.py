"""
Binary serialization for mesh data
"""

import struct
import numpy as np
from typing import Dict, Any


def convert_to_unity_space(positions: np.ndarray) -> np.ndarray:
    """
    Convert Blender coordinates (Z-up, right-handed) to Unity (Y-up, left-handed)

    Args:
        positions: Array of shape (N, 3) with Blender coordinates

    Returns:
        Array with Unity coordinates (x, z, -y)
    """
    # Swap Y and Z, then flip Z
    unity_positions = positions.copy()
    unity_positions[:, [1, 2]] = unity_positions[:, [2, 1]]  # Swap Y and Z
    unity_positions[:, 2] *= -1  # Flip Z
    return unity_positions


def convert_matrix_to_unity_space(matrices: np.ndarray) -> np.ndarray:
    """
    Convert Blender transform matrices to Unity space
    Blender: Z-up, right-handed
    Unity: Y-up, left-handed

    Args:
        matrices: Array of shape (N, 4, 4) with Blender transform matrices

    Returns:
        Array with Unity transform matrices
    """
    unity_matrices = matrices.copy()

    # Convert each matrix by swapping rows and columns appropriately
    # Blender: [x, y, z] -> Unity: [x, z, -y]
    for i in range(len(matrices)):
        m = matrices[i]

        # Create new matrix with swapped and flipped axes
        # Row 0 (X-axis): stays the same but swap Y/Z components
        # Row 1 (Y-axis): becomes Z-axis (swap Y/Z, flip new Z)
        # Row 2 (Z-axis): becomes -Y-axis (swap Y/Z, flip new Z)
        # Translation: swap Y/Z, flip new Z

        unity_matrices[i] = np.array([
            [m[0, 0],  m[0, 2], -m[0, 1], m[0, 3]],  # X-axis row
            [m[2, 0],  m[2, 2], -m[2, 1], m[2, 3]],  # Z-axis row (becomes Y in Unity)
            [-m[1, 0], -m[1, 2],  m[1, 1], -m[1, 3]], # -Y-axis row (becomes Z in Unity)
            [0,        0,        0,        1]         # Homogeneous row
        ], dtype=np.float32)

    return unity_matrices


def serialize_mesh(vertices: np.ndarray,
                   normals: np.ndarray,
                   uvs: np.ndarray,
                   indices: np.ndarray) -> bytes:
    """
    Serialize mesh data to binary format for Unity

    Binary format:
    - Header: vertex_count (uint32), index_count (uint32)
    - Vertex data: interleaved [x,y,z, nx,ny,nz, u,v] as float32 (32 bytes per vertex)
    - Index data: uint32 array

    Args:
        vertices: Vertex positions (N, 3) - Blender space
        normals: Vertex normals (N, 3) - Blender space
        uvs: UV coordinates (N, 2)
        indices: Triangle indices (M,)

    Returns:
        Binary data ready to send over network
    """
    # Convert to Unity coordinate system
    vertices_unity = convert_to_unity_space(vertices)
    normals_unity = convert_to_unity_space(normals)

    # Ensure correct dtypes
    vertices_unity = vertices_unity.astype(np.float32)
    normals_unity = normals_unity.astype(np.float32)
    uvs = uvs.astype(np.float32)
    indices = indices.astype(np.uint32)

    vertex_count = len(vertices_unity)
    index_count = len(indices)

    # Build header (little-endian)
    header = struct.pack('<I I', vertex_count, index_count)

    # Interleave vertex data: [x,y,z, nx,ny,nz, u,v]
    # Use explicit little-endian dtype
    vertex_data = np.empty((vertex_count, 8), dtype='<f4')  # <f4 = little-endian float32
    vertex_data[:, 0:3] = vertices_unity
    vertex_data[:, 3:6] = normals_unity
    vertex_data[:, 6:8] = uvs

    # Convert to bytes (already little-endian from dtype)
    vertex_bytes = vertex_data.tobytes()
    index_bytes = indices.astype('<u4').tobytes()  # <u4 = little-endian uint32

    return header + vertex_bytes + index_bytes


def serialize_instance_data(mesh_id: int,
                            transforms: np.ndarray) -> bytes:
    """
    Serialize instance transform data for Geometry Nodes instances

    Binary format:
    - Message type: 0x03 (instance data)
    - mesh_id (uint32): ID of the base mesh
    - count (uint32): number of instances
    - transforms: array of 4x4 matrices (float32)

    Args:
        mesh_id: Identifier for the base mesh
        transforms: Array of 4x4 transform matrices (N, 4, 4) in Blender space

    Returns:
        Binary data for instance transforms
    """
    count = len(transforms)

    # Convert transforms from Blender space to Unity space
    transforms_unity = convert_matrix_to_unity_space(transforms)
    transforms_unity = transforms_unity.astype(np.float32)

    # Unity expects column-major matrices, but NumPy stores row-major
    # Transpose each matrix before serialization
    transforms_unity_transposed = np.transpose(transforms_unity, (0, 2, 1))

    # Header (little-endian)
    header = struct.pack('<I I', mesh_id, count)

    # Flatten matrices and convert to bytes
    matrix_bytes = transforms_unity_transposed.tobytes()

    return header + matrix_bytes


def serialize_custom_attributes(attributes: Dict[str, Any]) -> bytes:
    """
    Serialize custom Geometry Nodes attributes

    Binary format:
    - attribute_count (uint32)
    - For each attribute:
    -   name_length (uint8)
    -   name (UTF-8 string)
    -   data_type (uint8): 0=float, 1=vec2, 2=vec3, 3=vec4, 4=int
    -   count (uint32)
    -   data (array of values)

    Args:
        attributes: Dictionary of attribute name -> {'type': str, 'data': np.ndarray}

    Returns:
        Binary attribute data
    """
    buffer = bytearray()

    # Attribute count (little-endian)
    buffer.extend(struct.pack('<I', len(attributes)))

    type_map = {
        'FLOAT': 0,
        'FLOAT2': 1,
        'FLOAT_VECTOR': 2,  # vec3
        'FLOAT_COLOR': 3,    # vec4
        'INT': 4
    }

    for name, attr_data in attributes.items():
        # Name
        name_bytes = name.encode('utf-8')
        buffer.append(len(name_bytes))
        buffer.extend(name_bytes)

        # Type
        data_type = type_map.get(attr_data['type'], 0)
        buffer.append(data_type)

        # Data
        data = attr_data['data'].astype(np.float32 if data_type < 4 else np.int32)
        count = len(data)
        buffer.extend(struct.pack('<I', count))
        buffer.extend(data.tobytes())

    return bytes(buffer)
