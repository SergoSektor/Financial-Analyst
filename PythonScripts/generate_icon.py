from PIL import Image, ImageDraw
import struct, io, math

SIZES = [16, 32, 48, 256]
BG = (0, 120, 212)
WHITE = (255, 255, 255)
GREEN = (46, 125, 50)

def create_png(size):
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    p = max(size // 6, 1)
    r = size // 4
    draw.rounded_rectangle((p, p, size - p, size - p), radius=r, fill=BG)

    cx = cy = size // 2
    rad = size * 0.30

    # 3 slices: 60% blue, 25% white, 15% green
    slices = [(0, 216, WHITE), (216, 306, GREEN), (306, 360, BG)]

    for start_deg, end_deg, color in slices:
        pts = [(cx, cy)]
        for deg in range(start_deg, end_deg + 1):
            a = math.radians(deg - 90)
            pts.append((cx + rad * math.cos(a), cy + rad * math.sin(a)))
        draw.polygon(pts, fill=color)

    return img

def create_ico():
    images = [create_png(s) for s in SIZES]
    num_images = len(images)
    header = struct.pack('<HHH', 0, 1, num_images)
    offset = 6 + num_images * 16
    data = bytearray()
    data.extend(header)
    for img, size in zip(images, SIZES):
        png_data = io.BytesIO()
        img.save(png_data, format='PNG')
        png_bytes = png_data.getvalue()
        w = 0 if size >= 256 else size
        h = 0 if size >= 256 else size
        entry = struct.pack('<BBBBHHII', w, h, 0, 0, 1, 32, len(png_bytes), offset)
        data.extend(entry)
        offset += len(png_bytes)
    for img in images:
        png_data = io.BytesIO()
        img.save(png_data, format='PNG')
        data.extend(png_data.getvalue())
    return bytes(data)

ico_data = create_ico()
with open('icon.ico', 'wb') as f:
    f.write(ico_data)
print(f"icon.ico created ({len(ico_data)} bytes)")
