"""Geometry filtering independent of the GPU/model, for reproducible validation."""
import numpy as np


def confidence_mask(points, confidence, image_mask, percentile):
    valid = image_mask & np.isfinite(points).all(axis=-1) & np.isfinite(confidence)
    # Per-view thresholds prevent one lower-confidence view disappearing entirely.
    for i in range(len(valid)):
        if valid[i].any():
            threshold = np.percentile(confidence[i][valid[i]], percentile)
            valid[i] &= confidence[i] >= threshold
    return valid


def spatial_sample(points, colors, limit):
    """Voxel centroids/colors cover space evenly and fuse nearby observations."""
    if len(points) <= limit:
        return points, colors
    origin = points.min(axis=0)
    extent = np.ptp(points, axis=0).max()
    low, high = 0., max(float(extent), 1e-9) * 2
    for _ in range(12):
        size = (low + high) / 2
        cells = np.floor((points - origin) / size).astype(np.int64)
        if len(np.unique(cells, axis=0)) > limit:
            low = size
        else:
            high = size
    cells = np.floor((points - origin) / high).astype(np.int64)
    _, inverse = np.unique(cells, axis=0, return_inverse=True)
    counts = np.bincount(inverse)
    xyz = np.column_stack([np.bincount(inverse, weights=points[:, k]) / counts for k in range(3)])
    rgb = np.column_stack([np.bincount(inverse, weights=colors[:, k]) / counts for k in range(colors.shape[1])])
    return xyz, np.clip(np.rint(rgb), 0, 255).astype(np.uint8)


def grid_faces(points, valid, depth, stride, strict_domain=None):
    """Keep local surfaces, rejecting depth jumps relative to camera distance."""
    h, w = valid.shape
    ys = np.unique(np.r_[np.arange(0, h, stride), h - 1])
    xs = np.unique(np.r_[np.arange(0, w, stride), w - 1])
    ids = np.arange(h * w).reshape(h, w)[np.ix_(ys, xs)]
    a, b, c, d = ids[:-1, :-1], ids[:-1, 1:], ids[1:, :-1], ids[1:, 1:]
    faces = np.concatenate([np.stack([a, c, d], -1).reshape(-1, 3),
                            np.stack([a, d, b], -1).reshape(-1, 3)])
    faces = faces[valid.ravel()[faces].all(axis=1)]
    if not len(faces):
        return faces
    z = depth.reshape(-1)[faces]
    good = (z.min(axis=1) > 0) & ((z.max(axis=1) - z.min(axis=1)) <= .08 * z.min(axis=1))
    # Do not bridge invalid pixels skipped by coarse sampling.
    bad = (~valid).astype(np.int32)
    integral = np.pad(bad, ((1, 0), (1, 0))).cumsum(0).cumsum(1)
    yy, xx = faces // w, faces % w
    y0, y1, x0, x1 = yy.min(1), yy.max(1) + 1, xx.min(1), xx.max(1) + 1
    missing = integral[y1, x1] - integral[y0, x1] - integral[y1, x0] + integral[y0, x0]
    # Allow isolated rejected pixels, but never cross a broad masked region.
    good &= missing <= np.maximum(1, (y1-y0)*(x1-x0) * .1)
    if strict_domain is not None:
        excluded = np.pad((~strict_domain).astype(np.int32), ((1,0),(1,0))).cumsum(0).cumsum(1)
        good &= (excluded[y1,x1]-excluded[y0,x1]-excluded[y1,x0]+excluded[y0,x0]) == 0
    triangles = points.reshape(-1, 3)[faces]
    good &= np.linalg.norm(np.cross(triangles[:, 1]-triangles[:, 0], triangles[:, 2]-triangles[:, 0]), axis=1) > 1e-12
    return faces[good]
