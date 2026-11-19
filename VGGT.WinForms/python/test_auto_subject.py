import unittest
import numpy as np
from auto_subject import _center_score


class AutoSubjectTests(unittest.TestCase):
    def test_centered_object_beats_background_and_corner(self):
        center=np.zeros((100,100),bool); center[30:70,35:65]=True
        corner=np.zeros((100,100),bool); corner[0:40,0:30]=True
        background=np.ones((100,100),bool)
        self.assertGreater(_center_score(center,.9),_center_score(corner,.9))
        self.assertLess(_center_score(background,.99),0)

    def test_temporal_overlap_stabilizes_selection(self):
        previous=np.zeros((100,100),bool); previous[30:70,35:65]=True
        stable=np.zeros((100,100),bool); stable[31:71,36:66]=True
        distractor=np.zeros((100,100),bool); distractor[35:65,35:65]=True
        self.assertGreater(_center_score(stable,.85,previous),_center_score(distractor,.9,previous))


if __name__=='__main__': unittest.main()
