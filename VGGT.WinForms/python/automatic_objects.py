"""Local SAM automatic candidate masks; never modifies the source or selected mask."""
import argparse
import json
import os
from pathlib import Path
import numpy as np
from PIL import Image

CHECKPOINT_URL='https://dl.fbaipublicfiles.com/segment_anything/sam_vit_b_01ec64.pth'


def generate(image_path, output):
    import torch
    try:
        from segment_anything import sam_model_registry, SamAutomaticMaskGenerator
    except ImportError as ex:
        raise RuntimeError('目前 Python 缺少 SAM。請在此 Python 環境安裝 segment-anything==1.0。') from ex
    output=Path(output)
    output.mkdir(parents=True,exist_ok=False)
    checkpoint=Path(os.environ.get('LOCALAPPDATA',Path.home()))/'VGGT Studio/models/sam_vit_b_01ec64.pth'
    checkpoint.parent.mkdir(parents=True,exist_ok=True)
    if not checkpoint.is_file():
        print('首次下載 SAM 模型（約 360 MB），不會上傳影像。',flush=True)
        temporary=checkpoint.with_suffix('.'+str(os.getpid())+'.download')
        try:
            torch.hub.download_url_to_file(CHECKPOINT_URL,str(temporary),progress=False)
            temporary.replace(checkpoint)
        finally:
            temporary.unlink(missing_ok=True)
    device='cuda' if torch.cuda.is_available() else 'cpu'
    print(f'載入 SAM，使用 {device}；正在自動找物件。',flush=True)
    model=sam_model_registry['vit_b'](checkpoint=str(checkpoint)).to(device).eval()
    with Image.open(image_path) as im:
        im=im.convert('RGB');im.thumbnail((1024,1024));image=np.asarray(im)
    generator=SamAutomaticMaskGenerator(model,points_per_side=32,points_per_batch=16,
        pred_iou_thresh=.86,stability_score_thresh=.92,crop_n_layers=0,min_mask_region_area=0)
    with torch.inference_mode():
        masks=generator.generate(image)
    masks=[m for m in masks if m['area']>=32]
    masks=sorted(masks,key=lambda m:m['predicted_iou'],reverse=True)[:120]
    palette=[(255,85,85),(60,200,255),(100,240,100),(255,200,60),(210,110,255),(255,130,200)]
    overlay=image.copy()
    records=[]
    for i,m in enumerate(masks):
        name=f'object_{i+1:03d}.png'
        Image.fromarray(m['segmentation'].astype(np.uint8)*255).save(output/name)
        records.append(dict(id=i+1,mask=name,area=int(m['area']),score=float(m['predicted_iou'])))
    # Paint smaller candidates last, so large background regions do not hide them.
    for i in sorted(range(len(masks)),key=lambda j:masks[j]['area'],reverse=True):
        region=masks[i]['segmentation']
        overlay[region]=(image[region]*.55+np.array(palette[i%len(palette)])*.45).astype(np.uint8)
    Image.fromarray(image).save(output/'source.png')
    Image.fromarray(overlay).save(output/'candidates.png')
    manifest=dict(width=image.shape[1],height=image.shape[0],source='source.png',preview='candidates.png',objects=records)
    (output/'objects.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'找到 {len(records)} 個候選區域。請多選並合併需要的物件。',flush=True)
    return manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--image',required=True);parser.add_argument('--output',required=True)
    args=parser.parse_args()
    generate(args.image,args.output)
