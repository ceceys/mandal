"""Mandal tanıtım videosu v2: kare kare Pillow animasyonu -> ffmpeg. 13 sahne, push/zoom/fade geçişleri.
Kullanım: anim_video.py <promo klasörü> <çıktı mp4> <müzik wav> <tr|en> [fps]"""
import os, sys, math, subprocess
from PIL import Image, ImageDraw, ImageFont, ImageFilter

promo, out_mp4, music, LANG = sys.argv[1:5]; FPS = int(sys.argv[5]) if len(sys.argv) > 5 else 30
W, H = 1920, 1080
FF = r"C:\Users\Tüm Dijital\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.2-full_build\bin\ffmpeg.exe"
INK = (247, 242, 232); MUTED = (200, 190, 175); ACCENT = (214, 166, 96); PAPER = (251, 248, 242); DARK = (74, 64, 54)

def font(size, bold=True):
    for n in (("segoeuib.ttf" if bold else "segoeui.ttf"), "arial.ttf"):
        try: return ImageFont.truetype(n, size)
        except OSError: pass
    return ImageFont.load_default()

# ---------- easing ----------
clamp = lambda p: max(0.0, min(1.0, p))
def eoc(p): p = clamp(p); return 1 - (1 - p) ** 3
def eob(p):
    p = clamp(p); c1 = 1.70158; c3 = c1 + 1; return 1 + c3 * (p - 1) ** 3 + c1 * (p - 1) ** 2
def ebounce(p):
    p = clamp(p); n1, d1 = 7.5625, 2.75
    if p < 1 / d1: return n1 * p * p
    if p < 2 / d1: p -= 1.5 / d1; return n1 * p * p + 0.75
    if p < 2.5 / d1: p -= 2.25 / d1; return n1 * p * p + 0.9375
    p -= 2.625 / d1; return n1 * p * p + 0.984375
def eio(p): p = clamp(p); return p * p * (3 - 2 * p)

# ---------- katmanlar ----------
_bg = None
def background():
    global _bg
    if _bg is None:
        im = Image.new("RGB", (W, H)); d = ImageDraw.Draw(im)
        for y in range(H):
            t = y / (H - 1); d.line([(0, y), (W, y)], fill=(int(44 + 48 * t), int(40 + 38 * t), int(36 + 24 * t)))
        _bg = im.convert("RGBA")
    return _bg.copy()

def text_layer(s, size, color=INK, bold=True, maxw=None, spacing=1.2, align="left"):
    f = font(size, bold); d0 = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    lines = [s]
    if maxw and s.strip():
        lines, cur = [], ""
        for w in s.split():
            t = (cur + " " + w).strip()
            if d0.textlength(t, font=f) <= maxw: cur = t
            else: lines.append(cur); cur = w
        if cur: lines.append(cur)
    lh = int(size * spacing); tw = int(max(d0.textlength(l, font=f) for l in lines)) + 8
    im = Image.new("RGBA", (tw, lh * len(lines) + 10), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    for i, l in enumerate(lines):
        x = 0 if align == "left" else (tw - d0.textlength(l, font=f)) / 2
        d.text((x, i * lh), l, font=f, fill=color)
    return im

def shot_layer(name, box=None, maxw=900, maxh=640, radius=16, shadow=True):
    im = Image.open(os.path.join(promo, name)).convert("RGBA")
    if box: im = im.crop(box)
    r = min(maxw / im.width, maxh / im.height, 2.6); im = im.resize((int(im.width * r), int(im.height * r)), Image.LANCZOS)
    m = Image.new("L", im.size, 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, im.width - 1, im.height - 1], radius=radius, fill=255)
    card = Image.new("RGBA", im.size, (0, 0, 0, 0)); card.paste(im, (0, 0), m)
    if not shadow: return card
    pad = 50; cv = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    sh = Image.new("RGBA", cv.size, (0, 0, 0, 0)); ImageDraw.Draw(sh).rounded_rectangle([pad, pad + 14, pad + im.width, pad + im.height + 14], radius=radius, fill=(0, 0, 0, 160))
    cv.alpha_composite(sh.filter(ImageFilter.GaussianBlur(22))); cv.alpha_composite(card, (pad, pad)); return cv

