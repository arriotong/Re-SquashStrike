"""Lay the PoseSheet tiles out as one labelled contact sheet.

    python Tools/PoseSheet/compose.py [out.png] [columns]

Reads Captures/PoseSheet.json and Captures/PoseSheet_tiles/<i>_<view>.png
(written by the Unity editor method SquashGame.Editor.PoseSheet).
"""
import json, os, sys
from PIL import Image, ImageDraw, ImageFont

root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
cap = os.path.join(root, 'Captures')
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(cap, 'PoseSheet.png')
cols = int(sys.argv[2]) if len(sys.argv) > 2 else 4

poses = json.load(open(os.path.join(cap, 'PoseSheet.json'), encoding='utf-8'))
tiles = os.path.join(cap, 'PoseSheet_tiles')
first = Image.open(os.path.join(tiles, '0_0.png'))
tw, th = first.size
cell_w, cell_h, label_h = tw * 2, th, 34
rows = (len(poses) + cols - 1) // cols
sheet = Image.new('RGB', (cols * cell_w, rows * (cell_h + label_h)), (22, 25, 31))
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype('arial.ttf', 22)
except OSError:
    font = ImageFont.load_default()
for k, p in enumerate(poses):
    c, r = k % cols, k // cols
    x, y = c * cell_w, r * (cell_h + label_h)
    for v in range(2):
        img = Image.open(os.path.join(tiles, '%d_%d.png' % (p['i'], v)))
        sheet.paste(img, (x + v * tw, y + label_h))
    draw.text((x + 10, y + 5), '%d  %s' % (k + 1, p['name']), fill=(240, 220, 120), font=font)
    draw.line([(x, y), (x, y + cell_h + label_h)], fill=(60, 64, 72), width=2)
sheet.save(out)
print('wrote', out, sheet.size)
