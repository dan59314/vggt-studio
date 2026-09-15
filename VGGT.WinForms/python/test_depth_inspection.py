import unittest
import numpy as np
from depth_inspection import inspect

class DepthInspectionTests(unittest.TestCase):
    def test_depth_uses_source_camera_and_sample_colors(self):
        depth=np.ones((2,4,4,1),np.float32);depth[1]*=2
        data=dict(depth=depth,image_valid_mask=np.ones((2,4,4),bool),
            intrinsic=np.array([np.eye(3),np.eye(3)]),
            extrinsic=np.array([np.c_[np.eye(3),[0,0,0]],np.c_[np.eye(3),[0,0,10]]]))
        colors=np.zeros((2,4,4,3),np.uint8);colors[0]=[10,20,30];colors[1]=[40,50,60]
        subject=np.ones((2,4,4),bool);subject[0,0,0]=False
        result=inspect(data,colors,subject,limit=100)
        self.assertEqual(result['total'],31);self.assertEqual(sum(result['counts']),31)
        self.assertEqual(result['samples'][-1],[6.,-6.,-8.,2.,40.,50.,60.])
        self.assertTrue(data['image_valid_mask'].all());self.assertEqual(result['maximum'],2)
        self.assertEqual(len(inspect(data,colors,limit=5)['samples']),5)
        with self.assertRaisesRegex(ValueError,'沒有有效'):
            inspect(data,colors,np.zeros_like(subject))
    def test_invalid_depth_and_padding_do_not_enter_histogram(self):
        data=dict(depth=np.array([[[[1.],[2.]],[[np.nan],[-1.]]]],np.float32),
            image_valid_mask=np.array([[[True,False],[True,True]]]),
            intrinsic=np.array([np.eye(3)]),extrinsic=np.array([np.c_[np.eye(3),[0,0,0]]]))
        result=inspect(data,np.zeros((1,2,2,3),np.uint8))
        self.assertEqual(result['total'],1);self.assertEqual(sum(result['counts']),1)

if __name__=='__main__':unittest.main()