def chip_layer(s, size=28):
    f = font(size, True); d0 = ImageDraw.Draw(Image.new("RGBA", (1, 1))); w = int(d0.textlength(s, font=f))
    im = Image.new("RGBA", (w + 44, size + 26), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([1, 1, w + 42, size + 24], radius=(size + 26) // 2, outline=ACCENT, width=3)
    d.text((22, 10), s, font=f, fill=INK); return im

def keycap_layer(s, size=64):
    f = font(size, True); d0 = ImageDraw.Draw(Image.new("RGBA", (1, 1))); w = max(int(d0.textlength(s, font=f)) + 60, size + 70)
    h = size + 60; im = Image.new("RGBA", (w, h + 10), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 10, w - 1, h + 9], radius=18, fill=(150, 118, 70))
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=18, fill=PAPER, outline=(214, 196, 160), width=3)
    d.text(((w - d0.textlength(s, font=f)) / 2, 22), s, font=f, fill=DARK); return im

def put(canvas, layer, cx, cy, alpha=1.0, scale=1.0, dx=0, dy=0, rot=0):
    """Katmanı merkezi (cx,cy) olacak şekilde yerleştirir."""
    if alpha <= 0.01: return
    L = layer
    if abs(scale - 1) > 0.005:
        L = L.resize((max(1, int(L.width * scale)), max(1, int(L.height * scale))), Image.BILINEAR)
    if rot: L = L.rotate(rot, resample=Image.BICUBIC, expand=True)
    if alpha < 0.995:
        a = L.getchannel("A").point(lambda v: int(v * alpha)); L = L.copy(); L.putalpha(a)
    canvas.alpha_composite(L, (int(cx + dx - L.width / 2), int(cy + dy - L.height / 2)))

# ---------- animasyon yardımcıları ----------
def A(t, start, dur, kind):
    """(alpha, scale, dx, dy) döndürür."""
    p = clamp((t - start) / dur) if dur > 0 else 1.0
    if t < start: return (0, 1, 0, 0)
    if kind == "fade": return (p, 1, 0, 0)
    if kind == "left": e = eoc(p); return (e, 1, -120 * (1 - e), 0)
    if kind == "right": e = eoc(p); return (e, 1, 120 * (1 - e), 0)
    if kind == "up": e = eoc(p); return (e, 1, 0, 90 * (1 - e))
    if kind == "down": e = eoc(p); return (e, 1, 0, -90 * (1 - e))
    if kind == "drop": return (min(1, p * 5), 1, 0, -420 * (1 - ebounce(p)))
    if kind == "pop": return (min(1, p * 3), 0.5 + 0.5 * eob(p), 0, 0)
    if kind == "grow": e = eoc(p); return (e, 0.55 + 0.45 * e, 0, 0)
    return (1, 1, 0, 0)

