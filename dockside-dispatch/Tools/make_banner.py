"""YouTube channel banner for Dockside Dispatch: 2560 x 1440 (YouTube's recommended size).

    python Tools/make_banner.py   ->  store/marketing/youtube_banner_2560x1440.png

YouTube shows different parts of it: TVs show all of it, desktop a 2560 x 423 strip across the middle, and phones
only the centre 1546 x 423 "safe area". So the logo and words sit inside that safe area; the harbour skyline and
ships above and below it are for the TV view.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, "DocksideDispatch", "Assets", "_Game")
FONT_B = os.path.join(GAME, "Resources", "Fonts", "Fredoka-Bold.ttf")
FONT_S = os.path.join(GAME, "Resources", "Fonts", "Fredoka-SemiBold.ttf")
LOGO = os.path.join(GAME, "Branding", "DocksideDispatch_Logo.png")
SCENE = os.path.join(ROOT, "store", "raw", "clean_hongkong_sunset_2x.png")   # sunset harbour, HUD hidden
OUT = os.path.join(ROOT, "store", "marketing", "youtube_banner_2560x1440.png")

W, H = 2560, 1440
SAFE = ((W - 1546) // 2, (H - 423) // 2, 1546, 423)          # x, y, w, h
INK, CREAM, BLUE = (46, 36, 34), (255, 246, 230), (43, 116, 214)


def outlined(text, font, fill, stroke):
    d = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    l, t, r, b = d.textbbox((0, 0), text, font=font, stroke_width=stroke)
    img = Image.new("RGBA", (r - l, b - t), (0, 0, 0, 0))
    ImageDraw.Draw(img).text((-l, -t), text, font=font, fill=fill, stroke_width=stroke, stroke_fill=INK)
    return img


def soft_shadow(layer, blur, alpha):
    sh = Image.new("RGBA", layer.size, (30, 14, 40, 0))
    sh.putalpha(layer.split()[3].point(lambda v: int(v * alpha)))
    pad = blur * 3
    big = Image.new("RGBA", (layer.width + pad * 2, layer.height + pad * 2), (0, 0, 0, 0))
    big.paste(sh, (pad, pad))
    return big.filter(ImageFilter.GaussianBlur(blur)), pad


def main():
    # Background: the sunset skyline, water and ships from the portrait capture, widened to 16:9.
    scene = Image.open(SCENE).convert("RGB")
    crop_h = round(scene.width * H / W)
    top = 390                                                   # skyline at the top, ships along the bottom
    bg = scene.crop((0, top, scene.width, top + crop_h)).resize((W, H), Image.LANCZOS)
    # A soft dusk band across the middle so the words read on the bright water.
    a = np.zeros((H, W), float)
    y = np.arange(H)[:, None]
    a += 0.42 * np.exp(-((y - H / 2) / 260.0) ** 2)
    band = Image.new("RGB", (W, H), (58, 30, 70))
    bg = Image.composite(band, bg, Image.fromarray((a * 255).astype(np.uint8)))
    img = bg.convert("RGBA")

    sx, sy, sw, sh = SAFE
    logo = Image.open(LOGO).convert("RGBA")
    lh = 380
    logo = logo.resize((round(lh * logo.width / logo.height), lh), Image.LANCZOS)

    title = outlined("A calm harbour puzzle", ImageFont.truetype(FONT_B, 88), CREAM, 7)
    sub = outlined("Phone · Tablet · Android TV", ImageFont.truetype(FONT_S, 54), (255, 255, 255), 5)
    pf = ImageFont.truetype(FONT_B, 52)
    label = "Free on Google Play"
    d = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    tw = d.textlength(label, font=pf)
    pw, ph = int(tw + 90), 94
    pill = Image.new("RGBA", (pw, ph + 10), (0, 0, 0, 0))
    pd = ImageDraw.Draw(pill)
    pd.rounded_rectangle((0, 10, pw, ph + 9), ph // 2, fill=(20, 60, 120, 255))
    pd.rounded_rectangle((0, 0, pw, ph), ph // 2, fill=BLUE)
    asc, desc = pf.getmetrics()
    pd.text(((pw - tw) / 2, (ph - asc - desc) / 2 + 4), label, font=pf, fill=CREAM)

    gap = 50
    text_w = max(title.width, sub.width, pill.width)
    total_w = logo.width + gap + text_w
    x = sx + (sw - total_w) // 2
    assert total_w <= sw - 80, f"content {total_w}px is wider than the safe area"
    # logo, vertically centred on the safe area
    lx, ly = x, sy + (sh - logo.height) // 2
    shd, pad = soft_shadow(logo, 18, 0.45)
    img.alpha_composite(shd, (lx - pad, ly - pad + 12))
    img.alpha_composite(logo, (lx, ly))
    # words, stacked and centred on the safe area
    tx = x + logo.width + gap
    stack = [title, sub, pill]
    gaps = [18, 34]
    th = sum(i.height for i in stack) + sum(gaps)
    ty = sy + (sh - th) // 2
    for i, it in enumerate(stack):
        shd, pad = soft_shadow(it, 10, 0.5 if it is not pill else 0.35)
        img.alpha_composite(shd, (tx - pad, ty - pad + 6))
        img.alpha_composite(it, (tx, ty))
        ty += it.height + (gaps[i] if i < len(gaps) else 0)

    img.convert("RGB").save(OUT, optimize=True)
    print(OUT, os.path.getsize(OUT) // 1024, "KB")


if __name__ == "__main__":
    main()
