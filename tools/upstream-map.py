#!/usr/bin/env python3
"""The map from every upstream provider's own tests to their equivalents here.

usage: upstream-map.py <docs/upstream-tests>

Reads the inventories (`<provider>.tsv`, written by tools/upstream-inventory.py) and merges them into MAP.tsv: one row per
behaviour, keyed by area (the test class with the provider's name taken out) and method, so a behaviour several
providers test is one row naming all of them. Rows already in MAP.tsv keep their status, equivalent and note; new rows
start as `todo`. Then prints the counts by status.

Statuses: todo; common (a shared suite covers it on all six, `equivalent` names the test); capability (common, skipped
where a database lacks the feature); dialect (a per-database test, usually SQL text); duplicate (another row covers it,
`equivalent` names the row); not-applicable (the note says why: a provider internal, a feature none of the six has).
"""
import collections
import glob
import os
import re
import sys

PROVIDER_WORDS = re.compile(r'SqlServer|Sqlite|Npgsql|MySql|DuckDB|DuckDb|NodaTime(?=TypeMapping)|(?<=[a-z])Pg(?=[A-Z])')
HEADER = ['area', 'method', 'providers', 'status', 'equivalent', 'note', 'where']


def area(cls):
    name = PROVIDER_WORDS.sub('', cls)
    return re.sub(r'Tests?$', '', name) or cls


def main():
    root = sys.argv[1]
    path = os.path.join(root, 'MAP.tsv')
    kept = {}
    if os.path.exists(path):
        for line in open(path, encoding='utf-8'):
            if line.startswith('#') or line.startswith('area\t'):
                continue
            row = dict(zip(HEADER, line.rstrip('\n').split('\t')))
            kept[(row['area'], row['method'])] = row

    behaviours = collections.OrderedDict()
    for inventory in sorted(glob.glob(os.path.join(root, '*.tsv'))):
        provider = os.path.basename(inventory)[:-4]
        if provider in ('MAP', 'spec-bases'):
            continue
        for line in open(inventory, encoding='utf-8'):
            if line.startswith('#') or line.startswith('class\t'):
                continue
            cls, method, kind, _, where = line.rstrip('\n').split('\t')
            if kind != 'own':
                continue
            row = behaviours.setdefault((area(cls), method), {'providers': [], 'where': []})
            if provider not in row['providers']:
                row['providers'].append(provider)
                row['where'].append(f'{provider}:{where}')

    counts = collections.Counter()
    with open(path, 'w', encoding='utf-8') as f:
        f.write('# One row per behaviour the upstream providers test themselves (tools/upstream-map.py); status and equivalent are kept on regeneration\n')
        f.write('\t'.join(HEADER) + '\n')
        for (a, method), row in sorted(behaviours.items()):
            old = kept.get((a, method), {})
            status = old.get('status', 'todo')
            counts[status] += 1
            f.write('\t'.join([a, method, ','.join(sorted(row['providers'])), status, old.get('equivalent', ''),
                               old.get('note', ''), ' '.join(row['where'])]) + '\n')
    print(f'{sum(counts.values())} behaviours: ' + ', '.join(f'{k} {v}' for k, v in counts.most_common()))


if __name__ == '__main__':
    main()