T = {"tr": dict(
    tag="Ekran görüntülerin için bir çamaşır ipi.", chips=["Alıntı al", "Pano izleme", "Notlar", "OCR", "Kısayollar", "12 dil"],
    s2=("Al.", "Bölge, tam ekran ya da pencere.", "Win+Shift+S ile aldıkların da ipe asılır."),
    s3=("İpe as.", "Her alıntı ekranın üstündeki ipe asılır.", "İp sen çağırana kadar görünmez."),
    s4=("Tıkla, kopyala.", "Tek tık: panoda. Ctrl+V ile yapıştır."),
    s5=("Sürükle, bırak.", "Mandalından tut; Word'e, WhatsApp'a, Explorer'a bırak."),
    s6=("Notlar da asılır.", "Bir parçasını seç ve bırak: sadece o kopyalanır."),
    s7=("Yerinde düzenle.", "Kalem simgesi: notu kartın üstünde değiştir."),
    s8=("Görsel → metin.", "Aa düğmesi: tek tuşla OCR, çevrimdışı."),
    s9=("Büyük önizleme.", "Çift tık. Boyutlandır, yakınlaştır, üstte tut."),
    s10=("Köşedeki mandal.", "Fareyi köşeye götür: ip iner. Çık: ip kaçar."),
    s11=("Kalıcı kısayol.", "Karta ata; yeniden açılışta da çalışır."),
    s12=("Senin düzenin.", ["12 dil", "Otomatik güncelleme", "Hiçbir şey silinmez", "Özel kısayollar"]),
    out1="Al. İpe as.", out2="Lazım olunca ordan al.", made="Yapan: Cuma Ali Dirik", free="Ücretsiz · Açık kaynak (MIT) · Windows 10/11 · Telemetri yok",
    strip="readme-line-tr.png"),
  "en": dict(
    tag="A clothesline for your screenshots.", chips=["Capture", "Clipboard watch", "Notes", "OCR", "Shortcuts", "12 languages"],
    s2=("Capture.", "Region, full screen or window.", "Anything you grab with Win+Shift+S is pinned too."),
    s3=("Pin it.", "Every capture hangs on a line at the top.", "The line stays hidden until you call it."),
    s4=("Click, copy.", "One click: on the clipboard. Paste with Ctrl+V."),
    s5=("Drag, drop.", "Grab the clothespin; drop into Word, WhatsApp, Explorer."),
    s6=("Notes hang too.", "Select a part and release: only that is copied."),
    s7=("Edit in place.", "Pencil icon: change the note right on the card."),
    s8=("Image → text.", "Aa button: one-click OCR, offline."),
    s9=("Large preview.", "Double-click. Resize, zoom, keep on top."),
    s10=("Corner clothespin.", "Move the mouse to the corner: line drops. Leave: it flies up."),
    s11=("Persistent shortcut.", "Assign it to a card; works after a restart too."),
    s12=("Your way.", ["12 languages", "Auto-update", "Nothing gets deleted", "Custom shortcuts"]),
    out1="Capture. Pin it.", out2="Grab it when you need it.", made="Made by Cuma Ali Dirik", free="Free · Open source (MIT) · Windows 10/11 · No telemetry",
    strip="readme-line.png")}[LANG]

# ---------- katman önbelleği ----------
L = {}
def lay(key, fn=None):
    if key not in L: L[key] = fn()
    return L[key]

def title_block(c, t, title, sub, sub2=None, x=160, y=330):
    put(c, lay("t" + title, lambda: text_layer(title, 96)), x + lay("t" + title).width / 2, y, *A(t, 0.1, 0.7, "left"))
    s1 = lay("s" + sub, lambda: text_layer(sub, 36, MUTED, False, maxw=680)); put(c, s1, x + s1.width / 2, y + 120, *A(t, 0.35, 0.7, "left"))
    if sub2:
        s2 = lay("s" + sub2, lambda: text_layer(sub2, 36, MUTED, False, maxw=680)); put(c, s2, x + s2.width / 2, y + 120 + s1.height, *A(t, 0.55, 0.7, "left"))
    gh = lay("gh", lambda: text_layer("github.com/ceceys/mandal", 24, ACCENT, True)); put(c, gh, 160 + gh.width / 2, 1000, *A(t, 0.8, 0.5, "fade"))

