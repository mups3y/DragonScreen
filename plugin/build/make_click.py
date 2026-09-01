#!/usr/bin/env python
"""
Generate the console button click: GameData/DragonScreen/sounds/panel_click.wav

    python build/make_click.py

---- WHY THE SOUND IS SYNTHESISED HERE AND NOT DOWNLOADED ----
BUILD_PLAN C7 puts external URLs off-limits as build inputs, and a sample pulled off a freesound
page would also drag a licence and an attribution obligation into a repo whose shippable-art rule
(C7.1) is deliberately narrow. A switch click is about sixty milliseconds of physics - a transient,
two resonances and a small body thump - so it is cheaper to author than to source, and authoring it
means the repo owns it outright with no attribution to keep straight.

It is DETERMINISTIC (fixed seed, integer maths): running this twice produces a byte-identical file,
so the asset can be regenerated without showing up as a spurious diff.

---- WHAT IT IS MODELLING ----
A guarded panel switch, heard from ~40 cm, which is a short sharp event with almost no tail:

    0-2 ms    the detent letting go: a noise transient, decaying fast
    0-9 ms    two damped resonances (1.9 kHz, 3.6 kHz) - the plastic body ringing
    0-28 ms   a low 170 Hz thump at low level - the switch bottoming out in the plate

The tail is faded to silence over the last 5 ms so the file cannot end on a discontinuity, which is
its own audible click and the one thing that would make this sound cheap.

⚠️ NOT VERIFIED ON GLASS. What it sounds like through KSP's mixer at IVA distance is a capsule
question - see REGISTER.md S17. This is the asset the code plays; whether it reads as a switch in
the cabin is judged there, not here.
"""
import math
import os
import struct
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'GameData', 'DragonScreen', 'sounds', 'panel_click.wav')

RATE = 44100
LENGTH = 0.060          # seconds
FADE = 0.005            # seconds of tail fade, so the file ends at zero
PEAK = 0.82             # headroom under full scale


def noise(seed):
    """A tiny deterministic LCG in -1..1. random.random() would tie the asset to a Python build."""
    state = seed
    while True:
        state = (1103515245 * state + 12345) & 0x7FFFFFFF
        yield (state / float(0x3FFFFFFF)) - 1.0


def main():
    n = int(RATE * LENGTH)
    rng = noise(20260902)
    samples = []

    for i in range(n):
        t = i / float(RATE)

        # The detent releasing: broadband, gone in about two milliseconds.
        transient = next(rng) * math.exp(-t / 0.0016) * 1.00

        # The body ringing. Two modes, both damped hard - a switch is not a bell.
        ring = (math.sin(2.0 * math.pi * 1900.0 * t) * math.exp(-t / 0.0032) * 0.55
                + math.sin(2.0 * math.pi * 3600.0 * t) * math.exp(-t / 0.0021) * 0.30)

        # Bottoming out against the plate: low, quiet, and the only part with any length to it.
        thump = math.sin(2.0 * math.pi * 170.0 * t) * math.exp(-t / 0.0090) * 0.22

        samples.append(transient + ring + thump)

    # Fade the tail rather than trusting the envelopes to have reached zero.
    fade = int(RATE * FADE)
    for i in range(fade):
        k = n - fade + i
        samples[k] *= 1.0 - (i / float(fade))

    high = max(abs(s) for s in samples) or 1.0
    scale = (PEAK * 32767.0) / high
    pcm = b''.join(struct.pack('<h', int(max(-32768, min(32767, round(s * scale)))))
                   for s in samples)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    w = wave.open(os.path.abspath(OUT), 'wb')
    try:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm)
    finally:
        w.close()
    print('wrote %s  (%d samples, %.0f ms, mono 16-bit %d Hz)'
          % (os.path.abspath(OUT), n, LENGTH * 1000.0, RATE))


if __name__ == '__main__':
    main()
