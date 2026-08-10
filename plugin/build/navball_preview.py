"""
DragonScreen - navball_preview

Renders the attitude ball OFFLINE, exactly the way NavBallRenderer renders it in the game: the same
mesh parameterisation, the same texture, the same quaternion pipeline, the same orthographic camera.

---- WHY THIS EXISTS ----
The ball's orientation cannot be settled from a screenshot: at any single attitude several wrong
orientations look plausible, and each guess costs a game restart. The in-game instrumentation logs
what the ball SHOULD read beside the euler angles actually applied - this script closes the loop by
rendering both, so a candidate correction is judged here and only the winner is built.

Same argument as the PNG page preview, applied to the one thing the page preview cannot draw.

---- THE VALIDATION THAT MAKES IT TRUSTWORTHY ----
`selftest` reconstructs `relative` from a real logged line and pushes it through this file's
reimplementation of Unity's quaternion maths. If the euler angles that fall out match the ones KSP
logged, then this script's model of the pipeline is proven against the game and its renders can be
believed. If it ever stops matching, the model is wrong and nothing below it means anything.

    2026-08-06 05:17:36, on the pad, pointing up, heading 180.7:
        surface pitch/heading/roll = 270.0 / 180.7 / 0.0   ball euler = 270.6 / 90.1 / 270.0

Usage:
    python navball_preview.py selftest
    python navball_preview.py render
"""
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
TEXTURE = os.path.join(ROOT, "GameData", "DragonScreen", "art", "navball.png")
OUTDIR = os.path.join(HERE, "preview")

SIZE = 320


# ------------------------------------------------------------------ Unity quaternion maths
# Unity uses ordinary quaternion algebra; its left-handedness lives in how the axes are arranged,
# not in the formulae. Composition q1 * q2 applies q2 first. Quaternion.Euler(x, y, z) is
# Ry(y) * Rx(x) * Rz(z) - "z degrees around z, x around x, y around y, applied in that order".

def q_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def q_conj(q):
    return (-q[0], -q[1], -q[2], q[3])


def q_axis_angle(axis, deg):
    n = math.sqrt(sum(c * c for c in axis))
    if n == 0:
        return (0.0, 0.0, 0.0, 1.0)
    h = math.radians(deg) * 0.5
    s = math.sin(h) / n
    return (axis[0] * s, axis[1] * s, axis[2] * s, math.cos(h))


def q_euler(x, y, z):
    """Unity's Quaternion.Euler."""
    return q_mul(q_mul(q_axis_angle((0, 1, 0), y), q_axis_angle((1, 0, 0), x)),
                 q_axis_angle((0, 0, 1), z))


def q_matrix(q):
    x, y, z, w = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]


def q_to_euler(q):
    """Unity's Quaternion.eulerAngles - the Ry*Rx*Rz decomposition, wrapped into [0, 360)."""
    m = q_matrix(q)
    sx = max(-1.0, min(1.0, -m[1][2]))
    x = math.asin(sx)
    if abs(sx) < 0.999999:
        z = math.atan2(m[1][0], m[1][1])
        y = math.atan2(m[0][2], m[2][2])
    else:                                   # gimbal lock: fold roll into yaw
        z = 0.0
        y = math.atan2(-m[2][0], m[0][0])
    return tuple(math.degrees(v) % 360.0 for v in (x, y, z))


def q_rotate(q, v):
    r = q_mul(q_mul(q, (v[0], v[1], v[2], 0.0)), q_conj(q))
    return (r[0], r[1], r[2])


def mirror_x_axis(q):
    """MASVesselComputer.cs:611-614, as ported into NavBallRenderer."""
    return (q[0], -q[1], -q[2], q[3])


NAVBALL_Y_ROTATE = q_euler(0.0, 180.0, 0.0)


# ------------------------------------------------------------------ UV conventions
# Candidate ways of laying the texture onto the generated sphere. The mesh puts the pole on +Y:
#
#     p = (sin phi cos theta, cos phi, sin phi sin theta)   phi 0 at +Y, theta about Y from +X to +Z
#
# so latitude and longitude come straight back out of a surface point.