# ---------- sahneler ----------
def sc_intro(t):
    c = background()
    put(c, lay("strip", lambda: Image.open(os.path.join(promo, T["strip"])).convert("RGBA")), W / 2, 107, *A(t, 0.0, 1.1, "drop"))
    put(c, lay("mandal", lambda: text_layer("Mandal", 170, INK, True)), W / 2, 460, *A(t, 0.5, 0.8, "pop"))
    put(c, lay("tag", lambda: text_layer(T["tag"], 46, MUTED, False)), W / 2, 600, *A(t, 1.0, 0.6, "up"))
    chips = [lay("chip" + s, lambda s=s: chip_layer(s)) for s in T["chips"]]
    total = sum(ch.width for ch in chips) + 20 * (len(chips) - 1); x = W / 2 - total / 2
    for i, ch in enumerate(chips):
        put(c, ch, x + ch.width / 2, 730, *A(t, 1.4 + i * 0.12, 0.45, "pop")); x += ch.width + 20
    put(c, lay("gh2", lambda: text_layer("github.com/ceceys/mandal · " + T["free"].split(" · ")[0], 26, ACCENT)), W / 2, 880, *A(t, 2.3, 0.6, "fade"))
    return c

def sc_region(t):  # bölge seçimi çizilir
    c = background(); title_block(c, t, *T["s2"])
    d = ImageDraw.Draw(c); x0, y0 = 1000, 300; p = eoc((t - 0.4) / 1.1) if t > 0.4 else 0
    if p > 0:
        x1, y1 = x0 + 760 * p, y0 + 440 * p
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ImageDraw.Draw(ov).rectangle([x0, y0, x1, y1], fill=(255, 255, 255, 28)); c.alpha_composite(ov)
        d = ImageDraw.Draw(c)
        for k in range(0, int(x1 - x0), 16): d.line([(x0 + k, y0), (min(x0 + k + 8, x1), y0)], fill=INK, width=3); d.line([(x0 + k, y1), (min(x0 + k + 8, x1), y1)], fill=INK, width=3)
        for k in range(0, int(y1 - y0), 16): d.line([(x0, y0 + k), (x0, min(y0 + k + 8, y1))], fill=INK, width=3); d.line([(x1, y0 + k), (x1, min(y0 + k + 8, y1))], fill=INK, width=3)
        lab = lay("lab", lambda: text_layer("760 × 440", 26, INK, False)); bg = Image.new("RGBA", (lab.width + 24, lab.height + 8), (20, 18, 16, 230))
        c.alpha_composite(bg, (int(x1) - bg.width, int(y1) + 14)); c.alpha_composite(lab, (int(x1) - bg.width + 12, int(y1) + 16))
    if t > 1.6:
        put(c, lay("cam", lambda: text_layer("Ctrl+Shift+S", 40, ACCENT)), 1380, 850, *A(t, 1.6, 0.5, "pop"))
    return c

def sc_pin(t):
    c = background(); title_block(c, t, *T["s3"])
    rope = Image.new("RGBA", (W, 40), (0, 0, 0, 0)); ImageDraw.Draw(rope).line([(900, 20), (1900, 26)], fill=(120, 92, 56), width=5)
    put(c, rope, W / 2, 230, *A(t, 0.0, 0.5, "fade"))
    for i, (name, box, x, st) in enumerate([("raw/01_hero_tr.png", (40, 20, 240, 170), 1060, 0.3), ("raw/01_hero_tr.png", (260, 20, 445, 165), 1330, 0.75), ("raw/01_hero_tr.png", (1295, 20, 1520, 160), 1620, 1.2)]):
        put(c, lay(f"card{i}", lambda name=name, box=box: shot_layer(name, box, 320, 260, 6)), x, 330, *A(t, st, 1.0, "drop"))
    return c

def sc_click(t):
    c = background(); title_block(c, t, *T["s4"])
    put(c, lay("hover", lambda: shot_layer("detail-card-buttons.png", (60, 60, 350, 230), 760, 500, 12)), 1400, 520, *A(t, 0.2, 0.8, "grow"))
    if t > 1.1:
        put(c, lay("copied", lambda: text_layer(("Kopyalandı" if LANG == "tr" else "Copied"), 44, INK)), 1400, 820, *A(t, 1.1, 0.5, "pop"))
    return c

