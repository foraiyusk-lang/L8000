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

# --- 配色: 黒地 + 白 + 赤の一点だけ。文字は原則まっ白、遠目でも読める ---
BG    = (8, 9, 11)        # 地(ゲームの黒)
INK   = (250, 250, 252)   # 主文字 = ほぼ純白
SOFT  = (216, 218, 224)   # 副文字(すこしだけ落とした白)
DIM   = (122, 125, 134)   # 遠景のクレジットのみ
LINE  = (92, 96, 108)     # 罫・目盛り
RED   = (224, 68, 56)     # 唯一の色。押しボタンの赤。強く使う
RED_HI = (244, 122, 108)
WARM  = RED               # ビームも赤(色は1色に絞る)
MID   = SOFT              # 後方互換(A4 が参照)
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


def credit_all():
    """credits.txt の「名前  役職」を全部(背景のスタッフロール用)。"""
    rows = []
    with open(os.path.join(_ROOT, "Assets", "Credits", "credits.txt"), encoding="utf-8") as fh:
        for ln in fh:
            ln = ln.strip()
            if ln.startswith(("//", "#", "=", "~", "[")) or "::" not in ln:
                continue
            role, name = [p.strip() for p in ln.split("::", 1)]
            if name and name != "???" and role != "???":
                rows.append(name + "   " + role)
    return rows


# 右側に置く本文(ゲームの声。宣伝しない。開催情報も書かない)。
POSTER_BODY = [
    "名前を ひとつ 撃つと、床が 一枚 でき、",
    "その人が 支えていた 何かが すこし 狂う。",
    "―― 音。光。重力。文字。",
    "",
    "のぼりきると、巻き戻りが 止まる。",
    "あとは 来た道を 降りるだけ。",
    "",
    "下からは ずっと、無が 来ている。",
    "捕まれば、大きく 戻される。",
    "",
    "何回 押せるか ―― それだけの ゲーム。",
]


# ===========================================================================
#  A3 横 : キービジュアル
# ===========================================================================

def make_poster():
    W, H = int(420 * MM), int(297 * MM)
    cv = Image.new("RGBA", (W, H), BG + (255,))
    dr = ImageDraw.Draw(cv)
    pad = int(18 * MM)

    void_y = int(H * 0.87)
    split_x = int(W * 0.50)            # 左=クレジットの壁 / 右=言葉

    logo = Image.open(os.path.join(ART, "logo.png")).convert("RGBA")
    lw = int(W * 0.30)
    logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.NEAREST)
    logo_x = int(W * 0.12)
    logo_y = pad
    logo_bot = logo_y + logo.height

    wall_l = int(W * 0.11)
    path_x = wall_l - int(9 * MM)      # ビーム = 壁のすぐ左を上へ

    # === 赤いビーム + プレイヤー。ロゴ(頂上)からプレイヤーまで1本 ===
    py = int(void_y - int(20 * MM))
    ps = int(10 * MM)
    dr.rectangle([path_x - int(4 * MM), logo_y + int(8 * MM),
                  path_x + int(4 * MM), py], fill=RED)

    # === 左半分 = スタッフロールそのもの。ロゴの下から名前がびっしり上へ ===
    roll = credit_all()
    lh = int(5.6 * MM)
    col_w = int((split_x - wall_l - int(4 * MM)) / 2)
    wall_top = logo_bot + int(9 * MM)
    wall_bot = py - int(8 * MM)
    ci, cy = 0, wall_top
    for s in roll:
        if ci >= 2:
            break
        txt(cv, s, wall_l + ci * col_w, cy, 18, (158, 161, 170))
        cy += lh
        if cy > wall_bot:
            ci, cy = ci + 1, wall_top

    # === ロゴ = 登り着く先。左上 ===
    paste(cv, logo, logo_x, logo_y)

    # プレイヤー(白1マス)+ 足場
    dr.rectangle([path_x - int(3 * MM), py + ps + int(2 * MM),
                  path_x + int(64 * MM), py + ps + int(2 * MM) + 6], fill=INK)
    dr.rectangle([path_x - ps // 2, py, path_x + ps // 2, py + ps], fill=INK)

    # === 右半分 = 言葉。うっすら明るい面 + 赤い縁で、壁から立ち上げる ===
    dr.rectangle([split_x, 0, W, void_y], fill=(22, 23, 28))
    dr.rectangle([split_x, 0, split_x + 8, void_y], fill=RED)

    tx = split_x + int(15 * MM)
    ty = pad + int(14 * MM)
    for s in ["エンドロールを、", "いちばん下から", "登る。"]:
        txt(cv, s, tx, ty, 96, INK)
        ty += int(24 * MM)
    ty += int(13 * MM)
    for s in POSTER_BODY:
        if s:
            txt(cv, s, tx, ty, 32, SOFT)
            ty += int(15 * MM)
        else:
            ty += int(9 * MM)

    # === 下端: 「無」。全幅をハードに断つ。縁は赤 ===
    dr.rectangle([0, void_y, W, H], fill=VOID)
    dr.rectangle([0, void_y, W, void_y + 9], fill=RED)
    txt(cv, "止まると、飲まれる。", wall_l, void_y + int(15 * MM), 44, INK)
    foot = text_img("残り8000回        Null Interactive", 28, SOFT)
    paste(cv, foot, W - pad - foot.width, H - pad - foot.height)

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
    y = block(colR, y, "残り回数", [
        "歩く・跳ぶ・撃つ ―― どれも 1回ぶん。",
        "長押しでも BackSpace でも 減らない。",
        "「無」に捕まると 100。0 で 指が止まる。",
    ])

    # TGS 出展の注記(赤で囲って目立たせる)
    y += int(4 * MM)
    note = "TGS 出店時には 時間の関係上 残り回数は 2000回 となっています"
    ng = text_img(note, 28, INK)
    dr.rectangle([colR - int(3 * MM), y - int(3 * MM),
                  colR + ng.width + int(6 * MM), y + ng.height + int(4 * MM)],
                 outline=RED, width=3)
    paste(cv, ng, colR + int(2 * MM), y + int(1 * MM))

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
