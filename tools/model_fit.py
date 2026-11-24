"""How a baked model (src/TestTrailer/Models/*.bin) sits on the bench's frame, wheels and Z drawbar: side, rear and
three-quarter pictures for a car's hitch height, the layout computed as TestTrailer's Layout does: one 0.40 m wheel on
every car, a 0.10 m lift when the Z's step would be over 0.40 m (spec 12).

Usage: python -I tools/model_fit.py src/TestTrailer/Models/M.bin OUT_PNG
"""
import struct, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

path, out = sys.argv[1], sys.argv[2]
b = open(path, "rb").read(); o = 0
def rd(fmt):
    global o
    v = struct.unpack_from("<" + fmt, b, o); o += struct.calcsize("<" + fmt); return v
assert b[:4] == b"OTHM"; o = 4; rd("i")
L, W, H, AF, WX, RX, AFR, ABK, ATOP = rd("9f")
nv, = rd("i"); vd = np.array(rd(f"{nv*6}f"), dtype=np.float32).reshape(nv, 6); P, N = vd[:, :3], vd[:, 3:]
groups = []
ns, = rd("i")
for _ in range(ns):
    col = rd("4f"); rd("2f"); nl, = rd("i"); o += nl; ni, = rd("i"); idx = np.array(rd(f"{ni}i")).reshape(-1, 3)
    groups.append((col, idx))
TONGUE, SPRINGS, FRAME, WHEEL, LIFT, LIFT_AT = 0.11, 0.15, 0.12, 0.40, 0.10, 0.40

def layout(ground, car_r, car_w):
    r = WHEEL; axleY = ground + r
    lift = LIFT if -(axleY + SPRINGS + FRAME / 2) > LIFT_AT else 0.0
    railBottom = axleY + SPRINGS + lift; floor = railBottom + FRAME; line = railBottom + FRAME / 2
    a = AF - L / 2
    comZ = a / (1 - 1 / (1 - TONGUE)); D = -comZ - L / 2
    front = -D; axleZ = front - AF
    return dict(ground=ground, r=r, w=car_w * r / car_r, lift=lift, axleY=axleY, railBottom=railBottom, floor=floor, line=line, front=front, back=front - L, axleZ=axleZ, D=D, step=-line)

