import unittest
import numpy as np
from process_cached import process_arrays


class CachedTests(unittest.TestCase):
    def setup_data(self):
        depth=np.ones((1,20,20,1),np.float32)
        return dict(depth=depth,depth_conf=np.ones((1,20,20)),image_valid_mask=np.ones((1,20,20),bool),
                    intrinsic=np.array([[[100,0,10],[0,100,10],[0,0,1]]]),extrinsic=np.array([np.c_[np.eye(3),np.zeros(3)]]))
    def settings(self):
        return dict(near=0,far=10,smooth=0,confidence=0,components=0,holeArea=0)
    def test_reconstruction_and_immutable_cache(self):
        data=self.setup_data();original=data['depth'].copy()
        points,depth,valid,repaired=process_arrays(data,self.settings())
        np.testing.assert_allclose(points[0,10,10],[0,0,1])
        np.testing.assert_array_equal(data['depth'],original)
        self.assertTrue(valid.all());self.assertFalse(repaired.any())
    def test_depth_exclusion_has_clear_error(self):
        settings=self.settings();settings['near']=2
        with self.assertRaisesRegex(ValueError,'所有點'):process_arrays(self.setup_data(),settings)
    def test_repair_is_visible_and_cache_not_modified(self):
        data=self.setup_data();data['depth_conf'][0,8:11,8:11]=0
        settings=self.settings();settings['confidence']=10;settings['holeArea']=16
        _,_,valid,repaired=process_arrays(data,settings)
        self.assertTrue(valid.all());self.assertEqual(repaired.sum(),9)
        self.assertEqual(data['depth_conf'][0,9,9],0)


if __name__=='__main__':unittest.main()
