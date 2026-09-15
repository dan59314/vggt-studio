import json
from pathlib import Path
import tempfile
import unittest
import numpy as np
import trimesh
from viewer_export import export_viewer, paired_names, texture_mesh


class ViewerExportTests(unittest.TestCase):
    def test_names_do_not_overwrite_images_or_depths(self):
        pairs = paired_names(['a.jpg', 'A.png', 'a_Depth.png', 'a_2.jpg'])
        names = [n.casefold() for pair in pairs for n in pair]
        self.assertEqual(len(set(names)), len(names))
        self.assertTrue(all(d == i[:-4] + '_Depth.png' for i, d in pairs))

    def test_camera_center_and_direction_follow_glb_coordinates(self):
        e = np.c_[np.eye(3), [-2., -3., -4.]]
        with tempfile.TemporaryDirectory() as tmp:
            project, path = export_viewer(tmp, [e], [np.diag([500., 500., 1.])], 500, ['a.jpg'])
            self.assertIsNone(project)
            frame = json.loads((Path(tmp) / path).read_text())['keyframes'][0]
            self.assertEqual(frame['from'], dict(x=2., y=-3., z=-4.))
            self.assertEqual(frame['to'], dict(x=2., y=-3., z=-5.))
            self.assertEqual(frame['up'], dict(x=0., y=1., z=0.))

    def test_texture_contains_original_corner_colors(self):
        rgb = np.array([[255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255]], np.uint8)
        mesh = trimesh.Trimesh(vertices=[[0, 0, 0], [1, 0, 0], [0, 1, 0]], faces=[[0, 1, 2]], vertex_colors=rgb, process=False)
        with tempfile.TemporaryDirectory() as tmp:
            source, target = Path(tmp)/'source.glb', Path(tmp)/'target.glb'
            mesh.export(source)
            texture_mesh(source, target)
            loaded = next(iter(trimesh.load(target).geometry.values()))
            self.assertEqual(len(loaded.faces), 1)
            self.assertTrue(loaded.visual.material.doubleSided)
            tex = np.asarray(loaded.visual.material.baseColorTexture)
            uv = loaded.visual.uv
            xx = np.rint(uv[:, 0]*tex.shape[1]-.5).astype(int)
            yy = np.rint((1-uv[:, 1])*tex.shape[0]-.5).astype(int)
            np.testing.assert_array_equal(tex[yy, xx, :3], rgb[:, :3])


if __name__ == '__main__':
    unittest.main()
