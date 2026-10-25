# Repository Guidelines

## Project Structure & Module Organization

This is a .NET 10 solution (`pengdows.efcore.adonet.slnx`) implementing an EF Core ADO.NET abstraction. Core abstractions, SQL generation, storage, metadata, and diagnostics are in `src/pengdows.efcore.adonet/`. Provider packages live in sibling directories such as `src/pengdows.efcore.adonet.sqlserver/`, with shared conformance helpers in `src/pengdows.efcore.adonet.Testing/`. Unit, provider, specification, and functional tests are in `test/`; provider-neutral documentation and upstream comparison records are in `docs/`.

## Build, Test, and Development Commands

Use the SDK specified by `global.json` (10.0.100 or a compatible latest feature band).

```bash
dotnet restore
dotnet build
dotnet test                                      # all projects; server tests require Docker/Testcontainers
dotnet test test/pengdows.efcore.adonet.Tests    # core-only tests; no server database required
dotnet test test/pengdows.efcore.adonet.sqlserver.Tests
```

`TreatWarningsAsErrors` is enabled for source projects, so keep the build clean. Run focused tests while iterating, then run the full suite before submitting changes.

## Coding Style & Naming Conventions

Use standard C# style: four-space indentation, nullable reference types enabled, implicit usings, and clear PascalCase public types/methods with camelCase locals and parameters. Keep provider-specific behavior in its provider project and shared behavior in core. Follow existing `AdoNet*`, `*Dialect`, and `*DbContextOptionsBuilderExtensions` naming patterns. There is no separate formatter configuration; rely on idiomatic `dotnet format` output and the surrounding code. Update the relevant `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` whenever public or protected API changes, and document the rationale in `docs/PUBLIC_API.md` when appropriate.

## Testing Guidelines

Tests use xUnit. Name test classes and methods for the behavior under test (for example, `PostgreSqlSqlTests` and `*Tests`). New providers should add a fixture and conformance-suite subclasses following the existing provider test projects. Record genuine engine limitations as `KnownEngineGaps`; do not hide regressions with broad skips. Server-backed tests need Docker and Testcontainers; core tests use fakeDb and in-memory SQLite.

## Commit & Pull Request Guidelines

Recent commits use concise, imperative or descriptive subjects, often naming the suite and provider (for example, `Base Where suite: 408 of 408 on MySQL too`). Keep commits focused. Pull requests should explain the behavior change, list affected providers, include the exact build/test commands and results, call out known gaps or API changes, and link relevant issues or upstream baselines. Do not overwrite unrelated working-tree changes.

## Security & Configuration Tips

Do not commit credentials, connection strings containing secrets, or generated artifacts. Use environment variables and local Docker/Testcontainers configuration for database tests. Review provider-specific connection and migration changes carefully, especially SQL that interpolates identifiers or controls bulk loading.
