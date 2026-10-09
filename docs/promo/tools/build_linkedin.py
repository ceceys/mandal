"""LinkedIn serisi (Mail Aktarıcı / Dijital Gözetleme Kulesi düzeni): beyaz kart, mavi vurgu, numaralı liste, carousel PDF."""
import os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
Image.init()
promo, out = sys.argv[1], sys.argv[2]; os.makedirs(out, exist_ok=True)
BLUE = (43, 108, 176); NAVY = (30, 42, 59); GRAY = (107, 114, 128); LINE = (229, 231, 235); WHITE = (255, 255, 255)

def F(size, bold=False):
    for n in (("segoeuib.ttf" if bold else "segoeui.ttf"), "arial.ttf"):
        try: return ImageFont.truetype(n, size)
        except OSError: pass
    return ImageFont.load_default()

def spaced(d, xy, s, f, fill, sp=3):
    x, y = xy
    for ch in s:
        d.text((x, y), ch, font=f, fill=fill); x += d.textlength(ch, font=f) + sp

def wrap(d, s, f, width):
    words, lines, cur = s.split(), [], ""
    for w in words:
        t = (cur + " " + w).strip()
        if d.textlength(t, font=f) <= width: cur = t
        else: lines.append(cur); cur = w
    if cur: lines.append(cur)
    return lines

