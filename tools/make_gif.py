"""Combine numbered PNG frames (DemoAutomation -recordFrames) into a small looping GIF for the README.

Usage: python tools/make_gif.py <frames_dir> <out.gif> [--width 960] [--fps 12]
"""
import argparse
from pathlib import Path

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("frames", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("--width", type=int, default=960)
    parser.add_argument("--fps", type=int, default=12)
    args = parser.parse_args()

    files = sorted(args.frames.glob("frame_*.png"))
    if not files:
        raise SystemExit(f"no frame_*.png in {args.frames}")
    frames = []
    for f in files:
        img = Image.open(f).convert("RGB")
        height = round(img.height * args.width / img.width)
        img = img.resize((args.width, height), Image.LANCZOS)
        frames.append(img.quantize(colors=128, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(args.out, save_all=True, append_images=frames[1:], duration=round(1000 / args.fps), loop=0, optimize=True)
    print(f"{len(frames)} frames -> {args.out} ({args.out.stat().st_size / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()
