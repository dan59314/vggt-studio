"""Read-only depth distribution and aligned per-camera samples for interactive preview."""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image


def inspect(data, colors, subject=None, limit=40000):
    depth=data['depth'][...,0]
    domain=data['image_valid_mask'] & np.isfinite(depth) & (depth>0)
    if subject is not None:
        domain &= subject
    ids=np.flatnonzero(domain)
    if not len(ids):
        raise ValueError('沒有有效深度可預覽，請檢查主體遮罩。')
    values=depth.ravel()[ids]
    maximum=float(values.max())
    counts,edges=np.histogram(np.log1p(values),bins=64,range=(0,np.log1p(maximum)))
    selected=ids[np.linspace(0,len(ids)-1,min(limit,len(ids)),dtype=int)]
    n,h,w=depth.shape
    frames=selected//(h*w);pixels=selected%(h*w);ys=pixels//w;xs=pixels%w
    z=depth.ravel()[selected]
    xyz=np.empty((len(selected),3),np.float32)
    for frame in np.unique(frames):
        keep=frames==frame;k=data['intrinsic'][frame];e=data['extrinsic'][frame]
        camera=np.column_stack([(xs[keep]-k[0,2])*z[keep]/k[0,0],(ys[keep]-k[1,2])*z[keep]/k[1,1],z[keep]])
        xyz[keep]=(camera-e[:,3])@e[:,:3]
    finite=np.isfinite(xyz).all(1)
    xyz[:,1]*=-1
    samples=np.column_stack([xyz,z,colors.reshape(-1,3)[selected]])[finite]
    return dict(minimum=float(values.min()),maximum=maximum,total=len(ids),counts=counts.tolist(),
        edges=np.expm1(edges).tolist(),samples=samples.tolist(),subject=subject is not None)


def run(cache,settings,output):
    cache=Path(cache);manifest=json.loads((cache/'result.json').read_text(encoding='utf-8'))
    with np.load(cache/manifest['denseArrayPath']) as data:
        colors=np.stack([np.asarray(Image.open(cache/p['imagePath']).convert('RGB')) for p in manifest['imageDepthPairs']])
        subject=None
        if settings.get('subjectMasks') is not None:
            from subject_masks import aligned_masks
            subject=aligned_masks([c['image'] for c in manifest['cameras']],settings['subjectMasks'],
                data['depth'].shape[1],data['depth'].shape[2],manifest.get('preprocessMode','crop'))
        result=inspect(data,colors,subject)
    Path(output).write_text(json.dumps(result,allow_nan=False),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--cache',required=True);parser.add_argument('--settings',required=True);parser.add_argument('--output',required=True)
    args=parser.parse_args()
    run(args.cache,json.loads(Path(args.settings).read_text(encoding='utf-8-sig')),args.output)
