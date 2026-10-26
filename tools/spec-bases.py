#!/usr/bin/env python3
"""Which EF specification bases the upstream providers run.

usage: spec-bases.py <ef-10.0.11-checkout> <npgsql-checkout> <pomelo-checkout> <duckdb-ef-checkout> <out-dir>

Writes <out-dir>/bases.json (each provider's bases), <out-dir>/union.json ([base, folder, test methods] for every base
any provider runs: tools/specreflect's input) and prints the counts. Read statically from the sources, nothing run.
"""
import json
import os
import re
import sys

ef, npgsql, pomelo, duckdb, out = sys.argv[1:]
DECL = re.compile(r'\bclass\s+(\w+)(?:\s*<[^{:]*?>)?(?:\s*\([^{:]*?\))?\s*(?::\s*([\w.]+))?', re.S)
TEST = re.compile(r'\[(?:Conditional)?(?:Fact|Theory)\b')
SPEC = [os.path.join(ef, 'test', p) for p in ['EFCore.Specification.Tests', 'EFCore.Relational.Specification.Tests']]
PROVIDERS = {
    'sqlserver': os.path.join(ef, 'test/EFCore.SqlServer.FunctionalTests'),
    'sqlite': os.path.join(ef, 'test/EFCore.Sqlite.FunctionalTests'),
    'postgresql': os.path.join(npgsql, 'test/EFCore.PG.FunctionalTests'),
    'mysql': os.path.join(pomelo, 'test/EFCore.MySql.FunctionalTests'),
    'duckdb': os.path.join(duckdb, 'test/DuckDB.EFCoreProvider.FunctionalTests'),
}


def sources(root):
    for d, _, files in os.walk(root):
        for f in files:
            if f.endswith('.cs'):
                path = os.path.join(d, f)
                yield d, re.sub(r'//.*', '', open(path, encoding='utf-8', errors='ignore').read())


def classes(root):
    found = {}
    for _, text in sources(root):
        for m in DECL.finditer(text):
            if m.group(2):
                found.setdefault(m.group(1), m.group(2).split('.')[-1])
    return found


# Every specification class: its base, its own test methods, and its folder.
info = {}
for root in SPEC:
    for d, text in sources(root):
        matches = list(DECL.finditer(text))
        for i, m in enumerate(matches):
            body = text[m.end():matches[i + 1].start() if i + 1 < len(matches) else len(text)]
            info.setdefault(m.group(1), {'base': (m.group(2) or '').split('.')[-1], 'tests': len(TEST.findall(body)),
                                         'folder': os.path.relpath(d, root).split(os.sep)[0]})


def total(name, seen=()):
    if name not in info or name in seen:
        return 0
    return info[name]['tests'] + total(info[name]['base'], seen + (name,))


# A provider runs a specification base when one of its classes derives from it, directly or through its own classes.
per = {}
for provider, root in PROVIDERS.items():
    local = classes(root)
    reached = set()
    for cls in local:
        base, seen = local[cls], set()
        while base in local and base not in info and base not in seen:
            seen.add(base)
            base = local[base]
        if base in info:
            reached.add(base)
    per[provider] = sorted(reached)

union = sorted({b for v in per.values() for b in v})
rows = [[b, info[b]['folder'], total(b)] for b in union if total(b) > 0]
os.makedirs(out, exist_ok=True)
json.dump(per, open(os.path.join(out, 'bases.json'), 'w'), indent=1)
json.dump(rows, open(os.path.join(out, 'union.json'), 'w'), indent=0)
print(f'{len(rows)} bases with tests, {sum(r[2] for r in rows)} test methods')
