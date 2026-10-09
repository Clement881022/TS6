"""Assemble labelled proof sheets from actual Unity captures; no model alteration."""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw
parser=argparse.ArgumentParser()
parser.add_argument('folder',type=Path)
parser.add_argument('--pose',default='front')
args=parser.parse_args()
files=sorted(args.folder.rglob(f'*-{args.pose}.png'))
cols=6; cell=320; rows=(len(files)+cols-1)//cols
sheet=Image.new('RGB',(cols*cell,rows*(cell+32)),(38,43,52))
draw=ImageDraw.Draw(sheet)
for i,path in enumerate(files):
    with Image.open(path) as source:
        thumbnail=source.resize((cell,cell))
        xy=((i%cols)*cell,(i//cols)*(cell+32))
        sheet.paste(thumbnail,xy,thumbnail.getchannel('A'))
        draw.text((xy[0]+8,xy[1]+cell+8),path.name.removesuffix(f'-{args.pose}.png'),fill='white')
out=args.folder/f'contact-{args.pose}.jpg'
sheet.save(out,quality=94)
print(f'{len(files)} models: {out}')
