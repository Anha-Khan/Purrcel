"""Export the Purrcel brand assets as PNGs into the Unity project.

Two families, and they are not interchangeable:

  icon  — a full-bleed tile (Honey #FABD4F) with the Ink mark inset. For the store
          listing and the launcher. The inset is deliberate: Android masks legacy icons
          to a circle and iOS rounds them, so a mark that touches the edge loses its ears.

  mark  — the bare mark on transparency. For drawing inside the game, where the hub
          supplies its own parchment ground and a tile would fight it.

Adaptive foreground/background are separate because Play Store requires them: the
foreground must keep the mark inside the centre 66% safe zone or the system mask eats it.
"""

import io
from pathlib import Path

import resvg_py
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"
# Repo root, derived from this file: <repo>/Tools/brand/<this file>
PROJECT = Path(__file__).resolve().parents[2]
BRAND = PROJECT / "Assets/_Project/Brand"

INK = (61, 41, 33, 255)  # HubSkin.Ink
PAPER = (253, 240, 214, 255)  # HubSkin.Paper
HONEY = (250, 189, 79, 255)  # HubSkin.Honey
TRANSPARENT = (0, 0, 0, 0)


def svg_png(svg_path, size):
    raw = bytes(resvg_py.svg_to_bytes(svg_path=str(svg_path), width=size, height=size))
    return Image.open(io.BytesIO(raw)).convert("RGBA")


def circle_mask(size, supersample=4):
    m = Image.new("L", (size * supersample, size * supersample), 0)
    ImageDraw.Draw(m).ellipse(
        [0, 0, size * supersample - 1, size * supersample - 1], fill=255
    )
    return m.resize((size, size), Image.LANCZOS)


def tile(mark_svg, size, ground, mark_ratio, round_it=False):
    """Ground tile with the mark centred at mark_ratio of the tile."""
    tile_img = Image.new("RGBA", (size, size), ground)
    mark_px = int(size * mark_ratio)
    mark = svg_png(mark_svg, mark_px)
    at = (size - mark_px) // 2
    tile_img.alpha_composite(mark, (at, at))
    if round_it:
        # Composite the round mask through alpha so the corners actually clear.
        out = Image.new("RGBA", (size, size), TRANSPARENT)
        out.paste(tile_img, (0, 0), circle_mask(size))
        return out
    return tile_img


def save(img, name, also=()):
    BRAND.mkdir(parents=True, exist_ok=True)
    img.save(BRAND / name)
    for size in also:
        img.resize((size, size), Image.LANCZOS).save(
            BRAND / f"{Path(name).stem}-{size}.png"
        )
    print(f"  {name}  {img.size}")


if __name__ == "__main__":
    mark = OUT / "symbol-ink.svg"
    mark_cut = OUT / "symbol-ink-cut.svg"
    paper_mark = OUT / "symbol-paper-on-ink.svg"

    print(
        "launcher / store tiles (Honey ground, Ink mark, 62% so masks do not eat the ears)"
    )
    save(
        tile(mark, 512, HONEY, 0.62),
        "icon-512.png",
        also=(432, 324, 192, 144, 96, 72, 48),
    )
    save(
        tile(mark, 512, HONEY, 0.62, round_it=True),
        "icon-512-round.png",
        also=(192, 48),
    )

    print("android adaptive (mark on transparency, 52% inside the 66% safe zone)")
    save(tile(mark, 512, TRANSPARENT, 0.52), "icon-foreground-512.png", also=(432, 192))
    save(Image.new("RGBA", (512, 512), HONEY), "icon-background-512.png")

    print("bare marks for drawing inside the game")
    save(svg_png(mark, 512), "mark-512.png", also=(256, 128, 64))
    save(svg_png(mark_cut, 512), "mark-cut-512.png", also=(256, 64, 32))
    save(svg_png(paper_mark, 512), "mark-reversed-512.png", also=(256, 128))

    print("\nwritten to", BRAND)

