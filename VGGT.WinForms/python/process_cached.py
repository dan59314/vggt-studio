"""Reversible CPU-only postprocessing of immutable VGGT predictions."""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image
from geometry_cleanup import confidence_mask, spatial_sample
from photo_mesh import repair_small_holes, build_photo_mesh
from vggt_runner import write_ascii_ply
from viewer_export import export_viewer


def process_arrays(data, settings, subject=None):
    import cv2
    depth = data['depth'][...,0].copy()
    domain = data['image_valid_mask'].copy() & np.isfinite(depth) & (depth > 0)
    domain &= (depth >= settings['near']) & (depth <= settings['far'])
    if subject is not None:
        if subject.shape != domain.shape:
            raise ValueError('主體遮罩尺寸不符。')
        domain &= subject
    if settings['smooth'] > 0:
        for i in range(len(depth)):
            if domain[i].any():
                median = float(np.median(depth[i][domain[i]]))
                safe = np.where(domain[i], depth[i], median).astype(np.float32)
                filtered = cv2.bilateralFilter(safe, 5, median*.01*settings['smooth'], 2.)
                # Do not smooth against padding or missing/filtered neighbors.
                interior = cv2.erode(domain[i].astype(np.uint8), np.ones((5,5),np.uint8)).astype(bool)
                depth[i][interior] = filtered[interior]
    n,h,w = depth.shape
    yy,xx = np.mgrid[:h,:w]
    points = np.empty((n,h,w,3),np.float32)
    for i in range(n):
        k,e = data['intrinsic'][i],data['extrinsic'][i]
        camera = np.stack([(xx-k[0,2])*depth[i]/k[0,0], (yy-k[1,2])*depth[i]/k[1,1],depth[i]],-1)
        points[i] = (camera-e[:,3]) @ e[:,:3]
    valid = confidence_mask(points,data['depth_conf'],domain,settings['confidence'])
    repaired_mask = np.zeros_like(valid)
    for i in range(n):
        if settings['components'] > 0:
            count,labels,stats,_=cv2.connectedComponentsWithStats(valid[i].astype(np.uint8),connectivity=8)
            sizes=stats[:,cv2.CC_STAT_AREA]
            valid[i] &= (labels>0) & (sizes[labels]>=settings['components'])
        if settings['holeArea'] > 0:
            before=valid[i].copy()
            points[i],depth[i],valid[i],_=repair_small_holes(points[i],depth[i],valid[i],domain[i],settings['holeArea'])
            repaired_mask[i]=valid[i]&~before
    if not valid.any():
        raise ValueError('目前參數移除了所有點，請放寬信心或深度範圍。')
    return points,depth,valid,repaired_mask


def run(cache, output, settings, mesh=False):
    cache,output=Path(cache).resolve(),Path(output).resolve()
    result=json.loads((cache/'result.json').read_text(encoding='utf-8'))
    with np.load(cache/result['denseArrayPath']) as data:
        print('PROGRESS|15|讀取預測快取並更新點雲',flush=True)
        subject = None
        if settings.get('subjectMasks') is not None:
            from subject_masks import aligned_masks
            subject = aligned_masks([c['image'] for c in result['cameras']],settings['subjectMasks'],
                data['depth'].shape[1],data['depth'].shape[2],result.get('preprocessMode','crop'))
        points,depth,valid,repaired=process_arrays(data,settings,subject)
        colors=np.stack([np.asarray(Image.open(cache/p['imagePath']).convert('RGB')) for p in result['imageDepthPairs']])
        marked=colors.copy(); marked[repaired]=[255,70,210]
        xyz,rgb=spatial_sample(points[valid],np.column_stack([colors[valid],marked[valid]]),settings['maxPoints'])
        output.mkdir(parents=True,exist_ok=False)
        xyz[:,1]*=-1
        write_ascii_ply(output/'scene.ply',xyz,rgb[:,:3])
        write_ascii_ply(output/'repaired.ply',xyz,rgb[:,3:])
        result['plyPath']='scene.ply'
        result['meshGlbPath']=None;result['viewerProjectPath']=None
        result['denseArrayPath']=str(cache/result['denseArrayPath'])
        result['cameraAnimationPath']=str(cache/result['cameraAnimationPath'])
        result['depthPreviews']=[str(cache/p) for p in result['depthPreviews']]
        for pair in result['imageDepthPairs']:
            pair['imagePath']=str(cache/pair['imagePath']);pair['depthPath']=str(cache/pair['depthPath'])
        result['processingStats']=dict(validPoints=int(valid.sum()),previewPoints=len(xyz),repairedPoints=int(repaired.sum()))
        result['processingSettings']=settings
        result['shapes'].pop('mesh',None)
        if mesh:
            print('PROGRESS|55|依目前點雲建立網格與貼圖',flush=True)
            paths=[c['image'] for c in result['cameras']]
            stats=build_photo_mesh(points,colors,data['depth_conf'],settings['confidence'],settings['maxFaces'],output/'scene_mesh.glb',
                data['image_valid_mask'] if subject is None else data['image_valid_mask'] & subject,
                depth,paths,result.get('preprocessMode','crop'),prepared_valid=valid,texture_size=settings['textureSize'])
            result['meshGlbPath']='scene_mesh.glb';result['shapes']['mesh']=list(stats[:2])
            project,animation=export_viewer(output,data['extrinsic'],data['intrinsic'],depth.shape[1],paths,'scene_mesh.glb',
                times=result.get('videoTimeline',{}).get('times') if result.get('videoTimeline') else None)
            result['viewerProjectPath']=project;result['cameraAnimationPath']=animation
            import trimesh
            scene=trimesh.load(output/'scene_mesh.glb')
            (output/'textures').mkdir()
            vertices=[];faces=[];offset=0
            for view_index,item in enumerate(scene.geometry.values()):
                item.visual.material.baseColorTexture.save(output/'textures'/f'texture_{view_index+1:03d}.png')
                uv=item.visual.uv;image=np.asarray(item.visual.material.baseColorTexture)
                px=np.clip((uv[:,0]*image.shape[1]).astype(int),0,image.shape[1]-1)
                py=np.clip(((1-uv[:,1])*image.shape[0]).astype(int),0,image.shape[0]-1)
                positions=item.vertices.copy();positions[:,2]*=-1
                vertices.extend(np.column_stack([positions,image[py,px,:3]]).tolist())
                faces.extend((item.faces+offset).tolist());offset+=len(positions)
            (output/'mesh_preview.json').write_text(json.dumps(dict(vertices=vertices,faces=faces)),encoding='utf-8')
        if result.get('videoTimeline'):
            (output/'video_timeline.json').write_text(json.dumps(result['videoTimeline']),encoding='utf-8')
        (output/'processing-settings.json').write_text(json.dumps(settings,indent=2),encoding='utf-8')
        (output/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print('PROGRESS|100|更新完成（未重新執行 AI）',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--cache',required=True);parser.add_argument('--output',required=True)
    parser.add_argument('--settings',required=True);parser.add_argument('--mesh',action='store_true')
    args=parser.parse_args()
    run(args.cache,args.output,json.loads(Path(args.settings).read_text(encoding='utf-8-sig')),args.mesh)
