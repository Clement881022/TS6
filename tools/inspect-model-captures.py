"""Read-only check of actual Unity review output: empty frames and image-edge clipping."""
import json
import sys
from pathlib import Path
from PIL import Image

folder=Path(sys.argv[1])
records=[]
for path in sorted(folder.glob('*.png')):
    with Image.open(path) as frame:
        assert frame.size==(1536,1536), (path.name,frame.size)
        box=frame.getchannel('A').getbbox()
        assert box is not None, f'Empty model: {path.name}'
        margin=min(box[0],box[1],1536-box[2],1536-box[3])
        records.append({'file':path.name,'bounds':box,'edgeMargin':margin,'clipped':margin<4})
report={'frames':len(records),'clippedFrames':[r['file'] for r in records if r['clipped']],'records':records,'visualApproval':False}
(folder/'frame-bounds.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'frames':report['frames'],'clippedFrames':report['clippedFrames']},ensure_ascii=False))
if report['clippedFrames']: sys.exit(1)
