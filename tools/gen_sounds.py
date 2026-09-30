"""Synthesizes Gravity Maze SFX into GravityMaze/Sounds/ (stdlib only). Run: python tools/gen_sounds.py"""
import wave, struct, math, random, os

SR = 44100
PEAK = 10 ** (-3 / 20)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "GravityMaze", "Sounds")
random.seed(7)
TAU = 2 * math.pi


def buf(dur):
    return [0.0] * int(SR * dur)


def add(b, s, at=0.0, gain=1.0):
    o = int(at * SR)
    for i, v in enumerate(s):
        if o + i < len(b):
            b[o + i] += v * gain


def tone(dur, f0, f1=None, partials=((1, 1.0),), attack=0.003, decay=8.0, detune=0.0, vibrato=0.0):
    """Sine partials with optional exponential pitch sweep f0->f1, exp decay, detuned twin."""
    n = int(SR * dur)
    f1 = f0 if f1 is None else f1
    out = [0.0] * n
    for d in ((0.0,) if detune == 0 else (-detune, detune)):
        ph = 0.0
        for i in range(n):
            t = i / SR
            f = f0 * (f1 / f0) ** (i / n) * (1 + d)
            if vibrato:
                f *= 1 + 0.01 * math.sin(TAU * vibrato * t)
            ph += TAU * f / SR
            e = min(1.0, t / attack) * math.exp(-decay * t)
            out[i] += e * sum(a * math.sin(ph * k) for k, a in partials)
    return out


def noise(dur):
    return [random.uniform(-1, 1) for _ in range(int(SR * dur))]


def lp(x, fc):
    a = 1 - math.exp(-TAU * fc / SR)
    y, o = 0.0, []
    for v in x:
        y += a * (v - y)
        o.append(y)
    return o


def hp(x, fc):
    return [v - w for v, w in zip(x, lp(x, fc))]


def bp(x, lo, hi):
    return hp(lp(x, hi), lo)


def env(x, attack=0.003, decay=8.0):
    return [v * min(1.0, i / SR / attack) * math.exp(-decay * i / SR) for i, v in enumerate(x)]


def sweep_noise(dur, lo0, hi0, lo1, hi1, shape):
    """Noise through a band whose edges glide (16 blocks), shaped by shape(0..1)."""
    n = int(SR * dur)
    x = noise(dur)
    out = []
    blocks = 16
    bs = n // blocks
    for k in range(blocks):
        u = k / (blocks - 1)
        seg = x[k * bs:(k + 1) * bs if k < blocks - 1 else n]
        out += bp(seg, lo0 + (lo1 - lo0) * u, hi0 + (hi1 - hi0) * u)
    return [v * shape(i / n) for i, v in enumerate(out)]


def save(name, b, fade=0.004):
    n = len(b)
    f = int(SR * fade)
    for i in range(min(f, n)):
        b[i] *= i / f
        b[n - 1 - i] *= i / f
    m = max(abs(v) for v in b) or 1
    b = [v * PEAK / m for v in b]
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in b))


BELL = ((1, 1.0), (2.76, 0.45), (5.4, 0.2), (8.93, 0.08))
note = lambda m: 440 * 2 ** ((m - 69) / 12)
os.makedirs(OUT, exist_ok=True)

# wall_hit: low thump + bright click
b = buf(0.12)
add(b, tone(0.12, 160, 70, decay=35, attack=0.001), 0, 1.0)
add(b, tone(0.12, 420, 300, partials=((1, 1), (2.3, .4)), decay=45, attack=0.0005), 0, 0.35)
add(b, env(hp(noise(0.12), 3000), 0.0005, 160), 0, 0.5)
save("wall_hit", b, 0.002)

# ice_slide: seamless loop. Generate N+F samples, crossfade the extra tail into the head.
N, F = int(SR * 1.5), int(SR * 0.25)
x = noise(1.75)
body = [a * 0.6 + c * 0.6 for a, c in zip(bp(x, 2500, 9000), bp(x, 800, 2000))]
# shimmer: modulation periods divide 1.5 s evenly so it is itself periodic
body = [v * (0.8 + 0.2 * math.sin(TAU * 4 * (i / SR)) * math.sin(TAU * (2 / 1.5) * (i / SR))) for i, v in enumerate(body)]
loop = body[:N]
for i in range(F):
    u = i / F
    loop[i] = body[i] * u + body[N + i] * (1 - u)
save("ice_slide", loop, fade=0)  # no fade: loop ends must meet, not dip to zero

# boost: rising electric whoosh
d = 0.35
b = sweep_noise(d, 300, 900, 2000, 7000, lambda u: math.sin(math.pi * u ** 0.8) ** 1.5)
add(b, tone(d, 200, 1400, partials=((1, 1), (2, .5), (3, .3)), attack=0.02, decay=2, detune=0.01), 0, 0.5)
add(b, tone(d, 400, 2800, attack=0.02, decay=3, vibrato=40), 0, 0.15)
save("boost", b, 0.01)

