"""Content-aware video analysis and key-frame extraction for reconstruction."""
import argparse, json, math, shutil, subprocess
from pathlib import Path

# Until sliding-window inference is added, defaults must remain safe for the
# current single-batch VGGT runner. Users may override the count explicitly.
QUALITY_TARGETS = {'quick': 12, 'balanced': 24, 'high': 32}
MODE_NAMES = {'auto': '自動判斷', 'orbit': '主體環繞', 'walkthrough': '場景走拍'}

def select_frames(timestamps, start, end, interval, limit):
    """Temporal candidates without the former 120-second and 32-frame caps."""
    if not (0 <= start < end and interval > 0 and limit >= 2):
        raise ValueError('起點需小於終點；間隔需大於 0；影格上限至少為 2。')
    candidates=[(i,t) for i,t in enumerate(timestamps) if math.isfinite(t) and start <= t < end]
    if not candidates: raise ValueError('選定時間範圍內沒有可解碼影格。')
    spacing=max(interval,(candidates[-1][1]-candidates[0][1])/max(1,limit-1))
    chosen=[candidates[0]]; target=candidates[0][1]+spacing
    for index,timestamp in candidates[1:]:
        if timestamp+1e-8 >= target and timestamp > chosen[-1][1]:
            chosen.append((index,timestamp)); target += spacing
            if len(chosen) >= limit: break
    if len(chosen)==1 and len(candidates)>1: chosen.append(candidates[-1])
    return chosen

def _normalize(values):
    import numpy as np
    a=np.asarray(values,dtype=float)
    lo,hi=np.percentile(a,[10,90]) if len(a)>2 else (a.min(),a.max())
    return np.clip((a-lo)/max(hi-lo,1e-9),0,1)

def analyze_images(paths,times,target,mode='auto'):
    """Score candidates and retain the best frame in each temporal bucket."""
    import cv2, numpy as np
    rows=[]; previous=None; previous_desc=None
    orb=cv2.ORB_create(nfeatures=700); matcher=cv2.BFMatcher(cv2.NORM_HAMMING,crossCheck=True)
    for path,timestamp in zip(paths,times):
        gray=cv2.imdecode(np.fromfile(path,dtype=np.uint8),cv2.IMREAD_GRAYSCALE)
        if gray is None: raise RuntimeError(f'無法讀取分析影格：{path}')
        scale=min(1.0,480.0/max(gray.shape)); small=cv2.resize(gray,None,fx=scale,fy=scale,interpolation=cv2.INTER_AREA)
        sharpness=float(cv2.Laplacian(small,cv2.CV_64F).var())
        exposure=max(0.0,1.0-float(np.mean(small<12))-float(np.mean(small>243)))
        keypoints,desc=orb.detectAndCompute(small,None)
        difference=None if previous is None else float(np.mean(cv2.absdiff(small,previous)))
        overlap=None
        if previous_desc is not None and desc is not None and len(previous_desc) and len(desc):
            matches=matcher.match(previous_desc,desc); overlap=sum(m.distance<55 for m in matches)/max(1,min(len(previous_desc),len(desc)))
        rows.append(dict(time=float(timestamp),sharpness=sharpness,exposure=exposure,difference=difference,
                         featureCount=len(keypoints),overlap=overlap))
        previous,previous_desc=small,desc
    sharp=_normalize([r['sharpness'] for r in rows]); features=_normalize([r['featureCount'] for r in rows])
    motion=_normalize([r['difference'] or 0 for r in rows])
    for i,row in enumerate(rows):
        motion_score=1.0-abs(float(motion[i])-.45)/.55
        row['score']=float(.42*sharp[i]+.23*features[i]+.20*row['exposure']+.15*max(0,motion_score))
    target=max(2,min(int(target),len(rows))); selected=set(); edges=np.linspace(0,len(rows),target+1).astype(int)
    for a,b in zip(edges[:-1],edges[1:]):
        if b>a: selected.add(max(range(a,b),key=lambda i:rows[i]['score']))
    selected.update([0,len(rows)-1])
    if len(selected)>target:
        protected={0,len(rows)-1}
        for index in sorted(selected-protected,key=lambda i:rows[i]['score'])[:len(selected)-target]: selected.remove(index)
    sharp_cut=float(np.percentile([r['sharpness'] for r in rows],20))
    blurry=sum(r['sharpness']<sharp_cut for r in rows); exposure_bad=sum(r['exposure']<.82 for r in rows)
    low_overlap=[r for r in rows[1:] if r['overlap'] is not None and r['overlap']<.035]
    low_texture=sum(r['featureCount']<80 for r in rows); duration=max(0.0,times[-1]-times[0]) if times else 0.0
    warnings=[]
    if blurry>len(rows)*.3: warnings.append('模糊影格偏多，建議縮短快速移動片段或重新拍攝。')
    if exposure_bad>len(rows)*.2: warnings.append('部分畫面過暗或過曝，可能降低貼圖與特徵品質。')
    if low_overlap: warnings.append('偵測到低重疊區段，已保留橋接影格；相機路徑仍可能中斷。')
    if low_texture>len(rows)*.3: warnings.append('低紋理畫面偏多，牆面、天空或水面可能無法可靠定位。')
    inferred=mode
    if mode=='auto':
        diffs=[r['difference'] for r in rows if r['difference'] is not None]
        inferred='orbit' if duration<=90 and diffs and np.std(diffs)<max(3,np.mean(diffs)*.7) else 'walkthrough'
    grade='良好' if not warnings else ('可用' if len(warnings)<=2 else '需注意')
    report=dict(mode=mode,inferredMode=inferred,qualityGrade=grade,analyzedFrames=len(rows),recommendedFrames=len(selected),
        blurryFrames=blurry,exposureProblemFrames=exposure_bad,lowOverlapSegments=len(low_overlap),lowTextureFrames=low_texture,
        problemTimes=[round(r['time'],3) for r in low_overlap[:20]],warnings=warnings)
    return rows,selected,report

