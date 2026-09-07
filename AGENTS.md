# QueryToCsv — Agent Instructions

QueryToCsv is a Console application targeting `net10.0`. It connects to Microsoft SQL
Server, runs a `.sql` file or an inline query, and writes the result set to CSV.

`CLAUDE.md` at the repository root is a one-line import of this file. This file is the
repository's router — facts, commands, and reading instructions; it holds no rule text.
Rule bodies live under `docs/rules/`. Edit this file, never `CLAUDE.md`.

## Technology Stack

| Item | Detail |
|------|--------|
| Language | C# |
| Runtime | net10.0 |
| UI | CLI (no UI) |
| Database | Microsoft SQL Server via `Microsoft.Data.SqlClient` |
| CSV | CsvHelper (RFC 4180) |
| Logging | NLog, one file per day under `logs/` next to the executable, falling back to the user's local application data when that folder cannot be written to |
| Distribution | Self-contained publish + Inno Setup installer; the published file set follows from the native dependencies (`docs/rules/dotnet.md` NATIVEDEP) |

## Applications

| Application | Projects | Rules | Specification |
|---|---|---|---|
| QueryToCsv | QueryToCsv, QueryToCsv.Tests | `docs/rules/QueryToCsv.md` | `docs/specs/QueryToCsv.md` |

## Rules and AUDIT

`docs/rules/` holds the core — `standard.md`, `documentation.md`, `git.md`, `cli.md`,
`dotnet.md` — and `QueryToCsv.md`, which carries this repository's own rules and its
overrides of the core. `QueryToCsv.md` wins on conflict; within the core `dotnet.md`
wins over `standard.md`.

**Before implementing any change, read all of them, and `docs/specs/QueryToCsv.md` with
them.** QueryToCsv is a console application, so `cli.md` binds too — nothing here is
skippable. Judging any one file unrelated to the work at hand and leaving it unread is
how a rule goes unapplied.

- **When transitioning from a plan to implementation**, re-read this file (root and any nested `AGENTS.md` covering the work area) and the rules files first, so all rules are loaded before code is written
- **Before reporting an implementation task as complete**, run the AUDIT procedure at the end of `docs/rules/standard.md`
- `docs/rules/standard.md`, `docs/rules/documentation.md`, `docs/rules/git.md`, `docs/rules/cli.md`, and `docs/rules/dotnet.md` are managed by dev-standards — never edit them; repository- and application-specific rules go in `docs/rules/QueryToCsv.md`

## Commands

| Purpose | Command |
|---------|---------|
| Format | `dotnet format QueryToCsv.slnx` |
| Format check (must pass before completion) | `dotnet format QueryToCsv.slnx --verify-no-changes` |
| Build | `dotnet build QueryToCsv.slnx -c Release` |
| Test | `dotnet test QueryToCsv.Tests/QueryToCsv.Tests.csproj` |
| Full build (format gate → tests → publish) | `build/Menu.bat` (interactive) or `powershell -ExecutionPolicy Bypass -File build/Build.ps1` |
| Installer | `powershell -ExecutionPolicy Bypass -File build/Installer.ps1` |
| Release | see `docs/guides/release.md` |

## Directory Layout

### `QueryToCsv/`

The main project. Contains all application source code.

- `Program.cs` — entry point: help, version, `--open`, one-liner mode, interactive flow
- `LogSetup.cs` — NLog configuration, log-directory fallback, retention sweep
- `CliInvocation.cs` — top-level CLI mode selection and `--open` target parsing
- `CliRunArgs.cs` — command-line parsing for one-liner mode
- `ApplicationVersion.cs` — product-version resolution and display text
- `ConsoleMessages.cs` — standard warning, runtime error, and usage error output
- `AppSettings.cs` — `appsettings.json` loading, path resolution, defaults for unusable values, required-input check
- `ConsoleUi.cs` — interactive prompts and CSV encoding resolution
- `QueryExecutor.cs` — SELECT-only check, query execution, CSV writing
- `appsettings.template.json` — the template shipped as the initial `appsettings.json`; the real `appsettings.json` holds connection strings and is gitignored

### `QueryToCsv.Tests/`

xUnit test project.

### `build/`

Build scripts and installer configuration.

- `Build.ps1` runs the configuration-file check, format verification (`dotnet format --verify-no-changes`), and tests, then publishes QueryToCsv as a self-contained single-file EXE to `build/QueryToCsv/`. All three gates must pass before publish proceeds. It also stages `appsettings.json` (from `appsettings.template.json`) and the `queries/`, `output/` folders that the installer ships
- `Installer.ps1` invokes Inno Setup (ISCC.exe) on `Setup_QueryToCsv.iss`; requires `build/QueryToCsv/QueryToCsv.exe` to exist. Before running ISCC it reads `<Version>` from `Directory.Build.props`, injects it via `/DMyAppVersion`, and — if `CHANGELOG.md` exists — verifies it contains a heading for the current version (fails otherwise). The version and CHANGELOG rules are `docs/rules/dotnet.md` VERSION
- Output directories: `build/QueryToCsv/` (self-contained EXE) and `build/Installer/` (installer package), both gitignored (rules: `docs/rules/dotnet.md` OUTPUT)

### `.github/workflows/`

- `ci.yml` — builds and tests on every push to `main` and every pull request targeting it
- `release.yml` — triggers on a `v*` tag: runs `build/Build.ps1` and `build/Installer.ps1` on a runner and creates the GitHub Release with this version's `CHANGELOG.md` section as its notes. Pushing the tag is what publishes; no asset is uploaded by hand (procedure: `docs/guides/release.md`)

### `docs/`

All non-source documents, placed in role-based subfolders (`rules/`, `adr/`, `specs/`,
`guides/`, `references/`, `investigations/`, `notes/`, `plans/`, `inbox/`, `archive/`).
Before creating, changing, moving, renaming, archiving, or deleting any document — or
when unsure where one belongs — read `docs/rules/documentation.md` (also distributed
in this repository) first; it defines placement, naming, and front matter.

- `docs/rules/` — rule bodies: `standard.md`, `documentation.md`, `git.md`, `cli.md` + `dotnet.md` (managed by dev-standards) and `QueryToCsv.md` (application rules)
- `docs/specs/QueryToCsv.md` — the QueryToCsv specification: what it does
- `docs/guides/` — repository-specific procedures, including `release.md`
- `docs/adr/` — Architecture Decision Records; retired ADRs move to `docs/adr/archive/`
- `docs/plans/` and `docs/archive/plans/` — working area for plans, gitignored (`docs/rules/documentation.md`, `docs/plans/`)

### Runtime layout (installed application)

`queries/`, `output/`, and `logs/` sit next to the executable and are resolved through
`AppContext.BaseDirectory`. `QueryFolder` and `OutputFolder` in `appsettings.json` may
point elsewhere; relative values resolve against the executable's directory.