def sc_drag(t):
    c = background(); title_block(c, t, *T["s5"])
    g = lay("ghost", lambda: shot_layer("raw/09_drag.png", (930, 170, 1140, 330), 420, 330, 8))
    p = eio((t - 0.3) / 2.2); x = 1050 + 560 * p; y = 420 + 260 * math.sin(p * math.pi) * 0.4 + 200 * p
    put(c, g, x, y, *A(t, 0.1, 0.4, "fade"), rot=-6 * math.sin(p * math.pi))
    tgt = lay("tgt", lambda: text_layer("Word · WhatsApp · Explorer", 34, ACCENT)); put(c, tgt, 1500, 900, *A(t, 1.6, 0.6, "up"))
    return c

def sc_simple(key, img, box=None, anim="right", maxw=860, maxh=620, pos=(1400, 580)):
    def f(t):
        c = background(); title_block(c, t, *T[key])
        put(c, lay(key + "img", lambda: shot_layer(img, box, maxw, maxh)), pos[0], pos[1], *A(t, 0.25, 0.9, anim))
        return c
    return f

def sc_corner(t):
    c = background(); title_block(c, t, *T["s10"])
    put(c, lay("corner", lambda: shot_layer("detail-corner.png", (80, 80, 260, 260), 420, 420, 12)), 1400, 520, *A(t, 0.3, 0.8, "pop"), rot=4 * math.sin(t * 5) * max(0, 1 - t / 2.2))
    return c

def sc_keys(t):
    c = background(); title_block(c, t, *T["s11"])
    keys = [lay("k" + s, lambda s=s: keycap_layer(s)) for s in ("Ctrl", "+", "Alt", "+", "1")]
    total = sum(k.width for k in keys) + 24 * 4; x = 1400 - total / 2
    for i, k in enumerate(keys):
        if keys[i].width < 120 and i in (1, 3): put(c, lay("plus", lambda: text_layer("+", 70, MUTED)), x + k.width / 2, 520, *A(t, 0.5 + i * 0.18, 0.4, "fade"))
        else: put(c, k, x + k.width / 2, 520, *A(t, 0.5 + i * 0.18, 0.5, "drop"))
        x += k.width + 24
    put(c, lay("arrow", lambda: text_layer("→  " + ("kart panoda" if LANG == "tr" else "card on clipboard"), 40, ACCENT)), 1400, 760, *A(t, 1.7, 0.5, "left"))
    return c

def sc_settings(t):
    c = background(); title_block(c, t, T["s12"][0], " ")
    put(c, lay("set", lambda: shot_layer("detail-settings.png", None, 820, 560)), 1420, 470, *A(t, 0.2, 0.9, "right"))
    for i, s in enumerate(T["s12"][1]):
        ch = lay("c2" + s, lambda s=s: chip_layer(s, 30)); put(c, ch, 160 + ch.width / 2, 520 + i * 80, *A(t, 0.8 + i * 0.25, 0.5, "pop"))
    return c

def sc_outro(t):
    c = background()
    put(c, lay("o1", lambda: text_layer(T["out1"], 120, INK)), W / 2, 300, *A(t, 0.1, 0.7, "left"))
    put(c, lay("o2", lambda: text_layer(T["out2"], 120, INK)), W / 2, 440, *A(t, 0.5, 0.7, "right"))
    put(c, lay("o3", lambda: text_layer("github.com/ceceys/mandal", 66, ACCENT)), W / 2, 640, *A(t, 1.1, 0.6, "pop"))
    put(c, lay("o4", lambda: text_layer(T["made"] + " · linkedin.com/in/cuma-ali-dirik", 36, MUTED, False)), W / 2, 740, *A(t, 1.6, 0.6, "up"))
    put(c, lay("o5", lambda: text_layer(T["free"], 30, MUTED, False)), W / 2, 880, *A(t, 2.0, 0.6, "fade"))
    put(c, lay("logo", lambda: Image.open(os.path.join(promo, "..", "logo-512.png")).convert("RGBA").resize((120, 120), Image.LANCZOS)), W / 2, 140, *A(t, 0.0, 0.8, "drop"))
    return c