def _probe(executable,video):
    probe=Path(executable).with_name('ffprobe.exe' if Path(executable).suffix.lower()=='.exe' else 'ffprobe')
    if not probe.exists(): raise RuntimeError('FFmpeg 資料夾內找不到 ffprobe。')
    result=subprocess.run([str(probe),'-v','error','-select_streams','v:0','-show_streams','-show_format','-show_frames',
        '-show_entries','stream=width,height,duration:format=duration:frame=best_effort_timestamp_time','-of','json',video],
        capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=300,check=True)
    payload=json.loads(result.stdout); stream=(payload.get('streams') or [{}])[0]
    raw=[float(f.get('best_effort_timestamp_time','nan')) for f in payload.get('frames',[])]
    finite=[t for t in raw if math.isfinite(t)]
    if not finite: raise ValueError('影片沒有有效的顯示時間戳記。')
    origin=finite[0]; times=[t-origin for t in raw]
    duration=float((payload.get('format') or {}).get('duration') or stream.get('duration') or finite[-1]-origin)
    return times,duration,int(stream.get('width') or 0),int(stream.get('height') or 0)

def extract(video,output,start,end,interval,limit,ffmpeg='ffmpeg',mode='auto',quality='quick'):
    executable=shutil.which(ffmpeg)
    if not executable: raise RuntimeError('找不到 FFmpeg。請將 ffmpeg 加入 PATH，或指定執行檔。')
    if mode not in MODE_NAMES or quality not in QUALITY_TARGETS: raise ValueError('未知的拍攝模式或品質設定。')
    video=str(Path(video).resolve()); print('PROGRESS|5|讀取完整影片與時間戳記',flush=True)
    timestamps,duration,width,height=_probe(executable,video); effective_end=duration if end<=0 else min(float(end),duration)
    if not (0<=start<effective_end): raise ValueError(f'有效片段必須位於 0～{duration:.1f} 秒。')
    target=limit if limit>0 else QUALITY_TARGETS[quality]; candidate_limit=min(max(target*3,target+8),600)
    candidates=select_frames(timestamps,start,effective_end,max(.03,interval/3),candidate_limit)
    output=Path(output).resolve(); output.mkdir(parents=True,exist_ok=False); analysis_dir=output/'analysis'; analysis_dir.mkdir()
    selection='+'.join(f'eq(n\\,{i})' for i,_ in candidates)
    print(f'PROGRESS|20|擷取 {len(candidates)} 張候選影格進行品質分析',flush=True)
    subprocess.run([executable,'-hide_banner','-loglevel','error','-nostdin','-i',video,'-map','0:v:0',
        '-vf',f'select={selection},scale=480:-2','-vsync','0','-frames:v',str(len(candidates)),'-q:v','3',str(analysis_dir/'candidate_%04d.jpg')],
        capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=900,check=True)
    paths=[analysis_dir/f'candidate_{i:04d}.jpg' for i in range(1,len(candidates)+1)]
    if not all(p.is_file() for p in paths): raise RuntimeError('候選影格擷取不完整。')
    rows,selected,report=analyze_images(paths,[t for _,t in candidates],target,mode)
    final=[candidates[i] for i in sorted(selected)]; final_selection='+'.join(f'eq(n\\,{i})' for i,_ in final)
    print(f'PROGRESS|65|輸出 {len(final)} 張建議關鍵影格',flush=True)
    subprocess.run([executable,'-hide_banner','-loglevel','error','-nostdin','-i',video,'-map','0:v:0','-vf',f'select={final_selection}',
        '-vsync','0','-frames:v',str(len(final)),str(output/'frame_%04d.png')],capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=1200,check=True)
    items=[]
    for number,source_index in enumerate(sorted(selected),1):
        index,timestamp=candidates[source_index]; row=rows[source_index]
        items.append(dict(path=str(output/f'frame_{number:04d}.png'),timeSeconds=timestamp,frameIndex=index,sharpness=row['sharpness'],
            difference=row['difference'],exposure=row['exposure'],featureCount=row['featureCount'],overlap=row['overlap'],recommended=True,score=row['score']))
    shutil.rmtree(analysis_dir,ignore_errors=True)
    report.update(dict(durationSeconds=duration,width=width,height=height,selectedStart=float(start),selectedEnd=effective_end))
    manifest=dict(videoPath=video,captureMode=mode,quality=quality,frames=items,report=report)
    (output/'video_frames.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f"PROGRESS|100|分析完成：{report['qualityGrade']}，建議 {len(items)} 張影格",flush=True)
    return manifest

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('--video',required=True); p.add_argument('--output',required=True)
    p.add_argument('--start',type=float,default=0); p.add_argument('--end',type=float,default=0); p.add_argument('--interval',type=float,default=1)
    p.add_argument('--limit',type=int,default=0); p.add_argument('--mode',choices=MODE_NAMES,default='auto'); p.add_argument('--quality',choices=QUALITY_TARGETS,default='quick'); p.add_argument('--ffmpeg',default='ffmpeg')
    a=p.parse_args()
    try: extract(a.video,a.output,a.start,a.end,a.interval,a.limit,a.ffmpeg,a.mode,a.quality)
    except subprocess.CalledProcessError as exc: raise SystemExit(f'影片解碼失敗：{exc.stderr}')
