"""
残り8000回 - TGS 提出用の印刷物 2種(いずれも横向き)

  poster_a3.png  : A3横 キービジュアル(タイトル入り)。壁に掛ける。
  howto_a4.png   : A4横 遊び方。クリアファイルに入れてテーブルに置く。

方針(反AIスロップ / Hallmark):
  ・素材はゲーム本編と同じものだけ。フォントは x12y16pxMaruMonica を
    16 の倍数でラスタライズ(= AA なし・素のドット)。
  ・配色は寒色モノクロ + 赤(押しボタン)の一点のみ。グラデーションを使わない。
  ・文字は「離れて / クリアファイル越しに」読める大きさを最優先。
    級数は 本文32 / 見出し48 / タグライン64 の3段だけ。中間色で潰さない。
  ・コピーはゲームの声(淡々・少し寂しい・乾いた冗談)。宣伝文句は書かない。
  ・余白は大きく、左揃え、階層は級数と色の2つだけで作る。

出力 : print/poster_a3.png / print/howto_a4.png (200dpi)
deps : Pillow
"""
import os

from PIL import Image, ImageDraw, ImageFont

_ROOT = os.path.join(os.path.dirname(__file__), "..")
FONT = os.path.join(_ROOT, "Assets", "Fonts", "x12y16pxMaruMonica.ttf")
ART = os.path.join(_ROOT, "Assets", "Art")

DPI = 200
MM = DPI / 25.4

# --- 配色: 黒地 + 寒色の白 + 赤の一点。中間トーンは「読める」明るさに寄せる ---
BG    = (9, 10, 13)       # 地(ゲームの黒)
INK   = (235, 241, 250)   # 主文字
MID   = (190, 200, 216)   # 副文字(これでも普通に読める明るさ)
DIM   = (146, 156, 174)   # 補足(32px 未満では使わない)
LINE  = (108, 118, 138)   # 罫・目盛り
RED   = (214, 84, 72)     # 押しボタンの赤(唯一の色み)
WARM  = (223, 191, 108)   # ビーム
VOID  = (4, 4, 6)         # 「無」

_fc = {}


def _raw(px16):
    if px16 not in _fc:
        _fc[px16] = ImageFont.truetype(FONT, px16)
    return _fc[px16]


def text_img(s, px, color=INK):
    """16px でラスタライズ → px へニアレスト拡大(px は 16 の倍数推奨 = 整数倍で綺麗)。"""
    f = _raw(16)
    tmp = ImageDraw.Draw(Image.new("L", (4, 4)))
    b = tmp.textbbox((0, 0), s, font=f)
    w, h = max(1, b[2] - b[0]), max(1, b[3] - b[1])
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).text((-b[0], -b[1]), s, fill=255, font=f)
    k = px / 16.0
    if abs(k - 1.0) > 0.01:
        m = m.resize((max(1, round(w * k)), max(1, round(h * k))), Image.NEAREST)
    out = Image.new("RGBA", m.size, (0, 0, 0, 0))
    out.paste(Image.new("RGBA", m.size, color + (255,)), (0, 0), m)
    return out


def paste(cv, img, x, y):
    cv.alpha_composite(img, (int(x), int(y)))


def txt(cv, s, x, y, px, color=INK):
    g = text_img(s, px, color)
    paste(cv, g, x, y)
    return g.height


def dotrule(dr, x0, x1, y, color=LINE, seg=16, gap=12, w=3):
    x = x0
    while x < x1:
        dr.rectangle([x, y, min(x + seg, x1), y + w], fill=color)
        x += seg + gap


# キービジュアルの梯子 = 足場にする「仕事」。credits.txt に実在する役職から、
# 壊すと歪みが起きる担当を上から順に(いちばん下は The Producer)。
LADDER_ROLES = [
    "Lead Game Designer",
    "Physics Programmer",
    "Rendering Engineer",
    "Music",
    "Sound Director",
    "Lighting Artist",
    "The Producer",
]


