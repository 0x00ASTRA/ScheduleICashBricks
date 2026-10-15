#!/usr/bin/env python3
"""Reject Harmony targets that share native code in an IL2CPP inspection dump."""

import argparse
from collections import Counter, defaultdict
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
PATCH = re.compile(
    r'\[HarmonyPatch\(typeof\((\w+)\),\s*(?:nameof\(\w+\.(\w+)\)|"(\w+)")'
    r'(?:,\s*new\[\]\s*\{\s*typeof\((\w+)\)\s*\})?\)\]'
)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dump", type=Path, default=ROOT / "inspection/dump.cs")
    parser.add_argument("--source-dir", type=Path, default=ROOT / "src")
    args = parser.parse_args()
    methods = defaultdict(set)
    signatures = defaultdict(set)
    references = Counter()
    owner = None
    address = None
    with args.dump.open() as dump:
        for line in dump:
            declaration = re.match(r"(?:public|private|internal|protected) (?:\w+ )*class (\S+)", line)
            if declaration:
                owner = declaration.group(1)
                address = None
            location = re.search(r"\bRVA: (0x[0-9A-Fa-f]+)", line)
            if location:
                native_address = int(location.group(1), 16)
                references[native_address] += 1
                if "// RVA:" in line:
                    address = native_address
            method = re.search(r"\b(\w+)\(([^\n]*)\)\s*\{\s*\}\s*$", line)
            if method and owner and address is not None:
                name = (owner, method.group(1))
                methods[name].add(address)
                parameters = tuple(parameter.split("=", 1)[0].strip().rsplit(" ", 1)[0]
                                   for parameter in method.group(2).split(",") if parameter.strip())
                signatures[(*name, parameters)].add(address)
                address = None

    failures = 0
    checked = 0
    for source in sorted(args.source_dir.glob("**/*.cs")):
        text = source.read_text()
        patches = list(PATCH.finditer(text))
        if text.count("[HarmonyPatch(") != len(patches):
            print(f"FAIL: unsupported Harmony target declaration in {source.name}")
            failures += 1
        for patch in patches:
            checked += 1
            owner, named, quoted, parameter = patch.groups()
            method = named or quoted
            addresses = (signatures.get((owner, method, (parameter,)), set()) if parameter else
                         methods.get((owner, method), set()))
            if len(addresses) != 1:
                print(f"FAIL: {owner}.{method} needs exactly one unambiguous native target, found {len(addresses)}")
                failures += 1
                continue
            address = next(iter(addresses))
            count = references[address]
            if count != 1:
                print(f"UNSAFE: {owner}.{method} at RVA 0x{address:X} shares code with {count} methods")
                failures += 1
            else:
                print(f"PASS: {owner}.{method} has unique native code at RVA 0x{address:X}")
    if checked == 0:
        print("FAIL: no Harmony targets found")
        failures += 1
    if failures:
        raise SystemExit(1)
    print(f"PASS: {checked} native Harmony targets audited against {args.dump.name}")


if __name__ == "__main__":
    main()
