"""Verify the MP4 captured and encoded by the Unity share-button test."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--directory', type=Path, default=Path('artifacts/visuals'))
parser.add_argument('--ffprobe', default='ffprobe')
parser.add_argument('--output', type=Path, help='Optional report path outside container-owned export directories')
args = parser.parse_args()
path = args.directory / 'HORIZON-run-001.mp4'
probe = json.loads(subprocess.check_output([args.ffprobe, '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)], text=True))
video = next(s for s in probe['streams'] if s['codec_type'] == 'video')
audio = next(s for s in probe['streams'] if s['codec_type'] == 'audio')
assert video['codec_name'] == 'h264' and (video['width'], video['height']) == (540, 960)
assert int(video['nb_frames']) == 60 and video['pix_fmt'] == 'yuv420p'
assert audio['codec_name'] == 'aac' and audio['sample_rate'] == '22050' and audio['channels'] == 1
assert abs(float(probe['format']['duration']) - 10) < 0.15
assert (args.directory / 'HORIZON-run-001.pcm').stat().st_size == 10 * 22050 * 2
result = {'video': path.name, 'seconds': float(probe['format']['duration']), 'frames': 60,
          'resolution': [540, 960], 'video_codec': 'h264', 'audio_codec': 'aac',
          'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'actual_unity_share_button': True}
if args.output:
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
