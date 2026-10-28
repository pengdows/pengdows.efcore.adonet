#!/usr/bin/env python3
"""
The base suite's results, per database, from its TRX files (one directory per host, as `dotnet test --results-directory
<dir>/<Host>` writes them):

  record  <trx-dir> <record-dir>   write <record-dir>/<Host>.tsv: each test that didn't pass, its outcome and failure signature
  compare <trx-dir> <record-dir>   report regressions, new passes and changed failures; exit 1 on any regression
  ledger  <trx-dir> <out.md>       per-base counts for each database, and each database's largest failure groups

A test missing from a record passed. A failure signature is the exception type and the first line of its message, with
quoted names and numbers replaced, so the same failure in another test or run compares equal.
"""
import collections, glob, os, re, sys
import xml.etree.ElementTree as E

NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
HOSTS = ['SqlServer', 'PostgreSql', 'MySql', 'MariaDb', 'Sqlite', 'DuckDb', 'FlatFile']
OUTCOMES = {'Passed': 'passed', 'Failed': 'failed', 'NotExecuted': 'skipped'}


def signature(message):
    first = (message or '').strip().splitlines()[0] if message and message.strip() else '?'
    first = re.sub(r"'[^']*'", "'…'", first)
    first = re.sub(r'"[^"]*"', '"…"', first)
    first = re.sub(r'\b\d+\b', 'N', first)
    return first[:200].replace('\t', ' ')


def results(trx_dir, host):
    """{test: (outcome, signature)} from the newest TRX file under trx_dir/host, or None when there is none."""
    files = glob.glob(os.path.join(trx_dir, host, '**', '*.trx'), recursive=True)
    if not files:
        return None
    out = {}
    for r in E.parse(max(files, key=os.path.getmtime)).getroot().findall('.//t:UnitTestResult', NS):
        outcome = OUTCOMES.get(r.get('outcome'), 'other')
        message = r.find('.//t:Message', NS)
        text = message.text if message is not None else ''
        name = r.get('testName').replace(f'pengdows.efcore.adonet.Specification.Tests.{host}.', '', 1)
        out[name] = (outcome, '' if outcome == 'passed' else signature(text))
    return out


def read_record(record_dir, host):
    path = os.path.join(record_dir, f'{host}.tsv')
    if not os.path.exists(path):
        return None
    record = {}
    with open(path, encoding='utf-8') as f:
        for line in f:
            if line.startswith('#') or not line.strip():
                continue
            test, outcome, sig = line.rstrip('\n').split('\t')
            record[test] = (outcome, sig)
    return record


def record(trx_dir, record_dir):
    os.makedirs(record_dir, exist_ok=True)
    for host in HOSTS:
        res = results(trx_dir, host)
        if res is None:
            continue
        counts = collections.Counter(o for o, _ in res.values())
        with open(os.path.join(record_dir, f'{host}.tsv'), 'w', encoding='utf-8') as f:
            f.write(f"# {host}: {counts['passed']} passed, {counts['failed']} failed, {counts['skipped']} skipped of {len(res)}. "
                    'Tests not listed passed.\n')
            for test in sorted(res):
                outcome, sig = res[test]
                if outcome != 'passed':
                    f.write(f'{test}\t{outcome}\t{sig}\n')
        print(f"{host}: {counts['passed']}/{len(res)} passed, record written")


def compare(trx_dir, record_dir):
    regressed = False
    for host in HOSTS:
        res, rec = results(trx_dir, host), read_record(record_dir, host)
        if res is None or rec is None:
            continue
        regressions = sorted(t for t, (o, _) in res.items() if o != 'passed' and t not in rec)
        passes = sorted(t for t, (o, _) in res.items() if o == 'passed' and t in rec)
        changed = sorted(t for t, (o, s) in res.items() if o != 'passed' and t in rec and rec[t] != (o, s))
        missing = sorted(t for t in rec if t not in res)
        print(f'{host}: {len(regressions)} regressions, {len(passes)} new passes, {len(changed)} changed failures, '
              f'{len(missing)} recorded tests not run')
        for t in regressions[:50]:
            print(f'  REGRESSION {t}: {res[t][1]}')
        for t in changed[:20]:
            print(f'  CHANGED    {t}: {rec[t][1]}  ->  {res[t][1]}')
        regressed |= bool(regressions)
    return 1 if regressed else 0


def ledger(trx_dir, out_path):
    rows = collections.defaultdict(lambda: collections.defaultdict(collections.Counter))
    groups = {}
    for host in HOSTS:
        res = results(trx_dir, host)
        if res is None:
            continue
        groups[host] = collections.Counter(s for o, s in res.values() if o == 'failed')
        for test, (outcome, _) in res.items():
            cls = test.split('(')[0].rsplit('.', 1)[0].rsplit('.', 1)[-1]
            base = cls[: -len(host + 'Test')] if cls.endswith(host + 'Test') else cls
            rows[base][host][outcome] += 1
    hosts = [h for h in HOSTS if h in groups]
    lines = ['| Specification base | ' + ' | '.join(hosts) + ' |', '|---|' + '---:|' * len(hosts)]
    totals = {h: collections.Counter() for h in hosts}
    for base in sorted(rows):
        cells = []
        for h in hosts:
            c = rows[base][h]
            totals[h].update(c)
            n = sum(c.values())
            cells.append('' if n == 0 else f"{c['passed']}/{n}" + (f" ({c['skipped']} skipped)" if c['skipped'] else ''))
        lines.append(f'| {base} | ' + ' | '.join(cells) + ' |')
    lines.append('| **Total** | ' + ' | '.join(
        f"**{t['passed']}/{sum(t.values())}**" + (f" ({t['skipped']} skipped)" if t['skipped'] else '') for t in totals.values()) + ' |')
    text = '\n'.join(lines)
    for h in hosts:
        text += f'\n\n### {h}: largest failure groups\n\n' + '\n'.join(f'- {n} × `{s}`' for s, n in groups[h].most_common(15))
    with open(out_path, 'w', encoding='utf-8') as f:
        f.write(text + '\n')
    print(f'ledger written: {out_path}')


if __name__ == '__main__':
    command, *args = sys.argv[1:]
    if command == 'record':
        record(*args)
    elif command == 'compare':
        sys.exit(compare(*args))
    elif command == 'ledger':
        ledger(*args)
    else:
        sys.exit(__doc__)
