"""Photo-textured organized surface reconstruction; no invented large surfaces."""
import math
from pathlib import Path
import numpy as np
from geometry_cleanup import confidence_mask, grid_faces


def repair_small_holes(points, depth, valid, domain, max_area=256):
    """Interpolate enclosed, small, depth-consistent gaps only.

    Padding, image boundaries, large holes and depth discontinuities stay open.
    Returns copies: dense prediction output remains untouched.
    """
    import cv2
    points, depth, valid = points.copy(), depth.copy(), valid.copy()
    count, labels, stats, _ = cv2.connectedComponentsWithStats((~valid).astype(np.uint8), connectivity=8)
    repaired = 0
    h, w = valid.shape
    for label in range(1, count):
        x, y, width, height, area = stats[label]
        if area > max_area or x == 0 or y == 0 or x+width >= w or y+height >= h:
            continue
        region = np.s_[y-1:y+height+1, x-1:x+width+1]
        hole = labels[region] == label
        if not domain[region][hole].all():
            continue
        ring = cv2.dilate(hole.astype(np.uint8), np.ones((3,3), np.uint8)).astype(bool) & ~hole
        local_valid = valid[region]
        if not local_valid[ring].all():
            continue
        boundary_z = depth[region][ring]
        if not np.isfinite(boundary_z).all() or boundary_z.min() <= 0:
            continue
        if np.ptp(boundary_z) > .04 * np.median(boundary_z):
            continue
        boundary_points = points[region][ring]
        center = boundary_points.mean(0)
        # Require a locally planar boundary, to avoid flattening curved edges.
        _, singular, axes = np.linalg.svd(boundary_points-center, full_matrices=False)
        scale = np.linalg.norm(np.ptp(boundary_points, axis=0))
        if scale <= 1e-10 or np.max(np.abs((boundary_points-center) @ axes[-1])) > scale*.015:
            continue
        # Harmonic interpolation from fixed surrounding valid pixels.
        local_points = points[region].copy()
        local_depth = depth[region].copy()
        local_points[hole] = center
        local_depth[hole] = np.mean(boundary_z)
        kernel = np.array([[0,.25,0],[.25,0,.25],[0,.25,0]], np.float32)
        for _ in range(max(width, height)*12):
            averaged = cv2.filter2D(local_points, -1, kernel)
            averaged_depth = cv2.filter2D(local_depth, -1, kernel)
            local_points[hole] = averaged[hole]
            local_depth[hole] = averaged_depth[hole]
        points[region][hole] = local_points[hole]
        depth[region][hole] = local_depth[hole]
        valid[region][hole] = True
        repaired += int(area)
    return points, depth, valid, repaired


def image_geometry(width, height, model_height, model_width, mode):
    if mode == 'pad':
        if width >= height:
            rw, rh = 518, round(height * (518/width)/14)*14
        else:
            rw, rh = round(width * (518/height)/14)*14, 518
        return rw, rh, (model_width-rw)//2, (model_height-rh)//2, 0
    rh = round(height*(518/width)/14)*14
    return 518, rh, 0, (model_height-min(rh,518))//2, max(0,(rh-518)//2)


def content_mask(path, model_height, model_width, mode):
    from PIL import Image
    with Image.open(path) as image:
        rw, rh, left, top, crop = image_geometry(*image.size, model_height, model_width, mode)
    mask = np.zeros((model_height, model_width), bool)
    mask[top:top+min(rh,518), left:left+rw] = True
    return mask


def photo_uv(path, pixel_ids, model_height, model_width, max_texture_size=2048, mode='crop'):
    """Invert the bundled official loader's width-518 resize/center crop/padding."""
    from PIL import Image
    with Image.open(path) as original:
        if original.mode == 'RGBA':
            original = Image.alpha_composite(Image.new('RGBA', original.size, (255,255,255,255)), original)
        image = original.convert('RGB')
    width, height = image.size
    resized_width, resized_height, pad_left, pad_top, crop_top = image_geometry(width,height,model_height,model_width,mode)
    uv = np.column_stack(((pixel_ids % model_width - pad_left + .5)/resized_width,
        1 - (pixel_ids // model_width - pad_top + crop_top + .5)/resized_height))
    image.thumbnail((max_texture_size, max_texture_size), Image.Resampling.LANCZOS)
    return uv, image


def build_photo_mesh(points, colors, confidence, percentile, max_faces, output,
                     image_mask, depth, source_images, preprocess_mode='crop',
                     prepared_valid=None, texture_size=2048):
    import trimesh
    n, h, w, _ = points.shape
    # A surface needs neighboring vertices. Use a gentler threshold than points.
    surface_percentile = percentile * .25 if prepared_valid is None else percentile
    valid = (confidence_mask(points, confidence, image_mask, surface_percentile)
             if prepared_valid is None else prepared_valid.copy())
    valid &= np.isfinite(depth) & (depth > 0)
    stride = 1
    while n*2*math.ceil((h-1)/stride)*math.ceil((w-1)/stride) > max_faces:
        stride += 1
        if stride > max(h,w):
            raise ValueError('Mesh 面數上限不足，請提高上限或減少影像。')
    scene = trimesh.Scene()
    vertex_count = face_count = repaired_count = 0
    texture_sizes = []
    for i in range(n):
        if prepared_valid is None:
            local_points, local_depth, local_valid, repaired = repair_small_holes(
                points[i], depth[i], valid[i], image_mask[i])
        else:
            local_points, local_depth, local_valid, repaired = points[i], depth[i], valid[i], 0
        faces = grid_faces(local_points, local_valid, local_depth, stride,strict_domain=image_mask[i])
        if not len(faces):
            continue
        used, inverse = np.unique(faces, return_inverse=True)
        vertices = local_points.reshape(-1,3)[used].astype(float)
        vertices[:,1:] *= -1
        faces = inverse.reshape(-1,3)
        uv, image = photo_uv(source_images[i], used, h, w, max_texture_size=texture_size, mode=preprocess_mode)
        texture_sizes.append(list(image.size))
        material = trimesh.visual.material.PBRMaterial(name=f'Photo {i+1}',
            baseColorTexture=image, baseColorFactor=[255,255,255,255],
            metallicFactor=0., roughnessFactor=1., doubleSided=True)
        tri = vertices[faces]
        normals = np.zeros_like(vertices)
        face_normals = np.cross(tri[:,1]-tri[:,0], tri[:,2]-tri[:,0])
        for corner in range(3):
            np.add.at(normals, faces[:,corner], face_normals)
        normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-15)
        mesh = trimesh.Trimesh(vertices=vertices, faces=faces, vertex_normals=normals,
            visual=trimesh.visual.texture.TextureVisuals(uv=uv, material=material), process=False)
        scene.add_geometry(mesh, node_name=f'view_{i:03d}', geom_name=f'view_{i:03d}')
        vertex_count += len(vertices)
        face_count += len(faces)
        repaired_count += repaired
    if not face_count:
        raise RuntimeError('無法建立 Mesh：沒有足夠連續的有效表面。')
    scene.export(output, file_type='glb')
    print(f'Photo Mesh: confidence percentile {surface_percentile:g}; repaired {repaired_count} pixels; textures {texture_sizes}', flush=True)
    return vertex_count, face_count, stride