def uv_standard(lon01, lat01):
    """What NavBallRenderer::Sphere does today: u = longitude, v = latitude (1 at the +Y pole)."""
    return lon01, lat01


def uv_transposed(lon01, lat01):
    """The texture's light/dark split runs along ITS u axis and its glyphs are rotated 90 degrees,
    which is a standard navball stored transposed. Swapping the axes undoes it."""
    return lat01, lon01


def uv_transposed_flip(lon01, lat01):
    """Transposed the other way round - the 90-degree rotation taken as clockwise, not anti."""
    return 1.0 - lat01, lon01


# The texture carries a light BORDER about 6 px wide on all four sides (measured: content settles at
# x=6 on the left and x=2042 on the right, out of 2048). Under the transposed mapping the latitude
# axis runs into those columns, so the border lands exactly ON the poles and smears right across the
# ball as a bright streak. Keeping off it costs a third of a degree of ladder and removes the streak.
POLE_INSET = 8.0 / 2048.0


def uv_readable(lon01, lat01):
    """
    The one mapping that gets all three right at once: sky up, pitch ladder the right way up, and
    glyphs that read forwards.

    `transposed` had the hemispheres right but mirrored glyphs; `transposed_flip` unmirrored the
    glyphs by flipping LATITUDE, which also turned the ball upside down. Mirroring LONGITUDE instead
    flips the same handedness while leaving up where it was.
    """
    u = POLE_INSET + lat01 * (1.0 - 2.0 * POLE_INSET)
    return u, 1.0 - lon01


UV_MODES = {
    "standard": uv_standard,
    "transposed": uv_transposed,
    "transposed_flip": uv_transposed_flip,
    "readable": uv_readable,
}


# ------------------------------------------------------------------ the renderer

def render(rotation, texture, uv_mode="standard", size=SIZE):
    """
    Orthographic, ball of radius 1 centred in frame - the same framing as the in-game camera
    (orthographicSize 1.01, aspect 1, looking along +Z with +Y up).
    """
    tex = texture.convert("RGB")
    tw, th = tex.size
    px = tex.load()

    out = Image.new("RGB", (size, size), (10, 12, 28))
    op = out.load()
    inv = q_conj(rotation)
    margin = 1.01

    for sy in range(size):
        ny = (1.0 - 2.0 * (sy + 0.5) / size) * margin      # screen +Y is up
        for sx in range(size):
            nx = (2.0 * (sx + 0.5) / size - 1.0) * margin
            rr = nx * nx + ny * ny
            if rr > 1.0:
                continue
            # Camera looks along +Z, so the face toward it is the -Z side of the ball.
            p = (nx, ny, -math.sqrt(max(0.0, 1.0 - rr)))
            lp = q_rotate(inv, p)

            lat01 = 1.0 - math.acos(max(-1.0, min(1.0, lp[1]))) / math.pi   # 1 at the +Y pole
            lon01 = (math.atan2(lp[2], lp[0]) / (2.0 * math.pi)) % 1.0

            u, v = UV_MODES[uv_mode](lon01, lat01)
            tx = min(tw - 1, max(0, int(u * tw)))
            ty = min(th - 1, max(0, int((1.0 - v) * th)))    # Unity v=1 is the image's top row
            op[sx, sy] = px[tx, ty]
    return out


# ------------------------------------------------------------------ attitudes

def relative_from_surface(pitch, heading, roll):
    """
    Rebuild the `relative` quaternion from the surface angles the game logged. Orient() logs
    `Quaternion.Inverse(relative).eulerAngles`, so inverting the euler gives `relative` back.
    """
    return q_conj(q_euler(pitch, heading, roll))


def ball_rotation(relative):
    """NavBallRenderer::Orient, line 186."""
    return q_mul(NAVBALL_Y_ROTATE, mirror_x_axis(relative))


