"""Builds the Dockside Dispatch promo videos from gameplay recorded with Tools/promo/rec.py.

    python Tools/promo/make_video.py <clips_dir> [vertical|landscape|all] [--preview]

Writes store/video/dockside_promo_vertical_1080x1920.mp4 (Shorts, Reels) and
store/video/dockside_promo_landscape_1920x1080.mp4 (YouTube, the Play listing video), H.264 + AAC, 30 fps.
The soundtrack is the game's own sound effects at the moments they played, over a calm music bed synthesised
here (original, no licence needed). --preview writes a contact sheet of key frames instead of the video.
"""
import math, os, subprocess, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import scipy.io.wavfile as wavfile
import imageio_ffmpeg

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GAME = os.path.join(ROOT, "DocksideDispatch", "Assets", "_Game")
FONT_B = os.path.join(GAME, "Resources", "Fonts", "Fredoka-Bold.ttf")
FONT_S = os.path.join(GAME, "Resources", "Fonts", "Fredoka-SemiBold.ttf")
LOGO = os.path.join(GAME, "Branding", "DocksideDispatch_Logo.png")
AUDIO = os.path.join(GAME, "Resources", "Audio")
CLEAN_BG = os.path.join(ROOT, "store", "raw", "clean_hongkong_sunset_2x.png")
QR = os.path.join(ROOT, "store", "marketing", "qr", "dockside_play_qr.png")   # the Play listing URL
OUT = os.path.join(ROOT, "store", "video")
FPS, SR, XF = 30, 48000, 9          # frame rate, audio rate, crossfade frames
INK, CREAM, BLUE = (46, 36, 34), (255, 246, 230), (43, 116, 214)

# A scene: clip folder (None = end card), where it starts in the clip, how long it runs (seconds), the caption, and
# the background gradient. Start is a frame number, or (sound, offset seconds) = relative to the first time that
# sound played in the clip (e.g. ("win", -3) = three seconds before the level was won).
VERTICAL = [
    dict(clip="load_hk_sunset_p", start=40, dur=4.6, title="Think one\nmove ahead", bg=("#FF9F7E", "#B86A9A")),
    dict(clip="bay_mum_afternoon_p", start=("horn", -1.6), dur=3.2, title="Fill a ship.\nSend it off!", bg=("#FFB27E", "#C0628A")),
    dict(clip="hard_sg_midday_p", start=172, dur=3.6, title="Park tricky ones\nin the holding bay", bg=("#F6C27A", "#D9834F")),
    dict(clip="special_mum_morn_p", start=48, dur=3.8, title="Keys, locks and\nmystery boxes", bg=("#9ED7F0", "#4C9BD6")),
    dict(clip="hard_sg_midday_p", start=("win", -2.3), dur=3.9, title="Every 5th level\nfights back", bg=("#F08A6E", "#C2453A")),
    dict(clip="unload_dxb_night_p", start=("horn", -1.4), dur=4.0, title="A second mode:\nunload to trucks", bg=("#6F5A8E", "#2E2442")),
    dict(clip="boosters_sunset_p", start=("berth", -0.5), dur=5.4, title="Boosters when\nyou're stuck", bg=("#FF9F7E", "#B86A9A")),
    dict(clip=None, dur=5.0),
]