# hole_fall: descending fwoop then soft thud
b = buf(0.6)
add(b, tone(0.45, 900, 90, partials=((1, 1), (2, .3)), attack=0.005, decay=3.5, detune=0.008), 0, 0.8)
add(b, sweep_noise(0.4, 1500, 4000, 200, 600, lambda u: (1 - u) ** 1.5), 0, 0.25)
add(b, tone(0.15, 90, 45, decay=22, attack=0.004), 0.44, 1.0)
add(b, env(lp(noise(0.15), 400), 0.003, 25), 0.44, 0.4)
save("hole_fall", b, 0.005)

# spawn: soft rising shimmer
b = buf(0.25)
for k, m in enumerate((72, 79, 84, 91)):
    add(b, tone(0.25 - k * 0.03, note(m) * 0.9, note(m), attack=0.04, decay=9, detune=0.004), k * 0.03, 0.6 / (k + 1) ** .3)
add(b, sweep_noise(0.25, 3000, 6000, 5000, 11000, lambda u: math.sin(math.pi * u)), 0, 0.12)
save("spawn", b, 0.01)

# goal: C-E-G-C bell arpeggio
b = buf(1.2)
for k, m in enumerate((72, 76, 79, 84)):
    add(b, tone(1.0, note(m), partials=BELL, decay=4.5 if k < 3 else 3, detune=0.002, attack=0.002), k * 0.12, 0.6 if k < 3 else 0.9)
save("goal", b, 0.005)

# star: sparkly ding
b = buf(0.3)
add(b, tone(0.3, note(91), partials=BELL, decay=12, detune=0.003), 0, 1.0)
add(b, tone(0.25, note(96), partials=((1, 1), (3, .2)), decay=16), 0.05, 0.5)
add(b, env(hp(noise(0.05), 7000), 0.0005, 70), 0, 0.15)
save("star", b, 0.003)

# countdown: clean beep; go: higher two-tone
sq = ((1, 1), (2, .25), (3, .1))
save("countdown", tone(0.15, 660, partials=sq, attack=0.004, decay=6, detune=0.002), 0.008)
b = buf(0.35)
add(b, tone(0.15, 880, partials=sq, decay=4, detune=0.003), 0, 0.8)
add(b, tone(0.25, 1320, partials=((1, 1), (2, .3), (3, .15)), decay=6, detune=0.003), 0.11, 1.0)
save("go", b, 0.006)

# tick: urgent short tick
b = buf(0.09)
add(b, tone(0.09, 1800, 1500, partials=((1, 1), (2.5, .4)), decay=55, attack=0.0005), 0, 0.8)
add(b, env(hp(noise(0.09), 4000), 0.0005, 90), 0, 0.3)
save("tick", b, 0.002)

# time_up: descending buzzer
b = buf(0.7)
saw = ((1, 1), (2, .5), (3, .33), (4, .25), (5, .2))
add(b, tone(0.7, 330, 110, partials=saw, attack=0.01, decay=2.5, detune=0.006, vibrato=22), 0, 0.8)
add(b, tone(0.7, 165, 55, attack=0.01, decay=2.5), 0, 0.6)
save("time_up", b, 0.01)

# menu sounds
save("menu_move", tone(0.06, 900, 1000, partials=((1, 1), (2, .2)), attack=0.004, decay=40), 0.004)
b = buf(0.18)
add(b, tone(0.18, note(79), partials=((1, 1), (2, .3)), decay=16, detune=0.003), 0, 0.7)
add(b, tone(0.15, note(86), partials=((1, 1), (2, .25)), decay=18, detune=0.003), 0.06, 1.0)
save("menu_confirm", b, 0.004)
save("menu_back", tone(0.12, 620, 420, partials=((1, 1), (2, .2)), attack=0.004, decay=25), 0.004)

# victory: fanfare
b = buf(2.5)
mel = [(0.0, 72, .25), (0.2, 72, .25), (0.4, 72, .25), (0.6, 72, .5), (1.05, 68, .45), (1.5, 70, .45), (1.95, 72, .5)]
for t0, m, dur in mel:
    add(b, tone(dur + 0.4, note(m), partials=((1, 1), (2, .5), (3, .3), (4, .15)), attack=0.005, decay=3.5, detune=0.004), t0, 0.5)
for m in (60, 64, 67, 72, 76, 79):
    add(b, tone(1.4, note(m), partials=BELL, decay=2.2, detune=0.002), 1.95 + (m % 7) * .015, 0.35)
for k, m in enumerate((84, 88, 91, 96, 100)):
    add(b, tone(0.5, note(m), partials=BELL, decay=7), 2.0 + k * 0.05, 0.2)
save("victory", b, 0.006)
print("done")
