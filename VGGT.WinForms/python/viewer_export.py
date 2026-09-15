"""Rv3dViewer project v10 / CameraAnimation v4 interoperability."""
import json
import math
import uuid
from pathlib import Path
import numpy as np


def texture_mesh(source, destination):
    """Bake vertex RGB into small padded triangle tiles for the viewer importer."""
    import trimesh
    from PIL import Image
    scene = trimesh.load(source)
    if scene.geometry and all(getattr(m.visual, 'kind', None) == 'texture' and
            getattr(m.visual.material, 'baseColorTexture', None) is not None for m in scene.geometry.values()):
        # Preserve photographic textures and all view meshes without rebaking.
        import shutil
        shutil.copyfile(source, destination)
        return
    mesh = next(iter(scene.geometry.values()))
    colors = (mesh.visual.vertex_colors if mesh.visual.kind == 'vertex'
              else mesh.visual.vertex_attributes['color'])[:, :3]
    faces = mesh.faces
    columns = math.ceil(math.sqrt(len(faces)))
    rows = math.ceil(len(faces) / columns)
    atlas = np.zeros((rows * 4, columns * 4, 3), dtype=np.uint8)
    y, x = np.mgrid[:4, :4]
    b, c = x / 2., y / 2.
    a = np.maximum(0, 1-b-c)
    weights = np.stack([a, b, c], -1)
    weights /= weights.sum(-1, keepdims=True)
    for start in range(0, len(faces), 8192):
        ids = np.arange(start, min(start+8192, len(faces)))
        tiles = np.einsum('xyk,nkc->nxyc', weights, colors[faces[ids]])
        yy = (ids // columns)[:, None, None] * 4 + y
        xx = (ids % columns)[:, None, None] * 4 + x
        atlas[yy, xx] = np.clip(np.rint(tiles), 0, 255).astype(np.uint8)
    ids = np.arange(len(faces))
    uv = np.empty((len(faces), 3, 2))
    uv[..., 0] = ((ids % columns)[:, None] * 4 + [.5, 2.5, .5]) / (columns * 4)
    uv[..., 1] = 1 - ((ids // columns)[:, None] * 4 + [.5, .5, 2.5]) / (rows * 4)
    material = trimesh.visual.material.PBRMaterial(baseColorTexture=Image.fromarray(atlas),
        baseColorFactor=[255, 255, 255, 255], metallicFactor=0., roughnessFactor=1., doubleSided=True)
    triangles = mesh.vertices[faces]
    face_normals = np.cross(triangles[:, 1]-triangles[:, 0], triangles[:, 2]-triangles[:, 0])
    normals = np.zeros_like(mesh.vertices)
    for corner in range(3):
        np.add.at(normals, faces[:, corner], face_normals)
    normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-15)
    result = trimesh.Trimesh(vertices=mesh.vertices[faces].reshape(-1, 3),
        faces=np.arange(len(faces)*3).reshape(-1, 3), process=False,
        vertex_normals=normals[faces].reshape(-1, 3),
        visual=trimesh.visual.texture.TextureVisuals(uv=uv.reshape(-1, 2), material=material))
    result.export(destination, file_type='glb')


def vector(v):
    return dict(zip(('x', 'y', 'z'), map(float, v)))


def export_viewer(output, extrinsics, intrinsics, height, image_names, mesh_name=None, times=None):
    output = Path(output)
    if times is None:
        times = list(map(float, range(len(image_names))))
    if len(times) != len(image_names) or not times or not np.isfinite(times).all() or any(b <= a for a, b in zip(times, times[1:])):
        raise ValueError('Camera 時間必須與影像數量相同且嚴格遞增。')
    relative_times = np.asarray(times) - times[0]
    if relative_times[-1] > 3600:
        raise ValueError('CameraAnimation 支援的最長路徑為 3600 秒。')
    frames = []
    flip = np.array([1., -1., -1.])
    for i, (extrinsic, intrinsic) in enumerate(zip(extrinsics, intrinsics)):
        e = np.asarray(extrinsic, dtype=float)
        r, t = e[:, :3], e[:, 3]
        position = (-r.T @ t) * flip
        forward = r.T[:, 2] * flip
        forward /= np.linalg.norm(forward)
        up = -r.T[:, 1] * flip
        up -= forward * np.dot(up, forward)
        up /= np.linalg.norm(up)
        reference = np.array([0., 1., 0.])
        if abs(forward[1]) > .999:
            reference = np.array([0., 0., -1. if forward[1] < 0 else 1.])
        right = np.cross(forward, reference)
        right /= np.linalg.norm(right)
        base_up = np.cross(right, forward)
        roll = math.degrees(math.atan2(np.dot(forward, np.cross(base_up, up)), np.dot(base_up, up)))
        fov = math.degrees(2 * math.atan(height / (2 * float(intrinsic[1][1]))))
        frames.append(dict(id=str(uuid.uuid4()), name=Path(image_names[i]).name,
            timeSeconds=float(relative_times[i]), **{'from': vector(position)}, to=vector(position + forward),
            up=vector(up), rollDegrees=roll, fieldOfViewDegrees=fov,
            nearPlane=.001, farPlane=10000., easing=0))
    if not frames:
        raise ValueError('Camera export requires at least one view')
    animation = dict(version=4, name='VGGT Camera Path', durationSeconds=max(.1, float(relative_times[-1])),
                     framesPerSecond=30, loop=False, keyframes=frames)
    animation_name = 'scene.camera-animation.json'
    (output / animation_name).write_text(json.dumps(animation, ensure_ascii=False, indent=2, allow_nan=False), encoding='utf-8')
    project_name = None
    if mesh_name:
        texture_mesh(output / mesh_name, output / 'viewer_scene.glb')
        project_name = 'scene.rv3dproj'
        camera = {k: frames[0][k] for k in ('from', 'to', 'up', 'rollDegrees', 'fieldOfViewDegrees', 'nearPlane', 'farPlane')}
        project = dict(formatVersion=10, name='VGGT Scene', camera=camera,
                       models=[dict(id=str(uuid.uuid4()), name='VGGT Scene', assetPath='viewer_scene.glb', isVisible=True)])
        (output / project_name).write_text(json.dumps(project, ensure_ascii=False, indent=2, allow_nan=False), encoding='utf-8')
    return project_name, animation_name


def paired_names(image_paths):
    """Reserve both names case-insensitively (including foo vs foo_Depth)."""
    used = set()
    result = []
    for path in image_paths:
        stem = Path(path).stem
        candidate, suffix = stem, 1
        while candidate.casefold() in used or (candidate + '_Depth').casefold() in used:
            suffix += 1
            candidate = f'{stem}_{suffix}'
        used.update([candidate.casefold(), (candidate + '_Depth').casefold()])
        result.append((candidate + '.png', candidate + '_Depth.png'))
    return result
