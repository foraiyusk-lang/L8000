"""
残り8000回 - タイトルロゴ(ドット絵 / コンセプト = 数字が減っていく)

構成:
  1段目 : 残り
  2段目 : [8000(下2/3を上に押し上げ)] [2px] [7999(上1/3)]  回(ボタン)

  「8000」は下から 2/3 だけ描いて 1/3 ぶん上へ押し上げる。空いた下 1/3 に
  「7999」の上 1/3 を 2px あけて置く。= オドメーターが回っている途中の見た目
  = 「残り回数が 1 減った瞬間」。

  「回」は文字ではなく "上から見た押しボタン": 灰色の四角 + 中央に赤い丸。
  このゲームは「ボタンをあと N 回押せる」話なので 回 をボタンにする。

  ゲーム本編と同じ x12y16pxMaruMonica を 32px(16 の倍数 = AA なし)でラスタライズ。
  「1 ラスタピクセル = 1 ドット」で組んで整数倍。

  出力 : Assets/Art/logo.png (透過 / Point / 圧縮なし)
  配色 : 文字は寒色の白。色みはボタンの赤/灰だけ(世界観を壊さない範囲の一点)。

deps: Pillow
"""
import os

from PIL import Image, ImageDraw, ImageFont

_ROOT = os.path.join(os.path.dirname(__file__), "..")
FONT = os.path.join(_ROOT, "Assets", "Fonts", "x12y16pxMaruMonica.ttf")
OUT = os.path.join(_ROOT, "Assets", "Art", "logo.png")

NATIVE = 32          # ラスタライズ画素
SCALE = 3            # 拡大率
MARGIN = 4           # 余白(ドット)
CUT = 0.30           # 上を隠す割合(≈ 1/3)。8000 は下 (1-CUT)、7999 は上 CUT。
ROLL_GAP = 2         # 8000 と 7999 の間(ドット)= 「2pix」
LINE_GAP = 3         # 1段目と2段目の間
BTN_GAP = 4          # 数字とボタンの間
RULE_DROP = 3
RULE_H = 2

INK   = (231, 237, 248, 255)   # ほぼ白・僅かに青(残り・8000)
INK2  = (206, 216, 232, 255)   # 7999(ほんの少しだけ淡く)
SHADOW = (12, 14, 22, 235)
RULE  = (116, 128, 152, 190)
BTN_RING = (122, 128, 140, 255)   # ボタンの灰
BTN_RED  = (198, 66, 58, 255)     # 押しボタンの赤(少しくすませる)
BTN_RED_HI = (226, 122, 112, 255)


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
    zan, zw, zh = glyph_mask("残り", NATIVE)
    n8, n8w, n8h = glyph_mask("8000", NATIVE)
    n7, n7w, n7h = glyph_mask("7999", NATIVE)

    third = round(n8h * CUT)                 # 上を隠す高さ
    keep8 = n8h - third                      # 8000 の下ぶん
    roll_h = keep8 + ROLL_GAP + third        # 2段目の数字ブロックの高さ
    btn = keep8 + 2                          # ボタンの一辺(現在の数字の高さに合わせる)
    indent = round(NATIVE * 0.62)            # 「　」ぶんの字下げ

    line2_w = n8w + BTN_GAP + btn
    content_w = max(zw, indent + line2_w)
    W = MARGIN * 2 + content_w
    rule_y = MARGIN + zh + LINE_GAP + max(roll_h, btn) + RULE_DROP
    H = rule_y + RULE_H + MARGIN

    grid = [[(0, 0, 0, 0)] * W for _ in range(H)]

    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H and c[3] != 0:
            grid[y][x] = c

    # ---- 1段目: 残り ----
    z_x, z_y = MARGIN, MARGIN
    for my in range(zh):
        for mx in range(zw):
            if zan[my][mx]:
                if my + 1 >= zh or not zan[my + 1][mx]:
                    put(z_x + mx, z_y + my + 1, SHADOW)
                put(z_x + mx, z_y + my, INK)

    # ---- 2段目 ----
    b_x = MARGIN + indent
    b_y = MARGIN + zh + LINE_GAP

    # 8000 の下 2/3 を、1/3 ぶん上へ(= b_y から描く)
    for my in range(third, n8h):
        for mx in range(n8w):
            if n8[my][mx]:
                put(b_x + mx, b_y + (my - third), INK)
    # 8000 の下辺に棚影
    for mx in range(n8w):
        if n8[n8h - 1][mx]:
            put(b_x + mx, b_y + keep8, SHADOW)

    # 空いた下 1/3 に 7999 の上 1/3(2px あけて)
    r_y = b_y + keep8 + ROLL_GAP
    for my in range(third):
        for mx in range(n7w):
            if n7[my][mx]:
                put(b_x + mx, r_y + my, INK2)

    # ---- 回 = 押しボタン(上から見た図)。現在の数字(8000 下ぶん)の中心に合わせる ----
    bx0 = b_x + n8w + BTN_GAP
    by0 = b_y + keep8 // 2 - btn // 2
    cx, cy = bx0 + btn / 2.0, by0 + btn / 2.0
    r_out = btn / 2.0 - 0.5
    r_red = btn / 2.0 - 4.0
    for yy in range(btn):
        for xx in range(btn):
            px, py = bx0 + xx, by0 + yy
            d = ((px + 0.5 - cx) ** 2 + (py + 0.5 - cy) ** 2) ** 0.5
            # 外枠(灰のリング)
            if btn // 2 - 1 <= max(abs(xx - btn // 2), abs(yy - btn // 2)) <= btn // 2:
                put(px, py, BTN_RING)
            # 中央の赤
            if d <= r_red:
                put(px, py, BTN_RED)
            elif d <= r_red + 1.2:
                put(px, py, BTN_RED_HI)
    # ボタンの棚影
    for xx in range(btn):
        put(bx0 + xx, by0 + btn, SHADOW)

    # ---- 底の罫線(= 登り出す ====== )----
    for yy in range(rule_y, rule_y + RULE_H):
        for x in range(1, W - 1):
            put(x, yy, RULE)

    # ---- 整数倍で拡大 ----
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
