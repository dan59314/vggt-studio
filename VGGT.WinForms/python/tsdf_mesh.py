"""Fuse aligned RGB-D views into one connected object mesh with Open3D TSDF."""
from pathlib import Path
import numpy as np


def multiview_consistency(depth, intrinsic, extrinsic, valid, tolerance=.055, minimum_support=1):
    """Keep a depth sample only when another view observes the same surface."""
    n,h,w=depth.shape; yy,xx=np.mgrid[:h,:w]; result=np.zeros_like(valid)
    for i in range(n):
        ids=np.flatnonzero(valid[i].reshape(-1))
        if not len(ids): continue
        d=depth[i].reshape(-1)[ids]; x=xx.reshape(-1)[ids]; y=yy.reshape(-1)[ids];k=intrinsic[i];e=extrinsic[i]
        camera=np.column_stack([(x-k[0,2])*d/k[0,0],(y-k[1,2])*d/k[1,1],d])
        world=(camera-e[:,3])@e[:,:3]
        support=np.zeros(len(ids),np.uint8)
        for j in range(n):
            if j==i: continue
            target=world@extrinsic[j][:,:3].T+extrinsic[j][:,3]
            z=target[:,2]; kj=intrinsic[j]
            u=np.rint(target[:,0]*kj[0,0]/np.maximum(z,1e-9)+kj[0,2]).astype(int)
            v=np.rint(target[:,1]*kj[1,1]/np.maximum(z,1e-9)+kj[1,2]).astype(int)
            inside=(z>0)&(u>=0)&(u<w)&(v>=0)&(v<h)
            pos=np.flatnonzero(inside)
            if not len(pos): continue
            observed=depth[j,v[pos],u[pos]]; observable=valid[j,v[pos],u[pos]]
            agrees=observable&(np.abs(z[pos]-observed)<=tolerance*np.maximum(observed,1e-6))
            support[pos[agrees]]+=1
        kept=ids[support>=minimum_support]
        # Do not destroy a view if predicted poses/depths are too inconsistent;
        # retaining its strongest central evidence is safer than an empty volume.
        if len(kept)<max(64,len(ids)*.08): kept=ids
        result[i].reshape(-1)[kept]=True
    return result


def build_tsdf_mesh(depth, colors, intrinsic, extrinsic, valid, max_faces, output,
                    resolution=224):
    import open3d as o3d
    import trimesh
    original_count=int(valid.sum())
    valid=multiview_consistency(depth,intrinsic,extrinsic,valid)
    print(f'Multi-view depth consistency: {original_count} -> {int(valid.sum())} samples',flush=True)
    points=[]
    h,w=depth.shape[1:]
    yy,xx=np.mgrid[:h,:w]
    for i in range(len(depth)):
        d=depth[i]; k=intrinsic[i]; e=extrinsic[i]
        camera=np.stack([(xx-k[0,2])*d/k[0,0],(yy-k[1,2])*d/k[1,1],d],-1)
        world=(camera-e[:,3])@e[:,:3]
        points.append(world[valid[i]])
    cloud=np.concatenate([x for x in points if len(x)])
    lo,hi=np.percentile(cloud,[1,99],axis=0)
    diagonal=float(np.linalg.norm(hi-lo)); extent=float(np.max(hi-lo))
    if not np.isfinite(diagonal) or diagonal<=0: raise ValueError('主體範圍無效，無法進行 TSDF 融合。')
    length=max(extent*1.25,diagonal*.8); voxel=max(length/resolution,1e-6); trunc=voxel*5
    center=(lo+hi)/2; origin=center-length/2
    volume=o3d.pipelines.integration.UniformTSDFVolume(length=length,resolution=int(resolution),sdf_trunc=trunc,
        color_type=o3d.pipelines.integration.TSDFVolumeColorType.RGB8,origin=origin)
    positive=depth[valid]
    depth_trunc=float(np.percentile(positive,99.5)*1.05)
    for i in range(len(depth)):
        masked=np.where(valid[i],depth[i],0).astype(np.float32)
        color=np.ascontiguousarray(colors[i].astype(np.uint8))
        rgbd=o3d.geometry.RGBDImage.create_from_color_and_depth(
            o3d.geometry.Image(color),o3d.geometry.Image(masked),depth_scale=1.0,
            depth_trunc=depth_trunc,convert_rgb_to_intensity=False)
        k=intrinsic[i]
        pinhole=o3d.camera.PinholeCameraIntrinsic(w,h,float(k[0,0]),float(k[1,1]),float(k[0,2]),float(k[1,2]))
        world_to_camera=np.eye(4); world_to_camera[:3,:4]=extrinsic[i]
        volume.integrate(rgbd,pinhole,world_to_camera)
    mesh=volume.extract_triangle_mesh()
    if len(mesh.triangles)==0: raise RuntimeError('TSDF 沒有產生表面；請降低信心門檻或增加環繞視角。')
    margin=(hi-lo)*.05+voxel*2
    mesh=mesh.crop(o3d.geometry.AxisAlignedBoundingBox(lo-margin,hi+margin))
    mesh.remove_degenerate_triangles();mesh.remove_duplicated_triangles();mesh.remove_duplicated_vertices();mesh.remove_unreferenced_vertices()
    if len(mesh.triangles):
        labels,counts,_=mesh.cluster_connected_triangles(); labels=np.asarray(labels);counts=np.asarray(counts)
        keep=np.isin(labels,np.flatnonzero(counts>=max(50,counts.max()*.01)))
        mesh.remove_triangles_by_mask(~keep);mesh.remove_unreferenced_vertices()
    if len(mesh.triangles)>max_faces:
        mesh=mesh.simplify_quadric_decimation(max_faces)
    mesh.compute_vertex_normals()
    vertices=np.asarray(mesh.vertices).copy();vertices[:,1:]*=-1
    faces=np.asarray(mesh.triangles)
    vertex_colors=(np.clip(np.asarray(mesh.vertex_colors),0,1)*255).astype(np.uint8)
    tri=trimesh.Trimesh(vertices=vertices,faces=faces,vertex_colors=vertex_colors,process=False)
    material=trimesh.visual.material.PBRMaterial(name='Fused object',metallicFactor=0.,roughnessFactor=1.,doubleSided=True)
    tri.visual=trimesh.visual.ColorVisuals(mesh=tri,vertex_colors=np.column_stack([vertex_colors,np.full(len(vertex_colors),255,np.uint8)]))
    def double_sided(tree):
        for item in tree.get('materials',[]): item['doubleSided']=True;item['metallicFactor']=0.;item['roughnessFactor']=1.
    Path(output).write_bytes(trimesh.exchange.gltf.export_glb(trimesh.Scene(tri),tree_postprocessor=double_sided))
    print(f'TSDF fusion: voxel {voxel:.6g}; {len(vertices)} vertices; {len(faces)} faces',flush=True)
    return len(vertices),len(faces),voxel