LANDSCAPE = [
    dict(clip="load_hk_sunset_l", start=40, dur=6.0, logo=True, title="Think one\nmove ahead",
         sub="Send every container to the ship of its colour", bg=("#FF9F7E", "#B86A9A")),
    dict(clip="bay_mum_afternoon_l", start=("horn", -2.2), dur=4.4, title="Fill a ship.\nSend it off!",
         sub="Ships keep arriving: plan the order", bg=("#FFB27E", "#C0628A")),
    dict(clip="hard_sg_midday_l", start=165, dur=4.4, title="Use the\nholding bay",
         sub="Park a container until its ship comes in", bg=("#F6C27A", "#D9834F")),
    dict(clip="special_mum_morn_l", start=44, dur=5.4, title="Keys, locks\nand mysteries",
         sub="Long containers, locked ones, and colours that hide until you reach them", bg=("#9ED7F0", "#4C9BD6")),
    dict(clip="hard_sg_midday_l", start=("win", -3.5), dur=5.4, title="Every 5th level\nfights back",
         sub="Hard levels pay extra coins", bg=("#F08A6E", "#C2453A")),
    dict(clip="unload_dxb_night_l", start=("horn", -2.0), dur=5.6, title="Mode 2:\nUnload",
         sub="Unload the ships onto the trucks waiting in the lanes", bg=("#6F5A8E", "#2E2442")),
    dict(clip="boosters_sunset_l", start=("berth", -0.6), dur=5.6, title="Boosters\nwhen stuck",
         sub="Extra Berth, Crane Lift and Undo", bg=("#FF9F7E", "#B86A9A")),
    dict(clip="tv_load_night_l", start=60, dur=6.0, tv=True, title="Play on\nyour TV",
         sub="Android TV with the remote. No ads on TV.", bg=("#5A6FA0", "#232A44")),
    dict(clip="menu_l", start=30, dur=5.0, title="200 levels,\n20 world ports",
         sub="Day and night skies. No timers.", bg=("#79C0EE", "#3F9FE3")),
    dict(clip=None, dur=6.5),
]


def hexrgb(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def ease(t): return 1 - (1 - max(0.0, min(1.0, t))) ** 3


def gradient(w, h, top, bottom):
    a, b = np.array(hexrgb(top), float), np.array(hexrgb(bottom), float)
    t = np.linspace(0, 1, h)[:, None, None]
    img = (a * (1 - t) + b * t).repeat(w, axis=1)
    # soft light in the upper left so the background isn't flat
    yy, xx = np.mgrid[0:h, 0:w]
    glow = np.exp(-(((xx - w * 0.25) / (w * 0.6)) ** 2 + ((yy - h * 0.1) / (h * 0.5)) ** 2))[..., None] * 40
    return Image.fromarray(np.clip(img + glow, 0, 255).astype(np.uint8), "RGB")


def rounded_mask(size, r):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), r, fill=255)
    return m


def shadow(size, r, blur, alpha=110):
    pad = blur * 3
    s = Image.new("RGBA", (size[0] + pad * 2, size[1] + pad * 2), (0, 0, 0, 0))
    ImageDraw.Draw(s).rounded_rectangle((pad, pad, pad + size[0], pad + size[1]), r, fill=(20, 12, 30, alpha))
    return s.filter(ImageFilter.GaussianBlur(blur)), pad


def fit_font(lines, path, size, width, stroke=0):
    """The largest font up to `size` at which every line fits in `width`."""
    d = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    while size > 30:
        f = ImageFont.truetype(path, size)
        if max(d.textlength(l, font=f) for l in lines) + 2 * stroke <= width: return f
        size -= 2
    return ImageFont.truetype(path, size)


