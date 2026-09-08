"""
残り8000回 - タイトルロゴ(ピクセルアート)
  ゲームと同じマルモニカで「残り8000回」をピクセル描画し、少しだけ「摩耗」させる。
  (クレジットが弾で削れていく世界観に合わせて、数ピクセル欠けさせる)
  出力: Assets/Art/logo.png  (透過, Point sampling で読み込む前提)

deps: Pillow
"""
import os
import random

from PIL import Image, ImageDraw, ImageFont

_ROOT = os.path.join(os.path.dirname(__file__), "..")
FONT = os.path.join(_ROOT, "Assets", "Fonts", "x12y16pxMaruMonica.ttf")
OUT = os.path.join(_ROOT, "Assets", "Art", "logo.png")

TEXT = "残り8000回"
PX = 16                      # マルモニカのネイティブ画素サイズ
SCALE = 3                    # ロゴなので少し大きめのグリッドで
PAD = 6

rng = random.Random(80009)


def render_text(size):
    font = ImageFont.truetype(FONT, size)
    # 計測
    tmp = Image.new("L", (4, 4))
    d = ImageDraw.Draw(tmp)
    box = d.textbbox((0, 0), TEXT, font=font)
    w, h = box[2] - box[0], box[3] - box[1]
    img = Image.new("L", (w + PAD * 2, h + PAD * 2), 0)
    d = ImageDraw.Draw(img)
    d.text((PAD - box[0], PAD - box[1]), TEXT, fill=255, font=font)
    return img


def main():
    mask = render_text(PX * SCALE)
    W, H = mask.size

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = out.load()
    m = mask.load()

    ink = (233, 238, 247, 255)      # ほぼ白、わずかに青
    shadow = (18, 20, 30, 235)

    # 影(右下1px、グリッド分)
    for y in range(H):
        for x in range(W):
            if m[x, y] > 120:
                sx, sy = x + SCALE, y + SCALE
                if sx < W and sy < H:
                    px[sx, sy] = shadow
    # 本体
    for y in range(H):
        for x in range(W):
            if m[x, y] > 120:
                px[x, y] = ink

    # 摩耗: 端のブロックをいくつか欠けさせる(弾で削れた跡)
    blocks = [(x, y) for y in range(0, H, SCALE) for x in range(0, W, SCALE)
              if m[min(x + 1, W - 1), min(y + 1, H - 1)] > 120]
    for (bx, by) in rng.sample(blocks, k=max(3, len(blocks) // 22)):
        for y in range(by, min(by + SCALE, H)):
            for x in range(bx, min(bx + SCALE, W)):
                px[x, y] = (0, 0, 0, 0)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print(f"logo: {OUT}  ({W}x{H})")


if __name__ == "__main__":
    main()
