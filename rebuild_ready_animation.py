#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Rebuild the ready-marker UI tween from parsed Arknights prefab parameters."""
import json
import math
import os
import sys
from PIL import Image


def colorize(src, rgb, alpha):
    out = Image.new("RGBA", src.size, (*rgb, 0))
    px = src.convert("RGBA")
    tint = Image.new("RGBA", src.size, (*rgb, 255))
    out = Image.composite(tint, out, px.getchannel("A"))
    a = px.getchannel("A").point(lambda v: int(v * alpha))
    out.putalpha(a)
    return out


def make_variant(src, out_root, name, rgb, fps=40):
    frame_dir = os.path.join(out_root, "animation_frames", name)
    os.makedirs(frame_dir, exist_ok=True)
    frames = []
    count = 20  # 0.5 seconds * 40 fps, matching the prefab tween.
    for i in range(count):
        t = i / count
        # The source sprite is 56 px; the prefab's RectSize tween is 47 -> 100.
        size = round(47 + (100 - 47) * t)
        img = colorize(src, rgb, 1.0 - t).resize((size, size), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        canvas.alpha_composite(img, ((128 - size) // 2, (128 - size) // 2))
        path = os.path.join(frame_dir, f"f{i:04d}.png")
        canvas.save(path)
        frames.append(canvas)
    gif = os.path.join(out_root, name + ".gif")
    frames[0].save(gif, save_all=True, append_images=frames[1:], duration=round(1000 / fps), loop=0, disposal=2)
    return {"frames": count, "fps": fps, "gif": gif, "frameDir": frame_dir}


def main():
    if len(sys.argv) != 3:
        print("usage: rebuild_ready_animation.py <sprite_skill_bg.png> <output_dir>", file=sys.stderr)
        return 2
    src_path, out_root = sys.argv[1], sys.argv[2]
    src = Image.open(src_path).convert("RGBA")
    os.makedirs(out_root, exist_ok=True)
    variants = {
        "manual_skill_ready_yellow": (255, 217, 0),
        "manual_skill_ready_blue": (91, 176, 255),
        "manual_skill_ready_enhance_orange": (255, 108, 0),
    }
    generated = {name: make_variant(src, out_root, name, rgb) for name, rgb in variants.items()}
    spec = {
        "source": "sprite_skill_bg.png",
        "prefabs": {
            "manual_skill_ready": {"fromSize": [47, 47], "toSize": [100, 100], "fromAlpha": 1.0, "toAlpha": 0.0, "duration": 0.5, "loop": -1, "easeType": 6},
            "manual_skill_ready_enhance": {"fromSize": [47, 47], "toSize": [100, 100], "fromAlpha": 1.0, "toAlpha": 0.0, "duration": 0.5, "loop": -1, "easeType": 6},
            "auto_skill_ready": {"sprite": "sprite_circle", "rotation": [0, 360], "duration": 10.0, "loop": -1, "easeType": 1},
            "mark_skill_ready": {"sprite": "sprite_skill_ready", "animated": False},
        },
        "reconstruction": "linear preview from the parsed tween endpoints; original sprite and parameters are preserved",
        "generated": generated,
    }
    with open(os.path.join(out_root, "ready_animation_spec.json"), "w", encoding="utf-8") as fh:
        json.dump(spec, fh, ensure_ascii=False, indent=2)
    print(json.dumps(spec, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
