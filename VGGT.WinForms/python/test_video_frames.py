import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import numpy as np
from video_frames import select_frames, extract
from viewer_export import export_viewer


class VideoTests(unittest.TestCase):
    def test_cap_spans_segment(self):
        selected = select_frames([i/10 for i in range(100)], 0, 10, .1, 4)
        self.assertEqual(len(selected), 4)
        self.assertAlmostEqual(selected[-1][1], 9.9)

    def test_invalid_settings(self):
        for args in [(3, 2, 1, 4), (0, 121, 1, 4), (0, 10, 0, 4), (0, 10, 1, 1)]:
            with self.assertRaises(ValueError):
                select_frames([0, 1, 2], *args)

    def test_selected_video_times_survive_camera_export(self):
        with tempfile.TemporaryDirectory() as tmp:
            e = np.c_[np.eye(3), np.zeros(3)]
            export_viewer(tmp, [e]*3, [np.diag([500.,500.,1.])]*3, 500,
                          ['a.png','b.png','c.png'], times=[5., 6.3, 9.8])
            data = json.loads((Path(tmp)/'scene.camera-animation.json').read_text())
            np.testing.assert_allclose([f['timeSeconds'] for f in data['keyframes']], [0,1.3,4.8])
            self.assertAlmostEqual(data['durationSeconds'], 4.8)

    @unittest.skipUnless(shutil.which('ffmpeg'), 'FFmpeg unavailable')
    def test_variable_frame_rate_decode(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp)/'含空格 video.mp4'
            subprocess.run([shutil.which('ffmpeg'), '-hide_banner', '-loglevel', 'error',
                '-f','lavfi','-i','testsrc2=size=96x64:rate=10:duration=4',
                '-vf','select=if(lt(t\\,2)\\,1\\,not(mod(n\\,2)))', '-vsync','vfr',
                '-c:v','libx264','-pix_fmt','yuv420p',str(source)], check=True, timeout=30)
            result = extract(source, Path(tmp)/'影格', 0, 4, .6, 4)
            self.assertEqual(len(result['frames']), 4)
            np.testing.assert_allclose([f['timeSeconds'] for f in result['frames']], [0,1.3,2.6,3.8])
            self.assertTrue(all(Path(f['path']).is_file() for f in result['frames']))


if __name__ == '__main__':
    unittest.main()
