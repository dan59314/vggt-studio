"""Interactive GrabCut proposals and optical-flow propagation; users review every frame."""
import argparse
import json
from pathlib import Path
import cv2
import numpy as np
from PIL import Image
from photo_mesh import image_geometry


def read_rgb(path, size=None):
    with Image.open(path) as im:
        im = im.convert('RGB')
        if size:
            im = im.resize(size, Image.Resampling.BILINEAR)
        else:
            im.thumbnail((1024, 1024))
        return np.array(im)


def segment(image, rectangle):
    h, w = image.shape[:2]
    x, y, rw, rh = rectangle
    x, y = max(0, int(x*w)), max(0, int(y*h))
    rw, rh = min(w-x, max(1, int(rw*w))), min(h-y, max(1, int(rh*h)))
    if rw*rh >= w*h or rw < 2 or rh < 2:
        raise ValueError('請框選主體並保留框外背景。')
    mask = np.zeros((h, w), np.uint8)
    cv2.grabCut(image, mask, (x, y, rw, rh), np.zeros((1,65)), np.zeros((1,65)), 5, cv2.GC_INIT_WITH_RECT)
    result = np.isin(mask, [cv2.GC_FGD, cv2.GC_PR_FGD])
    if not result.any():
        raise ValueError('無法分離主體，請重新框選或使用保留筆刷。')
    return result


def propagate(previous, current, mask):
    # Backward flow maps each current pixel into the previous frame.
    h, w = current.shape[:2]
    previous = cv2.resize(previous, (w,h))
    mask = cv2.resize(mask.astype(np.uint8), (w,h), interpolation=cv2.INTER_NEAREST)
    flow = cv2.calcOpticalFlowFarneback(cv2.cvtColor(current, cv2.COLOR_RGB2GRAY),
        cv2.cvtColor(previous, cv2.COLOR_RGB2GRAY), None, .5, 5, 25, 5, 7, 1.5, 0)
    yy, xx = np.mgrid[:h,:w].astype(np.float32)
    return cv2.remap(mask, xx+flow[...,0], yy+flow[...,1], cv2.INTER_NEAREST,
                     borderMode=cv2.BORDER_CONSTANT).astype(bool)


def refine(image, mask, hints):
    """Use painted hard labels; all unpainted pixels may be reclassified."""
    h,w=image.shape[:2]
    mask=cv2.resize(mask.astype(np.uint8),(w,h),interpolation=cv2.INTER_NEAREST)>0
    hints=cv2.resize(hints,(w,h),interpolation=cv2.INTER_NEAREST)
    foreground=(hints[...,0]>200)&(hints[...,1]>200)&(hints[...,2]>200)
    background=(hints[...,0]>200)&(hints[...,1]<80)&(hints[...,2]<80)
    if not (foreground.any() or background.any()):
        raise ValueError('請先塗畫保留或排除提示。')
    labels=np.where(mask,cv2.GC_PR_FGD,cv2.GC_PR_BGD).astype(np.uint8)
    labels[foreground]=cv2.GC_FGD
    labels[background]=cv2.GC_BGD
    if not np.isin(labels,[cv2.GC_FGD,cv2.GC_PR_FGD]).any() or not np.isin(labels,[cv2.GC_BGD,cv2.GC_PR_BGD]).any():
        raise ValueError('請同時留下主體與背景區域，或補畫另一種提示。')
    cv2.grabCut(image,labels,None,np.zeros((1,65)),np.zeros((1,65)),5,cv2.GC_INIT_WITH_MASK)
    result=np.isin(labels,[cv2.GC_FGD,cv2.GC_PR_FGD])
    if not result.any():
        raise ValueError('重新分割未找到主體；請補畫保留提示。原遮罩未變更。')
    return result


def aligned_masks(paths, mapping, height, width, mode):
    result = []
    lookup = {str(Path(k).resolve()).casefold(): v for k,v in mapping.items()}
    for path in paths:
        target = lookup.get(str(Path(path).resolve()).casefold())
        if not target or not Path(target).is_file():
            raise ValueError(f'缺少主體遮罩，請重新選取並確認：{path}')
        with Image.open(path) as source, Image.open(target) as mask:
            if abs(mask.width/mask.height-source.width/source.height) > .02:
                raise ValueError(f'遮罩比例不符：{path}')
            rw,rh,left,top,crop = image_geometry(*source.size,height,width,mode)
            resized = np.asarray(mask.convert('L').resize((rw,rh),Image.Resampling.NEAREST)) >= 128
        canvas = np.zeros((height,width),bool)
        kept = resized[crop:crop+min(rh,518)]
        canvas[top:top+kept.shape[0],left:left+rw] = kept
        result.append(canvas)
    return np.stack(result)


def run(request):
    output = Path(request['output'])
    output.mkdir(parents=True, exist_ok=True)
    images = request['images']
    start = request['start']
    current = read_rgb(images[start])
    if request['action'] in ('segment','refine'):
        if request['action']=='refine':
            with Image.open(request['mask']) as im:
                initial=np.asarray(im.convert('L'))>=128
            with Image.open(request['hints']) as im:
                hints=np.asarray(im.convert('RGB'))
            mask=refine(current,initial,hints)
        else:
            mask = segment(current, request['rectangle'])
        Image.fromarray(mask.astype(np.uint8)*255).save(output/f'{start:04d}.png')
    else:
        with Image.open(request['mask']) as im:
            mask = np.asarray(im.convert('L')) >= 128
        for i in range(start+1,len(images)):
            following = read_rgb(images[i])
            anchor = request.get('anchors',{}).get(str(i))
            if anchor:
                with Image.open(anchor) as im:
                    mask = np.asarray(im.convert('L')) >= 128
            else:
                mask = propagate(current, following, mask)
            Image.fromarray(mask.astype(np.uint8)*255).save(output/f'{i:04d}.png')
            current = following
            print(f'追蹤 {i+1}/{len(images)}（待人工確認）',flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--request',required=True)
    args = parser.parse_args()
    run(json.loads(Path(args.request).read_text(encoding='utf-8-sig')))
