"""Generate the game's audio bed.

The project ships no AudioClip assets, so AudioManager had nothing to play and the
build was silent. There is no recorded audio in this repo, so the clips are
synthesised: short blips for SFX and short loopable beds for music.

Run with:  python Tools/make-audio.py

Output:  Assets/_Project/Audio/Sfx/*.wav   (12 clips, one per SfxId)
         Assets/_Project/Audio/Music/*.wav (5 clips, one per MusicId)

Music buffers are rendered into a circular buffer so the tail wraps to the head.
That makes AudioSource.loop exact, with no gap and no click at the seam.
"""

import math
import os
import struct
import wave

import numpy as np

RATE = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SFX_DIR = os.path.join(ROOT, "Assets", "_Project", "Audio", "Sfx")
MUSIC_DIR = os.path.join(ROOT, "Assets", "_Project", "Audio", "Music")

TAU = 2.0 * math.pi


# --------------------------------------------------------------------------- utils


def env(n, attack, decay, curve=2.0):
    """Attack-decay envelope over n samples."""
    a = max(1, int(attack * RATE))
    out = np.zeros(n)
    if a < n:
        out[:a] = np.linspace(0.0, 1.0, a) ** 0.6
        d = np.linspace(0.0, 1.0, n - a) ** curve
        out[a:] = d
    return out


def sweep(n, f0, f1):
    """Frequency ramp from f0 to f1 across n samples."""
    t = np.linspace(0.0, n / RATE, n, endpoint=False)
    k = (f1 - f0) / (n / RATE)
    phase = TAU * (f0 * t + 0.5 * k * t * t)
    return np.sin(phase)


def sine(n, f, phase=0.0):
    t = np.linspace(0.0, n / RATE, n, endpoint=False)
    return np.sin(TAU * f * t + phase)


def square(n, f, duty=0.5):
    return (
        np.sign(
            np.sin(np.linspace(0.0, n / RATE, n, endpoint=False) * TAU * f)
            + (duty - 0.5) * 2.0
        )
        * 2.0
        - 1.0
    )


def saw(n, f):
    t = np.linspace(0.0, n / RATE, n, endpoint=False) * f
    return 2.0 * (t - np.floor(t + 0.5))


def noise(n):
    return np.random.uniform(-1.0, 1.0, n)


def one_pole_lp(x, cutoff):
    """Cheap low-pass. Good enough for noise shaping at these lengths."""
    a = math.exp(-TAU * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1.0 - a) * x[i] + a * acc
        y[i] = acc
    return y


def one_pole_hp(x, cutoff):
    return x - one_pole_lp(x, cutoff)


def normalise(x, peak=0.85):
    m = float(np.max(np.abs(x))) if len(x) else 0.0
    if m < 1e-9:
        return x
    return x * (peak / m)


def place(buf, signal, at):
    """Add signal into buf at sample offset, clipping the overhang off the tail."""
    i = int(at * RATE)
    if i >= len(buf):
        return
    end = min(len(buf), i + len(signal))
    buf[i:end] += signal[: end - i]


def place_circular(buf, signal, at):
    """Add signal into buf with wraparound, so decay tails survive the loop point."""
    i = int(at * RATE) % len(buf)
    n = len(buf)
    j = 0
    while j < len(signal):
        end = min(n, i + (len(signal) - j))
        buf[i:end] += signal[j : j + (end - i)]
        j += end - i
        i = end
        if i >= n:
            i = 0


def write_wav(path, data):
    peak = float(np.max(np.abs(data))) if len(data) else 0.0
    if peak > 1.0:
        data = data / peak
    pcm = (data * 32767.0).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    print(
        "  %-16s %5.2fs %7.1f kB"
        % (os.path.basename(path), len(data) / RATE, os.path.getsize(path) / 1024.0)
    )


def dur(seconds):
    return int(seconds * RATE)


# ---------------------------------------------------------------------------- sfx


