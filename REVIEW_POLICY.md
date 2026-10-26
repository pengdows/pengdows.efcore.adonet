# REVIEW_POLICY.md (canonical)

Use this as the single source of truth for Claude/Codex/Gemini/Copilot reviews.

---

## 1) Scope rules

* Review **only the PR diff** plus any code directly referenced by the diff.
* Do not propose large refactors unless a **P0 blocker** requires it.
* Do not propose new architectural patterns, wrappers, service layers, or abstractions unless required to resolve a P0 blocker.
* Ignore unrelated files, formatting-only changes, and generated code unless the diff changes runtime behavior or public contracts.
* Prefer smallest changes that preserve existing public contracts.

---

## 2) Review output contract

Every review must output, in this order:

1. **MERGE:** YES/NO
2. **Blockers (P0)** — must fix
3. **Majors (P1)** — should fix before merge; waivers only for confirmed false positives
4. **Minors (P2)** — optional/nits
5. **Missing evidence** — tests/profiling/threat note required
6. **DB impact notes** — only if SQL/dialect/DDL/transaction behavior changed
7. **Minimal patch guidance** — concrete steps, no handwaving
8. **Confidence:** HIGH / MEDIUM / LOW
   * **HIGH** — directly evidenced by diff or tests
   * **MEDIUM** — strongly indicated but needs confirmation
   * **LOW** — plausible concern, limited evidence

---

## 3) Foundational principles (inform all review decisions)

These are not checklist items — they are the lens through which every finding is evaluated.

**The Boundary Rule** — Systems fail at boundaries. Every boundary must be explicit and controlled: DB connections, thread pools, network calls, memory ownership, authentication context. A change that makes a boundary implicit or uncontrolled is a defect regardless of whether it manifests immediately.

**The Lampson Rule** — Ownership must be explicit. Every resource must have a single clear owner: connections, readers, transactions, memory, and execution contexts. Shared resources without explicit ownership eventually leak, corrupt state, or deadlock. Ask: who owns this resource? Who releases it? Can ownership escape the scope where it is controlled? Can two components believe they own it? If the answer is unclear, the design is wrong. *(Lampson, Hints for Computer System Design, 1983.)*

**The Abrash Rule** — Know what the machine is actually doing. If it looks simple and cheap, verify that it actually is. Optimization by belief is forbidden. Hidden costs are design defects.

**The Schneier Rule** — Assume hostile inputs at every trust boundary. Fail loudly — silent failure is a security defect. Secrets must never appear in logs, exceptions, metrics, or telemetry.

**The Holub/Martin Rule** — If it is hard to test, the design is wrong. Complexity is the enemy. Favor small, cohesive units whose behavior is easy to reason about and verify.

**The Simplicity Rule** — Prefer the simplest design that solves the problem correctly. Complexity must justify its existence. Solve the problem you have, not the one you imagine.

**The I/O Separation Rule** — Never mix business logic with I/O. Systems fail when domain rules are tangled with transport protocols or persistence mechanisms. Presentation I/O (controllers, route handlers, UI components) must only adapt transport protocols, bind inputs, extract auth context, delegate to domain operations, and format responses. Persistence I/O (SQL builders, repositories, database drivers) must only map schemas, build queries, execute statements, and manage transactions/connections. All business rules, invariants, validation logic, domain workflows, and multi-entity orchestrations belong exclusively in pure, isolated domain models, domain services, or use cases that can be verified in isolation without I/O.

---

## 4) P0 blockers (no merge)

### A. Tests (TDD enforcement)