def icon(size):
    """Mavi yuvarlak kare üzerinde beyaz mandal (logo-512'nin alfa maskesinden)."""
    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(tile).rounded_rectangle([0, 0, size - 1, size - 1], radius=int(size * 0.24), fill=BLUE)
    logo = Image.open(os.path.join(promo, "..", "logo-512.png")).convert("RGBA")
    g = int(size * 0.68); logo = logo.resize((g, g), Image.LANCZOS)
    white = Image.new("RGBA", logo.size, WHITE + (255,)); white.putalpha(logo.getchannel("A"))
    tile.alpha_composite(white, ((size - g) // 2, (size - g) // 2)); return tile

def footer(d, y, left, right, x0, x1):
    d.line([(x0, y), (x1, y)], fill=LINE, width=2)
    d.text((x0, y + 28), left, font=F(28), fill=NAVY)
    f = F(24); d.text((x1 - d.textlength(right, font=f), y + 32), right, font=f, fill=GRAY)

def framed_shot(name, box, max_w, max_h):
    im = Image.open(os.path.join(promo, name)).convert("RGBA")
    if box: im = im.crop(box)
    r = min(max_w / im.width, max_h / im.height); im = im.resize((int(im.width * r), int(im.height * r)), Image.LANCZOS)
    m = Image.new("L", im.size, 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, im.width - 1, im.height - 1], radius=18, fill=255)
    card = Image.new("RGBA", im.size, (0, 0, 0, 0)); card.paste(im, (0, 0), m)
    pad = 40; cv = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    sh = Image.new("RGBA", cv.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([pad, pad + 8, pad + im.width, pad + im.height + 8], radius=18, fill=(30, 42, 59, 70))
    cv.alpha_composite(sh.filter(ImageFilter.GaussianBlur(16))); cv.alpha_composite(card, (pad, pad)); return cv

ITEMS = ["Alıntı al, ipe asılsın; tıkla kopyala, sürükle bırak",
         "Notlar da ipe asılır; bir parçasını seç, sadece o kopyalanır",
         "Tek tuşla görselden metin (Windows OCR, çevrimdışı)",
         "Karta kalıcı kısayol: Ctrl+Alt+1 ile her zaman elinin altında",
         "12 dil, otomatik güncelleme; hiçbir şey kendiliğinden silinmez"]
KICKER = "AÇIK KAYNAK · WINDOWS ARACI"; SUB = "Ekran görüntülerin için bir çamaşır ipi."
URL = "github.com/ceceys/mandal"; VER = "v1.3.0 · MIT · by cecey"

# 1) Gönderi görseli 1080x1350
img = Image.new("RGB", (1080, 1350), WHITE); d = ImageDraw.Draw(img)
d.rectangle([0, 0, 14, 1350], fill=BLUE); ic = icon(120); img.paste(ic, (120, 110), ic)
spaced(d, (120, 272), KICKER, F(24, True), BLUE)
d.text((116, 300), "Mandal", font=F(112, True), fill=NAVY)
d.text((120, 450), SUB, font=F(38), fill=GRAY)
d.line([(120, 540), (960, 540)], fill=LINE, width=2)
y = 580
for i, t in enumerate(ITEMS, 1):
    d.text((120, y + 4), f"{i:02d}", font=F(32, True), fill=BLUE)
    lines = wrap(d, t, F(34), 740)
    for j, ln in enumerate(lines): d.text((205, y + j * 42), ln, font=F(34), fill=NAVY)
    y += 42 * len(lines) + 58
    if i < len(ITEMS): d.line([(120, y - 26), (960, y - 26)], fill=LINE, width=1)
footer(d, 1232, URL, VER, 120, 960)
img.save(os.path.join(out, "gonderi-1080x1350.png"))

# 2) Bağlantı önizlemesi 1200x627 (GitHub social preview)
img = Image.new("RGB", (1200, 627), WHITE); d = ImageDraw.Draw(img)
spaced(d, (60, 56), KICKER, F(20, True), BLUE, 2)
d.text((56, 84), "Mandal", font=F(84, True), fill=NAVY)
for j, ln in enumerate(wrap(d, "Ekran görüntülerini ve notları ekranın üstündeki çamaşır ipine asar; tıkla kopyala, sürükle bırak", F(28), 1000)):
    d.text((60, 200 + j * 36), ln, font=F(28), fill=GRAY)
grid = ["Bölge, tam ekran, pencere ve pano izleme", "Notlar, parça seçerek kopyalama",
        "Tek tuşla OCR ve büyük önizleme", "Karta kalıcı kısayol, 12 dil, otomatik güncelleme"]
for k, t in enumerate(grid):
    cx, cy = 60 + (k % 2) * 580, 310 + (k // 2) * 76
    d.text((cx, cy), t, font=F(24), fill=NAVY); d.line([(cx, cy + 50), (cx + 520, cy + 50)], fill=LINE, width=1)
footer(d, 520, URL, VER, 60, 1140)
img.save(os.path.join(out, "baglanti-onizleme-1200x627.png"))

# 3) Carousel (PDF) 1080x1080: kapak + 5 özellik + kapanış
pages = []
def page():
    im = Image.new("RGB", (1080, 1080), WHITE); return im, ImageDraw.Draw(im)
im, d = page(); d.rectangle([0, 0, 14, 1080], fill=BLUE); ic = icon(130); im.paste(ic, (120, 120), ic)
spaced(d, (120, 300), KICKER, F(24, True), BLUE); d.text((116, 330), "Mandal", font=F(120, True), fill=NAVY)
for j, ln in enumerate(wrap(d, "Ekran görüntülerin için bir çamaşır ipi. Al, ipe as, lazım olunca ordan al.", F(40), 820)):
    d.text((120, 490 + j * 50), ln, font=F(40), fill=GRAY)
d.text((120, 640), "5 özellik, 5 kart  →", font=F(30), fill=BLUE)
footer(d, 960, URL, VER, 120, 960); pages.append(im)
FEATS = [("Al, ipe as", "Bölge, tam ekran ya da pencere al; Win+Shift+S ile aldıkların da asılır. İp sen çağırana kadar görünmez.", "readme-line-tr.png", None),
         ("Tıkla kopyala, sürükle bırak", "Tek tık panoya kopyalar. Mandalından tutup Word'e, WhatsApp'a, Explorer'a bırak.", "raw/09_drag.png", (700, 100, 1370, 430)),
         ("Notlar da asılır", "Sağ karttan not yaz. Kartın üstünde bir parçasını seç ve bırak: sadece o kopyalanır. Yerinde düzenle.", "detail-note.png", None),
         ("Görselden metne, büyük önizleme", "Aa düğmesi görseli metne çevirir (Windows OCR, çevrimdışı). Çift tık: boyutlanabilir önizleme, istersen üstte kalır.", "detail-preview.png", None),
         ("Senin düzenin", "Tüm kısayollar değiştirilebilir, karta Ctrl+Alt+1 gibi kalıcı kısayol verilir. 12 dil, otomatik güncelleme, hiçbir şey silinmez.", "detail-settings.png", None)]
for i, (t, s, name, box) in enumerate(FEATS, 1):
    im, d = page(); d.rectangle([0, 0, 14, 1080], fill=BLUE)
    spaced(d, (120, 96), f"{i:02d} / 05 · MANDAL", F(24, True), BLUE)
    d.text((116, 126), t, font=F(62, True), fill=NAVY)
    yy = 215
    for ln in wrap(d, s, F(30), 840): d.text((120, yy), ln, font=F(30), fill=GRAY); yy += 40
    shot = framed_shot(name, box, 840, 560)
    im.paste(shot, (120 + (840 - shot.width) // 2, 360 + (560 - shot.height) // 2), shot)
    footer(d, 960, URL, f"{i} / 7", 120, 960); pages.append(im)
im, d = page(); d.rectangle([0, 0, 14, 1080], fill=BLUE); ic = icon(110); im.paste(ic, (120, 140), ic)
d.text((116, 290), "Ücretsiz, açık kaynak.", font=F(72, True), fill=NAVY)
d.text((120, 400), "Windows 10/11 · MIT · Telemetri yok", font=F(34), fill=GRAY)
d.text((120, 520), "İndir ve kaynak kod", font=F(26, True), fill=BLUE); d.text((120, 560), URL, font=F(44, True), fill=NAVY)
d.text((120, 660), "Geri bildirim", font=F(26, True), fill=BLUE); d.text((120, 700), "linkedin.com/in/cuma-ali-dirik", font=F(44, True), fill=NAVY)
footer(d, 960, URL, VER, 120, 960); pages.append(im)
for i, p in enumerate(pages, 1): p.save(os.path.join(out, f"carousel-{i:02d}.png"))
pages[0].save(os.path.join(out, "Mandal-carousel-TR.pdf"), save_all=True, append_images=pages[1:], resolution=150)

# kontrol kolajı
a = pages[0].resize((540, 540)); b = pages[2].resize((540, 540)); c = Image.open(os.path.join(out, "gonderi-1080x1350.png")).resize((432, 540)); e = Image.open(os.path.join(out, "baglanti-onizleme-1200x627.png")).resize((1033, 540))
s = Image.new("RGB", (540 + 540 + 432 + 1033 + 30, 540), (200, 200, 200)); x = 0
for im in (c, a, b, e): s.paste(im, (x, 0)); x += im.width + 10
s.save(sys.argv[3])
print("ok", sorted(os.listdir(out)))
