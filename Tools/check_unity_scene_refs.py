#!/usr/bin/env python3
"""Find YAML local fileIDs referenced but not defined in a Unity scene."""
import re
import sys
from pathlib import Path

REF_RE = re.compile(r"\{fileID:\s*(-?\d+)\}")
DEF_RE = re.compile(r"^--- !u!\d+ &(\d+)(?:\s+stripped)?", re.M)


def check_scene(path: Path) -> int:
    text = path.read_text(encoding="utf-8", errors="replace")
    defined = set(DEF_RE.findall(text, re.MULTILINE))
    refs = set(REF_RE.findall(text))
    # fileID 0 is null; negative IDs are often prefab/scene refs
    orphans = sorted(
        int(r) for r in refs if r not in defined and r != "0" and int(r) > 0
    )
    print(f"\n{path}")
    print(f"  lines: {text.count(chr(10)) + 1}")
    print(f"  defined: {len(defined)}, refs: {len(refs)}, orphan positive IDs: {len(orphans)}")
    for fid in orphans[:40]:
        # count usages
        n = text.count(f"{{fileID: {fid}}}")
        print(f"    missing &{fid}  ({n} refs)")
    if len(orphans) > 40:
        print(f"    ... and {len(orphans) - 40} more")
    # YAML sanity: unmatched braces in non-binary lines
    if text.rstrip()[-1:] not in ("}", "]") and "SceneRoots:" not in text[-500:]:
        print("  WARNING: file may be truncated (bad ending)")
    return len(orphans)


def main():
    root = Path(__file__).resolve().parents[1]
    scenes = [
        root / "Assets/Import/Scenes/Levels/PTSD.unity",
        root / "Assets/Import/Scenes/Levels/PTSD in Danger.unity",
    ]
    total = 0
    for s in scenes:
        if s.exists():
            total += check_scene(s)
    return 0 if total == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
