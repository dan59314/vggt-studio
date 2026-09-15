import tempfile
import unittest
from pathlib import Path
import numpy as np
from PIL import Image
import trimesh
from photo_mesh import repair_small_holes, photo_uv, build_photo_mesh, content_mask
from viewer_export import texture_mesh


class PhotoMeshTests(unittest.TestCase):
    def plane(self):
        y, x = np.mgrid[:40,:40]
        return np.stack([x*.01,y*.01,np.ones_like(x)],-1).astype(float)

    def test_enclosed_hole_repaired_without_changing_inputs(self):
        points = self.plane()
        valid = np.ones((40,40),bool); valid[15:20,15:20] = False
        repaired, depth, mask, count = repair_small_holes(points, points[...,2], valid, np.ones_like(valid))
        self.assertEqual(count,25)
        self.assertTrue(mask.all())
        self.assertFalse(valid[17,17])
        np.testing.assert_allclose(repaired,points,atol=.0001)

    def test_depth_edge_and_padding_not_filled(self):
        points = self.plane(); points[:,20:,2] = 2
        valid = np.ones((40,40),bool); valid[15:20,18:23] = False
        self.assertEqual(repair_small_holes(points,points[...,2],valid,np.ones_like(valid))[3],0)
        points = self.plane()
        self.assertEqual(repair_small_holes(points,points[...,2],valid,valid)[3],0)

    def test_uv_matches_portrait_crop_and_mixed_batch_padding(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'portrait.png'
            Image.new('RGB',(600,1200)).save(path)
            uv, image=photo_uv(path,np.array([0,517*518+517]),518,518)
            np.testing.assert_allclose(uv[:,1],[1-259.5/1036,1-776.5/1036])
            path=Path(tmp)/'landscape.png'
            Image.new('RGB',(1200,600)).save(path)
            # Rounded loader height = 252; centered padding = 133.
            uv,_=photo_uv(path,np.array([133*518]),518,518)
            self.assertAlmostEqual(uv[0,1],1-.5/252)

    def test_glb_keeps_full_photo_and_all_views(self):
        with tempfile.TemporaryDirectory() as tmp:
            source=Path(tmp)/'photo.png'
            Image.new('RGB',(1600,1200),(10,70,190)).save(source)
            points=np.repeat(self.plane()[None],2,axis=0)
            output=Path(tmp)/'scene.glb'
            _, faces,_=build_photo_mesh(points,np.zeros_like(points),np.ones(points.shape[:-1]),20,2000,
                output,np.ones(points.shape[:-1],bool),points[...,2],[source,source])
            scene=trimesh.load(output)
            self.assertEqual(len(scene.geometry),2)
            self.assertLessEqual(faces,2000)
            self.assertTrue(all(m.visual.material.baseColorTexture.size==(1600,1200) for m in scene.geometry.values()))
            target=Path(tmp)/'viewer.glb'
            texture_mesh(output,target)
            self.assertEqual(output.read_bytes(),target.read_bytes())

    def test_pad_preserves_whole_portrait_and_excludes_padding(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'portrait.png'
            Image.new('RGB',(1108,1477)).save(path)
            mask=content_mask(path,518,518,'pad')
            self.assertEqual(mask.sum(),392*518)
            self.assertFalse(mask[:,:63].any())
            self.assertTrue(mask[:,63:455].all())
            uv,_=photo_uv(path,np.array([63,517*518+454]),518,518,mode='pad')
            np.testing.assert_allclose(uv,[[.5/392,1-.5/518],[391.5/392,.5/518]])


if __name__=='__main__':
    unittest.main()
