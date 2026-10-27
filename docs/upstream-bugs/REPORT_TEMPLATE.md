# Upstream bug report template

Use this template for a defect found in EF Core, an EF provider, an ADO.NET driver, or a database engine. Keep the
report in `UPSTREAM_ISSUES.md` while it is being investigated, and add a row to `MATRIX.md` when it has a focused
regression test.

## Filing checklist

1. Pin the exact upstream package versions, database/server version, driver, target framework, and commit if applicable.
2. Reduce the behavior to the smallest SQL/EF/ADO.NET operation that still fails.
3. Add a local regression or conformance test. The test doc comment must name the upstream behavior or issue.
4. Compare against the relevant upstream provider/reference implementation where possible.
5. Classify the defect: EF Core, provider, ADO.NET driver, database engine, or expected behavior.
6. Record expected and actual results, including generated SQL, parameters, exception, or result set as applicable.
7. Search upstream issues and pull requests before filing; link related or duplicate reports.
8. Record any temporary workaround in this repository and its removal condition.
9. Update the status after filing and again when upstream fixes or rejects the report.

Do not call an engine limitation an upstream provider bug. If the behavior is intentional or the specification leaves it
implementation-defined, document it as such and link the relevant ISO or vendor documentation.

## Copy/paste entry

### `<project>: <short, observable defect>`

- **Where:** `<repository, package, type/method, version or commit>`
- **Classification:** `<EF Core | provider | ADO.NET driver | database engine | expected behavior>`
- **Environment:** `<target framework>; <database/server>; <driver>; <OS>`
- **Defect:** `<one paragraph describing the observable incorrect behavior>`
- **Minimal reproduction:** `<test name, SQL, or short code sample>`
- **Expected:** `<ISO/specification or documented provider behavior>`
- **Actual:** `<result, generated SQL, exception, or trace>`
- **Confirmed:** `<how it was reproduced and which local test proves it>`
- **Related upstream:** `<issue/PR links, or “none found”>`
- **Before filing:** `<remaining checks or “ready to file”>`
- **Our workaround:** `<link and explanation, or “none”>`
- **Status:** `unfiled | ready to file | issue filed | PR filed | fixed upstream | rejected/expected`
- **Upstream issue/PR:** `<link once filed>`

When filing, copy the minimal reproduction and the pinned environment into the upstream report. Keep claims narrow: one
reproducible defect per issue unless the upstream maintainers ask for a combined report.