# ===========================================================================
#  A3 横 : キービジュアル
# ===========================================================================

def make_poster():
    W, H = int(420 * MM), int(297 * MM)
    cv = Image.new("RGBA", (W, H), BG + (255,))
    dr = ImageDraw.Draw(cv)
    pad = int(22 * MM)

    void_y = int(H * 0.855)
    axis_x = int(W * 0.36)              # 登りの軸(画面の中央やや左)

    # --- ロゴ = 登り着く先。軸の真上に置く ---
    logo = Image.open(os.path.join(ART, "logo.png")).convert("RGBA")
    lw = int(W * 0.30)
    logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.NEAREST)
    logo_x = axis_x - int(lw * 0.34)
    logo_y = pad
    paste(cv, logo, logo_x, logo_y)

    ladder_top = logo_y + logo.height + int(20 * MM)
    ladder_bot = void_y - int(20 * MM)

    # --- 梯子 = 足場にした「仕事」。軸に沿って役職名を並べる ---
    dr.line([(axis_x, ladder_top - int(6 * MM)), (axis_x, ladder_bot + int(8 * MM))],
            fill=LINE, width=3)
    rungs = LADDER_ROLES
    n = max(1, len(rungs))
    player_i = n - 2
    for i, s in enumerate(rungs):
        ry = ladder_top + int((ladder_bot - ladder_top) * i / max(1, n - 1))
        near = abs(i - player_i) <= 1
        dr.rectangle([axis_x - int(5 * MM), ry + int(5 * MM),
                      axis_x + int(5 * MM), ry + int(5 * MM) + 3], fill=LINE)
        txt(cv, s, axis_x + int(13 * MM), ry, 32, INK if near else MID)

    # 足場 + プレイヤー(白1マス)+ ロゴへ伸びるビーム
    py = ladder_top + int((ladder_bot - ladder_top) * player_i / max(1, n - 1))
    ps = int(7 * MM)
    dr.rectangle([axis_x - int(9 * MM), py + ps + int(2 * MM),
                  axis_x + int(74 * MM), py + ps + int(2 * MM) + 4], fill=INK)
    dr.rectangle([axis_x + ps // 2 - 3, logo_y + logo.height - int(2 * MM),
                  axis_x + ps // 2 + 3, py], fill=WARM)
    dr.rectangle([axis_x, py, axis_x + ps, py + ps], fill=INK)

    # 梯子の下端 = 最初の線
    txt(cv, "＝＝＝＝   Thank you for playing.",
        axis_x + int(13 * MM), ladder_bot + int(4 * MM), 32, MID)

    # --- タグライン = 右上の空きを使う(梯子の上部と高さを合わせる)---
    tx = int(W * 0.57)
    ty = int(H * 0.17)
    ty += txt(cv, "エンドロールを、", tx, ty, 64, INK) + int(12 * MM)
    ty += txt(cv, "いちばん下から登る。", tx, ty, 64, INK) + int(24 * MM)
    for s in ["巻き戻しが止まる前に、頂上のタイトルへ。",
              "名前を壊せば、足場が空く。",
              "壊しすぎれば、世界のほうが歪む。"]:
        ty += txt(cv, s, tx, ty, 32, MID) + int(9 * MM)

    # --- 下端: 「無」。ハードに断ち切る ---
    dr.rectangle([0, void_y, W, H], fill=VOID)
    dr.rectangle([0, void_y, W, void_y + 7], fill=LINE)
    txt(cv, "下から、無がのぼってくる。止まると、飲まれる。",
        pad, void_y + int(12 * MM), 32, MID)
    foot = "残り8000回   ／   Null Interactive   ／   Tokyo Game Show 2026"
    fg = text_img(foot, 32, DIM)
    paste(cv, fg, W - pad - fg.width, H - pad - fg.height)

    out = os.path.join(_ROOT, "print", "poster_a3.png")
    cv.convert("RGB").save(out, dpi=(DPI, DPI))
    print("poster:", out, cv.size)


# ===========================================================================
#  A4 横 : 遊び方
# ===========================================================================

def keycap(dr, cv, x, y, w, h, label):
    dr.rectangle([x, y, x + w, y + h], outline=LINE, width=3)
    dr.rectangle([x + 5, y + 5, x + w - 5, y + h - 5], fill=(17, 19, 24))
    g = text_img(label, 32, INK)
    paste(cv, g, x + (w - g.width) // 2, y + (h - g.height) // 2)


def make_howto():
    W, H = int(297 * MM), int(210 * MM)
    cv = Image.new("RGBA", (W, H), BG + (255,))
    dr = ImageDraw.Draw(cv)
    pad = int(16 * MM)

    # --- ヘッダ ---
    logo = Image.open(os.path.join(ART, "logo.png")).convert("RGBA")
    lw = int(38 * MM)
    logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.NEAREST)
    paste(cv, logo, pad, pad)
    hd = text_img("― 遊び方 ―", 48, MID)
    paste(cv, hd, W - pad - hd.width, pad + int(4 * MM))
    hr = pad + logo.height + int(4 * MM)
    dotrule(dr, pad, W - pad, hr, LINE)

    top = hr + int(12 * MM)
    colL = pad
    colR = int(W * 0.52)
    BODY, HEAD = 32, 48
    lh = int(9 * MM)            # 本文の行送り

    def block(x, y, head, rows):
        txt(cv, head, x, y, HEAD, INK)
        y += int(13 * MM)
        for s in rows:
            if s:
                txt(cv, s, x, y, BODY, MID)
            y += lh
        return y

    # --- 左 ---
    y = block(colL, top, "目的", [
        "いちばん下の ＝＝＝＝ から、",
        "スタッフロールを足場にして いちばん上の",
        "タイトル「残り8000回」まで登る。",
        "触れたら 来た道を降りて、最初の線を抜ける。",
    ])
    y += int(8 * MM)
    block(colL, y, "壊す・戻す", [
        "名前を撃つと そこが足場になる。",
        "かわりに その担当の仕事が歪む。",
        "―― 音 / 傾き / 文字化け / 照明 …",
        "赤いボタンの壁は 撃たないと開かない。",
        "BackSpace ―― 直前の1つだけ 戻せる。",
    ])

    # --- 右: 操作 ---
    y = top
    txt(cv, "操作", colR, y, HEAD, INK)
    y += int(13 * MM)
    kw, kh = int(30 * MM), int(11 * MM)
    for key, desc in [("A  D", "左右に歩く"),
                      ("W", "ジャンプ(空中でもう一度)"),
                      ("Enter", "まっすぐ上へ ビーム"),
                      ("Back", "直前に壊した所を戻す")]:
        keycap(dr, cv, colR, y, kw, kh, key)
        d = text_img(desc, BODY, MID)
        paste(cv, d, colR + kw + int(8 * MM), y + (kh - d.height) // 2)
        y += kh + int(4 * MM)

    y += int(7 * MM)
    block(colR, y, "残り回数", [
        "歩く・跳ぶ・撃つ ―― どれも 1回ぶん。",
        "長押しでも BackSpace でも 減らない。",
        "「無」に捕まると 100。0 で 指が止まる。",
    ])

    # --- フッタ ---
    fr = H - pad - int(4 * MM)
    dotrule(dr, pad, W - pad, fr, LINE, seg=12, gap=12, w=2)
    txt(cv, "© 2026 tsubaki  ／  Null Interactive        すべての名前は 架空です。",
        pad, fr + int(4 * MM), BODY, DIM)

    out = os.path.join(_ROOT, "print", "howto_a4.png")
    cv.convert("RGB").save(out, dpi=(DPI, DPI))
    print("howto :", out, cv.size)


if __name__ == "__main__":
    make_poster()
    make_howto()