SCENES = [(4.2, sc_intro, "fade"), (3.2, sc_region, "push"), (3.4, sc_pin, "zoom"), (2.8, sc_click, "push"), (3.4, sc_drag, "fade"),
          (3.0, sc_simple("s6", "detail-note.png", anim="up"), "push"), (2.6, sc_simple("s7", "detail-inline-edit.png", (60, 60, 340, 240), "grow", 760, 520), "zoom"),
          (2.8, sc_simple("s8", "raw/02_hover.png", (0, 0, 270, 170), "pop", 760, 520), "push"), (3.0, sc_simple("s9", "detail-preview.png", anim="grow"), "fade"),
          (2.6, sc_corner, "push"), (3.0, sc_keys, "zoom"), (3.2, sc_settings, "push"), (4.6, sc_outro, "fade")]
TR_D = 0.5
starts = []; acc = 0.0
for i, (d, _, _) in enumerate(SCENES): starts.append(acc); acc += d - TR_D
TOTAL = acc + TR_D
N = int(TOTAL * FPS)

def transition(kind, a, b, p):
    if kind == "fade": return Image.blend(a, b, p)
    if kind == "push":
        e = eio(p); out = Image.new("RGBA", (W, H)); out.paste(a, (int(-W * e), 0)); out.paste(b, (int(W * (1 - e)), 0)); return out
    if kind == "zoom":
        e = eio(p); s = 1 + 0.12 * e; A2 = a.resize((int(W * s), int(H * s)), Image.BILINEAR); out = Image.new("RGBA", (W, H)); out.paste(A2, (int((W - A2.width) / 2), int((H - A2.height) / 2)))
        s2 = 1.1 - 0.1 * e; B2 = b.resize((int(W * s2), int(H * s2)), Image.BILINEAR); Bc = Image.new("RGBA", (W, H)); Bc.paste(B2, (int((W - B2.width) / 2), int((H - B2.height) / 2)))
        return Image.blend(out, Bc, e)
    return b

def frame(k):
    t = k / FPS
    i = max(j for j in range(len(SCENES)) if starts[j] <= t)
    d, fn, _ = SCENES[i]; a = fn(t - starts[i])
    if i + 1 < len(SCENES) and t >= starts[i + 1]:
        p = (t - starts[i + 1]) / TR_D; b = SCENES[i + 1][1](t - starts[i + 1]); a = transition(SCENES[i + 1][2], a, b, clamp(p))
    if t < 0.5: a = Image.blend(Image.new("RGBA", (W, H), (31, 27, 24, 255)), a, t / 0.5)
    if t > TOTAL - 0.8: a = Image.blend(a, Image.new("RGBA", (W, H), (31, 27, 24, 255)), (t - (TOTAL - 0.8)) / 0.8)
    return a.convert("RGB")

args = [FF, "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-", "-i", music,
        "-filter_complex", f"[1:a]atrim=0:{TOTAL:.2f},afade=t=in:d=1.0,afade=t=out:st={TOTAL-2.5:.2f}:d=2.5,volume=0.9[a]",
        "-map", "0:v", "-map", "[a]", "-t", f"{TOTAL:.2f}", "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-c:a", "aac", "-b:a", "192k", out_mp4]
proc = subprocess.Popen(args, stdin=subprocess.PIPE, stderr=subprocess.DEVNULL)
for k in range(N):
    proc.stdin.write(frame(k).tobytes())
    if k % (FPS * 5) == 0: print(f"{k}/{N}", flush=True)
proc.stdin.close(); proc.wait()
print("bitti", out_mp4, f"{TOTAL:.1f} sn", os.path.getsize(out_mp4) // 1024, "KB")
