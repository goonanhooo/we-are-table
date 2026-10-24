# usage: python3 tools/make-giraffe-table.py public/assets public/assets   (기린 그림 = 일반 그림 모양 그대로 + 기린 무늬)
# Rebuild the phone-controller giraffe table pictures from the clean normal-table pictures
# (same silhouette/framing), painting a reticulated giraffe pattern on the slab and legs.
import sys, numpy as np
from PIL import Image
A = sys.argv[1]; OUT = sys.argv[2]
base = np.asarray(Image.open(A + '/table-normal.png').convert('RGBA')).astype(np.float32)
cols = np.asarray(Image.open(A + '/table-normal-colors.png').convert('RGBA')).astype(np.float32)
H, W = base.shape[:2]
rgb = base[..., :3]; alpha = base[..., 3]
TOP = np.array([209, 152, 110.])
lum = rgb.mean(-1); toplum = TOP.mean()
is_top = (np.abs(rgb - TOP).sum(-1) < 12) & (alpha > 0)
ys, xs = np.nonzero(is_top)
# corners of the top-face parallelogram
L = np.array([xs.min(), ys[xs == xs.min()].mean()]); R = np.array([xs.max(), ys[xs == xs.max()].mean()])
T = np.array([xs[ys == ys.min()].mean(), ys.min()]); B = np.array([xs[ys == ys.max()].mean(), ys.max()])
print('corners L,T,R,B', L, T, R, B)
M = np.stack([R - T, L - T], 1); Minv = np.linalg.inv(M)
def uv_of(x, y): d = np.stack([x - T[0], y - T[1]], 0); return Minv @ d  # (2,N)
# lowest top-face y per column = upper edge of the slab sides
edge = np.full(W, -1)
for x in range(W):
    c = np.nonzero(is_top[:, x])[0]
    if len(c): edge[x] = c.max()
SLAB = 16  # slab side thickness in px (measured: 16-17)
rng = np.random.default_rng(7)
def voronoi(u, v, n, seed, line):
    # tileable reticulated pattern: n×n jittered sites per unit square, repeated 3×3; cream line where the nearest two sites are close
    r = np.random.default_rng(seed); m = int(round(n))
    base = (np.stack(np.meshgrid(np.arange(m), np.arange(m)), -1).reshape(-1, 2) + r.uniform(.15, .85, (m * m, 2))) / m
    shade0 = r.uniform(0, 1, m * m)
    offs = np.array([(a, b) for a in (-1, 0, 1) for b in (-1, 0, 1)], float)
    sites = (base[None] + offs[:, None]).reshape(-1, 2); shade = np.tile(shade0, 9)
    p = np.stack([np.mod(u, 1), np.mod(v, 1)], -1)[:, None, :]
    d = np.sqrt(((p - sites[None]) ** 2).sum(-1))
    o = np.argsort(d, 1)[:, :2]; d1 = np.take_along_axis(d, o[:, :1], 1)[:, 0]; d2 = np.take_along_axis(d, o[:, 1:2], 1)[:, 0]
    edgeT = np.clip(((d2 - d1) * m - line) / (line * .9), 0, 1)  # 0 = line, 1 = patch
    return edgeT, shade[o[:, 0]]
PATCH = [np.array([236, 150, 80.]), np.array([224, 132, 70.]), np.array([242, 162, 92.])]
CREAM = np.array([255, 238, 186.])
def paint(edgeT, shade, factor):
    pc = np.where(shade[:, None] < .33, PATCH[0], np.where(shade[:, None] < .66, PATCH[1], PATCH[2]))
    c = CREAM[None] * (1 - edgeT[:, None]) + pc * edgeT[:, None]
    return np.clip(c * factor[:, None], 0, 255)
out = base.copy()
opaque = alpha > 0
# --- slab: top face + the thin sides right under it
yy, xx = np.nonzero(opaque)
below = yy - edge[xx]
slab = is_top[yy, xx] | ((edge[xx] >= 0) & (below > 0) & (below <= SLAB))
sy, sx = yy[slab], xx[slab]
u, v = uv_of(sx.astype(float), sy.astype(float))
# side pixels: fold the pattern over the edge so it continues from the top face
side = ~is_top[sy, sx]
eu, ev = uv_of(sx.astype(float), edge[sx].astype(float))
onLB = sx < B[0]  # front-left side hangs under the L-B edge (v = 1), front-right under B-R (u = 1)
k = 1.0 / np.linalg.norm(L - T)
fold = (sy - edge[sx]) * k * 1.6
u = np.where(side & ~onLB, 1 + fold, np.where(side, eu, u))
v = np.where(side & onLB, 1 + fold, np.where(side, ev, v))
et, sh = voronoi(u * 1.1, v * 1.1, 3, 11, .045)
factor = np.clip(lum[sy, sx] / toplum, .45, 1.05)
out[sy, sx, :3] = paint(et, sh, factor)
slab_mask = np.zeros((H, W), bool); slab_mask[sy, sx] = True
# --- legs (plain picture only): smaller spots in screen space, keep each face's shading
legs = opaque & ~slab_mask
ly, lx = np.nonzero(legs)
et, sh = voronoi(lx / 52.0, ly / 52.0, 4, 23, .085)
out[ly, lx, :3] = paint(et, sh, np.clip(lum[ly, lx] / toplum, .45, 1.05) * .92)
Image.fromarray(out.astype(np.uint8), 'RGBA').save(OUT + '/table-giraffe.png')
# colored picture: same giraffe slab, legs keep their solid leg colors like every other mode
outc = cols.copy(); outc[slab_mask, :3] = out[slab_mask, :3]
Image.fromarray(outc.astype(np.uint8), 'RGBA').save(OUT + '/table-giraffe-colors.png')
print('slab px', slab_mask.sum(), 'leg px', legs.sum())
