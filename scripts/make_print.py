"""
残り8000回 - TGS 提出用の印刷物 2種(いずれも横向き)

  poster_a3.png  : A3横 キービジュアル(タイトル入り)。壁掛け。
  howto_a4.png   : A4横 遊び方。クリアファイルでテーブル設置。

方針(反AIスロップ / Hallmark):
  ・ゲーム本編と同じ素材だけで作る。フォントは x12y16pxMaruMonica、
    ドットは 16 の倍数でラスタライズして AA なし。
  ・配色は寒色モノクロ + 赤(押しボタン)の一点のみ。グラデも装飾も足さない。
  ・コピーはゲームの声(淡々・少し寂しい・乾いた冗談)。宣伝文句を書かない。
  ・余白を大きく、左揃え、明快な階層。印刷物としての作法。
  ・キービジュアル = 「エンドロールを下から登る」を1枚で。
    画面いっぱいの縦のクレジット、その途中に小さなプレイヤー、
    下端で断ち切れる「無」、上にタイトル。

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

BG      = (10, 11, 14)
INK     = (233, 239, 249)
MID     = (150, 162, 182)
DIM     = (98, 108, 126)
FAINT   = (52, 59, 73)
RULE    = (96, 106, 126)
RED     = (200, 66, 58)
VOID    = (5, 5, 7)

_fc = {}


def _raw(px16):
    if px16 not in _fc:
        _fc[px16] = ImageFont.truetype(FONT, px16)
    return _fc[px16]


def text_img(s, px, color=INK):
    """16px でラスタライズ → px 相当へニアレスト拡大した RGBA(ドット感を保つ)。"""
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
    paste(cv, text_img(s, px, color), x, y)


def dotrule(dr, x0, x1, y, color=RULE, seg=14, gap=10, w=3):
    x = x0
    while x < x1:
        dr.rectangle([x, y, min(x + seg, x1), y + w], fill=color)
        x += seg + gap


def credit_lines():
    out = []
    with open(os.path.join(_ROOT, "Assets", "Credits", "credits.txt"), encoding="utf-8") as fh:
        for ln in fh:
            ln = ln.strip()
            if not ln or ln.startswith(("//", "[", "~", "@", "=")):
                continue
            if ln.startswith("#"):
                out.append(("h", ln[1:].strip()))
            elif "::" in ln:
                role, name = ln.split("::", 1)
                out.append(("n", name.strip() + "  " + role.strip()))
            else:
                out.append(("n", ln))
    return out


# ---------------------------------------------------------------------------

def make_poster():
    W, H = int(420 * MM), int(297 * MM)
    cv = Image.new("RGBA", (W, H), BG + (255,))
    dr = ImageDraw.Draw(cv)
    pad = int(17 * MM)

    void_y = int(H * 0.85)
    top = pad

    # === 縦のエンドロール = 左の背骨。読ませるためでなく "登ってきた道の気配" ===
    band_l = pad + int(2 * MM)
    lines = credit_lines()
    step = int(8 * MM)
    y = top
    i = 0
    while y < void_y:
        kind, s = lines[i % len(lines)]
        t = (y - top) / (void_y - top)
        if kind == "h":
            c = MID if t < 0.5 else FAINT
        else:
            c = MID if t < 0.20 else DIM if t < 0.52 else FAINT if t < 0.85 else (34, 38, 47)
        g = text_img(s, 34 if kind == "h" else 18, c)
        paste(cv, g, band_l + (0 if kind == "h" else int(6 * MM)), y)
        y += step + (int(3 * MM) if kind == "h" else 0)
        i += 1

    # プレイヤー(白い1マス)+ 真上へのビーム。足場を1本、明るく。上から 40%
    py = top + int((void_y - top) * 0.40)
    ps = int(5 * MM)
    px_ = band_l + int(2 * MM)
    dr.rectangle([band_l - int(4 * MM), py + ps + int(1 * MM),
                  band_l + int(66 * MM), py + ps + int(3 * MM)], fill=RULE)     # 足場
    dr.rectangle([px_ + ps // 2 - 2, top + int(6 * MM), px_ + ps // 2 + 2, py],
                 fill=(214, 182, 92))                                          # ビーム(頂上へ)
    dr.rectangle([px_, py, px_ + ps, py + ps], fill=INK)

    # === ロゴ = 登り着く先。右上・大きく、画面の中心寄り ===
    logo = Image.open(os.path.join(ART, "logo.png")).convert("RGBA")
    lw = int(W * 0.44)
    logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.NEAREST)
    lx = int(W * 0.40)
    ly = pad + int(8 * MM)
    paste(cv, logo, lx, ly)

    # === コピー(ロゴの下・大きく)===
    cx = lx + int(2 * MM)
    cyy = ly + logo.height + int(20 * MM)
    txt(cv, "エンドロールを、", cx, cyy, 46, INK); cyy += int(19 * MM)
    txt(cv, "いちばん下から登る。", cx, cyy, 46, INK); cyy += int(28 * MM)
    for s in ["巻き戻しが止まる前に、頂上のタイトルへ。",
              "名前を壊せば、足場が空く。",
              "壊しすぎれば、世界のほうが歪む。"]:
        txt(cv, s, cx, cyy, 20, MID); cyy += int(9 * MM)

    # === 「無」= 下端でハードに断ち切れる ===
    dr.rectangle([0, void_y, W, H], fill=VOID)
    dr.rectangle([0, void_y, W, void_y + 6], fill=RULE)
    txt(cv, "下から、無がのぼってくる。止まると、飲まれる。", pad, void_y + int(9 * MM), 20, DIM)
    txt(cv, "残り8000回      Null Interactive      Tokyo Game Show 2026",
        W - pad - int(150 * MM), H - pad - int(1 * MM), 18, DIM)

    out = os.path.join(_ROOT, "print", "poster_a3.png")
    cv.convert("RGB").save(out, dpi=(DPI, DPI))
    print("poster:", out, cv.size)


def keycap(dr, cv, x, y, w, h, label):
    dr.rectangle([x, y, x + w, y + h], outline=RULE, width=3)
    dr.rectangle([x + 4, y + 4, x + w - 4, y + h - 4], fill=(19, 21, 26))
    g = text_img(label, 32, INK)
    paste(cv, g, x + (w - g.width) // 2, y + (h - g.height) // 2)


def make_howto():
    W, H = int(297 * MM), int(210 * MM)
    cv = Image.new("RGBA", (W, H), BG + (255,))
    dr = ImageDraw.Draw(cv)
    pad = int(14 * MM)

    # --- ヘッダ ---
    logo = Image.open(os.path.join(ART, "logo.png")).convert("RGBA")
    lw = int(42 * MM)
    logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.NEAREST)
    paste(cv, logo, pad, pad)
    hd = text_img("― 遊び方 ―", 32, MID)
    paste(cv, hd, W - pad - hd.width, pad + int(6 * MM))
    hr = pad + logo.height + int(3 * MM)
    dotrule(dr, pad, W - pad, hr, RULE)

    top = hr + int(12 * MM)
    colL = pad
    colR = int(W * 0.53)
    body = 24   # 本文

    # --- 左: 目的 ---
    y = top
    txt(cv, "目的", colL, y, 32, INK); y += int(13 * MM)
    for s in ["いちばん下 ―― ＝＝＝＝ / Thank you for playing。",
              "そこから スタッフロールを足場にして、",
              "いちばん上のタイトル「残り8000回」まで登る。",
              "",
              "頂上に触れると 巻き戻しが止まり、",
              "こんどは同じ道を降りて、最初の線を抜ける。"]:
        if s:
            txt(cv, s, colL, y, body, MID)
        y += int(8.5 * MM)

    y += int(7 * MM)
    txt(cv, "壊す・戻す", colL, y, 32, INK); y += int(13 * MM)
    for s, c in [("ビームで名前を撃つと、その担当の仕事が歪む。", MID),
                 ("音が歪む / 画面が傾く / 文字化け / 照明が落ちる …", DIM),
                 ("赤いボタンの担当の壁は、撃たないと開かない。", MID),
                 ("壊した所は戻らない。でも 直後の1つだけは", MID),
                 ("BackSpace で戻せる ―― 自分より上にあるうちは。", MID)]:
        txt(cv, s, colL, y, body if c == MID else 16, c)
        y += int(8.5 * MM)

    # --- 右: 操作 ---
    y = top
    txt(cv, "操作", colR, y, 32, INK); y += int(14 * MM)
    kw, kh = int(24 * MM), int(11 * MM)
    for key, desc in [("A  D", "左右に歩く"),
                      ("W", "ジャンプ(空中でもう1回)"),
                      ("Enter", "ビーム(まっすぐ上へ)"),
                      ("Back", "直前に壊した所を戻す")]:
        keycap(dr, cv, colR, y, kw, kh, key)
        d = text_img(desc, body, MID)
        paste(cv, d, colR + kw + int(7 * MM), y + (kh - d.height) // 2)
        y += kh + int(6 * MM)

    y += int(8 * MM)
    txt(cv, "残り回数", colR, y, 32, INK); y += int(13 * MM)
    for s, c in [("A / D / W / Enter / Back ―― 1入力で 1減る。", MID),
                 ("長押しでは減らない。押した回数だけ。", DIM),
                 ("下から「無」がのぼる。止まると飲まれ、", MID),
                 ("100消費して 中央へ戻される。", DIM),
                 ("0 になったら、指が止まる。", MID)]:
        txt(cv, s, colR, y, body if c == MID else 16, c)
        y += int(8.5 * MM)

    # --- 右端: たての図 ---
    gx = W - pad - int(6 * MM)
    gy0, gy1 = top, H - pad - int(10 * MM)
    dr.line([(gx, gy0), (gx, gy1)], fill=RULE, width=3)
    dr.rectangle([gx - int(10 * MM), gy0 - 3, gx + int(2 * MM), gy0 + 3], fill=INK)
    paste(cv, text_img("タイトル", 16, MID), gx - int(10 * MM), gy0 - int(7 * MM))
    for k in range(7):
        yy = gy0 + int((gy1 - gy0) * (0.16 + k * 0.11))
        xoff = int(7 * MM) if k % 2 else -int(7 * MM)
        dr.rectangle([gx + xoff - int(4 * MM), yy, gx + xoff + int(4 * MM), yy + 2], fill=DIM)
    dr.rectangle([gx - int(10 * MM), gy1 - 3, gx + int(2 * MM), gy1 + 3], fill=RULE)
    paste(cv, text_img("＝＝＝＝", 16, MID), gx - int(10 * MM), gy1 + int(3 * MM))
    ps = int(3 * MM)
    dr.rectangle([gx - int(7 * MM), gy1 - int(26 * MM), gx - int(7 * MM) + ps, gy1 - int(26 * MM) + ps], fill=INK)
    paste(cv, text_img("↑", 32, MID), gx - int(4 * MM), (gy0 + gy1) // 2)

    # --- フッタ ---
    fr = H - pad - int(3 * MM)
    dotrule(dr, pad, W - pad, fr, FAINT, seg=10, gap=10, w=2)
    txt(cv, "© 2026 tsubaki / Null Interactive      すべての名前は 架空です。", pad, fr + int(3 * MM), 16, FAINT)

    out = os.path.join(_ROOT, "print", "howto_a4.png")
    cv.convert("RGB").save(out, dpi=(DPI, DPI))
    print("howto :", out, cv.size)


if __name__ == "__main__":
    make_poster()
    make_howto()
