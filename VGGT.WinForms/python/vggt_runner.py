"""VGGT WinForms bridge. The official VGGT repository remains the inference implementation."""
from __future__ import annotations

import argparse
import json
import math
import sys
import time
from pathlib import Path


def progress(value: int, message: str) -> None:
    print(f"PROGRESS|{value}|{message}", flush=True)


def nested_list(a):
    return a.astype("float32").tolist()


def write_ascii_ply(path: Path, xyz, rgb) -> None:
    with path.open("w", encoding="ascii", newline="\n") as f:
        f.write("ply\nformat ascii 1.0\n")
        f.write(f"element vertex {len(xyz)}\n")
        f.write("property float x\nproperty float y\nproperty float z\n")
        f.write("property uchar red\nproperty uchar green\nproperty uchar blue\nend_header\n")
        for p, c in zip(xyz, rgb):
            f.write(f"{p[0]:.7g} {p[1]:.7g} {p[2]:.7g} {int(c[0])} {int(c[1])} {int(c[2])}\n")


def depth_preview(depth, path: Path) -> None:
    import numpy as np
    from PIL import Image
    d = depth.squeeze().astype("float32")
    valid = np.isfinite(d) & (d > 0)
    out = np.zeros(d.shape, dtype=np.uint8)
    if valid.any():
        lo, hi = np.percentile(d[valid], [2, 98])
        n = np.clip((d - lo) / max(hi - lo, 1e-8), 0, 1)
        out[valid] = (255 * (1 - n[valid])).astype(np.uint8)
    Image.fromarray(out, mode="L").save(path)