def tris(lay):
    """All triangles (model + bench parts) in trailer space with colours."""
    out = []
    off = np.array([0, lay["floor"], lay["front"]], dtype=np.float32)
    for col, idx in groups:
        out.append((P[idx] + off, col))
    def box(c, h, col):
        c = np.array(c); h = np.array(h)
        v = np.array([[sx, sy, sz] for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]) * h + c
        f = [(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
        t = np.array([[v[a], v[b_], v[c_]] for a, b_, c_, d in f] + [[v[a], v[c_], v[d]] for a, b_, c_, d in f])
        out.append((t, col))
    red = (0.8, 0.2, 0.2, 1); blue = (0.25, 0.3, 0.8, 1); grey = (0.35, 0.35, 0.35, 1)
    ry = lay["railBottom"] + FRAME / 2
    for s in (-1, 1):
        box((s * RX, ry, (lay["front"] + lay["back"]) / 2), (0.04, FRAME / 2, L / 2), red)
    for z in (lay["front"] - 0.04, lay["back"] + 0.04):
        box((0, ry, z), (RX, FRAME / 2 - 0.01, 0.04), red)
    # Z drawbar: level from the front cross member, 45° step to the coupling's height (0), level 0.2 m
    ss = -(0.2 + abs(lay["step"]))
    pts = [(lay["front"] - 0.04, lay["line"]), (ss, lay["line"]), (-0.2, 0.0), (0.0, 0.0)]
    for (z0, y0), (z1, y1) in zip(pts, pts[1:]):
        n = 6
        for i in range(n):
            za, ya = z0 + (z1 - z0) * i / n, y0 + (y1 - y0) * i / n
            zb, yb = z0 + (z1 - z0) * (i + 1) / n, y0 + (y1 - y0) * (i + 1) / n
            box((0, (ya + yb) / 2, (za + zb) / 2), (0.05, 0.05, max(0.05, abs(zb - za) / 2)), blue)
    # wheels (24-gon cylinders) and the axle beam
    r = lay["r"]; w = lay["w"]
    for s in (-1, 1):
        cx = s * WX
        k = 24; ring = [(math.cos(2 * math.pi * i / k) * r, math.sin(2 * math.pi * i / k) * r) for i in range(k)]
        t = []
        for i in range(k):
            (y0, z0), (y1, z1) = ring[i], ring[(i + 1) % k]
            a0 = [cx - w / 2, lay["axleY"] + y0, lay["axleZ"] + z0]; a1 = [cx - w / 2, lay["axleY"] + y1, lay["axleZ"] + z1]
            b0 = [cx + w / 2, lay["axleY"] + y0, lay["axleZ"] + z0]; b1 = [cx + w / 2, lay["axleY"] + y1, lay["axleZ"] + z1]
            t += [[a0, a1, b1], [a0, b1, b0]]
            for x in (cx - w / 2, cx + w / 2):
                t.append([[x, lay["axleY"], lay["axleZ"]], [x, lay["axleY"] + y0, lay["axleZ"] + z0], [x, lay["axleY"] + y1, lay["axleZ"] + z1]])
        out.append((np.array(t, dtype=np.float32), (0.08, 0.08, 0.08, 1)))
    box((0, lay["axleY"], lay["axleZ"]), (WX - w / 2, 0.035, 0.035), grey)
    for s in (-1, 1):
        box((s * RX, lay["axleY"] + 0.095, lay["axleZ"]), (0.035, 0.015, 0.35), grey)
    return out

def render(lay, yaw, pitch, size, scale, title, centre):
    img = Image.new("RGB", size, (235, 235, 235)); d = ImageDraw.Draw(img)
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    R = np.array([[cy, 0, -sy], [0, 1, 0], [sy, 0, cy]]) ; Rp = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]); M = Rp @ R
    light = np.array([0.4, 0.8, -0.45]); light /= np.linalg.norm(light)
    allt = []
    for t, col in tris(lay):
        t = np.asarray(t, dtype=np.float64)
        n = np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0]); ln = np.linalg.norm(n, axis=1); ok = ln > 1e-9
        n[ok] /= ln[ok, None]
        sh = 0.45 + 0.55 * np.abs(n @ light)
        v = (t - centre) @ M.T
        for i in range(len(t)):
            allt.append((v[i, :, 2].mean(), v[i], col, sh[i]))
    allt.sort(key=lambda a: -a[0])
    W2, H2 = size[0] / 2, size[1] / 2
    for _, v, col, sh in allt:
        pts = [(W2 + p[0] * scale, H2 - p[1] * scale) for p in v]
        c = tuple(int(255 * min(1, col[i] * sh)) for i in range(3))
        d.polygon(pts, fill=c)
    gy = H2 - (lay["ground"] - centre[1]) * scale
    if pitch == 0:
        d.line([(0, gy), (size[0], gy)], fill=(120, 120, 120), width=2)
    try: f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 20)
    except Exception: f = None
    d.text((10, 8), title, fill=(0, 0, 0), font=f)
    return img

rows = []
for car, ground, car_r, car_w in (("Mammoth", -1.113, 0.52, 0.35), ("Protector", -0.794, 0.42, 0.25)):
    lay = layout(ground, car_r, car_w)
    centre = np.array([0, lay["floor"] + 0.4, lay["front"] - L / 2 + 0.6])
    side = render(lay, math.pi / 2, 0, (900, 420), 150, f"{car}: side (car to the right), wheel {2*lay['r']:.2f} m, floor {lay['floor']-ground:.2f} m up, step {lay['step']:.2f}{' lift 4in' if lay['lift'] else ''}", centre)
    rear = render(lay, math.pi, 0, (520, 420), 150, f"{car}: from the front", centre)
    tq = render(lay, math.radians(140), math.radians(-18), (700, 420), 130, f"{car}: three-quarter, front left", centre)
    rows.append((side, rear, tq, lay))
sheet = Image.new("RGB", (900 + 520 + 700, 420 * len(rows)), "white")
for i, (a, b2, c, lay) in enumerate(rows):
    sheet.paste(a, (0, 420 * i)); sheet.paste(b2, (900, 420 * i)); sheet.paste(c, (1420, 420 * i))
    print(f"row {i}: drawbar {lay['D']:.3f} m (the body's front behind the coupling), axle z {lay['axleZ']:.3f}, floor over ground {lay['floor']-lay['ground']:.3f}, "
          f"wheel top over the floor {lay['axleY']+lay['r']-lay['floor']:.3f} (arch top {ATOP:.3f}), Z step {lay['step']:+.3f}, lift {lay['lift']:.2f}, tyre width {lay['w']:.3f}")
sheet.save(out)
print("measures", dict(L=L, W=W, H=H, AF=AF, WX=WX, RX=RX, archFront=AFR, archBack=ABK, archTop=ATOP))
