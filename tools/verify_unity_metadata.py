#!/usr/bin/env python3
"""Reject malformed or duplicate Unity asset GUIDs before editor import."""
from pathlib import Path
import re
import sys


def main():
    root = Path(__file__).resolve().parent.parent / 'Assets'
    known, problems = {}, []
    for path in sorted(root.rglob('*.meta')):
        match = re.search(r'^guid:\s*(\S+)\s*$', path.read_text(encoding='utf-8'), re.M)
        if not match or not re.fullmatch(r'[0-9a-f]{32}', match[1]):
            problems.append(f'{path.relative_to(root)}: expected a 32 character hexadecimal GUID')
            continue
        guid = match[1]
        if guid in known:
            problems.append(f'{path.relative_to(root)}: duplicate GUID also used by {known[guid]}')
        else:
            known[guid] = path.relative_to(root)
    if problems:
        print('\n'.join(problems), file=sys.stderr)
        return 1
    print(f'Verified {len(known)} valid, unique Unity asset GUIDs.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
