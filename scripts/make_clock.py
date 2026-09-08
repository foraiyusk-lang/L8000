"""
残り8000回 - 時計の文字盤(巻き戻しが止まる演出用)
  リング + 12 の目盛り。中心は透過。針は別スプライト(white.png)を回す。
  出力: Assets/Art/clock.png
"""
import math
import os

from PIL import Image, ImageDraw

_ROOT = os.path.join(os.path.dirname(__file__), "..")
OUT = os.path.join(_ROOT, "Assets", "Art", "clock.png")

S = 128
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
c = S / 2
col = (210, 216, 228, 235)

# 内側の暗い円盤(背後のクレジットが透けないように)
d.ellipse([7, 7, S - 7, S - 7], fill=(6, 7, 12, 232))

# リング
d.ellipse([6, 6, S - 6, S - 6], outline=col, width=3)

# 12 目盛り
for k in range(12):
    a = math.radians(k * 30 - 90)
    r0 = c - 12
    r1 = c - (20 if k % 3 == 0 else 16)
    x0, y0 = c + r0 * math.cos(a), c + r0 * math.sin(a)
    x1, y1 = c + r1 * math.cos(a), c + r1 * math.sin(a)
    d.line([x0, y0, x1, y1], fill=col, width=(4 if k % 3 == 0 else 2))

# 中心の点
d.ellipse([c - 3, c - 3, c + 3, c + 3], fill=col)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("clock:", OUT, img.size)
