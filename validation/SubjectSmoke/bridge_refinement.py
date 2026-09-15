"""Exercise user-style positive/negative strokes on the existing bridge frame."""
import json
import os
import sys
from pathlib import Path
import cv2
import numpy as np
from PIL import Image
sys.path.insert(0,str(Path('VGGT.WinForms/python').resolve()))
from subject_masks import read_rgb,segment,refine
saved=json.loads((Path(os.environ['LOCALAPPDATA'])/'VGGT Studio/last-images.json').read_text())
image=read_rgb(saved['Images'][0])
h,w=image.shape[:2]
initial=segment(image,[.005,.16,.99,.81])
hints=np.zeros_like(image)
def line(a,b,color,width=5):
    cv2.line(hints,(round(a[0]*w),round(a[1]*h)),(round(b[0]*w),round(b[1]*h)),color,width)
line((.416,.25),(.416,.67),(255,255,255))
line((.456,.24),(.456,.88),(255,255,255))
line((.02,.744),(.95,.648),(255,255,255),3)
line((.03,.15),(.94,.15),(255,0,0),15)
line((.04,.47),(.39,.37),(255,0,0),9)
line((.48,.40),(.95,.38),(255,0,0),9)
line((.04,.65),(.39,.57),(255,0,0),9)
line((.48,.57),(.97,.55),(255,0,0),9)
line((.05,.85),(.37,.85),(255,0,0),15)
line((.51,.85),(.95,.85),(255,0,0),15)
result=refine(image,initial,hints)
root=Path('validation/SubjectSmoke')
panels=[]
for mask in (initial,result):
    overlay=image.copy()
    overlay[mask]=(overlay[mask]*.6+np.array([0,255,90])*.4).astype(np.uint8)
    panels.append(overlay)
Image.fromarray(np.concatenate(panels,axis=1)).save(root/'bridge-before-after.png')
print('Bridge selected pixels before/after:',int(initial.sum()),int(result.sum()))
assert result[(hints[...,0]==255)&(hints[...,1]==255)].all()
assert not result[(hints[...,0]==255)&(hints[...,1]==0)].any()