# ------------------------------------------------------------------ entry points

LOGGED = [
    # (label, surface pitch/heading/roll, ball euler as logged by the game)
    ("pad, pointing up, hdg 180.7", (270.0, 180.7, 0.0), (270.6, 90.1, 270.0)),
]


def angle_between(a, b):
    """Degrees between two rotations. q and -q are the same rotation, hence the abs."""
    d = min(1.0, abs(sum(x * y for x, y in zip(a, b))))
    return math.degrees(2.0 * math.acos(d))


def selftest():
    # COMPARE ROTATIONS, NOT EULER TRIPLES. Both logged attitudes sit within a degree of gimbal lock
    # (pitch -90 straight up), where the Ry*Rx*Rz decomposition is ill-conditioned and yaw/roll trade
    # off against each other freely - (270.6, 90.1, 270.0) and (270.7, 360.0, 0.0) can be the SAME
    # rotation. euler -> quaternion is exact in both directions, so the honest test is the angle
    # between the two rotations.
    ok = True
    for label, surface, expected in LOGGED:
        rel = relative_from_surface(*surface)
        got_q = ball_rotation(rel)
        err = angle_between(got_q, q_euler(*expected))
        match = err < 0.5                      # the log rounds to 0.1 degrees
        ok = ok and match
        print("%-32s logged   %7.1f /%7.1f /%7.1f" % ((label,) + expected))
        print("%-32s model    %7.1f /%7.1f /%7.1f" % (("",) + q_to_euler(got_q)))
        print("%-32s rotation error %.3f deg   %s"
              % ("", err, "MATCH" if match else "MISMATCH"))
    print()
    print("pipeline model %s the game" % ("REPRODUCES" if ok else "DOES NOT REPRODUCE"))
    return 0 if ok else 1


ATTITUDES = [
    ("up",        (270.0, 180.7, 0.0)),   # on the pad: the zenith must sit dead centre, all sky
    ("horizon-n", (0.0, 0.0, 0.0)),       # level, facing north: horizon across the middle
    ("horizon-e", (0.0, 90.0, 0.0)),      # level, facing east: horizon STILL across the middle
    ("down",      (90.0, 180.0, 0.0)),    # nadir centred, all ground
    ("rolled",    (0.0, 0.0, 45.0)),      # level, 45 of roll: horizon must tilt 45
    ("pitch-30",  (330.0, 0.0, 0.0)),     # nose up 30: horizon must drop BELOW centre
]

# The ladder repeats every 90 degrees of heading, so absolute heading cannot be read off the ball -
# but the DIRECTION it turns can. Yawing right must sweep the markings LEFT across the screen.
HEADING_SWEEP = [("hdg-%d" % d, (0.0, float(d), 0.0)) for d in (0, 15, 30, 45)]


def render_all():
    if not os.path.isfile(TEXTURE):
        print("no texture at " + TEXTURE)
        return 1
    tex = Image.open(TEXTURE)
    if not os.path.isdir(OUTDIR):
        os.makedirs(OUTDIR)

    only = sys.argv[2] if len(sys.argv) > 2 else None
    for mode in UV_MODES:
        if only and mode != only:
            continue
        rows = [ATTITUDES, HEADING_SWEEP] if mode == "readable" else [ATTITUDES]
        width = max(len(r) for r in rows)
        sheet = Image.new("RGB", (SIZE * width, SIZE * len(rows)), (10, 12, 28))
        for ry, row in enumerate(rows):
            for i, (label, surface) in enumerate(row):
                rot = ball_rotation(relative_from_surface(*surface))
                sheet.paste(render(rot, tex, mode), (i * SIZE, ry * SIZE))
        path = os.path.join(OUTDIR, "navball_" + mode + ".png")
        sheet.save(path)
        print("wrote %-46s  %s" % (path, " | ".join(t[0] for r in rows for t in r)))
    return 0


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "selftest"
    sys.exit(selftest() if cmd == "selftest" else render_all())
