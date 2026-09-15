import json
from pathlib import Path
import struct
import tempfile
import unittest

import numpy as np
import trimesh
from geometry_cleanup import confidence_mask, spatial_sample, grid_faces
from vggt_runner import build_grid_mesh


class GeometryTests(unittest.TestCase):
    def plane(self, n=1, h=32, w=32):
        y, x = np.mgrid[:h, :w]
        points = np.stack([x * .01, y * .01, np.ones_like(x)], -1)
        return np.repeat(points[None], n, axis=0).astype(float)

    def test_mask_preserves_black_and_lower_confidence_view(self):
        points = self.plane(2)
        conf = np.ones(points.shape[:-1])
        conf[1] *= .01
        mask = np.ones_like(conf, dtype=bool)
        mask[:, :3] = False
        valid = confidence_mask(points, conf, mask, 20)
        self.assertFalse(valid[:, :3].any())
        self.assertTrue(valid[:, 3:].all())
        xyz, rgb = spatial_sample(points[valid], np.zeros((valid.sum(), 3)), 100)
        self.assertLessEqual(len(xyz), 100)
        self.assertTrue((rgb == 0).all())
        self.assertGreater(np.ptp(xyz[:, 0]), .25)

    def test_depth_discontinuity_is_not_bridged(self):
        points = self.plane()[0]
        points[:, 16:, 2] = 3
        faces = grid_faces(points, np.ones((32, 32), bool), points[..., 2], 2)
        z = points.reshape(-1, 3)[faces, 2]
        self.assertTrue((np.ptp(z, axis=1) == 0).all())

    def test_budget_preserves_connected_plane_and_glb_colors(self):
        points = self.plane()
        conf = np.ones(points.shape[:-1])
        colors = np.zeros(points.shape, np.uint8)
        colors[..., 0] = 180
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'plane.glb'
            _, count, stride = build_grid_mesh(points, colors, conf, 20, 120, path,
                                              depth=points[..., 2])
            self.assertLessEqual(count, 120)
            self.assertGreater(stride, 1)
            scene = trimesh.load(path)
            mesh = next(iter(scene.geometry.values()))
            # Shared-edge face connectivity, using networkx (no scipy dependency).
            import networkx as nx
            graph = nx.Graph()
            graph.add_nodes_from(range(len(mesh.faces)))
            graph.add_edges_from(mesh.face_adjacency)
            self.assertTrue(nx.is_connected(graph))
            loaded_colors = mesh.visual.vertex_attributes['color']
            self.assertTrue((loaded_colors[:, 0] == 180).all())
            raw = path.read_bytes()
            length = struct.unpack_from('<I', raw, 12)[0]
            tree = json.loads(raw[20:20+length])
            self.assertTrue(tree['materials'][-1]['doubleSided'])

    def test_identical_views_are_welded(self):
        points = self.plane(2, 8, 8)
        with tempfile.TemporaryDirectory() as tmp:
            _, faces, _ = build_grid_mesh(points, np.full(points.shape, 100, np.uint8),
                np.ones(points.shape[:-1]), 0, 1000, Path(tmp) / 'fused.glb', depth=points[..., 2])
            self.assertEqual(faces, 2 * 7 * 7)

    def test_invalid_scene_has_clear_error(self):
        points = self.plane()
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(RuntimeError, 'Mesh'):
                build_grid_mesh(points, np.zeros(points.shape, np.uint8),
                    np.ones(points.shape[:-1]), 20, 1000, Path(tmp) / 'empty.glb',
                    image_mask=np.zeros(points.shape[:-1], bool), depth=points[..., 2])


if __name__ == '__main__':
    unittest.main()
