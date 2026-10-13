#!/usr/bin/env python3
"""QG-04: checks line coverage for the critical modules and for the remaining ones.

Reads the merged Cobertura.xml and classifies each class as critical when its
full name equals a prefix from the critical modules file or starts with
"<prefix>.". Computes the line coverage of each set separately and exits with
code 1 if either set is below its threshold.

A prefix with no classes (module not implemented yet) only produces a warning.
"""
import argparse
import sys
import xml.etree.ElementTree as ET


def read_prefixes(path: str) -> list[str]:
    with open(path, encoding="utf-8") as file:
        lines = (line.strip() for line in file)
        return [line for line in lines if line and not line.startswith("#")]


def matching_prefix(class_name: str, prefixes: list[str]) -> str | None:
    for prefix in prefixes:
        if class_name == prefix or class_name.startswith(prefix + "."):
            return prefix
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("report")
    parser.add_argument("--critical-file", required=True, help="file with the critical module prefixes")
    parser.add_argument("--critical-min", type=float, required=True)
    parser.add_argument("--other-min", type=float, required=True)
    args = parser.parse_args()

    prefixes = read_prefixes(args.critical_file)
    per_prefix = {prefix: [0, 0] for prefix in prefixes}
    sets = {"critical": [0, 0, args.critical_min], "other": [0, 0, args.other_min]}

    for cls in ET.parse(args.report).getroot().iter("class"):
        # Only the class's own lines; the ones under <methods> repeat the same lines.
        lines = cls.findall("./lines/line")
        covered = sum(1 for line in lines if int(line.get("hits", "0")) > 0)
        prefix = matching_prefix(cls.get("name", ""), prefixes)
        key = "other" if prefix is None else "critical"
        sets[key][0] += covered
        sets[key][1] += len(lines)
        if prefix is not None:
            per_prefix[prefix][0] += covered
            per_prefix[prefix][1] += len(lines)

    print("Critical prefixes:")
    for prefix, (covered, total) in per_prefix.items():
        if total == 0:
            print(f"  WARNING {prefix}: no classes found (module not implemented yet or misspelled)")
        else:
            print(f"  {prefix}: {100 * covered / total:.1f}% ({covered}/{total})")

    failed = False
    for key, (covered, total, minimum) in sets.items():
        if total == 0:
            print(f"{key.capitalize()} modules: no lines measured (threshold of {minimum:g}% not applicable)")
            continue
        pct = 100 * covered / total
        ok = pct >= minimum
        failed |= not ok
        print(f"{key.capitalize()} modules: {pct:.1f}% ({covered}/{total}), minimum {minimum:g}% -> {'OK' if ok else 'FAILED'}")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