* Any new behavior or bug fix **must include unit tests**.
* Any change affecting DB behavior, SQL generation, transactions, pooling, or dialects **must include integration coverage**.
* Capability skips only: a test may skip solely because the database lacks the feature under test, and each such skip is attributed `[SKIP:<Product>]`. Any other skip, and any silent early `return` standing in for one, is a blocker.
* **Block if** behavior changed but tests did not change.
* Tests brought in from an existing EF provider are behavior references, not a reason to test only that provider. The rewritten test must try to run against all six providers (MySQL, PostgreSQL, SQL Server, flatfile, SQLite, and DuckDB); when the behavior cannot apply everywhere, it must cover as many providers as the capability model permits and record the remaining `[SKIP:<Product>]` reasons.
* Before adding an imported test, search the existing suites for the same setup, operation, expected result, and failure mode. If an existing test already covers that behavior, do not add the imported duplicate.
* Borrow from existing EF providers, and borrow the best: where a database has more than one public provider (MySQL: Pomelo and others; SQLite: Microsoft's and others; DuckDB: each community provider), compare them all and take the best code of the bunch, or write better code of our own. Borrowed code must be under a license compatible with this repository (MIT, Apache-2.0, PostgreSQL), keep its copyright notice, and name its source and license with a pinned link (`Adapted from EF Core's SQL Server SearchConditionConverter (MIT, .NET Foundation): <url>`); a re-implementation cites what it follows (`Behaviour reference (not copied): <url>`). Fit borrowed code to this repository's shared fixtures and abstractions rather than keeping the source's helper structure.
* Never copy test or implementation code from Oracle's providers (MySql.EntityFrameworkCore, MySql.Data / Connector/NET): they are GPL-licensed. Read them only to learn a behavior, and implement it independently.
* Prefer one shared behavior and API across providers. Do not blindly reproduce provider-specific APIs or divergent implementations when the same EF-visible capability can be expressed through the core abstraction and dialect capabilities.
* Classify each imported behavior as shared unchanged, shared with capability skips, shared with provider-specific expectations, or provider-only. The classification and provider matrix belong in the PR description.
* Compare semantic outcomes across all applicable providers. SQL text may differ by dialect, but equivalent operations should agree on results, affected rows, generated values, and exception behavior unless the database contract requires otherwise.

### B. Security invariants

Block if any of these occur:

* Unvalidated input crosses a trust boundary (SQL, file paths, serialization, network calls, logging, connection strings).
* Authentication/authorization logic changes without explicit tests.
* Secrets or sensitive values can appear in logs, exceptions, metrics, or telemetry.
* Custom crypto/token schemes introduced or modified without clear invariants and tests.
* A failure path is silent — swallowed exceptions, unchecked return codes, or missing error logging are security defects, not merely quality issues.
* A boundary (connection, pool, auth context, thread) becomes implicit or uncontrolled.

### C. Performance evidence (Abrash rule)

* If PR claims perf improvement, includes optimization, or touches known hot paths in a way that could affect runtime cost: require evidence.
  * Acceptable evidence: BenchmarkDotNet output, profiler summary, or before/after timing numbers.
  * Benchmarks must demonstrate correct warmup methodology. Cold-path and steady-state results must be distinguished where relevant.
  * Microbenchmarks must isolate the code under test. Benchmarking a surrounding pipeline is not acceptable evidence.
  * The measured delta must exceed the noise floor — overlapping confidence intervals is not evidence.
* Block "optimization by belief."
* Block "it looks simple and cheap therefore it is" reasoning without verification.

### D. API contract safety (interface-first)

* Public API surface must remain stable unless explicitly intended.
* **Expose only what needs exposing.** A public or protected type or member needs a consumer that cannot do without it and a documented reason; everything else is `internal` or `private`. Adding API later costs a minor version, removing it costs a major one, so the default is closed.
* **Raw provider objects never leave the library unexpectedly.** Provider connections, commands, readers, transactions, data sources, and related interfaces must not escape through public API unless that boundary is intentional, documented, and lifetime-safe. A governed wrapper is required when the library owns a resource lease. Accepting a raw provider object as an input is allowed only where unavoidable and needs a recorded reason.
* **Every new public or protected member is declared.** It appears in the project's public API baseline with a one-line reason; an undeclared addition is a blocker.
* A member added to a shipped public interface must have a default implementation; no new abstract member on a shipped interface.
* Concrete types must not cross module or service boundaries where an interface exists.
* Changes to interfaces require baseline verification updates.
* Compatibility with the last shipped version is checked with ApiCompat before release; anything it reports needs an explicit, recorded decision.

### E. Resource lifetime violations

Block if:

* `DbConnection` lifetime escapes execution scope.
* A reader or other provider resource is not deterministically disposed.
* Transactions are stored in long-lived fields.
* Async paths allow connection or reader leaks.

Resource leaks are architecture violations, not just bugs. The entire pool governance design exists to prevent this class of failure.

### F. Project hard bans

These rules are not stylistic preferences. They exist because violations create conditions where correctness defects become invisible in diffs, debugging sessions, and code review. They are therefore treated as correctness safeguards and enforced as P0 blockers.

* **Never mock provider behaviour.** Connections, commands, readers, transactions, parameters, factories, data sources and their failures must be exercised through an approved provider test double or a real provider. Add missing behavior to that test infrastructure; do not work around it with a mock. Mocking the library's own interfaces or loggers is acceptable where it does not conceal provider behavior.
* **Empty catch blocks and comment-only catch blocks need a stated reason.** An empty catch must say, in a comment, what exception is expected and why swallowing it is correct; otherwise narrow it, log it or rethrow.
* EF `TransactionScope` compatibility is required where EF supports ambient transactions. Do not ban or break `TransactionScope`; test ambient and explicit transaction paths according to EF's expected behavior, while keeping ownership and lifetime boundaries deterministic.
* No string interpolation for SQL values.
* No unquoted identifiers in custom SQL — use the repository's identifier-quoting helper.
* Where available, use analyzers to machine-enforce review invariants such as raw SQL value-injection safety.
* **One statement per line, always.** `if (something) break;` on one line is forbidden. The break is invisible in a diff and makes debugger breakpoints ambiguous. This is how correctness defects hide.
* **Braces are required for all control flow blocks without exception.** Brace-free single-line blocks are forbidden regardless of brevity. Braces and parentheses compile to nothing — file length is not a valid reason to omit them. An unbraced block is one added line away from a silent correctness bug.
* **After any scope-exiting statement — `return`, `break`, `continue`, or `throw` — do not use `else`.** The else branch is already implied by the exit. An else after a scope exit is logically redundant and adds indentation that obscures control flow.
* **Parentheses must make expression intent explicit.** Do not rely on operator precedence rules where parentheses would remove ambiguity. `(a && b) || c` is required; `a && b || c` is not acceptable.

### G. Metrics integrity

* If behavior changes, verify that metrics still tell the truth.
* A change that silently breaks a metric is equivalent to a change that breaks a query.

### H. Correctness regressions

Block if a change introduces any of the following:

* Error paths that leave state inconsistent.
* Retries that can duplicate side effects.
* Exception handling that changes observable behavior without tests.
* Logic changes that can produce incorrect results for valid, boundary, or adversarial inputs.

### I. Separation of business logic from I/O (no business logic in I/O)

Never mix business logic into I/O layers. Block if:

* **Business logic in controllers / endpoint handlers / presentation I/O**: Endpoint delegates, controllers, or route handlers perform business logic, domain calculations, invariant enforcement, multi-step domain workflows (e.g., revision walking, redirect traversal, parsing/category sync), or direct entity mutations. The I/O layer is strictly an adapter: bind inputs, resolve auth/tenant context, delegate to domain service/operation, and format the HTTP response.
* **Business logic in persistence I/O**: SQL builders, repositories, or database adapters contain business rules, domain validation, or cross-entity domain orchestration. Persistence components are strictly data-access adapters: build SQL, map rows to entities, execute statements, and commit/rollback transactions.
* **Domain logic coupled to I/O abstractions**: Business logic directly depends on HTTP primitives (`HttpContext`, `ClaimsPrincipal`, `IResult`) or raw database primitives (`IDbConnection`, `IDbCommand`) rather than domain models and clean abstractions.

---

## 5) P1 majors (fix or justify with false-positive evidence)

### A. Core library invariants

* **ValueTask in hot paths** — execution methods return `ValueTask`/`ValueTask<T>`; do not regress to `Task`.
* **Interface-first** — consumers depend on abstractions, not implementation types.

### B. Function and method design (Holub/Martin)

* A function must do one conceptual thing. If a meaningful sub-operation obscures the main flow, extract it into a named function.
* A function must be small enough to understand without losing the control flow in mental paging.
* Parameter count should normally be fewer than four. When more are required, consider whether a parameter object, richer domain type, or decomposition is the correct resolution.
* Constructor parameter count is a class responsibility signal. Many constructor dependencies usually indicate the class is doing too much or sitting at the wrong level of abstraction.
* Inheritance is a form of coupling. Flag new inheritance hierarchies where composition would serve.

### C. Behavior locality (Holub)

* Behavior must live with the structure that owns the data or invariant it manipulates.
* Never put business logic into I/O adapters: flag when business logic is placed in controllers, minimal API endpoint handlers, or HTTP presentation layers.
* Never put business logic into persistence adapters: flag when business logic or domain orchestration is placed in SQL builders, repositories, or database adapters.
* Flag when business logic is implemented in service layers that simply wrap persistence calls without adding domain rules.
* Flag when SQL logic is implemented outside the component responsible for that SQL.
* Flag "ask then act" patterns — code that retrieves state only to make decisions on it elsewhere. That decision belongs with the object that owns the state.
* Objects should expose capabilities, not data. Flag getters whose primary purpose is to allow outside code to enforce invariants that should live with the owning type. Do not flag simple data carriers, value objects, DTOs, or read models solely for exposing state.

### D. Hidden cost violations (Abrash rule — extended)

Flag methods that introduce hidden work not obvious from the call site: hidden allocations, implicit IO, hidden SQL generation, hidden blocking in async paths, or expensive LINQ pipelines.

Hidden cost violations are P1 unless they affect a known hot path or introduce blocking behavior in async code — in which case report them under P0 instead.

### E. Deterministic resource ownership

* Dispose readers and other provider resources promptly; every lease has one clear owner.
* No async leaks, no undisposed containers/readers/transactions.
* No long-lived transactions stored as fields.
* Every error path must be explicitly handled and tested — assume things will go wrong.
* Never silently ignore errors.

### F. SQL correctness and multi-dialect awareness

* Changes to dialects/SQL generation must consider: quoting rules, parameter marker rules, upsert behavior per DB, transaction/isolation semantics.
* If a change risks a specific DB family, call it out and require a targeted integration test.
* Do not represent the same SQL concept in two different ways simultaneously — duplicate dialect code paths are a correctness and maintenance hazard.
* When provider implementations expose equivalent behavior, prefer a unified core API and capability contract. A provider-specific API or code path needs a documented behavior difference that the shared abstraction cannot represent.
* Apply the behavior-precedence rule: EF's public contract comes first, the shared ADO.NET contract comes second, and database-specific behavior comes third. An upstream provider quirk is not automatically part of this library's API.
* Before adding a provider-specific API, overload, naming variant, or translation path, ask whether an existing core API, dialect component, or capability flag can express it. The default design is one coherent EF-facing contract with thin provider adapters.
* Do not force a lowest-common-denominator design. Unify genuinely common behavior, but preserve explicit extension points for real database capabilities and document why the shared contract is insufficient.
* Every provider feature needs a capability matrix covering all six providers, expected behavior, unsupported-feature reasons, and whether each difference comes from the database engine, driver, or adapter.

### G. Move errors left

* Move errors from runtime to compile time wherever possible.
* Prefer type safety, compiler checks, and static guarantees over runtime validation when feasible.
* If a design choice converts a compile-time guarantee into a runtime check, flag it.

### H. Comments

* Code must be understandable without comments. If a comment is needed, consider renaming or decomposing first.
* A comment is only justified to explain something the code is structurally incapable of expressing — a known bug workaround, a regulatory constraint, a non-obvious invariant.
* Bad comments are worse than no comments.
* Ceremonial header comments are forbidden — authorship and change history belong in source control.
* **Exception:** AI context headers are permitted where necessary to establish hot-path status or invariants that cannot be inferred from the diff alone.

---

## 6) P2 minors (nice to fix)

* Naming, local clarity, consistent style with the repo.
* Minor allocations or micro-optimizations in non-hot paths.
* Small simplifications that do not alter behavior.

---

## 7) Evidence requirements ("prove it")

### Tests

* Unit tests for behavior changes.
* Integration tests for DB-facing changes.
* Regression tests for bug fixes.
* Any change to an upsert, merge or other idempotent write needs a concurrent same-key test per affected database family, run against the real database: a single-threaded test cannot show a race.
* Tests should primarily verify observable behavior, not internal implementation. Tests that assert internal method calls or internal structure instead of observable outcomes are fragile and should be flagged.
* **Adversarial and boundary inputs are required** for any test touching string handling, identifier generation, SQL building, or type mapping.
  * Make no assumptions about input size or content. Where a developer puts `"foo"`, a user will put `"supercalifragilisticexpialidocious"` — or a 64KB string, a null, a reserved word in one or more dialects, a string containing a parameter marker, a Unicode edge case, or a value at the boundary of the underlying type.
  * Minimum bar: test with values that are longer, stranger, and more hostile than any example in the implementation.
* Test names must express intent. A well-named test is documentation that cannot go stale. `Test1` is not a test name.

### Cross-provider EF test imports

When adapting a test from EF Core, SQL Server, SQLite, Npgsql, Pomelo, DuckDB, or another provider suite:

* Treat the upstream test as the reference for the behavior and coverage. Write the test in the shared ADO.NET test harness, against the shared fixtures; borrow from its body, helpers, or expected SQL only under the rules in 4A (compatible license, attribution, never Oracle's).
* Normalize the test around the behavior it proves and give it a meaningful name. Prefer names such as `Can_insert_and_read_back_store_generated_key` or `Can_query_with_offset_without_limit` over names tied to the source provider or a private helper.
* Add a documentation comment to the test or test class naming the upstream behavior being covered and the providers that execute it. For example:

  ```csharp
  /// Covers EF Core's store-generated key round-trip behavior.
  /// Runs on: MySQL, PostgreSQL, SQL Server, flatfile, SQLite, and DuckDB.
  ```

* The comment must list the actual provider coverage. If a provider cannot run the test, state the capability reason next to its `[SKIP:<Product>]` attribution and keep the test active everywhere else.
* If multiple upstream tests are semantically equivalent, consolidate them into one shared test when their expected behavior and failure mode match. Retain separate tests only when they exercise a materially different path, such as result behavior versus dialect SQL shape.
* The PR description must identify the source suite, the duplicate search performed, the final provider matrix, and any intentionally omitted upstream cases.

### Performance

Required when:

* PR claims perf improvement.
* PR changes pooling, container execution, mapping, or dialect hot paths.

Requirements:

* BenchmarkDotNet with correct warmup.
* Cold-path and steady-state distinguished where relevant.
* Microbenchmarks must isolate the code under test.
* Delta must exceed noise floor.

### Threat note (security changes)

Required when touching: authn/authz, token handling, SQL building, deserialization, filesystem paths, logging, connection string handling.

Threat note format (5 bullets):

* Entry points
* Trust boundaries
* Assets at risk
* Attacker goals
* Mitigations and tests

---

## 8) Waivers

* **P1 majors** may only be waived when the PR author demonstrates the finding is a **false positive** — the rule does not apply given the actual context. Deferral is not a waiver; deferred concerns require a follow-up issue.
* **P0 blockers cannot be waived.**

---

## 9) DB impact notes

If SQL/dialect behavior changes, reviewers must state:

* Affected DBs (at least by family: Postgres-like, MySQL-like, SQL Server-like, embedded, warehouse)
* Expected behavior differences
* Which integration tests cover it, or what new test is required

---

## 10) Reviewer personas (how to think)

* **Boundary:** Where does control transfer? Where does ownership transfer? Is every boundary explicit and controlled?
* **Ownership:** Who owns this resource? Who releases it? Can ownership escape the scope where it is controlled? Can two components believe they own it?
* **Security:** Assume hostile inputs. Insist on invariants and tests. Silence is failure. Secrets go nowhere.
* **Runtime reality:** What does this actually do at runtime? Are there hidden allocations, blocking calls, implicit IO, or other invisible costs?
* **Behavior locality:** Does behavior live with the structure that owns the invariant? Are objects exposing capabilities or leaking data?
* **Design/Testability:** If it is hard to test, the design is wrong.
* **I/O Separation:** Is business logic mixed with I/O? Are controllers/endpoints doing more than protocol translation and delegation? Are persistence adapters doing more than SQL/data access? Does domain logic exist in a pure, easily testable layer free of transport and database primitives?
* **Clarity:** Code should read like an executable spec. One statement per line. Braces everywhere. Explicit parentheses. If you cannot say it in English, you cannot say it in code.

---

## Notes (repo-specific)

This policy aligns with the repository's mandates: TDD, integration coverage, interface-first design, multi-dialect correctness, deterministic resource ownership, safe public API evolution, and strict separation of business logic from I/O.

Influences: Abrash (measure everything, know the machine, expose hidden costs), Holub (behavior lives with the owning structure, design for testability, interfaces over concrete types), Lampson (explicit ownership, every resource has a single clear owner), Martin (small cohesive functions, single responsibility, clean contracts, dependency direction), Schneier (hostile inputs, explicit boundaries, fail loudly, secrets stay secret).

---

## 11) Enforcement Mechanisms

Compliance with this policy is enforced via:

1. **Project CI and build checks**: Run the repository's build, test, analyzer, formatting, and public-API checks before merge.
2. **Manual peer review**: Maintainers verify adherence to this policy and project-specific invariants.
3. **Public-API guards**: Public API analyzers and the project's `PublicAPI.*.txt` baselines must reject undeclared public or protected members and unexplained removals.

