"""Build the Purrcel lockups: the mark beside the wordmark set in real letter outlines.

The wordmark is drawn from Assets/_Project/Resources/PurrcelUI.ttf (Plus Jakarta Sans,
SIL OFL 1.1) instanced at wght 700 — the same face the game already renders in IMGUI — so
the lockup and the in-game UI are the same typeface by construction, not by resemblance.

Letterforms are emitted as paths, never <text>: a logo has to render identically wherever
it lands, and <text> depends on the font being installed on the machine viewing it.
"""

import io
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.misc.transform import Transform

HERE = Path(__file__).resolve().parent
# Repo root, derived from this file: <repo>/Tools/brand/<this file>
PROJECT = Path(__file__).resolve().parents[2]
FONT_PATH = PROJECT / "Assets/_Project/Resources/PurrcelUI.ttf"

WORD = "PURRCEL"
TRACKING = 0.02  # extra letter spacing, in em — the game sets its wordmark tight
CAP_RATIO = 0.30  # cap height as a fraction of the mark's height

MARK = (
    "M40 146A88 88 0 0 1 74 77L66 14L116 59A88 88 0 0 1 140 59L190 14L182 77"
    "A88 88 0 0 1 216 146A88 88 0 0 1 40 146Z M114 168L142 168L128 188Z"
)
MARK_CUT = (
    "M40 146A88 88 0 0 1 74 77L66 14L116 59A88 88 0 0 1 140 59L190 14L182 77"
    "A88 88 0 0 1 216 146A88 88 0 0 1 40 146Z"
)


def load_font(weight=700):
    font = TTFont(str(FONT_PATH))
    return instancer.instantiateVariableFont(font, {"wght": weight}, inplace=False)


def word_paths(word, font):
    """Return (svg path data, advance width, cap height) in font units, y already flipped."""
    upem = font["head"].unitsPerEm
    glyph_set = font.getGlyphSet()
    cmap = font.getBestCmap()
    hmtx = font["hmtx"]
    cap = font["OS/2"].sCapHeight

    out = []
    pen_x = 0.0
    tracking = TRACKING * upem
    for ch in word:
        name = cmap[ord(ch)]
        advance = hmtx[name][0]
        pen = SVGPathPen(glyph_set, ntos=lambda v: f"{v:.1f}")
        # Flip y (fonts run up, SVG runs down) and translate the glyph to the pen position.
        t = Transform(1, 0, 0, -1, pen_x, 0)
        glyph_set[name].draw(TransformPen(pen, t))
        out.append((pen.getCommands(), pen_x))
        pen_x += advance + tracking

    # Drop the trailing tracking so the lockup is not optically right-heavy.
    width = pen_x - tracking if word else 0
    return out, width, cap


def escape(d):
    return d.strip()


def horizontal(mark_d, word, font, height, gap_ratio, ink, paper=None, title="Purrcel"):
    """Mark on the left, wordmark on the right, optically aligned on the cap height."""
    parts, width, cap = word_paths(word, font)
    mark_h = 256.0
    scale = height / mark_h
    mark_w = mark_h * scale

    # Cap height target, converted from font units into the same pixel space.
    word_scale = (height * CAP_RATIO) / cap
    word_w = width * word_scale
    word_h = cap * word_scale
    word_dy = (
        height * 0.5 - word_h * 0.5
    )  # centre the caps on the mark's optical middle
    gap = height * gap_ratio

    total_w = mark_w + gap + word_w
    d = [
        f'<path fill="{ink}" fill-rule="evenodd" transform="scale({scale:.4f})" d="{escape(mark_d)}"/>'
    ]
    for path_d, _ in parts:
        d.append(
            f'<path fill="{ink}" transform="translate({mark_w + gap:.2f} {word_dy:.2f}) '
            f'scale({word_scale:.5f})" d="{escape(path_d)}"/>'
        )
    body = "\n    ".join(d)
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {total_w:.0f} {height:.0f}">\n'
        f"  <title>{title}</title>\n    {body}\n</svg>"
    )


def stacked(mark_d, word, font, mark_h, gap_ratio, ink, title="Purrcel"):
    """Mark centred above the wordmark — the square-ish lockup for an app tile."""
    parts, width, cap = word_paths(word, font)
    mark_scale = mark_h / 256.0
    mark_w = 256.0 * mark_scale
    word_scale = (mark_h * 0.30) / cap
    word_w = width * word_scale
    gap = mark_h * gap_ratio
    total_h = mark_h + gap + cap * word_scale
    total_w = max(mark_w, word_w)

    cx = total_w / 2
    d = [
        f'<path fill="{ink}" fill-rule="evenodd" transform="translate({cx - mark_w / 2:.2f} 0) '
        f'scale({mark_scale:.4f})" d="{escape(mark_d)}"/>'
    ]
    for path_d, _ in parts:
        d.append(
            f'<path fill="{ink}" transform="translate({cx - word_w / 2:.2f} {mark_h + gap:.2f}) '
            f'scale({word_scale:.5f})" d="{escape(path_d)}"/>'
        )
    body = "\n    ".join(d)
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {total_w:.0f} {total_h:.0f}">\n'
        f"  <title>{title}</title>\n    {body}\n</svg>"
    )


def symbol(mark_d, size=256, ink="#3D2921", title="Purrcel"):
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">\n'
        f"  <title>{title}</title>\n"
        f'  <path fill="{ink}" fill-rule="evenodd" d="{escape(mark_d)}"/>\n</svg>'
    )


if __name__ == "__main__":
    font = load_font(700)
    out = HERE / "out"
    out.mkdir(exist_ok=True)

    files = {
        # one-colour masters, ink
        "symbol-ink.svg": symbol(MARK),
        "symbol-ink-cut.svg": symbol(MARK_CUT),
        # hub palette
        "symbol-paper-on-ink.svg": symbol(MARK, ink="#FDF0D6"),
        "symbol-cream-on-slate.svg": symbol(MARK, ink="#FFE8BD"),
        "symbol-ink-on-honey.svg": symbol(MARK, ink="#3D2921"),
        # lockups
        "lockup-horizontal.svg": horizontal(MARK, WORD, font, 256, 0.20, "#3D2921"),
        "lockup-stacked.svg": stacked(MARK, WORD, font, 256, 0.16, "#3D2921"),
        "lockup-horizontal-cut.svg": horizontal(
            MARK_CUT, WORD, font, 256, 0.20, "#3D2921"
        ),
    }
    for name, svg in files.items():
        (out / name).write_text(svg, encoding="utf-8")
        print(f"  {name}  ({len(svg)} bytes)")
    print("written to", out)