def sfx_jump():
    n = dur(0.20)
    body = sweep(n, 360.0, 760.0)
    harm = sweep(n, 720.0, 1520.0) * 0.18
    return normalise((body + harm) * env(n, 0.004, 0.19, 2.4))


def sfx_double_jump():
    n = dur(0.24)
    body = sweep(n, 520.0, 1150.0)
    shimmer = sine(n, 2350.0) * env(n, 0.01, 0.2, 3.0) * 0.22
    return normalise(
        (body * 0.8 + shimmer + square(n, 520.0, 0.3) * 0.12) * env(n, 0.003, 0.23, 2.2)
    )


def sfx_land():
    n = dur(0.18)
    thud = sweep(n, 150.0, 62.0)
    grit = one_pole_lp(noise(n), 900.0) * env(n, 0.001, 0.1, 3.0) * 0.3
    return normalise((thud * 0.9 + grit) * env(n, 0.001, 0.17, 1.8))


def sfx_slide():
    n = dur(0.34)
    # Band-limited noise: high-passed, then a tone falling under it reads as a scrape.
    body = one_pole_hp(one_pole_lp(noise(n), 4200.0), 700.0)
    shaped = body * env(n, 0.02, 0.3, 1.6)
    tone = sweep(n, 900.0, 320.0) * 0.18 * env(n, 0.01, 0.28, 2.0)
    return normalise((shaped * 0.7 + tone) * env(n, 0.02, 0.31, 1.4))


def sfx_coin():
    n = dur(0.16)
    a = square(dur(0.05), 988.0, 0.45) * env(dur(0.05), 0.001, 0.045, 2.0)
    b = square(dur(0.11), 1319.0, 0.45) * env(dur(0.11), 0.001, 0.1, 2.2)
    out = np.zeros(n)
    place(out, a, 0.0)
    place(out, b, 0.05)
    return normalise(out)


def sfx_package():
    n = dur(0.26)
    rustle = one_pole_lp(noise(n), 3200.0)
    # Wobble the amplitude so it reads as paper rather than a hiss.
    t = np.linspace(0.0, n / RATE, n, endpoint=False)
    wobble = 0.55 + 0.45 * np.sin(TAU * 9.0 * t)
    return normalise(rustle * wobble * env(n, 0.006, 0.24, 1.8) * 0.8)


def sfx_deliver():
    """Three-note bell arpeggio. The most important SFX: it confirms a delivery."""
    notes = [(523.25, 0.00), (659.25, 0.085), (783.99, 0.17)]
    n = dur(0.46)
    out = np.zeros(n)
    for f, at in notes:
        m = dur(0.26)
        tone = sine(m, f) + sine(m, f * 2.0) * 0.3 + sine(m, f * 3.0) * 0.12
        place(out, tone * env(m, 0.002, 0.25, 2.6) * 0.5, at)
    return normalise(out)


def sfx_death():
    n = dur(0.62)
    t = np.linspace(0.0, n / RATE, n, endpoint=False)
    k = (95.0 - 400.0) / (n / RATE)
    fall = np.sin(TAU * (400.0 * t + 0.5 * k * t * t))
    grit = one_pole_lp(noise(n), 1500.0) * 0.22
    return normalise((fall * 0.8 + grit) * env(n, 0.004, 0.6, 1.5))


def sfx_bounce():
    n = dur(0.28)
    t = np.linspace(0.0, n / RATE, n, endpoint=False)
    # Fast pitch drop plus vibrato gives the cartoon "boing".
    drop = 620.0 * np.exp(-9.0 * t) + 190.0
    vib = 1.0 + 0.06 * np.sin(TAU * 22.0 * t)
    phase = np.cumsum(drop * vib) / RATE
    return normalise(np.sin(TAU * phase) * env(n, 0.003, 0.26, 2.0))


