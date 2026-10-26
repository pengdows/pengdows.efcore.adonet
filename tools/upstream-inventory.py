#!/usr/bin/env python3
"""Method-level inventory of an upstream EF provider's own test projects.

usage: upstream-inventory.py <provider> <source-root> <out.tsv> [test-dir ...]

Lists every test method in the provider's test sources: one row per method, with whether it overrides an inherited EF
specification test or is the provider's own, and where it is. Theory rows are not expanded. Read statically, nothing is
run. The rows are the denominator for "every upstream behaviour has an equivalent here" (docs/upstream-tests/MAP.tsv).
"""
import os
import re
import sys

ATTRIBUTE = re.compile(r'^\s*\[(?:Conditional)?(Fact|Theory)\b|^\s*\[(?:\w+\.)*(?:\w*Condition|\w*Fact|\w*Theory)\b')
METHOD = re.compile(r'^\s*public\s+(?:(override|virtual|new)\s+)?(?:async\s+)?(?:Task|void|ValueTask)\s+(\w+)\s*\(')
CLASS = re.compile(r'^\s*(?:public|internal)\s+(?:(?:abstract|sealed|partial|static)\s+)*class\s+(\w+)')
SKIP = re.compile(r'Skip\s*=')


def scan(path, rel):
    rows = []
    cls = None
    pending = None
    with open(path, encoding='utf-8', errors='replace') as f:
        for number, line in enumerate(f, 1):
            m = CLASS.match(line)
            if m:
                cls = m.group(1)
            if ATTRIBUTE.match(line):
                pending = pending or {'skip': False}
                pending['skip'] = pending['skip'] or bool(SKIP.search(line))
                continue
            m = METHOD.match(line)
            if m:
                modifier, name = m.group(1), m.group(2)
                if pending is not None or modifier == 'override':
                    kind = 'override' if modifier == 'override' else 'own'
                    if pending is None:
                        # An override without a test attribute of its own still replaces an inherited test.
                        pending = {'skip': False}
                    rows.append((cls or '?', name, kind, 'skipped' if pending['skip'] else '', f'{rel}:{number}'))
                pending = None
            elif pending is not None and line.strip() and not line.strip().startswith(('[', '//')):
                pending = None
    return rows


def main():
    provider, root, out, *dirs = sys.argv[1:]
    rows = []
    for d in dirs or ['test']:
        for base, _, files in os.walk(os.path.join(root, d)):
            if any(part in ('bin', 'obj') for part in base.split(os.sep)):
                continue
            for name in sorted(files):
                if name.endswith('.cs'):
                    path = os.path.join(base, name)
                    rows += scan(path, os.path.relpath(path, root))
    rows.sort()
    own = sum(1 for r in rows if r[2] == 'own')
    with open(out, 'w', encoding='utf-8') as f:
        f.write(f'# {provider}: {len(rows)} test methods ({own} its own, {len(rows) - own} overriding EF specification tests)\n')
        f.write('class\tmethod\tkind\tskipped\twhere\n')
        for r in rows:
            f.write('\t'.join(r) + '\n')
    print(f'{provider}: {len(rows)} methods, {own} own, {len(rows) - own} overrides -> {out}')


if __name__ == '__main__':
    main()
