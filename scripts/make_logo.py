"""
残り8000回 - タイトルロゴ(ドット絵)

方針:
  タイトルは「登り切った先の、まだ壊れていない場所」。だからロゴ自体は
  クリーンでどっしり。摩耗表現はクレジット(タイル)側の役目なのでここでは最小限。

  ゲーム本編と同じフォント x12y16pxMaruMonica を 32px(16 の倍数 = AA なしで
  ピクセル完全)でラスタライズし、「1 ラスタピクセル = 1 ドット」として扱う。
  そのドット格子の上で:
    - 棚影(shelf) : 文字下端の真下 1 ドットを濃紺に。重さを出す。
    - 底の罫線     : 文字の下に 2 ドットの横線(= 登り出す ====== )。全幅。
    - 欠け         : 底辺の輪郭を数ドットだけ、ごく僅かに(世界観の目配せ程度)。
  最後に整数倍(SCALE)で拡大。各ドットがカリッとした正方形になる。

  出力 : Assets/Art/logo.png (透過 / Point / 圧縮なし)
  配色 : 寒色モノクロのみ。世界観を壊す彩色はしない。
  差し替え: 同じパス・近い比率の PNG を置けば TitleScreen がそのまま使う。

deps: Pillow
"""
import os
import random

from PIL import Image, ImageDraw, ImageFont

_ROOT = os.path.join(os.path.dirname(__file__), "..")
FONT = os.path.join(_ROOT, "Assets", "Fonts", "x12y16pxMaruMonica.ttf")
OUT = os.path.join(_ROOT, "Assets", "Art", "logo.png")

TEXT = "残り8000回"
NATIVE = 32          # ラスタライズ画素(16 の倍数 = ピクセル完全)
SCALE = 3            # 拡大率(1 ドット = SCALE x SCALE px)
MARGIN_X = 6         # 左右余白(ドット)
MARGIN_TOP = 3
RULE_H = 2           # 底の罫線の太さ(ドット)
RULE_DROP = 3        # 文字下端から罫線までの間(ドット)

INK = (230, 236, 247, 255)      # ほぼ白・ごく僅かに青
SHADOW = (12, 14, 22, 240)      # 真下 1 ドットの棚影
RULE = (116, 128, 152, 200)     # 底の罫線

rng = random.Random(80009)


def glyph_mask(text, size):
    font = ImageFont.truetype(FONT, size)
    tmp = ImageDraw.Draw(Image.new("L", (4, 4)))
    box = tmp.textbbox((0, 0), text, font=font)
    w, h = box[2] - box[0], box[3] - box[1]
    img = Image.new("L", (w, h), 0)
    ImageDraw.Draw(img).text((-box[0], -box[1]), text, fill=255, font=font)
    px = img.load()
    return [[px[x, y] > 110 for x in range(w)] for y in range(h)], w, h


def main():
    mask, mw, mh = glyph_mask(TEXT, NATIVE)

    ox, oy = MARGIN_X, MARGIN_TOP
    W = mw + MARGIN_X * 2
    rule_y0 = oy + mh + RULE_DROP
    H = rule_y0 + RULE_H + MARGIN_TOP
    grid = [[(0, 0, 0, 0)] * W for _ in range(H)]

    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H:
            grid[y][x] = c

    def filled(mx, my):
        return 0 <= mx < mw and 0 <= my < mh and mask[my][mx]

    # 1) 棚影(文字下端の真下 1 ドット)
    for my in range(mh):
        for mx in range(mw):
            if mask[my][mx] and not filled(mx, my + 1):
                put(ox + mx, oy + my + 1, SHADOW)

    # 2) 本体インク
    for my in range(mh):
        for mx in range(mw):
            if mask[my][mx]:
                put(ox + mx, oy + my, INK)

    # 3) 底辺だけ、ごく僅かな欠け(下 2 行の輪郭から 4 ドット)
    bottom_edge = [
        (mx, my) for my in range(mh) for mx in range(mw)
        if mask[my][mx] and my >= mh - 2 and not filled(mx, my + 1)
    ]
    for (mx, my) in rng.sample(bottom_edge, k=min(4, len(bottom_edge))):
        put(ox + mx, oy + my, (0, 0, 0, 0))
        put(ox + mx, oy + my + 1, (0, 0, 0, 0))

    # 4) 底の罫線(全幅・2 ドット)= 登り出す ======
    for yy in range(rule_y0, rule_y0 + RULE_H):
        for x in range(1, W - 1):
            put(x, yy, RULE)

    # ---- 整数倍で拡大(ニアレスト)----
    out = Image.new("RGBA", (W * SCALE, H * SCALE), (0, 0, 0, 0))
    op = out.load()
    for y in range(H):
        for x in range(W):
            c = grid[y][x]
            if c[3] == 0:
                continue
            for dy in range(SCALE):
                for dx in range(SCALE):
                    op[x * SCALE + dx, y * SCALE + dy] = c

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print(f"logo: {OUT}  ({out.width}x{out.height}, grid {W}x{H}, x{SCALE})")


if __name__ == "__main__":
    main()