def sfx_combo_up():
    notes = [659.25, 783.99, 987.77, 1318.51]
    n = dur(0.34)
    out = np.zeros(n)
    for i, f in enumerate(notes):
        m = dur(0.13)
        tone = square(m, f, 0.4) * 0.4 + sine(m, f * 2.0) * 0.3
        place(out, tone * env(m, 0.002, 0.12, 2.4), i * 0.055)
    return normalise(out)


def sfx_ui_tap():
    n = dur(0.055)
    click = sine(n, 1180.0) * env(n, 0.001, 0.05, 3.0)
    tick = one_pole_hp(noise(n), 2600.0) * env(n, 0.0005, 0.03, 3.5) * 0.35
    return normalise((click * 0.7 + tick) * 0.55)


def sfx_purchase():
    """Sparkle up-shift plus a chord, for a confirmed purchase."""
    n = dur(0.6)
    out = np.zeros(n)
    for i, f in enumerate([880.0, 1174.66, 1567.98, 2093.0]):
        m = dur(0.34)
        tone = sine(m, f) + sine(m, f * 1.5) * 0.25
        place(out, tone * env(m, 0.002, 0.33, 2.2) * 0.4, i * 0.07)
    chord = dur(0.5)
    body = sine(chord, 440.0) + sine(chord, 659.25) + sine(chord, 880.0)
    place(out, body * env(chord, 0.004, 0.48, 1.8) * 0.3, 0.02)
    return normalise(out)


SFX = [
    ("Jump", sfx_jump),
    ("DoubleJump", sfx_double_jump),
    ("Land", sfx_land),
    ("Slide", sfx_slide),
    ("Coin", sfx_coin),
    ("Package", sfx_package),
    ("Deliver", sfx_deliver),
    ("Death", sfx_death),
    ("Bounce", sfx_bounce),
    ("ComboUp", sfx_combo_up),
    ("UiTap", sfx_ui_tap),
    ("Purchase", sfx_purchase),
]


# --------------------------------------------------------------------------- music
#
# Each bed is a loop of `bars` bars at a given tempo, rendered into a circular
# buffer. Harmony is a four-chord loop; the bass, pad and arp differ per track so
# the districts are distinguishable rather than four variations on one riff.

MINOR = [0, 2, 3, 5, 7, 8, 10]
MAJOR = [0, 2, 4, 5, 7, 9, 11]


def note_hz(root_hz, semitones):
    return root_hz * (2.0 ** (semitones / 12.0))


