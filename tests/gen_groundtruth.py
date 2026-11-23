#!/usr/bin/env python3
"""Regenerate the gpu-rgb-app ground-truth test files from a gpu_rgb.py.

Usage: python3 gen_groundtruth.py <path/to/gpu_rgb.py> <out_dir>

Produces in <out_dir>:
  groundtruth.bin/.json  32 patterns x 120 phases x 720B (PREVIEW_STATES[:32])
  driveframes.bin/.json  7 levels x 120 phases x 720B (frame_colors)

Format matches C:\\Users\\linke\\Desktop\\gpu-rgb-app\\tests\\ (2026-11-20
build). Validated: regenerates the 2026-11-20 .bin files byte-identically
from the 2026-11-18 ref gpu_rgb.py.
"""
import importlib.util
import json
import os
import sys


def load(py_path):
    spec = importlib.util.spec_from_file_location("gpy", py_path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def weights_for_level(level):
    """Same math as state_weights() at a flat drive level in [0,3]."""
    wA = max(0.0, 1.0 - level)
    wB = max(0.0, 1.0 - abs(level - 1.0))
    wC = max(0.0, 1.0 - abs(level - 2.0))
    wD = max(0.0, 1.0 - abs(level - 3.0))
    return (wA, wB, wC, wD)


def gen(py_path, out_dir):
    m = load(py_path)
    os.makedirs(out_dir, exist_ok=True)

    items = list(m.PREVIEW_STATES.items())[:32]
    assert len(items) == 32, f"expected 32 states, got {len(items)}"
    gt = {
        "source": "ref/gpu_rgb.py PREVIEW_STATES[:32]",
        "fps": 20,
        "phase_period": 120,
        "slots": 240,
        "layout": "pattern-major: n(1..32) x t(0..119) x 720B, slot i = bytes 3i..3i+2",
        "patterns": [],
    }
    with open(os.path.join(out_dir, "groundtruth.bin"), "wb") as f:
        for n, (name, (fn, base)) in enumerate(items, 1):
            gt["patterns"].append({"n": n, "name": name, "base_rgb": list(base)})
            for t in range(120):
                fr = fn(t)
                assert isinstance(fr, (bytes, bytearray)) and len(fr) == 720
                f.write(fr)
    with open(os.path.join(out_dir, "groundtruth.json"), "w") as f:
        json.dump(gt, f, indent=1)
        f.write("\n")

    levels = [0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0]
    df = {
        "source": "ref/gpu_rgb.py frame_colors(state_weights(level), phase)",
        "levels": levels,
        "layout": "level-major: 7 levels x 120 phases x 720B",
        "level_names": [
            "gpu-spotlight-idle",
            "gpu-spotlight-somewhat-active",
            "sweep",
            "red_white",
        ],
    }
    with open(os.path.join(out_dir, "driveframes.bin"), "wb") as f:
        for L in levels:
            w = weights_for_level(L)
            for t in range(120):
                fr = m.frame_colors(w, t)
                assert isinstance(fr, (bytes, bytearray)) and len(fr) == 720
                f.write(fr)
    with open(os.path.join(out_dir, "driveframes.json"), "w") as f:
        json.dump(df, f, indent=1)
        f.write("\n")
    print(f"wrote {out_dir}/groundtruth.bin (32x120x720) and driveframes.bin (7x120x720)")


if __name__ == "__main__":
    gen(sys.argv[1], sys.argv[2])
