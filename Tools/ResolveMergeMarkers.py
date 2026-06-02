import argparse
import pathlib


def resolve_merge_markers(text: str, keep: str) -> tuple[str, bool]:
    lines = text.splitlines(True)
    out: list[str] = []
    i = 0
    changed = False

    while i < len(lines):
        line = lines[i]
        if line.startswith("<<<<<<<"):
            changed = True
            i += 1

            head: list[str] = []
            while i < len(lines) and not lines[i].startswith("======="):
                head.append(lines[i])
                i += 1
            if i >= len(lines):
                out.extend(head)
                break

            i += 1  # skip =======

            theirs: list[str] = []
            while i < len(lines) and not lines[i].startswith(">>>>>>>"):
                theirs.append(lines[i])
                i += 1

            if i < len(lines):
                i += 1  # skip >>>>>>>

            out.extend(theirs if keep == "theirs" else head)
            continue

        out.append(line)
        i += 1

    return "".join(out), changed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--keep", choices=("theirs", "head"), default="theirs")
    parser.add_argument("paths", nargs="+")
    args = parser.parse_args()

    updated: list[str] = []
    skipped: list[tuple[str, str]] = []

    for raw in args.paths:
        p = pathlib.Path(raw)
        if not p.exists():
            skipped.append((raw, "missing"))
            continue

        data = p.read_text(encoding="utf-8", errors="surrogateescape")
        if "<<<<<<<" not in data:
            skipped.append((raw, "no markers"))
            continue

        new, changed = resolve_merge_markers(data, keep=args.keep)
        if changed and new != data:
            p.write_text(new, encoding="utf-8", errors="surrogateescape")
            updated.append(raw)
        else:
            skipped.append((raw, "unchanged"))

    print(f"updated: {len(updated)}")
    for f in updated:
        print(f" - {f}")
    if skipped:
        print(f"skipped: {len(skipped)}")
        for f, why in skipped:
            print(f" - {f}: {why}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