def text_block(lines, font, fill, width, align="center", spacing=0.12, stroke=0):
    """Renders lines of text into an RGBA image `width` wide."""
    asc, desc = font.getmetrics()
    lh = int((asc + desc) * (1 + spacing))
    img = Image.new("RGBA", (width, lh * len(lines) + 10 + 2 * stroke), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i, ln in enumerate(lines):
        tw = d.textlength(ln, font=font)
        x = (width - tw) / 2 if align == "center" else stroke
        d.text((x, i * lh + stroke), ln, font=font, fill=fill, stroke_width=stroke, stroke_fill=INK)
    return img


class Clip:
    def __init__(self, folder):
        self.dir = folder
        self.n = len([f for f in os.listdir(folder) if f.endswith(".jpg")])
        self.sfx = []
        p = os.path.join(folder, "sfx.csv")
        if os.path.exists(p):
            for line in open(p):
                f, name, vol = line.strip().split(",")
                self.sfx.append((int(f), name, float(vol)))

    def frame(self, i):
        return Image.open(os.path.join(self.dir, f"{max(0, min(self.n - 1, i)):05d}.jpg")).convert("RGB")

    def anchor(self, start):
        if isinstance(start, int): return start
        name, off = start
        hits = [f for f, n, _ in self.sfx if n == name]
        if not hits: raise SystemExit(f"{self.dir}: no '{name}' sound to anchor on")
        return max(0, hits[0] + int(off * FPS))


class Renderer:
    def __init__(self, clips_dir, scenes, w, h):
        self.w, self.h = w, h
        self.vertical = h > w
        self.scenes = scenes
        self.clips = {}
        self.logo = Image.open(LOGO).convert("RGBA")
        for s in scenes:
            if s["clip"]:
                if s["clip"] not in self.clips: self.clips[s["clip"]] = Clip(os.path.join(clips_dir, s["clip"]))
                s["c"] = self.clips[s["clip"]]
                s["f0"] = s["c"].anchor(s["start"])
            s["n"] = int(round(s["dur"] * FPS))
        # scene k starts XF frames before scene k-1 ends (crossfade)
        t = 0
        for s in scenes:
            s["g0"] = t
            t += s["n"] - XF
        self.total = t + XF
        self._layout()
        for s in scenes: self._prepare(s)

    # ----- layout -----
    def _layout(self):
        w, h = self.w, self.h
        if self.vertical:
            fw = 820; fh = round(fw * 16 / 9)
            self.box = ((w - fw) // 2, h - fh - 54, fw, fh)       # footage
            self.cap = (60, 70, w - 120, self.box[1] - 70 - 24)  # caption area
            self.title_font = ImageFont.truetype(FONT_B, 92)
            self.sub_font = ImageFont.truetype(FONT_S, 44)
        else:
            fw = 1200; fh = round(fw * 9 / 16)
            self.box = (w - fw - 90, (h - fh) // 2, fw, fh)
            self.cap = (80, 120, self.box[0] - 80 - 60, h - 240)
            self.title_font = ImageFont.truetype(FONT_B, 84)
            self.sub_font = ImageFont.truetype(FONT_S, 42)
        self.radius = 44 if self.vertical else 30
        self.mask = rounded_mask(self.box[2:], self.radius)
        self.shadow, self.shadow_pad = shadow(self.box[2:], self.radius, 26)

    def _prepare(self, s):
        w, h = self.w, self.h
        if s["clip"] is None:
            self._prepare_end(s)
            return
        s["bgimg"] = gradient(w, h, *s["bg"])
        cx, cy, cw, ch = self.cap
        parts = []
        if s.get("logo"):
            lw = 420 if self.vertical else 470
            parts.append(("logo", self.logo.resize((lw, round(lw * self.logo.height / self.logo.width)), Image.LANCZOS)))
        lines = s["title"].split("\n")
        tf = fit_font(lines, FONT_B, self.title_font.size, cw, stroke=6)
        parts.append(("title", text_block(lines, tf, CREAM, cw, "center" if self.vertical else "left", stroke=6)))
        if s.get("sub") and not self.vertical:
            parts.append(("sub", text_block(wrap(s["sub"], self.sub_font, cw), self.sub_font, (255, 255, 255, 235), cw, "left")))
        gap = 26
        th = sum(p.height for _, p in parts) + gap * (len(parts) - 1)
        if self.vertical and s.get("logo"):      # logo + 2-line title: shrink to fit above the footage
            scale = min(1.0, ch / th)
            if scale < 1:
                parts = [(k, p.resize((round(p.width * scale), round(p.height * scale)), Image.LANCZOS)) for k, p in parts]
                th = sum(p.height for _, p in parts) + gap * (len(parts) - 1)
        y = cy + (ch - th) // 2
        s["parts"] = []
        for k, p in parts:
            x = cx + (cw - p.width) // 2 if (self.vertical or k == "logo") else cx
            if k == "logo" and not self.vertical: x = cx - 10
            s["parts"].append((p, x, y))
            y += p.height + gap
        # text shadow layer for legibility on bright gradients
        s["tshadow"] = []
        for p, x, y in s["parts"]:
            a = p.split()[3].filter(ImageFilter.GaussianBlur(6))
            sh = Image.new("RGBA", p.size, (40, 20, 40, 0)); sh.putalpha(a.point(lambda v: v * 0.55))
            s["tshadow"].append((sh, x, y + 5))
        if s.get("tv"):
            s["tvframe"] = self._tv_frame()

    def _tv_frame(self):
        x, y, fw, fh = self.box
        b = 22
        img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.rounded_rectangle((x - b, y - b, x + fw + b, y + fh + b), 18, fill=(24, 24, 30, 255))
        d.rounded_rectangle((x + fw // 2 - 120, y + fh + b, x + fw // 2 + 120, y + fh + b + 14), 4, fill=(38, 38, 46, 255))
        d.rectangle((x + fw // 2 - 26, y + fh + b - 2, x + fw // 2 + 26, y + fh + b + 6), fill=(38, 38, 46, 255))
        return img

    def _prepare_end(self, s):
        w, h = self.w, self.h
        bg = Image.open(CLEAN_BG).convert("RGB")
        scale = max(w / bg.width, h / bg.height)
        bg = bg.resize((math.ceil(bg.width * scale), math.ceil(bg.height * scale)), Image.LANCZOS)
        bg = bg.crop(((bg.width - w) // 2, (bg.height - h) // 2, (bg.width - w) // 2 + w, (bg.height - h) // 2 + h))
        grad = gradient(w, h, "#F7956F", "#4A3768")
        s["bgimg"] = Image.blend(bg.filter(ImageFilter.GaussianBlur(14)), grad, 0.72)
        lw = 520 if self.vertical else 700
        s["logo"] = self.logo.resize((lw, round(lw * self.logo.height / self.logo.width)), Image.LANCZOS)
        tf = ImageFont.truetype(FONT_B, 72 if self.vertical else 66)
        sf = ImageFont.truetype(FONT_S, 50 if self.vertical else 46)
        s["line1"] = text_block(["A calm harbour puzzle"], tf, CREAM, w, stroke=5)
        s["line2"] = text_block(["Phone · Tablet · TV"], sf, (255, 255, 255, 230), w)
        # "Free on Google Play" pill
        pf = ImageFont.truetype(FONT_B, 54 if self.vertical else 50)
        label = "Free on Google Play"
        tw = ImageDraw.Draw(Image.new("RGB", (1, 1))).textlength(label, font=pf)
        pw, ph = int(tw + 110), 112 if self.vertical else 104
        pill = Image.new("RGBA", (pw, ph + 10), (0, 0, 0, 0))
        d = ImageDraw.Draw(pill)
        d.rounded_rectangle((0, 10, pw, ph + 9), ph // 2, fill=(20, 60, 120, 255))
        d.rounded_rectangle((0, 0, pw, ph), ph // 2, fill=BLUE)
        asc, desc = pf.getmetrics()
        d.text(((pw - tw) / 2, (ph - asc - desc) / 2 + 4), label, font=pf, fill=CREAM)
        s["pill"] = pill
        s["qr"] = qr_card(380, 40)
        # Vertical: one centred stack ending with the QR card. Landscape: the stack on the left, the QR card on the right.
        items = [s["logo"], s["line1"], s["line2"], s["pill"]] + ([s["qr"]] if self.vertical else [])
        gaps = [36, 6, 50, 50] if self.vertical else [34, 0, 46]
        total = sum(i.height for i in items) + sum(gaps[:len(items) - 1])
        y = (h - total) // 2 - (100 if self.vertical else 10)   # vertical: clear of the Reels/Shorts caption bar
        cx = w // 2 if self.vertical else int(w * 0.36)
        s["pos"] = []
        for i, it in enumerate(items):
            s["pos"].append((cx, y))
            y += it.height + (gaps[i] if i < len(gaps) else 0)
        if not self.vertical:
            s["pos"].append((int(w * 0.77), (h - s["qr"].height) // 2))

    # ----- drawing -----
    def scene_frame(self, s, t):
        """Frame t (0-based, may run past the end during a crossfade) of scene s."""
        if s["clip"] is None:
            return self._end_frame(s, t)
        img = s["bgimg"].copy()
        x, y, fw, fh = self.box
        if s.get("tv"):
            img.paste(s["tvframe"], (0, 0), s["tvframe"])
        else:
            img.paste(self.shadow, (x - self.shadow_pad, y - self.shadow_pad + 16), self.shadow)
        foot = s["c"].frame(s["f0"] + t)
        if foot.size != (fw, fh): foot = foot.resize((fw, fh), Image.LANCZOS)
        img.paste(foot, (x, y), self.mask if not s.get("tv") else None)
        # caption: each part rises and fades in, a few frames apart
        for i, ((p, px, py), (sh, sx, sy)) in enumerate(zip(s["parts"], s["tshadow"])):
            k = ease((t - 4 - i * 5) / 12)
            if k <= 0: continue
            dy = int((1 - k) * 34)
            self._alpha_paste(img, sh, sx, sy + dy, k)
            self._alpha_paste(img, p, px, py + dy, k)
        return img

    def _end_frame(self, s, t):
        img = s["bgimg"].copy()
        w = self.w
        for i, (it, (cx, y)) in enumerate(zip([s["logo"], s["line1"], s["line2"], s["pill"], s["qr"]], s["pos"])):
            k = ease((t - 2 - i * 6) / 14)
            if k <= 0: continue
            if i == 0:   # logo pops in
                sc = 0.82 + 0.18 * k + 0.05 * math.sin(min(1, k) * math.pi)
                lg = it.resize((max(1, int(it.width * sc)), max(1, int(it.height * sc))), Image.LANCZOS)
                self._alpha_paste(img, lg, cx - lg.width // 2, y + (it.height - lg.height) // 2, k)
            elif i == 3:  # the pill breathes once it's in
                pulse = 1 + 0.03 * math.sin(max(0, t - 40) / FPS * 2 * math.pi * 0.9) if t > 40 else 1
                pl = it.resize((int(it.width * pulse), int(it.height * pulse)), Image.LANCZOS)
                self._alpha_paste(img, pl, cx - pl.width // 2, y + (it.height - pl.height) // 2 + int((1 - k) * 30), k)
            elif i == 4:  # QR card scales up from 90% and then holds still, so phones can focus on it
                sc = 0.9 + 0.1 * k
                q = it if k >= 1 else it.resize((int(it.width * sc), int(it.height * sc)), Image.LANCZOS)
                self._alpha_paste(img, q, cx - q.width // 2, y + (it.height - q.height) // 2, k)
            else:
                self._alpha_paste(img, it, cx - it.width // 2, y + int((1 - k) * 30), k)
        return img

    @staticmethod
    def _alpha_paste(img, layer, x, y, k):
        if k >= 1:
            img.paste(layer, (x, y), layer)
        else:
            a = layer.split()[3].point(lambda v: int(v * k))
            img.paste(layer, (x, y), a)

    def frame(self, g):
        cur = max(i for i, s in enumerate(self.scenes) if s["g0"] <= g)
        s = self.scenes[cur]
        img = self.scene_frame(s, g - s["g0"])
        if cur > 0 and g - s["g0"] < XF:
            p = self.scenes[cur - 1]
            prev = self.scene_frame(p, g - p["g0"])
            img = Image.blend(prev, img, ease((g - s["g0"] + 1) / XF))
        # fade from / to black at the very start and end
        if g < 6: img = Image.blend(Image.new("RGB", img.size), img, g / 6)
        if g > self.total - 10: img = Image.blend(Image.new("RGB", img.size), img, max(0, self.total - g) / 10)
        return img

    # ----- audio -----
    def audio(self):
        n = int(self.total / FPS * SR) + SR
        out = np.zeros(n)
        cache = {}
        for s in self.scenes:
            if s["clip"] is None:
                continue
            g_from, g_to = s["g0"], s["g0"] + s["n"]
            for f, name, vol in s["c"].sfx:
                local = f - s["f0"]
                if local < 0 or local >= s["n"] - XF // 2: continue
                if name not in cache: cache[name] = load_wav(os.path.join(AUDIO, name + ".wav"))
                clip = cache[name] * vol * 0.75
                at = int((g_from + local) / FPS * SR)
                end = min(n, at + len(clip))
                out[at:end] += clip[:end - at]
        end_card = self.scenes[-1]
        if end_card["clip"] is None:   # a gentle chime as the logo lands
            win = load_wav(os.path.join(AUDIO, "win.wav")) * 0.6
            at = int((end_card["g0"] + 4) / FPS * SR)
            out[at:at + len(win)] += win[:max(0, min(len(win), n - at))]
        music = music_bed(n / SR) * 0.42
        mix = out + music[:n]
        # fade out the last second
        fade = int(1.2 * SR); total_n = int(self.total / FPS * SR)
        mix[total_n - fade:total_n] *= np.linspace(1, 0, fade)
        mix[total_n:] = 0
        peak = np.max(np.abs(mix)) or 1
        mix = mix / max(peak, 1.0) * 0.89
        return mix[:total_n]


def qr_card(size, label_px):
    """The Play-listing QR code on a cream card with "Scan to download" underneath. Pixel-sharp (nearest scaling)."""
    qr = Image.open(QR).convert("RGB")
    bbox = Image.eval(qr.convert("L"), lambda v: 255 - v).getbbox()      # trim the white margin, add our own
    qr = qr.crop(bbox).resize((size, size), Image.NEAREST)
    pad, gap = size // 9, 14
    f = ImageFont.truetype(FONT_B, label_px)
    asc, desc = f.getmetrics()
    cw, ch = size + pad * 2, size + pad * 2 + asc + desc + gap
    card = Image.new("RGBA", (cw, ch + 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(card)
    d.rounded_rectangle((0, 10, cw - 1, ch + 9), 34, fill=(60, 40, 60, 150))      # soft drop edge
    d.rounded_rectangle((0, 0, cw - 1, ch - 1), 34, fill=(255, 255, 255, 255))
    card.paste(qr, (pad, pad))
    label = "Scan to download"
    tw = d.textlength(label, font=f)
    d.text(((cw - tw) / 2, pad + size + gap - 4), label, font=f, fill=INK)
    return card


def wrap(text, font, width):
    words, lines, cur = text.split(), [], ""
    d = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    for wd in words:
        t = (cur + " " + wd).strip()
        if d.textlength(t, font=font) <= width: cur = t
        else: lines.append(cur); cur = wd
    if cur: lines.append(cur)
    return lines


def load_wav(path):
    sr, d = wavfile.read(path)
    d = d.astype(np.float64) / 32768.0
    if d.ndim > 1: d = d.mean(axis=1)
    if sr != SR:
        x = np.linspace(0, len(d) - 1, int(len(d) * SR / sr))
        d = np.interp(x, np.arange(len(d)), d)
    return d


def music_bed(seconds):
    """A calm, original loop: soft pad chords with a plucked arpeggio, 92 bpm, I-V-vi-IV in D major."""
    n = int(seconds * SR) + SR
    t = np.arange(n) / SR
    out = np.zeros(n)
    beat = 60 / 92
    bar = beat * 4
    chords = [[62, 66, 69], [57, 61, 64], [59, 62, 66], [55, 59, 62]]   # D, A, Bm, G
    hz = lambda m: 440 * 2 ** ((m - 69) / 12)
    nbars = int(seconds / bar) + 2
    for b in range(nbars):
        ch = chords[b % 4]
        s0 = int(b * bar * SR); s1 = min(n, int((b + 1) * bar * SR) + int(0.3 * SR))
        if s0 >= n: break
        tt = t[s0:s1] - b * bar
        env = np.minimum(1, tt / 0.6) * np.exp(-np.maximum(0, tt - bar) / 0.15)
        for m in ch:  # warm pad: fundamental plus a little octave, slightly detuned
            f = hz(m - 12)
            out[s0:s1] += env * 0.05 * (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * f * 2.003 * tt))
        f = hz(ch[0] - 24)  # bass
        out[s0:s1] += env * 0.07 * np.sin(2 * np.pi * f * tt)
        pattern = [0, 1, 2, 1, 2, 0, 1, 2]  # eighth-note arpeggio, an octave up
        for i, p in enumerate(pattern):
            st = b * bar + i * beat / 2
            a = int(st * SR); e = min(n, a + int(0.9 * SR))
            if a >= n: break
            tt = t[a:e] - st
            f = hz(ch[p] + 12)
            vel = 0.06 if i % 2 == 0 else 0.042
            out[a:e] += vel * np.exp(-tt / 0.28) * (np.sin(2 * np.pi * f * tt) + 0.25 * np.sin(2 * np.pi * 2 * f * tt)) * np.minimum(1, tt / 0.004)
    # gentle echo
    d = int(beat * 0.75 * SR)
    out[d:] += 0.25 * out[:-d]
    out[:int(1.5 * SR)] *= np.linspace(0, 1, int(1.5 * SR))
    return out


def encode(r, path, preview=False):
    if preview:
        keys = [s["g0"] + min(s["n"] - 1, 24) for s in r.scenes]
        th = [r.frame(g).resize((r.w // 4, r.h // 4)) for g in keys]
        sheet = Image.new("RGB", (len(th) * (r.w // 4 + 8), r.h // 4), (255, 255, 255))
        for i, im in enumerate(th): sheet.paste(im, (i * (r.w // 4 + 8), 0))
        sheet.save(path.replace(".mp4", "_preview.png"))
        print("preview:", path.replace(".mp4", "_preview.png"))
        return
    wav = path.replace(".mp4", "_audio.wav")
    wavfile.write(wav, SR, (r.audio() * 32767).astype(np.int16))
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    cmd = [ff, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{r.w}x{r.h}", "-r", str(FPS),
           "-i", "-", "-i", wav, "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
           "-profile:v", "high", "-af", "loudnorm=I=-14:TP=-1.5:LRA=11", "-ar", "48000", "-ac", "2", "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", path]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for g in range(r.total):
        proc.stdin.write(r.frame(g).tobytes())
        if g % 150 == 0: print(f"  {os.path.basename(path)}: {g}/{r.total}", flush=True)
    proc.stdin.close(); proc.wait()
    os.remove(wav)
    print(f"wrote {path} ({r.total / FPS:.1f} s, {os.path.getsize(path) / 1e6:.1f} MB)")


if __name__ == "__main__":
    clips_dir = sys.argv[1]
    which = sys.argv[2] if len(sys.argv) > 2 else "all"
    preview = "--preview" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    if which in ("vertical", "all"):
        encode(Renderer(clips_dir, [dict(s) for s in VERTICAL], 1080, 1920), os.path.join(OUT, "dockside_promo_vertical_1080x1920.mp4"), preview)
    if which in ("landscape", "all"):
        encode(Renderer(clips_dir, [dict(s) for s in LANDSCAPE], 1920, 1080), os.path.join(OUT, "dockside_promo_landscape_1920x1080.mp4"), preview)