def render_bed(bars, bpm, root_hz, scale, chords, lead_pattern, drums, bright):
    """Render one loop. Returns a buffer that loops seamlessly."""
    beat = 60.0 / bpm
    total = bars * 4 * beat
    n = dur(total)
    buf = np.zeros(n)

    chord_len = total / len(chords)

    # Pad: one sustained triad per chord, with the envelope kept inside the chord
    # so nothing spills across the loop point.
    for ci, chord in enumerate(chords):
        for semitone in chord:
            m = dur(chord_len)
            t = np.linspace(0.0, m / RATE, m, endpoint=False)
            # Two slightly detuned partials per voice make the pad breathe.
            a = np.sin(TAU * note_hz(root_hz, semitone) * t)
            b = np.sin(TAU * note_hz(root_hz, semitone) * (1.004) * t)
            voice = a + b * 0.7
            # Soft attack, gentle release, no tail past the chord boundary.
            e = np.sin(np.linspace(0.0, math.pi, m)) ** 0.8
            place_circular(buf, voice * e * 0.16, ci * chord_len)

    # Bass: root of each chord, on the beat.
    for ci, chord in enumerate(chords):
        for beat_index in range(2):
            m = dur(beat * 0.85)
            place_circular(
                buf,
                sine(m, note_hz(root_hz, chord[0] - 12))
                * env(m, 0.004, beat * 0.8, 1.6)
                * 0.42,
                ci * chord_len + beat_index * beat,
            )

    # Lead: eighth-note figure, offset above the chord root by lead_pattern.
    step = beat / 2.0
    steps = int(round(total / step))
    for i in range(steps):
        ci = min(len(chords) - 1, int((i * step) // chord_len))
        offset = lead_pattern[i % len(lead_pattern)]
        hz = note_hz(root_hz, chords[ci][0] + offset)
        m = dur(step * 1.6)
        tone = (
            sine(m, hz) * 0.6
            + (sine(m, hz * 2.0) if bright else square(m, hz, 0.35) * 0.25) * 0.3
        )
        place_circular(buf, tone * env(m, 0.003, step * 1.4, 2.4) * 0.3, i * step)

    # Drums: kick and hat, no snare tail so the seam stays clean.
    for b in range(bars * 4):
        at = b * beat
        if drums:
            km = dur(0.16)
            kick = sweep(km, 130.0, 45.0) * env(km, 0.001, 0.15, 2.0)
            place_circular(buf, kick * 0.55, at)
        if drums:
            for half in (0.0, 0.5):
                hm = dur(0.05)
                hat = one_pole_hp(noise(hm), 6500.0) * env(hm, 0.0005, 0.045, 3.0)
                place_circular(buf, hat * 0.16, at + half * beat)

    # A short reverb-ish tail from the pad, added circularly so it wraps.
    tail = one_pole_lp(buf, 2600.0)
    place_circular(buf, (tail - buf) * 0.18, 0.0)

    return normalise(buf, 0.7)


def music_hub():
    # Warm, unhurried, major. Home base.
    return render_bed(
        bars=8,
        bpm=92,
        root_hz=220.0,
        scale=MAJOR,
        chords=[(0, 4, 7), (5, 9, 12), (7, 11, 14), (5, 9, 12)],
        lead_pattern=[0, 4, 7, 4, 12, 7, 4, 2],
        drums=False,
        bright=True,
    )


def music_old_town():
    # Slightly folk, walking pace.
    return render_bed(
        bars=8,
        bpm=104,
        root_hz=196.0,
        scale=MAJOR,
        chords=[(0, 4, 7), (2, 5, 9), (9, 12, 16), (7, 11, 14)],
        lead_pattern=[0, 2, 4, 7, 9, 7, 4, 2],
        drums=False,
        bright=False,
    )


def music_downtown():
    # Driving minor, four-on-the-floor. The fast district.
    return render_bed(
        bars=8,
        bpm=132,
        root_hz=174.61,
        scale=MINOR,
        chords=[(0, 3, 7), (8, 12, 15), (5, 8, 12), (7, 10, 14)],
        lead_pattern=[0, 3, 7, 10, 12, 10, 7, 3],
        drums=True,
        bright=True,
    )


def music_harbour():
    # Open fifths, sparse, sea-air.
    return render_bed(
        bars=8,
        bpm=84,
        root_hz=164.81,
        scale=MINOR,
        chords=[(0, 7, 12), (5, 12, 17), (8, 15, 20), (3, 10, 15)],
        lead_pattern=[0, 7, 12, 15, 12, 7, 0, 7],
        drums=False,
        bright=True,
    )


def music_suburbs():
    # Calm major, back half of the run.
    return render_bed(
        bars=8,
        bpm=98,
        root_hz=246.94,
        scale=MAJOR,
        chords=[(0, 5, 9), (7, 12, 16), (2, 7, 11), (9, 14, 17)],
        lead_pattern=[0, 5, 9, 12, 9, 5, 7, 4],
        drums=False,
        bright=False,
    )


MUSIC = [
    ("Hub", music_hub),
    ("OldTown", music_old_town),
    ("Downtown", music_downtown),
    ("Harbour", music_harbour),
    ("Suburbs", music_suburbs),
]


def main():
    np.random.seed(7)
    print("SFX ->", SFX_DIR)
    for name, fn in SFX:
        write_wav(os.path.join(SFX_DIR, name + ".wav"), fn())
    print("Music ->", MUSIC_DIR)
    for name, fn in MUSIC:
        write_wav(os.path.join(MUSIC_DIR, name + ".wav"), fn())


if __name__ == "__main__":
    main()
