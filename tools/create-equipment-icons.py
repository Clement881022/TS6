from pathlib import Path
from uuid import uuid4
from PIL import Image, ImageDraw
import math

root = Path('client/Assets/Resources/EquipmentArt')
root.mkdir(exist_ok=True)
template = Path('client/Assets/Resources/ChibiSkin/armor.png.meta').read_text(encoding='utf-8')
S = 4
gold = '#e5b75e'
dark = '#614325'
light = '#fff0b9'
jade = '#368b80'
steel = '#b8d3db'
shapes = []
img = None
draw = None
def polygon(points, fill, outline=dark, width=3):
    draw.polygon([(int(x*S), int(y*S)) for x,y in points], fill=fill)
    draw.line([(int(x*S), int(y*S)) for x,y in points + [points[0]]], fill=outline, width=width*S, joint='curve')
    shapes.append(f'<polygon points="{" ".join(f"{x},{y}" for x,y in points)}" fill="{fill}" stroke="{outline}" stroke-width="{width}" stroke-linejoin="round"/>')
def line(points, color=gold, width=5):
    draw.line([(int(x*S), int(y*S)) for x,y in points], fill=color, width=width*S, joint='curve')
    shapes.append(f'<polyline points="{" ".join(f"{x},{y}" for x,y in points)}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linejoin="round" stroke-linecap="round"/>')
def ellipse(box, fill, outline=gold, width=3):
    draw.ellipse(tuple(int(v*S) for v in box), fill=fill, outline=outline, width=width*S)
    a,b,c,d = box
    shapes.append(f'<ellipse cx="{(a+c)/2}" cy="{(b+d)/2}" rx="{(c-a)/2}" ry="{(d-b)/2}" fill="{fill or "none"}" stroke="{outline}" stroke-width="{width}"/>')
def gem(x,y):
    polygon([(x,y-13),(x+11,y),(x,y+13),(x-11,y)],jade,gold,2)
    line([(x-4,y-3),(x,y-8),(x+3,y-3)],light,2)
def shaft():
    line([(75,215),(171,44)],dark,13)
    line([(75,215),(171,44)],gold,7)
    line([(78,211),(169,46)],light,2)

for name in ['weapon_tank','weapon_warrior','weapon_ranger','weapon_mage','weapon_strategist','weapon_healer','armor','accessory']:
    img = Image.new('RGBA',(256*S,256*S))
    draw = ImageDraw.Draw(img)
    shapes = []
    if name == 'weapon_warrior':
        polygon([(84,168),(154,55),(205,24),(192,91),(111,181)],steel)
        polygon([(100,167),(180,57),(192,41),(170,82)],'#ecf4ed',steel,1)
        polygon([(72,162),(83,148),(130,176),(119,190)],gold)
        polygon([(65,168),(87,181),(67,221),(45,207)],'#7a3130',gold)
        line([(65,178),(79,186),(60,213)],gold,3)
        ellipse((37,204,61,228),gold)
    elif name == 'weapon_tank':
        polygon([(68,44),(128,26),(188,44),(195,137),(170,190),(128,229),(86,190),(61,137)],gold)
        polygon([(81,57),(128,42),(175,57),(179,134),(156,177),(128,205),(100,177),(77,134)],'#304957',light,2)
        polygon([(128,57),(143,113),(168,130),(143,146),(128,193),(113,146),(89,130),(113,113)],gold)
        gem(128,128)
    elif name == 'weapon_ranger':
        pts = [(int(71+104*math.sin(t)),int(32+192*t/math.pi)) for t in [i*math.pi/40 for i in range(41)]]
        line(pts,dark,15)
        line(pts,gold,9)
        line([(71,32),(90,129),(71,224)],light,2)
        line([(53,157),(212,89)],dark,8)
        line([(53,157),(212,89)],steel,4)
        polygon([(201,88),(230,82),(211,105)],steel)
        polygon([(53,157),(41,141),(72,149),(80,165),(51,176)],'#7a3130',gold,2)
    elif name in ('weapon_mage','weapon_healer'):
        shaft()
        ellipse((139,22,198,81),'#283e46',gold,5)
        gem(168,51)
        if name == 'weapon_healer':
            line([(147,52),(187,52)],light,5)
            line([(167,32),(167,72)],light,5)
        else:
            polygon([(168,8),(195,35),(178,41)],gold)
            polygon([(133,44),(149,68),(138,90)],gold)
        line([(78,191),(94,199)],'#903f34',9)
    elif name == 'weapon_strategist':
        polygon([(102,172),(117,180),(102,223),(88,215)],gold)
        for i in range(7):
            a = -.92+i*.27
            x = 111+112*math.sin(a)
            y = 163-120*math.cos(a)
            polygon([(106,173),(x-12,y+8),(x,y-12),(x+12,y+10),(118,176)],'#d9e4d8',gold,2)
            line([(111,169),(x,y)],'#71978e',2)
        gem(112,175)
    elif name == 'armor':
        polygon([(79,41),(104,33),(114,52),(142,52),(152,33),(177,41),(214,75),(187,108),(172,96),(177,214),(79,214),(84,96),(69,108),(42,75)],gold)
        polygon([(93,65),(112,74),(144,74),(163,65),(161,198),(95,198)],'#35515d',light,2)
        for y in range(98,188,24):
            for x in range(97,153,18):
                polygon([(x,y),(x+18,y),(x+15,y+18),(x+3,y+18)],'#577983',gold,1)
        polygon([(86,84),(128,102),(170,84),(168,108),(128,126),(88,108)],gold)
        gem(128,109)
        line([(81,200),(175,200)],gold,7)
    else:
        ellipse((64,28,192,167),None,gold,8)
        ellipse((76,38,180,154),None,light,2)
        polygon([(128,128),(167,168),(149,210),(128,228),(107,210),(89,168)],gold)
        polygon([(128,142),(153,170),(139,202),(128,214),(117,202),(103,170)],jade,light,2)
        ellipse((117,161,139,183),None,gold,3)
        line([(128,219),(128,245)],'#8d3535',9)
        line([(115,239),(128,227),(141,239)],gold,3)
    img.resize((256,256),Image.Resampling.LANCZOS).save(root / (name+'.png'))
    (root / (name+'.svg')).write_text('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">'+''.join(shapes)+'</svg>',encoding='utf-8')
    meta = root / (name+'.png.meta')
    if not meta.exists():
        import re
        meta.write_text(re.sub(r'guid: [a-f0-9]+', 'guid: '+uuid4().hex, template,count=1),encoding='utf-8')
print('Created eight original vector equipment icons and transparent PNG exports.')
