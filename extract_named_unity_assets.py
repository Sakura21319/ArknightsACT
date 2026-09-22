#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Export named Texture2D/Sprite assets from a converted Unity AssetBundle."""
import argparse
import json
import os
import re
import sys
from io import BytesIO


def safe_name(value: str) -> str:
    value = (value or "").strip()
    value = value.replace("/", "_").replace("\\", "_")
    value = re.sub(r"[^0-9A-Za-z._#()\-\u4e00-\u9fff]+", "_", value)
    return value or "unnamed"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("bundle")
    ap.add_argument("out_dir")
    ap.add_argument("--pattern", default=".*")
    ap.add_argument("--list-only", action="store_true")
    args = ap.parse_args()

    try:
        import UnityPy
    except Exception as exc:
        print(json.dumps({"ok": False, "error": f"UnityPy import failed: {exc}"}, ensure_ascii=False))
        return 2

    rx = re.compile(args.pattern, re.IGNORECASE)
    env = UnityPy.load(args.bundle)
    os.makedirs(args.out_dir, exist_ok=True)
    found = []
    exported = []
    used = set()
    for obj in env.objects:
        try:
            typ = obj.type.name
            if typ not in ("Texture2D", "Sprite"):
                continue
            data = obj.read()
            name = (getattr(data, "m_Name", "") or "").strip()
            if not name or not rx.search(name):
                continue
            item = {"type": typ, "name": name, "path_id": int(getattr(obj, "path_id", 0))}
            found.append(item)
            if args.list_only:
                continue
            img = getattr(data, "image", None)
            if img is None:
                continue
            fname = safe_name(name)
            if not fname.lower().endswith(".png"):
                fname += ".png"
            if fname in used:
                stem, ext = os.path.splitext(fname)
                fname = f"{stem}__{obj.path_id}{ext}"
            used.add(fname)
            buf = BytesIO()
            img.save(buf, format="PNG")
            with open(os.path.join(args.out_dir, fname), "wb") as fh:
                fh.write(buf.getvalue())
            exported.append(fname)
        except Exception as exc:
            print(f"asset {getattr(obj, 'path_id', '?')} failed: {exc}", file=sys.stderr)

    print(json.dumps({"ok": True, "found": found, "exported": sorted(exported)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
