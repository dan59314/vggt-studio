import tempfile
import unittest
from pathlib import Path
import numpy as np
from PIL import Image
from subject_masks import segment, propagate, aligned_masks, refine
from process_cached import process_arrays
from geometry_cleanup import grid_faces


class SubjectTests(unittest.TestCase):
    def test_refinement_honors_keep_and_remove_and_reduces_background(self):
        image=np.full((100,140,3),[20,40,150],np.uint8)
        image[25:75,35:85]=[240,100,30]
        initial=np.zeros((100,140),bool);initial[10:90,10:120]=True
        hints=np.zeros_like(image);hints[40:50,45:55]=255;hints[15:20,20:100]=[255,0,0]
        original=initial.copy()
        result=refine(image,initial,hints)
        self.assertTrue(result[40:50,45:55].all())
        self.assertFalse(result[15:20,20:100].any())
        self.assertLess(result.sum(),initial.sum())
        np.testing.assert_array_equal(initial,original)
        with self.assertRaisesRegex(ValueError,'提示'):
            refine(image,initial,np.zeros_like(image))

    def test_segmentation_and_moving_subject(self):
        rng=np.random.default_rng(5)
        image=np.full((100,140,3),30,np.uint8)
        image[25:75,35:85]=rng.integers(130,250,(50,50,3),dtype=np.uint8)
        mask=segment(image,[.18,.15,.5,.7])
        self.assertGreater(mask[25:75,35:85].mean(),.95)
        self.assertFalse(mask[:10].any())
        shifted=np.roll(image,4,axis=1)
        moved=propagate(image,shifted,mask)
        target=np.roll(mask,4,axis=1)
        self.assertGreater((moved&target).sum()/(moved|target).sum(),.90)

    def test_alignment_padding_crop_missing_and_quick_subset(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);source=root/'portrait.png';maskpath=root/'mask.png'
            Image.new('RGB',(300,600)).save(source)
            mask=np.zeros((600,300),np.uint8);mask[:300]=255;Image.fromarray(mask).save(maskpath)
            mapping={str(source):str(maskpath),'unused-frame':'missing.png'}
            aligned=aligned_masks([source],mapping,518,518,'pad')[0]
            self.assertTrue(aligned[100,259]);self.assertFalse(aligned[400,259]);self.assertFalse(aligned[:,0].any())
            cropped=aligned_masks([source],mapping,518,518,'crop')[0]
            self.assertTrue(cropped[100,200]);self.assertFalse(cropped[400,200])
            with self.assertRaisesRegex(ValueError,'缺少'):aligned_masks([source],{},518,518,'pad')

    def test_subject_exclusion_is_never_filled_or_bridged(self):
        depth=np.ones((1,20,20,1),np.float32)
        data=dict(depth=depth,depth_conf=np.ones((1,20,20)),image_valid_mask=np.ones((1,20,20),bool),
            intrinsic=np.array([[[100,0,10],[0,100,10],[0,0,1]]]),extrinsic=np.array([np.c_[np.eye(3),np.zeros(3)]]))
        subject=np.ones((1,20,20),bool);subject[0,9,9]=False;subject[:,:,15:]=False
        settings=dict(near=0,far=10,smooth=2,confidence=0,components=0,holeArea=100)
        points,z,valid,repaired=process_arrays(data,settings,subject)
        np.testing.assert_array_equal(valid,subject);self.assertFalse(repaired.any())
        faces=grid_faces(points[0],valid[0],z[0],4,strict_domain=subject[0])
        self.assertGreater(len(faces),0)
        for face in faces:
            yy,xx=face//20,face%20
            self.assertTrue(subject[0,yy.min():yy.max()+1,xx.min():xx.max()+1].all())
        self.assertTrue(data['image_valid_mask'].all())


if __name__=='__main__':unittest.main()
