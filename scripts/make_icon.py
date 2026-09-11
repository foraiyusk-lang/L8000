"""
残り8000回 - アプリアイコン(.exe / タスクバー用)

ゲームの中心シンボル =「回(押しボタン)」を単独で置く。
  ・暗い背景に、上から見た押しボタン: 灰色の四角 + 中央の赤い丸。
  ・タイトルロゴ(make_logo.py)の「回」と同じ配色・同じ発想。
  ・小さく(16px)潰れても「四角＋赤い点」で識別できる、を最優先。

ロゴ本編と揃えるため native を整数倍したドット絵。色みは赤/灰の一点のみ。

出力: Assets/Art/icon.png (1024x1024 / 不透明 / Point)
deps: Pillow
"""
import os
from PIL import Image

_ROOT = os.path.join(os.path.dirname(__file__), "..")
OUT = os.path.join(_ROOT, "Assets", "Art", "icon.png")

NATIVE = 64          # ドット
SCALE = 16           # → 1024(Unity のアイコン最大スロット)
SIZE = NATIVE * SCALE

BG        = (11, 12, 16, 255)      # ほぼ黒(タイトル背景と同系)
BTN_RING  = (122, 128, 140, 255)   # ボタンの灰(make_logo と同じ)
BTN_TOP   = (150, 156, 168, 255)   # 天面の少し明るい灰
BTN_EDGE  = (78, 82, 92, 255)      # 縁の暗い線
SHADOW    = (6, 7, 11, 255)        # 棚影
BTN_RED   = (198, 66, 58, 255)     # 押しボタンの赤(make_logo と同じ)
BTN_RED_HI = (226, 122, 112, 255)


def main():
    img = Image.new("RGBA", (NATIVE, NATIVE), BG)
    px = img.load()

    # ボタンの正方形(中央、辺 = NATIVE の約 66%)
    side = 42
    x0 = (NATIVE - side) // 2
    y0 = (NATIVE - side) // 2 - 1
    cx = x0 + side / 2.0
    cy = y0 + side / 2.0

    # 棚影(下・右へ 2px)
    for yy in range(side + 2):
        for xx in range(side + 2):
            x, y = x0 + xx + 2, y0 + yy + 2
            if 0 <= x < NATIVE and 0 <= y < NATIVE:
                px[x, y] = SHADOW

    # ボタン本体
    for yy in range(side):
        for xx in range(side):
            x, y = x0 + xx, y0 + yy
            edge = (xx == 0 or yy == 0 or xx == side - 1 or yy == side - 1)
            top_band = yy < 3          # 天面のハイライト帯
            px[x, y] = BTN_EDGE if edge else (BTN_TOP if top_band else BTN_RING)

    # 中央の赤い丸
    r_red = side * 0.30
    for yy in range(side):
        for xx in range(side):
            x, y = x0 + xx, y0 + yy
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
            if d <= r_red:
                px[x, y] = BTN_RED
            elif d <= r_red + 1.1:
                px[x, y] = BTN_RED_HI

    big = img.resize((SIZE, SIZE), Image.NEAREST).convert("RGB")
    big.save(OUT)
    print("wrote", OUT, big.size)


if __name__ == "__main__":
    main()
