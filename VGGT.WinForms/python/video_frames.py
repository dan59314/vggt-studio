"""Bounded short-video extraction using decoded presentation timestamps."""
import argparse
import json
import math
from pathlib import Path
import shutil
import subprocess


def select_frames(timestamps, start, end, interval, limit):
    if not (0 <= start < end <= 120 and interval > 0 and 2 <= limit <= 32):
        raise ValueError('片段需在前 120 秒內；間隔需大於 0；影格上限需為 2–32。')
    candidates = [(i, t) for i, t in enumerate(timestamps) if math.isfinite(t) and start <= t < end]
    if not candidates:
        raise ValueError('選定時間範圍內沒有可解碼影格。')
    # If capped, spread samples across the segment instead of taking only its start.
    spacing = max(interval, (candidates[-1][1] - candidates[0][1]) / (limit - 1))
    chosen = [candidates[0]]
    target = candidates[0][1] + spacing
    for index, timestamp in candidates[1:]:
        if timestamp + 1e-8 >= target and timestamp > chosen[-1][1]:
            chosen.append((index, timestamp))
            target += spacing
            if len(chosen) >= limit:
                break
    return chosen


def extract(video, output, start, end, interval, limit, ffmpeg='ffmpeg'):
    executable = shutil.which(ffmpeg)
    if not executable:
        raise RuntimeError('找不到 FFmpeg。請將 ffmpeg 加入 PATH，或指定執行檔。')
    probe = Path(executable).with_name('ffprobe.exe' if Path(executable).suffix.lower() == '.exe' else 'ffprobe')
    if not probe.exists():
        raise RuntimeError('FFmpeg 資料夾內找不到 ffprobe。')
    if not (0 <= start < end <= 120):
        raise ValueError('第一版支援影片前 120 秒內的片段。')
    video = str(Path(video).resolve())
    print('PROGRESS|5|讀取影片時間戳記', flush=True)
    result = subprocess.run([str(probe), '-v', 'error', '-select_streams', 'v:0',
        '-read_intervals', f'%+{end}', '-show_frames', '-show_entries',
        'frame=best_effort_timestamp_time', '-of', 'json', video],
        capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=120, check=True)
    frames = json.loads(result.stdout).get('frames', [])
    timestamps = [float(f.get('best_effort_timestamp_time', 'nan')) for f in frames]
    finite = [t for t in timestamps if math.isfinite(t)]
    if not finite:
        raise ValueError('影片沒有有效的顯示時間戳記。')
    origin = finite[0]
    times = [t-origin for t in timestamps]
    chosen = select_frames(times, start, end, interval, limit)
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    selection = '+'.join(f'eq(n\\,{i})' for i, _ in chosen)
    print(f'PROGRESS|25|擷取 {len(chosen)} 張影格', flush=True)
    subprocess.run([executable, '-hide_banner', '-loglevel', 'error', '-nostdin', '-i', video,
        '-map', '0:v:0', '-vf', f'select={selection}', '-vsync', '0', '-frames:v', str(len(chosen)),
        str(output/'frame_%04d.png')], capture_output=True, text=True, encoding='utf-8',
        errors='replace', timeout=180, check=True)
    import cv2
    import numpy as np
    items = []
    previous = None
    for number, (index, timestamp) in enumerate(chosen, 1):
        path = output / f'frame_{number:04d}.png'
        gray = cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_GRAYSCALE)
        if gray is None:
            raise RuntimeError(f'無法讀取影格 {number}')
        small = cv2.resize(gray, (160, 100))
        score = float(cv2.Laplacian(small, cv2.CV_64F).var())
        difference = None if previous is None else float(np.mean(np.abs(small.astype(float)-previous)))
        previous = small.astype(float)
        items.append(dict(path=str(path), timeSeconds=timestamp, frameIndex=index,
                          sharpness=score, difference=difference))
    manifest = dict(videoPath=video, frames=items)
    (output/'video_frames.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print('PROGRESS|100|影格擷取完成', flush=True)
    return manifest


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--video', required=True)
    p.add_argument('--output', required=True)
    p.add_argument('--start', type=float, default=0)
    p.add_argument('--end', type=float, default=30)
    p.add_argument('--interval', type=float, default=1)
    p.add_argument('--limit', type=int, default=4)
    p.add_argument('--ffmpeg', default='ffmpeg')
    a = p.parse_args()
    try:
        extract(a.video, a.output, a.start, a.end, a.interval, a.limit, a.ffmpeg)
    except subprocess.CalledProcessError as exc:
        raise SystemExit(f'影片解碼失敗：{exc.stderr}')