def build_grid_mesh(world_points, colors, confidence, confidence_percentile: float,
                    max_faces: int, output_path: Path, image_mask=None, depth=None, source_images=None,
                    preprocess_mode='crop') -> tuple[int, int, int]:
    """Coarsen whole grids to budget; never delete a subset of surface triangles."""
    import numpy as np
    import trimesh
    from geometry_cleanup import confidence_mask, grid_faces

    n, h, w, _ = world_points.shape
    if image_mask is None:
        image_mask = np.ones(confidence.shape, dtype=bool)
    if depth is None:
        raise ValueError("Camera-space depth is required for mesh boundary filtering")
    if source_images is not None:
        from photo_mesh import build_photo_mesh
        return build_photo_mesh(world_points, colors, confidence, confidence_percentile,
            max_faces, output_path, image_mask, depth, source_images, preprocess_mode)
    valid = confidence_mask(world_points, confidence, image_mask, confidence_percentile)
    valid &= np.isfinite(depth) & (depth > 0)
    stride = 1
    while n * 2 * math.ceil((h - 1) / stride) * math.ceil((w - 1) / stride) > max_faces:
        stride += 1
        if stride > max(h, w):
            raise ValueError("Mesh face budget is too small for the number of images")
    vertices, vertex_colors, faces = [], [], []
    offset = 0
    for i in range(n):
        local_faces = grid_faces(world_points[i], valid[i], depth[i], stride)
        if not len(local_faces):
            continue
        used, inverse = np.unique(local_faces, return_inverse=True)
        vertices.append(world_points[i].reshape(-1, 3)[used])
        vertex_colors.append(colors[i].reshape(-1, 3)[used])
        faces.append(inverse.reshape(-1, 3) + offset)
        offset += len(used)
    if not faces:
        raise RuntimeError("無法建立 Mesh：有效且連續的深度網格不足。請降低信心門檻或增加影像。")
    vertices = np.concatenate(vertices).astype(np.float64)
    vertex_colors = np.concatenate(vertex_colors)
    faces = np.concatenate(faces)

    # Conservative vertex welding joins almost coincident samples from views.
    # It is not volumetric fusion: separated/misaligned surfaces remain separate.
    edges = np.linalg.norm(vertices[faces[:, 0]] - vertices[faces[:, 1]], axis=1)
    tolerance = max(float(np.median(edges)) * .02, 1e-10)
    cells = np.floor((vertices - vertices.min(0)) / tolerance).astype(np.int64)
    _, inverse = np.unique(cells, axis=0, return_inverse=True)
    counts = np.bincount(inverse)
    vertices = np.column_stack([np.bincount(inverse, weights=vertices[:, k]) / counts for k in range(3)])
    vertex_colors = np.column_stack([np.bincount(inverse, weights=vertex_colors[:, k]) / counts for k in range(3)]).astype(np.uint8)
    faces = inverse[faces]
    vertices[:, 1:] *= -1  # OpenCV -> glTF
    rgba = np.column_stack([vertex_colors, np.full(len(vertex_colors), 255, dtype=np.uint8)])
    mesh = trimesh.Trimesh(vertices=vertices, faces=faces, vertex_colors=rgba, process=False)
    mesh.update_faces(mesh.nondegenerate_faces())
    mesh.update_faces(mesh.unique_faces())
    mesh.remove_unreferenced_vertices()
    # Only repair triangular/quadrilateral holes. Keep the user's face budget.
    repaired = mesh.copy()
    trimesh.repair.fill_holes(repaired)
    # A three/four-edge boundary can also be a large intentional opening.
    # Only accept newly created faces whose edges are locally small.
    added = repaired.faces[len(mesh.faces):]
    if len(added):
        tri = repaired.vertices[added]
        lengths = np.linalg.norm(tri - np.roll(tri, 1, axis=1), axis=2)
        keep = lengths.max(axis=1) <= np.median(edges) * 2
        repaired.update_faces(np.r_[np.ones(len(mesh.faces), dtype=bool), keep])
    if len(repaired.faces) <= max_faces:
        mesh = repaired
    mesh.update_faces(mesh.nondegenerate_faces())
    mesh.update_faces(mesh.unique_faces())
    mesh.remove_unreferenced_vertices()
    # Photographic surfaces are thin; show their back sides as well.
    def double_sided(tree):
        materials = tree.setdefault("materials", [])
        materials.append({"doubleSided": True, "pbrMetallicRoughness": {
            "baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0, "roughnessFactor": 1}})
        for item in tree.get("meshes", []):
            for primitive in item["primitives"]:
                primitive["material"] = len(materials) - 1
    output_path.write_bytes(trimesh.exchange.gltf.export_glb(
        trimesh.Scene(mesh), tree_postprocessor=double_sided))
    print(f"Mesh cleanup: weld tolerance {tolerance:.6g}; budget-preserving grid sampling", flush=True)
    return len(mesh.vertices), len(mesh.faces), stride


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--images-file", required=True)
    ap.add_argument("--output", required=True)
    ap.add_argument("--model", default="facebook/VGGT-1B")
    ap.add_argument("--confidence-percentile", type=float, default=20)
    ap.add_argument("--max-points", type=int, default=150000)
    ap.add_argument("--max-mesh-faces", type=int, default=400000)
    ap.add_argument("--query-points")
    ap.add_argument("--timeline")
    ap.add_argument("--image-mode", choices=['pad', 'crop'], default='pad')
    ap.add_argument("--save-dense", action="store_true")
    ap.add_argument("--depth-camera-points", action="store_true")
    ap.add_argument("--generate-mesh", action="store_true")
    args = ap.parse_args()

    repo = Path(args.repo).resolve()
    sys.path.insert(0, str(repo))
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    image_paths = [x.strip() for x in Path(args.images_file).read_text(encoding="utf-8-sig").splitlines() if x.strip()]
    if not image_paths:
        raise ValueError("No input images")
    timeline = json.loads(Path(args.timeline).read_text(encoding="utf-8-sig")) if args.timeline else None
    if timeline is not None:
        times = timeline["times"]
        if len(times) != len(image_paths) or not all(math.isfinite(t) for t in times) or any(b <= a for a, b in zip(times, times[1:])):
            raise ValueError("影片時間與影像數量不符或不是遞增時間。")
        (out / "video_timeline.json").write_text(json.dumps(timeline, ensure_ascii=False, indent=2), encoding="utf-8")

    progress(4, "載入 PyTorch 與 VGGT")
    import numpy as np
    import torch
    from vggt.models.vggt import VGGT
    from vggt.utils.load_fn import load_and_preprocess_images
    from vggt.utils.pose_enc import pose_encoding_to_extri_intri
    from vggt.utils.geometry import unproject_depth_map_to_point_map

    if not torch.cuda.is_available():
        raise RuntimeError("VGGT-1B 實務上需要 CUDA GPU；目前 PyTorch 未偵測到 CUDA。")
    device = torch.device("cuda")
    major = torch.cuda.get_device_capability()[0]
    dtype = torch.bfloat16 if major >= 8 else torch.float16
    gpu_name = torch.cuda.get_device_name(0)
    print(f"GPU: {gpu_name}", flush=True)
    print(f"PyTorch: {torch.__version__} | CUDA: {torch.version.cuda} | dtype: {dtype}", flush=True)

    progress(12, f"載入模型 {args.model}")
    model = VGGT.from_pretrained(args.model).to(device).eval()
    progress(25, f"預處理 {len(image_paths)} 張影像")
    images = load_and_preprocess_images(image_paths, mode=args.image_mode).to(device)
    # Match the bundled loader's resize/pad geometry; never classify valid
    # black or white surfaces as padding from their pixel colors.
    from photo_mesh import content_mask
    image_mask = np.stack([content_mask(path, *images.shape[-2:], args.image_mode) for path in image_paths])
    query = None
    if args.query_points:
        query_np = np.loadtxt(args.query_points, delimiter=",", dtype=np.float32, ndmin=2)
        query = torch.from_numpy(query_np[:, :2]).to(device)

    progress(40, "執行 VGGT 前向推論")
    torch.cuda.empty_cache()
    torch.cuda.reset_peak_memory_stats()
    torch.cuda.synchronize()
    inference_started = time.perf_counter()
    with torch.inference_mode(), torch.amp.autocast("cuda", dtype=dtype):
        pred = model(images, query_points=query)
    torch.cuda.synchronize()
    inference_seconds = time.perf_counter() - inference_started
    peak_gpu_mb = torch.cuda.max_memory_allocated() / (1024 * 1024)
    print(f"GPU inference: {inference_seconds:.3f} s | peak allocated VRAM: {peak_gpu_mb:.0f} MiB", flush=True)
    extrinsic, intrinsic = pose_encoding_to_extri_intri(pred["pose_enc"], images.shape[-2:])

    def np0(value):
        return value.detach().float().cpu().numpy().squeeze(0)

    depth = np0(pred["depth"])
    depth_conf = np0(pred["depth_conf"])
    extrinsic_np = np0(extrinsic)
    intrinsic_np = np0(intrinsic)
    images_np = np0(pred["images"]).transpose(0, 2, 3, 1)
    images_np = np.clip(images_np * 255, 0, 255).astype(np.uint8)
    pointmap = np0(pred["world_points"])
    point_conf = np0(pred["world_points_conf"])
    world_from_depth = unproject_depth_map_to_point_map(depth, extrinsic_np, intrinsic_np)
    chosen = world_from_depth if args.depth_camera_points else pointmap
    chosen_conf = depth_conf if args.depth_camera_points else point_conf

    progress(67, "篩選點雲並產生 PLY")
    xyz = chosen.reshape(-1, 3)
    rgb = images_np.reshape(-1, 3)
    from geometry_cleanup import confidence_mask, spatial_sample
    image_mask &= np.isfinite(depth[..., 0]) & (depth[..., 0] > 0)
    valid = confidence_mask(chosen, chosen_conf, image_mask, args.confidence_percentile).reshape(-1)
    if not valid.any():
        raise RuntimeError("沒有有效點雲。請檢查影像或降低信心濾除比例。")
    sampled_xyz, sampled_rgb = spatial_sample(xyz[valid], rgb[valid], args.max_points)
    print(f"Point cleanup: {valid.sum()} valid samples -> {len(sampled_xyz)} spatial samples", flush=True)
    ply_path = out / "scene.ply"
    # VGGT uses OpenCV coordinates (x right, y down, z forward).  The custom
    # WinForms point renderer already views along OpenCV +Z, so only Y needs to
    # be changed to Y-up.  Flipping Z here (as required for glTF below) would
    # make the point cloud appear from behind and therefore left/right mirrored.
    ply_xyz = sampled_xyz.copy()
    ply_xyz[:, 1] *= -1
    write_ascii_ply(ply_path, ply_xyz, sampled_rgb)

    mesh_path = None
    mesh_stats = None
    if args.generate_mesh:
        progress(73, "建立三角 Mesh 並輸出 GLB")
        mesh_path = out / "scene_mesh.glb"
        mesh_stats = build_grid_mesh(chosen, images_np, chosen_conf,
                                     args.confidence_percentile, args.max_mesh_faces, mesh_path,
                                     image_mask=image_mask, depth=depth[..., 0], source_images=image_paths,
                                     preprocess_mode=args.image_mode)
        print(f"Mesh: {mesh_stats[0]} vertices, {mesh_stats[1]} faces, grid stride {mesh_stats[2]}", flush=True)

    progress(82, "輸出深度預覽與相機資料")
    from PIL import Image
    from viewer_export import export_viewer, paired_names
    pair_dir = out / "images_depth"
    pair_dir.mkdir(exist_ok=True)
    image_depth_pairs = []
    previews = []
    for i, (d, names) in enumerate(zip(depth, paired_names(image_paths))):
        image_name, depth_name = names
        Image.fromarray(images_np[i]).save(pair_dir / image_name)
        p = pair_dir / depth_name
        depth_preview(d, p)
        previews.append(f"images_depth/{depth_name}")
        image_depth_pairs.append(dict(imagePath=f"images_depth/{image_name}", depthPath=f"images_depth/{depth_name}"))
    project_name, animation_name = export_viewer(out, extrinsic_np, intrinsic_np,
        images_np.shape[1], image_paths, mesh_path.name if mesh_path else None,
        times=timeline["times"] if timeline else None)

    dense_path = None
    if args.save_dense:
        dense_path = out / "predictions.npz"
        payload = dict(depth=depth, depth_conf=depth_conf, extrinsic=extrinsic_np,
                       intrinsic=intrinsic_np, world_points=pointmap,
                       world_points_conf=point_conf, world_points_from_depth=world_from_depth, image_valid_mask=image_mask)
        if query is not None:
            payload.update(track=np0(pred["track"]), visibility=np0(pred["vis"]), track_conf=np0(pred["conf"]))
        np.savez_compressed(dense_path, **payload)

    result = {
        "plyPath": ply_path.name,
        "imageDepthPairs": image_depth_pairs,
        "viewerProjectPath": project_name,
        "cameraAnimationPath": animation_name,
        "videoTimeline": timeline,
        "preprocessMode": args.image_mode,
        "meshGlbPath": mesh_path.name if mesh_path else None,
        "denseArrayPath": dense_path.name if dense_path else None,
        "runtime": {"device": "cuda:0", "gpu": gpu_name, "pytorch": torch.__version__,
                    "cuda": torch.version.cuda, "dtype": str(dtype),
                    "inferenceSeconds": inference_seconds, "peakGpuMemoryMb": peak_gpu_mb},
        "depthPreviews": previews,
        "cameras": [{"index": i, "image": image_paths[i], "intrinsic": nested_list(intrinsic_np[i]),
                     "extrinsic": nested_list(extrinsic_np[i])} for i in range(len(image_paths))],
        "shapes": {"depth": list(depth.shape), "worldPoints": list(pointmap.shape),
                   **({"mesh": [mesh_stats[0], mesh_stats[1]]} if mesh_stats else {}),
                   **({"track": list(np0(pred["track"]).shape)} if query is not None else {})},
    }
    (out / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    progress(100, "完成")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        import traceback
        traceback.print_exc()
        raise SystemExit(1)
