"""Automatically select and track a centered object in an orbit capture."""
import argparse
import json
import os
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

from automatic_objects import CHECKPOINT_URL


def _center_score(mask, predicted_iou, previous=None):
    h, w = mask.shape
    area = float(mask.mean())
    if area < .008 or area > .55:
        return -1e9
    yy, xx = np.nonzero(mask)
    if not len(xx):
        return -1e9
    cx, cy = float(xx.mean()/w), float(yy.mean()/h)
    distance = ((cx-.5)**2 + (cy-.5)**2) ** .5
    center_patch = mask[max(0,h//2-h//20):min(h,h//2+h//20),
                        max(0,w//2-w//20):min(w,w//2+w//20)].mean()
    area_prior = np.exp(-abs(np.log(max(area,1e-6)/.12)))
    score = 2.2*predicted_iou + 1.8*center_patch + .8*area_prior - 2.0*distance
    if previous is not None:
        intersection = np.logical_and(mask, previous).sum()
        union = np.logical_or(mask, previous).sum()
        score += 3.0*intersection/max(1, union)
    return float(score)


def generate(images, output, max_side=1024):
    import torch
    from segment_anything import sam_model_registry, SamAutomaticMaskGenerator
    checkpoint = Path(os.environ.get('LOCALAPPDATA', Path.home()))/'VGGT Studio/models/sam_vit_b_01ec64.pth'
    checkpoint.parent.mkdir(parents=True, exist_ok=True)
    if not checkpoint.is_file():
        temporary = checkpoint.with_suffix('.download')
        torch.hub.download_url_to_file(CHECKPOINT_URL, str(temporary), progress=False)
        temporary.replace(checkpoint)
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model = sam_model_registry['vit_b'](checkpoint=str(checkpoint)).to(device).eval()
    generator = SamAutomaticMaskGenerator(model, points_per_side=32, points_per_batch=16,
        pred_iou_thresh=.84, stability_score_thresh=.90, crop_n_layers=0, min_mask_region_area=64)
    output = Path(output); output.mkdir(parents=True, exist_ok=True)
    previous = None; records = []
    for index, path in enumerate(images):
        with Image.open(path) as source:
            original_size = source.size
            source = source.convert('RGB'); source.thumbnail((max_side,max_side)); rgb = np.asarray(source)
        with torch.inference_mode():
            candidates = generator.generate(rgb)
        scored = [(_center_score(x['segmentation'], x['predicted_iou'], previous), x) for x in candidates]
        score, best = max(scored, key=lambda x:x[0]) if scored else (-1e9, None)
        if best is None or score < 0:
            raise RuntimeError(f'第 {index+1} 張影格找不到可信的中央主體。')
        mask = best['segmentation'].astype(np.uint8)
        # Close small segmentation gaps without growing across large background areas.
        kernel = np.ones((5,5),np.uint8)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        previous = mask.astype(bool)
        full = cv2.resize(mask, original_size, interpolation=cv2.INTER_NEAREST)
        name = f'{index:04d}.png'; Image.fromarray(full*255).save(output/name)
        records.append(dict(image=str(Path(path).resolve()), mask=str((output/name).resolve()),
                            score=score, areaFraction=float(mask.mean()), predictedIou=float(best['predicted_iou'])))
        print(f'自動主體 {index+1}/{len(images)}：面積 {mask.mean():.1%}，信心 {best["predicted_iou"]:.3f}', flush=True)
    manifest = dict(method='SAM ViT-B centered-orbit', device=device, masks=records)
    (output/'auto_subject.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--images-file',required=True); parser.add_argument('--output',required=True)
    args=parser.parse_args(); images=[x.strip() for x in Path(args.images_file).read_text(encoding='utf-8-sig').splitlines() if x.strip()]
    generate(images,args.output)
