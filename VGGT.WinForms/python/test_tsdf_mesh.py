import tempfile,unittest
from pathlib import Path
import numpy as np
from tsdf_mesh import build_tsdf_mesh,multiview_consistency


class TsdfTests(unittest.TestCase):
    def test_inconsistent_depth_is_removed(self):
        h=w=16;depth=np.ones((2,h,w),np.float32)*2;valid=np.ones((2,h,w),bool)
        depth[0,8,8]=1
        intrinsic=np.repeat(np.array([[[20.,0,8],[0,20.,8],[0,0,1]]]),2,axis=0)
        extrinsic=np.repeat(np.c_[np.eye(3),np.zeros(3)][None],2,axis=0)
        kept=multiview_consistency(depth,intrinsic,extrinsic,valid,tolerance=.03)
        self.assertFalse(kept[0,8,8]);self.assertTrue(kept[0,7,7])

    def test_two_views_fuse_into_single_glb(self):
        h=w=48; depth=np.ones((2,h,w),np.float32)*2
        colors=np.zeros((2,h,w,3),np.uint8);colors[...,0]=220;colors[...,1]=80
        valid=np.zeros((2,h,w),bool);valid[:,8:40,8:40]=True
        intrinsic=np.repeat(np.array([[[45.,0,24],[0,45.,24],[0,0,1]]]),2,axis=0)
        extrinsic=np.repeat(np.c_[np.eye(3),np.zeros(3)][None],2,axis=0);extrinsic[1,0,3]=.04
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'fused.glb'
            vertices,faces,_=build_tsdf_mesh(depth,colors,intrinsic,extrinsic,valid,20000,path,resolution=64)
            self.assertTrue(path.is_file());self.assertGreater(vertices,20);self.assertGreater(faces,20)


if __name__=='__main__':unittest.main()
