"""Validate subject-only GLB on existing six-view predictions without GPU inference."""
import hashlib
import json
import sys
import uuid
from pathlib import Path
import numpy as np
from PIL import Image
import trimesh

sys.path.insert(0,str(Path('VGGT.WinForms/python').resolve()))
from process_cached import run

cache=Path('validation/livingroom-photo-mesh').resolve()
result=json.loads((cache/'result.json').read_text(encoding='utf-8'))
dense=cache/result['denseArrayPath']
before=hashlib.sha256(dense.read_bytes()).hexdigest()
folder=Path('validation/SubjectSmoke')/uuid.uuid4().hex
folder.mkdir()
mapping={}
for i,camera in enumerate(result['cameras']):
    with Image.open(camera['image']) as image:
        mask=np.zeros((image.height,image.width),np.uint8)
        mask[image.height//4:3*image.height//4,image.width//4:3*image.width//4]=255
    path=(folder/f'mask{i}.png').resolve();Image.fromarray(mask).save(path)
    mapping[camera['image']]=str(path)
settings=dict(near=0,far=100000,smooth=0,confidence=0,components=0,holeArea=64,
    maxPoints=10000,maxFaces=50000,textureSize=512,subjectMasks=mapping)
run(cache,folder/'result',settings,mesh=True)
processed=json.loads((folder/'result/result.json').read_text(encoding='utf-8'))
assert processed['cameras']==result['cameras']
with np.load(dense) as data:
    assert 0<processed['processingStats']['validPoints']<data['image_valid_mask'].sum()*.4
scene=trimesh.load(folder/'result/scene_mesh.glb')
for geometry in scene.geometry.values():
    assert geometry.visual.uv.min()>.24 and geometry.visual.uv.max()<.76
assert before==hashlib.sha256(dense.read_bytes()).hexdigest()
assert (folder/'result'/processed['viewerProjectPath']).is_file()
print('PASS: six-view subject-only point cloud, textured GLB, Viewer export, unchanged cameras and immutable cache.',folder)
